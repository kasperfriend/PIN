using System;
using System.Numerics;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.Entities.Vehicle;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Target;

/// <summary>
///     Acquires a single target in front of the caster within Range. Used in ~60 ability chains for
///     single-target abilities (e.g. direct damage, debuffs). Previously a no-op placeholder.
///     <para>
///     Origin is Self.Position or InitPosition when UseInitPos=1.
///     Forward is AimDirection or body facing (same as TargetConeAE).
///     Picks the closest entity in front (dot>0) within Range, with line-of-sight check when IgnoreWalls=0.
///     StaticOnly=1 restricts to non-character entities (deployables/vehicles) as best-effort.
///     SetOffset is logged but not yet used for projectile offset.
///     </para>
/// </summary>
public class TargetSingleCommand : Command, ICommand
{
    private const float EyeHeight = 1.4f;
    private const float Epsilon = 0.0001f;

    private TargetSingleCommandDef Params;

    public TargetSingleCommand(TargetSingleCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var previousTargets = context.Targets;
        // For TargetSingle, we keep former targets but new list will contain at most one acquired target
        // plus possibly existing? The observed chain pattern is TargetSingle then TargetHostiles, so
        // TargetSingle should append to existing list (like PBAE/ConeAE). We'll follow that: former = previous, then append single.
        context.FormerTargets = new AptitudeTargets(previousTargets);

        if (Params.Range <= 0f)
        {
            Logger.Debug(\"{Command} {CommandId} has range {Range}, no target can be acquired\", nameof(TargetSingleCommand), Params.Id, Params.Range);
            return true;
        }

        Vector3 origin = Params.UseInitPos == 1 ? context.InitPosition : context.Self.Position;

        if (!TryGetForward(context.Self, out Vector3 forward))
        {
            Logger.Debug(\"{Command} {CommandId} could not resolve forward for {Self}\", nameof(TargetSingleCommand), Params.Id, context.Self);
            return true;
        }

        BaseAptitudeEntity closest = null;
        float closestDist = float.MaxValue;

        foreach (var pair in context.Shard.Entities)
        {
            if (pair.Value is not BaseAptitudeEntity candidate)
            {
                continue;
            }

            if (candidate == context.Self)
            {
                continue;
            }

            if (Params.StaticOnly == 1 && candidate is CharacterEntity)
            {
                // Best-effort: static_only restricts to non-character
                continue;
            }

            Vector3 offset = candidate.Position - origin;
            float distance = offset.Length();

            if (distance > Params.Range || distance < Epsilon)
            {
                continue;
            }

            // Must be in front (dot > 0)
            float dot = Vector3.Dot(offset, forward);
            if (dot <= 0f)
            {
                continue;
            }

            // Optional angle check: if target is far to the side, skip. Use 90-degree half-angle (in front hemisphere)
            // This is permissive; we could tighten to 45 degrees, but no angle param exists.
            // We'll keep hemisphere check only (dot>0) for now.

            if (Params.IgnoreWalls != 1 && !HasLineOfSight(context, origin, candidate))
            {
                continue;
            }

            if (distance < closestDist)
            {
                closestDist = distance;
                closest = candidate;
            }
        }

        if (closest != null)
        {
            if (Params.SetOffset == 1)
            {
                Logger.Debug(\"{Command} {CommandId} SetOffset=1 requested for target {Target}, offset handling not yet implemented\", nameof(TargetSingleCommand), Params.Id, closest);
            }

            context.Targets.Push(closest);
            Logger.Debug(\"{Command} {CommandId} acquired single target {Target} at {Distance}m\", nameof(TargetSingleCommand), Params.Id, closest, closestDist);
        }
        else
        {
            Logger.Debug(\"{Command} {CommandId} found no target within {Range}m in front of {Self}\", nameof(TargetSingleCommand), Params.Id, Params.Range, context.Self);
        }

        return true;
    }

    private static bool TryGetForward(IAptitudeTarget source, out Vector3 forward)
    {
        Vector3 candidate = Vector3.Zero;

        candidate = source switch
        {
            CharacterEntity character => character.AimDirection,
            DeployableEntity deployable => deployable.AimDirection,
            VehicleEntity vehicle => vehicle.AimDirection,
            _ => Vector3.Zero
        };

        if (candidate.LengthSquared() < Epsilon && source is BaseEntity entity)
        {
            // Body facing: model's local +Y forward, transformed by inverse orientation (see AiVectors.OrientationFacing)
            candidate = Vector3.Transform(new Vector3(0f, 1f, 0f), Quaternion.Conjugate(entity.Orientation));
        }

        if (candidate.LengthSquared() < Epsilon)
        {
            forward = Vector3.Zero;
            return false;
        }

        forward = Vector3.Normalize(candidate);
        return true;
    }

    private static bool HasLineOfSight(Context context, Vector3 origin, BaseAptitudeEntity candidate)
    {
        var physics = context.Shard.Physics;
        if (physics == null)
        {
            return true;
        }

        Vector3 from = origin + new Vector3(0f, 0f, EyeHeight);
        Vector3 to = candidate.Position + new Vector3(0f, 0f, EyeHeight);
        var hit = physics.SegmentRayCast(from, to, context.Self.EntityId);
        return !hit.Hit || hit.HitEntityId == candidate.EntityId;
    }
}