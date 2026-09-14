using System;
using System.Collections.Generic;

namespace GameServer.Systems.Ai;

/// <summary>What the base CAIS invocation actually tells us about ambient locomotion.</summary>
public enum NpcRoutineKind
{
    Unspecified,
    Stationary,
    Wander,
    Work,
    ExternalRoute,
}

/// <summary>
///     Compatibility defaults, NOT recovered CAIS tree defaults or original patrol coordinates.
///     Explicit parameters in the monster/deployable rows take precedence. Keeping these separate
///     makes the boundary between shipped data and PIN policy visible and testable.
/// </summary>
public sealed record NpcRoutineRules
{
    public float WanderDistance { get; init; } = 10f;
    public float HomeRadius { get; init; } = 30f;
    public int RestMinMs { get; init; } = 3000;
    public int RestMaxMs { get; init; } = 7000;
    public int WorkDurationMs { get; init; } = 15000;
    public int RetryMs { get; init; } = 2000;
    public int StuckTimeoutMs { get; init; } = 8000;
}

/// <summary>
///     Ambient movement resolved from a monster's base behaviour, not from its faction, display name
///     or a mission waypoint. Unknown trees and route requests without route data do not get made-up
///     patrols. See Docs/NPC_ROUTINES.md and the complete prod-1962 movement census.
/// </summary>
public sealed record NpcRoutineProfile
{
    // Explicit names, not a substring match. EliteStationary is handled separately for its
    // explicit wanderDistance; BasicCivilian_Stationary never acquires a city's walking routine.
    private static readonly HashSet<string> Wanderers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Wander", "FastWander", "FastWanderCore", "WanderWithEmoteVocalized",
        "AggressiveWanderer", "EliteWanderer", "SwarmWanderer", "SuicideWanderer",
        "MeleeGruntWanderer", "GruntWanderer", "ShotGruntWanderer", "PassiveWanderer",
        "HeavyWanderer", "MedicWanderer", "PeacetimeCityWanderer", "PeacetimeCityWandererCore",
        "PeacetimeCityWandererWithHealing", "GuardCityWanderer", "BasicCivilian",
    };

    private static readonly HashSet<string> FixedBodies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Null", "Stand", "StandStill", "StayAtSpawn", "OneOff_StandStill",
        "BasicCivilian_Stationary", "StationaryCivilianDialog", "PerformEmoteNoPhysics",
    };

    private static readonly HashSet<string> ExternalRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "StockShootAndFollowRoute", "OneOff_FollowRoute", "NavigateToLocation", "Arch_Follower",
        "TestFollowPlayer", "ProtectVehicle",
    };

    public string BehaviorName { get; init; } = string.Empty;
    public NpcRoutineKind Kind { get; init; }
    public bool FixedInPlace { get; init; }
    public bool Walk { get; init; } = true;
    public bool? CombatWalk { get; init; }
    public bool? LeashWalk { get; init; }
    public float WanderDistance { get; init; }
    public float HomeRadius { get; init; }
    public bool NearSpawn { get; init; }
    public int RestMinMs { get; init; }
    public int RestMaxMs { get; init; }
    public int RetryMs { get; init; }
    public int StuckTimeoutMs { get; init; }
    public float WanderChance { get; init; } = 1f;
    public string WorkFunction { get; init; } = string.Empty;
    public float? LeashDistance { get; init; }
    public string MissingData { get; init; } = string.Empty;

    public bool HasRoutine => Kind is NpcRoutineKind.Wander or NpcRoutineKind.Work;

    public static NpcRoutineProfile Resolve(
        NpcBehaviorParams behavior,
        NpcBehaviorParams offensive = null,
        NpcRoutineRules rules = null,
        float leashRadius = 120f)
    {
        behavior ??= NpcBehaviorParams.Empty;
        offensive ??= NpcBehaviorParams.Empty;
        rules ??= new NpcRoutineRules();
        string name = behavior.Name;
        bool fixedBody = FixedBodies.Contains(name) || Bool(behavior, "stationary") == true;
        float? leash = NonNegative(behavior, "leashDistance") ?? NonNegative(behavior, "leashDist");
        float safetyRadius = Positive(leashRadius, 120f);
        if (leash is > 0f)
        {
            safetyRadius = MathF.Min(safetyRadius, leash.Value);
        }

        float distance = NonNegative(behavior, "wanderDistance")
            ?? NonNegative(behavior, "distance")
            ?? Positive(rules.WanderDistance, 10f);
        bool nearSpawn = Bool(behavior, "nearSpawn") == true || Bool(behavior, "calmNearSpawn") == true;
        float homeRadius = MathF.Min(safetyRadius,
            NonNegative(behavior, "maxDistFromSpawn") ?? (nearSpawn ? distance : MathF.Max(distance, Positive(rules.HomeRadius, 30f))));
        distance = MathF.Min(distance, homeRadius);
        float chance = Math.Clamp(NonNegative(behavior, "calmWanderChance") ?? 1f, 0f, 1f);

        int restMin = Milliseconds(behavior, "restDurationMin") ?? Math.Max(0, rules.RestMinMs);
        int restMax = Milliseconds(behavior, "restDurationMax") ?? Math.Max(restMin, rules.RestMaxMs);
        // SwarmWanderer names its idle wait in the idleEmote time pair, not in restDuration.
        restMin = Milliseconds(behavior, "idleEmoteMinTime") ?? restMin;
        restMax = Milliseconds(behavior, "idleEmoteMaxTime") ?? restMax;
        restMax = Math.Max(restMin, restMax);

        string function = Text(behavior, "restFunction");
        if (name.Equals("UseWorkDeployables", StringComparison.OrdinalIgnoreCase))
        {
            function = Text(behavior, "function");
        }

        var kind = fixedBody ? NpcRoutineKind.Stationary
            : Wanderers.Contains(name) || (name.Equals("EliteStationary", StringComparison.OrdinalIgnoreCase) &&
                NonNegative(behavior, "wanderDistance").HasValue) ? NpcRoutineKind.Wander
            : name.Equals("UseWorkDeployables", StringComparison.OrdinalIgnoreCase) ? NpcRoutineKind.Work
            : NpcRoutineKind.Unspecified;
        string missing = string.Empty;
        if (!fixedBody && (ExternalRoutes.Contains(name) || Text(behavior, "city_prefix").Length > 0))
        {
            kind = NpcRoutineKind.ExternalRoute;
            missing = "authored route / named world points or follow target";
        }

        // These requests require world/locomotion data PIN does not have. In particular the monkey's
        // UseWorkDeployables asks for a spawn volume, climbing and a 1.6 m offset. Walking that monkey
        // on the human navigation plane would not implement its request.
        if (!fixedBody && (Bool(behavior, "climber") == true || Bool(behavior, "grounded") == false ||
            Bool(behavior, "inSpawnVolume") == true || NonNegative(behavior, "groundOffset") is > 0f))
        {
            kind = NpcRoutineKind.ExternalRoute;
            missing = "spawn-volume / climbing or non-ground locomotion data";
        }

        return new NpcRoutineProfile
        {
            BehaviorName = name,
            Kind = kind,
            FixedInPlace = fixedBody,
            Walk = Bool(behavior, "walkInRoute") ?? Bool(behavior, "walk") ?? Bool(behavior, "calmWalk")
                ?? !name.StartsWith("FastWander", StringComparison.OrdinalIgnoreCase),
            CombatWalk = Bool(offensive, "combatWalk") ?? Bool(behavior, "combatWalk"),
            LeashWalk = Bool(behavior, "leashWalk"),
            WanderDistance = distance,
            HomeRadius = homeRadius,
            NearSpawn = nearSpawn,
            RestMinMs = restMin,
            RestMaxMs = restMax,
            RetryMs = Math.Max(250, rules.RetryMs),
            StuckTimeoutMs = Math.Max(1000, rules.StuckTimeoutMs),
            WanderChance = chance,
            WorkFunction = function,
            LeashDistance = leash is > 0f ? leash : null,
            MissingData = missing,
        };
    }

    private static string Text(NpcBehaviorParams behavior, string key)
        => behavior.Values.TryGetValue(key, out string value) ? value.Trim() : string.Empty;

    private static bool? Bool(NpcBehaviorParams behavior, string key)
        => behavior.TryGetBool(key, out bool value) ? value : null;

    private static float? NonNegative(NpcBehaviorParams behavior, string key)
        => behavior.TryGetFloat(key, out float value) && float.IsFinite(value) && value >= 0f ? value : null;

    private static int? Milliseconds(NpcBehaviorParams behavior, string key)
        => behavior.TryGetInt(key, out int value) && value >= 0 ? value : null;

    private static float Positive(float value, float fallback)
        => float.IsFinite(value) && value > 0f ? value : fallback;
}
