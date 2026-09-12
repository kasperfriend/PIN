using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

public class NpcAttackDamageMathTests
{
    private const int LevelRating = 13_934; // the level-45 MonsterScaling damage rating
    private const float Fraction = 0.1f;
    private const int Fallback = 5;

    [Fact]
    public void ResolvePerRound_CreatureWeaponModifier_ScalesTheLevelRating()
    {
        // Attribute 1145 of the weapon: the level's rating is the level term, the modifier the weapon term.
        int damage = NpcAttackDamageMath.ResolvePerRound(0f, 0.0217f, LevelRating, 1f, Fraction, Fallback);

        Assert.Equal(302, damage);
    }

    [Fact]
    public void ResolvePerRound_ModalCreatureWeaponModifier_ReproducesTheRatingShare()
    {
        // 1145 = 0.1 is the most common value on monster weapons, and the share the engine used before
        // weapons were resolved; a row that carries it must land on the same number.
        int damage = NpcAttackDamageMath.ResolvePerRound(0f, 0.1f, LevelRating, 1f, Fraction, Fallback);

        Assert.Equal(1_393, damage);
    }

    [Fact]
    public void ResolvePerRound_ItemDamagePerRound_WinsOverTheRating()
    {
        // Attribute 954 on a level-matched PvE weapon item: the literal per-round damage.
        int damage = NpcAttackDamageMath.ResolvePerRound(400f, 0f, LevelRating, 1f, Fraction, Fallback);

        Assert.Equal(400, damage);
    }

    [Fact]
    public void ResolvePerRound_CreatureDamageModifier_MultipliesEveryBranch()
    {
        // Attribute 1144 of the monster (Necronus carries 1.5).
        Assert.Equal(454, NpcAttackDamageMath.ResolvePerRound(0f, 0.0217f, LevelRating, 1.5f, Fraction, Fallback));
        Assert.Equal(600, NpcAttackDamageMath.ResolvePerRound(400f, 0f, LevelRating, 1.5f, Fraction, Fallback));
    }

    [Fact]
    public void ResolvePerRound_WithoutAWeaponModifier_KeepsTheRatingShare()
    {
        int damage = NpcAttackDamageMath.ResolvePerRound(0f, 0f, LevelRating, 1f, Fraction, Fallback);

        Assert.Equal(1_393, damage);
    }

    [Fact]
    public void ResolvePerRound_WithoutAGroupRow_ReturnsTheFlatFallback()
    {
        // Already a per-attack number, so the fraction is not applied to it.
        int damage = NpcAttackDamageMath.ResolvePerRound(0f, 0f, 0, 1f, 1f, 76);

        Assert.Equal(76, damage);
    }

    [Fact]
    public void ResolvePerRound_NeverReturnsZero_WhenTheRowExists()
    {
        // A weapon modifier of 0.0000001 on a real rating must not round a monster's attack away.
        Assert.Equal(NpcAttackDamageMath.MinimumDamage, NpcAttackDamageMath.ResolvePerRound(0f, 0.0000001f, 10, 1f, Fraction, 0));
    }

    [Fact]
    public void ResolveAttackIntervalMs_BehaviourTimingWins()
    {
        uint interval = NpcAttackDamageMath.ResolveAttackIntervalMs(1500, 1000, 0u, 100u, 1200u);

        Assert.Equal(2500u, interval);
    }

    [Fact]
    public void ResolveAttackIntervalMs_FallsBackToTheWeaponCadence()
    {
        Assert.Equal(1300u, NpcAttackDamageMath.ResolveAttackIntervalMs(0, 0, 0u, 1300u, 1200u));
        Assert.Equal(900u, NpcAttackDamageMath.ResolveAttackIntervalMs(0, 0, 900u, 100u, 1200u));
    }

    [Fact]
    public void ResolveAttackIntervalMs_ClampsDegenerateWeaponRows()
    {
        // Charge Sniper Rifle rows carry ms_per_burst 50; the AI tick is 50 ms, so the floor keeps a
        // fire-animation value from becoming an attack rate.
        Assert.Equal(NpcAttackDamageMath.MinimumAttackIntervalMs, NpcAttackDamageMath.ResolveAttackIntervalMs(0, 0, 0u, 50u, 1200u));
    }

    [Fact]
    public void ResolveAttackIntervalMs_WithoutAnyRow_UsesTheRulesCooldown()
    {
        Assert.Equal(1200u, NpcAttackDamageMath.ResolveAttackIntervalMs(0, 0, 0u, 0u, 1200u));
        Assert.Equal(NpcAttackDamageMath.MinimumAttackIntervalMs, NpcAttackDamageMath.ResolveAttackIntervalMs(0, 0, 0u, 0u, 0u));
    }

    [Fact]
    public void ResolveCreatureModifier_AddsThePerLevelColumn()
    {
        Assert.Equal(1.5f, NpcAttackDamageMath.ResolveCreatureModifier(1.5f, 0f, 45));
        Assert.Equal(2.5f, NpcAttackDamageMath.ResolveCreatureModifier(1f, 0.5f, 3));
    }
}
