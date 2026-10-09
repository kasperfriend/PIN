using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Projectile;

/// <summary>
///     <c>aptfs::DetonateProjectilesCommandDef</c> (59 rows): blows the caster's in-flight rounds of one
///     ammo type early - the remote explosive, the detonator, the "shoot then trigger" family. Eight
///     stock chassis kits use it. It was an unconditional <c>return true</c>, so a planted charge could
///     never be set off.
///     <para>
///         The rounds are ended through the projectile simulation's own expiry path rather than a
///         separate explosion routine, so the callbacks that fire are exactly the ones a round that ran
///         out of lifetime would have fired - including the ammo's airburst ability, which is where the
///         data puts the explosion. See <see cref="Systems.ProjectileSim.ProjectileSim.Detonate" /> for
///         why that path is reused instead of copied.
///     </para>
///     <para>
///         <c>AmmoTypeId</c> selects which rounds; 0 means every round the caster has in the air. Only
///         the caster's own rounds are touched - the row carries no ownership column, and a detonator
///         that set off other players' charges would be a different ability.
///     </para>
/// </summary>
public class DetonateProjectilesCommand : Command, ICommand
{
    private DetonateProjectilesCommandDef Params;

    public DetonateProjectilesCommand(DetonateProjectilesCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character)
        {
            Logger.Debug("DetonateProjectiles {CommandId}: self is not a character, nothing in flight to detonate", Params.Id);
            return true;
        }

        context.Shard.ProjectileSim?.Detonate(character, Params.AmmoTypeId);

        return true;
    }
}
