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
public sealed class BlazorDndDropTarget<TItem> : ComponentBase, IAsyncDisposable
{
    [CascadingParameter] private BlazorDndDragDropManager Manager { get; set; } = default!;
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
    private string[] _accept = Array.Empty<string>();
    private (string Token, string NativeType)[] _nativeAccepts = Array.Empty<(string, string)>();
    private ElementReference _element;

    // native (OS) drag local state — the manager only tracks in-app drags
    private int _nativeEnterCount;
    private string? _nativeItemType;
    private DotNetObjectReference<BlazorDndDropTarget<TItem>>? _selfRef;
    private BlazorDndIJSObjectReferenceWrapper? _nativeReg;

    public BlazorDndDropTarget()
    {
        _handlerId = Guid.NewGuid().ToString("N");
    }

    protected override void OnInitialized() => Manager.StateChanged += OnStateChanged;

    protected override void OnParametersSet()
    {
        var list = new List<string>();
        if (!string.IsNullOrEmpty(Accept)) list.Add(Accept);
        if (AcceptTypes is not null) list.AddRange(AcceptTypes);
        _accept = list.Distinct().ToArray();
        _nativeAccepts = _accept
            .Where(BlazorDndNativeTypes.IsNative)
            .Select(t => (BlazorDndNativeTypes.ToDataTransferType(t), t))
            .ToArray();

        Manager.RegisterTarget(new BlazorDndDropTargetEntry
        {
            Id = _handlerId,
            Accept = _accept,
            CanDrop = CanDropPredicate is null
                ? null
                : (item, monitor) => CanDropPredicate(Cast(item), monitor),
        });
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _nativeAccepts.Length > 0)
        {
            _selfRef = DotNetObjectReference.Create(this);
            _nativeReg = new BlazorDndIJSObjectReferenceWrapper(
                await Interop.RegisterNativeDropAsync(_element, _selfRef));
        }
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    private static TItem? Cast(object? item) => item is TItem t ? t : default;

    private bool Accepts() =>
        Manager.Monitor.GetItemType() is { } type && _accept.Contains(type);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var monitor = Manager.Monitor;
        var native = !monitor.IsDragging() && _nativeOver;

        var state = new BlazorDndDropState<TItem>
        {
            Monitor = monitor,
            IsOver = native || monitor.IsOver(_handlerId),
            IsOverShallow = native || monitor.IsOver(_handlerId, shallow: true),
            CanDrop = native || monitor.CanDrop(_handlerId),
            ItemType = native ? _nativeItemType : monitor.GetItemType(),
            Item = Cast(monitor.GetItem()),
        };

        builder.OpenElement(0, Tag);
        if (AdditionalAttributes is not null)
            builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "ondragenter",
            EventCallback.Factory.Create<DragEventArgs>(this, HandleDragEnter));
        builder.AddEventPreventDefaultAttribute(3, "ondragenter", true);
        builder.AddAttribute(4, "ondragover",
            EventCallback.Factory.Create<DragEventArgs>(this, HandleDragOver));
        builder.AddEventPreventDefaultAttribute(5, "ondragover", true);
        builder.AddAttribute(6, "ondragleave",
            EventCallback.Factory.Create<DragEventArgs>(this, HandleDragLeave));
        builder.AddAttribute(7, "ondrop",
            EventCallback.Factory.Create<DragEventArgs>(this, HandleDropAsync));
        builder.AddEventPreventDefaultAttribute(8, "ondrop", true);
        builder.AddElementReferenceCapture(9, el => _element = el);
        builder.AddContent(10, ChildContent?.Invoke(state));
        builder.CloseElement();
    }

    private bool _nativeOver => _nativeEnterCount > 0;

    private string? MatchNative(DataTransfer? dt)
    {
        if (dt?.Types is null) return null;
        foreach (var (token, nativeType) in _nativeAccepts)
            if (dt.Types.Contains(token))
                return nativeType;
        return null;
    }

    private void HandleDragEnter(DragEventArgs e)
    {
        if (Manager.Monitor.IsDragging())
        {
            if (Accepts()) Manager.TargetEnter(_handlerId);
            return;
        }
        // external/native drag
        var matched = MatchNative(e.DataTransfer);
        if (matched is not null)
        {
            _nativeEnterCount++;
            _nativeItemType = matched;
            StateHasChanged();
        }
    }

    private async Task HandleDragOver(DragEventArgs e)
    {
        if (!Manager.Monitor.IsDragging()) return; // native handled by JS dragover
        if (!Accepts()) return;
        Manager.SetClientOffset(new BlazorDndXYCoord(e.ClientX, e.ClientY));
        if (OnHover.HasDelegate)
        {
            await OnHover.InvokeAsync(new BlazorDndDropEventArgs<TItem>
            {
                Monitor = Manager.Monitor,
                Item = Cast(Manager.Monitor.GetItem()),
            });
        }
    }

    private void HandleDragLeave(DragEventArgs e)
    {
        if (Manager.Monitor.IsDragging())
        {
            if (Accepts()) Manager.TargetLeave(_handlerId);
            return;
        }
        if (_nativeEnterCount > 0) _nativeEnterCount--;
        if (_nativeEnterCount == 0)
        {
            _nativeItemType = null;
            StateHasChanged();
        }
    }

    private async Task HandleDropAsync(DragEventArgs e)
    {
        if (!Manager.Monitor.IsDragging()) return; // native drop handled by JS
        if (!Accepts() || !Manager.Monitor.CanDrop(_handlerId)) return;

        var args = new BlazorDndDropEventArgs<TItem>
        {
            Monitor = Manager.Monitor,
            Item = Cast(Manager.Monitor.GetItem()),
        };
        if (OnDrop.HasDelegate) await OnDrop.InvokeAsync(args);
        Manager.RecordDrop(args.Result);
    }

    /// <summary>Invoked from JS when a native OS drop occurs on this target.</summary>
    [JSInvokable]
    public async Task HandleNativeDrop(BlazorDndNativeDropPayload payload)
    {
        _nativeEnterCount = 0;
        var nativeType = _nativeAccepts
            .Where(na => payload.Types.Contains(na.Token))
            .Select(na => na.NativeType)
            .FirstOrDefault();
        if (nativeType is null) { StateHasChanged(); return; }

        if (OnNativeDrop.HasDelegate)
        {
            await OnNativeDrop.InvokeAsync(new BlazorDndNativeDropEventArgs
            {
                ItemType = nativeType,
                Files = payload.Files,
                Data = payload.Data,
            });
        }
        _nativeItemType = null;
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        Manager.StateChanged -= OnStateChanged;
        Manager.UnregisterTarget(_handlerId);
        if (_nativeReg is not null) await _nativeReg.DisposeAsync();
        _selfRef?.Dispose();
    }
}
