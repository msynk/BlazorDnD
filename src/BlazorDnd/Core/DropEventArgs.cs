namespace BlazorDnd;

/// <summary>
/// Passed to <see cref="DropTarget{TItem}.OnDrop"/> and <see cref="DropTarget{TItem}.OnHover"/>,
/// mirroring React DnD's <c>drop(item, monitor)</c> / <c>hover(item, monitor)</c> callbacks.
/// </summary>
public sealed class DropEventArgs<TItem>
{
    public required IDndMonitor Monitor { get; init; }

    /// <summary>The item currently being dragged.</summary>
    public TItem? Item { get; init; }

    /// <summary>
    /// Set this inside an <c>OnDrop</c> handler to publish a drop result. It becomes available
    /// to the drag source via <c>monitor.GetDropResult()</c> in its <c>OnEnd</c> callback.
    /// </summary>
    public object? Result { get; set; }
}
