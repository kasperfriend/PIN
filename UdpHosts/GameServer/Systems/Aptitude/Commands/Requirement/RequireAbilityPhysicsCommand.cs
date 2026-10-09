using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>customdata::RequireAbilityPhysicsCommandDef</c> (51 rows): the gate that pairs with
///     <c>AddPhysicsCommand</c> — it passes while the character has a body installed and fails once it
///     is gone. The chain shapes show it used both ways round: as a gate on an effect that must only run
///     while the body is up (<c>RequireCState → BattleFrameDuration → RequireAbilityPhysics</c>) and as
///     the first thing a chain checks (<c>RequireAbilityPhysics → RequireCState → TimeDuration</c>).
///     <para>
///         The def is id-only, so it cannot name which body it means; it reads the one on the entity the
///         chain is running on, which is the only body <c>AddPhysics</c> can have installed there.
///     </para>
/// </summary>
public class RequireAbilityPhysicsCommand : Command, ICommand
{
    private RequireAbilityPhysicsCommandDef Params;

    public RequireAbilityPhysicsCommand(RequireAbilityPhysicsCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        bool present = context.Self is CharacterEntity character && character.AbilityPhysics != null;

        Logger.Debug("RequireAbilityPhysics {CommandId}: {Result}", Params.Id, present);

        return present;
    }
}
