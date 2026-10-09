using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     <c>customdata::RegisterAbilityTriggerCommandDef</c> (492 rows): installs an ability trigger on
///     the entity the chain is running on, for the lifetime of the effect that carries it.
///     <c>ActivateAbilityTriggerCommand</c> fires it. See <see cref="AbilityTriggerRegistration" /> for
///     what a trigger does and why, given the def is id-only.
/// </summary>
public class RegisterAbilityTriggerCommand : AbilityTriggerCommandBase
{
    public RegisterAbilityTriggerCommand(RegisterAbilityTriggerCommandDef par)
        : base(par)
    {
    }

    protected override bool Timed => false;
}
