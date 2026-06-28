namespace BlazorDnd;

/// <summary>Internal registration record for a drop target.</summary>
internal sealed class BlazorDndDropTargetEntry
{
    public required string Id { get; init; }
    public string[] Accept { get; set; } = Array.Empty<string>();
    public Func<object?, BlazorDndIDndMonitor, bool>? CanDrop { get; set; }
    public int EnterCount { get; set; }
}
