using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shared.Common.Accounts;

namespace Shared.Common.Characters;

/// <summary>
/// Process-wide store of player characters, backed by a JSON file on disk.
///
/// Both the web ClientApi (which renders the character selection screen) and the
/// GRPC service the GameServer calls read from this same store, so selection and
/// in-game state cannot drift apart.
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
    /// expects. Seeds the account's zone-picker entries on first sight (covers
    /// accounts added by hand-editing accounts.json).
    /// </summary>
    public static IReadOnlyList<CharacterRecord> GetAll(ulong accountId)
    {
        Init();
        EnsureSeededForAccount(accountId);
        return Characters.Values.Where(c => c.AccountId == accountId).OrderBy(c => c.SortOrder).ToList();
    }

    /// <summary>
    /// Guid prefix for a character belonging to <paramref name="accountId"/>: the
    /// legacy <see cref="GuidPrefix"/> for the admin account (keeping existing
    /// files valid), <see cref="GeneratedAccountGuidPrefixBase"/> plus the account
    /// id for everyone else.
    /// </summary>
    public static ulong GuidPrefixForAccount(ulong accountId)
    {
        return accountId == AccountStore.AdminAccountId ? GuidPrefix : GeneratedAccountGuidPrefixBase | (accountId << 16);
    }

    /// <summary>
    /// Make sure an account owns its zone-picker entries. Every account gets its
    /// own copy of the seed list (with its own guid prefix), so the selection
    /// screen keeps working as a zone picker no matter which account is logged in.
    /// </summary>
    public static void EnsureSeededForAccount(ulong accountId)
    {
        Init();

        if (Characters.Values.Any(c => c.AccountId == accountId))
        {
            return;
        }

        lock (SaveLock)
        {
            if (Characters.Values.Any(c => c.AccountId == accountId))
            {
                return;
            }

            foreach (var character in BuildZoneSeed(accountId, GuidPrefixForAccount(accountId)))
            {
                Characters[character.CharacterGuid] = character;
            }

            Save();
        }
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
