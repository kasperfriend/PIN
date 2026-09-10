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
    /// entries can be replaced — a character that already lives here blocks the
    /// creation with <see cref="AccountErrors.ErrDuplicateCharacter"/>.
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
    /// Build the record for a newly created character. The visual color ids come
    /// from the creation request; the ARGB color values themselves stay at the
    /// default template's until appearance editing (NewYou) is served from the
    /// static database.
    /// </summary>
    public static CharacterRecord Create(
        ulong accountId,
        ulong characterGuid,
        int sortOrder,
        string name,
        uint gender,
        uint battleframeSdbId,
        uint head,
        uint voiceSet,
        uint skinColorItemId,
        uint eyeColorItemId,
        uint hairColorItemId,
        uint headAccessoryA)
    {
        var template = new CharacterVisualsRecord
        {
            Head = head,
            VoiceSet = voiceSet,
            SkinColorId = skinColorItemId,
            EyeColorId = eyeColorItemId,
            HairColorId = hairColorItemId,
            HeadAccessories = headAccessoryA != 0 ? [headAccessoryA] : []
        };

        return new CharacterRecord
        {
            AccountId = accountId,
            CharacterGuid = characterGuid,
            IsCustom = true,
            Name = name.Trim(),
            SortOrder = sortOrder,
            Gender = gender,
            CurrentBattleframeSDBId = battleframeSdbId,
            // A fresh battleframe starts at progression level 1 (see
            // FrameProgressionLevel); the seed zone entries keep the template
            // levels instead.
            CurrentLevel = 1,
            MaxFrameLevel = 1,
            LastZoneId = (uint)(characterGuid & 0xffff),
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            Visuals = template
        };
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
