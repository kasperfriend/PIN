namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Set your battleframe progression level (debug)", "level <1-50>", "level", "setlevel")]
public class LevelChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        if (parameters.Length != 1)
        {
            SourceFeedback("Usage: level <1-50>", context);
            return;
        }

        context.Shard.Admin.ExecuteCommand($"setlevel {parameters[0]}", context.SourcePlayer);
    }
}
