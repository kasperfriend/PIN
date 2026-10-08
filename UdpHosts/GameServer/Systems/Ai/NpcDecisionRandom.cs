using System;
using AiPrng = GameServer.Systems.PRNG.PRNG;

namespace GameServer.Systems.Ai;

/// <summary>
///     Reproducible PIN decision noise, not recovered CAIS randomness. Unlike a projectile round
///     index, an NPC's low entity byte is a controller discriminator: mix the whole id and clock
///     before using the shared PRNG, so different bodies do not share every chance roll.
/// </summary>
public static class NpcDecisionRandom
{
    /// <summary>A finite roll in [0,1), keyed by the body, action and decision opportunity.</summary>
    public static float Roll(ulong entityId, uint actionId, ulong currentTime)
    {
        ulong seed = unchecked(entityId ^ ((ulong)actionId << 32) ^ (currentTime * 0x9E3779B97F4A7C15UL));
        seed = unchecked((seed ^ (seed >> 30)) * 0xBF58476D1CE4E5B9UL);
        seed = unchecked((seed ^ (seed >> 27)) * 0x94D049BB133111EBUL);
        seed ^= seed >> 31;
        // PRNG.Float converts uint to float and can round its largest output up to 1.
        return MathF.Min(AiPrng.Float(AiPrng.Trace((uint)seed, (byte)(seed >> 32))), MathF.BitDecrement(1f));
    }
}
