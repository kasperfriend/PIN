using System;
using System.Numerics;
using GameServer.Entities.Character;
using GameServer.Physics;

namespace GameServer.Systems.Ai;

/// <summary>
///     Where an NPC's shot goes: the middle of the target's model, led for its movement and,
///     for the parabolic rows, compensated for the drop. The weapon's own first-shot cone on
///     top of that is <see cref="NpcAttackSpreadMath" />'s job.
/// </summary>
/// <remarks>
///     <para>
///     Before this, an NPC aimed a fixed height above the target's feet (the eyes). The shot
///     still registered - a character's collision volume is forgiving - but it visibly sailed
///     over the model. The aim is now the centre of the volume the shot actually hits: the
///     physics engine's cached middle of the target's current collision compound (the same
///     resolution the body uses, so crouch, sprint and prone are tracked), or half the pose's
///     <c>PhysicsHeight</c> when the shape does not resolve.
///     </para>
///     <para>
///     Aiming at the model is not the same as landing in it: the round is in the air for
///     distance/speed seconds, in which a running target keeps running, and the parabolic rows
///     drop <c>g·t²/2</c> over that flight. The aim therefore leads the target's velocity over
///     the flight time, and for a parabolic simulation solves the drop in closed form (the
///     exact parabola <c>ProjectileSim</c> integrates) instead of firing at the point the round
///     will have fallen away from. Both degrade to the old straight shot when the data says
///     nothing: zero velocity, no gravity, or an arc the speed cannot reach.
///     </para>
/// </remarks>
public static class NpcAttackAim
{
    /// <summary>Mid-torso of a character whose pose carries no usable physics height.</summary>
    public const float DefaultAimHeight = 0.9f;

    /// <summary>A lead beyond this is chasing a bad velocity, not the target; it is clamped.</summary>
    public const float MaxLeadMetres = 3f;

    /// <summary>
    ///     The world-space point a shot must pass through to hit the middle of the target's
    ///     model: the centre of its current collision volume when physics resolves one, else
    ///     half the database's physics height for the pose, else the default mid-torso.
    /// </summary>
    public static Vector3 AimPoint(PhysicsEngine? physics, CharacterEntity target)
    {
        if (physics != null && physics.TryGetCharacterAimPoint(target, out var point))
        {
            return point;
        }

        float height = target.Collision?.PoseTypeRecord?.PhysicsHeight ?? 0f;
        if (!float.IsFinite(height) || height <= 0f)
        {
            height = DefaultAimHeight * 2f;
        }

        return target.Position + new Vector3(0f, 0f, height * 0.5f);
    }

    /// <summary>
    ///     The direction to fire along so a round of <paramref name="projectileSpeed" /> lands
    ///     on <paramref name="aimPoint"/>: led for <paramref name="targetVelocity" /> over the
    ///     flight time, and, when <paramref name="gravity" /> is positive, launched on the
    ///     parabola that ends at the point (0 for a straight simulation - pass 0 for the
    ///     non-parabolic rows, which the sim does not drop). <paramref name="flightTime" /> is
    ///     the time the round spends in the air. Degenerate inputs (no point in front of the
    ///     muzzle, no usable speed) return <see cref="Vector3.Zero" /> so the caller can fall
    ///     back to where the shooter faces.
    /// </summary>
    public static Vector3 ShotDirection(
        Vector3 origin,
        Vector3 aimPoint,
        Vector3 targetVelocity,
        float projectileSpeed,
        float gravity,
        out float flightTime)
    {
        flightTime = 0f;

        bool hasSpeed = float.IsFinite(projectileSpeed) && projectileSpeed > 0f;
        if (!hasSpeed)
        {
            return Vector3.Zero;
        }

        float drop = float.IsFinite(gravity) && gravity > 0f ? gravity : 0f;
        var velocity = Finite(targetVelocity) ? targetVelocity : Vector3.Zero;

        // Lead and drop feed each other: the round spends the shot's flight time in the air and
        // the target keeps moving through it, so the lead is computed with the flight time the
        // shot actually has - the parabola's for the rows the sim drops, not the straight
        // distance/speed estimate. The aim point (aim + lead) is the fixed point of "aim plus
        // the lead over that shot's own flight time"; the offset is clamped so a bad velocity
        // aims wide, not across the map. The settle is a contraction by |velocity|/speed, so
        // it closes in a handful of passes.
        var point = aimPoint;
        for (int i = 0; i < 8; i++)
        {
            SolveFlightTime(origin, point, projectileSpeed, drop, out flightTime);
            var lead = velocity * flightTime;
            float leadLength = lead.Length();
            if (leadLength > MaxLeadMetres)
            {
                lead *= MaxLeadMetres / leadLength;
            }

            var next = aimPoint + lead;
            if ((next - point).LengthSquared() <= 0.000001f)
            {
                point = next;
                break;
            }

            point = next;
        }

        // The final solve runs on the settled point: the closed-form parabola through it when
        // the row drops (an unreachable arc falls through to the straight shot, which is the
        // old behaviour).
        if (drop > 0f && TrySolveParabolic(origin, point, projectileSpeed, drop, out var direction, out flightTime))
        {
            return direction;
        }

        var straight = point - origin;
        if (straight.LengthSquared() <= 0.0001f)
        {
            return Vector3.Zero;
        }

        flightTime = straight.Length() / projectileSpeed;
        return Vector3.Normalize(straight);
    }

    /// <summary>
    ///     The flight time a shot from <paramref name="origin" /> to <paramref name="point" />
    ///     has: the parabola's when the row drops and the speed reaches the point, else the
    ///     straight distance/speed.
    /// </summary>
    private static void SolveFlightTime(Vector3 origin, Vector3 point, float speed, float gravity, out float time)
    {
        if (gravity > 0f && TrySolveParabolic(origin, point, speed, gravity, out _, out time))
        {
            return;
        }

        time = (point - origin).Length() / speed;
    }

    /// <summary>
    ///     The initial direction of a constant-speed shot that lands on <paramref name="point" />
    ///     under a constant downward <paramref name="gravity" />, or false when no arc at that
    ///     speed reaches the point. The solve inverts <c>ProjectileSim</c>'s own parabola
    ///     (<c>start + v·t + (0,0,-g)·t²/2</c>): with <c>u = t²</c> the unit-direction constraint
    ///     reduces to <c>(g/2)²u² + (g·Δz − v²)u + (Δz² + d²) = 0</c>, where <c>d</c> is the
    ///     horizontal distance and <c>Δz</c> the height difference. The low arc is preferred;
    ///     the high arc is the answer for a target below the muzzle.
    /// </summary>
    private static bool TrySolveParabolic(Vector3 origin, Vector3 point, float speed, float gravity, out Vector3 direction, out float time)
    {
        direction = Vector3.Zero;
        time = 0f;

        var delta = point - origin;
        float dz = delta.Z;
        var flat = new Vector3(delta.X, delta.Y, 0f);
        float d = flat.Length();

        float a = 0.25f * gravity * gravity;
        float b = gravity * dz - speed * speed;
        float c = dz * dz + d * d;
        float discriminant = b * b - 4f * a * c;
        if (discriminant < 0f)
        {
            return false;
        }

        float sqrtDiscriminant = MathF.Sqrt(discriminant);
        float u = (-b - sqrtDiscriminant) / (2f * a);
        if (u <= 0f)
        {
            u = (-b + sqrtDiscriminant) / (2f * a);
        }

        if (!float.IsFinite(u) || u <= 0f)
        {
            return false;
        }

        float t = MathF.Sqrt(u);
        // The horizontal displacement is the horizontal velocity times t; the vertical one
        // adds the fall. By the quadratic, the resulting speed is exactly `speed`.
        var velocity = new Vector3(flat.X / t, flat.Y / t, (dz + 0.5f * gravity * u) / t);
        if (!Finite(velocity) || velocity.LengthSquared() <= 0.0001f)
        {
            return false;
        }

        direction = Vector3.Normalize(velocity);
        time = t;
        return true;
    }

    private static bool Finite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
