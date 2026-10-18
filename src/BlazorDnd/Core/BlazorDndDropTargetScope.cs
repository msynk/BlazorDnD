namespace BlazorDnd;

/// <summary>
/// Cascaded by every <see cref="BlazorDndDropTarget{TItem}"/> to its content so nested drop
/// targets know their enclosing target (needed for shallow <c>IsOver</c>).
/// </summary>
internal sealed class BlazorDndDropTargetScope
{
    public BlazorDndDropTargetScope(BlazorDndDragDropManager manager, string handlerId, BlazorDndDropTargetScope? parent)
    {
        Manager = manager;
        HandlerId = handlerId;
        Parent = parent;
    }

    public BlazorDndDragDropManager Manager { get; }
    public string HandlerId { get; }
    public BlazorDndDropTargetScope? Parent { get; }

    /// <summary>
    /// The handler id of the nearest target in this scope chain (this one included) that belongs
    /// to <paramref name="manager"/>; targets of nested providers are skipped.
    /// </summary>
    public string? FindTargetId(BlazorDndDragDropManager manager)
    {
        for (var scope = this; scope is not null; scope = scope.Parent)
        {
            if (ReferenceEquals(scope.Manager, manager)) return scope.HandlerId;
        }
        return null;
    }
}
