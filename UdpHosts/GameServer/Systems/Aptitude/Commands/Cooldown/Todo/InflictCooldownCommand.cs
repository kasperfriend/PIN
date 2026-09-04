using System;
using GameServer.Enums;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Cooldown;

public class InflictCooldownCommand : Command, ICommand
{
    private InflictCooldownCommandDef Params;

    public InflictCooldownCommand(InflictCooldownCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        uint time = context.Shard.CurrentTime;
        var state = context.Abilities.GetOrAddState(context.Self);

        // Precool counts start the cooldown before the ability finishes; that
        // only shifts the visible timer start and is not modeled yet.
        if (Params.LocalCooldownPrecoolCount != 0 || Params.CategoryCooldownPrecoolCount != 0)
        {
            Logger.Debug("{Command} {CommandId} ignores precool counts {Local}/{Category}", nameof(InflictCooldownCommand), Params.Id, Params.LocalCooldownPrecoolCount, Params.CategoryCooldownPrecoolCount);
        }

        if (Params.LocalCooldown != 0 && context.AbilityId != 0)
        {
            float duration = Params.DurationRegop != 0
                ? AbilitySystem.RegistryOp(context.Register, Params.LocalCooldown, (Operand)Params.DurationRegop)
                : Params.LocalCooldown;
            var entry = state.StartCooldown(AbilityCooldownKind.Local, context.AbilityId, 0, (uint)MathF.Max(0f, duration), time);
            if (entry != null)
            {
                Logger.Debug("{Command} {CommandId} started {Duration}ms cooldown for ability {AbilityId}", nameof(InflictCooldownCommand), Params.Id, entry.ReadyAgainTime - entry.ActivatedTime, context.AbilityId);
            }
        }

        if (Params.CategoryCooldown != 0 && Params.Category != 0)
        {
            float duration = Params.CategoryPrecoolRegop != 0
                ? AbilitySystem.RegistryOp(context.Register, Params.CategoryCooldown, (Operand)Params.CategoryPrecoolRegop)
                : Params.CategoryCooldown;
            state.StartCooldown(AbilityCooldownKind.Category, 0, Params.Category, (uint)MathF.Max(0f, duration), time);
        }

        if (Params.GlobalCooldown != 0)
        {
            state.StartCooldown(AbilityCooldownKind.Global, 0, 0, Params.GlobalCooldown, time);
        }

        return true;
    }
}
