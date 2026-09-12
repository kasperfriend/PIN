using GameServer.Systems.Combat;
using Xunit;

namespace GameServer.Tests;

public class DamageMitigationMathTests
{
    [Fact]
    public void MissingResponseDataLeavesDamageUnchanged()
    {
        Assert.Equal(125, DamageMitigationMath.Apply(125, damageTakenMultiplier: 1f));
    }

    [Fact]
    public void TypeSpecificResponseReplacesTheDefaultResponse()
    {
        // The per-damage-type row is an override in Firefall's response table,
        // not another copy of the default multiplier.
        Assert.Equal(
            25,
            DamageMitigationMath.Apply(
                100,
                damageTakenMultiplier: 1f,
                defaultResponseMultiplier: 0.5f,
                damageTypeMultiplier: 0.25f));
    }

    [Fact]
    public void DamageTakenAndResponseMultipliersStack()
    {
        // A 50 percent DamageTaken modifier and a 50 percent response leave a
        // quarter of the incoming hit.
        Assert.Equal(
            25,
            DamageMitigationMath.Apply(
                100,
                damageTakenMultiplier: 0.5f,
                defaultResponseMultiplier: 0.5f));
    }

    [Fact]
    public void ZeroMultiplierMakesTheTargetImmune()
    {
        Assert.Equal(0, DamageMitigationMath.Apply(100, damageTakenMultiplier: 0f));
        Assert.Equal(0, DamageMitigationMath.Apply(100, damageTakenMultiplier: 1f, damageTypeMultiplier: 0f));
    }

    [Fact]
    public void PositiveDamageDoesNotDisappearFromSubUnitMultipliers()
    {
        Assert.Equal(1, DamageMitigationMath.Apply(1, damageTakenMultiplier: 0.01f));
    }

    [Fact]
    public void DamageRoundsAwayFromZero()
    {
        Assert.Equal(13, DamageMitigationMath.Apply(25, damageTakenMultiplier: 0.5f));
    }

    [Fact]
    public void InvalidNegativeMultipliersAreTreatedAsImmunity()
    {
        Assert.Equal(0, DamageMitigationMath.Apply(100, damageTakenMultiplier: -1f));
        Assert.Equal(0, DamageMitigationMath.Apply(100, damageTakenMultiplier: 1f, defaultResponseMultiplier: -1f));
    }
}
