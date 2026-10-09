using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     <c>customdata::RegisterTimedTriggerCommandDef</c> (250 rows): the timed variant of the ability
///     trigger. It is installed the same way, but it also fires by itself when the effect that carries
///     it ends — a fuse laid by an effect, burning out when the effect runs out. The def is id-only, so
///     the interval cannot come from the row; the carrying effect's own duration is the timer it has,
///     and 108 of the 250 rows sit directly inside an <c>ImpactApplyEffect</c> chain, i.e. inside an
///     applied effect where there is a duration to be had.
/// </summary>
public class RegisterTimedTriggerCommand : AbilityTriggerCommandBase
{
    public RegisterTimedTriggerCommand(RegisterTimedTriggerCommandDef par)
        : base(par)
    {
    }

    protected override bool Timed => true;
}
