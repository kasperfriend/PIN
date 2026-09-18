using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using GameServer.Physics;
using GameServer.Systems.Spawning.Population;
using GameServer.Systems.SystemEvents;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The overhead-cover check that keeps world population on open ground, run against a real
///     physics engine loaded with a floor and roof slabs instead of a zone. The shapes stand for
///     the two halves of the complaint the check answers: a covered spot must stop resolving (a
///     mob in a cave or under the terrain, invisible to whoever walks above it yet still shooting
///     them), and an open spot must keep resolving - an earlier take on this check got the second
///     half wrong (the whole zone read as covered) and took all spawning with it.
/// </summary>
public class PhysicsWorldPopulationTerrainTests
{
    private readonly PhysicsEngine _engine = new(new EventBus(), 448);
    private readonly PhysicsWorldPopulationTerrain _terrain;

    public PhysicsWorldPopulationTerrainTests()
    {
        _terrain = new PhysicsWorldPopulationTerrain(_engine, new StandardWorldPopulationRules());
    }

    [Fact]
    public void AnOpenSpotOnTheFloorResolves()
    {
        AddBox(new Vector3(0f, 0f, -1f), new Vector3(20f, 20f, 1f)); // flat ground with its top at z = 0

        Assert.True(_engine.TryGetGroundSurface(new Vector3(0f, 0f, 0f), out var ground, out _));
        Assert.InRange(ground.Z, -0.01f, 0.01f);

        Assert.False(_engine.HasOverheadCover(new Vector3(0f, 0f, 0f), 1.8f));

        Assert.True(_terrain.TryResolveStandingSpot(new Vector3(0f, 0f, 0f), 0.7f, 1.8f, out var spot));
        Assert.InRange(spot.Z, -0.01f, 0.01f);
    }

    [Fact]
    public void ASpotUnderARoofIsRefusedEvenThoughTheBodyFits()
    {
        AddBox(new Vector3(0f, 0f, -1f), new Vector3(20f, 20f, 1f)); // flat ground with its top at z = 0
        AddBox(new Vector3(0f, 0f, 11f), new Vector3(5f, 5f, 1f)); // a roof hanging over the origin

        // The floor under it is still walkable, the body still fits - and the spot is still
        // refused, because a mob standing where the world hangs over it is the one that reads as
        // spawning underground.
        Assert.True(_engine.HasOverheadCover(new Vector3(0f, 0f, 0f), 1.8f));
        Assert.False(_terrain.TryResolveStandingSpot(new Vector3(0f, 0f, 0f), 0.7f, 1.8f, out _));

        // And the ground beside the roof stays open, so a cell that holds both keeps spawning.
        Assert.False(_engine.HasOverheadCover(new Vector3(15f, 15f, 0f), 1.8f));
        Assert.True(_terrain.TryResolveStandingSpot(new Vector3(15f, 15f, 0f), 0.7f, 1.8f, out _));
    }

    [Fact]
    public void AHighArchIsNotCover()
    {
        AddBox(new Vector3(0f, 0f, -1f), new Vector3(20f, 20f, 1f)); // flat ground with its top at z = 0
        AddBox(new Vector3(0f, 0f, 21f), new Vector3(5f, 5f, 1f)); // an arch whose underside starts at z = 20

        // The probe reaches 12 m above the head and no further: whatever hangs higher than that
        // (a natural arch, a tree line) is scenery over open ground, not the roof of a cave -
        // and treating it as a roof is how the field stops spawning again.
        Assert.False(_engine.HasOverheadCover(new Vector3(0f, 0f, 0f), 1.8f));
        Assert.True(_terrain.TryResolveStandingSpot(new Vector3(0f, 0f, 0f), 0.7f, 1.8f, out _));
    }

    [Fact]
    public void AZoneTheCoverRuleReadsAsCoveredEverywhereStillPopulates()
    {
        AddBox(new Vector3(0f, 0f, -1f), new Vector3(20f, 20f, 1f)); // ground, top at z = 0
        AddBox(new Vector3(0f, 0f, 11f), new Vector3(20f, 20f, 1f)); // a roof over the whole of it

        // Every spot the rule is asked about is refused. That is the shape that emptied whole zones
        // twice before, so the rule suspends itself instead of letting the plan park on ground the
        // zone's own collision calls walkable. The suspicion threshold is higher than the original
        // 40 to avoid tripping on the first few outpost-dense cells at plan start; here every
        // refusal is genuine so drive enough attempts to reach it.
        const int refusalsNeeded = 200;
        for (int i = 0; i < refusalsNeeded; i++)
        {
            Assert.False(_terrain.TryResolveStandingSpot(new Vector3(0f, 0f, 0f), 0.7f, 1.8f, out _));
        }

        Assert.True(_terrain.CoverRuleSuspended);
        Assert.Equal(refusalsNeeded, _terrain.CoverRefusals);

        // And from there on the spot resolves: the population comes back as it was before the rule.
        Assert.True(_terrain.TryResolveStandingSpot(new Vector3(0f, 0f, 0f), 0.7f, 1.8f, out var spot));
        Assert.InRange(spot.Z, -0.01f, 0.01f);
    }

    [Fact]
    public void AZoneWithOpenGroundForTheCoverRuleToApproveKeepsIt()
    {
        AddBox(new Vector3(0f, 0f, -1f), new Vector3(20f, 20f, 1f)); // ground, top at z = 0
        AddBox(new Vector3(0f, 0f, 11f), new Vector3(5f, 5f, 1f)); // a roof over the origin only

        // Half the spots are sheltered and half are open: the rule is answering about single spots,
        // not contradicting the zone, so it stays on however long it is asked - even when the count
        // is well past the suspicion threshold, the refusal ratio is only 50% (not 95%).
        for (int i = 0; i < 300; i++)
        {
            if (i % 2 == 0)
            {
                Assert.False(_terrain.TryResolveStandingSpot(new Vector3(0f, 0f, 0f), 0.7f, 1.8f, out _));
            }
            else
            {
                Assert.True(_terrain.TryResolveStandingSpot(new Vector3(15f, 15f, 0f), 0.7f, 1.8f, out _));
            }
        }

        Assert.False(_terrain.CoverRuleSuspended);
        Assert.Equal(150, _terrain.CoverRefusals);
    }

    [Fact]
    public void ASpotThatCannotBeProvenOpenIsRefused()
    {
        AddBox(new Vector3(0f, 0f, -1f), new Vector3(20f, 20f, 1f)); // flat ground with its top at z = 0

        Assert.True(_engine.HasOverheadCover(new Vector3(float.NaN, 0f, 0f), 1.8f));
        Assert.True(_engine.HasOverheadCover(new Vector3(0f, 0f, 0f), float.NaN));
        Assert.False(_terrain.TryResolveStandingSpot(new Vector3(float.NaN, 0f, 0f), 0.7f, 1.8f, out _));
    }

    private void AddBox(Vector3 center, Vector3 halfExtents)
    {
        // Bepu's Box takes full dimensions (TagfileLoader doubles Havok's half extents for it).
        var stat = new StaticDescription(
            new RigidPose(center),
            _engine.Simulation.Shapes.Add(new Box(halfExtents.X * 2f, halfExtents.Y * 2f, halfExtents.Z * 2f)));
        _engine.Simulation.Statics.Add(stat);
    }
}
