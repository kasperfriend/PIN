using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Effect;

public class ApplyClientStatusEffectCommand : Command, ICommand
{
    private ApplyClientStatusEffectCommandDef Params;

    public ApplyClientStatusEffectCommand(ApplyClientStatusEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // The status effect netfields set by AddEffect are what the client reacts;
        // any client-environment chains of the effect run client-side from there.
        if (Params.ApplyToSelf == 1)
        {
            return context.Abilities.DoApplyEffect(Params.StatusEffectId, context.Self, context);
        }

        foreach (IAptitudeTarget target in context.Targets)
        {
            if (!context.Abilities.DoApplyEffect(Params.StatusEffectId, target, context))
            {
                return false;
            }
        }

        return true;
    }
}
