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
        // The status effect netfields set by AddEffect are what the client reacts to;
        // any client-environment chains of the effect run client-side from there.
        if (Params.ApplyToSelf == 1)
        {
            context.Abilities.DoApplyEffect(Params.StatusEffectId, context.Self, context);
        }
        else
        {
            foreach (IAptitudeTarget target in context.Targets)
            {
                context.Abilities.DoApplyEffect(Params.StatusEffectId, target, context);
            }
        }

        return true;
    }
}