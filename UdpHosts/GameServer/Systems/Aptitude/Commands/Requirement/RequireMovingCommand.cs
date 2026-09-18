using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

public class RequireMovingCommand : Command, ICommand
{
    private RequireMovingCommandDef Params;

    public RequireMovingCommand(RequireMovingCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // The gate reads "is the character moving faster than Velocitytol" and
        // Negate (98 of 148 rows: the RequireStationary-style chains) inverts it.
        // CheckVelocity=1 + Velocitytol=0 means "any movement at all": the
        // input-driven Movement flag is authoritative there because the physics
        // velocity can lag the input flags by a tick inside effect update chains.
        // Velocitytol>0 compares the physics speed from the last pose update.
        //
        // Both forms used to fail outside Velocitytol==0 (an unimplemented todo
        // covered 110 of the 148 SDB rows), and Velocitytol==0 returned the
        // inverted flag, so every non-negated RequireMoving gate passed while
        // the character stood still and failed while moving.
        bool result = false;

        var target = context.Self;

        if (target is CharacterEntity character)
        {
            if (Params.CheckVelocity == 1)
            {
                result = Params.Velocitytol == 0
                    ? character.MovementStateContainer.Movement
                    : character.Velocity.Length() > Params.Velocitytol;
            }
            else
            {
                // No server-side SDB row sets CheckVelocity=0; fall back to the
                // movement flag so the command keeps working if one ever shows up.
                result = character.MovementStateContainer.Movement;
            }
        }
        else
        {
            Logger.Warning("{Command} {CommandId} fails because target is not a Character. If this is happening, we should investigate why.", nameof(RequireMovingCommand), Params.Id);
        }

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }
}