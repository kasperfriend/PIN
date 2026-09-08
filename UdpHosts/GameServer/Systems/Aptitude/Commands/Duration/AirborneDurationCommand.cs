using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Duration;

public class AirborneDurationCommand : Command, ICommand
{
    private AirborneDurationCommandDef Params;

    public AirborneDurationCommand(AirborneDurationCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var target = context.Self; // NOTE: Investigate

        bool result = false;
        if (target is CharacterEntity character)
        {
            // A launch the server itself commanded (glider pad ForcePush) has no reported pose behind it yet:
            // the client cannot send the post-launch pose before the forced movement window has played, while
            // this effect's duration chain already runs on the server's tick. Count the character as airborne
            // for the provisional launch window (see CharacterEntity.MarkServerLaunchPending) so the launch
            // effects survive until the client's poses confirm the flight or the window expires.
            result = character.IsAirborne || character.IsServerLaunchPending;
        }

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }
}