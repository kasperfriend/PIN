namespace GameServer.Systems.Chat.Commands;

[ChatCommand(
    "Set your outgoing damage multiplier; -1 = one hit kill (debug)",
    "dmg <0.5|1|2|-1>",
    "dmg")]
public class DmgChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        if (parameters.Length != 1)
        {
            SourceFeedback("Usage: dmg <multiplier> (1 = normal, 10 = x10, -1 = one hit kill, 0 = no damage)", context);
            return;
        }

        context.Shard.Admin.ExecuteCommand($"dmg {parameters[0]}", context.SourcePlayer);
    }
}
