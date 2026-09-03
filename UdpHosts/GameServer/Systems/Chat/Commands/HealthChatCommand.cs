namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Print your character's vitals (debug)", "health", "health", "vitals", "hp")]
public class HealthChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        var character = context.SourcePlayer?.CharacterEntity;
        if (character == null)
        {
            SourceFeedback("No player character available", context);
            return;
        }

        var lifecycleState = context.Shard.CharacterLifecycle.GetState(character);
        string message = $"Health: {character.CurrentHealth}/{character.MaxHealth.Value}"
                       + $" | Shields: {character.CurrentShields}/{character.MaxShields.Value}"
                       + $" | State: {character.CharacterState.State} ({lifecycleState})";

        SourceFeedback(message, context);
    }
}
