using GameServer.Systems.Spawning;

namespace GameServer.Systems.Chat.Commands;

[ChatCommand(
    "Show the static database row behind a spawnable id or name.",
    "sdbinfo <monster|deployable|vehicle|carryable|turret> <id|name>",
    "sdbinfo",
    "sdbshow",
    "sdbrow")]
public class SdbInfoChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        var message = SDBSpawner.Info(parameters);

        if (context.SourcePlayer != null)
        {
            context.SourcePlayer.SendDebugLog(message);
            context.SourcePlayer.SendDebugChat("Static database row printed to console");
        }
        else
        {
            _logger.Information("{Message}", message);
        }
    }
}
