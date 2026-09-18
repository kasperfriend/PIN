using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.Entities.Vehicle;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetByObjectTypeCommand : Command, ICommand
{
    private TargetByObjectTypeCommandDef Params;

    public TargetByObjectTypeCommand(TargetByObjectTypeCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Params.Projectile and Params.Tinyobject have no targets to match: the server
        // has no projectile or tiny-object entity classes (projectiles are simulated,
        // not targetable entities), so rows setting those flags behave as "no match".
        // CarryableEntity is the one live entity class the def has no flag for; no row
        // asks for it.
        var previousTargets = context.Targets;
        var newTargets = new AptitudeTargets();
        foreach (IAptitudeTarget target in previousTargets)
        {
            if (Params.Character == 1 && target is CharacterEntity)
            {
                newTargets.Push(target);
            }
            else if (Params.Deployable == 1 && target is DeployableEntity)
            {
                newTargets.Push(target);
            }
            else if (Params.Vehicle == 1 && target is VehicleEntity)
            {
                newTargets.Push(target);
            }
        }

        context.FormerTargets = previousTargets;
        context.Targets = newTargets;

        if (Params.FailNoTargets == 1 && context.Targets.Count == 0)
        {
            return false;
        }
        else
        {
            return true;
        }
    }
}