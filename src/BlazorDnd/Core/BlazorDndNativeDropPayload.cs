namespace BlazorDnd;

/// <summary>Raw payload shape received from JS for a native drop.</summary>
public sealed class BlazorDndNativeDropPayload
{
    public string[] Types { get; set; } = Array.Empty<string>();
    public Dictionary<string, string> Data { get; set; } = new();
    public BlazorDndNativeFile[] Files { get; set; } = Array.Empty<BlazorDndNativeFile>();
}
