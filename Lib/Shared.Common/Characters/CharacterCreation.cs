using System;
using System.Collections.Generic;
using System.Linq;
using Shared.Common.Accounts;

namespace Shared.Common.Characters;

/// <summary>
/// Character creation rules, kept pure (no store access) so they can be unit
/// tested: the name rules the original service enforced on
/// <c>POST api/v1/characters/validate_name</c> and <c>POST api/v1/characters</c>,
/// and the record built for a successful creation.
///
/// Name rules mirror the original client error codes: length bounds
/// (<c>ERR_NAME_TOO_SHORT</c>/<c>ERR_NAME_TOO_LONG</c>), ASCII letters, digits
/// and spaces only (<c>ERR_INVALID_CHARACTER</c>), must not start with a digit
/// (<c>ERR_NAME_STARTS_WITH_NUMBER</c>) and must not be taken
/// (<c>ERR_NAME_IN_USE</c>). The original reports every violated rule as a
/// <c>reason</c> list with the umbrella code <c>ERR_NAME_INVALID</c>.
/// </summary>
public static class CharacterCreation
{
    /// <summary>Shortest accepted character name (the client's own input rules start here too).</summary>
    public const int MinNameLength = 4;

    /// <summary>Longest accepted character name.</summary>
    public const int MaxNameLength = 40;

    /// <summary>
    /// Validate a character name against the rules and the already-taken names.
    /// Returns the list of violated error codes (empty when the name is valid).
    /// </summary>
    /// <param name="name">The requested name.</param>
    /// <param name="takenNames">Names that already belong to a character (compared case-insensitively).</param>
    public static List<string> ValidateName(string name, IEnumerable<string> takenNames)
    {
        var reasons = new List<string>();

        if (string.IsNullOrEmpty(name))
        {
            reasons.Add(AccountErrors.ErrNameTooShort);
            return reasons;
        }

        if (name.Length < MinNameLength)
        {
            reasons.Add(AccountErrors.ErrNameTooShort);
        }

        if (name.Length > MaxNameLength)
        {
            reasons.Add(AccountErrors.ErrNameTooLong);
        }

        if (reasons.Count == 0 && char.IsDigit(name[0]))
        {
            reasons.Add(AccountErrors.ErrNameStartsWithNumber);
        }

        if (reasons.Count == 0 && HasInvalidCharacters(name))
        {
            reasons.Add(AccountErrors.ErrInvalidCharacter);
        }

        if (reasons.Count == 0 && takenNames.Any(taken => string.Equals(taken, name, StringComparison.OrdinalIgnoreCase)))
        {
            reasons.Add(AccountErrors.ErrNameInUse);
        }

        return reasons;
    }

    /// <summary>
    /// Whether a record is one of the built-in zone-picker seed entries (which
    /// may be replaced by a created character) rather than somebody's character:
    /// seed entries are never custom and still carry their seed zone name.
    /// </summary>
    public static bool IsZoneSeedEntry(CharacterRecord record)
    {
        return !record.IsCustom && CharacterStore.IsSeedZoneName(record.Name);
    }

    /// <summary>
    /// Whether a created character may take over this slot: only untouched seed
    /// entries can be replaced — a character that already lives here keeps it
    /// (the creation then moves on to the account's next free slot instead of
    /// failing; it only runs out of slots once every guid the scheme can
    /// address is taken, see <see cref="CharacterStore.MaxCharacterSlotsPerAccount"/>).
    /// </summary>
    public static bool TryClaimSlot(CharacterRecord existingSlot, out string errorCode)
    {
        errorCode = null;

        if (existingSlot != null && !IsZoneSeedEntry(existingSlot))
        {
            errorCode = AccountErrors.ErrDuplicateCharacter;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Build the record for a newly created character. The visual color ids
    /// come from the creation request and are resolved to their packed ARGB
    /// values via <see cref="CharacterColorPalettes"/> (precomputed from
    /// <c>dbvisualrecords::WarpaintPalette</c> using the same
    /// <c>FColor.CombineLightDark</c> formula the GameServer uses), so the
    /// skin/eye/hair color matches what the player picked on the creation
    /// form instead of every character silently inheriting the default
    /// template's face. The armor colors (warpaint) come from the chosen
    /// chassis' own stock colors in the static database — never from the admin
    /// account's hardcoded Raptor paint, and never empty, because an empty
    /// warpaint makes the client fall back to that same purple default avatar
    /// (which is how created characters used to look like the admin's).
    /// </summary>
    public static CharacterRecord Create(
        ulong accountId,
        ulong characterGuid,
        int sortOrder,
        string name,
        uint gender,
        uint battleframeSdbId,
        uint head,
        uint eyes,
        uint voiceSet,
        uint skinColorItemId,
        uint eyeColorItemId,
        uint lipColorItemId,
        uint hairColorItemId,
        uint facialHairColorItemId,
        uint headAccessoryA,
        uint headAccessoryB,
        uint facialHair)
    {
        // Body colors come from dbvisualrecords::WarpaintPalette rows identified
        // by the palette id the client sends. Resolve each chosen palette to its
        // packed light-dark ARGB color the same way FColor.CombineLightDark does
        // (low 16 bits = shadow RGB565, high 16 bits = highlight RGB565), so the
        // stored value matches the color the player picked on the creation form
        // instead of silently inheriting the default template's skin/hair/eye.
        // A missing/unknown palette id falls back to the hardcoded template
        // default for that slot, which keeps legacy/hand-crafted creates safe.
        var skinColor = ResolveBodyColor(skinColorItemId, DefaultCharacterTemplate.SkinColor);
        var eyeColor = ResolveBodyColor(eyeColorItemId, DefaultCharacterTemplate.EyeColor);
        var lipColor = ResolveBodyColor(lipColorItemId, DefaultCharacterTemplate.LipColor);
        var hairColor = ResolveBodyColor(hairColorItemId, DefaultCharacterTemplate.HairColor);
        var facialHairColor = ResolveBodyColor(facialHairColorItemId, DefaultCharacterTemplate.FacialHairColor);

        // The head accessory list the client sends is a pair: accessory A is the
        // hair mesh, accessory B is the facial-hair mesh. The original service's
        // data also carries a dedicated facial_hair slot; both are worn as the
        // leading entries of the head-accessories array (see
        // CharacterAppearance.HeadAccessoryMeshes), which is what lets both the
        // selection screen preview and the in-game character see the beard.
        // Prefer the client's explicit facialHair value when it differs from
        // headAccessoryB so that any future UI that separates them still works,
        // and fall back to B when it isn't supplied (both are 0 when nothing is
        // selected, which matches the neutral defaults).
        var facialHairMesh = facialHair != 0 ? facialHair : headAccessoryB;
        var hairMesh = headAccessoryA;

        var accessories = new List<uint>();
        AddAccessory(accessories, hairMesh);
        AddAccessory(accessories, facialHairMesh);

        var template = new CharacterVisualsRecord
        {
            Head = head,
            Eyes = eyes,
            VoiceSet = voiceSet,
            // The creation form has no glider / vehicle picker yet; new
            // characters start with none (0) rather than inheriting whatever
            // the admin template last carried.
            Glider = 0,
            Vehicle = 0,
            Hair = hairMesh,
            FacialHair = facialHairMesh,
            SkinColorId = skinColorItemId,
            EyeColorId = eyeColorItemId,
            LipColorId = lipColorItemId,
            HairColorId = hairColorItemId,
            FacialHairColorId = facialHairColorItemId,
            SkinColor = skinColor,
            EyeColor = eyeColor,
            LipColor = lipColor,
            HairColor = hairColor,
            FacialHairColor = facialHairColor,
            HeadAccessories = accessories,
            // No head-accessory tint on a fresh character — the selection form
            // doesn't expose a tint picker, so the neutral 0 is the right
            // starting value (the admin template's HeadAccessoryColor was for
            // the previews, not a character default).
            HeadAccessoryColor = 0,
            // The frame's own stock armor colors (see ChassisDefaultWarpaints):
            // a chassis without a resolvable default palette keeps an empty
            // warpaint, and the GameServer wears its default SDB colors for it.
            WarpaintId = 0,
            Warpaint = ChassisDefaultWarpaints.TryGet(battleframeSdbId, out var chassisWarpaint)
                ? [.. chassisWarpaint]
                : []
        };

        return new CharacterRecord
        {
            AccountId = accountId,
            CharacterGuid = characterGuid,
            IsCustom = true,
            Name = name.Trim(),
            SortOrder = sortOrder,
            Gender = gender,
            // The creation screen only offers one race (Human, 0); anything
            // other than 0 would be a hand-crafted request, which we clamp to
            // 0 so the character never inherits the admin's race by accident.
            Race = 0,
            // Fresh characters have no title; setting this explicitly keeps
            // them from carrying whatever the admin template has if that
            // constant ever changes.
            TitleId = 0,
            CurrentBattleframeSDBId = battleframeSdbId,
            // A fresh battleframe starts at progression level 1 (see
            // FrameProgressionLevel); the seed zone entries keep the template
            // levels (10) instead.
            CurrentLevel = 1,
            MaxFrameLevel = 1,
            // A brand new character is not in an army yet: the property
            // initializer defaults (ArmyTag="ARMY", ArmyGuid=1,
            // ArmyIsOfficer=true) match the admin seed, so stamp "no army"
            // explicitly rather than leaking those placeholder values.
            ArmyTag = string.Empty,
            ArmyGuid = 0,
            ArmyIsOfficer = false,
            TimePlayed = 0,
            LastZoneId = (uint)(characterGuid & 0xffff),
            LastOutpostId = 0,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            Visuals = template
        };
    }

    private static void AddAccessory(List<uint> accessories, uint id)
    {
        if (id != 0 && !accessories.Contains(id))
        {
            accessories.Add(id);
        }
    }

    /// <summary>
    /// Resolve a character-creation color palette id (skin/eye/lip/hair/facial
    /// hair) to its packed light-dark ARGB color. Zero or unknown ids fall back
    /// to the hardcoded default for that slot (DefaultCharacterTemplate), so a
    /// character created with an omitted/invalid palette (e.g. a hand-crafted
    /// request, an older client, or an SDB mismatch) still renders with a
    /// sane face instead of pure black.
    /// </summary>
    private static uint ResolveBodyColor(uint paletteId, uint fallback)
    {
        if (paletteId != 0 && CharacterColorPalettes.TryGet(paletteId, out var color))
        {
            return color;
        }

        return fallback;
    }

    /// <summary>Only ASCII letters, digits and spaces — the characters the original names may contain.</summary>
    private static bool HasInvalidCharacters(string name)
    {
        foreach (var character in name)
        {
            if (character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or ' ')
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
