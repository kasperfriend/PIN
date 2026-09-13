using System.Numerics;

namespace Shared.Collision.ZoneLoading;

/// <summary>
///     A zone chunk marked by the original static database as excluded from AI pathing.
///     The chunk metadata is a bit field; a non-zero value is intentionally retained rather
///     than interpreted here because the individual bits are owned by the original CAIS runtime.
/// </summary>
public readonly record struct ZoneNavigationRegion(Vector3 Min, Vector3 Max, ulong ExcludeFromPathing)
{
    public bool Contains(Vector3 point)
    {
        return point.X >= Min.X && point.X < Max.X &&
               point.Y >= Min.Y && point.Y < Max.Y;
    }
}
