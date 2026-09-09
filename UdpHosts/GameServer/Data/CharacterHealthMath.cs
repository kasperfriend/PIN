using System;
using GameServer.Enums;

namespace GameServer.Data;

/// <summary>
///     Pure helpers that turn the static database's health rows into a character's health
///     pool. Kept free of shard/entity state so the resolution rules are unit testable.
/// </summary>
public static class CharacterHealthMath
{
    /// <summary>
    ///     The item attribute that accumulates a loadout's health contributions
    ///     (<c>dbitems::AttributeDefinition</c> 6, display name "Health"). Every battleframe
    ///     chassis item carries 100; gear items add theirs on top, so the loadout sum is the
    ///     character's item-driven health (the "Core Health" of the original game's formula).
    /// </summary>
    public const ushort HealthAttributeId = (ushort)ItemAttributeId.Health;

    /// <summary>
    ///     Scale that converts the per-frame-level health curve
    ///     (<c>dbitems::LevelItemAttributes</c>, attribute 6) into health-pool points.
    /// </summary>
    /// <remarks>
    ///     Build <c>prod-1962</c> ships a per-level curve only for attribute 6 (levels 1-3 =
    ///     0, then 20, 100, 280, 610, 1060, 1930, 2710, 4120, 6360, 9780). Nothing in the
    ///     database defines how many pool points a curve unit is worth, so the value is an
    ///     approximation anchored on the one capture this codebase has: the old flat pool of
    ///     19,192 belonged to a level-45 character whose item health summed to ~1,038; the
    ///     curve at 45 is 6,360, i.e. the pool was ≈ 3 × the curve (+ items). 3.0 reproduces
    ///     that capture within ~5% (20,117 vs 19,192) while keeping lower levels dominated by
    ///     the items themselves (the curve is 0 below level 4, so a fresh frame's pool is
    ///     exactly its item sum, per the "frames start at level 1" rule).
    /// </remarks>
    public const float LevelCurveToPoolScale = 3.0f;

    /// <summary>
    ///     Computes a character's max-health pool from the two database-shaped inputs:
    ///     the sum of the Health attribute over the equipped items and the frame level's
    ///     health-curve value. Returns 0 for a character with neither.
    /// </summary>
    public static int ComputeMaxHealth(float itemHealthSum, float levelCurveHealth)
    {
        float pool = itemHealthSum + (levelCurveHealth * LevelCurveToPoolScale);
        return pool > 0f ? (int)MathF.Floor(pool) : 0;
    }
}
