using System.Linq;
using Shared.Common;
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
            eyes: 10001,
            voiceSet: 1033,
            skinColorItemId: 118969,
            eyeColorItemId: 118980,
            lipColorItemId: 0,
            hairColorItemId: 77193,
            facialHairColorItemId: 0,
            headAccessoryA: 10117,
            headAccessoryB: 0,
            facialHair: 0);

        Assert.Equal(account, character.AccountId);
        Assert.Equal(guid, character.CharacterGuid);
        Assert.True(character.IsCustom);
        Assert.Equal("Kasper", character.Name);
        Assert.Equal(1u, character.Gender);
        Assert.Equal(0u, character.Race);
        Assert.Equal(76334u, character.CurrentBattleframeSDBId);

        // A fresh character starts at progression level 1...
        Assert.Equal(1, character.CurrentLevel);
        Assert.Equal(1, character.MaxFrameLevel);

        // ...and its zone is encoded in the guid, so the GameServer spawns it
        // in the spawn zone.
        Assert.Equal(CharacterStore.DefaultSpawnZoneId, (uint)(character.CharacterGuid & 0xffff));
        Assert.Equal(CharacterStore.DefaultSpawnZoneId, character.LastZoneId);

        // No admin carryover: race, title, glider, vehicle, ornaments,
        // head-accessory tint, warpaint id are all fresh defaults.
        Assert.Equal(0u, character.TitleId);
        Assert.Equal(0u, character.Visuals.Glider);
        Assert.Equal(0u, character.Visuals.Vehicle);
        Assert.Equal(0u, character.Visuals.HeadAccessoryColor);
        Assert.Equal(0, character.Visuals.WarpaintId);
        Assert.Empty(character.Visuals.Ornaments);

        // Army fields must not carry the admin seed's "ARMY" / ArmyGuid=1 /
        // IsOfficer=true placeholders.
        Assert.Equal(string.Empty, character.ArmyTag);
        Assert.Equal(0UL, character.ArmyGuid);
        Assert.False(character.ArmyIsOfficer);
        Assert.Equal(0u, character.TimePlayed);
        Assert.Equal(0u, character.LastOutpostId);

        // The visual item ids come from the creation form...
        Assert.Equal(10026u, character.Visuals.Head);
        Assert.Equal(10001u, character.Visuals.Eyes);
        Assert.Equal(1033u, character.Visuals.VoiceSet);
        Assert.Equal(118969u, character.Visuals.SkinColorId);
        Assert.Equal(118980u, character.Visuals.EyeColorId);
        Assert.Equal(77193u, character.Visuals.HairColorId);

        // ...and the packed ARGB body colors are resolved from those palette ids
        // through CharacterColorPalettes (dbvisualrecords::WarpaintPalette,
        // combined via FColor.CombineLightDark) — they are the real color the
        // player picked, not the hardcoded admin template defaults. The three
        // expected values are 0xffff7186 / 0x61601060 / 0x724f0021 for the
        // default template palette ids.
        Assert.Equal(0xffff7186u, character.Visuals.SkinColor);
        Assert.Equal(0x61601060u, character.Visuals.EyeColor);
        Assert.Equal(0x724f0021u, character.Visuals.HairColor);

        // ...and the head accessory A is worn as the hair mesh, so the selection
        // screen and the in-game character show the same hair.
        Assert.Equal(10117u, character.Visuals.Hair);
        Assert.Equal(new[] { 10117u }, character.Visuals.HeadAccessories);

        // Armor colors are the Raptor's own stock SDB colors — not the admin
        // account's purple Raptor paint, and not empty (an empty warpaint
        // makes the client fall back to that same purple default avatar).
        Assert.Equal(
            new uint[] { 0xacf018e3, 0x62480000, 0x00000000, 0xdedb0000, 0x00007bae, 0xefebbe80, 0xefebbe80 },
            character.Visuals.Warpaint);
        Assert.NotEqual(DefaultCharacterTemplate.Warpaint, character.Visuals.Warpaint);
    }

    [Fact]
    public void Create_WearsTheChassisOwnStockColors()
    {
        // The Biotech's stock colors come from its SDB default palette
        // (77221), not from the admin template's Raptor palette (143225).
        var character = CharacterCreation.Create(1, CharacterStore.GuidPrefix + 448, 0, "Biotech", 0, 75774, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Equal(0, character.Visuals.WarpaintId);
        Assert.Equal(
            new uint[] { 0xffff2104, 0x9cd30000, 0x31860000, 0x4a490000, 0x94b27bae, 0xcc803141, 0xcc803141 },
            character.Visuals.Warpaint);
        Assert.NotEqual(DefaultCharacterTemplate.Warpaint, character.Visuals.Warpaint);

        // The Raptor (76334) is the frame the admin template was built for, but
        // its SDB stock colors are still not the admin's custom purple paint.
        var raptor = CharacterCreation.Create(2, CharacterStore.GuidPrefix + 448, 0, "Raptor", 0, 76334, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Equal(
            new uint[] { 0xacf018e3, 0x62480000, 0x00000000, 0xdedb0000, 0x00007bae, 0xefebbe80, 0xefebbe80 },
            raptor.Visuals.Warpaint);
        Assert.NotEqual(DefaultCharacterTemplate.Warpaint, raptor.Visuals.Warpaint);
    }

    [Fact]
    public void Create_WithoutAStockPaletteForTheChassis_KeepsAnEmptyWarpaint()
    {
        // A chassis the generated table does not know (no resolvable default
        // palette in the SDB) keeps an empty warpaint: the GameServer wears its
        // default SDB colors for it, and the admin purple never comes back.
        var character = CharacterCreation.Create(1, CharacterStore.GuidPrefix + 448, 0, "Unknown", 0, 12345, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Equal(0, character.Visuals.WarpaintId);
        Assert.Empty(character.Visuals.Warpaint);
    }

    [Fact]
    public void Create_WithoutHeadAccessory_WearsNone()
    {
        var character = CharacterCreation.Create(1, CharacterStore.GuidPrefix + 448, 0, "Plain", 0, 76334, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Empty(character.Visuals.HeadAccessories);
    }

    [Fact]
    public void Create_WithFacialHair_WearsItAsSecondHeadAccessory()
    {
        // Facial hair mesh is the second entry in the head accessory list, the
        // same order CharacterAppearance.HeadAccessoryMeshes emits.
        var character = CharacterCreation.Create(
            1, CharacterStore.GuidPrefix + 448, 0, "Bearded", 0, 76334,
            head: 10026, eyes: 10001, voiceSet: 1033,
            skinColorItemId: 118969, eyeColorItemId: 118980, lipColorItemId: 0,
            hairColorItemId: 77193, facialHairColorItemId: 77193,
            headAccessoryA: 10117, headAccessoryB: 10150, facialHair: 10150);

        Assert.Equal(10117u, character.Visuals.Hair);
        Assert.Equal(10150u, character.Visuals.FacialHair);
        Assert.Equal(new[] { 10117u, 10150u }, character.Visuals.HeadAccessories);
        // Facial hair color is resolved from its palette just like hair color.
        Assert.Equal(0x724f0021u, character.Visuals.FacialHairColor);
    }

    [Fact]
    public void Create_DoesNotInheritAdminTemplateValues()
    {
        // Guard: a fresh character with zeroed inputs must not carry any of the
        // admin-specific template values that were previously leaking onto new
        // characters (e.g. HeadAccessoryColor=1211031763, WarpaintId=143225,
        // the admin Raptor warpaint, level 10, etc.).
        var character = CharacterCreation.Create(
            999UL, CharacterStore.GuidPrefixForAccount(999UL) + CharacterStore.DefaultSpawnZoneId,
            0, "Fresh", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        // Meta fields
        Assert.True(character.IsCustom);
        Assert.Equal(999UL, character.AccountId);
        Assert.Equal(0u, character.Gender);
        Assert.Equal(0u, character.Race);
        Assert.Equal(0, (int)character.TitleId);
        Assert.Equal(1, character.CurrentLevel);
        Assert.Equal(1, character.MaxFrameLevel);

        // Visuals: no carryover
        Assert.Equal(0u, character.Visuals.Head);
        Assert.Equal(0u, character.Visuals.Eyes);
        Assert.Equal(0u, character.Visuals.VoiceSet);
        Assert.Equal(0u, character.Visuals.Glider);
        Assert.Equal(0u, character.Visuals.Vehicle);
        Assert.Equal(0u, character.Visuals.Hair);
        Assert.Equal(0u, character.Visuals.FacialHair);
        Assert.Equal(0u, character.Visuals.HeadAccessoryColor);
        Assert.Equal(0, character.Visuals.WarpaintId);
        Assert.Empty(character.Visuals.HeadAccessories);
        Assert.Empty(character.Visuals.Warpaint);
        // Admin-specific head accessory color must not leak.
        Assert.NotEqual(DefaultCharacterTemplate.HeadAccessoryColor, character.Visuals.HeadAccessoryColor);
        // Admin-specific warpaint must not leak.
        Assert.NotEqual(DefaultCharacterTemplate.Warpaint, character.Visuals.Warpaint);
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
        var first = CharacterCreation.Create(26294423UL, CharacterStore.GuidPrefixForAccount(26294423UL) + CharacterStore.DefaultSpawnZoneId, 0, "Kasper", 0, 76334, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var second = CharacterCreation.Create(26294424UL, CharacterStore.GuidPrefixForAccount(26294424UL) + CharacterStore.DefaultSpawnZoneId, 0, "Morpheus", 0, 76334, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        Assert.NotEqual(first.CharacterGuid, second.CharacterGuid);
        Assert.NotEqual(first.CharacterGuid & 0xffffffffffffff00, second.CharacterGuid & 0xffffffffffffff00);

        // Both encode the same (spawn) zone in the low 16 bits.
        Assert.Equal(first.CharacterGuid & 0xffff, second.CharacterGuid & 0xffff);
    }
}
