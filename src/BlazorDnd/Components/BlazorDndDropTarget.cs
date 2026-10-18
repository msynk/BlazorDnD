using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace BlazorDnd;

/// <summary>
/// Wires its host element into the DnD system as a drop target (React DnD's <c>useDrop</c>).
/// Besides in-app drags it can also accept native OS drags (files, text, urls, html) by
/// listing one of the <see cref="BlazorDndNativeTypes"/> in <see cref="Accept"/>/<see cref="AcceptTypes"/>.
/// </summary>
/// <typeparam name="TItem">The expected dragged item type.</typeparam>
public sealed class BlazorDndDropTarget<TItem> : ComponentBase, IHandleEvent, IAsyncDisposable
{
    [CascadingParameter] private BlazorDndDragDropManager? Manager { get; set; }
    [CascadingParameter] private BlazorDndDropTargetScope? ParentScope { get; set; }
    [Inject] private BlazorDndDndInterop Interop { get; set; } = default!;

    /// <summary>A single accepted item type.</summary>
    [Parameter] public string? Accept { get; set; }

    /// <summary>Multiple accepted item types (combined with <see cref="Accept"/>).</summary>
    [Parameter] public IEnumerable<string>? AcceptTypes { get; set; }

    /// <summary>Called when a compatible in-app item is dropped. Set <c>args.Result</c> to publish a result.</summary>
    [Parameter] public EventCallback<BlazorDndDropEventArgs<TItem>> OnDrop { get; set; }

    /// <summary>Called repeatedly while a compatible in-app item is hovered.</summary>
    [Parameter] public EventCallback<BlazorDndDropEventArgs<TItem>> OnHover { get; set; }

    /// <summary>Called when a native OS drag (file/text/url/html) is dropped.</summary>
    [Parameter] public EventCallback<BlazorDndNativeDropEventArgs> OnNativeDrop { get; set; }

    /// <summary>Predicate gating whether this target can accept the current in-app item.</summary>
    [Parameter] public Func<TItem?, BlazorDndIDndMonitor, bool>? CanDropPredicate { get; set; }

    /// <summary>Host element tag name. Defaults to <c>div</c>.</summary>
    [Parameter] public string Tag { get; set; } = "div";

    [Parameter] public RenderFragment<BlazorDndDropState<TItem>>? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private readonly string _handlerId;
    private BlazorDndDropTargetScope _scope = default!;
    private string[] _accept = Array.Empty<string>();
    private (string Token, string NativeType)[] _nativeAccepts = Array.Empty<(string, string)>();
    private string[] _nativeTokens = Array.Empty<string>();
    private ElementReference _element;
    private BlazorDndJSRegistration<BlazorDndDropTargetOptions>? _registration;
    private DotNetObjectReference<BlazorDndDropTarget<TItem>>? _selfRef;
    private bool _canDropInApp;
    private bool _disposed;

    // native (OS) drag local state — the manager only tracks in-app drags
    private string? _nativeItemType;

    public BlazorDndDropTarget()
    {
        _handlerId = Guid.NewGuid().ToString("N");
    }

    protected override void OnInitialized()
    {
        if (Manager is null)
            throw new InvalidOperationException(
                $"{nameof(BlazorDndDropTarget<TItem>)} must be placed inside a {nameof(BlazorDndDndProvider)}.");
        _scope = new BlazorDndDropTargetScope(Manager, _handlerId, ParentScope);
        _registration = new(RegisterAsync);
        Manager.StateChanged += OnStateChanged;
    }

    protected override void OnParametersSet()
    {
        var list = new List<string>();
        if (!string.IsNullOrEmpty(Accept)) list.Add(Accept);
        if (AcceptTypes is not null) list.AddRange(AcceptTypes.Where(t => !string.IsNullOrEmpty(t)));
        _accept = list.Distinct().ToArray();
        _nativeAccepts = _accept
            .Where(BlazorDndNativeTypes.IsNative)
            .Select(t => (BlazorDndNativeTypes.ToDataTransferType(t), t))
            .ToArray();
        var tokens = _nativeAccepts.Select(na => na.Token).ToArray();
        if (!tokens.SequenceEqual(_nativeTokens)) _nativeTokens = tokens;

        var predicate = CanDropPredicate;
        Manager!.RegisterTarget(new BlazorDndDropTargetEntry
        {
            Id = _handlerId,
            ParentId = ParentScope?.FindTargetId(Manager),
            Accept = _accept,
            CanDrop = predicate is null
                ? null
                : (item, monitor) => predicate(Cast(item), monitor),
        });
    }

    protected override Task OnAfterRenderAsync(bool firstRender) =>
        _registration!.SyncAsync(_element, new BlazorDndDropTargetOptions(_canDropInApp, _nativeTokens));

    private ValueTask<IJSObjectReference?> RegisterAsync(ElementReference element, BlazorDndDropTargetOptions options)
    {
        _selfRef ??= DotNetObjectReference.Create(this);
        return Interop.RegisterDropTargetAsync(element, _selfRef, options);
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    // Drag events must not trigger the automatic re-render ComponentBase performs after every
    // event (dragover fires continuously); the manager and the native hover state drive renders.
    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg) => callback.InvokeAsync(arg);

    private static TItem? Cast(object? item) => item is TItem t ? t : default;

    private bool Accepts() =>
        Manager!.Monitor.GetItemType() is { } type && _accept.Contains(type);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var monitor = Manager!.Monitor;
        var native = !monitor.IsDragging() && _nativeItemType is not null;
        _canDropInApp = monitor.CanDrop(_handlerId);

        var state = new BlazorDndDropState<TItem>
        {
            Monitor = monitor,
            IsOver = native || monitor.IsOver(_handlerId),
            IsOverShallow = native || monitor.IsOver(_handlerId, shallow: true),
            CanDrop = native || _canDropInApp,
            ItemType = native ? _nativeItemType : monitor.GetItemType(),
            Item = Cast(monitor.GetItem()),
        };

        // Hover tracking, drop-effect feedback (preventDefault/dropEffect) and native drops are
        // handled by the JS registration; the managed handlers cover in-app hover and drops.
        builder.OpenElement(0, Tag);
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        if (OnHover.HasDelegate)
        {
            builder.AddAttribute(2, "ondragover",
                EventCallback.Factory.Create<DragEventArgs>(this, HandleDragOverAsync));
        }
        builder.AddAttribute(3, "ondrop",
            EventCallback.Factory.Create<DragEventArgs>(this, HandleDropAsync));
        builder.AddElementReferenceCapture(4, el => _element = el);
        // lets nested drop targets find their parent target (shallow IsOver)
        builder.OpenComponent<CascadingValue<BlazorDndDropTargetScope>>(5);
        builder.AddAttribute(6, "Value", _scope);
        builder.AddAttribute(7, "IsFixed", true);
        builder.AddAttribute(8, "ChildContent", (RenderFragment)(b =>
        {
            b.AddContent(0, ChildContent?.Invoke(state));
        }));
        builder.CloseComponent();
        builder.CloseElement();
    }

    private string? MatchNative(IReadOnlyCollection<string>? types)
    {
        if (types is null) return null;
        foreach (var (token, nativeType) in _nativeAccepts)
            if (types.Contains(token))
                return nativeType;
        return null;
    }

    // Drags started by a BlazorDnd draggable of any provider (or document) are never native.
    private static bool IsInAppDrag(IEnumerable<string>? types) =>
        types?.Any(t => t.StartsWith(BlazorDndDndInterop.SourceTypePrefix, StringComparison.Ordinal)) == true;

    private void SetNativeItemType(string? nativeType)
    {
        if (_nativeItemType == nativeType) return;
        _nativeItemType = nativeType;
        StateHasChanged();
    }

    private async Task HandleDragOverAsync(DragEventArgs e)
    {
        var manager = Manager!;
        if (!manager.IsDragging() || !Accepts()) return;
        await manager.FlushBeginAsync();
        manager.SetClientOffset(new BlazorDndXYCoord(e.ClientX, e.ClientY));
        await OnHover.InvokeAsync(new BlazorDndDropEventArgs<TItem>
        {
            Monitor = manager.Monitor,
            Item = Cast(manager.GetItem()),
        });
    }

    private async Task HandleDropAsync(DragEventArgs e)
    {
        var manager = Manager!;
        if (!manager.IsDragging()) return; // native drops are handled in JS
        await manager.FlushBeginAsync();
        if (!Accepts() || !manager.CanDrop(_handlerId)) return;

        // the drop event reports the exact final pointer position
        manager.SetClientOffset(new BlazorDndXYCoord(e.ClientX, e.ClientY));
        var dragId = manager.DragId;
        var args = new BlazorDndDropEventArgs<TItem>
        {
            Monitor = manager.Monitor,
            Item = Cast(manager.GetItem()),
        };
        var onDrop = OnDrop.InvokeAsync(args);
        // An async handler may still run when the source's dragend arrives: mark the drop as
        // handled right away so OnEnd sees DidDrop; the result is recorded once it completes.
        if (!onDrop.IsCompleted) manager.RecordDrop(dragId, null);
        await onDrop;
        manager.RecordDrop(dragId, args.Result);
    }

    /// <summary>Invoked from JS when the pointer enters or leaves this target during any drag.</summary>
    [JSInvokable, EditorBrowsable(EditorBrowsableState.Never)]
    public async Task HandleHoverChanged(bool isOver, string[]? types)
    {
        if (_disposed) return;
        var manager = Manager!;
        if (manager.IsDragging())
        {
            await manager.FlushBeginAsync();
            SetNativeItemType(null);
            manager.SetTargetOver(_handlerId, isOver && Accepts());
            return;
        }
        // external/native drag
        SetNativeItemType(isOver && !IsInAppDrag(types) ? MatchNative(types) : null);
    }

    /// <summary>Invoked from JS when a native OS drop occurs on this target.</summary>
    [JSInvokable, EditorBrowsable(EditorBrowsableState.Never)]
    public async Task HandleNativeDrop(BlazorDndNativeDropPayload payload)
    {
        if (_disposed) return;
        SetNativeItemType(null);
        if (Manager!.IsDragging() || IsInAppDrag(payload.Types)) return;
        var nativeType = MatchNative(payload.Types);
        if (nativeType is null) return;

        await OnNativeDrop.InvokeAsync(new BlazorDndNativeDropEventArgs
        {
            ItemType = nativeType,
            Files = payload.Files ?? Array.Empty<BlazorDndNativeFile>(),
            Data = payload.Data ?? new Dictionary<string, string>(),
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (Manager is not null)
        {
            Manager.StateChanged -= OnStateChanged;
            Manager.UnregisterTarget(_handlerId);
        }
        if (_registration is not null) await _registration.DisposeAsync();
        _selfRef?.Dispose();
    }
}
