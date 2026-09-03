namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Heal your own character (debug)", "heal <amount>", "heal")]
public class HealChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        if (parameters.Length != 1)
        {
            SourceFeedback("Usage: heal <amount>", context);
            return;
        }

        context.Shard.Admin.ExecuteCommand($"healme {parameters[0]}", context.SourcePlayer);
    }
}
