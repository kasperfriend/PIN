namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Put your own character into bleedout (debug)", "down", "down", "bleed")]
public class DownChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        context.Shard.Admin.ExecuteCommand("downme", context.SourcePlayer);
    }
}
