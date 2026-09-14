using GameServer.Systems.Spawning.Population;

namespace GameServer.Systems.Chat.Commands;

[ChatCommand(
    "Inspect and toggle world population: the zone's monsters and NPCs, placed from the database.",
    "population [on|off|status|near [radius]]",
    "population",
    "pop")]
public class PopulationChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        PopulationCommand.Run(
            context.Shard,
            parameters,
            context.SourcePlayer?.CharacterEntity?.Position,
            message => SourceFeedback(message, context));
    }
}
