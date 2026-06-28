namespace BlazorDnd;

/// <summary>
/// Passed to <see cref="BlazorDndDraggable{TItem}.OnEnd"/> when a drag operation finishes,
/// mirroring the arguments of React DnD's <c>end(item, monitor)</c> callback.
/// </summary>
public sealed class BlazorDndDragEndContext<TItem>
{
    public required BlazorDndIDndMonitor Monitor { get; init; }

    /// <summary>The item that was being dragged.</summary>
    public TItem? Item { get; init; }

    /// <summary>True if a compatible drop target handled the drop.</summary>
    public bool DidDrop { get; init; }

    /// <summary>The drop result returned by the handling target, if any.</summary>
    public object? DropResult { get; init; }
}
