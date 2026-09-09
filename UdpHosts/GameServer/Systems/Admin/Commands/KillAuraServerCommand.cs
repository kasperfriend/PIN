
namespace GameServer.Systems.Admin.Commands;

[ServerCommand(
    "Kill every enemy around you once per second (killaura [radius] | killaura off)",
    "killaura [<radius>|on|off]",
    "killaura",
    "aura")]
public class KillAuraServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer == null || context.SourcePlayer.CharacterEntity == null)
        {
            SourceFeedback("killaura requires a player character", context);
            return;
        }

        if (parameters.Length > 1)
        {
            SourceFeedback("Usage: killaura (toggle), killaura on|off, killaura <radius>", context);
            return;
        }

        var state = context.Shard.Cheats.GetState(context.SourcePlayer);

        if (parameters.Length == 0)
        {
            state.KillAura = !state.KillAura;
            SourceFeedback($"Kill aura: {(state.KillAura ? "ON" : "OFF")} (radius {state.KillAuraRadiusMeters:0.#} m)", context);
            return;
        }

        string arg = parameters[0].ToLowerInvariant();
        if (arg == "off" || arg == "0")
        {
            state.KillAura = false;
            SourceFeedback("Kill aura: OFF", context);
            return;
        }

        if (arg == "on")
        {
            state.KillAura = true;
            SourceFeedback($"Kill aura: ON (radius {state.KillAuraRadiusMeters:0.#} m)", context);
            return;
        }

        float radius = ParseFloatParameter(arg);
        if (float.IsNaN(radius) || radius <= 0f)
        {
            SourceFeedback("Radius must be a positive number of metres", context);
            return;
        }

        state.KillAuraRadiusMeters = radius;
        state.KillAura = true;
        SourceFeedback($"Kill aura: ON (radius {state.KillAuraRadiusMeters:0.#} m)", context);
    }
}
