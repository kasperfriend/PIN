using GameServer.Entities.Carryable;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.Entities.Vehicle;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Other;

public class ApplySinCardCommand : Command, ICommand
{
    private ApplySinCardCommandDef Params;

    public ApplySinCardCommand(ApplySinCardCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // The def carries only the SIN-card type; the generic SinCardField_N netfields
        // (the per-type payload, e.g. position data for a scan waypoint) stay null because
        // no loaded table maps types to field values. Filling them in needs a captured
        // packet from the live game; the type alone is what the client needs to render
        // the SIN marker, so the command stays useful without the payload.
        if (Params.Type == null || Params.Type == 0)
        {
            return true;
        }

        var target = context.Self;

        if (target is CharacterEntity characterEntity)
        {
            characterEntity.Character_ObserverView.SinCardTypeProp = (uint)Params.Type;
        }
        else if (target is DeployableEntity deployableEntity)
        {
            deployableEntity.Deployable_ObserverView.SinCardTypeProp = (uint)Params.Type;
        }
        else if (target is CarryableEntity carryableEntity)
        {
            carryableEntity.CarryableObject_ObserverView.SinCardTypeProp = (uint)Params.Type;
        }
        else if (target is VehicleEntity vehicleEntity)
        {
            vehicleEntity.Vehicle_ObserverView.SinCardTypeProp = (uint)Params.Type;
        }
        else
        {
            Logger.Warning("Can't apply SinCard in {Command} {CommandId}, failing.", nameof(ApplySinCardCommand), Params.Id);
            return false;
        }

        return true;
    }
}