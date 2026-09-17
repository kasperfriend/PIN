namespace GameServer.Systems.Chat.Commands;

[ChatCommand(
    "Add any stacked currency/resource to your wallet by SDB id. Works for crystite (10), tokens, salvage mats, etc. e.g. \\resource 10 500000 or \\resource 85771 100.",
    "resource <sdbId> [amount]",
    "resource",
    "addresource",
    "giveresource",
    "addcurrency",
    "givecurrency",
    "currency")]
public class ResourceChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        if (parameters.Length == 0)
        {
            SourceFeedback("Usage: \\resource <sdbId> [amount]  e.g. \\resource 10 500000  (10 = crystite)  or \\wallet, \\crystite", context);
            return;
        }

        var cmd = parameters.Length >= 2 ? $"resource {parameters[0]} {parameters[1]}" : $"resource {parameters[0]}";
        context.Shard.Admin.ExecuteCommand(cmd, context.SourcePlayer);
    }
}
