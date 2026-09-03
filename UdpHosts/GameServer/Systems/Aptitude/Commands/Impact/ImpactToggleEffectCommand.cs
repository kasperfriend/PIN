using System.Collections.Generic;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Impact;

public class ImpactToggleEffectCommand : Command, ICommand
{
    private ImpactToggleEffectCommandDef Params;

    public ImpactToggleEffectCommand(ImpactToggleEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // The effect's nested chains share the root activation transaction
        // and retain ability/module identity. Toggling an effect does not pass
        // the caller's target set into the effect chain.
        Context effectContext = Context.CopyContext(context);
        effectContext.Targets = new AptitudeTargets();
        effectContext.FormerTargets = new AptitudeTargets();
        effectContext.TargetStack = new Stack<AptitudeTargets>();

        if (Params.PassRegister == 1)
        {
            effectContext.Register = context.Register;
        }

        if (Params.PassBonus == 1)
        {
            effectContext.Bonus = context.Bonus;
        }

        foreach (IAptitudeTarget target in context.Targets)
        {
            bool targetHasEffect = false;
            foreach (EffectState active in target.GetActiveEffects())
            {
                if (active == null)
                {
                    continue;
                }

                if (active.Effect.Id == Params.EffectId)
                {
                    targetHasEffect = true;
                    effectContext.ExecutionHint = ExecutionHint.RemoveEffect;
                    if (!effectContext.Abilities.DoRemoveEffect(active) && Params.FailOnRemove == 1)
                    {
                        return false;
                    }

                    break;
                }
            }

            if (targetHasEffect)
            {
                continue;
            }

            if (Params.PreApplyChain != 0)
            {
                var chain = effectContext.Abilities.Factory.LoadChain(Params.PreApplyChain);
                if (!chain.Execute(effectContext))
                {
                    return false;
                }
            }

            effectContext.ExecutionHint = ExecutionHint.ApplyEffect;
            if (!effectContext.Abilities.DoApplyEffect(Params.EffectId, target, effectContext))
            {
                return false;
            }
        }

        return true;
    }
}