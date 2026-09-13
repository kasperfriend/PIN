using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.NpcDeath;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The rule that turns a character's chassis into the gib visuals a corpse reports: the battleframe's
///     <c>gibset_id</c>, 0 included, and nothing at all when the database has no battleframe for the chassis.
/// </summary>
public class GibVisualsResolutionTests
{
    private static Battleframe BattleframeWith(uint gibsetId) => new() { GibsetId = gibsetId };

    [Fact]
    public void Resolve_ReadsTheChassisBattleframesGibSet()
    {
        // NPC Guard Rifle's monster battleframe carries gib set 176, a row dbcharacter::GibVisuals has.
        Assert.True(GibVisualsResolution.TryResolve(32740, _ => BattleframeWith(176), out var gibVisualsId));

        Assert.Equal(176u, gibVisualsId);
    }

    [Fact]
    public void Resolve_GibSetZero_IsTheDatabasesDefaultRow_NotAnAbsence()
    {
        // 804 of the 1,676 battleframes (and the 1,806 monsters using them) carry gibset_id 0, which is
        // dbcharacter::GibVisuals row 0 - the same value the deployable death path has always reported.
        Assert.True(GibVisualsResolution.TryResolve(125_038, _ => BattleframeWith(0), out var gibVisualsId));

        Assert.Equal(0u, gibVisualsId);
    }

    [Fact]
    public void Resolve_DanglingGibSet_IsReportedAsTheDatabaseStatesIt()
    {
        // 54 monsters name a GibVisuals id this build does not ship (132, 7, 168, ...). The server reports
        // what the row says; the client resolves it against its own copy of the database.
        Assert.True(GibVisualsResolution.TryResolve(1, _ => BattleframeWith(132), out var gibVisualsId));

        Assert.Equal(132u, gibVisualsId);
    }

    [Fact]
    public void Resolve_WithoutABattleframeRow_HasNoGibVisuals()
    {
        // 23 monster rows point at a chassis with no dbitems::Battleframe row.
        Assert.False(GibVisualsResolution.TryResolve(30_643, _ => null, out var gibVisualsId));

        Assert.Equal(0u, gibVisualsId);
    }

    [Fact]
    public void Resolve_WithoutAChassis_DoesNotConsultTheDatabase()
    {
        // 89 legacy monster rows carry chassis_id 0.
        bool lookedUp = false;

        bool resolved = GibVisualsResolution.TryResolve(
            0,
            _ =>
            {
                lookedUp = true;
                return BattleframeWith(176);
            },
            out var gibVisualsId);

        Assert.False(resolved);
        Assert.False(lookedUp);
        Assert.Equal(0u, gibVisualsId);
    }
}
