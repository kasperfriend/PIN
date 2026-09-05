using GameServer.Systems.Spawning;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand(
    "Spawn anything from the static database by id or name.",
    "spawn <monster|deployable|vehicle|carryable|turret> <id|name> [<x> <y> <z>]",
    "spawn",
    "sdbspawn",
    "spawn_sdb")]
public class SpawnFromSDBServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        SourceFeedback(SDBSpawner.Spawn(parameters, context.Shard, context.SourcePlayer), context);
    }
}
