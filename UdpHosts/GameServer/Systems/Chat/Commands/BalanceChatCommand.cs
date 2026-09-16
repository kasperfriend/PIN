namespace GameServer.Systems.Chat.Commands;

[ChatCommand(
    "Show your wallet: every stacked resource pool you carry (crystite, tokens, mats) with SDB id and quantity.",
    "balance",
    "balance",
    "showwallet",
    "showcurrencies")]
public class BalanceChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        context.Shard.Admin.ExecuteCommand("balance", context.SourcePlayer);
    }
}
