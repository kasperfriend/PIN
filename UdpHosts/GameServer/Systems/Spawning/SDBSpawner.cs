namespace GameServer.Systems.Spawning;

using System;
using System.Globalization;
using System.Numerics;
using GameServer.StaticDB;

/// <summary>
/// Shared implementation of the generic "spawn something from the static
/// database" flow, so the chat command and the admin/server command behave
/// identically. Everything here works on plain strings and returns the
/// feedback message the caller should show, keeping the command classes thin.
/// </summary>
public static class SDBSpawner
{
    /// <summary>
    /// Parse and execute `spawn &lt;kind&gt; &lt;id|name&gt; [x y z]`.
    /// </summary>
    /// <param name="parameters">Command parameters, excluding the command name.</param>
    /// <param name="shard">Shard the entity is spawned into.</param>
    /// <param name="sourcePlayer">Player issuing the command; may be null (console).</param>
    /// <returns>Feedback message for the caller.</returns>
    public static string Spawn(string[] parameters, IShard shard, INetworkPlayer sourcePlayer)
    {
        if (parameters == null || parameters.Length < 2)
        {
            return $"Usage: spawn <{SDBCatalog.KindList}> <id|name> [<x> <y> <z>]";
        }

        if (!SDBCatalog.TryParseKind(parameters[0], out var kind))
        {
            return $"Unknown kind \"{parameters[0]}\". Expected one of: {SDBCatalog.KindList}";
        }

        // Everything between the kind and an optional trailing "x y z" is the
        // id or (possibly multi-word) name, e.g. spawn monster Melded Wyrm 1 2 3.
        bool hasPosition = parameters.Length >= 5 && TryParsePosition(parameters, parameters.Length - 3, out _);
        int queryEnd = hasPosition ? parameters.Length - 3 : parameters.Length;
        string query = string.Join(' ', parameters[1..queryEnd]);

        if (query.Length == 0)
        {
            return $"Usage: spawn <{SDBCatalog.KindList}> <id|name> [<x> <y> <z>]";
        }

        if (!SDBCatalog.TryResolve(kind, query, out uint typeId, out string error))
        {
            return error;
        }

        var character = sourcePlayer?.CharacterEntity;

        Vector3 position;
        if (hasPosition)
        {
            TryParsePosition(parameters, parameters.Length - 3, out position);
        }
        else if (character != null)
        {
            position = character.Position;
        }
        else
        {
            return "Must provide a position when no player character is available";
        }

        var orientation = character?.Orientation ?? Quaternion.Identity;
        var entry = SDBCatalog.GetEntry(kind, typeId);
        var label = $"{kind.ToString().ToLowerInvariant()} {typeId} ({entry?.DisplayName ?? "?"})";

        switch (kind)
        {
            case SDBCatalogKind.Monster:
                shard.EntityMan.SpawnCharacter(typeId, position, orientation: orientation);
                break;

            case SDBCatalogKind.Deployable:
                shard.EntityMan.SpawnDeployable(typeId, position, orientation);
                break;

            case SDBCatalogKind.Vehicle:
                shard.EntityMan.SpawnVehicle((ushort)typeId, position, orientation, character, false);
                break;

            case SDBCatalogKind.Carryable:
                shard.EntityMan.SpawnCarryable(typeId, position);
                break;

            case SDBCatalogKind.Turret:
                if (character == null)
                {
                    return "Turrets attach to an entity - a player character is required to spawn one";
                }

                // Turrets are child entities; attach to the requesting character.
                shard.EntityMan.SpawnTurret(typeId, character);
                return $"Spawned {label} attached to your character";

            default:
                return $"Spawning {kind} is not supported";
        }

        return $"Spawned {label} at {Format(position)}";
    }

    /// <summary>Implementation of the `sdb` browse/search command.</summary>
    public static string Browse(string[] parameters)
    {
        if (parameters == null || parameters.Length == 0)
        {
            return SDBCatalog.Overview() +
                   $"\nUsage: sdb <{SDBCatalog.KindList}> [<id|name filter>] [limit]";
        }

        if (!SDBCatalog.TryParseKind(parameters[0], out var kind))
        {
            return $"Unknown kind \"{parameters[0]}\". Expected one of: {SDBCatalog.KindList}";
        }

        int limit = SDBCatalog.DefaultSearchLimit;
        int queryEnd = parameters.Length;

        if (parameters.Length >= 2 &&
            int.TryParse(parameters[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedLimit) &&
            parsedLimit > 0 &&
            parameters.Length > 2)
        {
            limit = parsedLimit;
            queryEnd = parameters.Length - 1;
        }

        string query = string.Join(' ', parameters[1..queryEnd]);
        var results = SDBCatalog.Search(kind, query, limit);

        if (results.Count == 0)
        {
            return $"No {kind.ToString().ToLowerInvariant()} rows matching \"{query}\"";
        }

        var header = query.Length == 0
            ? $"{SDBCatalog.GetTableName(kind)} (first {results.Count}):"
            : $"{SDBCatalog.GetTableName(kind)} matching \"{query}\" ({results.Count} shown):";

        return header + Environment.NewLine + string.Join(Environment.NewLine, results);
    }

    /// <summary>Implementation of the `sdbinfo` detail command.</summary>
    public static string Info(string[] parameters)
    {
        if (parameters == null || parameters.Length < 2)
        {
            return $"Usage: sdbinfo <{SDBCatalog.KindList}> <id|name>";
        }

        if (!SDBCatalog.TryParseKind(parameters[0], out var kind))
        {
            return $"Unknown kind \"{parameters[0]}\". Expected one of: {SDBCatalog.KindList}";
        }

        string query = string.Join(' ', parameters[1..]);
        return !SDBCatalog.TryResolve(kind, query, out uint typeId, out string error)
            ? error
            : SDBCatalog.Describe(kind, typeId);
    }

    private static bool TryParsePosition(string[] parameters, int startIndex, out Vector3 position)
    {
        position = Vector3.Zero;

        if (startIndex < 0 || startIndex + 2 >= parameters.Length)
        {
            return false;
        }

        if (float.TryParse(parameters[startIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
            float.TryParse(parameters[startIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
            float.TryParse(parameters[startIndex + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
        {
            position = new Vector3(x, y, z);
            return true;
        }

        return false;
    }

    private static string Format(Vector3 position)
        => string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", position.X, position.Y, position.Z);
}
