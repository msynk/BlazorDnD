namespace BlazorDnd;

/// <summary>Non-generic seam so <see cref="BlazorDndDragHandle"/> can control any draggable.</summary>
public interface BlazorDndIDragHandleController
{
    bool RequiresHandle { get; }
    void Arm();
    void Disarm();
}
