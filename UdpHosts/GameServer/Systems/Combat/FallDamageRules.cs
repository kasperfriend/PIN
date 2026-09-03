namespace GameServer.Systems.Combat;

/// <summary>
///     Tuning knobs for the fall damage system. Impact speed is the downward
///     velocity (world units / second, Z-up) the character carried right before
///     landing, as reported by the client's movement samples.
/// </summary>
public interface IFallDamageRules
{
    /// <summary>Fall damage is computed only when this is true.</summary>
    bool Enabled { get; }

    /// <summary>Impacts at or below this speed are harmless.</summary>
    float SafeImpactSpeed { get; }

    /// <summary>Impacts at or above this speed are instantly lethal.</summary>
    float LethalImpactSpeed { get; }

    /// <summary>Damage dealt per unit of impact speed beyond <see cref="SafeImpactSpeed" />.</summary>
    int DamagePerSpeed { get; }

    /// <summary>Falls shorter than this air time never deal damage (protects against state jitter).</summary>
    float MinAirTimeMs { get; }
}

/// <summary>
///     Default fall damage tuning. The server simulation runs with gravity
///     (0, 0, -8), so an impact speed of 12 u/s corresponds to roughly a 9u
///     drop and 48 u/s to roughly a 144u drop without air resistance.
/// </summary>
public class StandardFallDamageRules : IFallDamageRules
{
    public bool Enabled { get; init; } = true;

    public float SafeImpactSpeed { get; init; } = 12f;

    public float LethalImpactSpeed { get; init; } = 48f;

    public int DamagePerSpeed { get; init; } = 130;

    public float MinAirTimeMs { get; init; } = 250f;
}
