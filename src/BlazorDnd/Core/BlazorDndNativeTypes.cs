namespace BlazorDnd;

/// <summary>
/// Item types for OS-level (native) drags, mirroring React DnD's HTML5 backend
/// <c>BlazorDndNativeTypes</c> (<c>FILE</c>, <c>URL</c>, <c>TEXT</c>). A <see cref="BlazorDndDropTarget{TItem}"/>
/// that accepts one of these will react to drags originating outside the application
/// (dragged files, selected text, links, etc.).
/// </summary>
public static class BlazorDndNativeTypes
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
