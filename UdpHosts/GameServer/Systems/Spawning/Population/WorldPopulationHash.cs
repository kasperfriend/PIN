namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     The deterministic pseudo-randomness the world population plan uses to spread its NPCs over a
///     cell, turn them away from each other and pick which row fills a cell next.
/// </summary>
/// <remarks>
///     <para>
///         Deterministic on purpose: a plan is built once per shard and its cells are activated and
///         deactivated many times as players come and go, and a cell that reshuffled its positions
///         every time would show the player a different world on every walk past. Seeding from the
///         cell's grid key and the slot's index means the same zone plans the same way on every
///         server, and a retry can ask for a different spot without any state to keep.
///     </para>
///     <para>
///         Not the server's <c>Systems/PRNG</c>: that one is a weapon spread model (its state is a
///         burst counter and its output is a cone), not a general purpose generator, and borrowing it
///         would couple spawn placement to ballistics tuning.
///     </para>
/// </remarks>
public static class WorldPopulationHash
{
    /// <summary>
    ///     Mixes a cell key and a salt into a 32 bit hash: FNV-1a over the key's halves and the salt,
    ///     finished with a multiply-xorshift avalanche so neighbouring keys do not give neighbouring
    ///     hashes.
    /// </summary>
    public static uint Mix(long key, int salt = 0)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)(key & 0xFFFFFFFFL)) * 16777619u;
            hash = (hash ^ (uint)(key >> 32)) * 16777619u;
            hash = (hash ^ (uint)salt) * 16777619u;
            hash ^= hash >> 15;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            hash *= 3266489917u;
            hash ^= hash >> 16;
            return hash;
        }
    }

    /// <summary>A float in [0, 1) taken from the top 24 bits of a hash.</summary>
    public static float Unit(uint hash) => (hash >> 8) * (1f / 16777216f);
}
