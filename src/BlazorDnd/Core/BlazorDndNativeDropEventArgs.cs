namespace BlazorDnd;

/// <summary>Payload delivered to <see cref="BlazorDndDropTarget{TItem}.OnNativeDrop"/>.</summary>
public sealed class BlazorDndNativeDropEventArgs
{
    /// <summary>The matched native item type (one of <see cref="BlazorDndNativeTypes"/>).</summary>
    public required string ItemType { get; init; }

    /// <summary>Files for a <see cref="BlazorDndNativeTypes.File"/> drop.</summary>
    public IReadOnlyList<BlazorDndNativeFile> Files { get; init; } = Array.Empty<BlazorDndNativeFile>();

    /// <summary>Raw data keyed by DataTransfer type (e.g. <c>text/plain</c>, <c>text/html</c>).</summary>
    public IReadOnlyDictionary<string, string> Data { get; init; } =
        new Dictionary<string, string>();

    public string? Text => Data.TryGetValue("text/plain", out var v) ? v : null;
    public string? Html => Data.TryGetValue("text/html", out var v) ? v : null;
    public string? Url => Data.TryGetValue("text/uri-list", out var v) ? v : null;

    /// <summary>URLs for a <see cref="BlazorDndNativeTypes.Url"/> drop (uri-list split by lines).</summary>
    public IReadOnlyList<string> Urls => Url is null
        ? Array.Empty<string>()
        : Url.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0 && !s.StartsWith('#')).ToArray();
}
