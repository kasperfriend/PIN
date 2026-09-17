namespace GameServer.StaticDB.Records.customdata;

public record UnlockOrnamentsCommandDef : ICommandDef
{
    public uint Id { get; set; }
    public uint OrnamentId { get; set; }
}
