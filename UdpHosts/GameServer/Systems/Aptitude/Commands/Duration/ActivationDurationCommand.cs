using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Duration;

/// <summary>
///     <c>aptfs::ActivationDurationCommandDef</c>: keeps an effect alive while a specific ability is
///     being held active on the effect's owner. The interaction flow is its flagship use: the root
///     "interacting" effect 269 lists <c>ActivationDuration(ability_id=187, activated=1)</c> in its
///     duration chain, so the channel ends the moment the player lets go of the E key - the client
///     sends <c>DeactivateAbility</c>, the combat controller tells the ability system the activation
///     ended, and this gate fails on the next duration tick.
/// </summary>
/// <remarks>
///     Active activations are tracked per entity in <see cref="AbilityState.ActiveActivations" />:
///     the character combat controller registers one when a client-initiated activation succeeds and
///     removes it on <c>DeactivateAbility</c> (or when the entity's state is swept). This command
///     reads that set, which is what the old stub's TODO suggested instead of guessing from the
///     chain context.
/// </remarks>
public class ActivationDurationCommand : Command, ICommand
{
    private ActivationDurationCommandDef Params;

    public ActivationDurationCommand(ActivationDurationCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        bool isActive = context.Abilities.IsAbilityActivationActive(context.Self, Params.AbilityId);
        return Params.Activated == 1 ? isActive : !isActive;
    }
}
