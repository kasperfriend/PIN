using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>aptgss::RequireTryingToMoveCommandDef</c> (id only): "is the character pushing a
///     movement direction", independent of whether that input translates into motion. This is
///     deliberately the input flag and not <c>RequireMoving</c>'s speed check: it stays true
///     while the character is wedged against a wall or immobilized, which is what chains that
///     break stance on movement intent need.
/// </summary>
public class RequireTryingToMoveCommand : Command, ICommand
{
    private RequireTryingToMoveCommandDef Params;

    public RequireTryingToMoveCommand(RequireTryingToMoveCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var character = CharacterRequirement.Find(context, false);
        if (character == null)
        {
            return true;
        }

        return character.MovementStateContainer.Movement;
    }
}
