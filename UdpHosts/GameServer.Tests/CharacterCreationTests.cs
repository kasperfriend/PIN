using System.Linq;
using Shared.Common.Accounts;
using Shared.Common.Characters;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Tests for the character creation rules (<see cref="CharacterCreation"/>):
///     the original name validation codes, the record built for a created
///     character, and which zone-picker slot a created character may claim.
/// </summary>
public class CharacterCreationTests
{
    [Fact]
    public void ValidateName_AcceptsPlainNames()
    {
        Assert.Empty(CharacterCreation.ValidateName("Kasper", []));
        Assert.Empty(CharacterCreation.ValidateName("Raptor 5", []));
        Assert.Empty(CharacterCreation.ValidateName("x".PadRight(40, 'x'), []));
    }

    [Fact]
    public void ValidateName_RejectsTooShortNames()
    {
        Assert.Equal(new[] { AccountErrors.ErrNameTooShort }, CharacterCreation.ValidateName(null, []));
        Assert.Equal(new[] { AccountErrors.ErrNameTooShort }, CharacterCreation.ValidateName(string.Empty, []));
        Assert.Equal(new[] { AccountErrors.ErrNameTooShort }, CharacterCreation.ValidateName("abc", []));
    }

    [Fact]
    public void ValidateName_RejectsTooLongNames()
    {
        Assert.Equal(new[] { AccountErrors.ErrNameTooLong }, CharacterCreation.ValidateName("x".PadRight(41, 'x'), []));
    }

    [Fact]
    public void ValidateName_RejectsLeadingDigit()
    {
        Assert.Equal(new[] { AccountErrors.ErrNameStartsWithNumber }, CharacterCreation.ValidateName("4theMelding", []));
    }

    [Fact]
    public void ValidateName_RejectsInvalidCharacters()
    {
        Assert.Equal(new[] { AccountErrors.ErrInvalidCharacter }, CharacterCreation.ValidateName("Kasper_The-Great", []));
        Assert.Equal(new[] { AccountErrors.ErrInvalidCharacter }, CharacterCreation.ValidateName("Ünicode", []));
    }

    [Fact]
    public void ValidateName_RejectsTakenNamesCaseInsensitively()
    {
        Assert.Equal(new[] { AccountErrors.ErrNameInUse }, CharacterCreation.ValidateName("Kasper", ["kasper"]));
        Assert.Equal(new[] { AccountErrors.ErrNameInUse }, CharacterCreation.ValidateName("KASPER", ["Kasper"]));
    }

    [Fact]
    public void Create_BuildsAFreshCustomCharacter()
    {
        var account = 26294423UL;
        var guid = CharacterStore.GuidPrefixForAccount(account) + CharacterStore.DefaultSpawnZoneId;

        var character = CharacterCreation.Create(
            account,
            guid,
            37,
            "Kasper",
            gender: 1,
            battleframeSdbId: 76334,
            head: 10026,
            voiceSet: 1033,
            skinColorItemId: 118969,
            eyeColorItemId: 118980,
            hairColorItemId: 77193,
            headAccessoryA: 10117);

        Assert.Equal(account, character.AccountId);
        Assert.Equal(guid, character.CharacterGuid);
        Assert.True(character.IsCustom);
        Assert.Equal("Kasper", character.Name);
        Assert.Equal(1u, character.Gender);
        Assert.Equal(76334u, character.CurrentBattleframeSDBId);

        // A fresh character starts at progression level 1...
        Assert.Equal(1, character.CurrentLevel);
        Assert.Equal(1, character.MaxFrameLevel);

        // ...and its zone is encoded in the guid, so the GameServer spawns it
        // in the spawn zone.
        Assert.Equal(CharacterStore.DefaultSpawnZoneId, (uint)(character.CharacterGuid & 0xffff));
        Assert.Equal(CharacterStore.DefaultSpawnZoneId, character.LastZoneId);

        // The visual item ids come from the creation form.
        Assert.Equal(10026u, character.Visuals.Head);
        Assert.Equal(1033u, character.Visuals.VoiceSet);
        Assert.Equal(118969u, character.Visuals.SkinColorId);
        Assert.Equal(118980u, character.Visuals.EyeColorId);
        Assert.Equal(77193u, character.Visuals.HairColorId);
        Assert.Equal(new[] { 10117u }, character.Visuals.HeadAccessories);
    }

    [Fact]
    public void Create_WithoutHeadAccessory_WearsNone()
    {
        var character = CharacterCreation.Create(1, CharacterStore.GuidPrefix + 448, 0, "Plain", 0, 76334, 0, 0, 0, 0, 0, 0);
        Assert.Empty(character.Visuals.HeadAccessories);
    }

    [Fact]
    public void IsZoneSeedEntry_DetectsSeedEntriesOnly()
    {
        // An untouched seed entry: not custom, still named after its zone.
        var seed = CharacterStore.BuildZoneSeed(AccountStore.AdminAccountId, CharacterStore.GuidPrefix).Single(c => c.LastZoneId == 448);
        Assert.True(CharacterCreation.IsZoneSeedEntry(seed));

        // A created character is custom.
        Assert.False(CharacterCreation.IsZoneSeedEntry(new CharacterRecord { AccountId = 1, IsCustom = true, Name = "New Eden" }));

        // Pre-account-system entries with custom names are characters, not seeds.
        Assert.False(CharacterCreation.IsZoneSeedEntry(new CharacterRecord { AccountId = 1, Name = "New Eden - Raptor" }));
    }

    [Fact]
    public void TryClaimSlot_AllowsReplacingSeedsAndEmptySlots()
    {
        Assert.True(CharacterCreation.TryClaimSlot(null, out var errorCode));
        Assert.Null(errorCode);

        var seed = CharacterStore.BuildZoneSeed(AccountStore.AdminAccountId, CharacterStore.GuidPrefix).Single(c => c.LastZoneId == 448);
        Assert.True(CharacterCreation.TryClaimSlot(seed, out errorCode));
        Assert.Null(errorCode);
    }

    [Fact]
    public void TryClaimSlot_RejectsExistingCustomCharacters()
    {
        var custom = new CharacterRecord { AccountId = 1, IsCustom = true, Name = "Kasper" };
        Assert.False(CharacterCreation.TryClaimSlot(custom, out var errorCode));
        Assert.Equal(AccountErrors.ErrDuplicateCharacter, errorCode);
    }

    [Fact]
    public void IsSeedZoneName_MatchesTheSeedList()
    {
        Assert.True(CharacterStore.IsSeedZoneName("New Eden"));
        Assert.True(CharacterStore.IsSeedZoneName("M22 Homecoming"));
        Assert.False(CharacterStore.IsSeedZoneName("Kasper"));
        Assert.False(CharacterStore.IsSeedZoneName(null));
    }

    [Fact]
    public void CreatedCharacters_KeepDistinctGuidsAcrossAccounts()
    {
        // The same created character for two accounts must not collide on an
        // entity id — that is what lets two players be online in the same zone
        // at the same time.
        var first = CharacterCreation.Create(26294423UL, CharacterStore.GuidPrefixForAccount(26294423UL) + CharacterStore.DefaultSpawnZoneId, 0, "Kasper", 0, 76334, 0, 0, 0, 0, 0, 0);
        var second = CharacterCreation.Create(26294424UL, CharacterStore.GuidPrefixForAccount(26294424UL) + CharacterStore.DefaultSpawnZoneId, 0, "Morpheus", 0, 76334, 0, 0, 0, 0, 0, 0);

        Assert.NotEqual(first.CharacterGuid, second.CharacterGuid);
        Assert.NotEqual(first.CharacterGuid & 0xffffffffffffff00, second.CharacterGuid & 0xffffffffffffff00);

        // Both encode the same (spawn) zone in the low 16 bits.
        Assert.Equal(first.CharacterGuid & 0xffff, second.CharacterGuid & 0xffff);
    }
}
