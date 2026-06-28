namespace BlazorDnd;

/// <summary>
/// Item types for OS-level (native) drags, mirroring React DnD's HTML5 backend
/// <c>NativeTypes</c> (<c>FILE</c>, <c>URL</c>, <c>TEXT</c>). A <see cref="DropTarget{TItem}"/>
/// that accepts one of these will react to drags originating outside the application
/// (dragged files, selected text, links, etc.).
/// </summary>
public static class NativeTypes
{
    public const string File = "__NATIVE_FILE__";
    public const string Url = "__NATIVE_URL__";
    public const string Text = "__NATIVE_TEXT__";
    public const string Html = "__NATIVE_HTML__";

    internal static bool IsNative(string type) => type.StartsWith("__NATIVE_", StringComparison.Ordinal);

    /// <summary>Maps a native item type to its <c>DataTransfer</c> type token.</summary>
    internal static string ToDataTransferType(string nativeType) => nativeType switch
    {
        File => "Files",
        Url => "text/uri-list",
        Text => "text/plain",
        Html => "text/html",
        _ => nativeType,
    };
}

/// <summary>A single file described by a native file drop (metadata only; no contents).</summary>
public sealed class NativeFile
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public string Type { get; set; } = "";
}

/// <summary>Payload delivered to <see cref="DropTarget{TItem}.OnNativeDrop"/>.</summary>
public sealed class NativeDropEventArgs
{
    /// <summary>The matched native item type (one of <see cref="NativeTypes"/>).</summary>
    public required string ItemType { get; init; }

    /// <summary>Files for a <see cref="NativeTypes.File"/> drop.</summary>
    public IReadOnlyList<NativeFile> Files { get; init; } = Array.Empty<NativeFile>();

    /// <summary>Raw data keyed by DataTransfer type (e.g. <c>text/plain</c>, <c>text/html</c>).</summary>
    public IReadOnlyDictionary<string, string> Data { get; init; } =
        new Dictionary<string, string>();

    public string? Text => Data.TryGetValue("text/plain", out var v) ? v : null;
    public string? Html => Data.TryGetValue("text/html", out var v) ? v : null;
    public string? Url => Data.TryGetValue("text/uri-list", out var v) ? v : null;

    /// <summary>URLs for a <see cref="NativeTypes.Url"/> drop (uri-list split by lines).</summary>
    public IReadOnlyList<string> Urls => Url is null
        ? Array.Empty<string>()
        : Url.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0 && !s.StartsWith('#')).ToArray();
}

/// <summary>Raw payload shape received from JS for a native drop.</summary>
public sealed class NativeDropPayload
{
    public string[] Types { get; set; } = Array.Empty<string>();
    public Dictionary<string, string> Data { get; set; } = new();
    public NativeFile[] Files { get; set; } = Array.Empty<NativeFile>();
}
