namespace GameServer.StaticDB.Records.customdata;

/// <summary>
///     Rolls loot tables into the owner's inventory. <see cref="LootTableId" /> is the single-table
///     form; <see cref="LootTableIds" /> rolls several (an upgrade kit rolls one piece per slot).
/// </summary>
public record SpawnLootCommandDef : ICommandDef
{
    public uint Id { get; set; }
    public uint LootTableId { get; set; }
    public uint[] LootTableIds { get; set; } = [];
    public byte RollEach { get; set; }
}
