namespace GameServer.Systems.Admin.Commands;

[ServerCommand(
    "Set your outgoing damage multiplier (dmg <mult>), one-hit-kill everything (dmg -1) or reset (dmg 1)",
    "dmg <0.5|1|2|-1>",
    "dmg",
    "damagemult",
    "cheatdamage")]
public class DmgServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer == null || context.SourcePlayer.CharacterEntity == null)
        {
            SourceFeedback("dmg requires a player character", context);
            return;
        }

        if (parameters.Length != 1)
        {
            SourceFeedback("Usage: dmg <multiplier> (e.g. 1 = normal, 10 = x10, -1 = one hit kill, 0 = no damage)", context);
            return;
        }

        float multiplier = ParseFloatParameter(parameters[0]);
        if (float.IsNaN(multiplier))
        {
            SourceFeedback("Invalid multiplier (use e.g. 1, 0.5, 10 or -1)", context);
            return;
        }

        var state = context.Shard.Cheats.GetState(context.SourcePlayer);
        state.DamageMultiplier = multiplier;

        string description = multiplier < 0f
            ? "one-hit kill everything"
            : multiplier == 0f
                ? "deal no damage"
                : $"{multiplier:0.###}x damage";

        SourceFeedback($"Outgoing damage: {description}", context);
    }
}
