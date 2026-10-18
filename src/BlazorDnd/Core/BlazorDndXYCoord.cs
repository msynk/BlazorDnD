namespace BlazorDnd;

/// <summary>
/// Represents an { x, y } coordinate pair, mirroring React DnD's <c>XYCoord</c>.
/// Used for the various client/source offsets exposed by the monitor.
/// </summary>
public readonly record struct BlazorDndXYCoord(double X, double Y)
{
    public static BlazorDndXYCoord operator -(BlazorDndXYCoord a, BlazorDndXYCoord b) => new(a.X - b.X, a.Y - b.Y);
    public static BlazorDndXYCoord operator +(BlazorDndXYCoord a, BlazorDndXYCoord b) => new(a.X + b.X, a.Y + b.Y);
}
