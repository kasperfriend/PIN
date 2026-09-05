using GameServer.Systems.Spawning;

namespace GameServer.Systems.Chat.Commands;

[ChatCommand(
    "Browse/search the static database catalog of spawnable things.",
    "sdb [<monster|deployable|vehicle|carryable|turret>] [<id|name filter>] [limit]",
    "sdb",
    "sdblist",
    "sdbsearch",
    "sdbfind")]
public class SdbBrowseChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        var message = SDBSpawner.Browse(parameters);

        // Multi-line listings are unreadable in the chat window; use the console.
        if (context.SourcePlayer != null)
        {
            context.SourcePlayer.SendDebugLog(message);
            context.SourcePlayer.SendDebugChat("Static database results printed to console");
        }
        else
        {
            _logger.Information("{Message}", message);
        }
    }
}
