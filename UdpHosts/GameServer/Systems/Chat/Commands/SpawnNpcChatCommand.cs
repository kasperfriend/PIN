using System.Numerics;
using GameServer.StaticDB;

namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Spawn a mob (NPC/monster) by characterTypeId, optionally at a location.",
    "npc <characterTypeId> [<x> <y> <z>]",
    "npc", "character", "monster", "spawn_npc", "spawn_character", "spawn_monster")]
public class SpawnNpcChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        if (parameters.Length != 1 && parameters.Length != 4)
        {
            SourceFeedback("Invalid number of parameters for spawn npc command", context);
            return;
        }

        if (context.SourcePlayer?.CharacterEntity == null && parameters.Length != 4)
        {
            SourceFeedback("Must provide position if player character is not available", context);
            return;
        }

        uint typeId = ParseUIntParameter(parameters[0]);
        if (SDBInterface.GetMonster(typeId) == null)
        {
            SourceFeedback("No monster data for this typeId", context);
            return;
        }

        if (parameters.Length == 4)
        {
            Vector3? paramPosition = ParseVector3Parameters(parameters, 1);
            if (paramPosition != null)
            {
                var position = (Vector3)paramPosition;
                context.Shard.EntityMan.SpawnCharacter(typeId, position);
                SourceFeedback($"Spawned NPC {typeId} at {position}", context);
            }
            else
            {
                SourceFeedback("Failed to parse position", context);
            }
        }
        else
        {
            var position = context.SourcePlayer.CharacterEntity.Position;
            context.Shard.EntityMan.SpawnCharacter(typeId, position);
            SourceFeedback($"Spawned NPC {typeId}", context);
        }
    }
}
