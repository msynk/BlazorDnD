namespace BlazorDnd;

/// <summary>
/// State surfaced to a <see cref="Draggable{TItem}"/>'s child content, equivalent to the
/// "collected props" returned by React DnD's <c>useDrag</c>.
/// </summary>
public sealed class DragState
{
    public required IDndMonitor Monitor { get; init; }

    /// <summary>True while this source is being dragged.</summary>
    public bool IsDragging { get; init; }

    /// <summary>True if dragging is currently allowed for this source.</summary>
    public bool CanDrag { get; init; }
}

/// <summary>
/// State surfaced to a <see cref="DropTarget{TItem}"/>'s child content, equivalent to the
/// "collected props" returned by React DnD's <c>useDrop</c>.
/// </summary>
/// <typeparam name="TItem">The expected dragged item type.</typeparam>
public sealed class DropState<TItem>
{
    public required IDndMonitor Monitor { get; init; }

    /// <summary>True if a drag is in progress and the pointer is over this target (or a child).</summary>
    public bool IsOver { get; init; }

    /// <summary>True if the pointer is over this target but not a nested target.</summary>
    public bool IsOverShallow { get; init; }

    /// <summary>True if this target can accept the item currently being dragged.</summary>
    public bool CanDrop { get; init; }

    /// <summary>The type of the item currently being dragged, or null.</summary>
    public string? ItemType { get; init; }

    /// <summary>The item currently being dragged (if assignable to <typeparamref name="TItem"/>).</summary>
    public TItem? Item { get; init; }
}

/// <summary>
/// State surfaced to a <see cref="DragLayer{TItem}"/>'s child content, equivalent to the
/// values exposed by React DnD's <c>useDragLayer</c> / <c>DragLayerMonitor</c>.
/// </summary>
public sealed class DragLayerState<TItem>
{
    public required IDndMonitor Monitor { get; init; }
    public bool IsDragging { get; init; }
    public string? ItemType { get; init; }
    public TItem? Item { get; init; }
    public XYCoord? InitialClientOffset { get; init; }
    public XYCoord? InitialSourceClientOffset { get; init; }
    public XYCoord? ClientOffset { get; init; }
    public XYCoord? DifferenceFromInitialOffset { get; init; }
    public XYCoord? SourceClientOffset { get; init; }
}
