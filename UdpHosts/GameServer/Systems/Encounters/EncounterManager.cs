using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using AeroMessages.Common;
using AeroMessages.GSS.Character.Command;
using AeroMessages.GSS.Character.Event;
using AeroMessages.GSS.Generic;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Encounters.Encounters;

namespace GameServer.Systems.Encounters;

public class EncounterManager
{
    public Factory Factory;

    private const ulong _updateFlushIntervalMs = 40;
    private const ulong _lifetimeCheckIntervalMs = 1000;

    private readonly Dictionary<BaseEntity, IProximityHandler> _entitiesToCheckProximity = [];
    private readonly Dictionary<ulong, IEncounter> _uiQueries = [];
    private readonly HashSet<IEncounter> _encountersToUpdate = [];
    private readonly ConcurrentDictionary<ulong, Lifetime> _lifetimeByEncounter = new();

    private readonly Shard _shard;
    private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<EncounterManager>();
    private ulong _lastUpdateFlush;
    private ulong _lastLifetimeCheck;
    private bool _hasSpawnedZoneEncounters;

    public EncounterManager(Shard shard)
    {
        _shard = shard;
        Factory = new Factory(shard);
    }

    public void SendUiQuery(NewUiQuery uiQuery, INetworkPlayer target, IEncounter encounter)
    {
        _uiQueries.Add(uiQuery.QueryGuid, encounter);

        target.NetChannels[ChannelType.ReliableGss].SendMessage(uiQuery, target.CharacterEntity.EntityId);
    }

    public void HandleUiQueryResponse(UiQueryResponse uiQueryResponse, INetworkPlayer player)
    {
        if (_uiQueries.TryGetValue(uiQueryResponse.QueryGuid, out var encounter)
            && encounter is IDonationHandler donationHandler)
        {
            donationHandler.OnDonation(uiQueryResponse, player);

            _uiQueries.Remove(uiQueryResponse.QueryGuid);
        }
    }

    public Thumper CreateThumper(
        uint nodeType,
        Vector3 position,
        CharacterEntity owner,
        ResourceNodeBeaconCalldownCommandDef commandDef)
    {
        var thumperEntity = _shard.EntityMan.SpawnThumper(nodeType, position, owner, commandDef);
        if (thumperEntity == null || owner?.Player == null)
        {
            _logger.Error("CreateThumper: failed to spawn the thumper entity (nodeType {nodeType})", nodeType);
            return null;
        }

        // add squadmates later
        var thumper = new Thumper(
          _shard,
          _shard.GetNextGuid(),
          [owner.Player],
          thumperEntity);

        thumperEntity.Encounter = new EncounterComponent() { EncounterId = thumper.EntityId, Instance = thumper };

        Add(thumper.EntityId, thumper);

        return thumper;
    }

    public void SpawnZoneEncounters(uint zoneId)
    {
        // Isolate each definition: one malformed row must not abort the remaining spawns
        foreach (var entry in CustomDBInterface.GetZoneMeldingRepulsors(zoneId))
        {
            try
            {
                var guid = _shard.GetNextGuid((byte)Controller.Encounter);
                Add(guid, new MeldingRepulsor(_shard, guid, [], entry.Value));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SpawnZoneEncounters: failed to spawn melding repulsor {repulsorId}", entry.Key);
            }
        }

        foreach (var entry in CustomDBInterface.GetZoneLgvRaces(zoneId))
        {
            try
            {
                var t = entry.Value.Terminal;
                var terminal = _shard.EntityMan.SpawnDeployable(820, t.Position, t.Orientation);
                if (terminal != null)
                {
                    terminal.Encounter = new EncounterComponent() { SpawnDef = entry.Value, Events = EncounterComponent.Event.Interaction };
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SpawnZoneEncounters: failed to spawn lgv race terminal {raceId}", entry.Key);
            }
        }
    }

    public void SetRemainingLifetime(ICanTimeout encounter, uint timeMs)
    {
        var tracker = _lifetimeByEncounter.TryGetValue(encounter.EntityId, out var value) ? value : new Lifetime();

        tracker.ExpireAt = _shard.CurrentTimeLong + timeMs;
        _lifetimeByEncounter[encounter.EntityId] = tracker;
    }

    public void StartUpdatingEncounter(IEncounter encounter)
    {
        _encountersToUpdate.Add(encounter);
    }

    public void StopUpdatingEncounter(IEncounter encounter)
    {
        _encountersToUpdate.Remove(encounter);
    }

    public void AddCheckingOfProximity(BaseEntity entity, IProximityHandler encounter)
    {
        _entitiesToCheckProximity.Add(entity, encounter);
    }

    /// <summary>
    ///     Drops a player that left the shard from the participant sets of all live encounters: the sets
    ///     are strong references, so an entry would otherwise pin the player's whole object graph until
    ///     the encounter ends. The encounters themselves keep running — every encounter terminates on
    ///     its own schedule (a race through its lifetime timeout, a thumper through its state machine)
    ///     and cleans up its entities on the way out, at which point <see cref="Remove(IEncounter)" />
    ///     follows it through every registry. Removing them here instead would orphan the entities of
    ///     the types that only clean up in their success path.
    /// </summary>
    public void ForgetPlayer(INetworkPlayer player)
    {
        // ConcurrentDictionary enumeration tolerates encounters being removed while this runs.
        foreach (var encounter in _shard.Encounters.Values)
        {
            encounter.Participants.Remove(player);
        }
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (!_hasSpawnedZoneEncounters && currentTime != 0)
        {
            _hasSpawnedZoneEncounters = true;

            if (_shard.Settings.LoadZoneEntities)
            {
                SpawnZoneEncounters(_shard.ZoneId);
            }
        }

        if (currentTime > _lastUpdateFlush + _updateFlushIntervalMs)
        {
            _lastUpdateFlush = currentTime;

            foreach (var encounter in _encountersToUpdate)
            {
                // todo add update queue
                encounter.OnUpdate(currentTime);

                // FlushChanges(encounter);
            }

            foreach (var (entity, encounter) in _entitiesToCheckProximity)
            {
                foreach (var p in encounter.Participants)
                {
                    if (!_shard.EntityMan.HasScopedInEntity(entity.EntityId, p))
                    {
                        continue;
                    }

                    if (Vector3.Distance(entity.Position, p.CharacterEntity.Position) < entity.Encounter.ProximityDistance)
                    {
                        encounter.OnProximity(entity, p);
                    }
                }
            }
        }

        if (currentTime > _lastLifetimeCheck + _lifetimeCheckIntervalMs)
        {
            _lastLifetimeCheck = currentTime;

            foreach ((ulong entityId, Lifetime tracker) in _lifetimeByEncounter)
            {
                if (currentTime > tracker.ExpireAt)
                {
                    // Remove the tracker before (and regardless of) the timeout: the encounter it belongs
                    // to may already have been removed, and a tracker for a dead encounter was previously
                    // left in place — re-checked (and re-leaked) every second, forever. Removing first
                    // also keeps a tracker that OnTimeOut re-arms through SetRemainingLifetime.
                    _lifetimeByEncounter.TryRemove(entityId, out _);

                    if (_shard.Encounters.TryGetValue(entityId, out var e) && e is ICanTimeout encounter)
                    {
                        encounter.OnTimeOut();
                    }
                }
            }
        }
    }

    public void Add(ulong guid, IEncounter encounter)
    {
        _shard.Encounters.Add(guid, encounter);
        ScopeIn(encounter);
    }

    public void Remove(IEncounter encounter)
    {
        ScopeOut(encounter);
        ForgetEncounter(encounter);
    }

    public void Remove(ulong guid)
    {
        if (_shard.Encounters.TryGetValue(guid, out var encounter))
        {
            Remove(encounter);
        }
    }

    /// <summary>
    ///     Leaves every registry an encounter was entered in, not just the shard map. The update set, the
    ///     proximity checks, the outstanding UI queries and the lifetime tracker each hold a strong
    ///     reference, so an encounter that is removed from the shard map but stays in any of them is never
    ///     collected — and keeps ticking. A finished LGV race, for example, used to leak its finish-line
    ///     entity (and through the encounter's participant set, the player) into the proximity map, where
    ///     its distance checks ran every flush for the rest of the shard's life.
    /// </summary>
    private void ForgetEncounter(IEncounter encounter)
    {
        _encountersToUpdate.Remove(encounter);
        _lifetimeByEncounter.TryRemove(encounter.EntityId, out _);

        if (_entitiesToCheckProximity.Count > 0)
        {
            List<BaseEntity> staleProximityEntities = null;
            foreach (var (entity, handler) in _entitiesToCheckProximity)
            {
                if (handler == encounter)
                {
                    staleProximityEntities ??= new List<BaseEntity>();
                    staleProximityEntities.Add(entity);
                }
            }

            if (staleProximityEntities != null)
            {
                foreach (var entity in staleProximityEntities)
                {
                    _entitiesToCheckProximity.Remove(entity);
                }
            }
        }

        if (_uiQueries.Count > 0)
        {
            List<ulong> staleQueries = null;
            foreach (var (queryGuid, candidate) in _uiQueries)
            {
                if (candidate == encounter)
                {
                    staleQueries ??= new List<ulong>();
                    staleQueries.Add(queryGuid);
                }
            }

            if (staleQueries != null)
            {
                foreach (var queryGuid in staleQueries)
                {
                    _uiQueries.Remove(queryGuid);
                }
            }
        }

        _shard.Encounters.Remove(encounter.EntityId);
    }

    private void ScopeIn(IEncounter encounter)
    {
        if (encounter.View == null || encounter.View.GetPackedChangesSize() == 0)
        {
            return;
        }

        var size = encounter.View.GetPackedChangesSize();
        var serializedData = new Memory<byte>(new byte[size]);
        encounter.View.PackChanges(serializedData.Span);

        var msg = new EncounterUIScopeIn(size)
                  {
                      EncounterId = encounter.AeroEntityId,
                      Header = encounter.View.GetHeader(),
                      SinCard = [],
                      SchemaVersion = 2,
                      ShadowFieldValues = serializedData.ToArray()
                  };

        foreach (var player in encounter.Participants)
        {
            player.NetChannels[ChannelType.ReliableGss].SendMessage(msg, player.CharacterEntity.EntityId);
        }
    }

    private void FlushChanges(IEncounter encounter)
    {
        if (encounter.View == null || encounter.View.GetPackedChangesSize() == 0)
        {
            return;
        }

        var size = encounter.View.GetPackedChangesSize();
        var serializedData = new Memory<byte>(new byte[size]);
        encounter.View.PackChanges(serializedData.Span);

        var msg = new EncounterUIUpdate(size)
                  {
                      EncounterId = encounter.AeroEntityId,
                      ShadowFieldValues = serializedData.ToArray(),
                      BlobData = [],
                  };

        foreach (var player in encounter.Participants)
        {
            player.NetChannels[ChannelType.ReliableGss].SendMessage(msg, player.CharacterEntity.EntityId);
        }
    }

    private void ScopeOut(IEncounter encounter)
    {
        if (encounter.View == null)
        {
            return;
        }

        var msg = new EncounterUIScopeOut() { EncounterId = encounter.AeroEntityId };
        foreach (var player in encounter.Participants)
        {
            player.NetChannels[ChannelType.ReliableGss].SendMessage(msg, player.CharacterEntity.EntityId);
        }
    }

    private class Lifetime
    {
        public ulong ExpireAt;
    }
}