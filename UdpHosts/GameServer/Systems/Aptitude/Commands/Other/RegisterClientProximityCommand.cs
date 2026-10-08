using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     <c>aptfs::RegisterClientProximityCommandDef</c> (239 rows): registers a proximity trigger that
///     lives as long as the effect carrying this command. Every <c>RetryInterval</c> ms the ability
///     system looks for entities within <c>Radius</c> of the registrant and runs the row's
///     <c>Chain</c> against up to <c>MaxTargets</c> of them - or activates <c>AbilityId</c> on them when
///     the row names an ability instead (79 of the 239 rows carry no chain at all). This is the
///     proximity half of the trigger family, and unlike <c>RegisterAbilityTrigger</c> every column it
///     needs is loadable, so it is implemented rather than documented away.
///     <para>
///         The command's own name says the <em>client</em> detects the proximity, and the AeroMessages
///         tree carries no client→server "something entered my radius" message. The server therefore
///         answers the same question itself by scanning the shard on the row's retry interval; that is
///         an adaptation, and the reason the interval is honoured rather than firing every tick.
///     </para>
///     <para>
///         <c>RadiusRegop</c> is resolved once, here, at registration: the register belongs to the
///         activation that carried the command, and re-reading it on the tick would pick up whatever
///         chain happens to be running then.
///     </para>
///     <para>
///         There is no hostility column, so the scan takes everything in range except the registrant
///         and lets the fired chain filter - which is what those chains do, opening with
///         <c>TargetHostiles</c>/<c>TargetFriendlies</c> or a <c>Require*</c> gate.
///     </para>
/// </summary>
public class RegisterClientProximityCommand : Command, ICommand
{
    private RegisterClientProximityCommandDef Params;

    public RegisterClientProximityCommand(RegisterClientProximityCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.Chain == 0 && Params.AbilityId == 0)
        {
            Logger.Debug(
                "{Command} {CommandId} names neither a chain nor an ability; nothing to fire",
                nameof(RegisterClientProximityCommand), Params.Id);
            return true;
        }

        context.Actives.Add(this, new RegisterClientProximityActiveContext());

        return true;
    }

    public void OnApply(Context context, ICommandActiveContext activeCommandContext)
    {
        if (activeCommandContext is not RegisterClientProximityActiveContext active)
        {
            return;
        }

        if (context.Self == null)
        {
            return;
        }

        float radius = AbilitySystem.RegistryOp(context.Register, Params.Radius, (Operand)Params.RadiusRegop);
        if (radius <= 0f)
        {
            Logger.Debug(
                "{Command} {CommandId} resolves to radius {Radius}; not registering",
                nameof(RegisterClientProximityCommand), Params.Id, radius);
            return;
        }

        active.Registration = context.Abilities.RegisterClientProximity(
            context.Self,
            Params.Chain,
            Params.AbilityId,
            Params.MaxTargets,
            Params.RetryInterval,
            radius);
    }

    public void OnRemove(Context context, ICommandActiveContext activeCommandContext)
    {
        if (activeCommandContext is not RegisterClientProximityActiveContext { Registration: not null } active)
        {
            return;
        }

        context.Abilities.UnregisterClientProximity(active.Registration);
        active.Registration = null;
    }

    private class RegisterClientProximityActiveContext : ICommandActiveContext
    {
        public ClientProximityRegistration Registration;
    }
}
