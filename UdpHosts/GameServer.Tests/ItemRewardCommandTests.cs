using System;
using System.Collections.Generic;
using System.Linq;
using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.customdata;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Effect;
using GameServer.Systems.Aptitude.Commands.Other;
using GameServer.Systems.Aptitude.Commands.Self;
using GameServer.Systems.Aptitude.Commands.Unlock;
using GameServer.Systems.Loot;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The reward-side item commands - the ones that were placeholders returning <c>true</c> - run
///     against a live inventory and unlock ledger, in the shapes the shipped data uses: a title kit
///     with no <c>ConsumeItem</c> of its own, a fragment stack that pays for a component, a crate that
///     rolls a loot table, a rental contract, and a boost that expires.
/// </summary>
public sealed class ItemRewardCommandTests : IDisposable
{
    private const uint TitleKit = 90001;      // consumable (type 7)
    private const uint Fragment = 90002;      // consumable stack
    private const uint Component = 90003;     // consumable stack
    private const uint Crate = 90004;         // consumable
    private const uint Rifle = 90005;         // weapon (guid item)
    private const uint Booster = 90006;       // powerup (type 9)
    private const uint CrateTable = 7000;

    public ItemRewardCommandTests()
    {
        PlayerRewards.LookupItem = sdbId => sdbId switch
        {
            Rifle => new RootItem { SdbId = sdbId, Type = (byte)ItemType.Weapon },
            Booster => new RootItem { SdbId = sdbId, Type = (byte)ItemType.Powerup },
            _ => new RootItem { SdbId = sdbId, Type = (byte)ItemType.Consumable },
        };
        PlayerRewards.RollLoot = tableId => tableId == CrateTable ? [new LootAward(Rifle, 1, CrateTable), new LootAward(Component, 3, CrateTable)] : [];
    }

    public void Dispose()
    {
        PlayerRewards.LookupItem = SDBInterface.GetRootItem;
        PlayerRewards.RollLoot = LootTableRoller.Roll;
    }

    [Fact]
    public void UnlockTitles_SpendsTheKitOnceAndRecordsTheTitle()
    {
        var (context, character, inventory) = CreateActivation(TitleKit);
        inventory.AddResource(TitleKit, 2);
        var command = new UnlockTitlesCommand(new UnlockTitlesCommandDef { Id = 1, TitleId = 114 });

        Assert.True(command.Execute(context));
        Assert.True(command.Execute(Context.CopyContext(context))); // a second node in the same activation

        Assert.Equal(1u, inventory.GetResourceQuantity(TitleKit));
        Assert.True(character.Unlocks.Has(CharacterUnlocks.Titles, 114));
        Assert.Contains(character, context.DirtyUnlocks);
    }

    [Fact]
    public void UnlockTitles_WithoutTheKit_Fails()
    {
        var (context, character, _) = CreateActivation(TitleKit);

        Assert.False(new UnlockTitlesCommand(new UnlockTitlesCommandDef { Id = 1, TitleId = 114 }).Execute(context));
        Assert.False(character.Unlocks.Has(CharacterUnlocks.Titles, 114));
    }

    [Fact]
    public void Unlock_RolledBack_RemovesItAgain()
    {
        var (context, character, inventory) = CreateActivation(TitleKit);
        inventory.AddResource(TitleKit, 1);

        Assert.True(new UnlockTitlesCommand(new UnlockTitlesCommandDef { Id = 1, TitleId = 114 }).Execute(context));
        foreach (var rollback in Enumerable.Reverse(context.ActivationRollbacks))
        {
            rollback();
        }

        Assert.False(character.Unlocks.Has(CharacterUnlocks.Titles, 114));
        Assert.Equal(1u, inventory.GetResourceQuantity(TitleKit));
    }

    [Fact]
    public void GrantOwnerItem_FragmentsPayForTheComponent()
    {
        var (context, _, inventory) = CreateActivation(Fragment);
        inventory.AddResource(Fragment, 60);
        var command = new GrantOwnerItemCommand(new GrantOwnerItemCommandDef { Id = 1, ItemSdbId = Component, Quantity = 1, CostSdbId = Fragment, CostQuantity = 50 });

        Assert.True(command.Execute(context));

        Assert.Equal(10u, inventory.GetResourceQuantity(Fragment)); // the stack is the cost; no extra "one more" spent
        Assert.Equal(1u, inventory.GetResourceQuantity(Component));
        Assert.Single(context.AwardedItems);
    }

    [Fact]
    public void GrantOwnerItem_ShortOnFragments_GrantsNothing()
    {
        var (context, _, inventory) = CreateActivation(Fragment);
        inventory.AddResource(Fragment, 20);

        Assert.False(new GrantOwnerItemCommand(new GrantOwnerItemCommandDef { Id = 1, ItemSdbId = Component, CostSdbId = Fragment, CostQuantity = 50 }).Execute(context));

        Assert.Equal(20u, inventory.GetResourceQuantity(Fragment));
        Assert.Equal(0u, inventory.GetResourceQuantity(Component));
    }

    [Fact]
    public void SpawnLoot_RollsTheTableIntoTheBag()
    {
        var (context, _, inventory) = CreateActivation(Crate);
        inventory.AddResource(Crate, 1);
        context.ActivatingItemConsumed = true; // the crate chains carry their own ConsumeItem

        Assert.True(new SpawnLootCommand(new SpawnLootCommandDef { Id = 1, LootTableId = CrateTable }).Execute(context));

        Assert.Equal(1, inventory.CountItemsBySdbId(Rifle));
        Assert.Equal(3u, inventory.GetResourceQuantity(Component));
        Assert.Equal(1u, inventory.GetResourceQuantity(Crate)); // already accounted for by ConsumeItem
        Assert.Equal(2, context.AwardedItems.Count);
    }

    [Fact]
    public void GrantedItems_AreTakenBackOnRollback()
    {
        var (context, _, inventory) = CreateActivation(Crate);
        context.ActivatingItemConsumed = true;
        Assert.True(new SpawnLootCommand(new SpawnLootCommandDef { Id = 1, LootTableId = CrateTable }).Execute(context));

        foreach (var rollback in Enumerable.Reverse(context.ActivationRollbacks))
        {
            rollback();
        }

        Assert.Equal(0, inventory.CountItemsBySdbId(Rifle));
        Assert.Equal(0u, inventory.GetResourceQuantity(Component));
    }

    [Fact]
    public void AddAccountGroup_RentalExpiresAndExtends()
    {
        var (context, character, inventory) = CreateActivation(Booster);
        inventory.AddResource(Booster, 2);
        var command = new AddAccountGroupCommand(new AddAccountGroupCommandDef { Id = 1, Group = "lgv_rental", DurationSeconds = 3600 });
        ulong now = PlayerRewards.UnixNow();

        Assert.True(command.Execute(context));
        Assert.True(character.Unlocks.IsInAccountGroup("lgv_rental", now));
        Assert.False(character.Unlocks.IsInAccountGroup("lgv_rental", now + 3601));

        // A second contract stacks onto the remaining time rather than replacing it.
        var (second, _, _) = CreateActivation(Booster, character, inventory);
        Assert.True(command.Execute(second));
        Assert.True(character.Unlocks.IsInAccountGroup("lgv_rental", now + 3601));
        Assert.Equal(0u, inventory.GetResourceQuantity(Booster));
    }

    [Fact]
    public void PermanentEffect_BoostIsAppliedRemovedAndExpired()
    {
        var (context, character, inventory) = CreateActivation(Booster);
        inventory.AddResource(Booster, 1);
        ulong now = PlayerRewards.UnixNow();

        Assert.True(new ApplyPermanentEffectCommand(new ApplyPermanentEffectCommandDef { Id = 1, EffectId = 4331, BoostType = CharacterUnlocks.BoostReputation, Percent = 50, DurationSeconds = 600 }).Execute(context));
        Assert.Equal(0.5f, character.Unlocks.BoostFraction(CharacterUnlocks.BoostReputation, now), 3);
        Assert.Equal(0u, inventory.GetResourceQuantity(Booster));

        Assert.True(character.Unlocks.Expire(now + 601));
        Assert.Equal(0f, character.Unlocks.BoostFraction(CharacterUnlocks.BoostReputation, now + 601), 3);

        character.Unlocks.AddBoost(CharacterUnlocks.BoostXp, 10, 0, permanent: true, effectId: 3509, now);
        var (cleanse, _, _) = CreateActivation(0, character, inventory);
        Assert.True(new RemovePermanentEffectCommand(new RemovePermanentEffectCommandDef { Id = 2, EffectId = 3509 }).Execute(cleanse));
        Assert.Empty(character.Unlocks.Boosts);
    }

    [Fact]
    public void Unlocks_RoundTripThroughRecords()
    {
        var unlocks = new CharacterUnlocks();
        ulong now = 1_000_000;
        unlocks.Unlock(CharacterUnlocks.Warpaints, 77);
        unlocks.AddAccountGroup("vip", 100, now);
        unlocks.AddBoost(CharacterUnlocks.BoostCrystite, 25, 100, permanent: false, effectId: 4332, now);
        unlocks.AddReputation(5, 40);

        var restored = new CharacterUnlocks();
        restored.LoadRecords(unlocks.ToRecords(), now + 50);

        Assert.True(restored.Has(CharacterUnlocks.Warpaints, 77));
        Assert.True(restored.IsInAccountGroup("vip", now + 50));
        Assert.Equal(0.25f, restored.BoostFraction(CharacterUnlocks.BoostCrystite, now + 50), 3);
        Assert.Equal(40, restored.Reputation[5]);

        // Expired entries are dropped on load.
        var later = new CharacterUnlocks();
        later.LoadRecords(unlocks.ToRecords(), now + 200);
        Assert.False(later.IsInAccountGroup("vip", now + 200));
        Assert.Empty(later.Boosts);
        Assert.True(later.Has(CharacterUnlocks.Warpaints, 77));
    }

    [Fact]
    public void LootTableRoller_HonoursRollModes()
    {
        var tables = new Dictionary<uint, LootTable>
        {
            [1] = new() { Id = 1, RollMode = 0 },                              // weighted, one winner
            [2] = new() { Id = 2, RollMode = 2, StackDuplicateResults = 1 },   // every row
        };
        var items = new Dictionary<uint, IReadOnlyList<LootTableItemDist>>
        {
            [1] = [new() { LootTableId = 1, ItemdropId = 10, Probability = 100, MinQuantity = 1, MaxQuantity = 1 }, new() { LootTableId = 1, ItemdropId = 11, Probability = 0, MinQuantity = 1, MaxQuantity = 1 }],
            [2] = [new() { LootTableId = 2, ItemdropId = 20, Probability = 100, MinQuantity = 2, MaxQuantity = 2 }, new() { LootTableId = 2, ItemdropId = 20, Probability = 100, MinQuantity = 1, MaxQuantity = 1 }],
        };
        var subTables = new Dictionary<uint, IReadOnlyList<LootTableSubTableDist>> { [2] = [new() { LootTableId = 2, SubtableId = 1, Probability = 100 }] };
        var random = new Random(1);

        var weighted = LootTableRoller.Roll(1, random, id => tables.GetValueOrDefault(id), id => items.GetValueOrDefault(id) ?? [], id => subTables.GetValueOrDefault(id) ?? []);
        Assert.Equal([10u], weighted.Select(a => a.SdbId));

        var all = LootTableRoller.Roll(2, random, id => tables.GetValueOrDefault(id), id => items.GetValueOrDefault(id) ?? [], id => subTables.GetValueOrDefault(id) ?? []);
        Assert.Equal(2, all.Count); // 20 x3 stacked, plus the sub-table's 10
        Assert.Equal(3u, all.Single(a => a.SdbId == 20).Quantity);
        Assert.Contains(all, a => a.SdbId == 10);
    }

    private static (Context Context, CharacterEntity Character, CharacterInventory Inventory) CreateActivation(uint itemSdbId, CharacterEntity existing = null, CharacterInventory existingInventory = null)
    {
        var shard = existing?.Shard as FakeShard ?? new FakeShard { CurrentTimeLong = 60_000 };
        var character = existing;
        var inventory = existingInventory;
        if (character == null)
        {
            character = FakeCharacterFactory.Create(shard);
            shard.EntityMan.Add(character.EntityId, character);
            var player = new FakeNetworkPlayer(shard) { CharacterEntity = character };
            character.SetControllingPlayer(player);
            player.AttachRealChannels(); // effect removal and unlock/boost updates send on ReliableGss
            inventory = new CharacterInventory(shard, null, character);
            player.Inventory = inventory;
        }

        var context = new Context(shard, character) { AbilityModuleId = itemSdbId };
        context.Targets.Push(character);
        return (context, character, inventory);
    }
}
