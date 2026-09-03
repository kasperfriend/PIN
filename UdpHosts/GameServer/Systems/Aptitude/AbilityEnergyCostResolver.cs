using System.Collections.Generic;
using GameServer.StaticDB;
using AptCommandType = GameServer.Systems.Aptitude.CommandType;

namespace GameServer.Systems.Aptitude;

/// <summary>
/// Resolves the server-side fallback cost for abilities whose energy command
/// is absent from the server's usable aptitude data. The normal path remains
/// data-driven: a fallback is used only when the complete ability graph does
/// not contain an effective energy-spend command.
/// </summary>
public static class AbilityEnergyCostResolver
{
    private sealed class FallbackCost
    {
        public FallbackCost(uint abilityModuleId, float amount)
        {
            AbilityModuleId = abilityModuleId;
            Amount = amount;
        }

        public uint AbilityModuleId { get; }
        public float Amount { get; }
    }

    // The server data currently exposes these Raptor activation commands as
    // client-only or omits them altogether. Keep the fallback deliberately
    // narrow and keyed by both ability and module so it cannot charge an
    // unrelated ability. These are server fallback values, not battleframe
    // maximum-energy values; replace them with the SDB amount when it becomes
    // available.
    private static readonly Dictionary<uint, FallbackCost> FallbackCosts = new()
    {
        [39405] = new FallbackCost(105981, 50f),
        [35909] = new FallbackCost(104170, 100f),
        [35345] = new FallbackCost(103986, 75f),
        [35567] = new FallbackCost(106103, 150f),
    };

    /// <summary>
    /// Resolves a fallback cost for an activation. A zero module id is allowed
    /// for internally called abilities, but a non-zero module id must match the
    /// known ability/module pair.
    /// </summary>
    public static bool TryGetFallbackCost(uint abilityId, uint abilityModuleId, out float amount)
    {
        amount = 0f;
        if (!FallbackCosts.TryGetValue(abilityId, out var fallback))
        {
            return false;
        }

        if (abilityModuleId != 0 && abilityModuleId != fallback.AbilityModuleId)
        {
            return false;
        }

        amount = fallback.Amount;
        return amount > 0f;
    }

    /// <summary>
    /// Returns true when an effective server-side energy-spend command exists
    /// anywhere in the ability's reachable aptitude graph. This follows chain
    /// links, logic branches/loops, called abilities, and all chains attached
    /// to status effects to avoid double charging a data-driven activation.
    /// </summary>
    public static bool HasDataDrivenEnergySpend(uint abilityId)
    {
        var visitedAbilities = new HashSet<uint>();
        var visitedChains = new HashSet<uint>();
        var visitedEffects = new HashSet<uint>();
        return HasEnergySpendInAbility(abilityId, visitedAbilities, visitedChains, visitedEffects);
    }

    private static bool HasEnergySpendInAbility(
        uint abilityId,
        HashSet<uint> visitedAbilities,
        HashSet<uint> visitedChains,
        HashSet<uint> visitedEffects)
    {
        if (abilityId == 0 || !visitedAbilities.Add(abilityId))
        {
            return false;
        }

        var ability = SDBInterface.GetAbilityData(abilityId);
        return ability != null
            && HasEnergySpendInChain(ability.Chain, visitedAbilities, visitedChains, visitedEffects);
    }

    private static bool HasEnergySpendInChain(
        uint chainId,
        HashSet<uint> visitedAbilities,
        HashSet<uint> visitedChains,
        HashSet<uint> visitedEffects)
    {
        if (chainId == 0 || !visitedChains.Add(chainId))
        {
            return false;
        }

        uint next = chainId;
        var visitedCommands = new HashSet<uint>();
        while (next != 0 && visitedCommands.Add(next))
        {
            var baseCommand = SDBInterface.GetBaseCommandDef(next);
            if (baseCommand == null)
            {
                return false;
            }

            var commandType = SDBInterface.GetCommandType(baseCommand.Subtype);
            if (commandType == null)
            {
                next = baseCommand.Next;
                continue;
            }

            if (HasEnergySpendInCommand(
                    baseCommand.Id,
                    (AptCommandType)commandType.Id,
                    visitedAbilities,
                    visitedChains,
                    visitedEffects))
            {
                return true;
            }

            next = baseCommand.Next;
        }

        return false;
    }

    private static bool HasEnergySpendInCommand(
        uint commandId,
        AptCommandType commandType,
        HashSet<uint> visitedAbilities,
        HashSet<uint> visitedChains,
        HashSet<uint> visitedEffects)
    {
        switch (commandType)
        {
            case AptCommandType.ConsumeEnergy:
                {
                    var command = SDBInterface.GetConsumeEnergyCommandDef(commandId);
                    return command != null && (command.Amount > 0f || command.AmountRegop != 0);
                }

            case AptCommandType.ConsumeEnergyOverTime:
                {
                    var command = SDBInterface.GetConsumeEnergyOverTimeCommandDef(commandId);
                    return command != null && (command.Amount > 0f || command.AmountRegop != 0);
                }

            case AptCommandType.RequireEnergyByRange:
                {
                    var command = SDBInterface.GetRequireEnergyByRangeCommandDef(commandId);
                    return command != null
                        && command.AlsoConsume == 1
                        && (command.MinEnergy > 0f || command.AmountRegop != 0);
                }

            case AptCommandType.ConditionalBranch:
                {
                    var command = SDBInterface.GetConditionalBranchCommandDef(commandId);
                    return command != null
                        && (HasEnergySpendInChain(command.IfChain, visitedAbilities, visitedChains, visitedEffects)
                            || HasEnergySpendInChain(command.ThenChain, visitedAbilities, visitedChains, visitedEffects)
                            || HasEnergySpendInChain(command.ElseChain, visitedAbilities, visitedChains, visitedEffects));
                }

            case AptCommandType.LogicOr:
                {
                    var command = SDBInterface.GetLogicOrCommandDef(commandId);
                    return command != null
                        && (HasEnergySpendInChain(command.AChain, visitedAbilities, visitedChains, visitedEffects)
                            || HasEnergySpendInChain(command.BChain, visitedAbilities, visitedChains, visitedEffects));
                }

            case AptCommandType.LogicOrChain:
                {
                    var command = SDBInterface.GetLogicOrChainCommandDef(commandId);
                    return command != null
                        && HasEnergySpendInChain(command.OrChain, visitedAbilities, visitedChains, visitedEffects);
                }

            case AptCommandType.LogicAndChain:
                {
                    var command = SDBInterface.GetLogicAndChainCommandDef(commandId);
                    return command != null
                        && HasEnergySpendInChain(command.AndChain, visitedAbilities, visitedChains, visitedEffects);
                }

            case AptCommandType.LogicNegate:
                {
                    var command = SDBInterface.GetLogicNegateCommandDef(commandId);
                    return command != null
                        && HasEnergySpendInChain(command.NegateChain, visitedAbilities, visitedChains, visitedEffects);
                }

            case AptCommandType.WhileLoop:
                {
                    var command = SDBInterface.GetWhileLoopCommandDef(commandId);
                    return command != null
                        && (HasEnergySpendInChain(command.ConditionChain, visitedAbilities, visitedChains, visitedEffects)
                            || HasEnergySpendInChain(command.BodyChain, visitedAbilities, visitedChains, visitedEffects));
                }

            case AptCommandType.Call:
                {
                    var command = SDBInterface.GetCallCommandDef(commandId);
                    return command != null
                        && HasEnergySpendInAbility(command.AbilityId, visitedAbilities, visitedChains, visitedEffects);
                }

            case AptCommandType.UpdateWaitAndFireOnce:
                {
                    var command = SDBInterface.GetUpdateWaitAndFireOnceCommandDef(commandId);
                    return command != null
                        && HasEnergySpendInChain(command.Chain, visitedAbilities, visitedChains, visitedEffects);
                }

            case AptCommandType.ImpactApplyEffect:
                {
                    var command = SDBInterface.GetImpactApplyEffectCommandDef(commandId);
                    return command != null
                        && HasEnergySpendInEffect(command.EffectId, visitedAbilities, visitedChains, visitedEffects);
                }

            case AptCommandType.ApplyClientStatusEffect:
                {
                    var command = SDBInterface.GetApplyClientStatusEffectCommandDef(commandId);
                    return command != null
                        && HasEnergySpendInEffect(command.StatusEffectId, visitedAbilities, visitedChains, visitedEffects);
                }

            case AptCommandType.RemoveClientStatusEffect:
                {
                    var command = SDBInterface.GetRemoveClientStatusEffectCommandDef(commandId);
                    return command != null
                        && HasEnergySpendInEffect(command.StatusEffectId, visitedAbilities, visitedChains, visitedEffects);
                }

            case AptCommandType.ImpactRemoveEffect:
                {
                    var command = CustomDBInterface.GetImpactRemoveEffectCommandDef(commandId);
                    return command?.EffectId is uint effectId
                        && HasEnergySpendInEffect(effectId, visitedAbilities, visitedChains, visitedEffects);
                }

            case AptCommandType.ImpactToggleEffect:
                {
                    var command = SDBInterface.GetImpactToggleEffectCommandDef(commandId);
                    return command != null
                        && (HasEnergySpendInChain(command.PreApplyChain, visitedAbilities, visitedChains, visitedEffects)
                            || HasEnergySpendInEffect(command.EffectId, visitedAbilities, visitedChains, visitedEffects));
                }

            case AptCommandType.StagedActivation:
                {
                    var command = SDBInterface.GetStagedActivationCommandDef(commandId);
                    return command != null
                        && HasEnergySpendInEffect(command.SelfEffectId, visitedAbilities, visitedChains, visitedEffects);
                }

            default:
                return false;
        }
    }

    private static bool HasEnergySpendInEffect(
        uint effectId,
        HashSet<uint> visitedAbilities,
        HashSet<uint> visitedChains,
        HashSet<uint> visitedEffects)
    {
        if (effectId == 0 || !visitedEffects.Add(effectId))
        {
            return false;
        }

        var effect = SDBInterface.GetStatusEffectData(effectId);
        return effect != null
            && (HasEnergySpendInChain(effect.ApplyChain, visitedAbilities, visitedChains, visitedEffects)
                || HasEnergySpendInChain(effect.RemoveChain, visitedAbilities, visitedChains, visitedEffects)
                || HasEnergySpendInChain(effect.UpdateChain, visitedAbilities, visitedChains, visitedEffects)
                || HasEnergySpendInChain(effect.DurationChain, visitedAbilities, visitedChains, visitedEffects));
    }
}
