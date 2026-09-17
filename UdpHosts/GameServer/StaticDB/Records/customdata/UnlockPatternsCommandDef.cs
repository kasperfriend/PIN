namespace GameServer.StaticDB.Records.customdata;

public record UnlockPatternsCommandDef : ICommandDef
{
    public uint Id { get; set; }
    public uint[] PatternIds { get; set; } = [];
}
