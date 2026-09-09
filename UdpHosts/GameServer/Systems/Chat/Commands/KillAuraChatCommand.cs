namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Kill every enemy around you once per second (debug)", "killaura [<radius>|on|off]", "killaura", "aura")]
public class KillAuraChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        if (parameters.Length == 0)
        {
            context.Shard.Admin.ExecuteCommand("killaura", context.SourcePlayer);
            return;
        }

        if (parameters.Length == 1)
        {
            context.Shard.Admin.ExecuteCommand($"killaura {parameters[0]}", context.SourcePlayer);
            return;
        }

        SourceFeedback("Usage: killaura (toggle) | killaura on|off | killaura <radius>", context);
    }
}
