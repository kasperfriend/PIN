using GameServer.Data;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class ShardZoneTests
{
    [Fact]
    public void PlayerInShardZone_CountsAsPresent()
    {
        var shard = new FakeShard(); // ZoneId 448
        var player = new FakeNetworkPlayer(shard) { CurrentZone = new Zone { ID = 448, Name = "New Eden" } };

        Assert.True(ShardZone.IsPlayerInZone(shard, player));
    }

    [Fact]
    public void PlayerInAnotherZone_CountsAsElsewhere()
    {
        var shard = new FakeShard(); // ZoneId 448
        var player = new FakeNetworkPlayer(shard) { CurrentZone = new Zone { ID = 1030, Name = "Sertao" } };

        Assert.False(ShardZone.IsPlayerInZone(shard, player));
    }

    [Fact]
    public void PlayerWithNoZoneYet_CountsAsPresent()
    {
        var shard = new FakeShard(); // ZoneId 448
        var player = new FakeNetworkPlayer(shard); // CurrentZone null: not placed anywhere yet

        Assert.True(ShardZone.IsPlayerInZone(shard, player));
    }
}
