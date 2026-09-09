using System.Collections.Generic;
using GameServer.Systems.WeaponSim;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The pure half of player weapon damage: which database value a shot's per-round
///     damage comes from (<c>dbitems::AttributeRange</c> attribute 954 "Damage Per Round"
///     of the weapon item, falling back to the resolved weapon template
///     <c>damage_per_round</c>), how that value grows with the wielder's progression level
///     (the curve the database itself uses for the per-item-level variants of a weapon family)
///     and how the fired ammo row's falloff
///     (<c>damage_decay</c> / <c>damage_decay_rangefrac</c> / <c>min_damage_frac</c>)
///     reduces it with the distance travelled.
/// </summary>
public class WeaponDamageMathTests
{
    private const int Fallback = 1337;

    // --- Per-round damage resolution --------------------------------------

    [Fact]
    public void RoundDamage_UsesTheWeaponItemsDamagePerRoundAttribute_WhenPresent()
    {
        // e.g. the Accord Assault plasma cannon (86742): attribute 954 = 100.
        var attributes = new Dictionary<ushort, float>
        {
            { 954, 100f },
            { 957, 90f },
            { 958, 2f },
        };

        Assert.Equal(100, WeaponDamageMath.ResolveRoundDamage(attributes, templateDamagePerRound: 39, Fallback));
    }

    [Fact]
    public void RoundDamage_FallsBackToTheResolvedTemplateDamagePerRound_WhenTheItemHasNoDamageAttribute()
    {
        // e.g. an NPC weapon row: no item attribute range at all, the template value applies.
        var attributes = new Dictionary<ushort, float>
        {
            { 957, 150f },
            { 958, 4f },
        };

        Assert.Equal(46, WeaponDamageMath.ResolveRoundDamage(attributes, templateDamagePerRound: 46, Fallback));
    }

    [Fact]
    public void RoundDamage_PrefersTheTemplateOverTheLegacyPlaceholder()
    {
        Assert.Equal(1, WeaponDamageMath.ResolveRoundDamage(null, templateDamagePerRound: 1, Fallback));
    }

    [Fact]
    public void RoundDamage_UsesThePlaceholder_OnlyWhenTheDatabaseHasNothingUsable()
    {
        Assert.Equal(Fallback, WeaponDamageMath.ResolveRoundDamage(null, templateDamagePerRound: 0, Fallback));
        Assert.Equal(Fallback, WeaponDamageMath.ResolveRoundDamage(new Dictionary<ushort, float> { { 954, 0f } }, 0, Fallback));
        Assert.Equal(Fallback, WeaponDamageMath.ResolveRoundDamage(new Dictionary<ushort, float> { { 954, -5f } }, -1, Fallback));
    }

    [Fact]
    public void RoundDamage_IgnoresANegativeTemplateValue()
    {
        Assert.Equal(Fallback, WeaponDamageMath.ResolveRoundDamage(null, templateDamagePerRound: -10, Fallback));
    }

    // --- Growing with the wielder's level ---------------------------------

    [Theory]
    [InlineData(0, 1f)]     // an entity with no progression level at all (NPCs) fights at its rows' values
    [InlineData(1, 1f)]     // a fresh frame: exactly the value the item row carries
    [InlineData(2, 1.1f)]
    [InlineData(3, 1.205f)]
    [InlineData(4, 1.315f)]
    [InlineData(10, 2.103f)]
    [InlineData(20, 4.054f)]
    [InlineData(45, 16.114f)]
    public void LevelScale_MatchesTheDatabaseItemLevelChain(int level, float expected)
    {
        // The live game ships one item per level of a weapon family (autogen group 10020 for the
        // Accord Assault rifle: 11, 12.1, 13.255, 14.468 ...). The growth is not a flat 10% compound:
        // the per-level *step* grows by 5% each level, whose sum is 2 x 1.05^(level-1) - 1.
        Assert.Equal(expected, WeaponDamageMath.DamageLevelScale(level), 3);
    }

    [Fact]
    public void LevelScale_ReproducesTheAuthoredValuesOfEveryLevelOfAWeaponFamily()
    {
        // The plasma cannon family (group 10003) is authored as 100 at item level 1, 210.27 at 10,
        // 405.39 at 20; the R36's family reads 39 at 1. Same curve for all of them.
        Assert.Equal(100, WeaponDamageMath.RoundDamage(100f * WeaponDamageMath.DamageLevelScale(1)));
        Assert.Equal(210, WeaponDamageMath.RoundDamage(100f * WeaponDamageMath.DamageLevelScale(10)));
        Assert.Equal(405, WeaponDamageMath.RoundDamage(100f * WeaponDamageMath.DamageLevelScale(20)));
        Assert.Equal(39, WeaponDamageMath.RoundDamage(39f * WeaponDamageMath.DamageLevelScale(1)));
    }

    [Fact]
    public void LevelScale_KeepsGrowingToTheLastFrameLevel()
    {
        // dbitems::FrameProgressionLevel ends at 50; the same curve is what the item chains use
        // beyond it (they run to item level 65 for high quality gear), so nothing flattens here.
        Assert.Equal(20.843f, WeaponDamageMath.DamageLevelScale(50), 3);
        Assert.True(WeaponDamageMath.DamageLevelScale(51) > WeaponDamageMath.DamageLevelScale(50));
    }

    [Fact]
    public void RoundDamage_ScalesTheWeaponItemsDamageWithTheWieldersLevel()
    {
        // The reported bug: 60 damage per shot at level 1 and at level 45 alike.
        var attributes = new Dictionary<ushort, float> { { 954, 60f } };

        Assert.Equal(60, WeaponDamageMath.ResolveRoundDamage(attributes, templateDamagePerRound: 0, Fallback, progressionLevel: 1));
        Assert.Equal(967, WeaponDamageMath.ResolveRoundDamage(attributes, templateDamagePerRound: 0, Fallback, progressionLevel: 45));
    }

    [Fact]
    public void RoundDamage_ScalesTheTemplateFallbackTheSameWay()
    {
        // A weapon whose item row has no 954 at all still fights at its template value, and that
        // value grows with the wielder's level too (an NPC stays at 1, so NPC shots are unchanged).
        Assert.Equal(46, WeaponDamageMath.ResolveRoundDamage(null, templateDamagePerRound: 46, Fallback));
        Assert.Equal(97, WeaponDamageMath.ResolveRoundDamage(null, templateDamagePerRound: 46, Fallback, progressionLevel: 10));
        Assert.Equal(741, WeaponDamageMath.ResolveRoundDamage(null, templateDamagePerRound: 46, Fallback, progressionLevel: 45));
    }

    [Fact]
    public void RoundDamage_LeavesTheLegacyPlaceholderUnscaled()
    {
        // 1337 is not a database number, so it must not silently become a level-45 number.
        Assert.Equal(Fallback, WeaponDamageMath.ResolveRoundDamage(null, templateDamagePerRound: 0, Fallback, progressionLevel: 45));
    }

    [Fact]
    public void RoundDamage_AtLevel1_IsTheItemRowValueAlone()
    {
        var attributes = new Dictionary<ushort, float> { { 954, 11f } };

        Assert.Equal(11, WeaponDamageMath.ResolveRoundDamage(attributes, templateDamagePerRound: 0, Fallback, progressionLevel: 1));
    }

    // --- Ammo falloff -----------------------------------------------------

    [Theory]
    [InlineData(0f)] // muzzle hit
    [InlineData(10f)]
    [InlineData(99f)]
    [InlineData(150f)] // beyond range
    public void Falloff_KeepsFullDamage_WhenTheAmmoHasNoDecay(float distance)
    {
        // damage_decay = 0: plasma balls, tesla, rockets - flat damage over the whole flight.
        Assert.Equal(100, WeaponDamageMath.ApplyDamageFalloff(100, distance, weaponRange: 100f, damageDecay: 0, decayRangeFrac: 0.7f, minDamageFrac: 0.33f));
    }

    [Fact]
    public void Falloff_KeepsFullDamage_InsideTheDecayStartWindow()
    {
        // PvE assault rifle ammo (58): decay starts at 70% of the 150 m range, i.e. 105 m.
        Assert.Equal(46, WeaponDamageMath.ApplyDamageFalloff(46, distanceTravelled: 105f, weaponRange: 150f, damageDecay: 1, decayRangeFrac: 0.7f, minDamageFrac: 0.33f));
        Assert.Equal(46, WeaponDamageMath.ApplyDamageFalloff(46, distanceTravelled: 0f, weaponRange: 150f, damageDecay: 1, decayRangeFrac: 0.7f, minDamageFrac: 0.33f));
    }

    [Fact]
    public void Falloff_TapersLinearly_BetweenTheDecayStartAndMaxRange()
    {
        // Range 100 m, decay from 50 m to min 50% at 100 m:
        //   at 75 m (halfway through the band) damage is 75% of the base.
        Assert.Equal(75, WeaponDamageMath.ApplyDamageFalloff(100, distanceTravelled: 75f, weaponRange: 100f, damageDecay: 1, decayRangeFrac: 0.5f, minDamageFrac: 0.5f));
        //   at 87.5 m (three quarters through the band): 62.5% -> rounds to 63.
        Assert.Equal(63, WeaponDamageMath.ApplyDamageFalloff(100, distanceTravelled: 87.5f, weaponRange: 100f, damageDecay: 1, decayRangeFrac: 0.5f, minDamageFrac: 0.5f));
    }

    [Fact]
    public void Falloff_ReachesTheAmmoMinDamageFraction_AtMaxRange()
    {
        // PvE assault rifle: 33% of the per-round damage at max range: 46 * 0.33 = 15.18 -> 15.
        Assert.Equal(15, WeaponDamageMath.ApplyDamageFalloff(46, distanceTravelled: 150f, weaponRange: 150f, damageDecay: 1, decayRangeFrac: 0.7f, minDamageFrac: 0.33f));
        Assert.Equal(15, WeaponDamageMath.ApplyDamageFalloff(46, distanceTravelled: 500f, weaponRange: 150f, damageDecay: 1, decayRangeFrac: 0.7f, minDamageFrac: 0.33f));
    }

    [Fact]
    public void Falloff_RoundsHalfAwayFromZero()
    {
        // 33 * 0.5 taper at halfway of the band... use a clean midpoint: base 33 -> 16.5 -> 17.
        Assert.Equal(17, WeaponDamageMath.ApplyDamageFalloff(33, distanceTravelled: 75f, weaponRange: 100f, damageDecay: 1, decayRangeFrac: 0.5f, minDamageFrac: 0f));
    }

    [Fact]
    public void Falloff_IsDisabled_WhenTheDecayWindowOrFloorMakesNoSense()
    {
        // decay starts at max range: no band to taper over.
        Assert.Equal(100, WeaponDamageMath.ApplyDamageFalloff(100, distanceTravelled: 99f, weaponRange: 100f, damageDecay: 1, decayRangeFrac: 1f, minDamageFrac: 0.33f));
        // floor of 1 (100%): nothing to lose.
        Assert.Equal(100, WeaponDamageMath.ApplyDamageFalloff(100, distanceTravelled: 150f, weaponRange: 100f, damageDecay: 1, decayRangeFrac: 0.5f, minDamageFrac: 1f));
        // no usable weapon range.
        Assert.Equal(100, WeaponDamageMath.ApplyDamageFalloff(100, distanceTravelled: 10f, weaponRange: 0f, damageDecay: 1, decayRangeFrac: 0.5f, minDamageFrac: 0.33f));
        // never multiply a non-positive base (callers pick their fallback before this runs).
        Assert.Equal(0, WeaponDamageMath.ApplyDamageFalloff(0, distanceTravelled: 150f, weaponRange: 100f, damageDecay: 1, decayRangeFrac: 0.5f, minDamageFrac: 0.33f));
    }

    [Fact]
    public void RoundDamage_WrapsTheDecimalDamageOfAnItem()
    {
        // e.g. a shotgun-style pellet at 9.6 per round.
        Assert.Equal(10, WeaponDamageMath.RoundDamage(9.6f));
        Assert.Equal(9, WeaponDamageMath.RoundDamage(9.2f));
    }
}
