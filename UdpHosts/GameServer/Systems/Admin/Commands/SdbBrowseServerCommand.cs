using GameServer.Systems.Spawning;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand(
    "Browse/search the static database catalog of spawnable things.",
    "sdb [<monster|deployable|vehicle|carryable|turret>] [<id|name filter>] [limit]",
    "sdb",
    "sdblist",
    "sdbsearch",
    "sdbfind")]
public class SdbBrowseServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        var message = SDBSpawner.Browse(parameters);

        if (context.SourcePlayer != null)
        {
            context.SourcePlayer.SendDebugLog(message);
            context.SourcePlayer.SendDebugChat("Static database results printed to console");
        }
        else
        {
            Logger.Information("{Message}", message);
        }
    }
}
