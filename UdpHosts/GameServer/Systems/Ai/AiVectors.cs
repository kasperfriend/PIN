using System;
using System.Numerics;

namespace GameServer.Systems.Ai;

/// <summary>
///     Small pure helpers shared by the AI engine. Kept separate so the maths can be
///     asserted on directly instead of only through a running shard.
/// </summary>
public static class AiVectors
{
    /// <summary>Distance between two points ignoring Z, which is what all the AI ranges mean.</summary>
    public static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    ///     Builds the orientation a character must have to look along <paramref name="forward" />.
    /// </summary>
    /// <remarks>
    ///     <c>CharacterEntity</c> resolves its facing as
    ///     <c>QuaternionEx.Transform(new Vector3(0, 0, 1), QuaternionEx.Inverse(Orientation))</c>
    ///     (see <c>CharacterEntity.CalculateProjectileOrigin</c>), i.e. the character's local +Z
    ///     axis is where it looks. So the orientation has to be a rotation that carries the
    ///     world forward direction onto world +Z, which is a 90 degree turn around the axis
    ///     perpendicular to both.
    /// </remarks>
    public static Quaternion OrientationFacing(Vector3 forward)
    {
        var flat = new Vector3(forward.X, forward.Y, 0f);
        if (flat.LengthSquared() < 0.0001f)
        {
            return Quaternion.Identity;
        }

        flat = Vector3.Normalize(flat);
        var axis = Vector3.Cross(flat, Vector3.UnitZ);
        if (axis.LengthSquared() < 0.0001f)
        {
            return Quaternion.Identity;
        }

        return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI / 2f);
    }
}
