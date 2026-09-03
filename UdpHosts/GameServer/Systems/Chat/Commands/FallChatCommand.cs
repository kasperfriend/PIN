namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Simulate a fall impact at the given downward speed in u/s (debug, 12 = safe, 48+ = lethal)", "fall <speed>", "fall", "falldamage")]
public class FallChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        var character = context.SourcePlayer?.CharacterEntity;
        if (character == null)
        {
            SourceFeedback("No player character available", context);
            return;
        }

        if (parameters.Length != 1 || !float.TryParse(parameters[0], out float impactSpeed) || impactSpeed <= 0)
        {
            SourceFeedback("Usage: fall <impactSpeed> (u/s, try 20 for a big hit or 60 for a lethal one)", context);
            return;
        }

        context.Shard.FallDamage.SimulateLanding(character, impactSpeed);
        SourceFeedback($"Simulated a landing at {impactSpeed:0.#} u/s. Health: {character.CurrentHealth}/{character.MaxHealth.Value}", context);
    }
}
