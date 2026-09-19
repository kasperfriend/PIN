using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Aero.Protocol;
using AeroMessages.GSS.Character;
using AeroMessages.GSS.Character.Command;
using AeroMessages.GSS.Character.Event;
using GameServer.Entities.Character;
using GameServer.Extensions;
using GameServer.Packets;
using GameServer.StaticDB;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Combat;
using Serilog;

namespace GameServer.Controllers.Character;

[Typecode(GssCharacterView.CombatController)]
public class CombatController : Base
{
    private ILogger _logger;

    public override void Init(INetworkClient client, IPlayer player, IShard shard, ILogger logger)
    {
        _logger = logger.ForContext<CharacterEntity>();
    }

    [MessageID(GssCharacterCommand.FireInputIgnored)]
    public void FireInputIgnored(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        // Deliberate no-op: the client only reports inputs IT dropped (out of ammo,
        // cooling weapon, suppressed), so the server has no state to correct here.
        // Consuming the packet keeps the report out of the unhandled-command log.
    }

    [MessageID(GssCharacterCommand.FireBurst)]
    public void FireBurst(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        if (!CharacterWeaponFire.ShouldFireEquippedWeapon(player.CharacterEntity))
        {
            return;
        }

        var query = packet.Unpack<FireBurst>();
        player.CharacterEntity.SetFireBurst(query.Time);
    }

    [MessageID(GssCharacterCommand.FireWeaponProjectile)]
    public void FireWeaponProjectile(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        if (!CharacterWeaponFire.ShouldFireEquippedWeapon(player.CharacterEntity))
        {
            return;
        }

        var fireWeaponProjectile = packet.Unpack<FireWeaponProjectile>();

        Vector3? shooterVelocity = fireWeaponProjectile.HaveShooterVelocity == 1 ? fireWeaponProjectile.ShooterVelocity : null;
        player.HandleFireWeaponProjectile(fireWeaponProjectile.Time, fireWeaponProjectile.AimDirection, shooterVelocity);

        var weaponProjectileFired = new WeaponProjectileFired
        {
            ShortTime = (ushort)fireWeaponProjectile.Time,
            Aim = fireWeaponProjectile.AimDirection,
            HaveShooterVelocity = fireWeaponProjectile.HaveShooterVelocity,
            ShooterVelocity = fireWeaponProjectile.ShooterVelocity
        };

        client.NetChannels[ChannelType.ReliableGss].SendMessage(weaponProjectileFired, player.CharacterEntity.EntityId);

        // The shooter already has the echo above. Other clients scoped into this character
        // only draw the tracer if they get WeaponProjectileFired too - the same event NPC
        // fire announces to every watcher. exceptOwner keeps the gunner from receiving it
        // twice (SendToScoped includes the character's own client).
        ProjectileFiredAnnouncement.SendToWatchers(
            player.CharacterEntity.Shard,
            player.CharacterEntity,
            weaponProjectileFired,
            exceptOwner: true);
    }

    [MessageID(GssCharacterCommand.FireEnd)]
    public void FireEnd(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        if (!CharacterWeaponFire.ShouldFireEquippedWeapon(player.CharacterEntity))
        {
            return;
        }

        var query = packet.Unpack<FireEnd>();
        player.CharacterEntity.SetFireEnd(query.Time);
    }

    [MessageID(GssCharacterCommand.FireCancel)]
    public void FireCancel(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        if (!CharacterWeaponFire.ShouldFireEquippedWeapon(player.CharacterEntity))
        {
            return;
        }

        var query = packet.Unpack<FireCancel>();
        player.CharacterEntity.SetFireCancel(query.Time);
    }

    [MessageID(GssCharacterCommand.UseScope)]
    public void UseScope(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<UseScope>();

        // InScope is a signed byte, any non-zero value means "scoped in".
        // Casting it straight to byte would turn -1 into 255 and make the client disagree with the
        // server about the fire mode, which plays the scope-in animation and then snaps back to hip
        // fire: the field has to carry the same 0/1 the client's own scoped state uses.
        bool inScope = query.InScope != 0;

        var character = player.CharacterEntity;
        if (unchecked((int)(query.Time - character.FireMode_1.Time)) < 0)
        {
            _logger.Debug("[Scope] Ignored stale UseScope InScope={InScope} Time={ClientTime} Latest={LatestTime}",
                query.InScope, query.Time, character.FireMode_1.Time);
            return;
        }

        character.SetScopedState(inScope, query.Time);

        var effect = character.GetActiveEffects().FirstOrDefault(state => state?.Effect.Id == character.ScopeStatusEffectId);
        _logger.Debug(
            "[Scope] UseScope InScope={InScope} Time={ClientTime} ServerTime={ServerTime} Weapon={WeaponIndex} " +
            "FireMode_0={FireMode} FireMode_1={ScopedMode} Effect={EffectId} EffectTime={EffectTime} " +
            "MoveState={MoveState} CombatFlags={CombatFlags}",
            query.InScope, query.Time, character.Shard.CurrentTime, character.WeaponIndex.Index,
            character.FireMode_0.Mode, character.FireMode_1.Mode, character.ScopeStatusEffectId, effect?.Time,
            character.MovementStateContainer.Movestate, character.CombatFlags.Value);
    }

    [MessageID(GssCharacterCommand.SelectWeapon)]
    public void SelectWeapon(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<SelectWeapon>();

        // Switching weapons (or fire modes) never keeps the sights of the previous one: without this, a scope
        // effect whose "scope out" message got lost would hold the zoom over the whole next weapon.
        if (player.CharacterEntity.FireMode_1.Mode != 0)
        {
            _logger.Debug("[Scope] Reset by SelectWeapon Index={WeaponIndex} Time={ClientTime}", query.SelectedWeaponIndex, query.Time);
        }

        player.CharacterEntity.SetScopedState(false, query.Time);

        player.CharacterEntity.SetWeaponIndex(new WeaponIndexData
        {
            Index = query.SelectedWeaponIndex,
            Unk1 = query.Unk3,
            Unk2 = 0,
            Time = query.Time,
        });
    }

    [MessageID(GssCharacterCommand.SelectFireMode)]
    public void SelectFireMode(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<SelectFireMode>();
        if (player.CharacterEntity.FireMode_1.Mode != 0)
        {
            _logger.Debug("[Scope] Reset by SelectFireMode Mode={FireMode} Time={ClientTime}", query.FireMode, query.Time);
        }

        player.CharacterEntity.SetScopedState(false, query.Time);

        player.CharacterEntity.SetFireMode(0, new FireModeData
        {
           Mode = query.FireMode,
           Time = query.Time,
        });
    }

    [MessageID(GssCharacterCommand.ReloadWeapon)]
    public void ReloadWeapon(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<ReloadWeapon>();
        player.CharacterEntity.SetWeaponReloaded(query.Time);
    }

    [MessageID(GssCharacterCommand.CancelReload)]
    public void CancelReload(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<CancelReload>();
        player.CharacterEntity.SetWeaponReloadCancelled(query.Time);
    }

    [MessageID(GssCharacterCommand.ActivateConsumable)]
    public void ActivateConsumable(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<ActivateConsumable>();
        _logger.Information("ActivateConsumable {ItemSdbId}", query?.ItemSdbId);
        if (query == null)
        {
            return;
        }

        var abilityModule = SDBInterface.GetAbilityModule(query.ItemSdbId);
        if (abilityModule == null)
        {
            return;
        }

        uint abilityId = abilityModule.AbilityChainId;
        if (abilityId != 0)
        {
            var character = player.CharacterEntity;
            var activationTime = query.Time;
            var shard = character.Shard;

            // The activation costs one copy of the consumable (the aptitude chain's ConsumeItem
            // command); refuse it up front when the player does not have any, so a spammed hot
            // key cannot run free effects.
            if (player.Inventory != null && !player.Inventory.HasItemOrResource(query.ItemSdbId))
            {
                _logger.Information("ActivateConsumable {ItemSdbId} from {Player} refused: the player does not have that consumable", query.ItemSdbId, character);
                SendAbilityActivationResponse(character, abilityId, activationTime, activated: false);
                return;
            }

            var initiator = character as IAptitudeTarget;
            var targets = new AptitudeTargets();

            bool success = shard.Abilities.HandleActivateAbility(shard, initiator, abilityId, activationTime, targets, abilityModuleId: query.ItemSdbId);
            if (character.IsPlayerControlled)
            {
                SendAbilityActivationResponse(character, abilityId, activationTime, success);
            }
        }
    }

    [MessageID(GssCharacterCommand.ActivateAbility)]
    public void ActivateAbility(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var activateAbility = packet.Unpack<ActivateAbility>();
        _logger.Information("ActivateAbility Slot {AbilitySlotIndex}", activateAbility?.AbilitySlotIndex);
        if (activateAbility == null)
        {
            return;
        }

        // Get the ability id based on the slotted ability
        var abilitySlot = activateAbility.AbilitySlotIndex;
        var character = player.CharacterEntity;
        uint abilityId = 0;
        uint moduleId = 0;

        byte abilityCategory = 0;

        // Using the local data until we can get the loadout remotely
        if (character.CurrentLoadout != null)
        {
            moduleId = character.CurrentLoadout.GetAbilityModuleIdBySlotIndex(abilitySlot);
            if (moduleId != 0)
            {
                var abilityModule = SDBInterface.GetAbilityModule(moduleId);
                if (abilityModule != null)
                {
                    abilityId = abilityModule.AbilityChainId;
                    abilityCategory = abilityModule.UiCategory;
                }
                else if (abilitySlot is 16 or 17)
                {
                    // Slots 16 (vehicle, V) and 17 (glider, T) hold the vehicle/glider
                    // item, not an ability module, so there is no AbilityModule record
                    // to find here.
                    _logger.Debug("ActivateAbility slot {AbilitySlotIndex}: id {ModuleId} is the vehicle/glider item, not an ability module", abilitySlot, moduleId);
                }
                else
                {
                    _logger.Warning("ActivateAbility slot {AbilitySlotIndex}: module {ModuleId} has no AbilityModule SDB record", abilitySlot, moduleId);
                }
            }
        }

        // Frame-default buttons: the server-side loadout has no per-slot default
        // abilities. The char-create loadout rows (SDB ccsl, loadouts 287-298) slot
        // ability modules only into slots 6/7/8/9 (HKM and the three ability buttons)
        // plus gear; every other button starts empty, and an empty slot means
        // "no ability" - which the client already knows from its replicated loadout.
        // The battleframe's built-in kit lives in the ability GROUPS as passives
        // (sprint, jetpack, core triggers), not as button abilities, and the jetpack
        // permission is granted at spawn. Med systems are module-only (slotted by the
        // player). The genuinely built-in buttons are Interact (E -> 187) and SIN
        // targeting (F -> 43); every other empty slot stays unresolved on purpose.
        // Slots 16 (vehicle, V) and 17 (glider, T) hold the vehicle/glider item rather
        // than an ability module, and the calldown ability chains (e.g. 34571
        // "Convoy - Calldown") carry no AbilityModule rows, so no vehicle/glider
        // ability can be derived from loadout data - a documented gap, not a lookup bug.
        if (abilityId == 0)
        {
            if (abilitySlot == 4) // AbilityInteract - Default button E
            {
                abilityId = 187; // Interact
            }
            else if (abilitySlot == 13) // AbilitySIN - Default button F
            {
                abilityId = 43; // 40? SIN Targetting
            }
        }

        if (abilityId == 0 && moduleId != 0)
        {
            if (abilitySlot is 16 or 17)
            {
                _logger.Debug("ActivateAbility slot {AbilitySlotIndex}: vehicle/glider item {ModuleId} has no ability chain on the server (calldown wiring gap)", abilitySlot, moduleId);
            }
            else
            {
                _logger.Warning("ActivateAbility slot {AbilitySlotIndex}: module {ModuleId} did not resolve to an ability id (AbilityChainId is 0 or missing)", abilitySlot, moduleId);
            }
        }

        _logger.Information("ActivateAbility slot {AbilitySlotIndex}: module {ModuleId} resolved to ability {AbilityId} (category {AbilityCategory})", abilitySlot, moduleId, abilityId, abilityCategory);

        if (abilityId != 0)
        {
            var activationTime = activateAbility.Time;
            var shard = character.Shard;
            var initiator = character as IAptitudeTarget;

            // Server-side cooldown gate: while the ability is cooling down,
            // reject the activation instead of running the chain again.
            if (!shard.Abilities.IsAbilityReady(initiator, abilityId, abilityCategory, activationTime, out uint readyAgainTime))
            {
                _logger.Information("ActivateAbility slot {AbilitySlotIndex} (ability {AbilityId}) rejected: cooling down until {ReadyAgainTime}", abilitySlot, abilityId, readyAgainTime);
                if (character.IsPlayerControlled)
                {
                    SendAbilityActivationResponse(character, abilityId, activationTime, activated: false);
                }

                return;
            }

            var targets = activateAbility.Targets
            .Select(entityId =>
            {
                shard.Entities.TryGetValue(entityId.Backing & 0xffffffffffffff00, out var target);
                return target as IAptitudeTarget;
            })
            .Where(target => target != null)
            .ToArray();

            bool success = shard.Abilities.HandleActivateAbility(shard, initiator, abilityId, activationTime, new AptitudeTargets(targets), abilityModuleId: moduleId);

            // The client holds the button for channelled activations (the E-key interaction is the
            // flagship case) and reports the release with DeactivateAbility. Register the held
            // activation so ActivationDuration duration gates can fail it when the key goes up.
            if (success)
            {
                shard.Abilities.BeginAbilityActivation(initiator, abilityId);
            }

            if (character.IsPlayerControlled)
            {
                SendAbilityActivationResponse(character, abilityId, activationTime, success);
            }
        }
    }

    [MessageID(GssCharacterCommand.DeactivateAbility)]
    public void DeactivateAbility(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var deactivateAbility = packet.Unpack<DeactivateAbility>();
        if (deactivateAbility == null)
        {
            return;
        }

        var character = player.CharacterEntity;
        var shard = character.Shard;

        // Resolve the slot the same way ActivateAbility does: a loadout module first, then the
        // fixed default slots (E key -> interact ability 187, SIN -> 43).
        uint abilityId = 0;
        byte abilitySlot = deactivateAbility.AbilitySlotIndex;
        if (character.CurrentLoadout != null)
        {
            uint moduleId = character.CurrentLoadout.GetAbilityModuleIdBySlotIndex(abilitySlot);
            if (moduleId != 0)
            {
                var abilityModule = SDBInterface.GetAbilityModule(moduleId);
                if (abilityModule != null)
                {
                    abilityId = abilityModule.AbilityChainId;
                }
            }
        }

        if (abilityId == 0)
        {
            if (abilitySlot == 4)
            {
                abilityId = 187; // Interact
            }
            else if (abilitySlot == 13)
            {
                abilityId = 43; // SIN Targetting
            }
        }

        if (abilityId != 0)
        {
            shard.Abilities.HandleDeactivateAbility(character, abilityId);
        }
    }

    private static AbilityCooldownsData BuildAbilityCooldownsData(IShard shard, AbilityState state, uint activationTime)
    {
        uint now = shard.CurrentTime;

        var group1 = new List<ActiveCooldown>();
        var group2 = new List<ActiveCooldown>();

        // The cooldown entries are kept in shard time, so the global cooldown
        // window has to be expressed in shard time too - mixing in the
        // client-supplied activation time makes the client timer jump.
        uint globalReadyAgain = now + 300;
        foreach (var entry in state.Cooldowns)
        {
            if (!entry.IsActive(now))
            {
                continue;
            }

            switch (entry.Kind)
            {
                case AbilityCooldownKind.Local:
                    group1.Add(entry.ToActiveCooldown());
                    break;
                case AbilityCooldownKind.Category:
                    group2.Add(entry.ToActiveCooldown());
                    break;
                case AbilityCooldownKind.Global:
                    globalReadyAgain = Math.Max(globalReadyAgain, entry.ReadyAgainTime);
                    break;
                default:
                    break;
            }
        }

        return new AbilityCooldownsData
        {
            ActiveCooldowns_Group1 = group1.ToArray(),
            ActiveCooldowns_Group2 = group2.ToArray(),
            Unk = 0,
            GlobalCooldown_Activated_Time = now,
            GlobalCooldown_ReadyAgain_Time = globalReadyAgain,
        };
    }

    /// <summary>
    /// Acknowledges an ability activation (or its failure) with the cooldown
    /// payload the client uses to show the ability timer and gate re-use.
    /// </summary>
    private void SendAbilityActivationResponse(CharacterEntity character, uint abilityId, uint activationTime, bool activated)
    {
        var shard = character.Shard;
        var state = shard.Abilities.GetOrAddState(character);
        var cooldownsData = BuildAbilityCooldownsData(shard, state, activationTime);

        if (activated)
        {
            var message = new AbilityActivated
            {
                ActivatedAbilityId = abilityId,
                ActivatedTime = activationTime,
                AbilityCooldownsData = cooldownsData,
            };
            _logger.ForContext<AbilitySystem>()
                   .Information("AbilityActivated {ActivatedAbilityId} at {ActivatedTime}", message.ActivatedAbilityId, message.ActivatedTime);
            character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(message, character.EntityId);
        }
        else
        {
            var message = new AbilityFailed
            {
                FailedAbilityId = abilityId,
                Unk2 = 0, // 0 in captures
                AbilityCooldownsData = cooldownsData,
            };
            _logger.ForContext<AbilitySystem>()
                   .Information("AbilityFailed {FailedAbilityId} at {ActivationTime}", message.FailedAbilityId, activationTime);
            character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(message, character.EntityId);
        }
    }
}