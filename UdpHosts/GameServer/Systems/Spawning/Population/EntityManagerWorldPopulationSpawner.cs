using System;
using System.Collections.Generic;
using System.Numerics;
using GameServer.Entities.Character;
using Serilog;

namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     Spawns world population through the shard's entity manager, the same path an authored
///     <c>character_spawn.json</c> entry takes, so a population NPC is an ordinary NPC everywhere
///     else in the server: it has a physics body, an AI brain, loot, hostility and a scope.
/// </summary>
/// <remarks>
///     Two differences from an authored spawn, both deliberate:
///     <list type="bullet">
///         <item>
///             The position is not snapped again. The placement that produced it already probed the
///             ground with a short window (see <see cref="PhysicsWorldPopulationTerrain"/>), while
///             <see cref="Systems.EntityManager.EntityManager.SpawnCharacter"/>'s own snap searches
///             10 km down and would move a validated spot under a bridge or a roof onto the
///             structure above it.
///         </item>
///         <item>
///             The scope-in is distance filtered. <see cref="Systems.EntityManager.EntityManager"/>
///             introduces a new entity to <b>every</b> connected client (its own comment calls that a
///             temporary hack), which is fine for the handful of entities a zone loads at startup and
///             wasteful for a system whose whole job is spawning hundreds of them: a mob 4 km away
///             would be sent to a player who is then told to forget it at the next scope check.
///         </item>
///     </list>
/// </remarks>
public sealed class EntityManagerWorldPopulationSpawner : IWorldPopulationSpawner
{
    private readonly IShard _shard;
    private readonly ILogger _logger;

    /// <summary>Rows that threw while being spawned, so each is reported once rather than per attempt.</summary>
    private readonly HashSet<uint> _rowsThatThrew = [];

    public EntityManagerWorldPopulationSpawner(IShard shard)
    {
        _shard = shard;
        _logger = shard.Logger.ForContext<EntityManagerWorldPopulationSpawner>();
    }

    public ulong Spawn(uint monsterId, Vector3 position, Quaternion orientation, byte level)
    {
        if (_shard.EntityMan == null)
        {
            return 0;
        }

        try
        {
            var character = _shard.EntityMan.SpawnCharacter(
                monsterId,
                position,
                orientation: orientation,
                level: level,
                snapToGround: false,
                scopeToNearbyClientsOnly: true);

            return character?.EntityId ?? 0;
        }
        catch (Exception ex)
        {
            // This system offers every monster row in the database to the entity manager, so a row
            // it cannot build - a chassis with no shape, a weapon table entry that is missing - is
            // an expected outcome rather than a bug in the caller. It must not take the shard's tick
            // down with it: the row is reported once, answered with a 0, and the slot that asked for
            // it is parked by the service after a few of those.
            if (_rowsThatThrew.Add(monsterId))
            {
                _logger.Warning(
                    ex,
                    "World population: spawning dbcharacter::Monster row {MonsterId} threw, so the row is treated as unspawnable ({RowCount} such rows so far)",
                    monsterId,
                    _rowsThatThrew.Count);
            }

            return 0;
        }
    }

    public bool IsAlive(ulong entityId) => entityId != 0 && _shard.Entities.ContainsKey(entityId);

    public void Despawn(ulong entityId)
    {
        if (entityId == 0 || !_shard.Entities.TryGetValue(entityId, out var entity))
        {
            return;
        }

        // Guard the removal against a bookkeeping mistake of this system's own: the only thing world
        // population may ever remove is an NPC it spawned. A player character is not one, whatever
        // id it carries.
        if (entity is not CharacterEntity { IsPlayerControlled: false })
        {
            _logger.Warning(
                "World population tried to despawn entity {EntityId}, which is not an NPC it owns; leaving it alone",
                entityId);
            return;
        }

        try
        {
            _shard.EntityMan.Remove(entityId);
        }
        catch (Exception ex)
        {
            // A removal that throws halfway through must not abort the clear it was part of: the
            // rest of the world's NPCs still have to come out.
            _logger.Warning(ex, "World population: removing NPC {EntityId} threw", entityId);
        }
    }
}
