using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Duration;

/// <summary>
///     <c>aptgss::ReplenishEffectDurationCommandDef</c> (command type 294): sits in an ability chain next
///     to the <c>ImpactApplyEffectCommandDef</c> that applied an effect, and gives the effects that
///     activation applies their length - the value the activation carries in the register.
/// </summary>
/// <remarks>
///     <para>
///     A weapon's charge-up ability reads <em>apply the charge effect, then replenish its duration</em>:
///     the charge effect's duration chain is a <c>ReplenishableDurationCommandDef</c>, whose length comes
///     from the register of the context it runs in. That context only receives the activation's register
///     when the effect's <c>ImpactApplyEffect</c> row says <c>pass_register</c>, and the rows of the
///     build's monster weapons do not - so without this command the charge effect would fall back to the
///     duration command's own default instead of the length the weapon describes.
///     </para>
///     <para>
///     The command runs before the apply in one of the database's chains and after it in the others, so it
///     does both: it records the value on the activation for the effects still to come
///     (<see cref="Context.AppliedEffectDuration" />, consumed by <c>AbilitySystem.DoApplyEffect</c>) and
///     hands it to the effects the activation has already applied.
///     </para>
///     <para>
///     The command's definition table is server-only and absent from <c>clientdb.sd2</c>, so it has no
///     decoded fields; the value it hands over is whatever the activation carries. An activation without
///     one - every player ability, since the client has no register to pass - changes nothing.
///     </para>
/// </remarks>
public class ReplenishEffectDurationCommand : Command, ICommand
{
    private ReplenishEffectDurationCommandDef Params;

    public ReplenishEffectDurationCommand(ReplenishEffectDurationCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        float duration = context.Register;
        if (float.IsNaN(duration) || duration <= 0f)
        {
            return true;
        }

        context.AppliedEffectDuration = duration;

        if (context.AppliedEffects == null)
        {
            return true;
        }

        foreach (var applied in context.AppliedEffects)
        {
            var effectContext = applied?.State?.Context;
            if (effectContext != null)
            {
                effectContext.Register = duration;
            }
        }

        return true;
    }
}
