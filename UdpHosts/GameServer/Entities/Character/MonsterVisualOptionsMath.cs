using System.Collections.Generic;
using System.Linq;
using GameServer.StaticDB.Records.dbcharacter;

namespace GameServer.Entities.Character;

/// <summary>
///     Picks the <c>dbcharacter::MonsterVisualOption</c> rows a monster wears. The header
///     (<c>MonsterVisualOptions</c>) is the variant set <c>Monster.visual_options_id</c> names;
///     <c>Female</c>/<c>Male</c> are the counts of variants that gender has (0 means this gender
///     has none). Options are grouped by <c>Type</c> and one of each type is chosen from a
///     stable seed (the entity id) so a respawn of the same entity wears the same variant.
/// </summary>
/// <remarks>
///     <c>Type</c> has no named enum in the table. The catalog documents the variants as
///     "heads, colors", so the two types this math applies are 0 (head) and 1 (skin color);
///     every other type is still selected so a later mapping can consume it without inventing
///     one here.
/// </remarks>
public static class MonsterVisualOptionsMath
{
    /// <summary><c>MonsterVisualOption.Type</c> that replaces <c>StaticInfo.HeadMain</c>.</summary>
    public const byte HeadType = 0;

    /// <summary><c>MonsterVisualOption.Type</c> that replaces <c>StaticInfo.Visuals.Colors[0]</c> (skin).</summary>
    public const byte ColorType = 1;

    /// <summary>
    ///     One option of each <c>Type</c> in <paramref name="options" />, or empty when the header
    ///     gives this gender no variants.
    /// </summary>
    public static IReadOnlyList<MonsterVisualOption> Select(
        MonsterVisualOptions header,
        IReadOnlyList<MonsterVisualOption> options,
        bool female,
        uint seed)
    {
        if (header == null || options == null || options.Count == 0)
        {
            return [];
        }

        if (female ? header.Female == 0 : header.Male == 0)
        {
            return [];
        }

        return options
            .Where(row => row != null)
            .GroupBy(row => row.Type)
            .OrderBy(group => group.Key)
            .Select(group => Pick(group.OrderBy(row => row.Value).ToList(), Mix(seed, group.Key)))
            .ToList();
    }

    /// <summary>Applies the head and color types of <paramref name="selected" /> onto <paramref name="info" />.</summary>
    public static void Apply(AeroMessages.GSS.Character.StaticInfoData info, IReadOnlyList<MonsterVisualOption> selected)
    {
        if (info == null || selected == null)
        {
            return;
        }

        foreach (var option in selected)
        {
            if (option == null)
            {
                continue;
            }

            if (option.Type == HeadType)
            {
                info.HeadMain = option.Value;
            }
            else if (option.Type == ColorType && info.Visuals.Colors is { Length: > 0 })
            {
                info.Visuals.Colors[0] = option.Value;
            }
        }
    }

    private static MonsterVisualOption Pick(IReadOnlyList<MonsterVisualOption> rows, uint seed)
    {
        return rows[(int)(seed % (uint)rows.Count)];
    }

    private static uint Mix(uint seed, byte type)
    {
        unchecked
        {
            return (seed * 16777619u) ^ type;
        }
    }
}
