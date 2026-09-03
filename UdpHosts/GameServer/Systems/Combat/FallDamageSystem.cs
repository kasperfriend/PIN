using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using AeroMessages.Common;
using AeroMessages.GSS;
using AeroMessages.GSS.Character;
using AeroMessages.GSS.Character.Event;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Enums;
using Serilog;

namespace GameServer.Systems.Combat;

/// <summary>
///     Tracks player controlled characters while they are airborne and applies
///     fall damage when they land, based on the fastest downward speed observed
///     during the fall. Movement in Firefall is client authoritative, so this
///     hooks into the movement input relay where the client reports its pose,
///     velocity and movestate.
/// </summary>
public class FallDamageSystem
{
    /// <summary>
    ///     A landing is only trusted when it closely follows the last airborne
    ///     sample. If more time than this passed, the character was most likely
    ///     teleported/respawned and the tracked impact speed is stale.
    /// </summary>
    public const float LandingGraceMs = 150f;

    private static readonly ILogger Logger = Log.ForContext<FallDamageSystem>();

    private readonly IShard _shard;
    private readonly DamageSystem _damage;
    private readonly IFallDamageRules _rules;
    private readonly IDictionary<ulong, FallTracker> _trackers = new ConcurrentDictionary<ulong, FallTracker>();

    public FallDamageSystem(IShard shard, DamageSystem damage, IFallDamageRules rules)
    {
        _shard = shard;
        _damage = damage;
        _rules = rules;
    }

    /// <summary>
    ///     Feeds a movement pose into the fall tracker. Call for every movement
    ///     input a (player controlled) character produces, before the pose is
    ///     confirmed back to the client.
    /// </summary>
    public void OnMovementInput(CharacterEntity character, MovementPoseData poseData)
    {
        if (character == null || _rules == null || !_rules.Enabled)
        {
            return;
        }

        // Only characters the server does not simulate itself take fall damage;
        // NPC poses never flow through here, but keep the guard anyway.
        if (!character.IsPlayerControlled)
        {
            return;
        }

        if (!character.Alive || !character.IsAlive)
        {
            _trackers.Remove(character.EntityId);
            return;
        }

        var movestate = new MovementStateContainer { MovementStateValue = (ushort)poseData.PosRotState.MovementState }.Movestate;

        // Occupants of vehicles are moved by the vehicle, never take fall damage.
        // Ignore their samples entirely so mounting/dismounting cannot fake a landing.
        if (movestate == Movestate.Occupant)
        {
            return;
        }

        var tracker = _trackers.TryGetValue(character.EntityId, out var existing) ? existing : new FallTracker();
        if (!ReferenceEquals(tracker, existing))
        {
            _trackers[character.EntityId] = tracker;
        }

        if (IsAirborneMovestate(movestate))
        {
            tracker.Active = true;
            tracker.Airborne = true;
            tracker.LastAirborneAtMs = _shard.CurrentTimeLong;
            tracker.MaxFallSpeed = float.Max(tracker.MaxFallSpeed, -poseData.Velocity.Z);
            tracker.AirTimeMs = float.Max(tracker.AirTimeMs, MathF.Abs(poseData.GroundTimePositiveAirTimeNegative));

            if (IsThrusterMovestate(movestate))
            {
                tracker.UsedThrusterOrGlider = true;
            }

            if (movestate == Movestate.KnockdownFalling)
            {
                tracker.KnockdownFall = true;
            }
        }
        else if (tracker.Active && tracker.Airborne)
        {
            // First grounded sample after being airborne: this is a landing.
            bool freshLanding = (_shard.CurrentTimeLong - tracker.LastAirborneAtMs) <= LandingGraceMs;
            var context = BuildImpactContext(character, poseData, tracker);

            tracker.Active = false;
            tracker.Airborne = false;
            tracker.MaxFallSpeed = 0f;
            tracker.AirTimeMs = 0f;
            tracker.UsedThrusterOrGlider = false;
            tracker.KnockdownFall = false;

            if (freshLanding)
            {
                ApplyFallDamage(character, in context);
            }
        }
    }

    /// <summary>
    ///     Debug/testing helper that applies fall damage as if the character had
    ///     just landed with the given downward impact speed.
    /// </summary>
    public void SimulateLanding(CharacterEntity character, float impactSpeed)
    {
        if (character == null || _rules == null || !_rules.Enabled || !character.IsAlive)
        {
            return;
        }

        var context = new FallImpactContext(
            ImpactSpeed: impactSpeed,
            AirTimeMs: float.Max(_rules.MinAirTimeMs * 2f, 1000f),
            InWater: false,
            UsedThrusterOrGlider: false,
            KnockdownFall: false,
            ImmuneToFallDamage: character.HasCombatFlag(CombatFlagsData.CharacterCombatFlags.immune_falldamage));

        ApplyFallDamage(character, in context);
    }

    /// <summary>
    ///     Clears any in-progress fall tracking for a character. Call on respawn,
    ///     teleport and other server directed moves.
    /// </summary>
    public void ResetFor(CharacterEntity character)
    {
        if (character != null)
        {
            _trackers.Remove(character.EntityId);
        }
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return;
        }

        // Prune trackers for entities that went away (or were taken over by something else).
        foreach (var kvp in _trackers)
        {
            if (!_shard.Entities.TryGetValue(kvp.Key, out var entity) || entity is not CharacterEntity character || !character.IsPlayerControlled)
            {
                _trackers.Remove(kvp.Key);
            }
        }
    }

    private FallImpactContext BuildImpactContext(CharacterEntity character, MovementPoseData poseData, FallTracker tracker)
    {
        // WaterLevelAndDesc packs ddddllll: the low nibble is the water level at the character.
        bool inWater = (poseData.WaterLevelAndDesc & 0x0F) > 0;

        return new FallImpactContext(
            ImpactSpeed: tracker.MaxFallSpeed,
            AirTimeMs: tracker.AirTimeMs,
            InWater: inWater,
            UsedThrusterOrGlider: tracker.UsedThrusterOrGlider,
            KnockdownFall: tracker.KnockdownFall,
            ImmuneToFallDamage: character.HasCombatFlag(CombatFlagsData.CharacterCombatFlags.immune_falldamage));
    }

    private void ApplyFallDamage(CharacterEntity character, in FallImpactContext context)
    {
        var result = FallDamageMath.Evaluate(in context, _rules, character.GetCurrentStatModifierValue(StatModifierIdentifier.DamageTaken));
        if (result.Damage <= 0)
        {
            return;
        }

        // Lethal falls deal exactly enough damage to down the character, so the
        // hit feedback shows a sane number instead of the raw scaling result.
        int damage = result.Lethal ? character.CurrentHealth + character.CurrentShields : result.Damage;

        Logger.Information("{Name} hit the ground at {ImpactSpeed} u/s after {AirTime} ms, taking {Damage} fall damage{Lethal}",
            character, context.ImpactSpeed, context.AirTimeMs, damage, result.Lethal ? " (lethal)" : string.Empty);

        _damage.ApplyDamage(character, damage, character);

        SendTookHitFeedback(character, damage);
        character.Player?.SendDebugChat($"Fall damage: {damage} (impact {context.ImpactSpeed:0.#} u/s)");
    }

    private void SendTookHitFeedback(CharacterEntity character, int damage)
    {
        if (_shard.EntityMan == null)
        {
            return;
        }

        _shard.EntityMan.SendToScoped(character, new TookHit
        {
            HaveDamage = 1,
            DamageData = new DamageHitStruct
            {
                Target = character.AeroEntityId,
                HaveDealer = 0,
                Dealer = new EntityId(),
                DamageValue = damage,
            },
            DamageFlags = 0,
            ShortTime = _shard.CurrentShortTime,
            Unk2 = 0,
        });
    }

    private static bool IsAirborneMovestate(Movestate movestate)
    {
        return movestate is Movestate.Falling
            or Movestate.Jetpack
            or Movestate.JetpackSprint
            or Movestate.Glider
            or Movestate.GliderThrusters
            or Movestate.GliderStalling
            or Movestate.KnockdownFalling;
    }

    private static bool IsThrusterMovestate(Movestate movestate)
    {
        return movestate is Movestate.Jetpack
            or Movestate.JetpackSprint
            or Movestate.Glider
            or Movestate.GliderThrusters
            or Movestate.GliderStalling;
    }

    private sealed class FallTracker
    {
        public bool Active;
        public bool Airborne;
        public bool UsedThrusterOrGlider;
        public bool KnockdownFall;
        public float MaxFallSpeed;
        public float AirTimeMs;
        public ulong LastAirborneAtMs;
    }
}
