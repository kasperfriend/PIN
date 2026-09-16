using System.Collections.Generic;
using GameServer.Data;
using GameServer.StaticDB.Records.dbcharacter;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Tests for <see cref="CharCreateLoadoutPicker"/>, the rule that decides
///     which of a battleframe's char-create loadouts a character starts with.
/// </summary>
/// <remarks>
///     The static database ships several loadouts per frame and they are tiers,
///     not variations: the starter kit at required level 1, the
///     <c>Elite ... Reward</c> kit the operator's sandbox is built from at
///     required level 20, and a <c>Migration 40</c> one at 40. Every frame also
///     has a PvP loadout whose PvE gear sits in slots PIN does not equip, so
///     picking "the frame's loadout" by id or by name is what used to hand a new
///     character an endgame kit — or nothing to shoot with.
/// </remarks>
public class CharCreateLoadoutPickerTests
{
    private static CharCreateLoadout Loadout(uint id, string name, byte isStartingLoadout = 0, byte isDev = 0)
    {
        return new CharCreateLoadout
               {
                   Id = id,
                   Name = name,
                   IsStartingLoadout = isStartingLoadout,
                   IsDev = isDev
               };
    }

    /// <summary>What each loadout carries, keyed by loadout id.</summary>
    private static StockLoadoutKit Kit(Dictionary<uint, StockLoadoutKit> kits, CharCreateLoadout loadout)
    {
        return kits.GetValueOrDefault(loadout.Id);
    }

    [Fact]
    public void Pick_TakesTheStartingLoadoutWhenTheFrameHasOne()
    {
        var starter = Loadout(1u, "Accord Dreadnaught - Player", isStartingLoadout: 1);
        var reward = Loadout(287u, "Elite Dreadnaught Reward Loadout");
        var kits = new Dictionary<uint, StockLoadoutKit>
                   {
                       [starter.Id] = new StockLoadoutKit(10, 1),
                       [reward.Id] = new StockLoadoutKit(13, 20)
                   };

        var picked = CharCreateLoadoutPicker.Pick(new[] { reward, starter }, loadout => Kit(kits, loadout));

        Assert.Same(starter, picked);
    }

    [Fact]
    public void Pick_TakesTheLowestTierKitThatGearsTheCharacter()
    {
        // The advanced frames have no starting loadout; their level 1 kit is what
        // a fresh character of the frame should wear, not the level 20 reward one
        // the sandbox hands to operators.
        var starter = Loadout(99u, "Firecat Test Stage 1 Loadout");
        var reward = Loadout(299u, "Elite Firecat Reward Loadout");
        var migration = Loadout(310u, "Migration 40 Firecat Loadout");
        var kits = new Dictionary<uint, StockLoadoutKit>
                   {
                       [starter.Id] = new StockLoadoutKit(6, 1),
                       [reward.Id] = new StockLoadoutKit(14, 20),
                       [migration.Id] = new StockLoadoutKit(14, 40)
                   };

        var picked = CharCreateLoadoutPicker.Pick(new[] { migration, reward, starter }, loadout => Kit(kits, loadout));

        Assert.Same(starter, picked);
    }

    [Fact]
    public void Pick_PrefersTheFullerKitOfTheSameTier()
    {
        var partial = Loadout(28u, "Astrek \"Firecat\" - Player");
        var full = Loadout(99u, "Firecat Test Stage 1 Loadout");
        var kits = new Dictionary<uint, StockLoadoutKit>
                   {
                       [partial.Id] = new StockLoadoutKit(1, 1),
                       [full.Id] = new StockLoadoutKit(6, 1)
                   };

        var picked = CharCreateLoadoutPicker.Pick(new[] { partial, full }, loadout => Kit(kits, loadout));

        Assert.Same(full, picked);
    }

    [Fact]
    public void Pick_FallsBackToThePlayerNamedLoadoutOfTheSameTierAndFullness()
    {
        var player = Loadout(28u, "Astrek \"Firecat\" - Player");
        var other = Loadout(113u, "Firecat Test Stage 3 Loadout");
        var kits = new Dictionary<uint, StockLoadoutKit>
                   {
                       [player.Id] = new StockLoadoutKit(6, 1),
                       [other.Id] = new StockLoadoutKit(6, 1)
                   };

        var picked = CharCreateLoadoutPicker.Pick(new[] { other, player }, loadout => Kit(kits, loadout));

        Assert.Same(player, picked);
    }

    [Fact]
    public void Pick_SkipsDevLoadoutsAndLoadoutsThatEquipNothing()
    {
        // A dev loadout and the advanced frames' PvP loadout (its PvE gear sits in
        // slots 140 and up, i.e. nothing a character wears): both would leave the
        // character bare or in someone's test gear.
        var dev = Loadout(43u, "Firecat Dev", isDev: 1);
        var pvp = Loadout(28u, "Astrek \"Firecat\" - Player");
        var usable = Loadout(99u, "Firecat Test Stage 1 Loadout");
        var kits = new Dictionary<uint, StockLoadoutKit>
                   {
                       [dev.Id] = new StockLoadoutKit(11, 1),
                       [pvp.Id] = new StockLoadoutKit(0, 1),
                       [usable.Id] = new StockLoadoutKit(6, 1)
                   };

        var picked = CharCreateLoadoutPicker.Pick(new[] { dev, pvp, usable }, loadout => Kit(kits, loadout));

        Assert.Same(usable, picked);
    }

    [Fact]
    public void Pick_IsNullWhenNothingGearsTheFrame()
    {
        var pvp = Loadout(28u, "Astrek \"Firecat\" - Player");
        var dev = Loadout(43u, "Firecat Dev", isDev: 1);
        var kits = new Dictionary<uint, StockLoadoutKit>
                   {
                       [pvp.Id] = new StockLoadoutKit(0, 1),
                       [dev.Id] = new StockLoadoutKit(11, 1)
                   };

        Assert.Null(CharCreateLoadoutPicker.Pick(new[] { pvp, dev }, loadout => Kit(kits, loadout)));
        Assert.Null(CharCreateLoadoutPicker.Pick(null, loadout => Kit(kits, loadout)));
        Assert.Null(CharCreateLoadoutPicker.Pick(new CharCreateLoadout[0], loadout => Kit(kits, loadout)));
    }
}
