using AeroMessages.GSS.Character.Event;
using GameServer.Entities.Character;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand("Set your (or your target's) battleframe progression level", "setlevel <1-50>", "setlevel", "level", "playerlevel")]
public class SetLevelServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer == null)
        {
            SourceFeedback("setlevel requires a player character", context);
            return;
        }

        if (parameters.Length != 1)
        {
            SourceFeedback("Usage: setlevel <1-50>", context);
            return;
        }

        uint raw = ParseUIntParameter(parameters[0]);
        if (raw < 1 || raw > CharacterEntity.MaxFrameProgressionLevel)
        {
            SourceFeedback($"Level must be between 1 and {CharacterEntity.MaxFrameProgressionLevel}", context);
            return;
        }

        var character = context.Target as CharacterEntity ?? context.SourcePlayer.CharacterEntity;
        if (character == null || !character.IsPlayerControlled || character.Player == null)
        {
            SourceFeedback("setlevel only applies to a player-controlled character", context);
            return;
        }

        character.SetFrameProgressionLevel((byte)raw);

        // Keep the frame XP panel in sync (same payload the spawn path sends).
        character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(new ProgressionXPRefresh()
        {
            Frames =
            [
                new()
                {
                    ChassisID = character.CurrentLoadout?.ChassisID ?? 0,
                    XpValue1 = 0,
                    XpValue2 = 0,
                    CurrentLevel = character.FrameProgressionLevel,
                    Unk = 0,
                },
            ]
        }, character.EntityId);

        SourceFeedback(
            $"Level set to {character.FrameProgressionLevel}. Health: {character.CurrentHealth}/{character.MaxHealth.Value}",
            context);
    }
}
