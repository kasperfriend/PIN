namespace GameServer.StaticDB.Records.customdata;

/// <summary>
///     A boost the character carries independently of the status effect system: an XP, crystite
///     (resource) or reputation percentage for a duration, or permanently. <see cref="EffectId" /> is
///     the optional status effect that shows the boost on the character (particles, emissive); the
///     numbers live in the boost itself.
/// </summary>
public record ApplyPermanentEffectCommandDef : ICommandDef
{
    public uint Id { get; set; }
    public uint EffectId { get; set; }
    public uint DurationSeconds { get; set; }

    /// <summary>"xp", "crystite" or "reputation".</summary>
    public string BoostType { get; set; } = string.Empty;

    /// <summary>The bonus in percent (50 = +50%).</summary>
    public uint Percent { get; set; }

    public byte Permanent { get; set; }

    /// <summary>Legacy spelling of an XP <see cref="Percent" /> used by the first hand-authored rows.</summary>
    public uint ExperienceBoost { get; set; }
}
