namespace GameServer.StaticDB.Records.customdata;

/// <summary>Marks an unlock as applied (the "applied" group), the flag <c>RequireAppliedUnlock</c> checks.</summary>
public record ApplyUnlockCommandDef : ICommandDef
{
    public uint Id { get; set; }
    public string UnlockType { get; set; } = string.Empty;
    public uint UnlockId { get; set; }
}
