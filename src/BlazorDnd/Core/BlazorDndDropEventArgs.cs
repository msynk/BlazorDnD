namespace BlazorDnd;

/// <summary>
/// Passed to <see cref="BlazorDndDropTarget{TItem}.OnDrop"/> and <see cref="BlazorDndDropTarget{TItem}.OnHover"/>,
/// mirroring React DnD's <c>drop(item, monitor)</c> / <c>hover(item, monitor)</c> callbacks.
/// </summary>
public sealed class BlazorDndDropEventArgs<TItem>
{
    public required BlazorDndIDndMonitor Monitor { get; init; }

    /// <summary>The item currently being dragged.</summary>
    public TItem? Item { get; init; }

    /// <summary>
    /// Set this inside an <c>OnDrop</c> handler to publish a drop result. It becomes available
    /// to the drag source via <c>monitor.GetDropResult()</c> in its <c>OnEnd</c> callback.
    /// </summary>
    public object? Result { get; set; }
}
