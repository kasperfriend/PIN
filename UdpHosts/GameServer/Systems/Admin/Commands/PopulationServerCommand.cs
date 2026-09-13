using GameServer.Systems.Spawning.Population;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand(
    "Inspect and toggle world population: the zone's monsters and NPCs, placed from the database.",
    "population [on|off|status|near [radius]]",
    "population",
    "pop")]
public class PopulationServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        PopulationCommand.Run(
            context.Shard,
            parameters,
            context.SourcePlayer?.CharacterEntity?.Position,
            message => SourceFeedback(message, context));
    }
}
