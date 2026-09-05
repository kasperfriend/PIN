using GameServer.Systems.Spawning;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand(
    "Show the static database row behind a spawnable id or name.",
    "sdbinfo <monster|deployable|vehicle|carryable|turret> <id|name>",
    "sdbinfo",
    "sdbshow",
    "sdbrow")]
public class SdbInfoServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        var message = SDBSpawner.Info(parameters);

        if (context.SourcePlayer != null)
        {
            context.SourcePlayer.SendDebugLog(message);
            context.SourcePlayer.SendDebugChat("Static database row printed to console");
        }
        else
        {
            Logger.Information("{Message}", message);
        }
    }
}
