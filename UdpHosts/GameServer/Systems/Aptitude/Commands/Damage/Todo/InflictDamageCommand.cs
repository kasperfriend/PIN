using System;
using System.Collections.Generic;
using System.Numerics;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Combat;

namespace GameServer.Systems.Aptitude.Commands.Damage;

public class InflictDamageCommand : Command, ICommand
{
    private static readonly HostilityResolver _hostility = new();

    private InflictDamageCommandDef Params;

    public InflictDamageCommand(InflictDamageCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var attacker = ResolveAttacker(context);
        if (attacker == null)
        {
            Logger.Debug("{Command} {CommandId} could not resolve an attacker ({Initiator}/{Self})", nameof(InflictDamageCommand), Params.Id, context.Initiator, context.Self);
            return true;
        }

        float damage = AbilitySystem.RegistryOp(context.Register, Params.Damagepoints, (Operand)Params.DamagepointsRegop);
        if (damage <= 0)
        {
            return true;
        }

        if (Params.Usedmgdealt == 1)
        {
            // Would reuse the damage dealt by a previous hit in this execution;
            // dealt damage is not tracked on the context yet.
            Logger.Debug("{Command} {CommandId} specifies Usedmgdealt which is not implemented", nameof(InflictDamageCommand), Params.Id);
        }

        float splashRange = AbilitySystem.RegistryOp(context.Register, Params.Splashrange, (Operand)Params.SplashrangeRegop);

        // Keyed by entity id because a direct target and a splash target arrive
        // as different entities of the same game object.
        var damaged = new HashSet<ulong>();

        foreach (IAptitudeTarget target in context.Targets)
        {
            if (!damaged.Add(target.EntityId))
            {
                continue;
            }

            ApplyDamageToTarget(context, target, attacker, damage);
        }

        if (splashRange > 0f)
        {
            Vector3 origin;
            if (Params.Frominitiatorpos == 1)
            {
                origin = context.InitPosition;
            }
            else if (context.Targets.TryPeek(out var splashCenter))
            {
                origin = splashCenter.Position;
            }
            else
            {
                origin = context.Self.Position;
            }

            foreach (var pair in context.Shard.Entities)
            {
                if (!damaged.Add(pair.Key))
                {
                    continue;
                }

                if (pair.Value is not IAptitudeTarget splashTarget || ReferenceEquals(splashTarget, attacker))
                {
                    continue;
                }

                float distance = Vector3.Distance(origin, splashTarget.Position);
                if (distance > splashRange)
                {
                    continue;
                }

                float scale = 1f;
                if (Params.Falloff == 1 && splashRange > 0f)
                {
                    float pointBlank = Math.Max(0f, Params.Pointblankrange);
                    float t = Math.Clamp((distance - pointBlank) / Math.Max(splashRange - pointBlank, 0.01f), 0f, 1f);
                    scale = 1f - t;
                }

                ApplyDamageToTarget(context, splashTarget, attacker, damage * scale);
            }
        }

        return true;
    }

    private static CharacterEntity ResolveAttacker(Context context)
    {
        if (context.Initiator is CharacterEntity initiator)
        {
            return initiator;
        }

        if (context.Initiator?.Owner is CharacterEntity initiatorOwner)
        {
            return initiatorOwner;
        }

        if (context.Self is CharacterEntity self)
        {
            return self;
        }

        return context.Self?.Owner;
    }

    private void ApplyDamageToTarget(Context context, IAptitudeTarget target, CharacterEntity attacker, float damage)
    {
        if (damage <= 0)
        {
            return;
        }

        // Prevent players from damaging themselves or their own deployables with
        // their own abilities; the projectile path treats that as friendly fire.
        if (ReferenceEquals(target, attacker) || ReferenceEquals(target, context.Self) || target.EntityId == attacker.EntityId)
        {
            return;
        }

        if (!context.Shard.Entities.TryGetValue(target.EntityId, out var entity))
        {
            return;
        }

        if (entity is not (CharacterEntity or DeployableEntity))
        {
            Logger.Debug("{Command} {CommandId} can not damage {Target}, which has no health", nameof(InflictDamageCommand), Params.Id, target);
            return;
        }

        if (entity is CharacterEntity character && character.CurrentHealth <= 0)
        {
            return;
        }

        var stance = _hostility.GetStance(attacker.HostilityInfo, entity.HostilityInfo);
        if (stance == HostilityStance.Friendly || stance == HostilityStance.Self)
        {
            Logger.Debug("{Command} {CommandId} skips {Target}: {Attacker} is {Stance} towards it", nameof(InflictDamageCommand), Params.Id, target, attacker, stance);
            return;
        }

        int damageInt = (int)MathF.Round(damage);
        if (damageInt < 1)
        {
            damageInt = 1;
        }

        context.Shard.Damage.ApplyDamage(entity, damageInt, attacker);
        context.Shard.Combat.HitFeedback?.TookDebugHit(entity, attacker, damageInt, false, false);
    }
}
