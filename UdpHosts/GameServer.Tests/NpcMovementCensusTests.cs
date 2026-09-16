using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Tests the actual prod-1962 movement surface, not just hand-written sample invocations.
///     The JSON is regenerated/verified directly from the bundled sd2 by npc_movement_audit.py.
/// </summary>
public class NpcMovementCensusTests
{
    [Fact]
    public void EveryMonsterAndAllThreeBehaviorColumnsResolveWithoutInventingRoutes()
    {
        using var stream = typeof(NpcMovementCensusTests).Assembly.GetManifestResourceStream("NpcMovementReference.json");
        Assert.NotNull(stream);
        using var document = JsonDocument.Parse(stream);
        Assert.Equal("prod-1962", document.RootElement.GetProperty("patch").GetString());
        var rows = document.RootElement.GetProperty("monsters");
        Assert.Equal(3109, rows.GetArrayLength());
        var ids = new HashSet<uint>();
        int wanderers = 0;
        int disabledDistance = 0;
        foreach (var row in rows.EnumerateArray())
        {
            uint id = row.GetProperty("id").GetUInt32();
            Assert.True(ids.Add(id));
            foreach (string field in new[] { "behavior", "behavior_offensive", "behavior_defensive" })
            {
                var parsed = NpcBehaviorParams.Parse(row.GetProperty(field).GetString());
                var profile = NpcRoutineProfile.Resolve(parsed);
                Assert.True(float.IsFinite(profile.WanderDistance));
                Assert.True(float.IsFinite(profile.HomeRadius));
                Assert.InRange(profile.WanderDistance, 0f, profile.HomeRadius);
                Assert.InRange(profile.WanderChance, 0f, 1f);
                Assert.InRange(profile.RestMinMs, 0, profile.RestMaxMs);
                if (profile.Kind == NpcRoutineKind.ExternalRoute)
                {
                    Assert.False(profile.HasRoutine);
                    Assert.NotEmpty(profile.MissingData);
                }
            }

            var routineProfile = NpcRoutineProfile.Resolve(
                NpcBehaviorParams.Parse(row.GetProperty("behavior").GetString()),
                NpcBehaviorParams.Parse(row.GetProperty("behavior_offensive").GetString()));
            if (routineProfile.Kind == NpcRoutineKind.Wander)
            {
                wanderers++;
                if (routineProfile.WanderDistance == 0f)
                {
                    disabledDistance++;
                }
            }

            var routine = new NpcRoutine((ulong)id << 8, id, Vector3.Zero, routineProfile, 0, null);
            routine.Update(routine.NextActionAt, Vector3.Zero, true, true, true);
            if (!routineProfile.HasRoutine || routineProfile.WanderDistance == 0f)
            {
                Assert.Null(routine.Goal);
            }
            else if (routine.Goal.HasValue)
            {
                Assert.True(NpcGroundMovement.Finite(routine.Goal.Value));
                Assert.InRange(AiVectors.HorizontalDistance(Vector3.Zero, routine.Goal.Value), 0f, routineProfile.HomeRadius + 0.001f);
            }
        }

        // 510 declared wanderer rows, minus the two city_prefix requests with missing named points,
        // plus the 1,068 rows that ship with an empty CAIS string and now mill with the same
        // bounded roam. One of the declared rows (700) explicitly says wanderDistance=0 and must
        // not move.
        Assert.Equal(1576, wanderers);
        Assert.Equal(1, disabledDistance);
    }
}
