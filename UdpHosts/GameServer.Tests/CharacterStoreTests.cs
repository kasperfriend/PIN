using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Shared.Common;
using Shared.Common.Accounts;
using Shared.Common.Characters;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Tests for the per-account character rules of <see cref="CharacterStore"/>
///     and the pure resolution logic in <see cref="CharacterResolver"/>:
///     fresh accounts and the admin-only zone picker, the guid scheme (including
///     the character slots beyond an account's first), and how a (possibly
///     low-byte-clobbered) character guid resolves back to a record once several
///     accounts exist.
/// </summary>
public class CharacterStoreTests
{
    [Fact]
    public void BuildZoneSeed_CreatesOneEntryPerZoneForTheAccount()
    {
        var prefix = CharacterStore.GuidPrefixForAccount(AccountStore.AdminAccountId);

        var seed = CharacterStore.BuildZoneSeed(AccountStore.AdminAccountId, prefix);

        Assert.Equal(38, seed.Count);
        Assert.All(seed, character => Assert.Equal(AccountStore.AdminAccountId, character.AccountId));

        // Every zone id is encoded in the low 16 bits of its guid, all guids are
        // distinct and the sort order covers the list.
        Assert.True(seed.All(c => (c.CharacterGuid & 0xffff) == c.LastZoneId));
        Assert.Equal(seed.Count, seed.Select(c => c.CharacterGuid).Distinct().Count());
        Assert.Equal(Enumerable.Range(0, seed.Count), seed.Select(c => c.SortOrder));

        // The admin prefix is the legacy one, so pre-account characters.json guids
        // keep resolving.
        Assert.Equal(CharacterStore.GuidPrefix, prefix);
        Assert.Equal(CharacterStore.GuidPrefix + 448, seed.Single(c => c.LastZoneId == 448).CharacterGuid);
    }

    [Fact]
    public void GuidPrefixForAccount_GivesEveryAccountItsOwnRange()
    {
        Assert.Equal(CharacterStore.GuidPrefix, CharacterStore.GuidPrefixForAccount(AccountStore.AdminAccountId));
        Assert.NotEqual(
            CharacterStore.GuidPrefixForAccount(26294423),
            CharacterStore.GuidPrefixForAccount(26294424));

        // Generated prefixes keep the zone id in the low 16 bits and never
        // collide with the legacy admin prefix.
        var prefix = CharacterStore.GuidPrefixForAccount(26294423);
        Assert.Equal(CharacterStore.GeneratedAccountGuidPrefixBase, prefix & 0xffff000000000000);
        Assert.Equal(26294423UL << 16, prefix & 0x0000ffffffff0000);
    }

    [Fact]
    public void CharacterGuidForSlot_SlotZero_KeepsTheGuidsCreatedCharactersAlwaysHad()
    {
        // Slot 0 is the guid a created character has always used: the account's
        // own prefix plus the spawn zone. For the admin account that is the
        // legacy prefix, so its created character still replaces the seeded
        // "New Eden" zone-picker entry.
        var account = 26294423UL;
        Assert.Equal(
            CharacterStore.GuidPrefixForAccount(account) + CharacterStore.DefaultSpawnZoneId,
            CharacterStore.CharacterGuidForSlot(account, 0));
        Assert.Equal(
            CharacterStore.GuidPrefix + CharacterStore.DefaultSpawnZoneId,
            CharacterStore.CharacterGuidForSlot(AccountStore.AdminAccountId, 0));
    }

    [Fact]
    public void CharacterGuidForSlot_FurtherSlots_GetTheirOwnGuids()
    {
        var account = 26294423UL;
        var first = CharacterStore.CharacterGuidForSlot(account, 0);
        var second = CharacterStore.CharacterGuidForSlot(account, 1);
        var third = CharacterStore.CharacterGuidForSlot(account, 2);

        // Distinct guids and distinct entity ids (the upper seven bytes the
        // GameServer derives a player id from)...
        Assert.NotEqual(first, second);
        Assert.NotEqual(second, third);
        Assert.NotEqual(first & 0xffffffffffffff00, second & 0xffffffffffffff00);

        // ...with the slot index in bits 48..55 and the account still in its
        // own range...
        Assert.Equal(1UL << 48, second & 0x00ff000000000000);
        Assert.Equal(2UL << 48, third & 0x00ff000000000000);
        Assert.Equal(account << 16, second & 0x0000ffffffff0000);

        // ...and every slot encodes the spawn zone in the low 16 bits, so the
        // GameServer spawns them all into New Eden.
        Assert.All(new[] { first, second, third }, guid => Assert.Equal(CharacterStore.DefaultSpawnZoneId, (uint)(guid & 0xffff)));

        // The admin account's further slots use the generated range too: its
        // legacy prefix already carries non-zero bits where the slot index goes.
        Assert.Equal(
            CharacterStore.GeneratedAccountGuidPrefixBase | (1UL << 48) | (AccountStore.AdminAccountId << 16) | CharacterStore.DefaultSpawnZoneId,
            CharacterStore.CharacterGuidForSlot(AccountStore.AdminAccountId, 1));
    }

    [Fact]
    public void StaleZonePickerSeeds_MatchesOnlyNonAdminUntouchedSeeds()
    {
        // What a store written by the seed-every-account build contains: the
        // admin's zone picker, a copied zone picker for a player account, that
        // player's created character, and a pre-account-system record with a
        // custom name.
        var adminSeed = CharacterStore.BuildZoneSeed(AccountStore.AdminAccountId, CharacterStore.GuidPrefix)
                                      .Single(c => c.LastZoneId == 448);
        var playerAccount = 26294424UL;
        var playerSeed = CharacterStore.BuildZoneSeed(playerAccount, CharacterStore.GuidPrefixForAccount(playerAccount))
                                       .Single(c => c.LastZoneId == 448);
        var playerCharacter = new CharacterRecord
                              {
                                  AccountId = playerAccount,
                                  CharacterGuid = CharacterStore.GuidPrefixForAccount(playerAccount) + 448,
                                  IsCustom = true,
                                  Name = "Kasper"
                              };
        var legacyNamed = new CharacterRecord { AccountId = playerAccount, Name = "New Eden - Raptor" };

        var stale = CharacterStore.StaleZonePickerSeeds(new[] { adminSeed, playerSeed, playerCharacter, legacyNamed });

        // Only the player account's untouched seed copy is stale: accounts
        // start fresh now, the admin keeps its zone picker, and created or
        // hand-named characters survive the pruning.
        var staleSeed = Assert.Single(stale);
        Assert.Same(playerSeed, staleSeed);
    }

    [Fact]
    public void RefreshInheritedWarpaints_RepaintsCreatedCharactersWithTheirChassisDefault()
    {
        // A created character that still wears the copied admin purple Raptor
        // paint (store written before creation stopped copying the admin look):
        // it gets its own chassis' stock colors (the Dreadnaught's).
        var created = CharacterCreation.Create(26294423UL, CharacterStore.CharacterGuidForSlot(26294423UL, 0), 0, "Dread", 0, 75772, 0, 0, 0, 0, 0, 0);
        created.Visuals.WarpaintId = DefaultCharacterTemplate.WarpaintId;
        created.Visuals.Warpaint = [.. DefaultCharacterTemplate.Warpaint];

        // A created character saved after the copy was stripped: its warpaint
        // was never written, so it is empty and looks purple to the client.
        var createdEmpty = CharacterCreation.Create(26294425UL, CharacterStore.CharacterGuidForSlot(26294425UL, 0), 0, "Biotech", 0, 75774, 0, 0, 0, 0, 0, 0);
        createdEmpty.Visuals.Warpaint = [];

        // A created character whose chassis has no stock palette in the table:
        // the purple must be cleared, even though there is nothing to stamp.
        var createdUnknownChassis = CharacterCreation.Create(26294426UL, CharacterStore.CharacterGuidForSlot(26294426UL, 0), 0, "Mystery", 0, 12345, 0, 0, 0, 0, 0, 0);
        createdUnknownChassis.Visuals.WarpaintId = DefaultCharacterTemplate.WarpaintId;
        createdUnknownChassis.Visuals.Warpaint = [.. DefaultCharacterTemplate.Warpaint];

        // The admin zone-picker seed keeps its admin look, a character with a
        // custom paint job is left alone, and a record without visuals survives.
        var seed = CharacterStore.BuildZoneSeed(AccountStore.AdminAccountId, CharacterStore.GuidPrefix)
                                 .Single(c => c.LastZoneId == 448);
        var customPaint = CharacterCreation.Create(26294424UL, CharacterStore.CharacterGuidForSlot(26294424UL, 0), 0, "Painted", 0, 75772, 0, 0, 0, 0, 0, 0);
        customPaint.Visuals.Warpaint = [1, 2, 3];
        var noVisuals = new CharacterRecord { AccountId = 26294427UL, IsCustom = true, Name = "Bare", Visuals = null };

        var refreshed = CharacterStore.RefreshInheritedWarpaints(new[] { created, createdEmpty, createdUnknownChassis, seed, customPaint, noVisuals });

        // Exactly the three created characters with an inherited or missing
        // paint job are touched (in name order: Biotech, Dread, Mystery).
        Assert.Equal(
            new[] { createdEmpty, created, createdUnknownChassis },
            refreshed.OrderBy(c => c.Name, StringComparer.Ordinal));

        // The purple is gone everywhere...
        Assert.All(refreshed, c => Assert.NotEqual(DefaultCharacterTemplate.Warpaint, c.Visuals?.Warpaint));
        Assert.All(refreshed, c => Assert.Equal(0, c.Visuals.WarpaintId));

        // ...and the known chassis wear their own stock colors (the Dreadnaught
        // and the Biotech share palette 77221 in the SDB).
        Assert.Equal(
            new uint[] { 0xffff2104, 0x9cd30000, 0x31860000, 0x4a490000, 0x94b27bae, 0xcc803141, 0xcc803141 },
            created.Visuals.Warpaint);
        Assert.Equal(created.Visuals.Warpaint, createdEmpty.Visuals.Warpaint);

        // The unknown chassis has nothing to stamp, so its warpaint is empty.
        Assert.Empty(createdUnknownChassis.Visuals.Warpaint);

        // ...while everything else is untouched.
        Assert.Equal(DefaultCharacterTemplate.Warpaint, seed.Visuals.Warpaint);
        Assert.Equal(new uint[] { 1, 2, 3 }, customPaint.Visuals.Warpaint);
        Assert.Null(noVisuals.Visuals);
    }

    [Fact]
    public void LegacyCharacterRecord_WithoutAccountId_BelongsToAdmin()
    {
        // characters.json files from before the account system have no AccountId;
        // they deserialize as admin-owned so existing installs keep working.
        var legacy = JsonSerializer.Deserialize<CharacterRecord>("""{"CharacterGuid": 11068046444225741608, "Name": "New Eden - Raptor"}""");
        Assert.Equal(AccountStore.AdminAccountId, legacy.AccountId);
    }

    [Fact]
    public void Resolver_ExactGuid_Wins()
    {
        var characters = new List<CharacterRecord>
                         {
                             new() { AccountId = 1, CharacterGuid = 0x99aabbccddee0000 + 448, Name = "admin" },
                             new() { AccountId = 2, CharacterGuid = 0xaa00000000000000 + (26294423UL << 16) + 448, Name = "player" }
                         };

        Assert.Equal("player", CharacterResolver.Find(characters, 0xaa00000000000000 + (26294423UL << 16) + 448).Name);
        Assert.Equal("admin", CharacterResolver.Find(characters, 0x99aabbccddee0000 + 448).Name);
    }

    [Fact]
    public void Resolver_ClobberedLowByte_UsesTheZoneIdFromThePayload()
    {
        // Both accounts have a New Eden (448) entry; the client overwrote the low
        // byte of the guid. The command's zone id decides whose character it is.
        var adminGuid = 0x99aabbccddee0000 + 448;
        var playerAccount = 26294423UL;
        var playerGuid = CharacterStore.GuidPrefixForAccount(playerAccount) + 448;
        var characters = new List<CharacterRecord>
                         {
                             new() { AccountId = 1, CharacterGuid = adminGuid, Name = "admin" },
                             new() { AccountId = 2, CharacterGuid = playerGuid, Name = "player" }
                         };

        var clobbered = playerGuid | 0xfe;
        Assert.Equal("player", CharacterResolver.Find(characters, clobbered, 448).Name);

        var clobberedAdmin = adminGuid | 0xfe;
        Assert.Equal("admin", CharacterResolver.Find(characters, clobberedAdmin, 448).Name);
    }

    [Fact]
    public void Resolver_ClobberedLowByte_AmbiguousZonesFallsThroughToZoneId()
    {
        // 25 of the seeded zone ids share the 0x04 high byte (1089..1173), so a
        // clobbered guid is ambiguous even within one account; only the payload
        // zone id can pick the right one.
        var playerAccount = 26294423UL;
        var prefix = CharacterStore.GuidPrefixForAccount(playerAccount);
        var characters = new List<CharacterRecord>
                         {
                             new() { AccountId = 2, CharacterGuid = prefix + 1089, Name = "miru" },
                             new() { AccountId = 2, CharacterGuid = prefix + 1100, Name = "shadow" },
                             new() { AccountId = 2, CharacterGuid = prefix + 1147, Name = "refinery" }
                         };

        var clobbered = (prefix + 1100) | 0xfe;
        Assert.Equal("shadow", CharacterResolver.Find(characters, clobbered, 1100).Name);

        // Without a zone id the clobbered guid is ambiguous and must not guess.
        Assert.Null(CharacterResolver.Find(characters, clobbered, 0));
    }

    [Fact]
    public void Resolver_UnknownGuid_FallsBackToAdminZoneEntry()
    {
        // Legacy behaviour of CharacterStore.Get: an unknown guid still resolved
        // to the admin account's entry for the zone encoded in its low 16 bits.
        var characters = new List<CharacterRecord>
                         {
                             new() { AccountId = 1, CharacterGuid = CharacterStore.GuidPrefix + 1030, Name = "sertao" }
                         };

        Assert.Equal("sertao", CharacterResolver.Find(characters, CharacterStore.GuidPrefix + 1030).Name);

        // A guid from a different range that still encodes zone 1030 in its low
        // 16 bits resolves to the admin account's entry for that zone.
        Assert.Equal("sertao", CharacterResolver.Find(characters, 0x1234567800000000 + 1030).Name);
    }

    [Fact]
    public void Resolver_UnknownEverything_ReturnsNull()
    {
        var characters = new List<CharacterRecord>
                         {
                             new() { AccountId = 1, CharacterGuid = CharacterStore.GuidPrefix + 448, Name = "admin" }
                         };

        Assert.Null(CharacterResolver.Find(characters, 0x1234500000000000));
        Assert.Null(CharacterResolver.Find(characters, 0x1234500000000000, 9999));
        Assert.Null(CharacterResolver.Find(new List<CharacterRecord>(), 0x1234500000000000, 448));
    }

    [Fact]
    public void Resolver_SessionSave_ResolvesAcrossAccounts()
    {
        // The pattern of UpdateSessionData: the guid arrives with its low byte
        // clobbered and the command carries the exact zone id. Each account's
        // own entry must be found, never the other account's.
        var playerAccount = 26294423UL;
        var characters = new List<CharacterRecord>
                         {
                             new() { AccountId = 1, CharacterGuid = CharacterStore.GuidPrefix + 833, Name = "admin-833" },
                             new() { AccountId = 1, CharacterGuid = CharacterStore.GuidPrefix + 448, Name = "admin-448" },
                             new() { AccountId = 2, CharacterGuid = CharacterStore.GuidPrefixForAccount(playerAccount) + 448, Name = "player-448" },
                             new() { AccountId = 2, CharacterGuid = CharacterStore.GuidPrefixForAccount(playerAccount) + 833, Name = "player-833" }
                         };

        var clobberedPlayer448 = (CharacterStore.GuidPrefixForAccount(playerAccount) + 448) | 0xfe;
        var resolved = CharacterResolver.Find(characters, clobberedPlayer448, 448);
        Assert.Equal("player-448", resolved.Name);

        var clobberedAdmin833 = (CharacterStore.GuidPrefix + 833) | 0xfe;
        Assert.Equal("admin-833", CharacterResolver.Find(characters, clobberedAdmin833, 833).Name);
    }

    [Fact]
    public void Resolver_CreatedSlotsOfOneAccount_ResolveIndividually()
    {
        // A fresh account's characters: slot 0 plus the numbered slots, all
        // spawning into the same zone. The slot bits keep their upper seven
        // bytes distinct, so each one resolves on its own — the client
        // clobbered the low byte of the second character's guid here.
        var account = 26294424UL;
        var characters = new List<CharacterRecord>
                         {
                             new() { AccountId = account, CharacterGuid = CharacterStore.CharacterGuidForSlot(account, 0), Name = "first" },
                             new() { AccountId = account, CharacterGuid = CharacterStore.CharacterGuidForSlot(account, 1), Name = "second" }
                         };

        Assert.Equal("second", CharacterResolver.Find(characters, CharacterStore.CharacterGuidForSlot(account, 1)).Name);
        Assert.Equal("first", CharacterResolver.Find(characters, CharacterStore.CharacterGuidForSlot(account, 0)).Name);

        var clobbered = CharacterStore.CharacterGuidForSlot(account, 1) | 0xfe;
        Assert.Equal("second", CharacterResolver.Find(characters, clobbered).Name);
        Assert.Equal("second", CharacterResolver.Find(characters, clobbered, CharacterStore.DefaultSpawnZoneId).Name);
    }
}
