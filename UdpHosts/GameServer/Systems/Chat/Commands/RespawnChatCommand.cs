namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Force respawn your character (debug)", "respawn", "respawn")]
public class RespawnChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        context.Shard.Admin.ExecuteCommand("respawn", context.SourcePlayer);
    }
}
