namespace GameServer.Systems.Chat.Commands;

[ChatCommand(
    "Add crystite (vendor currency) to your wallet - the one vendors charge. No amount = 100k, e.g. \\crystite 500000.",
    "crystite [amount]",
    "crystite",
    "cy",
    "money",
    "cash")]
public class CrystiteChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        // Forward to the server command that does the actual inventory work
        var arg = parameters.Length >= 1 ? $" {parameters[0]}" : string.Empty;
        context.Shard.Admin.ExecuteCommand($"crystite{arg}", context.SourcePlayer);
    }
}
