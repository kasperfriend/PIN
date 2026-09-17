namespace GameServer.StaticDB.Records.customdata;

/// <summary>Removes a boost by type, or every boost when no type is given (Polymorph Cleanse).</summary>
public record RemovePermanentEffectCommandDef : ICommandDef
{
    public uint Id { get; set; }
    public uint EffectId { get; set; }
    public string BoostType { get; set; } = string.Empty;
}
