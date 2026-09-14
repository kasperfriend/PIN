using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.ProjectileSim;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The splash radii a fire request carries come straight out of <c>dbitems::Ammo</c>, and the
///     table uses <c>-1</c> for "this round has no splash": 1165 of its 1264 rows carry
///     <c>max_radius = -1</c>, the assault rifle's own "Normal Bullet (assault rifle)" among them.
///     Refusing to fire over a negative radius discarded every round from 92% of the game's ammo,
///     which is what made NPCs immune to the player's weapons.
/// </summary>
public class ProjectileSplashRadiusTests
{
    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(-0.5f, 0f)]
    [InlineData(float.MinValue, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(2f, 2f)]
    [InlineData(2.5f, 2.5f)]
    public void ClampRadius_ReadsTheDatabasesNoSplashSentinelAsZero(float stored, float expected)
    {
        Assert.Equal(expected, ProjectileSim.ClampRadius(stored));
    }

    [Fact]
    public void ClampRadius_LeavesANonFiniteRadiusForTheFinitenessCheckToReject()
    {
        // Clamping must not launder a malformed value into a fireable one: the caller's own
        // finiteness check is what discards those.
        Assert.True(float.IsNaN(ProjectileSim.ClampRadius(float.NaN)));
        Assert.True(float.IsPositiveInfinity(ProjectileSim.ClampRadius(float.PositiveInfinity)));
        Assert.True(float.IsNegativeInfinity(ProjectileSim.ClampRadius(float.NegativeInfinity)));
    }

    [Fact]
    public void TheShippedAssaultRifleAmmo_ClampsToAFireableRadius()
    {
        // dbitems::Ammo row 58, "PvE - Assault Rifle Ammo": no splash at all, and the row the server
        // reported discarding in the field ("ammo 58, speed 1000, range 125, impact radius 0,
        // max radius -1").
        var ammo = new Ammo { Id = 58, ProjectileSpeed = 1000f, ImpactRadius = 0f, MaxRadius = -1f };

        Assert.Equal(0f, ProjectileSim.ClampRadius(ammo.ImpactRadius));
        Assert.Equal(0f, ProjectileSim.ClampRadius(ammo.MaxRadius));
    }
}
