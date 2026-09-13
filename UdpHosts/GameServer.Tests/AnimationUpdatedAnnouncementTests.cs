using GameServer.Systems.Combat;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class AnimationUpdatedAnnouncementTests
{
    [Fact]
    public void SendToWatchers_WithoutAnEntityManager_IsANoOp()
    {
        var shard = new FakeShard();
        var source = FakeCharacterFactory.Create(shard);

        AnimationUpdatedAnnouncement.SendToWatchers(
            shard,
            source,
            unk1: 2,
            AnimationUpdatedAnnouncement.BurstStarted);
        AnimationUpdatedAnnouncement.SendToWatchers(null, source, 2, AnimationUpdatedAnnouncement.BurstEnded);
        AnimationUpdatedAnnouncement.SendToWatchers(shard, null, 2, AnimationUpdatedAnnouncement.BurstStarted);
    }
}
