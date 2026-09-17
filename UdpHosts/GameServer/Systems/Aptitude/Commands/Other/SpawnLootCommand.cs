using System.Collections.Generic;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     Rolls loot into the owner's bag: booster packs, caches, crates and the level upgrade kits. The
///     tables come from the command's row; a row with several tables rolls each of them (an upgrade kit
///     is one piece of gear per slot). The chains that carry it spend the container with their own
///     <c>ConsumeItem</c> node; the loot is undone with the rest of the activation if it fails later.
/// </summary>
public class SpawnLootCommand : Command, ICommand
{
    private SpawnLootCommandDef Params;

    public SpawnLootCommand(SpawnLootCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var tables = new List<uint>();
        if (Params.LootTableId != 0)
        {
            tables.Add(Params.LootTableId);
        }

        if (Params.LootTableIds != null)
        {
            tables.AddRange(Params.LootTableIds);
        }

        if (tables.Count == 0)
        {
            Logger.Warning("{Command} {CommandId} has no loot table authored yet (item {Item}); nothing spawned", nameof(SpawnLootCommand), Params.Id, context.AbilityModuleId);
            return true;
        }

        var character = PlayerRewards.OwnerOf(context);
        if (character?.Player?.Inventory == null)
        {
            Logger.Warning("{Command} {CommandId}: no player character to spawn loot for", nameof(SpawnLootCommand), Params.Id);
            return false;
        }

        if (!PlayerRewards.ConsumeActivatingItem(context, character))
        {
            return false;
        }

        int awarded = 0;
        foreach (var table in tables)
        {
            awarded += PlayerRewards.GrantLoot(context, character, table).Count;
        }

        if (awarded == 0)
        {
            Logger.Warning("{Command} {CommandId}: loot tables {Tables} awarded nothing to {Character}", nameof(SpawnLootCommand), Params.Id, tables, character);
        }

        return true;
    }
}
