namespace BlazorDnd;

/// <summary>
/// Passed to <see cref="Draggable{TItem}.OnEnd"/> when a drag operation finishes,
/// mirroring the arguments of React DnD's <c>end(item, monitor)</c> callback.
/// </summary>
public sealed class DragEndContext<TItem>
{
    public required IDndMonitor Monitor { get; init; }

    /// <summary>The item that was being dragged.</summary>
    public TItem? Item { get; init; }

    /// <summary>True if a compatible drop target handled the drop.</summary>
    public bool DidDrop { get; init; }

    /// <summary>The drop result returned by the handling target, if any.</summary>
    public object? DropResult { get; init; }
}
