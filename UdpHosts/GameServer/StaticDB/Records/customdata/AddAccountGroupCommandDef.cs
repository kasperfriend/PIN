namespace GameServer.StaticDB.Records.customdata;

/// <summary>Puts the owner into a named account group (VIP, an LGV rental, ...) for a while or for good.</summary>
public record AddAccountGroupCommandDef : ICommandDef
{
    public uint Id { get; set; }
    public string Group { get; set; } = string.Empty;

    /// <summary>How long the membership lasts; 0 is permanent.</summary>
    public uint DurationSeconds { get; set; }
}
