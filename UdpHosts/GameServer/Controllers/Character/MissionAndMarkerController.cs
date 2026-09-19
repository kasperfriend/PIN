using Aero.Protocol;
using GameServer.Packets;
using Serilog;

namespace GameServer.Controllers.Character;

[Typecode(GssCharacterView.MissionAndMarkerController)]
public class MissionAndMarkerController : Base
{
    public override void Init(INetworkClient client, IPlayer player, IShard shard, ILogger logger)
    {
    }

    [MessageID(GssCharacterCommand.RequestAllAchievements)]
    public void RequestAllAchievements(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        // Unimplemented: there is no server-side achievement tracking yet, so there
        // is nothing to answer. Consuming the packet keeps it out of the
        // unhandled-command log; the client shows an empty achievement list.
    }

    [MessageID(GssCharacterCommand.TryResumeTutorialChain)]
    public void TryResumeTutorialChain(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        // Unimplemented: tutorial chains are mission-script driven and the tutorial
        // missions are not authored server-side. Consuming the packet keeps it out
        // of the unhandled-command log.
    }
}