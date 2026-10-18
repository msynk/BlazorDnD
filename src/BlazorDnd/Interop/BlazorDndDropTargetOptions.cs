namespace BlazorDnd;

/// <summary>Options of a drop target registration in <c>blazor-dnd.js</c>.</summary>
/// <param name="CanDrop">Whether the target can accept the in-app item currently being dragged.</param>
/// <param name="NativeTypes">
/// The <c>DataTransfer</c> types of the native drags the target accepts. The same array instance
/// is reused while the accepted types are unchanged, so unchanged options compare equal.
/// </param>
internal sealed record BlazorDndDropTargetOptions(bool CanDrop, string[] NativeTypes);
