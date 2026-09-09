using GameServer.Entities.Character;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand(
    "Set/reset your (or your target's) health: hp resets to the database value, hp <amount> sets it",
    "hp [<amount>]",
    "hp",
    "sethealth",
    "resethealth")]
public class HpServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer == null)
        {
            SourceFeedback("hp requires a player character", context);
            return;
        }

        if (parameters.Length > 1)
        {
            SourceFeedback("Usage: hp (reset to database value) | hp <amount>", context);
            return;
        }

        var character = context.Target as CharacterEntity ?? context.SourcePlayer.CharacterEntity;
        if (character == null)
        {
            SourceFeedback("No character to modify", context);
            return;
        }

        if (parameters.Length == 1)
        {
            uint amount = ParseUIntParameter(parameters[0]);
            if (amount == 0)
            {
                SourceFeedback("Amount must be positive (use bare `hp` to reset)", context);
                return;
            }

            character.SetMaxHealth((int)amount, resetCurrent: true);
            SourceFeedback(
                $"Health set to {character.MaxHealth.Value}. Health: {character.CurrentHealth}/{character.MaxHealth.Value}",
                context);
            return;
        }

        int before = character.MaxHealth.Value;
        character.ResetMaxHealthFromDatabase();
        if (character.MaxHealth.Value == before)
        {
            // No database source applies yet (e.g. the pre-loadout default pool): heal to full.
            character.SetCurrentHealth(character.MaxHealth.Value);
            SourceFeedback(
                $"No database health source to reset to; kept the pool at {character.MaxHealth.Value} and filled it",
                context);
            return;
        }

        SourceFeedback(
            $"Health reset to the database value. Health: {character.CurrentHealth}/{character.MaxHealth.Value}",
            context);
    }
}
