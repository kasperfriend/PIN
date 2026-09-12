using System.Collections.Generic;
using GameServer.StaticDB.Records.apt;
using GameServer.Systems.Aptitude;

namespace GameServer.Systems.Ai;

/// <summary>
///     What the chains behind a weapon's ability ids do, as far as an NPC is concerned: whether the data
///     declares an animation in them, and whether they deliver the hit themselves.
/// </summary>
/// <param name="Animates">
///     A <c>apttf::tfPlayAnimationCommandDef</c> or <c>tfAbilityAnimationCommandDef</c> is reachable. Those
///     are the database's only animation instructions, and they are client-side commands: a client plays them
///     from the replicated status effect, which is why an NPC has to apply the effect to animate at all.
/// </param>
/// <param name="DeliversDamage">
///     An <c>aptfs::InflictDamageCommandDef</c> or <c>FireProjectileCommandDef</c> is reachable: the chain is
///     the attack, so the AI must not add its own direct hit on top of it.
/// </param>
public readonly record struct NpcWeaponAbilityScan(bool Animates, bool DeliversDamage)
{
    /// <summary>A weapon with no ability ids, or one whose chains do neither.</summary>
    public static readonly NpcWeaponAbilityScan None = default;
}

/// <summary>
///     Walks the aptitude chains a monster weapon's ability ids point at and reports what the database
///     declares in them. Pure: it reads through <see cref="INpcAttackDataSource" />, so the whole walk is
///     unit tested without a shard or a database file.
/// </summary>
/// <remarks>
///     The walk follows what the data can reach from a weapon ability: the command <c>next</c> chain, the
///     <c>if</c>/<c>then</c>/<c>else</c> bodies of <c>apt::ConditionalBranchCommandDef</c>, the ability a
///     <c>apt::CallCommandDef</c> names, and the apply/remove/update/duration chains of every
///     <c>apt::ImpactApplyEffectCommandDef</c> it finds - one effect level deep, which is as deep as the
///     build's monster weapon trees nest. A node is visited once, so shared tails and cycles cannot loop.
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
        bool animates = false;
        bool deliversDamage = false;

        ScanAbility(data, attackAbilityId, visited, ref animates, ref deliversDamage);
        ScanAbility(data, burstAbilityId, visited, ref animates, ref deliversDamage);

        return new NpcWeaponAbilityScan(animates, deliversDamage);
    }

    private static void ScanAbility(
        INpcAttackDataSource data,
        uint abilityId,
        HashSet<uint> visited,
        ref bool animates,
        ref bool deliversDamage)
    {
        var ability = abilityId != 0 ? data.GetAbility(abilityId) : null;
        if (ability == null || ability.Chain == 0)
        {
            return;
        }

        WalkChain(data, ability.Chain, visited, ref animates, ref deliversDamage);
    }

    private static void WalkChain(
        INpcAttackDataSource data,
        uint chainId,
        HashSet<uint> visited,
        ref bool animates,
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

            switch ((CommandType)command.Subtype)
            {
                case CommandType.PlayAnimation:
                case CommandType.AbilityAnimation:
                    animates = true;
                    break;

                case CommandType.InflictDamage:
                case CommandType.FireProjectile:
                    deliversDamage = true;
                    break;

                case CommandType.ConditionalBranch:
                {
                    var branch = data.GetConditionalBranch(commandId);
                    if (branch != null)
                    {
                        WalkChain(data, branch.IfChain, visited, ref animates, ref deliversDamage);
                        WalkChain(data, branch.ThenChain, visited, ref animates, ref deliversDamage);
                        WalkChain(data, branch.ElseChain, visited, ref animates, ref deliversDamage);
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
                            WalkChain(data, called.Chain, visited, ref animates, ref deliversDamage);
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
                        WalkChain(data, effect.ApplyChain, visited, ref animates, ref deliversDamage);
                        WalkChain(data, effect.RemoveChain, visited, ref animates, ref deliversDamage);
                        WalkChain(data, effect.UpdateChain, visited, ref animates, ref deliversDamage);
                        WalkChain(data, effect.DurationChain, visited, ref animates, ref deliversDamage);
                    }

                    break;
                }
            }

            commandId = command.Next;
        }
    }
}
