using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>aptfs::RequireResourceFromTargetCommandDef</c>: the resource check of
///     <c>RequireResourceCommand</c>, but named "from target", so it is read against the chain's
///     target (the pool the chain wants to draw from) before falling back to the character the
///     chain runs for. Only characters own resource pools; a target that is not a character cannot
///     satisfy the row.
/// </summary>
public class RequireResourceFromTargetCommand : Command, ICommand
{
    private RequireResourceFromTargetCommandDef Params;

    public RequireResourceFromTargetCommand(RequireResourceFromTargetCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.Behavior != 0)
        {
            Logger.Debug("{Command} {CommandId}: Behavior {Behavior} is not decoded, gating on quantity only", nameof(RequireResourceFromTargetCommand), Params.Id, Params.Behavior);
        }

        if (context.Targets.Count > 0)
        {
            foreach (IAptitudeTarget target in context.Targets)
            {
                if (target is not CharacterEntity character || !HasEnough(character))
                {
                    return false;
                }
            }

            return true;
        }

        var poolOwner = context.Self as CharacterEntity;
        return poolOwner != null && HasEnough(poolOwner);
    }

    private bool HasEnough(CharacterEntity character)
    {
        return character.Player?.Inventory != null
            && character.Player.Inventory.GetResourceQuantity(Params.ResourceSdbId) >= Params.Amount;
    }
}
