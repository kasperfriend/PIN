using System;

namespace GameServer.Systems.Ai;

/// <summary>
///     Authored invocation values used to constrain PIN's optional tactical fallback, not recovered
///     CAIS scheduling semantics. Required pursuit, ability movement and leash return ignore these.
/// </summary>
public sealed record NpcCombatMovementProfile
{
    public static readonly NpcCombatMovementProfile Default = new();

    public float MoveChance { get; init; } = 1f;
    public float MaxMove { get; init; } = 12f;
    public float SpeedMultiplier { get; init; } = 1f;
    public bool GroundTactics { get; init; } = true;

    public static NpcCombatMovementProfile Resolve(NpcBehaviorParams behavior, NpcBehaviorParams offensive)
    {
        behavior ??= NpcBehaviorParams.Empty;
        offensive ??= NpcBehaviorParams.Empty;
        // Select the authored value before validating it. An unsupported offensive value must
        // not silently resurrect a different base value. In particular moveChance=10.0 is NOT 1.
        float? chance = Number(behavior, offensive, "moveChance");
        float? maxMove = Number(behavior, offensive, "maxMove");
        float? speed = Number(behavior, offensive, "speedMultiplier");
        return new NpcCombatMovementProfile
        {
            MoveChance = chance is >= 0f and <= 1f ? chance.Value : 1f,
            MaxMove = maxMove is >= 0f ? MathF.Min(12f, maxMove.Value) : 12f,
            SpeedMultiplier = speed is > 0f and <= 4f ? speed.Value : 1f,
            GroundTactics = Flag(behavior, offensive, "grounded") != false &&
                Flag(behavior, offensive, "climber") != true,
        };
    }

    private static NpcBehaviorParams Source(NpcBehaviorParams behavior, NpcBehaviorParams offensive, string key)
        => offensive.Values.ContainsKey(key) ? offensive : behavior;

    private static float? Number(NpcBehaviorParams behavior, NpcBehaviorParams offensive, string key)
        => Source(behavior, offensive, key).TryGetFloat(key, out float value) && float.IsFinite(value) ? value : null;

    private static bool? Flag(NpcBehaviorParams behavior, NpcBehaviorParams offensive, string key)
        => Source(behavior, offensive, key).TryGetBool(key, out bool value) ? value : null;
}
