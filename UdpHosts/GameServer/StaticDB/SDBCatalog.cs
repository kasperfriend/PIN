namespace GameServer.StaticDB;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// The kinds of spawnable things PIN can look up in the static database
/// (`clientdb.sd2`). Each kind maps onto one SDB table and one
/// <see cref="Systems.EntityManager.EntityManager"/> spawn method.
/// </summary>
public enum SDBCatalogKind
{
    /// <summary>`dbcharacter::Monster` -> EntityManager.SpawnCharacter.</summary>
    Monster,

    /// <summary>`dbcharacter::Deployable` -> EntityManager.SpawnDeployable.</summary>
    Deployable,

    /// <summary>`vcs::VehicleInfo` -> EntityManager.SpawnVehicle.</summary>
    Vehicle,

    /// <summary>`dbitems::CarryableObject` -> EntityManager.SpawnCarryable.</summary>
    Carryable,

    /// <summary>`dbcharacter::Turret` -> EntityManager.SpawnTurret (needs a parent entity).</summary>
    Turret,
}

/// <summary>
/// One row of a spawnable static database table, reduced to the fields that
/// matter when browsing or spawning from chat / the admin console.
/// </summary>
public record class SDBCatalogEntry
{
    public SDBCatalogKind Kind { get; init; }

    public uint Id { get; init; }

    /// <summary>Resolved display name, or null when the row has no localized text.</summary>
    public string Name { get; init; }

    /// <summary>Faction internal name when the row has one.</summary>
    public string Faction { get; init; }

    /// <summary>Short kind-specific summary (behavior, category, class, ...).</summary>
    public string Summary { get; init; }

    public string DisplayName => Name ?? "<unnamed>";

    public override string ToString()
    {
        var parts = new List<string> { $"{Id,7}  {DisplayName}" };

        if (Faction != null)
        {
            parts.Add($"[{Faction}]");
        }

        if (!string.IsNullOrEmpty(Summary))
        {
            parts.Add(Summary);
        }

        return string.Join("  ", parts);
    }
}

/// <summary>
/// Browsing/searching layer over the static database, used by the `sdb`
/// chat and admin commands so the ~7,400 spawnable rows in `clientdb.sd2`
/// can be discovered in-game instead of by dumping the file offline.
/// </summary>
public static class SDBCatalog
{
    public const int DefaultSearchLimit = 20;
    public const int MaxSearchLimit = 200;

    private static readonly Dictionary<string, SDBCatalogKind> KindAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "monster", SDBCatalogKind.Monster },
        { "monsters", SDBCatalogKind.Monster },
        { "npc", SDBCatalogKind.Monster },
        { "npcs", SDBCatalogKind.Monster },
        { "char", SDBCatalogKind.Monster },
        { "character", SDBCatalogKind.Monster },
        { "characters", SDBCatalogKind.Monster },
        { "mob", SDBCatalogKind.Monster },
        { "mobs", SDBCatalogKind.Monster },
        { "deployable", SDBCatalogKind.Deployable },
        { "deployables", SDBCatalogKind.Deployable },
        { "dep", SDBCatalogKind.Deployable },
        { "vehicle", SDBCatalogKind.Vehicle },
        { "vehicles", SDBCatalogKind.Vehicle },
        { "veh", SDBCatalogKind.Vehicle },
        { "carryable", SDBCatalogKind.Carryable },
        { "carryables", SDBCatalogKind.Carryable },
        { "carry", SDBCatalogKind.Carryable },
        { "turret", SDBCatalogKind.Turret },
        { "turrets", SDBCatalogKind.Turret },
    };

    /// <summary>Human readable list of the accepted kind keywords.</summary>
    public static string KindList => "monster|deployable|vehicle|carryable|turret";

    public static bool TryParseKind(string value, out SDBCatalogKind kind)
    {
        kind = SDBCatalogKind.Monster;
        return value != null && KindAliases.TryGetValue(value, out kind);
    }

    /// <summary>The SDB table a kind is read from (for docs / feedback).</summary>
    public static string GetTableName(SDBCatalogKind kind) => kind switch
    {
        SDBCatalogKind.Monster => "dbcharacter::Monster",
        SDBCatalogKind.Deployable => "dbcharacter::Deployable",
        SDBCatalogKind.Vehicle => "vcs::VehicleInfo",
        SDBCatalogKind.Carryable => "dbitems::CarryableObject",
        SDBCatalogKind.Turret => "dbcharacter::Turret",
        _ => "?",
    };

    /// <summary>All rows of one kind, ordered by id.</summary>
    public static IEnumerable<SDBCatalogEntry> GetEntries(SDBCatalogKind kind)
    {
        switch (kind)
        {
            case SDBCatalogKind.Monster:
                foreach (var (id, row) in Ordered(SDBInterface.GetMonsters()))
                {
                    yield return new SDBCatalogEntry
                    {
                        Kind = kind,
                        Id = id,
                        Name = SDBInterface.GetLocalizedString(row.LocalizedNameId),
                        Faction = GetFactionName(row.FactionId),
                        Summary = string.IsNullOrEmpty(row.Behavior) ? null : $"behavior={row.Behavior}",
                    };
                }

                break;

            case SDBCatalogKind.Deployable:
                foreach (var (id, row) in Ordered(SDBInterface.GetDeployables()))
                {
                    yield return new SDBCatalogEntry
                    {
                        Kind = kind,
                        Id = id,
                        Name = SDBInterface.GetLocalizedString(row.LocalizedNameId),
                        Faction = GetFactionName(row.DefaultFaction),
                        Summary = $"hp={row.StandardHealth}",
                    };
                }

                break;

            case SDBCatalogKind.Vehicle:
                foreach (var (id, row) in Ordered(SDBInterface.GetVehicleInfos()))
                {
                    yield return new SDBCatalogEntry
                    {
                        Kind = kind,
                        Id = id,
                        Name = SDBInterface.GetLocalizedString(row.LocalizedNameId),
                        Faction = GetFactionName(row.FactionId),
                        Summary = $"class={row.VehicleClass}",
                    };
                }

                break;

            case SDBCatalogKind.Carryable:
                foreach (var (id, row) in Ordered(SDBInterface.GetCarryableObjects()))
                {
                    yield return new SDBCatalogEntry
                    {
                        Kind = kind,
                        Id = id,
                        Name = SDBInterface.GetLocalizedString(row.LocalizedNameId),
                        Summary = $"type={row.Type}",
                    };
                }

                break;

            case SDBCatalogKind.Turret:
                foreach (var (id, row) in Ordered(SDBInterface.GetTurrets()))
                {
                    yield return new SDBCatalogEntry
                    {
                        Kind = kind,
                        Id = id,

                        // Turret rows carry a plain-text name instead of a localization id.
                        Name = string.IsNullOrWhiteSpace(row.Name) ? null : row.Name,
                        Summary = $"posture={row.Posture}",
                    };
                }

                break;
        }
    }

    /// <summary>True when a row with this id exists for the given kind.</summary>
    public static bool Exists(SDBCatalogKind kind, uint id) => kind switch
    {
        SDBCatalogKind.Monster => SDBInterface.GetMonster(id) != null,
        SDBCatalogKind.Deployable => SDBInterface.GetDeployable(id) != null,
        SDBCatalogKind.Vehicle => id <= ushort.MaxValue && SDBInterface.GetVehicleInfo((ushort)id) != null,
        SDBCatalogKind.Carryable => SDBInterface.GetCarryableObject(id) != null,
        SDBCatalogKind.Turret => SDBInterface.GetTurret(id) != null,
        _ => false,
    };

    public static SDBCatalogEntry GetEntry(SDBCatalogKind kind, uint id)
    {
        return Exists(kind, id) ? GetEntries(kind).FirstOrDefault(entry => entry.Id == id) : null;
    }

    /// <summary>
    /// Search a kind by numeric id or by (case-insensitive substring of the)
    /// localized name. Exact id and exact name matches are ranked first.
    /// </summary>
    public static List<SDBCatalogEntry> Search(SDBCatalogKind kind, string query, int limit = DefaultSearchLimit)
    {
        limit = Math.Clamp(limit, 1, MaxSearchLimit);
        query = query?.Trim() ?? string.Empty;

        if (query.Length == 0)
        {
            return [.. GetEntries(kind).Take(limit)];
        }

        bool isId = uint.TryParse(query, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint queryId);

        return [.. GetEntries(kind)
            .Select(entry => new
            {
                Entry = entry,
                Rank = Rank(entry, query, isId, queryId),
            })
            .Where(match => match.Rank >= 0)
            .OrderBy(match => match.Rank)
            .ThenBy(match => match.Entry.Id)
            .Take(limit)
            .Select(match => match.Entry)];
    }

    /// <summary>
    /// Resolve a spawn argument that is either a numeric id or a name. Returns
    /// false and fills <paramref name="error"/> when nothing (or too much)
    /// matched, so callers can hand the message straight to the player.
    /// </summary>
    public static bool TryResolve(SDBCatalogKind kind, string query, out uint id, out string error)
    {
        id = 0;
        error = null;

        if (uint.TryParse(query, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed))
        {
            if (!Exists(kind, parsed))
            {
                error = $"No {kind.ToString().ToLowerInvariant()} with id {parsed} in {GetTableName(kind)}";
                return false;
            }

            id = parsed;
            return true;
        }

        var matches = Search(kind, query, 6);
        if (matches.Count == 0)
        {
            error = $"No {kind.ToString().ToLowerInvariant()} matching \"{query}\" in {GetTableName(kind)}";
            return false;
        }

        var exact = matches.Where(entry => string.Equals(entry.Name, query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 1)
        {
            id = exact[0].Id;
            return true;
        }

        if (matches.Count > 1 && exact.Count != 1)
        {
            error = $"\"{query}\" is ambiguous: {string.Join(", ", matches.Select(entry => $"{entry.Id} {entry.DisplayName}"))}";
            return false;
        }

        id = matches[0].Id;
        return true;
    }

    /// <summary>Multi-line detail view of a single row, for the `sdbinfo` commands.</summary>
    public static string Describe(SDBCatalogKind kind, uint id)
    {
        var entry = GetEntry(kind, id);
        if (entry == null)
        {
            return $"No {kind.ToString().ToLowerInvariant()} with id {id} in {GetTableName(kind)}";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{GetTableName(kind)} #{id}: {entry.DisplayName}");

        switch (kind)
        {
            case SDBCatalogKind.Monster:
            {
                var row = SDBInterface.GetMonster(id);
                sb.AppendLine($"  faction        : {row.FactionId} ({entry.Faction ?? "?"})");
                sb.AppendLine($"  chassis        : {row.ChassisId}   backpack: {row.BackpackId}");
                sb.AppendLine($"  weapons        : {row.Weapon1Id} / {row.Weapon2Id}");
                sb.AppendLine($"  behavior       : {Or(row.Behavior)} (off: {Or(row.BehaviorOffensive)}, def: {Or(row.BehaviorDefensive)})");
                sb.AppendLine($"  scaling table  : {row.ScalingTableId}   health regen: {row.HealthRegen}");
                sb.AppendLine($"  speed          : normal {row.NormalSpeed}, fast {row.FastSpeed}");
                sb.AppendLine($"  body           : radius {row.BodyRadius}, mass {row.BodyMass}");
                sb.AppendLine($"  loot tables    : {row.LootTableId} / {row.LootTable2Id}");
                sb.AppendLine($"  ai spawn delay : {row.AiSpawnDelayMs} ms");
                sb.AppendLine($"  spawn with     : npc {id} [<x> <y> <z>]");
                break;
            }

            case SDBCatalogKind.Deployable:
            {
                var row = SDBInterface.GetDeployable(id);
                sb.AppendLine($"  default faction: {row.DefaultFaction} ({entry.Faction ?? "?"})");
                sb.AppendLine($"  health         : {row.StandardHealth} (start {row.StartHitpoints})");
                sb.AppendLine($"  category       : {row.DeployableCategory}   function: {row.Function}");
                sb.AppendLine($"  scale          : {row.Scale}   scope range: {row.ScopeRange}");
                sb.AppendLine($"  build time     : {row.BuildTimeMs} ms");
                sb.AppendLine($"  spawn with     : deployable {id} [<x> <y> <z>]");
                break;
            }

            case SDBCatalogKind.Vehicle:
            {
                var row = SDBInterface.GetVehicleInfo((ushort)id);
                sb.AppendLine($"  faction        : {row.FactionId} ({entry.Faction ?? "?"})");
                sb.AppendLine($"  vehicle class  : {row.VehicleClass}   race: {row.Race}");
                sb.AppendLine($"  scaling table  : {row.ScalingTableId}");
                sb.AppendLine($"  spawn with     : vehicle {id} [<x> <y> <z>]");
                break;
            }

            case SDBCatalogKind.Carryable:
            {
                var row = SDBInterface.GetCarryableObject(id);
                sb.AppendLine($"  type           : {row.Type}   visual record: {row.VisualRecordId}");
                sb.AppendLine($"  pickup radius  : {row.PickupRadius} (thrown {row.ThrownPickupRadius})");
                sb.AppendLine($"  granted ability: {row.AbilityGrantedId}");
                sb.AppendLine($"  spawn with     : carryable {id} [<x> <y> <z>]");
                break;
            }

            case SDBCatalogKind.Turret:
            {
                var row = SDBInterface.GetTurret(id);
                sb.AppendLine($"  posture        : {row.Posture}   attack type: {row.AttackType}");
                sb.AppendLine($"  pitch          : {row.MinPitch} .. {row.MaxPitch}");
                sb.AppendLine($"  yaw            : {row.MinYaw} .. {row.MaxYaw}");
                sb.AppendLine($"  behavior       : {Or(row.Behavior)}   visual record: {row.Visualrec}");
                sb.AppendLine("  spawn with     : turret <turretTypeId> (attaches to your character)");
                break;
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>`dbcharacter::MonsterScaling` health/damage for a level.</summary>
    public static string DescribeScaling(uint level)
    {
        var row = SDBInterface.GetMonsterScaling(level);
        return row == null
            ? $"No dbcharacter::MonsterScaling row for level {level}"
            : $"MonsterScaling level {row.Level}: health {row.Health}, damage {row.Damage}";
    }

    /// <summary>Row counts per kind, for the `sdb` overview command.</summary>
    public static string Overview()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Static database (clientdb.sd2) spawnable catalog:");

        foreach (var kind in Enum.GetValues<SDBCatalogKind>())
        {
            var entries = GetEntries(kind).ToList();
            var named = entries.Count(entry => entry.Name != null);
            sb.AppendLine($"  {kind,-10} {entries.Count,6} rows ({named} named)  {GetTableName(kind)}");
        }

        sb.AppendLine($"  scaling    {SDBInterface.GetMonsterScalings()?.Count ?? 0,6} rows              dbcharacter::MonsterScaling");
        return sb.ToString().TrimEnd();
    }

    private static string Or(string value) => string.IsNullOrWhiteSpace(value) ? "-" : value;

    private static IEnumerable<(uint Id, T Row)> Ordered<TKey, T>(IReadOnlyDictionary<TKey, T> source)
        where TKey : struct, IConvertible
    {
        if (source == null)
        {
            yield break;
        }

        var ordered = source
            .Select(pair => (Id: Convert.ToUInt32(pair.Key, CultureInfo.InvariantCulture), Row: pair.Value))
            .OrderBy(pair => pair.Id);

        foreach (var pair in ordered)
        {
            yield return pair;
        }
    }

    private static string GetFactionName(uint factionId)
    {
        var faction = factionId == 0 ? null : SDBInterface.GetFaction(factionId);
        if (faction == null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(faction.InternalName)
            ? SDBInterface.GetLocalizedString(faction.LocalizedNameId)
            : faction.InternalName;
    }

    private static int Rank(SDBCatalogEntry entry, string query, bool isId, uint queryId)
    {
        if (isId && entry.Id == queryId)
        {
            return 0;
        }

        if (entry.Name == null)
        {
            return -1;
        }

        if (string.Equals(entry.Name, query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (entry.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ? 3 : -1;
    }
}
