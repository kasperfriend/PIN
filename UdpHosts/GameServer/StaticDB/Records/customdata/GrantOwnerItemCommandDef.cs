namespace GameServer.StaticDB.Records.customdata;

/// <summary>
///     Hands the owner an item, optionally in exchange for another one (a Turbo kit takes the plain
///     LGV, a fragment stack of fifty becomes one component, a locker takes its key), or rolls a loot
///     table for them instead (the secure lockers).
/// </summary>
public record GrantOwnerItemCommandDef : ICommandDef
{
    public uint Id { get; set; }
    public uint ItemSdbId { get; set; }
    public uint Quantity { get; set; } = 1;
    public uint CostSdbId { get; set; }
    public uint CostQuantity { get; set; } = 1;
    public uint LootTableId { get; set; }
}
