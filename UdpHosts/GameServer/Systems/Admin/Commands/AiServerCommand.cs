using System.Text;
using GameServer.Systems.Ai;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand(
    "Inspect and toggle server side NPC AI.",
    "ai [on|off|status|list]",
    "ai",
    "mobai")]
public class AiServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        var engine = context.Shard.AI;
        if (engine == null)
        {
            SourceFeedback("NPC AI is not available on this shard", context);
            return;
        }

        var action = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "status";

        switch (action)
        {
            case "on":
            case "enable":
            case "1":
                engine.Enabled = true;
                SourceFeedback("NPC AI enabled", context);
                break;

            case "off":
            case "disable":
            case "0":
                engine.Enabled = false;
                SourceFeedback("NPC AI disabled", context);
                break;

            case "list":
                SourceFeedback(DescribeTracked(engine), context);
                break;

            case "status":
                SourceFeedback(
                    $"NPC AI: {(engine.Enabled ? "on" : "off")} | tracking {engine.TrackedCount} NPC(s)",
                    context);
                break;

            default:
                SourceFeedback("Unknown ai action. Try: on, off, status, list", context);
                break;
        }
    }

    private string DescribeTracked(AiEngine engine)
    {
        if (engine.TrackedCount == 0)
        {
            return "No NPCs are being simulated";
        }

        var builder = new StringBuilder($"NPC AI tracking {engine.TrackedCount}:");
        foreach (var entityId in engine.GetTrackedEntityIds())
        {
            builder.Append($" {entityId}={engine.GetState(entityId)}");
        }

        return builder.ToString();
    }
}
