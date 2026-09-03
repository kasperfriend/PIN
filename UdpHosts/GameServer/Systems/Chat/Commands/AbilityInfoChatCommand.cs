using System;
using System.Collections.Generic;
using System.Text;
using GameServer.Data;
using GameServer.StaticDB;
using GameServer.Systems.Aptitude;
using AptCommandType = GameServer.Systems.Aptitude.CommandType;

namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Inspect how the current loadout slots map to ability modules and dump their aptitude chains", "abilityinfo [abilityId]", "abilityinfo", "abi", "ability", "aptitudeinfo")]
public class AbilityInfoChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        var character = context.SourcePlayer?.CharacterEntity;
        if (character == null)
        {
            SourceFeedback("Requires a player character", context);
            return;
        }

        var loadout = character.CurrentLoadout;
        if (loadout == null)
        {
            SourceFeedback("Current loadout is null", context);
            return;
        }

        if (parameters.Length >= 1 && uint.TryParse(parameters[0], out uint requestedAbilityId))
        {
            var single = new StringBuilder();
            single.AppendLine($"=== Ability {requestedAbilityId} chain ===");
            AppendAbilityChain(single, requestedAbilityId, context.Shard.Abilities.Factory);
            AppendAbilityState(single, context, character);
            context.SourcePlayer.SendDebugLog(single.ToString());
            SourceFeedback("Ability chain info printed to console", context);
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"=== Loadout ability slots (chassis {loadout.ChassisID}) ===");
        foreach (KeyValuePair<AbilitySlotType, LoadoutSlotType> pair in CharacterLoadout.AbilityToLoadoutSlotMap)
        {
            AppendSlotInfo(builder, context, loadout, pair.Key, pair.Value);
        }

        AppendAbilityState(builder, context, character);

        context.SourcePlayer.SendDebugLog(builder.ToString());
        SourceFeedback("Ability info printed to console", context);
    }

    private static void AppendSlotInfo(StringBuilder sb, ChatCommandContext context, CharacterLoadout loadout, AbilitySlotType abilitySlot, LoadoutSlotType loadoutSlot)
    {
        uint moduleId = loadout.SlottedItems.GetValueOrDefault(loadoutSlot);
        if (moduleId == 0)
        {
            sb.AppendLine($"Slot {abilitySlot} (loadout {loadoutSlot}): empty");
            return;
        }

        var module = SDBInterface.GetAbilityModule(moduleId);
        if (module == null)
        {
            sb.AppendLine($"Slot {abilitySlot}: module {moduleId} has NO dbitems::AbilityModule record");
            return;
        }

        sb.AppendLine($"Slot {abilitySlot} (loadout {loadoutSlot}): module {moduleId} (ModuleType {module.ModuleType}, UiCategory {module.UiCategory}, alt-fire mask {module.WeaponAltfireMask}) -> ability {module.AbilityChainId}");
        if (module.AbilityChainId != 0)
        {
            AppendAbilityChain(sb, module.AbilityChainId, context.Shard.Abilities.Factory);
        }
    }

    private static void AppendAbilityChain(StringBuilder sb, uint abilityId, Factory factory)
    {
        var ability = SDBInterface.GetAbilityData(abilityId);
        if (ability == null)
        {
            sb.AppendLine($"  NO apt::AbilityData record for ability {abilityId}");
            return;
        }

        uint chainId = ability.Chain;
        if (chainId == 0)
        {
            sb.AppendLine($"  Ability {abilityId} has no chain (Chain = 0)");
            return;
        }

        sb.AppendLine($"  Ability {abilityId} -> chain {chainId}");
        uint next = chainId;
        int index = 0;
        while (next != 0)
        {
            var baseDef = SDBInterface.GetBaseCommandDef(next);
            if (baseDef == null)
            {
                sb.AppendLine($"  [{index}] chain node {next}: missing BaseCommandDef");
                break;
            }

            var typeRecord = SDBInterface.GetCommandType(baseDef.Subtype);
            string environment = typeRecord?.Environment ?? "unknown-env";
            string typeName = typeRecord != null ? ((AptCommandType)typeRecord.Id).ToString() : baseDef.Subtype.ToString();
            string runtime = DescribeRuntimeClass(factory, baseDef.Id, baseDef.Subtype, typeRecord?.Environment);
            sb.AppendLine($"  [{index}] cmd {baseDef.Id} | type {baseDef.Subtype} {typeName} | env {environment} | {runtime}");
            AppendCommandParams(sb, baseDef.Id, baseDef.Subtype);
            next = baseDef.Next;
            index++;
        }
    }

    private static void AppendCommandParams(StringBuilder sb, uint cmdId, uint subtype)
    {
        string prefix = "      params: ";
        switch ((AptCommandType)subtype)
        {
            case AptCommandType.TimeCooldown:
                if (SDBInterface.GetTimeCooldownCommandDef(cmdId) is { } tcd)
                {
                    sb.AppendLine($"{prefix}dur {tcd.Duration}ms cat {tcd.Category} checkLocal {tcd.CheckLocal} checkCat {tcd.CheckCategory} checkGlobal {tcd.CheckGlobal}");
                }

                break;
            case AptCommandType.InflictCooldown:
                if (SDBInterface.GetInflictCooldownCommandDef(cmdId) is { } icd)
                {
                    sb.AppendLine($"{prefix}local {icd.LocalCooldown}ms(precool {icd.LocalCooldownPrecoolCount}) cat {icd.CategoryCooldown}ms(catId {icd.Category}, precool {icd.CategoryCooldownPrecoolCount}) global {icd.GlobalCooldown}ms");
                }

                break;
            case AptCommandType.RequireEnergy:
                if (SDBInterface.GetRequireEnergyCommandDef(cmdId) is { } red)
                {
                    sb.AppendLine($"{prefix}amount {red.Amount} regop {red.AmountRegop} negate {red.Negate}");
                }

                break;
            case AptCommandType.ConsumeEnergy:
                if (SDBInterface.GetConsumeEnergyCommandDef(cmdId) is { } ced)
                {
                    sb.AppendLine($"{prefix}amount {ced.Amount} regop {ced.AmountRegop} onTargets {ced.OnTargets} overcharge {ced.AllowOvercharge} predict {ced.AllowPrediction}");
                }

                break;
            case AptCommandType.EnergyToDamage:
                if (SDBInterface.GetEnergyToDamageCommandDef(cmdId) is { } etd)
                {
                    sb.AppendLine($"{prefix}energyRequired {etd.EnergyRequired} maxAllowed {etd.MaxEnergyAllowed} energyPerPoint {etd.EnergyPerPoint} dmgType {etd.DamageType}");
                }

                break;
            case AptCommandType.FireProjectile:
                if (SDBInterface.GetFireProjectileCommandDef(cmdId) is { } fpd)
                {
                    sb.AppendLine($"{prefix}ammo {fpd.Ammotype} dmg {fpd.Damage}(regop {fpd.DamageRegop}, useWeaponDmg {fpd.UseWeaponDamage}) range {fpd.Range}(regop {fpd.RangeRegop}) spread {fpd.Spread} burst {fpd.Burstcount} hardpoint {fpd.Hardpoint} aimAtTarget {fpd.AimAtTarget} gravity {fpd.AimWithGravity} aimOffset '{fpd.AimOffset}' originOffset '{fpd.AimOriginOffset}'");
                }

                break;
            case AptCommandType.InflictDamage:
                if (SDBInterface.GetInflictDamageCommandDef(cmdId) is { } idd)
                {
                    sb.AppendLine($"{prefix}dmg {idd.Damagepoints}(regop {idd.DamagepointsRegop}) splash {idd.Splashrange}(regop {idd.SplashrangeRegop}) pointBlank {idd.Pointblankrange} falloff {idd.Falloff} fromInitiatorPos {idd.Frominitiatorpos} dmgType {idd.DamageType} weaponDmg {idd.Weapondamage} usedmgdealt {idd.Usedmgdealt}");
                }

                break;
            case AptCommandType.HealDamage:
                if (SDBInterface.GetHealDamageCommandDef(cmdId) is { } hdd)
                {
                    sb.AppendLine($"{prefix}heal {hdd.Healpoints}(regop {hdd.HealpointsRegop}) usedmgdealt {hdd.Usedmgdealt} dmgType {hdd.DamageType}");
                }

                break;
            case AptCommandType.ApplyClientStatusEffect:
                if (SDBInterface.GetApplyClientStatusEffectCommandDef(cmdId) is { } ace)
                {
                    sb.AppendLine($"{prefix}effect {ace.StatusEffectId} applyToSelf {ace.ApplyToSelf} useTargetClients {ace.UseTargetClients}");
                }

                break;
            case AptCommandType.RemoveClientStatusEffect:
                if (SDBInterface.GetRemoveClientStatusEffectCommandDef(cmdId) is { } rce)
                {
                    sb.AppendLine($"{prefix}effect {rce.StatusEffectId} applyToSelf {rce.ApplyToSelf} useTargetClients {rce.UseTargetClients} forcePrediction {rce.ForcePrediction}");
                }

                break;
            default:
                break;
        }
    }

    private static string DescribeRuntimeClass(Factory factory, uint commandId, uint subtype, string environment)
    {
        if (environment == "client")
        {
            return "CLIENT-ONLY (server does nothing)";
        }

        try
        {
            var command = factory.LoadCommand(commandId, subtype);
            string typeName = command.GetType().Name;
            if (typeName == "CustomNOOPCommand")
            {
                return "resolves to CLIENT-ONLY no-op";
            }

            if (typeName == "CustomPlaceholderCommand")
            {
                return "NOT IMPLEMENTED server-side (placeholder)";
            }

            return $"implemented -> {typeName}";
        }
        catch (Exception ex)
        {
            return $"factory error ({ex.GetType().Name}: {ex.Message})";
        }
    }

    private static void AppendAbilityState(StringBuilder sb, ChatCommandContext context, GameServer.Entities.Character.CharacterEntity character)
    {
        if (!context.Shard.Abilities.TryGetState(character, out var state))
        {
            sb.AppendLine("No server AbilityState yet (created on first activation/energy use)");
            return;
        }

        sb.AppendLine($"Server AbilityState: energy {state.Energy:F1}/{state.MaxEnergy:F1}, regen {state.EnergyRegenPerSecond:F1}/s");
        if (state.Cooldowns.Length == 0)
        {
            sb.AppendLine("  no active cooldowns");
            return;
        }

        uint now = context.Shard.CurrentTime;
        foreach (var entry in state.Cooldowns)
        {
            string stateText = entry.IsActive(now) ? $"active until {entry.ReadyAgainTime}" : "expired";
            sb.AppendLine($"  cooldown {entry.Kind} ability {entry.AbilityId} category {entry.Category} {stateText}");
        }
    }
}
