namespace BlazorDnd;

/// <summary>A single file described by a native file drop (metadata only; no contents).</summary>
public sealed class BlazorDndNativeFile
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public string Type { get; set; } = "";
}
