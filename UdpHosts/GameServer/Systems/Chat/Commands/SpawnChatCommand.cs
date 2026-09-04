using GameServer.Systems.Spawning;

namespace GameServer.Systems.Chat.Commands;

[ChatCommand(
    "Spawn anything from the static database by id or name.",
    "spawn <monster|deployable|vehicle|carryable|turret> <id|name> [<x> <y> <z>]",
    "spawn",
    "sdbspawn",
    "spawn_sdb")]
public class SpawnChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        SourceFeedback(SDBSpawner.Spawn(parameters, context.Shard, context.SourcePlayer), context);
    }
}
