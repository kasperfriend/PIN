using System.Collections.Generic;
using System.Linq;

namespace Shared.Common.Characters;

/// <summary>
/// Resolves which persisted character a GameServer request refers to, given the
/// character guid the client sent (and, when available, the zone id carried in
/// the same command).
///
/// Pure logic, kept separate from <see cref="CharacterStore"/> so the resolution
/// rules can be unit tested without the process-wide store.
///
/// The resolution has to cope with the way the original client treats guids:
/// several flows overwrite the low byte of the guid (the GameServer itself adds
/// <c>0xFE</c> when handing guids to the client), and characters of different
/// accounts can share the same zone id, so a zone-only lookup is ambiguous the
/// moment more than one account exists.
/// </summary>
public static class CharacterResolver
{
    /// <summary>Masks off the byte the client overwrites.</summary>
    private const ulong LowByteMask = 0xffffffffffffff00;

    /// <summary>Masks off the zone id, leaving the guid prefix and account id bits.</summary>
    private const ulong PrefixAndAccountMask = 0xffffffffffff0000;

    private const ulong ZoneMask = 0xffff;

    /// <summary>
    /// Resolve a character by guid alone. Used on the login path, where the
    /// client sends the guid it picked from the character list.
    /// </summary>
    public static CharacterRecord Find(IEnumerable<CharacterRecord> characters, ulong characterGuid)
    {
        var candidates = AsList(characters);

        // Legacy behaviour of CharacterStore.Get: an unknown guid still resolved
        // to the admin account's entry for the zone encoded in its low 16 bits.
        // Kept so odd callers keep getting a character instead of a failure.
        return FindByGuid(candidates, characterGuid)
               ?? FindClobberedUnique(candidates, characterGuid)
               ?? FindLegacyZone(candidates, characterGuid & ZoneMask);
    }

    /// <summary>
    /// Resolve a character by guid with the authoritative zone id from the same
    /// command (session data, battleframe saves). The zone id is exact even when
    /// the guid's low byte was clobbered.
    /// </summary>
    public static CharacterRecord Find(IEnumerable<CharacterRecord> characters, ulong characterGuid, uint zoneId)
    {
        var candidates = AsList(characters);
        return FindByGuid(candidates, characterGuid)
               ?? FindClobberedUnique(candidates, characterGuid)
               ?? FindByAccountAndZone(candidates, characterGuid, zoneId)
               ?? FindLegacyZone(candidates, zoneId);
    }

    private static IReadOnlyList<CharacterRecord> AsList(IEnumerable<CharacterRecord> characters)
    {
        return characters as IReadOnlyList<CharacterRecord> ?? characters.ToList();
    }

    /// <summary>1. The untouched guid — the normal case on the login path.</summary>
    private static CharacterRecord FindByGuid(IReadOnlyList<CharacterRecord> characters, ulong characterGuid)
    {
        return characters.FirstOrDefault(c => c.CharacterGuid == characterGuid);
    }

    /// <summary>
    /// 2. The guid with its low byte overwritten. Only unambiguous when exactly
    /// one character shares the upper seven bytes (several of the seeded zones
    /// share a zone-id high byte, e.g. 1089..1173 all sit in 0x04xx, so this
    /// regularly stays ambiguous and the zone id has to decide).
    /// </summary>
    private static CharacterRecord FindClobberedUnique(IReadOnlyList<CharacterRecord> characters, ulong characterGuid)
    {
        var masked = characterGuid & LowByteMask;
        CharacterRecord match = null;
        var count = 0;
        foreach (var character in characters)
        {
            if ((character.CharacterGuid & LowByteMask) != masked)
            {
                continue;
            }

            match = character;
            count++;
            if (count > 1)
            {
                return null;
            }
        }

        return count == 1 ? match : null;
    }

    /// <summary>
    /// 3. The account bits of the (possibly clobbered) guid plus the exact zone
    /// id from the command payload. The account id encoded in a generated guid
    /// prefix keeps this scoped to one account.
    /// </summary>
    private static CharacterRecord FindByAccountAndZone(IReadOnlyList<CharacterRecord> characters, ulong characterGuid, uint zoneId)
    {
        if (zoneId == 0)
        {
            return null;
        }

        var accountKey = characterGuid & PrefixAndAccountMask;
        return characters.FirstOrDefault(c =>
                                             (c.CharacterGuid & PrefixAndAccountMask) == accountKey
                                             && (c.CharacterGuid & ZoneMask) == zoneId);
    }

    /// <summary>4. Legacy fallback: the admin account's entry for a zone id.</summary>
    private static CharacterRecord FindLegacyZone(IReadOnlyList<CharacterRecord> characters, ulong zoneId)
    {
        if (zoneId == 0)
        {
            return null;
        }

        return characters.FirstOrDefault(c => c.CharacterGuid == CharacterStore.GuidPrefix + zoneId);
    }
}
