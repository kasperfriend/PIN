using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using GameServer.Physics;
using GameServer.Systems.Ai;
using GameServer.Systems.Spawning.Population;
using GameServer.Systems.SystemEvents;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The ground probe every NPC step rests on, run against a real physics engine loaded with mesh
///     triangles instead of slabs. BepuPhysics mesh shapes are single-sided: a ray registers a
///     triangle only when it strikes the front face the winding names, and both of PIN's loaders
///     keep the bake's vertex order when they build the collision mesh, so the winding is whatever
///     the zone file wrote. PIN's own navigation mesh reads that same order with the opposite cross
///     product (<see cref="Shared.Collision.Navigation.NavigationTriangle.Normal" /> against Bepu's
///     <c>RayTest</c>), so a face it calls walkable is one a downward ray sees from below - and the
///     ground probe the locomotion stack used was a straight downward ray. A surface wound away from
///     the sky was therefore invisible under the feet: the shape of the reported "mobs exist, shoot,
///     and never take a step" bug. The spawn snap and the population's standing-spot check read the
///     same probe, so they are held to the same answer here.
/// </summary>
public class PhysicsGroundWindingTests
{
    private readonly PhysicsEngine _engine = new(new EventBus(), 448);
    private readonly BufferPool _pool = new();

    [Fact]
    public void ADownwardRayMissesASurfaceWoundAwayFromIt()
    {
        // The mesh-side fact everything below is about, stated as the engine sees it.
        AddMesh(Ground(z: 0f, facesUp: false));

        Assert.False(_engine.SegmentRayCast(
            new Vector3(0.25f, 0.25f, 5f), new Vector3(0.25f, 0.25f, -5f), 0, staticOnly: true).Hit);

        var fromBelow = _engine.SegmentRayCast(
            new Vector3(0.25f, 0.25f, -5f), new Vector3(0.25f, 0.25f, 5f), 0, staticOnly: true);
        Assert.True(fromBelow.Hit);
        Assert.InRange(fromBelow.HitPosition.Z, -0.01f, 0.01f);
        Assert.True(fromBelow.Normal.Z < -0.9f);
    }

    [Fact]
    public void TheGroundProbeSeesASurfaceOnlyTheUpwardRayReaches()
    {
        AddMesh(Ground(z: 0f, facesUp: false));

        // The window is the walking one: a step's half metre of samples around the agent's feet.
        Assert.True(_engine.TryGetGroundSurface(new Vector3(0.25f, 0.25f, 0f), out var ground, out var normal,
            searchUp: 1.25f, searchDown: 1.25f));

        Assert.InRange(ground.Z, -0.01f, 0.01f);
        Assert.InRange(ground.X, 0.24f, 0.26f);
        Assert.True(normal.Z < -0.9f);
    }

    [Fact]
    public void TheSurfaceUnderTheFeetBeatsTheFloorBeneathIt()
    {
        AddMesh(Ground(z: 0f, facesUp: false)); // the surface the mob stands on, invisible from above
        AddMesh(Ground(z: -2f, facesUp: true)); // the floor under it, which the downward ray does see

        // The population's window is the one that reaches the floor below; the surface the feet rest
        // on still wins, so a mob is not dragged through the ground onto the floor under it.
        Assert.True(_engine.TryGetGroundSurface(new Vector3(0.25f, 0.25f, 0f), out var ground, out _,
            searchUp: 1.5f, searchDown: 3f));

        Assert.InRange(ground.Z, -0.01f, 0.01f);
    }

    [Fact]
    public void TheSpawnSnapLandsOnTheSurfaceNotOnTheFloorFarBelow()
    {
        AddMesh(Ground(z: 0f, facesUp: false));
        AddMesh(Ground(z: -50f, facesUp: true));

        // This is what put mobs under the terrain: the snap's ten kilometre downward probe sailed
        // through the surface and settled on the floor below it.
        var ground = _engine.FindGround(new Vector3(0.25f, 0.25f, 0f));

        Assert.NotNull(ground);
        Assert.InRange(ground!.Value.Z, -0.01f, 0.01f);
    }

    [Fact]
    public void ThePopulationCanStandAMobOnGroundOnlyTheUpwardProbeSees()
    {
        AddMesh(Ground(z: 0f, facesUp: false));

        var terrain = new PhysicsWorldPopulationTerrain(_engine, new StandardWorldPopulationRules());

        Assert.True(terrain.TryResolveStandingSpot(new Vector3(0.25f, 0.25f, 0f), 0.7f, 1.8f, out var spot));
        Assert.InRange(spot.Z, -0.01f, 0.01f);
    }

    [Fact]
    public void AnNpcTakesAStepOnGroundOnlyTheUpwardProbeSees()
    {
        AddMesh(Ground(z: 0f, facesUp: false));

        var shard = new FakeShard { Physics = _engine };
        var navigation = new PhysicsNpcNavigation(shard, new StandardAiRules());
        var agent = new NpcNavigationAgent(1, 0.7f, 1.8f);

        // Half a metre of chase movement: the step the mob used to refuse every single tick, which
        // left it standing at range while it kept firing.
        Assert.True(navigation.TryStep(Vector3.Zero, new Vector3(0.5f, 0f, 0f), agent, out var position));
        Assert.InRange(position.X, 0.49f, 0.5f);
        Assert.InRange(position.Z, -0.01f, 0.01f);
    }

    [Fact]
    public void AnNpcIsStillRefusedWhenTheGroundIsOutOfReach()
    {
        // Six metres under the agent is not ground it can stand on: the walking window is the
        // step's own 1.25 m, and even the spawn snap's ten kilometre window stops its winding
        // fallback five metres under the query rather than dropping to the bottom of the world.
        AddMesh(Ground(z: -6f, facesUp: false));

        var shard = new FakeShard { Physics = _engine };
        var navigation = new PhysicsNpcNavigation(shard, new StandardAiRules());
        var agent = new NpcNavigationAgent(1, 0.7f, 1.8f);

        Assert.False(navigation.TryStep(Vector3.Zero, new Vector3(0.5f, 0f, 0f), agent, out _));
        Assert.False(_engine.FindGround(new Vector3(0.25f, 0.25f, 0f)).HasValue);
    }

    [Fact]
    public void TheWindingReportNamesGroundTheDownwardProbeCannotSee()
    {
        AddMesh(Ground(z: 0f, facesUp: false));

        // The load-time report (PhysicsEngine.MeasureGroundFaceVisibility, logged by
        // LogGroundVisibility) asks the two probes about the same points: on ground the bake wound
        // away from the sky the straight downward probe finds none of them and the two-way probe
        // finds every one. That pair of numbers is how an operator tells which way their zone's
        // ground was baked, and it is the same blindness every ground probe used to answer with.
        var (downward, twoWay) = _engine.MeasureGroundVisibility(
            [new Vector3(0.25f, 0.25f, 0f), new Vector3(4.25f, 4.25f, 0f)]);

        Assert.Equal(0, downward);
        Assert.Equal(2, twoWay);
    }

    [Fact]
    public void TheWindingReportAgreesWithItselfOnGroundThatFacesTheSky()
    {
        AddMesh(Ground(z: 0f, facesUp: true));

        var (downward, twoWay) = _engine.MeasureGroundVisibility([new Vector3(0.25f, 0.25f, 0f)]);

        Assert.Equal(1, downward);
        Assert.Equal(1, twoWay);
    }

    private void AddMesh(params Triangle[] triangles)
    {
        _pool.Take<Triangle>(triangles.Length, out var buffer);
        for (int i = 0; i < triangles.Length; i++)
        {
            buffer[i] = triangles[i];
        }

        var mesh = new Mesh(buffer, Vector3.One, _pool);
        _engine.Simulation.Statics.Add(
            new StaticDescription(new RigidPose(Vector3.Zero), _engine.Simulation.Shapes.Add(mesh)));
    }

    /// <summary>
    ///     A wide quad at <paramref name="z" />, wound so a downward ray hits it
    ///     (<paramref name="facesUp" />) or misses it and only an upward ray finds it.
    /// </summary>
    private static Triangle[] Ground(float z, bool facesUp)
    {
        var a = new Vector3(-1000f, -1000f, z);
        var b = new Vector3(-1000f, 1000f, z);
        var c = new Vector3(1000f, 1000f, z);
        var d = new Vector3(1000f, -1000f, z);

        // Swapping two vertices of a triangle reverses which way Bepu sees it facing.
        return facesUp
            ? [new Triangle(a, b, c), new Triangle(a, c, d)]
            : [new Triangle(a, c, b), new Triangle(a, d, c)];
    }
}
