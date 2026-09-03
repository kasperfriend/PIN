using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Effect;

public class RemoveClientStatusEffectCommand : Command, ICommand
{
    private RemoveClientStatusEffectCommandDef Params;

    public RemoveClientStatusEffectCommand(RemoveClientStatusEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.ApplyToSelf == 1)
        {
            return context.Abilities.DoRemoveEffect(context.Self, Params.StatusEffectId);
        }

        foreach (IAptitudeTarget target in context.Targets)
        {
            if (!context.Abilities.DoRemoveEffect(target, Params.StatusEffectId))
            {
                return false;
            }
        }

        return true;
    }
}