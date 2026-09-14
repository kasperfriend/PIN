using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServer.Entities.Deployable;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.Systems.Emotes;

namespace GameServer.Systems.Ai;

/// <summary>
///     Joins a monster's restFunction to DeployableFunction.name, then to a LIVE deployable with
///     that function. Template offsets, mission markers and unplaced deployables are not locations.
///     Reservations are per shard and exclusive; one chair/work point never seats a crowd.
/// </summary>
public sealed class SdbNpcActivityWorld : INpcActivityWorld
{
    private readonly IShard _shard;
    private readonly EmoteService _emotes;
    private readonly Func<uint, Deployable> _deployables;
    private readonly Func<uint, DeployableFunction> _functions;
    private readonly int _defaultDurationMs;
    private readonly Dictionary<ulong, ulong> _owners = [];
    private readonly Dictionary<ulong, ulong> _byNpc = [];
    private readonly object _gate = new();

    public SdbNpcActivityWorld(
        IShard shard,
        EmoteService emotes,
        NpcRoutineRules rules = null,
        Func<uint, Deployable> deployables = null,
        Func<uint, DeployableFunction> functions = null)
    {
        _shard = shard ?? throw new ArgumentNullException(nameof(shard));
        _emotes = emotes ?? throw new ArgumentNullException(nameof(emotes));
        _deployables = deployables ?? SDBInterface.GetDeployable;
        _functions = functions ?? SDBInterface.GetDeployableFunction;
        _defaultDurationMs = Math.Max(0, (rules ?? new NpcRoutineRules()).WorkDurationMs);
    }

    public bool TryReserve(
        ulong npcId, string function, Vector3 position, Vector3 home, float radius, ulong previousSpot,
        out NpcActivitySpot spot)
    {
        spot = default;
        if (npcId == 0 || string.IsNullOrWhiteSpace(function) || !float.IsFinite(radius) || radius <= 0f)
        {
            return false;
        }

        // Deterministic ties. Prefer a different station after completing one, but allow reuse if
        // this is the only available point. Only the rare destination decision enumerates entities.
        var candidates = new List<(NpcActivitySpot Spot, float Distance)>();
        foreach (var entity in _shard.Entities.Values)
        {
            if (entity is not DeployableEntity deployable || !TryDescribe(deployable, function, out var candidate))
            {
                continue;
            }

            float fromHome = AiVectors.HorizontalDistance(home, candidate.Position);
            float distance = Vector3.DistanceSquared(position, candidate.Position);
            if (float.IsFinite(distance) && fromHome <= radius &&
                AiVectors.HeightDelta(position, candidate.Position) <= 1.25f)
            {
                candidates.Add((candidate, distance));
            }
        }

        lock (_gate)
        {
            ReleaseCore(npcId);
            foreach (var candidate in candidates
                .OrderBy(x => x.Spot.EntityId == previousSpot)
                .ThenBy(x => x.Distance)
                .ThenBy(x => x.Spot.EntityId))
            {
                ulong id = candidate.Spot.EntityId;
                if (_owners.ContainsKey(id) || !IsPresent(candidate.Spot))
                {
                    continue;
                }

                _owners[id] = npcId;
                _byNpc[npcId] = id;
                spot = candidate.Spot;
                return true;
            }
        }

        return false;
    }

    public bool IsValid(ulong npcId, in NpcActivitySpot spot)
    {
        lock (_gate)
        {
            return _owners.TryGetValue(spot.EntityId, out ulong owner) && owner == npcId && IsPresent(spot);
        }
    }

    public void Release(ulong npcId)
    {
        lock (_gate)
        {
            ReleaseCore(npcId);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _owners.Clear();
            _byNpc.Clear();
        }
    }

    private void ReleaseCore(ulong npcId)
    {
        if (_byNpc.Remove(npcId, out ulong spotId))
        {
            _owners.Remove(spotId);
        }
    }

    private bool IsPresent(in NpcActivitySpot spot)
        => _shard.Entities.TryGetValue(spot.EntityId, out var entity) &&
           entity is DeployableEntity { IsDead: false, Owner: null, Encounter: null } deployable &&
           !deployable.IsPlayerOwned && deployable.Position == spot.Position && deployable.Orientation == spot.Orientation;

    private bool TryDescribe(DeployableEntity entity, string function, out NpcActivitySpot spot)
    {
        spot = default;
        // Do not take a player's deployed object, a vehicle seat or an encounter-owned interaction.
        if (entity.IsDead || entity.Owner != null || entity.IsPlayerOwned || entity.Encounter != null)
        {
            return false;
        }

        var row = _deployables(entity.Type);
        if (row == null || !string.Equals(_functions(row.Function)?.Name, function, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var behavior = NpcBehaviorParams.Parse(row.Behavior);
        bool holster = behavior.Name.Equals("DynamicEmoteHolstered", StringComparison.OrdinalIgnoreCase);
        if (!holster && !behavior.Name.Equals("DynamicEmote", StringComparison.OrdinalIgnoreCase) &&
            !behavior.Name.Equals("PerformEmote", StringComparison.OrdinalIgnoreCase))
        {
            // WaterPather, queue/conversation scripts, statusEffect-only posts and interaction abilities
            // need their own semantics. Matching the function alone is not permission to invent them.
            return false;
        }

        ushort emote = _emotes.ResolveEmoteName(behavior.EmoteName);
        if (emote == EmoteService.NoEmote)
        {
            return false;
        }

        int duration = _defaultDurationMs;
        if (behavior.Values.ContainsKey("emoteDuration") &&
            (!behavior.TryGetInt("emoteDuration", out duration) || duration < -1))
        {
            return false;
        }

        ushort endEmote = behavior.Values.TryGetValue("endWithEmote", out string endName)
            ? _emotes.ResolveEmoteName(endName) : EmoteService.NoEmote;
        var orientation = entity.Orientation;
        if (!float.IsFinite(orientation.LengthSquared()) || MathF.Abs(orientation.LengthSquared() - 1f) > 0.01f)
        {
            return false;
        }

        spot = new NpcActivitySpot(entity.EntityId, entity.Position, orientation, emote, duration, holster, endEmote);
        return true;
    }
}
