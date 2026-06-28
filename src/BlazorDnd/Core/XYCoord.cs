namespace BlazorDnd;

/// <summary>
/// Represents an { x, y } coordinate pair, mirroring React DnD's <c>XYCoord</c>.
/// Used for the various client/source offsets exposed by the monitor.
/// </summary>
public readonly record struct XYCoord(double X, double Y)
{
    public static XYCoord operator -(XYCoord a, XYCoord b) => new(a.X - b.X, a.Y - b.Y);
    public static XYCoord operator +(XYCoord a, XYCoord b) => new(a.X + b.X, a.Y + b.Y);
}
