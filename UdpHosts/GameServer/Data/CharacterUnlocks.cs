using System;
using System.Collections.Generic;
using System.Linq;
using AeroMessages.GSS.Character.Event;

namespace GameServer.Data;

/// <summary>
///     Everything a character owns that is not an item: cosmetic unlocks (warpaints, decals, patterns,
///     ornaments, hairstyles, titles), certificates, unlocked battleframes, account groups (VIP, an LGV
///     rental) and the XP/crystite/reputation boosts. This is what the unlock consumables write to and
///     what the <c>RequireHas*</c> chain nodes read.
///     <para>
///         The unlock groups use the group keys of the client's <c>UnlocksUpdate</c> message
///         (<c>certificate</c>, <c>titles</c>, <c>decals</c>, <c>czi_patterns</c>, <c>visual_overrides</c>,
///         <c>warpaints</c>, ...) so the state can be replicated as-is, and the keys the
///         <c>aptfs::RequireHasUnlockCommandDef</c> rows of the client database use to ask about them.
///     </para>
/// </summary>
public sealed class CharacterUnlocks
{
    public const string Certificates = "certificate";
    public const string Titles = "titles";
    public const string Decals = "decals";
    public const string Patterns = "czi_patterns";
    public const string VisualOverrides = "visual_overrides";
    public const string Warpaints = "warpaints";
    public const string Ornaments = "ornaments";
    public const string HeadAccessories = "head_accessories";
    public const string Battleframes = "battleframes";
    public const string Applied = "applied";

    /// <summary>The boost types <see cref="ActiveBoost" /> knows, matching the character's XP/Resource/Reputation modifier props.</summary>
    public const string BoostXp = "xp";
    public const string BoostCrystite = "crystite";
    public const string BoostReputation = "reputation";

    private readonly Dictionary<string, HashSet<uint>> _groups = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AccountGroupMembership> _accountGroups = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ActiveBoost> _boosts = [];
    private readonly Dictionary<uint, int> _reputation = [];

    /// <summary>A membership in a named account group, until <see cref="ExpiresAt" /> (unix seconds; 0 = never).</summary>
    public sealed record AccountGroupMembership(string Group, ulong ExpiresAt);

    /// <summary>
    ///     A boost: <see cref="Type" /> is one of the <c>Boost*</c> constants, <see cref="Percent" /> the bonus,
    ///     <see cref="ExpiresAt" /> unix seconds (0 = permanent), <see cref="EffectId" /> the status effect that
    ///     visualises it on the character (0 for none).
    /// </summary>
    public sealed record ActiveBoost(string Type, uint Percent, ulong ExpiresAt, uint EffectId);

    public IReadOnlyCollection<ActiveBoost> Boosts => _boosts;

    public IReadOnlyCollection<AccountGroupMembership> AccountGroups => _accountGroups.Values;

    public IReadOnlyDictionary<uint, int> Reputation => _reputation;

    public IEnumerable<string> Groups => _groups.Keys;

    /// <summary>Grants an unlock. Returns false when the character already had it.</summary>
    public bool Unlock(string group, uint id)
    {
        if (string.IsNullOrEmpty(group) || id == 0)
        {
            return false;
        }

        if (!_groups.TryGetValue(group, out var set))
        {
            set = [];
            _groups[group] = set;
        }

        return set.Add(id);
    }

    public bool Revoke(string group, uint id) =>
        !string.IsNullOrEmpty(group) && _groups.TryGetValue(group, out var set) && set.Remove(id);

    public bool Has(string group, uint id) =>
        !string.IsNullOrEmpty(group) && _groups.TryGetValue(group, out var set) && set.Contains(id);

    public IReadOnlyCollection<uint> Get(string group) =>
        !string.IsNullOrEmpty(group) && _groups.TryGetValue(group, out var set) ? set : [];

    public bool HasCertificate(uint certificateId) => Has(Certificates, certificateId);

    /// <summary>Adds (or extends) an account group membership; returns false when nothing changed.</summary>
    public bool AddAccountGroup(string group, uint durationSeconds, ulong now)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            return false;
        }

        ulong expiresAt = durationSeconds == 0 ? 0 : now + durationSeconds;
        if (_accountGroups.TryGetValue(group, out var existing))
        {
            if (existing.ExpiresAt == 0)
            {
                return false;
            }

            // Stacking rentals extend from the current end, not from now.
            expiresAt = durationSeconds == 0 ? 0 : Math.Max(existing.ExpiresAt, now) + durationSeconds;
        }

        _accountGroups[group] = new AccountGroupMembership(group, expiresAt);
        return true;
    }

    public bool RemoveAccountGroup(string group) => !string.IsNullOrEmpty(group) && _accountGroups.Remove(group);

    public bool IsInAccountGroup(string group, ulong now) =>
        !string.IsNullOrEmpty(group)
        && _accountGroups.TryGetValue(group, out var membership)
        && (membership.ExpiresAt == 0 || membership.ExpiresAt > now);

    /// <summary>
    ///     Adds a boost. A boost of the same type and percent extends the running one (two 50% packs
    ///     back to back give one 50% boost for twice as long, the way the live game stacked them); a
    ///     different percent runs alongside, the strongest active one counts.
    /// </summary>
    public ActiveBoost AddBoost(string type, uint percent, uint durationSeconds, bool permanent, uint effectId, ulong now)
    {
        if (string.IsNullOrEmpty(type) || percent == 0)
        {
            return null;
        }

        ulong expiresAt = permanent || durationSeconds == 0 ? 0 : now + durationSeconds;
        int index = _boosts.FindIndex(b => b.Type == type && b.Percent == percent);
        if (index >= 0)
        {
            var existing = _boosts[index];
            if (existing.ExpiresAt == 0)
            {
                return existing;
            }

            expiresAt = expiresAt == 0 ? 0 : Math.Max(existing.ExpiresAt, now) + durationSeconds;
            _boosts[index] = existing with { ExpiresAt = expiresAt, EffectId = effectId != 0 ? effectId : existing.EffectId };
            return _boosts[index];
        }

        var boost = new ActiveBoost(type, percent, expiresAt, effectId);
        _boosts.Add(boost);
        return boost;
    }

    /// <summary>Removes boosts by type, by visual effect, or all of them (both empty). Returns what went.</summary>
    public List<ActiveBoost> RemoveBoosts(string type = null, uint effectId = 0)
    {
        var removed = _boosts
            .Where(b => (string.IsNullOrEmpty(type) || b.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
                        && (effectId == 0 || b.EffectId == effectId))
            .ToList();
        _boosts.RemoveAll(removed.Contains);
        return removed;
    }

    /// <summary>The strongest active bonus of a type, as a fraction (0.5 for +50%).</summary>
    public float BoostFraction(string type, ulong now)
    {
        uint best = 0;
        foreach (var boost in _boosts)
        {
            if (boost.Type.Equals(type, StringComparison.OrdinalIgnoreCase) && (boost.ExpiresAt == 0 || boost.ExpiresAt > now))
            {
                best = Math.Max(best, boost.Percent);
            }
        }

        return best / 100f;
    }

    public int AddReputation(uint factionId, int amount)
    {
        _reputation.TryGetValue(factionId, out var current);
        current += amount;
        _reputation[factionId] = current;
        return current;
    }

    /// <summary>
    ///     Drops memberships and boosts whose time is up. Returns true when something expired, so the
    ///     owner can push the new modifier props to the client.
    /// </summary>
    public bool Expire(ulong now)
    {
        bool changed = false;
        foreach (var membership in _accountGroups.Values.Where(m => m.ExpiresAt != 0 && m.ExpiresAt <= now).ToList())
        {
            _accountGroups.Remove(membership.Group);
            changed = true;
        }

        changed |= _boosts.RemoveAll(b => b.ExpiresAt != 0 && b.ExpiresAt <= now) > 0;
        return changed;
    }

    /// <summary>The full-state <c>UnlocksUpdate</c> (every group), for login.</summary>
    public UnlocksUpdate BuildFullUpdate()
    {
        return new UnlocksUpdate
        {
            ClearExistingData = 1,
            Groups = [.. _groups.Where(pair => pair.Value.Count > 0).Select(pair => BuildGroup(pair.Key, pair.Value, []))],
        };
    }

    /// <summary>A partial <c>UnlocksUpdate</c> adding/removing ids in one group.</summary>
    public static UnlocksUpdate BuildPartialUpdate(string group, IEnumerable<uint> added, IEnumerable<uint> removed = null)
    {
        return new UnlocksUpdate
        {
            ClearExistingData = 0,
            Groups = [BuildGroup(group, added, removed ?? [])],
        };
    }

    /// <summary>Flat persistence form: one record per unlock, membership, boost and reputation standing.</summary>
    public List<CharacterUnlockRecord> ToRecords()
    {
        var records = new List<CharacterUnlockRecord>();
        foreach (var (group, ids) in _groups)
        {
            records.AddRange(ids.Select(id => new CharacterUnlockRecord(CharacterUnlockRecord.KindUnlock, group, id, 0, 0)));
        }

        records.AddRange(_accountGroups.Values.Select(m => new CharacterUnlockRecord(CharacterUnlockRecord.KindAccountGroup, m.Group, 0, 0, m.ExpiresAt)));
        records.AddRange(_boosts.Select(b => new CharacterUnlockRecord(CharacterUnlockRecord.KindBoost, b.Type, b.EffectId, (int)b.Percent, b.ExpiresAt)));
        records.AddRange(_reputation.Select(pair => new CharacterUnlockRecord(CharacterUnlockRecord.KindReputation, string.Empty, pair.Key, pair.Value, 0)));
        return records;
    }

    /// <summary>Replaces the state with persisted records (expired timed entries are dropped).</summary>
    public void LoadRecords(IEnumerable<CharacterUnlockRecord> records, ulong now)
    {
        _groups.Clear();
        _accountGroups.Clear();
        _boosts.Clear();
        _reputation.Clear();
        if (records == null)
        {
            return;
        }

        foreach (var record in records)
        {
            bool expired = record.ExpiresAt != 0 && record.ExpiresAt <= now;
            switch (record.Kind)
            {
                case CharacterUnlockRecord.KindUnlock:
                    Unlock(record.Group, record.Id);
                    break;
                case CharacterUnlockRecord.KindAccountGroup when !expired && !string.IsNullOrEmpty(record.Group):
                    _accountGroups[record.Group] = new AccountGroupMembership(record.Group, record.ExpiresAt);
                    break;
                case CharacterUnlockRecord.KindBoost when !expired && record.Value > 0 && !string.IsNullOrEmpty(record.Group):
                    _boosts.Add(new ActiveBoost(record.Group, (uint)record.Value, record.ExpiresAt, record.Id));
                    break;
                case CharacterUnlockRecord.KindReputation:
                    _reputation[record.Id] = record.Value;
                    break;
            }
        }
    }

    private static UnlockGroup BuildGroup(string key, IEnumerable<uint> added, IEnumerable<uint> removed)
    {
        return new UnlockGroup
        {
            Key = key,
            AddEntries = [.. added.Take(255).Select(id => new UnlockGroupEntry { UnlockId = id, HaveUnk1 = 0, HaveUnk2 = 0, HaveUnk3 = 0 })],
            RemEntries = [.. removed.Take(255).Select(id => new UnlockGroupEntrySmall { CertId = id, HaveUnk2 = 0 })],
        };
    }
}

/// <summary>
///     One persisted line of <see cref="CharacterUnlocks" />. <see cref="Kind" /> says how to read the rest:
///     an unlock (<see cref="Group" /> + <see cref="Id" />), an account group (<see cref="Group" /> +
///     <see cref="ExpiresAt" />), a boost (<see cref="Group" /> = type, <see cref="Value" /> = percent,
///     <see cref="Id" /> = visual effect, <see cref="ExpiresAt" />) or a faction standing
///     (<see cref="Id" /> = faction, <see cref="Value" /> = reputation).
/// </summary>
public sealed record CharacterUnlockRecord(string Kind, string Group, uint Id, int Value, ulong ExpiresAt)
{
    public const string KindUnlock = "unlock";
    public const string KindAccountGroup = "account_group";
    public const string KindBoost = "boost";
    public const string KindReputation = "reputation";
}
