namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Switch your battleframe on the fly (debug)", "frame <frameName|frameId>", "frame", "setframe")]
public class FrameChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        if (parameters.Length != 1)
        {
            SourceFeedback("Usage: frame <frameName|frameId> (assault, dreadnaught, recon, firecat, ...)", context);
            return;
        }

        context.Shard.Admin.ExecuteCommand($"setframe {parameters[0]}", context.SourcePlayer);
    }
}
