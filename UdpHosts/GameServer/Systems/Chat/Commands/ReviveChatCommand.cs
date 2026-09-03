namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Revive your own character from bleedout (debug)", "revive", "revive")]
public class ReviveChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        context.Shard.Admin.ExecuteCommand("revive", context.SourcePlayer);
    }
}
