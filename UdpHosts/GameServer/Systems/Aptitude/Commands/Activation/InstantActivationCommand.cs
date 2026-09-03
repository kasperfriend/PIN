using System;
using GameServer.Enums;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Activation;

/// <summary>
/// The activation node of a player ability chain. Every activated ability chain
/// starts with one of the activation commands, and this is the node that carries
/// the ability's cooldown configuration (local / category / global) in SDB.
/// <para>
/// It is both a requirement and an effect: it fails the chain while the ability
/// is still cooling down (which is what stops re-casting and makes the client
/// show the ability as unavailable), and it queues the cooldowns that the
/// AbilitySystem starts once the whole chain has succeeded. Deferring the start
/// keeps an activation that fails a later requirement (for example not enough
/// energy) from consuming the cooldown.
/// </para>
/// </summary>
public class InstantActivationCommand : Command, ICommand
{
    private InstantActivationCommandDef Params;

    public InstantActivationCommand(InstantActivationCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params == null)
        {
            return true;
        }

        // Always use the shard clock: cooldown pruning (AbilitySystem.Tick) and
        // the other cooldown commands are all expressed in shard time, and the
        // client-supplied activation time must not be trusted for gating.
        uint time = context.Shard.CurrentTime;
        var state = context.Abilities.GetOrAddState(context.ActivationInitiator);

        // Remember which cooldown category this ability belongs to so that the
        // activation gate in CombatController can check category cooldowns
        // before the chain is even started.
        if (Params.Category != 0 && context.AbilityId != 0)
        {
            context.Abilities.RegisterAbilityCategory(context.AbilityId, Params.Category);
        }

        // Requirement half: refuse to activate while a cooldown that covers this
        // ability is still running.
        var blocking = state.GetActiveCooldown(context.AbilityId, Params.Category, time);
        if (blocking != null)
        {
            Logger.Debug(
                "{Command} {CommandId} fails: ability {AbilityId} is on {Kind} cooldown until {ReadyAgainTime}",
                nameof(InstantActivationCommand),
                Params.Id,
                context.AbilityId,
                blocking.Kind,
                blocking.ReadyAgainTime);
            return false;
        }

        // Effect half: queue the cooldowns defined for this activation. They
        // are started by the AbilitySystem only once the whole chain has
        // succeeded, so an activation that fails a later requirement (for
        // example not enough energy) does not consume the cooldown.
        if (context.EnergyTransaction?.IsActive == true)
        {
            QueueCooldowns(context);
        }
        else
        {
            StartCooldowns(context, state, time);
        }

        return true;
    }

    private void QueueCooldowns(Context context)
    {
        if (Params.LocalCooldown != 0 && context.AbilityId != 0)
        {
            uint duration = ApplyRegop(context, Params.LocalCooldown, Params.DurationRegop);
            context.PendingCooldowns.Add(new AbilityCooldownRequest
            {
                Kind = AbilityCooldownKind.Local,
                AbilityId = context.AbilityId,
                Category = Params.Category,
                DurationMs = duration,
            });
            Logger.Debug(
                "{Command} {CommandId} queued {Duration}ms local cooldown for ability {AbilityId}",
                nameof(InstantActivationCommand),
                Params.Id,
                duration,
                context.AbilityId);
        }

        if (Params.CategoryCooldown != 0 && Params.Category != 0)
        {
            uint duration = ApplyRegop(context, Params.CategoryCooldown, Params.CategoryPrecoolRegop);
            context.PendingCooldowns.Add(new AbilityCooldownRequest
            {
                Kind = AbilityCooldownKind.Category,
                Category = Params.Category,
                DurationMs = duration,
            });
            Logger.Debug(
                "{Command} {CommandId} queued {Duration}ms category {Category} cooldown",
                nameof(InstantActivationCommand),
                Params.Id,
                duration,
                Params.Category);
        }

        if (Params.GlobalCooldown != 0)
        {
            context.PendingCooldowns.Add(new AbilityCooldownRequest
            {
                Kind = AbilityCooldownKind.Global,
                DurationMs = Params.GlobalCooldown,
            });
            Logger.Debug(
                "{Command} {CommandId} queued {Duration}ms global cooldown",
                nameof(InstantActivationCommand),
                Params.Id,
                Params.GlobalCooldown);
        }
    }

    private void StartCooldowns(Context context, AbilityState state, uint time)
    {
        if (Params.LocalCooldown != 0 && context.AbilityId != 0)
        {
            uint duration = ApplyRegop(context, Params.LocalCooldown, Params.DurationRegop);
            state.StartCooldown(AbilityCooldownKind.Local, context.AbilityId, Params.Category, duration, time);
        }

        if (Params.CategoryCooldown != 0 && Params.Category != 0)
        {
            uint duration = ApplyRegop(context, Params.CategoryCooldown, Params.CategoryPrecoolRegop);
            state.StartCooldown(AbilityCooldownKind.Category, 0, Params.Category, duration, time);
        }

        if (Params.GlobalCooldown != 0)
        {
            state.StartCooldown(AbilityCooldownKind.Global, 0, 0, Params.GlobalCooldown, time);
        }
    }

    private static uint ApplyRegop(Context context, uint value, byte regop)
    {
        if (regop == 0)
        {
            return value;
        }

        float result = AbilitySystem.RegistryOp(context.Register, value, (Operand)regop);
        return (uint)MathF.Max(0f, result);
    }
}
