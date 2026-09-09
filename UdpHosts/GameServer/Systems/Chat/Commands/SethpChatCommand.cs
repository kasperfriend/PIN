namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Reset your health to the database value, or set it (debug)", "sethp [<amount>]", "sethp", "hpme", "fullhp")]
public class SethpChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        if (parameters.Length > 1)
        {
            SourceFeedback("Usage: sethp (reset to database value) | sethp <amount>", context);
            return;
        }

        context.Shard.Admin.ExecuteCommand(parameters.Length == 1 ? $"hp {parameters[0]}" : "hp", context.SourcePlayer);
    }
}
