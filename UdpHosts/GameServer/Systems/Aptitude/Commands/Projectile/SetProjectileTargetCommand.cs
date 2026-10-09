using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Projectile;

/// <summary>
///     <c>aptfs::SetProjectileTargetCommandDef</c> (52 rows): redirects the caster's in-flight rounds of
///     one ammo type onto the chain's current target - a guided round acquiring a lock mid-flight. It
///     was an unconditional <c>return true</c>.
///     <para>
///         Only rounds that were already homing change course. A ballistic round has no seek behaviour
///         to redirect, so pointing one at a target would make it stop obeying its own simulation mode;
///         the filter lives in <see cref="Systems.ProjectileSim.ProjectileSim.SetTarget" />.
///     </para>
///     <para>
///         <c>SetNpcTarget</c> (37 of the 52 rows) says the target comes from the ability chain rather
///         than from a lock the round already had, which is what makes <c>context.Targets</c> the
///         source; with no target in the chain there is nothing to redirect onto. <c>TargetingThis</c>
///         is 0 in all 52 rows, so its meaning is not derivable from the data and it is not guessed at.
///     </para>
/// </summary>
public class SetProjectileTargetCommand : Command, ICommand
{
    private SetProjectileTargetCommandDef Params;

    public SetProjectileTargetCommand(SetProjectileTargetCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character)
        {
            Logger.Debug("SetProjectileTarget {CommandId}: self is not a character, no rounds to redirect", Params.Id);
            return true;
        }

        if (!context.Targets.TryPeek(out var target))
        {
            Logger.Debug("SetProjectileTarget {CommandId}: the chain carries no target, nothing to redirect onto", Params.Id);
            return true;
        }

        context.Shard.ProjectileSim?.SetTarget(character, Params.ForAmmoType, target.EntityId);

        return true;
    }
}
