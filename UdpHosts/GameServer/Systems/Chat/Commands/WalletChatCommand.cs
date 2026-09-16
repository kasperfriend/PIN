namespace GameServer.Systems.Chat.Commands;

[ChatCommand(
    "Fill your wallet for vendors: crystite + vending tokens. 'wallet' gives 500k cy + 100 tokens, 'wallet all' tops every fallback resource too.",
    "wallet [all|amount]",
    "wallet",
    "fillwallet",
    "givewallet",
    "refill",
    "currencies")]
public class WalletChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        var arg = parameters.Length >= 1 ? $" {string.Join(' ', parameters)}" : string.Empty;
        context.Shard.Admin.ExecuteCommand($"wallet{arg}", context.SourcePlayer);
    }
}
