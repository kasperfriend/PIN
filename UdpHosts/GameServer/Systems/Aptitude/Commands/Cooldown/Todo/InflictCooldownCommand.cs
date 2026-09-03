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
        uint localDuration = ResolveDuration(context, Params.LocalCooldown, Params.DurationRegop);
        uint categoryDuration = ResolveDuration(context, Params.CategoryCooldown, Params.CategoryPrecoolRegop);

        if (Params.LocalCooldownPrecoolCount != 0 || Params.CategoryCooldownPrecoolCount != 0)
        {
            Logger.Debug(
                "{Command} {CommandId} ignores precool counts {Local}/{Category}",
                nameof(InflictCooldownCommand),
                Params.Id,
                Params.LocalCooldownPrecoolCount,
                Params.CategoryCooldownPrecoolCount);
        }

        // Activation chains use the same transaction as energy commands. Queue
        // every cooldown variant here instead of starting it immediately so a
        // later failed requirement cannot leave a cooldown behind.
        if (context.EnergyTransaction?.IsActive == true)
        {
            QueueCooldowns(context, localDuration, categoryDuration);
            return true;
        }

        var state = context.Abilities.GetOrAddState(context.Self);
        StartCooldowns(context, state, time, localDuration, categoryDuration);
        return true;
    }

    private void QueueCooldowns(Context context, uint localDuration, uint categoryDuration)
    {
        if (localDuration != 0 && context.AbilityId != 0)
        {
            context.PendingCooldowns.Add(new AbilityCooldownRequest
            {
                Kind = AbilityCooldownKind.Local,
                AbilityId = context.AbilityId,
                Category = Params.Category,
                DurationMs = localDuration,
            });
        }

        if (categoryDuration != 0 && Params.Category != 0)
        {
            context.PendingCooldowns.Add(new AbilityCooldownRequest
            {
                Kind = AbilityCooldownKind.Category,
                Category = Params.Category,
                DurationMs = categoryDuration,
            });
        }

        if (Params.GlobalCooldown != 0)
        {
            context.PendingCooldowns.Add(new AbilityCooldownRequest
            {
                Kind = AbilityCooldownKind.Global,
                DurationMs = Params.GlobalCooldown,
            });
        }
    }

    private void StartCooldowns(Context context, AbilityState state, uint time, uint localDuration, uint categoryDuration)
    {
        if (localDuration != 0 && context.AbilityId != 0)
        {
            var entry = state.StartCooldown(AbilityCooldownKind.Local, context.AbilityId, 0, localDuration, time);
            if (entry != null)
            {
                Logger.Debug(
                    "{Command} {CommandId} started {Duration}ms cooldown for ability {AbilityId}",
                    nameof(InflictCooldownCommand),
                    Params.Id,
                    entry.ReadyAgainTime - entry.ActivatedTime,
                    context.AbilityId);
            }
        }

        if (categoryDuration != 0 && Params.Category != 0)
        {
            state.StartCooldown(AbilityCooldownKind.Category, 0, Params.Category, categoryDuration, time);
        }

        if (Params.GlobalCooldown != 0)
        {
            state.StartCooldown(AbilityCooldownKind.Global, 0, 0, Params.GlobalCooldown, time);
        }
    }

    private static uint ResolveDuration(Context context, uint value, byte regop)
    {
        if (value == 0)
        {
            return 0;
        }

        float duration = regop == 0
            ? value
            : AbilitySystem.RegistryOp(context.Register, value, (Operand)regop);
        return (uint)MathF.Max(0f, duration);
    }
}
