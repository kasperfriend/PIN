namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Damage your own character (debug)", "hurt <amount>", "hurt", "damage")]
public class HurtChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        if (parameters.Length != 1)
        {
            SourceFeedback("Usage: hurt <amount>", context);
            return;
        }

        context.Shard.Admin.ExecuteCommand($"hurtme {parameters[0]}", context.SourcePlayer);
    }
}
