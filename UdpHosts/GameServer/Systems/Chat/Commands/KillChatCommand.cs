namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Kill your own character (debug)", "kill", "kill", "suicide")]
public class KillChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        context.Shard.Admin.ExecuteCommand("killme", context.SourcePlayer);
    }
}
