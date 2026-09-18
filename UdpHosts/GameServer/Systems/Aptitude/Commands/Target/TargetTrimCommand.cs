using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetTrimCommand : Command, ICommand
{
    private TargetTrimCommandDef Params;

    public TargetTrimCommand(TargetTrimCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Keeps <trimSize> targets on the stack, from ability 30187. Guardian Angel - II ; Protect 2 closest allies
        // Params.Chomp (0 in 298 instances, 1 in 70 instances) stays undecoded: no record of
        // what it toggles survives, and the one live row that sets it (Heavy Turret 39360)
        // behaves correctly with the plain trim below.
        var trimSize = AbilitySystem.RegistryOp(context.Register, Params.Trimsize, (Enums.Operand)Params.TrimsizeRegop);

        if (Params.Former == 1)
        {
            Trim(context.FormerTargets, trimSize);
        }

        if (Params.Current == 1)
        {
            Trim(context.Targets, trimSize);
        }

        return true;
    }

    private void Trim(AptitudeTargets targets, float trimSize)
    {
        // Removing is only meaningful when the list is larger than the trim size.
        // The previous code took Math.Abs on a negative removal count, which wiped the
        // whole target list whenever it was smaller than the trim size (e.g. 39360
        // Heavy Turret asking to keep 1 target with only the single hostile on deck).
        var targetsToRemove = targets.Count - (int)trimSize;
        if (targetsToRemove <= 0)
        {
            return;
        }

        if (Params.FromFront == 1)
        {
            targets.RemoveBottomN(targetsToRemove);
        }
        else
        {
            targets.PopN(targetsToRemove);
        }
    }
}