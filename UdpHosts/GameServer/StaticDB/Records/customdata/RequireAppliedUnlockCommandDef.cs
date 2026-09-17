namespace GameServer.StaticDB.Records.customdata;

public record RequireAppliedUnlockCommandDef : ICommandDef
{
    public uint Id { get; set; }
    public string UnlockType { get; set; } = string.Empty;
    public uint UnlockId { get; set; }
    public byte Negate { get; set; }
}
