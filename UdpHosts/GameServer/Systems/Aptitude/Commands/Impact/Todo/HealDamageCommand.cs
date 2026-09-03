using System;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Combat;

namespace GameServer.Systems.Aptitude.Commands.Impact;

public class HealDamageCommand : Command, ICommand
{
    private static readonly HostilityResolver _hostility = new();

    private HealDamageCommandDef Params;

    public HealDamageCommand(HealDamageCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var healer = context.Initiator as CharacterEntity ?? context.Self as CharacterEntity ?? context.Initiator?.Owner;
        if (healer == null)
        {
            Logger.Debug("{Command} {CommandId} could not resolve a healer ({Initiator}/{Self})", nameof(HealDamageCommand), Params.Id, context.Initiator, context.Self);
            return true;
        }

        float heal = AbilitySystem.RegistryOp(context.Register, Params.Healpoints, (Operand)Params.HealpointsRegop);
        if (heal <= 0)
        {
            return true;
        }

        if (Params.Usedmgdealt == 1 || Params.Weapondamage == 1)
        {
            Logger.Debug("{Command} {CommandId} ignores Usedmgdealt/Weapondamage modifiers", nameof(HealDamageCommand), Params.Id);
        }

        int healInt = (int)MathF.Round(heal);
        if (healInt < 1)
        {
            healInt = 1;
        }

        foreach (IAptitudeTarget target in context.Targets)
        {
            if (!context.Shard.Entities.TryGetValue(target.EntityId, out var entity))
            {
                continue;
            }

            if (entity is not (CharacterEntity or DeployableEntity))
            {
                Logger.Debug("{Command} {CommandId} can not heal {Target}, which has no health", nameof(HealDamageCommand), Params.Id, target);
                continue;
            }

            if (entity is CharacterEntity character && character.CurrentHealth <= 0)
            {
                continue;
            }

            var stance = _hostility.GetStance(healer.HostilityInfo, entity.HostilityInfo);
            if (stance == HostilityStance.Hostile)
            {
                continue;
            }

            context.Shard.Damage.ApplyHeal(entity, healInt, healer);
        }

        return true;
    }
}
