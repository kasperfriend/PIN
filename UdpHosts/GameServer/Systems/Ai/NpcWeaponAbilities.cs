using System.Collections.Generic;
using GameServer.Systems.Aptitude;

namespace GameServer.Systems.Ai;

/// <summary>
///     What the chains behind a weapon's ability ids do, as far as an NPC is concerned: whether the data
///     makes a client draw something in them, and whether they deliver the hit themselves.
/// </summary>
/// <param name="ClientFeedback">
///     A command the client runs (a <c>apttf::</c> definition table: <c>tfPlayAnimationCommandDef</c>,
///     <c>tfAbilityAnimationCommandDef</c>, <c>tfPerformEmoteCommandDef</c>,
///     <c>tfParticleEffectAssetCommandDef</c>, <c>tfAudioFeedbackCommandDef</c>, ...) is reachable. Every
///     one of those commands is a client-side instruction the server does not execute, so a chain that
///     holds one produces nothing at all until the effect carrying it is applied and replicated - which is
///     what this flag tells the AI to do.
/// </param>
/// <param name="DeliversDamage">
///     An <c>aptfs::InflictDamageCommandDef</c> or <c>FireProjectileCommandDef</c> is reachable: the chain is
///     the attack, so the AI must not add its own direct hit on top of it.
/// </param>
public readonly record struct NpcWeaponAbilityScan(bool ClientFeedback, bool DeliversDamage)
{
    /// <summary>A weapon with no ability ids, or one whose chains carry no client feedback and no damage.</summary>
    public static readonly NpcWeaponAbilityScan None = default;
}

/// <summary>
///     Walks the aptitude chains a monster weapon's ability ids point at and reports what the database
///     declares in them. Pure: it reads through <see cref="INpcAttackDataSource" />, so the whole walk is
///     unit tested without a shard or a database file.
/// </summary>
/// <remarks>
///     The walk follows what the data can reach from a weapon ability: the command <c>next</c> chain, the
///     <c>if</c>/<c>then</c>/<c>else</c> bodies of <c>apt::ConditionalBranchCommandDef</c>, the chains the rest
///     of the control flow hands execution to (the and/or/negate chains of the logic commands, a while loop's
///     condition and body, the pre-apply chain of an effect toggle), the chain an
///     <c>apt::UpdateWaitAndFireOnceCommandDef</c> fires once its wait is up, the ability a
///     <c>apt::CallCommandDef</c> names, and the apply/remove/update/duration chains of every
///     <c>apt::ImpactApplyEffectCommandDef</c> it finds, however deeply the data nests them. A command is
///     visited once, so shared tails and cycles cannot loop, and the walk stops at
///     <see cref="MaxCommands" /> as a guard against malformed data.
///     <para>
///         Following the chains a command hands execution to is what keeps the flags true to the engine: a
///         behaviour ability module can hide its whole attack behind a wait (module 81952's ability 35803
///         fires its projectile from the effect update loop) or behind a logic branch (module 120937's ability
///         38700 picks its stage that way, animations included). A walk that only followed <c>next</c> would
///         call those chains harmless and let the AI add its own attack on top of them.
///     </para>
///     <para>
///         Whether a command is the client's is decided from the database, not from a list kept here: the
///         kind of a command instance is its <c>apt::BaseCommandDef.subtype</c>, and the table that subtype's
///         parameters live in (<c>apt::CommandType.sdb_fullname</c>) says who runs it. The <c>apttf::</c>
///         tables are the client's feedback commands, the <c>aptfs::</c> ones are the server's functions and
///         the bare <c>apt::</c> ones are control flow both sides know.
///     </para>
/// </remarks>
public static class NpcWeaponAbilities
{
    /// <summary>Ceiling on commands visited, as a belt-and-braces stop for malformed data.</summary>
    private const int MaxCommands = 2_000;

    /// <summary>Scans the ability ids a weapon carries (0 for the hooks the row leaves empty).</summary>
    public static NpcWeaponAbilityScan Scan(INpcAttackDataSource data, uint attackAbilityId, uint burstAbilityId)
    {
        if (data == null || (attackAbilityId == 0 && burstAbilityId == 0))
        {
            return NpcWeaponAbilityScan.None;
        }

        var visited = new HashSet<uint>();
        bool clientFeedback = false;
        bool deliversDamage = false;

        ScanAbility(data, attackAbilityId, visited, ref clientFeedback, ref deliversDamage);
        ScanAbility(data, burstAbilityId, visited, ref clientFeedback, ref deliversDamage);

        return new NpcWeaponAbilityScan(clientFeedback, deliversDamage);
    }

    /// <summary>
    ///     Scans one ability id with a fresh visited set: the walk a weapon hook and a behaviour set's
    ///     ability module (<see cref="NpcBehaviorAbilities" />) both use.
    /// </summary>
    /// <param name="data">The database the chains are read from.</param>
    /// <param name="abilityId">The <c>apt::AbilityData</c> id, or 0 for none.</param>
    /// <returns>What the ability's chains carry, <see cref="NpcWeaponAbilityScan.None" /> for a missing ability.</returns>
    public static NpcWeaponAbilityScan ScanAbility(INpcAttackDataSource data, uint abilityId)
    {
        if (data == null || abilityId == 0)
        {
            return NpcWeaponAbilityScan.None;
        }

        var visited = new HashSet<uint>();
        bool clientFeedback = false;
        bool deliversDamage = false;
        ScanAbility(data, abilityId, visited, ref clientFeedback, ref deliversDamage);
        return new NpcWeaponAbilityScan(clientFeedback, deliversDamage);
    }

    private static void ScanAbility(
        INpcAttackDataSource data,
        uint abilityId,
        HashSet<uint> visited,
        ref bool clientFeedback,
        ref bool deliversDamage)
    {
        var ability = abilityId != 0 ? data.GetAbility(abilityId) : null;
        if (ability == null || ability.Chain == 0)
        {
            return;
        }

        WalkChain(data, ability.Chain, visited, ref clientFeedback, ref deliversDamage);
    }

    private static void WalkChain(
        INpcAttackDataSource data,
        uint chainId,
        HashSet<uint> visited,
        ref bool clientFeedback,
        ref bool deliversDamage)
    {
        for (uint commandId = chainId; commandId != 0 && visited.Count < MaxCommands;)
        {
            if (!visited.Add(commandId))
            {
                // Either the end of a shared tail or a cycle in the data: the flags are already accumulated.
                return;
            }

            var command = data.GetCommand(commandId);
            if (command == null)
            {
                return;
            }

            if (data.IsClientCommand(command.Subtype))
            {
                clientFeedback = true;
            }

            switch ((CommandType)command.Subtype)
            {
                case CommandType.InflictDamage:
                case CommandType.FireProjectile:
                    deliversDamage = true;
                    break;

                case CommandType.ConditionalBranch:
                {
                    var branch = data.GetConditionalBranch(commandId);
                    if (branch != null)
                    {
                        WalkChain(data, branch.IfChain, visited, ref clientFeedback, ref deliversDamage);
                        WalkChain(data, branch.ThenChain, visited, ref clientFeedback, ref deliversDamage);
                        WalkChain(data, branch.ElseChain, visited, ref clientFeedback, ref deliversDamage);
                    }

                    break;
                }

                case CommandType.UpdateWaitAndFireOnce:
                {
                    var wait = data.GetUpdateWaitAndFireOnce(commandId);
                    if (wait != null)
                    {
                        WalkChain(data, wait.Chain, visited, ref clientFeedback, ref deliversDamage);
                    }

                    break;
                }

                case CommandType.LogicAndChain:
                {
                    var and = data.GetLogicAndChain(commandId);
                    if (and != null)
                    {
                        WalkChain(data, and.AndChain, visited, ref clientFeedback, ref deliversDamage);
                    }

                    break;
                }

                case CommandType.LogicOrChain:
                {
                    var or = data.GetLogicOrChain(commandId);
                    if (or != null)
                    {
                        WalkChain(data, or.OrChain, visited, ref clientFeedback, ref deliversDamage);
                    }

                    break;
                }

                case CommandType.LogicNegate:
                {
                    var negate = data.GetLogicNegate(commandId);
                    if (negate != null)
                    {
                        WalkChain(data, negate.NegateChain, visited, ref clientFeedback, ref deliversDamage);
                    }

                    break;
                }

                case CommandType.WhileLoop:
                {
                    var loop = data.GetWhileLoop(commandId);
                    if (loop != null)
                    {
                        WalkChain(data, loop.ConditionChain, visited, ref clientFeedback, ref deliversDamage);
                        WalkChain(data, loop.BodyChain, visited, ref clientFeedback, ref deliversDamage);
                    }

                    break;
                }

                case CommandType.ImpactToggleEffect:
                {
                    var toggle = data.GetImpactToggleEffect(commandId);
                    if (toggle != null)
                    {
                        WalkChain(data, toggle.PreApplyChain, visited, ref clientFeedback, ref deliversDamage);
                    }

                    break;
                }

                case CommandType.Call:
                {
                    var call = data.GetCall(commandId);
                    if (call != null)
                    {
                        var called = call.AbilityId != 0 ? data.GetAbility(call.AbilityId) : null;
                        if (called != null)
                        {
                            WalkChain(data, called.Chain, visited, ref clientFeedback, ref deliversDamage);
                        }
                    }

                    break;
                }

                case CommandType.ImpactApplyEffect:
                {
                    var apply = data.GetImpactApplyEffect(commandId);
                    var effect = apply != null && apply.EffectId != 0 ? data.GetStatusEffect(apply.EffectId) : null;
                    if (effect != null)
                    {
                        WalkChain(data, effect.ApplyChain, visited, ref clientFeedback, ref deliversDamage);
                        WalkChain(data, effect.RemoveChain, visited, ref clientFeedback, ref deliversDamage);
                        WalkChain(data, effect.UpdateChain, visited, ref clientFeedback, ref deliversDamage);
                        WalkChain(data, effect.DurationChain, visited, ref clientFeedback, ref deliversDamage);
                    }

                    break;
                }
            }

            commandId = command.Next;
        }
    }
}
