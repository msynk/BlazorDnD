namespace BlazorDnd;

/// <summary>Internal registration record for a drop target.</summary>
internal sealed class BlazorDndDropTargetEntry
{
    public required string Id { get; init; }

    /// <summary>The id of the nearest enclosing drop target of the same manager, if any.</summary>
    public string? ParentId { get; init; }

    public string[] Accept { get; set; } = Array.Empty<string>();
    public Func<object?, BlazorDndIDndMonitor, bool>? CanDrop { get; set; }

    /// <summary>True while the pointer is over the target and the target accepts the item type.</summary>
    public bool IsOver { get; set; }
}
