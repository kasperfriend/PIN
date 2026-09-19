using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>aptfs::RequireInCombatCommandDef</c>: gates on the character being "in combat". The server
///     models combat state as having dealt or taken damage inside
///     <see cref="CharacterEntity.CombatStateWindowMs"/> of now: every post-mitigation damage event
///     stamps the character in <c>DamageSystem.ApplyDamage</c> (with owned-entity credit, so a
///     turret's fire fights for its owner). Fully mitigated or rejected hits do not touch the
///     stamps, which is exactly what "no meaningful combat" should answer. AllowPrediction is a
///     client-side hint and is ignored here.
/// </summary>
public class RequireInCombatCommand : Command, ICommand
{
    private RequireInCombatCommandDef Params;

    public RequireInCombatCommand(RequireInCombatCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // 'In combat' belongs to the character; a chain owned by a deployable asks about the
        // player on the other side of it (see CharacterRequirement). A chain with no character
        // cannot violate a combat gate, so it passes as not applicable.
        var character = CharacterRequirement.Find(context, false);
        if (character == null)
        {
            return true;
        }

        bool result = character.IsInCombatState(context.Shard.CurrentTime);

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }
}
