using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     What one NPC attack is worth: the <c>dbcharacter::MonsterScaling</c> row of a monster's
///     level carries that level's damage <i>rating</i> (half of its health rating on every row of
///     the table), and an attack commits a fraction of it. Reading the rating as a per-swing
///     number is what made a monster kill a same-level player in one or two hits.
/// </summary>
public class AiAttackDamageTests
{
    [Fact]
    public void ResolvesAFractionOfTheLevelsDamageRating()
    {
        // The level-45 row of dbcharacter::MonsterScaling: health 27,869, damage 13,934.
        Assert.Equal(1_393, AiAttackDamage.Resolve(13_934, fallbackDamage: 180, attackDamageFraction: 0.1f));

        // A tenth of the level-1 row is 5, which is the whole point of a curve that is steeper than
        // the player's own: low level mobs stop being executioners.
        Assert.Equal(5, AiAttackDamage.Resolve(50, fallbackDamage: 180, attackDamageFraction: 0.1f));
    }

    [Theory]
    [InlineData(13_934, 0.1f, 1_393)]
    [InlineData(5_057, 0.1f, 506)]
    [InlineData(1_950, 0.1f, 195)]
    [InlineData(373, 0.1f, 37)]
    [InlineData(50, 0.1f, 5)]
    public void TheFractionIsAppliedToTheRatingOfEveryLevel(int rating, float fraction, int expected)
    {
        Assert.Equal(expected, AiAttackDamage.Resolve(rating, fallbackDamage: 5, fraction));
    }

    [Fact]
    public void RoundsAwayFromZero()
    {
        // 125 x 0.1 = 12.5 -> 13, not 12 (the same rounding the weapon damage path uses).
        Assert.Equal(13, AiAttackDamage.Resolve(125, fallbackDamage: 5, 0.1f));
    }

    [Fact]
    public void AMonsterThatCanReachYouAlwaysHurtsAtLeastAUnit()
    {
        // A fraction small enough to round a low rating to 0 must not produce a mob that punches
        // for nothing at all.
        Assert.Equal(1, AiAttackDamage.Resolve(20, fallbackDamage: 5, 0.01f));
    }

    [Fact]
    public void AFractionOutOfRangeIsClamped()
    {
        Assert.Equal(100, AiAttackDamage.Resolve(100, fallbackDamage: 5, 1f));       // the whole rating
        Assert.Equal(1, AiAttackDamage.Resolve(100, fallbackDamage: 5, -1f));        // never negative
        Assert.Equal(1_000, AiAttackDamage.Resolve(1_000, fallbackDamage: 5, 4f));   // never more than the rating
    }

    [Fact]
    public void FallsBackToTheConfiguredPerSwingDamageUnscaled()
    {
        // 0 means "the database has no row for this monster/level" (see SdbAiMonsterStats), and the
        // rules' value is already a per-swing number, so it must not be fractioned a second time.
        Assert.Equal(76, AiAttackDamage.Resolve(0, fallbackDamage: 76, 0.1f));
        Assert.Equal(76, AiAttackDamage.Resolve(-5, fallbackDamage: 76, 0.1f));
    }

    [Fact]
    public void NoFallbackMeansNoDamage()
    {
        Assert.Equal(0, AiAttackDamage.Resolve(0, fallbackDamage: 0, 0.1f));
        Assert.Equal(0, AiAttackDamage.Resolve(0, fallbackDamage: -10, 0.1f));
    }
}
