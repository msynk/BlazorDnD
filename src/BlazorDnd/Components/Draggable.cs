using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace BlazorDnd;

/// <summary>
/// Wires its host element into the DnD system as a drag source.
/// This is the component equivalent of React DnD's <c>useDrag</c> hook: you declare a
/// <see cref="Type"/> and an <see cref="Item"/> (the data), and the child content receives
/// a <see cref="DragState"/> with the collected props (e.g. <c>IsDragging</c>).
/// </summary>
/// <typeparam name="TItem">The type of the dragged item payload.</typeparam>
public sealed class Draggable<TItem> : ComponentBase, IAsyncDisposable
{
    [CascadingParameter] private DragDropManager Manager { get; set; } = default!;
    [Inject] private DndInterop Interop { get; set; } = default!;

    /// <summary>Required. The item type used to match against drop-target <c>Accept</c> lists.</summary>
    [Parameter, EditorRequired] public string Type { get; set; } = default!;

    /// <summary>The item payload. Ignored when <see cref="ItemFactory"/> is supplied.</summary>
    [Parameter] public TItem? Item { get; set; }

    /// <summary>Produces the item lazily at drag start. Return <c>default</c> to cancel the drag.</summary>
    [Parameter] public Func<TItem?>? ItemFactory { get; set; }

    /// <summary>Whether dragging is allowed. Defaults to true.</summary>
    [Parameter] public bool CanDrag { get; set; } = true;

    /// <summary>Optional predicate overriding <see cref="CanDrag"/>, evaluated at render time.</summary>
    [Parameter] public Func<bool>? CanDragPredicate { get; set; }

    /// <summary>Custom <c>isDragging</c> implementation (e.g. id-based for re-parented items).</summary>
    [Parameter] public Func<IDndMonitor, bool>? IsDraggingOverride { get; set; }

    /// <summary>Hide the native drag image (use together with a <see cref="DragLayer{TItem}"/>).</summary>
    [Parameter] public bool UseCustomDragLayer { get; set; }

    /// <summary>Require a <see cref="DragHandle"/> child to initiate the drag.</summary>
    [Parameter] public bool UseHandle { get; set; }

    /// <summary>The drop effect / cursor hint: <c>move</c> (default) or <c>copy</c>.</summary>
    [Parameter] public string DropEffect { get; set; } = "move";

    /// <summary>Fired when the drag begins.</summary>
    [Parameter] public EventCallback<TItem?> OnBegin { get; set; }

    /// <summary>Fired when the drag ends (drop or cancel).</summary>
    [Parameter] public EventCallback<DragEndContext<TItem>> OnEnd { get; set; }

    /// <summary>Host element tag name. Defaults to <c>div</c>.</summary>
    [Parameter] public string Tag { get; set; } = "div";

    [Parameter] public RenderFragment<DragState>? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private readonly string _handlerId;
    private ElementReference _element;
    private bool _handleArmed;
    private IJSObjectReferenceWrapper? _emptyPreview;
    private IJSObjectReferenceWrapper? _dragConfig;
    private TItem? _draggingItem;
    private readonly HandleController _handleController;

    public Draggable()
    {
        _handlerId = Guid.NewGuid().ToString("N");
        _handleController = new HandleController(this);
    }

    protected override void OnInitialized() => Manager.StateChanged += OnStateChanged;

    private bool EffectiveCanDrag => CanDragPredicate?.Invoke() ?? CanDrag;

    private bool DraggableAttr => EffectiveCanDrag && (!UseHandle || _handleArmed);

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var monitor = Manager.Monitor;
        var isDragging = IsDraggingOverride is not null
            ? IsDraggingOverride(monitor)
            : monitor.IsDragging(_handlerId);

        var state = new DragState
        {
            Monitor = monitor,
            IsDragging = isDragging,
            CanDrag = EffectiveCanDrag,
        };

        builder.OpenElement(0, Tag);
        if (AdditionalAttributes is not null)
            builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "draggable", DraggableAttr ? "true" : "false");
        builder.AddAttribute(3, "ondragstart",
            EventCallback.Factory.Create<DragEventArgs>(this, HandleDragStartAsync));
        // stop nested (ancestor) draggables from also starting a drag
        builder.AddEventStopPropagationAttribute(4, "ondragstart", true);
        builder.AddAttribute(5, "ondrag",
            EventCallback.Factory.Create<DragEventArgs>(this, HandleDrag));
        builder.AddAttribute(6, "ondragend",
            EventCallback.Factory.Create<DragEventArgs>(this, HandleDragEndAsync));
        builder.AddElementReferenceCapture(7, el => _element = el);
        // expose handle wiring to descendant DragHandle components
        builder.OpenComponent<CascadingValue<IDragHandleController>>(8);
        builder.AddAttribute(9, "Value", _handleController);
        builder.AddAttribute(10, "IsFixed", true);
        builder.AddAttribute(11, "ChildContent", (RenderFragment)(b =>
        {
            b.AddContent(0, ChildContent?.Invoke(state));
        }));
        builder.CloseComponent();
        builder.CloseElement();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            if (UseCustomDragLayer)
                _emptyPreview = new IJSObjectReferenceWrapper(
                    await Interop.UseEmptyDragPreviewAsync(_element));
            _dragConfig = new IJSObjectReferenceWrapper(
                await Interop.ConfigureDragSourceAsync(_element, DropEffect));
        }
    }

    private async Task HandleDragStartAsync(DragEventArgs e)
    {
        if (!EffectiveCanDrag) return;

        _draggingItem = ItemFactory is not null ? ItemFactory() : Item;
        if (ItemFactory is not null && _draggingItem is null) return; // cancelled

        var clientOffset = new XYCoord(e.ClientX, e.ClientY);

        // Begin synchronously (provisional source offset = pointer) so drop targets see the
        // item immediately and don't miss the first dragenter, then patch the real source
        // offset once the async bounding-rect lookup resolves.
        Manager.BeginDrag(_handlerId, _draggingItem, Type, clientOffset, clientOffset);

        if (OnBegin.HasDelegate) await OnBegin.InvokeAsync(_draggingItem);

        var rect = await Interop.GetBoundingClientRectAsync(_element);
        if (rect is not null)
            Manager.SetInitialSourceClientOffset(new XYCoord(rect.Left, rect.Top));
    }

    private void HandleDrag(DragEventArgs e)
    {
        // The final 'drag' event often reports (0,0); ignore it to avoid a jump.
        if (e.ClientX == 0 && e.ClientY == 0) return;
        Manager.SetClientOffset(new XYCoord(e.ClientX, e.ClientY));
    }

    private async Task HandleDragEndAsync(DragEventArgs e)
    {
        _handleArmed = false;
        if (Manager.Monitor.IsDragging(_handlerId))
        {
            if (OnEnd.HasDelegate)
            {
                await OnEnd.InvokeAsync(new DragEndContext<TItem>
                {
                    Monitor = Manager.Monitor,
                    Item = _draggingItem,
                    DidDrop = Manager.Monitor.DidDrop(),
                    DropResult = Manager.Monitor.GetDropResult(),
                });
            }
            Manager.EndDrag();
            Manager.ResetDropState();
        }
        _draggingItem = default;
    }

    internal void ArmHandle() { _handleArmed = true; StateHasChanged(); }

    internal void DisarmHandle() { if (_handleArmed) { _handleArmed = false; StateHasChanged(); } }

    public async ValueTask DisposeAsync()
    {
        Manager.StateChanged -= OnStateChanged;
        if (_emptyPreview is not null) await _emptyPreview.DisposeAsync();
        if (_dragConfig is not null) await _dragConfig.DisposeAsync();
    }

    private sealed class HandleController : IDragHandleController
    {
        private readonly Draggable<TItem> _owner;
        public HandleController(Draggable<TItem> owner) => _owner = owner;
        public bool RequiresHandle => _owner.UseHandle;
        public void Arm() => _owner.ArmHandle();
        public void Disarm() => _owner.DisarmHandle();
    }
}

/// <summary>Non-generic seam so <see cref="DragHandle"/> can control any draggable.</summary>
public interface IDragHandleController
{
    bool RequiresHandle { get; }
    void Arm();
    void Disarm();
}

/// <summary>Wraps an IJSObjectReference's disposal in an exception-safe ValueTask.</summary>
internal sealed class IJSObjectReferenceWrapper : IAsyncDisposable
{
    private readonly IJSObjectReference _reference;
    public IJSObjectReferenceWrapper(IJSObjectReference reference) => _reference = reference;

    public async ValueTask DisposeAsync()
    {
        try { await _reference.InvokeVoidAsync("dispose"); }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) { }
        finally
        {
            try { await _reference.DisposeAsync(); } catch { /* ignore */ }
        }
    }
}
