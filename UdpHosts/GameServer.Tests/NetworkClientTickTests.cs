using System.Net;
using System.Threading;
using GameServer.Tests.Fakes;
using Serilog;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The shard thread ticks every client in its map. A client that is in the map but not yet
///     initialised has no channels and no send queue, and ticking it used to throw a
///     NullReferenceException out of <c>NetworkClient.NetworkTick</c> - logged as "Shard {id} failed
///     to process network traffic for client {socket}" a moment after the player's character was
///     created. <c>Shard.MigrateIn</c> now initialises before it publishes the client; these pin the
///     tick itself against a half-built client either way.
/// </summary>
public class NetworkClientTickTests
{
    private static NetworkPlayer CreateClient(uint socketId = 4278255445) =>
        new(new IPEndPoint(IPAddress.Loopback, 25001), socketId, Log.Logger);

    [Fact]
    public void NetworkTick_OnAClientThatWasNeverInitialised_DoesNotThrow()
    {
        var client = CreateClient();

        Assert.Null(client.NetChannels);
        Assert.Null(client.SequencedMessages);

        var exception = Record.Exception(() => client.NetworkTick(16.0, 0, CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public void NetworkTick_AfterInit_StillFlushesTheSequencedMessages()
    {
        var shard = new FakeShard { Settings = new GameServerSettings() };
        var client = CreateClient(socketId: 0x01020304);
        client.Init(shard);

        byte[] payload = [0x11, 0x22, 0x33];
        client.SequencedMessages.Enqueue(payload);

        client.NetworkTick(16.0, 0, CancellationToken.None);

        // One datagram: the big-endian socket id the packet server routes by, then the payload.
        var sent = Assert.Single(shard.SentPackets);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x11, 0x22, 0x33 }, sent);
        Assert.Empty(client.SequencedMessages);
    }

    [Fact]
    public void NetworkTick_AfterInit_WithNothingQueued_SendsNothing()
    {
        var shard = new FakeShard { Settings = new GameServerSettings() };
        var client = CreateClient();
        client.Init(shard);

        client.NetworkTick(16.0, 0, CancellationToken.None);

        Assert.Empty(shard.SentPackets);
    }
}
