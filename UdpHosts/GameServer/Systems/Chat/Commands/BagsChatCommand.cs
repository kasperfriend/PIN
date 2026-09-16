namespace GameServer.Systems.Chat.Commands;

[ChatCommand(
    "Fix a stuck 'inventory full' red blink and show bag usage. Forces a bag-model sync so the client's local 9-bag check matches the server's real inventory.",
    "bags [fix|status]",
    "bags",
    "fixbags",
    "bagfix")]
public class BagsChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        var arg = parameters.Length >= 1 ? $" {parameters[0]}" : string.Empty;
        context.Shard.Admin.ExecuteCommand($"bags{arg}", context.SourcePlayer);
    }
}
