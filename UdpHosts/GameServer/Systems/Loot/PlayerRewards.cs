using System;
using System.Collections.Generic;
using System.Linq;
using Aero.Gen;
using AeroMessages.Common;
using AeroMessages.GSS.Character;
using AeroMessages.GSS.Character.Controller;
using AeroMessages.GSS.Character.Event;
using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.StaticDB;
using GameServer.Systems.Aptitude;
using Serilog;

namespace GameServer.Systems.Loot;

/// <summary>
///     The shared "hand the player something" path of the item chain nodes: items and loot into the
///     inventory (undone if the activation fails afterwards), unlocks into <see cref="CharacterEntity.Unlocks" />
///     with the matching <c>UnlocksUpdate</c> to the client, boosts into the character's modifier props.
/// </summary>
public static class PlayerRewards
{
    private static readonly ILogger Logger = Log.ForContext(typeof(PlayerRewards));

    /// <summary>Test seam: <c>dbitems::RootItem</c> by id (the item type decides stack vs. guid item).</summary>
    internal static Func<uint, StaticDB.Records.dbitems.RootItem> LookupItem { get; set; } = SDBInterface.GetRootItem;

    /// <summary>Test seam: the loot roll.</summary>
    internal static Func<uint, IReadOnlyList<LootAward>> RollLoot { get; set; } = LootTableRoller.Roll;

    /// <summary>The player-controlled character an owner-targeting node acts on: the target set, else the initiator.</summary>
    public static CharacterEntity OwnerOf(Context context)
    {
        if (context.Targets != null)
        {
            foreach (var target in context.Targets)
            {
                if (target is CharacterEntity { IsPlayerControlled: true } targeted)
                {
                    return targeted;
                }
            }
        }

        if (context.Self is CharacterEntity { IsPlayerControlled: true } self)
        {
            return self;
        }

        return context.Initiator is CharacterEntity { IsPlayerControlled: true } initiator ? initiator : null;
    }

    /// <summary>
    ///     Puts <paramref name="quantity" /> of an item into the player's bag - stackables onto their pool,
    ///     gear as one item per copy - records it for the reward screen and queues the undo.
    /// </summary>
    public static bool GrantItem(Context context, CharacterEntity character, uint sdbId, uint quantity)
    {
        var inventory = character?.Player?.Inventory;
        if (inventory == null || sdbId == 0 || quantity == 0)
        {
            return false;
        }

        var root = LookupItem(sdbId);
        if (root == null)
        {
            Logger.Warning("Cannot grant item {SdbId} to {Character}: no dbitems::RootItem row", sdbId, character);
            return false;
        }

        if (ItemStacking.IsStackedAsResource(root.Type))
        {
            inventory.AddResource(sdbId, quantity);
            context.ActivationRollbacks.Add(() => inventory.ConsumeResource(sdbId, quantity));
        }
        else
        {
            var guids = new List<ulong>();
            for (uint copy = 0; copy < quantity; copy++)
            {
                guids.Add(inventory.CreateItem(sdbId));
            }

            context.ActivationRollbacks.Add(() =>
            {
                foreach (var guid in guids)
                {
                    inventory.RemoveItem(guid);
                }
            });
        }

        context.AwardedItems.Add(new AwardedItem(sdbId, quantity));
        Logger.Information("{Character} received {Item} x{Quantity}", character, ItemName(sdbId), quantity);
        return true;
    }

    /// <summary>Takes <paramref name="quantity" /> of an item from the player, with the undo queued. False when they do not have it.</summary>
    public static bool TakeItem(Context context, CharacterEntity character, uint sdbId, uint quantity)
    {
        var inventory = character?.Player?.Inventory;
        if (inventory == null || sdbId == 0 || quantity == 0)
        {
            return false;
        }

        if (!inventory.ConsumeItemBySdbId(sdbId, quantity, out var removed))
        {
            return false;
        }

        context.ActivationRollbacks.Add(() =>
        {
            if (removed.Count == 0)
            {
                inventory.AddResource(sdbId, quantity);
            }
            else
            {
                foreach (var item in removed)
                {
                    inventory.RestoreItem(item);
                }
            }
        });
        return true;
    }

    /// <summary>
    ///     Spends the consumable the activation came from, once per activation, for the reward nodes
    ///     whose chains carry no <c>ConsumeItem</c> of their own (the unlock kits, the boosts, the rental
    ///     contracts, the level upgrade kits): the live server took the item inside those commands. Only
    ///     consumables and delivery items (<c>dbitems::RootItem</c> types 7 and 9) are spent - an
    ///     equipped ability module that runs the same node is not. The spend is undone with the activation.
    /// </summary>
    /// <returns>False when the player does not hold the item (the activation should fail).</returns>
    public static bool ConsumeActivatingItem(Context context, CharacterEntity character)
    {
        if (context.AbilityModuleId == 0 || context.ActivatingItemConsumed || context.Initiator != character)
        {
            return true;
        }

        var root = LookupItem(context.AbilityModuleId);
        if (root == null || ((Enums.ItemType)root.Type != Enums.ItemType.Consumable && (Enums.ItemType)root.Type != Enums.ItemType.Powerup))
        {
            return true;
        }

        if (!TakeItem(context, character, context.AbilityModuleId, 1))
        {
            Logger.Warning("{Character} activated {Item} without holding one; failing the activation", character, ItemName(context.AbilityModuleId));
            return false;
        }

        context.ActivatingItemConsumed = true;
        return true;
    }

    /// <summary>Rolls a loot table and grants everything it produced.</summary>
    public static IReadOnlyList<LootAward> GrantLoot(Context context, CharacterEntity character, uint lootTableId)
    {
        var awards = RollLoot(lootTableId);
        foreach (var award in awards)
        {
            GrantItem(context, character, award.SdbId, award.Quantity);
        }

        return awards;
    }

    /// <summary>
    ///     Grants an unlock and tells the client. Already-owned unlocks are a no-op that still succeeds:
    ///     the chains guard with <c>RequireHasUnlock(negate)</c> where a duplicate should be refused.
    /// </summary>
    public static bool Unlock(Context context, CharacterEntity character, string group, uint id)
    {
        if (character?.Unlocks == null || id == 0)
        {
            return false;
        }

        if (!character.Unlocks.Unlock(group, id))
        {
            return true;
        }

        context?.ActivationRollbacks.Add(() =>
        {
            character.Unlocks.Revoke(group, id);
            SendUnlocks(character, CharacterUnlocks.BuildPartialUpdate(group, [], [id]));
        });

        SendUnlocks(character, CharacterUnlocks.BuildPartialUpdate(group, [id]));
        context?.DirtyUnlocks.Add(character);
        Logger.Information("{Character} unlocked {Group} {Id}", character, group, id);
        return true;
    }

    public static void SendUnlocks(CharacterEntity character, UnlocksUpdate update) => SendToPlayer(character, update);

    /// <summary>Sends a character-view message to the player behind a character; false when there is no live channel (tests, a player still loading).</summary>
    public static bool SendToPlayer<TMessage>(CharacterEntity character, TMessage message)
        where TMessage : class, IAero
    {
        if (character?.Player?.CanReceiveGSS != true || character.Player.NetChannels == null
            || !character.Player.NetChannels.TryGetValue(ChannelType.ReliableGss, out var channel))
        {
            return false;
        }

        channel.SendMessage(message, character.EntityId);
        return true;
    }

    /// <summary>Pushes the strongest active boost of each kind into the character's replicated modifier props.</summary>
    public static void SyncBoosts(CharacterEntity character, ulong nowUnixSeconds)
    {
        var controller = character?.Character_BaseController;
        if (controller == null || character.Unlocks == null)
        {
            return;
        }

        controller.XpBoostModifierProp = new StatModifierData { ModifierId = 0, StatValue = character.Unlocks.BoostFraction(CharacterUnlocks.BoostXp, nowUnixSeconds) };
        controller.ResourceBoostModifierProp = new StatModifierData { ModifierId = 0, StatValue = character.Unlocks.BoostFraction(CharacterUnlocks.BoostCrystite, nowUnixSeconds) };
        controller.ReputationBoostModifierProp = new StatModifierData { ModifierId = 0, StatValue = character.Unlocks.BoostFraction(CharacterUnlocks.BoostReputation, nowUnixSeconds) };
        controller.PermanentStatusEffectsProp = new PermanentStatusEffectsData
        {
            Effects = [.. character.Unlocks.Boosts
                .Where(b => b.EffectId != 0)
                .Select(b => new PermanentStatusEffectsInnerData { Id = b.EffectId, Unk2 = (uint)Math.Min(uint.MaxValue, b.ExpiresAt) })],
        };

        if (character.Player?.CanReceiveGSS == true && character.Player.NetChannels != null
            && character.Player.NetChannels.TryGetValue(ChannelType.ReliableGss, out var channel))
        {
            channel.SendChanges(controller, character.EntityId);
        }
    }

    /// <summary>The <c>DisplayRewards</c> screen for what the activation awarded so far.</summary>
    public static DisplayRewards BuildRewardScreen(Context context, byte screenType, uint titleTextId)
    {
        var rewards = context.AwardedItems
            .GroupBy(a => a.SdbId)
            .Select(g => new RewardInfoData
            {
                SdbId = g.Key,
                Quantity = (ushort)Math.Min(ushort.MaxValue, g.Sum(a => (long)a.Quantity)),
                Quality = 0,
                Boosted = 0,
                Module1 = 0,
                Module2 = 0,
                Unk = 0,
            })
            .Take(255)
            .ToArray();

        return new DisplayRewards
        {
            IndexId = 1,
            TitleTextId = titleTextId,
            Unk1 = 0,
            Stats = [],
            Rewards1 = rewards,
            Rewards2 = [],
            ResourceTargetId = new EntityId { Backing = 0 },
            ResourceTargetType = 0,
            Experience = 0,
            ScreenType = screenType,
            DisplayQuality = 0,
            Reputations = [],
        };
    }

    public static string ItemName(uint sdbId) =>
        SDBInterface.GetLocalizedString(LookupItem(sdbId)?.NameId ?? 0u) ?? $"item {sdbId}";

    public static ulong UnixNow() => (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
