using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using Shared.Common.Accounts;

namespace Shared.Common.Characters;

/// <summary>
/// Process-wide store of player characters, backed by a JSON file on disk.
///
/// Both the web ClientApi (which renders the character selection screen) and the
/// GRPC service the GameServer calls read from this same store, so selection and
/// in-game state cannot drift apart.
///
/// Accounts start fresh: a new account owns no characters until it creates one
/// through the client's character creation flow (<c>POST api/v1/characters</c>).
/// The 38 built-in zone-picker entries are the admin account's dev tool — they
/// are what lets an operator jump into any zone straight from the selection
/// screen — and are deliberately not given to anyone else (an earlier build
/// seeded every account with its own copy, which read as "every new account
/// already has the admin's characters"; those copies are pruned on load, see
/// <see cref="StaleZonePickerSeeds"/>).
///
/// This is deliberately a simple file-backed store rather than a real database:
/// it needs no external dependencies and matches how the rest of PIN keeps state.
/// </summary>
public static class CharacterStore
{
    /// <summary>Guid prefix used for the built-in seeded characters.</summary>
    public const ulong GuidPrefix = 0x99aabbccddee0000;

    /// <summary>
    /// Base of the guid prefix for characters of accounts created through the
    /// account system: <c>0xaa</c> marker | account id (bits 16..47) | zone id in
    /// the low 16 bits. Distinct from the admin account's legacy
    /// <see cref="GuidPrefix"/> so existing characters.json guids stay valid.
    /// </summary>
    public const ulong GeneratedAccountGuidPrefixBase = 0xaa00000000000000;

    /// <summary>
    /// Zone a newly created character spawns in and gets its guid slot for:
    /// New Eden, the open world every new character of the original game started
    /// its life in reach of.
    /// </summary>
    public const uint DefaultSpawnZoneId = 448;

    /// <summary>
    /// Character slots the guid scheme can address per account: the slot index
    /// beyond the first lives in guid bits 48..55 (see
    /// <see cref="CharacterGuidForSlot"/>), so an account can create at most
    /// this many characters (the account's reported character limit is the
    /// lower, client-visible bound).
    /// </summary>
    public const int MaxCharacterSlotsPerAccount = 256;

    private static readonly object SaveLock = new();

    private static readonly ConcurrentDictionary<ulong, CharacterRecord> Characters = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly (string Name, int ZoneId)[] SeedZones =
    [
        ("M22 Homecoming", 1181),
        ("M20 Razor Edge", 833),
        ("M19 Gatecrasher", 1171),
        ("M18 Vagrant Dawn", 1007),
        ("M17 SOS", 1151),
        ("M16 Unearthed", 864),
        ("M15 Agrievan", 803),
        ("M14 Icebreaker", 1008),
        ("M13 Accelerate", 1154),
        ("M12 Prison Break", 1155),
        ("M11 Consequence", 1114),
        ("M10 Off the Grid", 1106),
        ("M09 Taken", 1099),
        ("M08 Catch", 1134),
        ("M07 Trespass", 1101),
        ("M06 Safehouse", 1113),
        ("M05 No Exit", 1117),
        ("M04 Razorwind", 1102),
        ("M03 Crash Down", 1003),
        ("M02 Bathsheba", 1104),
        ("M01 Shadow", 1100),
        ("OP3 ARES Team", 1089),
        ("OP2 High Tide", 1093),
        ("OP1 Miru", 1069),
        ("TDM Refinery", 1147),
        ("Omnidyne-M Stadium", 844),
        ("Holdout Jericho", 1163),
        ("R1 Defense of Dredge", 1173),
        ("Epicenter Melding Tornado", 805),
        ("Abyss Melding Tornado", 865),
        ("Cinerarium", 868),
        ("Danger Room", 1162),
        ("Baneclaw Lair", 1051),
        ("Battlelab", 1125),
        ("Nothing", 12),
        ("Diamond Head", 162),
        ("Sertao", 1030),
        ("New Eden", 448)
    ];

    private static string _storePath;

    private static bool _initialised;

    /// <summary>
    /// Load the store from disk, seeding the default entries on first run.
    /// Safe to call more than once; only the first call does any work.
    /// </summary>
    /// <param name="storePath">Path of the JSON file to persist to.</param>
    public static void Init(string storePath = null)
    {
        lock (SaveLock)
        {
            if (_initialised)
            {
                return;
            }

            _storePath = storePath ?? Path.Combine(AppContext.BaseDirectory, "characters.json");

            if (File.Exists(_storePath))
            {
                try
                {
                    var json = File.ReadAllText(_storePath);
                    var loaded = JsonSerializer.Deserialize<List<CharacterRecord>>(json, JsonOptions);
                    if (loaded != null)
                    {
                        foreach (var character in loaded)
                        {
                            Characters[character.CharacterGuid] = character;
                        }
                    }
                }
                catch (Exception)
                {
                    // A corrupt or partially written store must not stop the servers from
                    // booting. Fall through and reseed instead.
                }
            }

            if (Characters.IsEmpty)
            {
                Seed();
                SaveUnsafe();
            }

            // Migration for stores written by the build that seeded the 38
            // zone-picker entries for every account: drop the untouched copies
            // so those accounts come back fresh (accounts start with no
            // characters now; the zone picker is the admin account's dev
            // tool). Never touches created characters, the admin's own entries
            // or anything hand-added with a non-seed name.
            // Created characters used to inherit the admin Raptor warpaint
            // (CharacterVisualsRecord defaults). Strip that copy so they wear
            // the chassis they actually created with.
            var strippedWarpaint = StripInheritedAdminWarpaint(Characters.Values);
            if (strippedWarpaint.Count > 0)
            {
                SaveUnsafe();
                Log.Warning(
                    "Cleared inherited admin Raptor warpaint from {Count} created character(s) in {StorePath}: new characters wear their chassis' own default colors",
                    strippedWarpaint.Count,
                    _storePath);
            }

            var staleSeeds = StaleZonePickerSeeds(Characters.Values);
            if (staleSeeds.Count > 0)
            {
                foreach (var staleSeed in staleSeeds)
                {
                    Characters.TryRemove(staleSeed.CharacterGuid, out _);
                }

                SaveUnsafe();

                // At Warning, the level the WebHostManager shows by default:
                // without this line, an operator wondering where a fresh
                // account's characters went would have no pointer to the
                // change that removed them.
                Log.Warning(
                    "Removed {StaleSeedCount} zone-picker seed entries of non-admin accounts from {StorePath}: accounts start fresh now, only the admin account keeps the zone picker (created characters are untouched)",
                    staleSeeds.Count,
                    _storePath);
            }

            _initialised = true;
        }
    }

    /// <summary>Get every character, ordered the way the selection screen expects.</summary>
    public static IReadOnlyList<CharacterRecord> GetAll()
    {
        Init();
        return Characters.Values.OrderBy(c => c.SortOrder).ThenBy(c => c.AccountId).ToList();
    }

    /// <summary>
    /// Get the characters of one account, ordered the way the selection screen
    /// expects. Accounts start with none — characters arrive through the
    /// creation flow (<see cref="TryCreateCharacter"/>) — and only the built-in
    /// admin account owns the seeded zone-picker entries.
    /// </summary>
    public static IReadOnlyList<CharacterRecord> GetAll(ulong accountId)
    {
        Init();
        return Characters.Values.Where(c => c.AccountId == accountId).OrderBy(c => c.SortOrder).ToList();
    }

    /// <summary>
    /// Guid prefix for a character belonging to <paramref name="accountId"/>: the
    /// legacy <see cref="GuidPrefix"/> for the admin account (keeping existing
    /// files valid), <see cref="GeneratedAccountGuidPrefixBase"/> plus the account
    /// id for everyone else. This prefix addresses an account's first character
    /// slot; characters beyond the first add their slot index in guid bits
    /// 48..55 (see <see cref="CharacterGuidForSlot"/>).
    /// </summary>
    public static ulong GuidPrefixForAccount(ulong accountId)
    {
        return accountId == AccountStore.AdminAccountId ? GuidPrefix : GeneratedAccountGuidPrefixBase | (accountId << 16);
    }

    /// <summary>
    /// The guid of the character an account creates in <paramref name="slot"/>:
    /// the spawn zone stays in the low 16 bits (the GameServer reads the zone to
    /// spawn into from there) and the slot index goes into bits 48..55, so an
    /// account can create more than one character. Slot 0 keeps the plain
    /// per-account prefix — for the admin account that is the legacy prefix, so
    /// a created character replaces the seeded "New Eden" zone-picker entry
    /// exactly the way it always did — while slots above 0 always use the
    /// generated range, because the legacy admin prefix already carries non-zero
    /// bits where the slot index goes.
    /// </summary>
    public static ulong CharacterGuidForSlot(ulong accountId, int slot)
    {
        if (slot == 0)
        {
            return GuidPrefixForAccount(accountId) + DefaultSpawnZoneId;
        }

        return GeneratedAccountGuidPrefixBase | ((ulong)slot << 48) | (accountId << 16) | DefaultSpawnZoneId;
    }

    /// <summary>Whether <paramref name="name"/> is one of the built-in zone-picker seed names.</summary>
    public static bool IsSeedZoneName(string name)
    {
        foreach (var (seedName, _) in SeedZones)
        {
            if (string.Equals(seedName, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Create a character for an account through the client's character creation
    /// flow (<c>POST api/v1/characters</c>).
    ///
    /// The new character takes the account's next free slot (see
    /// <see cref="CharacterGuidForSlot"/>): slot 0, which replaces an untouched
    /// seed entry — the admin account's "New Eden" zone-picker slot — while one
    /// is still there, and then the numbered slots above it, so a fresh account
    /// can create as many characters as its limit allows instead of exactly one.
    /// Every created character encodes the spawn zone
    /// (<see cref="DefaultSpawnZoneId"/>) in its guid, which is the scheme the
    /// GameServer resolves spawns by; a slot already owned by a custom character
    /// is skipped, never replaced.
    /// </summary>
    public static bool TryCreateCharacter(
        ulong accountId,
        string name,
        uint gender,
        uint battleframeSdbId,
        uint head,
        uint voiceSet,
        uint skinColorItemId,
        uint eyeColorItemId,
        uint hairColorItemId,
        uint headAccessoryA,
        out CharacterRecord character,
        out string errorCode,
        out string errorMessage)
    {
        character = null;
        errorCode = null;
        errorMessage = null;

        Init();

        // Names are reserved by real characters across all accounts (the seed
        // zone entries do not reserve their zone names).
        var takenNames = Characters.Values
                                   .Where(c => !CharacterCreation.IsZoneSeedEntry(c))
                                   .Select(c => c.Name)
                                   .ToList();

        var reasons = CharacterCreation.ValidateName(name, takenNames);
        if (reasons.Count > 0)
        {
            errorCode = AccountErrors.ErrNameInvalid;
            errorMessage = "That name is not available";
            return false;
        }

        // The account's next free slot: slot 0 unless a custom character
        // already lives there (an untouched seed entry is replaceable, a
        // created character is not — the creation moves on to the next slot
        // instead of failing).
        ulong characterGuid = 0;
        CharacterRecord existingSlot = null;
        int slot;
        for (slot = 0; slot < MaxCharacterSlotsPerAccount; slot++)
        {
            characterGuid = CharacterGuidForSlot(accountId, slot);
            if (!Characters.TryGetValue(characterGuid, out existingSlot) || CharacterCreation.TryClaimSlot(existingSlot, out _))
            {
                break;
            }
        }

        if (slot == MaxCharacterSlotsPerAccount)
        {
            errorCode = AccountErrors.ErrDuplicateCharacter;
            errorMessage = "This account has no free character slot";
            return false;
        }

        // A request without a usable start class keeps the default frame, so the
        // record is never built with an unknown chassis.
        if (battleframeSdbId == 0)
        {
            battleframeSdbId = DefaultCharacterTemplate.FrameSdbId;
        }

        // Keep the replaced seed's position in the selection list; a character
        // in a new slot sorts behind the account's existing entries.
        var sortOrder = existingSlot?.SortOrder ?? NextSortOrder(accountId);

        character = CharacterCreation.Create(
            accountId,
            characterGuid,
            sortOrder,
            name,
            gender,
            battleframeSdbId,
            head,
            voiceSet,
            skinColorItemId,
            eyeColorItemId,
            hairColorItemId,
            headAccessoryA);

        Characters[characterGuid] = character;
        Save();

        return true;
    }

    /// <summary>Sort position for a character in a new slot: behind the account's existing entries.</summary>
    private static int NextSortOrder(ulong accountId)
    {
        var highest = Characters.Values
                                .Where(c => c.AccountId == accountId)
                                .Select(c => (int?)c.SortOrder)
                                .Max();

        return (highest ?? -1) + 1;
    }

    /// <summary>
    /// Created characters used to inherit the admin Raptor warpaint
    /// (the colors <see cref="DefaultCharacterTemplate.Warpaint"/>
    /// used to stamp on every <see cref="CharacterVisualsRecord"/>). Clears
    /// that copy so they wear the chassis they actually created with. Admin
    /// zone-picker seeds and characters that already have a different paint
    /// job are left alone.
    /// </summary>
    public static IReadOnlyList<CharacterRecord> StripInheritedAdminWarpaint(IEnumerable<CharacterRecord> characters)
    {
        var stripped = new List<CharacterRecord>();
        var admin = DefaultCharacterTemplate.Warpaint;
        foreach (var character in characters)
        {
            if (character == null || !character.IsCustom)
            {
                continue;
            }

            var paint = character.Visuals?.Warpaint;
            if (paint == null || paint.Count != admin.Length)
            {
                continue;
            }

            var matches = true;
            for (var i = 0; i < admin.Length; i++)
            {
                if (paint[i] != admin[i])
                {
                    matches = false;
                    break;
                }
            }

            if (!matches)
            {
                continue;
            }

            character.Visuals.WarpaintId = 0;
            character.Visuals.Warpaint = [];
            stripped.Add(character);
        }

        return stripped;
    }

    /// <summary>
    /// The records to drop when a store was written by a build that seeded the
    /// zone-picker entries for <em>every</em> account: the untouched seed
    /// entries of accounts other than the built-in admin. Created characters,
    /// the admin account's own zone picker and anything renamed
    /// (pre-account-system records, hand-added entries) are never matched, so
    /// pruning only ever removes the never-touched copies.
    /// </summary>
    public static IReadOnlyList<CharacterRecord> StaleZonePickerSeeds(IEnumerable<CharacterRecord> characters)
    {
        return characters
            .Where(c => c.AccountId != AccountStore.AdminAccountId && CharacterCreation.IsZoneSeedEntry(c))
            .ToList();
    }

    /// <summary>Look up a single character, or null when it is not known.</summary>
    /// <remarks>
    /// The guid the client sends may have its low byte overwritten and is not
    /// unique per zone once several accounts exist, so the exact rules live in
    /// <see cref="CharacterResolver"/> (exact guid, then low-byte-clobbered
    /// match, then the admin account's entry for the zone encoded in the guid).
    /// </remarks>
    public static CharacterRecord Get(ulong characterGuid)
    {
        Init();
        return CharacterResolver.Find(Characters.Values, characterGuid);
    }

    /// <summary>Insert or replace a character and persist the store.</summary>
    public static void Upsert(CharacterRecord character)
    {
        ArgumentNullException.ThrowIfNull(character);

        Init();
        Characters[character.CharacterGuid] = character;
        Save();
    }

    /// <summary>
    /// Record the battleframe the player is currently using, so the next login
    /// and the selection screen both reflect it.
    /// </summary>
    /// <remarks>
    /// The guid the GameServer sends may have its low byte overwritten, but the
    /// command carries the real zone id separately; both go through
    /// <see cref="CharacterResolver"/>, which prefers the guid and falls back to
    /// the account bits of the guid plus the exact zone id.
    /// </remarks>
    public static void UpdateCurrentBattleframe(ulong characterGuid, uint zoneId, uint battleframeSdbId)
    {
        if (battleframeSdbId == 0)
        {
            return;
        }

        Init();

        var character = CharacterResolver.Find(Characters.Values, characterGuid, zoneId);

        if (character == null || character.CurrentBattleframeSDBId == battleframeSdbId)
        {
            return;
        }

        character.CurrentBattleframeSDBId = battleframeSdbId;
        Save();
    }

    /// <summary>Persist where the player logged out and how long they played.</summary>
    /// <remarks>
    /// Same resolution strategy as <see cref="UpdateCurrentBattleframe"/>: the
    /// guid first, then the account bits of the guid plus the exact zone id from
    /// the command payload.
    /// </remarks>
    public static void UpdateSessionData(ulong characterGuid, uint zoneId, uint outpostId, uint timePlayed)
    {
        Init();

        var character = CharacterResolver.Find(Characters.Values, characterGuid, zoneId);

        if (character == null)
        {
            return;
        }

        character.LastZoneId = zoneId;
        character.LastOutpostId = outpostId;
        character.TimePlayed = timePlayed;
        character.LastSeenAt = DateTime.UtcNow;
        Save();
    }

    /// <summary>
    /// Create the built-in entries for one account. Each one is a zone you can
    /// load into, which is how PIN has always used the selection screen. Pure
    /// function so the seeding can be unit tested.
    /// </summary>
    public static IReadOnlyList<CharacterRecord> BuildZoneSeed(ulong accountId, ulong guidPrefix)
    {
        var seed = new List<CharacterRecord>(SeedZones.Length);

        for (var i = 0; i < SeedZones.Length; i++)
        {
            var (name, zoneId) = SeedZones[i];
            seed.Add(new CharacterRecord
                     {
                         AccountId = accountId,
                         CharacterGuid = guidPrefix + (ulong)zoneId,
                         Name = name,
                         SortOrder = i,
                         LastZoneId = (uint)zoneId,
                         LastSeenAt = DateTime.UtcNow - TimeSpan.FromDays(365)
                     });
        }

        return seed;
    }

    /// <summary>Write the store back to disk.</summary>
    public static void Save()
    {
        lock (SaveLock)
        {
            SaveUnsafe();
        }
    }

    private static void SaveUnsafe()
    {
        if (_storePath == null)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(_storePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(Characters.Values.ToList(), JsonOptions);

            // Write to a temp file and move it into place so a crash mid-write
            // cannot leave a truncated store behind.
            var tempPath = _storePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _storePath, true);
        }
        catch (Exception)
        {
            // Persistence is best effort; never take a server down over it.
        }
    }

    /// <summary>
    /// First-run seed: the zone entries belong to the built-in admin account,
    /// which is also what records from before the account system deserialize as
    /// (see <see cref="CharacterRecord.AccountId"/>).
    /// </summary>
    private static void Seed()
    {
        foreach (var character in BuildZoneSeed(AccountStore.AdminAccountId, GuidPrefix))
        {
            Characters[character.CharacterGuid] = character;
        }
    }
}
