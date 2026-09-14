using System.Numerics;

namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     Puts a planned NPC into the world and takes it out again. Split out from
///     <see cref="WorldPopulationService"/> so the service can be tested without an entity manager,
///     a physics engine or a database; the production implementation is
///     <see cref="EntityManagerWorldPopulationSpawner"/>.
/// </summary>
public interface IWorldPopulationSpawner
{
    /// <summary>
    ///     Spawns one NPC. Returns its entity id, or 0 when it could not be spawned (no such monster
    ///     row, no engine to spawn into) - a 0 is not an error the caller retries forever, it is a
    ///     refusal the caller reports.
    /// </summary>
    ulong Spawn(uint monsterId, Vector3 position, Quaternion orientation, byte level);

    /// <summary>
    ///     Whether an NPC this system spawned is still in the world. False once it died, was removed
    ///     by another system, or never existed - which is how the service notices a slot to refill.
    /// </summary>
    bool IsAlive(ulong entityId);

    /// <summary>
    ///     Removes an NPC this system spawned. Must do the full removal (physics body, AI
    ///     registration, scope out to the clients that were told about it), and must never remove
    ///     anything it did not spawn.
    /// </summary>
    void Despawn(ulong entityId);
}
