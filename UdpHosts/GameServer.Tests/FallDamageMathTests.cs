using GameServer.Systems.Combat;
using Xunit;

namespace GameServer.Tests;

public class FallDamageMathTests
{
    private static StandardFallDamageRules CreateRules()
    {
        return new StandardFallDamageRules
        {
            Enabled = true,
            SafeImpactSpeed = 12f,
            LethalImpactSpeed = 48f,
            DamagePerSpeed = 130,
            MinAirTimeMs = 250f,
        };
    }

    private static FallImpactContext Context(
        float impactSpeed = 0f,
        float airTimeMs = 1000f,
        bool inWater = false,
        bool usedThrusterOrGlider = false,
        bool knockdownFall = false,
        bool immuneToFallDamage = false)
    {
        return new FallImpactContext(impactSpeed, airTimeMs, inWater, usedThrusterOrGlider, knockdownFall, immuneToFallDamage);
    }

    [Fact]
    public void Evaluate_BelowSafeSpeed_DealsNoDamage()
    {
        var result = FallDamageMath.Evaluate(Context(impactSpeed: 11.9f), CreateRules());

        Assert.Equal(0, result.Damage);
        Assert.False(result.Lethal);
    }

    [Fact]
    public void Evaluate_AtSafeSpeed_DealsNoDamage()
    {
        var result = FallDamageMath.Evaluate(Context(impactSpeed: 12f), CreateRules());

        Assert.Equal(0, result.Damage);
    }

    [Fact]
    public void Evaluate_AboveSafeSpeed_ScalesLinearly()
    {
        // (30 - 12) * 130 = 2340
        var result = FallDamageMath.Evaluate(Context(impactSpeed: 30f), CreateRules());

        Assert.Equal(2340, result.Damage);
        Assert.Equal(18f, result.ExcessSpeed);
        Assert.False(result.Lethal);
    }

    [Fact]
    public void Evaluate_AtLethalSpeed_FlagsLethal()
    {
        var result = FallDamageMath.Evaluate(Context(impactSpeed: 48f), CreateRules());

        Assert.True(result.Lethal);
        Assert.True(result.Damage > 0);
    }

    [Fact]
    public void Evaluate_BeyondLethalSpeed_FlagsLethal()
    {
        var result = FallDamageMath.Evaluate(Context(impactSpeed: 60f), CreateRules());

        Assert.True(result.Lethal);
    }

    [Fact]
    public void Evaluate_ShortFalls_DealNoDamage()
    {
        var result = FallDamageMath.Evaluate(Context(impactSpeed: 40f, airTimeMs: 100f), CreateRules());

        Assert.Equal(0, result.Damage);
    }

    [Fact]
    public void Evaluate_WaterLanding_DealsNoDamage()
    {
        var result = FallDamageMath.Evaluate(Context(impactSpeed: 40f, inWater: true), CreateRules());

        Assert.Equal(0, result.Damage);
    }

    [Fact]
    public void Evaluate_ThrusterOrGliderLanding_DealsNoDamage()
    {
        var result = FallDamageMath.Evaluate(Context(impactSpeed: 40f, usedThrusterOrGlider: true), CreateRules());

        Assert.Equal(0, result.Damage);
    }

    [Fact]
    public void Evaluate_KnockdownFall_DealsNoDamage()
    {
        var result = FallDamageMath.Evaluate(Context(impactSpeed: 40f, knockdownFall: true), CreateRules());

        Assert.Equal(0, result.Damage);
    }

    [Fact]
    public void Evaluate_ImmuneToFallDamage_DealsNoDamage()
    {
        var result = FallDamageMath.Evaluate(Context(impactSpeed: 40f, immuneToFallDamage: true), CreateRules());

        Assert.Equal(0, result.Damage);
    }

    [Fact]
    public void Evaluate_AppliesDamageTakenMultiplier()
    {
        // (20 - 12) * 130 * 2.0 = 2080
        var result = FallDamageMath.Evaluate(Context(impactSpeed: 20f), CreateRules(), damageTakenMultiplier: 2f);

        Assert.Equal(2080, result.Damage);
    }

    [Fact]
    public void Evaluate_DisabledRules_DealNoDamage()
    {
        var result = FallDamageMath.Evaluate(Context(impactSpeed: 40f), new StandardFallDamageRules { Enabled = false });

        Assert.Equal(0, result.Damage);
    }
}
