using System.Numerics;
using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

public class NpcAttackSpreadMathTests
{
    [Fact]
    public void ResolveSpreadPct_StandingFirstShot_IsTheTemplateCone()
    {
        // Min 2, max 8, starting 0.5: the first standing shot opens at the midpoint of the band,
        // the same number WeaponSpreadProfile.Build + GetCurrentSpreadPct would give a player with
        // no heat, no movement and agility 1.
        Assert.Equal(5f, NpcAttackSpreadMath.ResolveSpreadPct(2f, 8f, 0.5f, 0f));
    }

    [Fact]
    public void ResolveSpreadPct_StartingSpreadOfZero_OpensAtMinSpread()
    {
        Assert.Equal(2f, NpcAttackSpreadMath.ResolveSpreadPct(2f, 8f, 0f, 0f));
    }

    [Fact]
    public void ResolveSpreadPct_StartingSpreadOfOne_OpensAtMaxSpread()
    {
        Assert.Equal(8f, NpcAttackSpreadMath.ResolveSpreadPct(2f, 8f, 1f, 0f));
    }

    [Fact]
    public void ResolveSpreadPct_ClampsStartingSpreadToTheBand()
    {
        // A starting_spread outside [0, 1] cannot push the cone past min or max: the player path
        // clamps the same way.
        Assert.Equal(2f, NpcAttackSpreadMath.ResolveSpreadPct(2f, 8f, -1f, 0f));
        Assert.Equal(8f, NpcAttackSpreadMath.ResolveSpreadPct(2f, 8f, 2f, 0f));
    }

    [Fact]
    public void ResolveSpreadPct_ItemSpreadAttribute_ScalesTheCone()
    {
        // Attribute 958 is a fraction of the template max, identical to WeaponSpreadProfile.Build:
        // attr 4 against max 8 is a 0.5 scale, so min 2 / band 6 become 1 / 3 and the midpoint is 2.5.
        Assert.Equal(2.5f, NpcAttackSpreadMath.ResolveSpreadPct(2f, 8f, 0.5f, 4f));
    }

    [Fact]
    public void ResolveSpreadPct_WithoutAMax_TreatsTheAttributeAsTheCone()
    {
        // A template with no max: the attribute is the cone itself (WeaponSpreadProfile.Build), then
        // starting_spread walks the (attr - min * attr) band. min 0, attr 4, starting 0.5 -> 2.
        Assert.Equal(2f, NpcAttackSpreadMath.ResolveSpreadPct(0f, 0f, 0.5f, 4f));
    }

    [Fact]
    public void ResolveSpreadPct_AWeaponTheDatabaseGivesNoSpread_IsZero()
    {
        Assert.Equal(0f, NpcAttackSpreadMath.ResolveSpreadPct(0f, 0f, 0f, 0f));
        Assert.Equal(0f, NpcAttackSpreadMath.ResolveSpreadPct(0f, 0f, 1f, 0f));
    }

    [Fact]
    public void Apply_ZeroSpread_ReturnsTheAimUnchanged()
    {
        var aim = Vector3.Normalize(new Vector3(1f, 0.2f, 0.1f));

        Vector3 result = NpcAttackSpreadMath.Apply(aim, 0f, 60_050u, 0, 0, Vector3.Zero, 60_050u);

        Assert.Equal(aim, result);
    }

    [Fact]
    public void Apply_BelowTheMinimum_IsANoOp()
    {
        var aim = Vector3.UnitX;

        Vector3 result = NpcAttackSpreadMath.Apply(
            aim,
            NpcAttackSpreadMath.MinimumSpreadPct * 0.5f,
            60_050u,
            0,
            0,
            Vector3.Zero,
            60_050u);

        Assert.Equal(aim, result);
    }

    [Fact]
    public void Apply_NonZeroSpread_ScattersTheAim()
    {
        var aim = Vector3.UnitX;

        Vector3 result = NpcAttackSpreadMath.Apply(aim, 3f, 60_050u, 2, 0, Vector3.Zero, 60_050u);

        Assert.NotEqual(aim, result);
        Assert.True(Vector3.Dot(Vector3.Normalize(result), aim) > 0.9f, "a 3% cone still points at the target");
    }

    [Fact]
    public void Apply_IsDeterministic()
    {
        var aim = Vector3.Normalize(new Vector3(0.4f, 0.9f, 0.1f));

        Vector3 first = NpcAttackSpreadMath.Apply(aim, 4f, 12_345u, 1, 2, Vector3.Zero, 12_345u);
        Vector3 second = NpcAttackSpreadMath.Apply(aim, 4f, 12_345u, 1, 2, Vector3.Zero, 12_345u);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Apply_TwoRoundsOfABurst_DoNotLandOnTheSameRay()
    {
        // The seed includes the round number, so a shotgun's pellets scatter instead of stacking.
        var aim = Vector3.UnitY;

        Vector3 first = NpcAttackSpreadMath.Apply(aim, 6f, 60_050u, 0, 0, Vector3.Zero, 60_050u);
        Vector3 second = NpcAttackSpreadMath.Apply(aim, 6f, 60_050u, 0, 1, first, 60_050u);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Apply_AimingStraightUp_DoesNotProduceNaN()
    {
        // Aim x world +Z vanishes when the target is directly above the muzzle; the fallback basis
        // (aim x world +X) has to keep the shot on a real direction.
        Vector3 result = NpcAttackSpreadMath.Apply(Vector3.UnitZ, 3f, 60_050u, 0, 0, Vector3.Zero, 60_050u);

        Assert.False(float.IsNaN(result.X) || float.IsNaN(result.Y) || float.IsNaN(result.Z));
        Assert.True(result.LengthSquared() > 0.5f);
        Assert.True(Vector3.Dot(Vector3.Normalize(result), Vector3.UnitZ) > 0.9f);
    }

    [Fact]
    public void Apply_AimingStraightDown_DoesNotProduceNaN()
    {
        Vector3 result = NpcAttackSpreadMath.Apply(-Vector3.UnitZ, 3f, 60_050u, 0, 0, Vector3.Zero, 60_050u);

        Assert.False(float.IsNaN(result.X) || float.IsNaN(result.Y) || float.IsNaN(result.Z));
        Assert.True(result.LengthSquared() > 0.5f);
        Assert.True(Vector3.Dot(Vector3.Normalize(result), -Vector3.UnitZ) > 0.9f);
    }
}
