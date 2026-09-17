namespace GameServer.StaticDB.Records.customdata;

public record UnlockDecalsCommandDef : ICommandDef
{
    public uint Id { get; set; }
    public uint DecalId { get; set; }
}
