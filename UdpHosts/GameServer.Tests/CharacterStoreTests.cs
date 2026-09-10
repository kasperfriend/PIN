using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Shared.Common.Accounts;
using Shared.Common.Characters;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Tests for the per-account character rules of <see cref="CharacterStore"/>
///     and the pure resolution logic in <see cref="CharacterResolver"/>:
///     zone-picker seeding per account, the guid scheme, and how a (possibly
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
}
