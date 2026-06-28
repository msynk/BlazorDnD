namespace BlazorDnd;

/// <summary>
/// State surfaced to a <see cref="BlazorDndDragLayer{TItem}"/>'s child content, equivalent to the
/// values exposed by React DnD's <c>useDragLayer</c> / <c>DragLayerMonitor</c>.
/// </summary>
public sealed class BlazorDndDragLayerState<TItem>
{
    public required BlazorDndIDndMonitor Monitor { get; init; }
    public bool IsDragging { get; init; }
    public string? ItemType { get; init; }
    public TItem? Item { get; init; }
    public BlazorDndXYCoord? InitialClientOffset { get; init; }
    public BlazorDndXYCoord? InitialSourceClientOffset { get; init; }
    public BlazorDndXYCoord? ClientOffset { get; init; }
    public BlazorDndXYCoord? DifferenceFromInitialOffset { get; init; }
    public BlazorDndXYCoord? SourceClientOffset { get; init; }
}
