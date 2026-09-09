using GameServer.Data;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Tests for the DB-shaped max-health pool rule (<see cref="CharacterHealthMath"/>):
///     the pool is the sum of the equipped items' Health attribute plus the per-frame-level
///     health curve (<c>dbitems::LevelItemAttributes</c> attribute 6) converted to pool
///     points by <see cref="CharacterHealthMath.LevelCurveToPoolScale"/>.
/// </summary>
public class CharacterHealthMathTests
{
    [Fact]
    public void ComputeMaxHealth_NoHealthNoCurve_IsZero()
    {
        Assert.Equal(0, CharacterHealthMath.ComputeMaxHealth(0f, 0f));
    }

    [Fact]
    public void ComputeMaxHealth_NegativeInputs_ClampsAtZero()
    {
        Assert.Equal(0, CharacterHealthMath.ComputeMaxHealth(-10f, 0f));
        Assert.Equal(0, CharacterHealthMath.ComputeMaxHealth(0f, -5f));
    }

    [Fact]
    public void ComputeMaxHealth_ItemSumOnly_LevelOneCurveIsZero()
    {
        // Fresh battleframe at progression level 1: the DB curve is 0 below level 4, so the
        // pool is exactly the item sum. The default Accord Assault loadout's Health
        // (frame 100 + gear) sums to ~360.83.
        Assert.Equal(360, CharacterHealthMath.ComputeMaxHealth(360.83f, 0f));
    }

    [Fact]
    public void ComputeMaxHealth_LevelCurve_IsScaledIntoPoolPoints()
    {
        // The curve is in attribute units; LevelCurveToPoolScale converts them to pool
        // points (see the constant's documentation for the anchor).
        Assert.Equal(300, CharacterHealthMath.ComputeMaxHealth(0f, 100f));
        Assert.Equal(19080, CharacterHealthMath.ComputeMaxHealth(0f, 6360f));
    }

    [Fact]
    public void ComputeMaxHealth_ItemSumAndCurve_Add()
    {
        // Level 45 shape: item sum ~1037.83 + 3 x curve(45) 6360 -> 20117 (floor).
        Assert.Equal(20117, CharacterHealthMath.ComputeMaxHealth(1037.83f, 6360f));
    }

    [Fact]
    public void ComputeMaxHealth_FloorsFractionalPools()
    {
        Assert.Equal(100, CharacterHealthMath.ComputeMaxHealth(100.9f, 0f));
        Assert.Equal(13, CharacterHealthMath.ComputeMaxHealth(4.9f, 2.9f));
    }

    [Fact]
    public void HealthAttributeId_IsAttributeSix()
    {
        // dbitems::AttributeDefinition 6 = "Health", carried by every chassis item (100)
        // and by gear; the loadout aggregate keys ItemAttributes by this id.
        Assert.Equal(6, CharacterHealthMath.HealthAttributeId);
    }
}
