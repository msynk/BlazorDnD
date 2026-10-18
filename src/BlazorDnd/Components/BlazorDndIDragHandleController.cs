namespace BlazorDnd;

/// <summary>Non-generic seam so <see cref="BlazorDndDragHandle"/> can control any draggable.</summary>
public interface BlazorDndIDragHandleController
{
    /// <summary>True when the owning draggable only starts drags from a <see cref="BlazorDndDragHandle"/>.</summary>
    bool RequiresHandle { get; }

    /// <summary>True when the owning draggable currently allows dragging.</summary>
    bool CanDrag { get; }
}
