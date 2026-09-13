using System.Numerics;

namespace Shared.Collision.Navigation;

/// <summary>
///     A collision triangle retained for navigation. Unlike a Bepu mesh, this keeps the original
///     physics-material id so CAIS pathing cost can be applied before the geometry is reduced to a
///     runtime physics shape.
/// </summary>
public readonly record struct NavigationTriangle(
    Vector3 A,
    Vector3 B,
    Vector3 C,
    uint PhysicsMaterialId)
{
    public Vector3 Centroid => (A + B + C) / 3f;

    public Vector3 Normal
    {
        get
        {
            var cross = Vector3.Cross(B - A, C - A);
            return cross.LengthSquared() > 0.000001f ? Vector3.Normalize(cross) : Vector3.Zero;
        }
    }
}
