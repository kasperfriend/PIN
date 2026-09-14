using System;
using System.Globalization;
using System.Numerics;
using GameServer.StaticDB;

namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     The <c>population</c> command's behaviour, shared by its chat and admin spellings
///     (<see cref="Chat.Commands.PopulationChatCommand"/> and
///     <see cref="Admin.Commands.PopulationServerCommand"/>) so the two cannot drift apart - which is
///     what the duplicated <c>ai</c> command pair has to be checked for by hand.
/// </summary>
public static class PopulationCommand
{
    /// <summary>Radius <c>near</c> looks at when it is not given one.</summary>
    private const float DefaultNearbyRadius = 100f;

    /// <summary>How many NPCs <c>near</c> lists. One line each, so this is a chat window's worth.</summary>
    private const int NearbyListingLimit = 15;

    /// <summary>
    ///     Runs one <c>population</c> invocation.
    /// </summary>
    /// <param name="shard">The shard the command was issued on.</param>
    /// <param name="parameters">The command's parameters; the first is the action.</param>
    /// <param name="origin">
    ///     Where the player issuing the command is standing, or null when they have no character in
    ///     the world. Only <c>near</c> needs it.
    /// </param>
    /// <param name="feedback">How the command answers; one call per line.</param>
    public static void Run(IShard shard, string[] parameters, Vector3? origin, Action<string> feedback)
    {
        var service = shard.WorldPopulation;
        if (service == null)
        {
            feedback("World population is not available on this shard");
            return;
        }

        var action = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "status";

        switch (action)
        {
            case "on":
            case "enable":
            case "1":
                service.Enabled = true;
                feedback("World population on: the zone fills in around players over the next few seconds");
                break;

            case "off":
            case "disable":
            case "0":
                service.Enabled = false;
                feedback("World population off: every NPC it spawned is removed at the next update; the zone's own entities stay");
                break;

            case "near":
                DescribeNearby(service, parameters, origin, feedback);
                break;

            case "status":
                foreach (var line in service.DescribeStatus().Split('\n'))
                {
                    feedback(line.TrimEnd('\r'));
                }

                break;

            default:
                feedback("Unknown population action. Try: on, off, status, near [radius]");
                break;
        }
    }

    private static void DescribeNearby(
        WorldPopulationService service,
        string[] parameters,
        Vector3? origin,
        Action<string> feedback)
    {
        if (origin == null)
        {
            feedback("You have no position in the world to look around");
            return;
        }

        float radius = DefaultNearbyRadius;
        if (parameters.Length > 1 &&
            float.TryParse(parameters[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) &&
            parsed > 0f)
        {
            radius = parsed;
        }

        var nearby = service.ListLiveNear(origin.Value, radius, NearbyListingLimit);
        if (nearby.Count == 0)
        {
            feedback($"No world population NPCs within {radius:0} m ({service.LiveCount} alive in the zone)");
            return;
        }

        feedback($"{nearby.Count} world population NPC(s) within {radius:0} m, nearest first:");

        foreach (var (monsterId, position, distance) in nearby)
        {
            feedback(string.Format(
                CultureInfo.InvariantCulture,
                "  {0} (id {1}) at {2:0.0}, {3:0.0}, {4:0.0} - {5:0} m",
                MonsterName(monsterId),
                monsterId,
                position.X,
                position.Y,
                position.Z,
                distance));
        }
    }

    private static string MonsterName(uint monsterId)
    {
        var monster = SDBInterface.GetMonster(monsterId);
        if (monster == null)
        {
            return "unknown monster";
        }

        // The row's own name, then its title - both are localized string ids - then give up and call
        // it a monster, which still leaves the id in the line to look the row up by.
        var name = SDBInterface.GetLocalizedString(monster.LocalizedNameId);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = SDBInterface.GetLocalizedString(monster.Title);
        }

        return string.IsNullOrWhiteSpace(name) ? "monster" : name;
    }
}
