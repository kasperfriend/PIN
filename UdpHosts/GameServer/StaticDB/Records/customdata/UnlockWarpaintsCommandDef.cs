namespace GameServer.StaticDB.Records.customdata;

public record UnlockWarpaintsCommandDef : ICommandDef
{
    public uint Id { get; set; }
    public uint WarpaintId { get; set; }
}
