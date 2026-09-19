using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>aptfs::RequireResourceCommandDef</c>: every target that can carry resources must hold at
///     least <c>Amount</c> of the row's resource (the stackable pool the inventory keeps). The
///     def's <c>Behavior</c> flag is not decoded (no row sample disagrees with the plain gate) and
///     <c>ApplyToArmy</c> is answered by the player's own pool: armies have no pooled storage on
///     this server, so the player's resources ARE the army's resources.
/// </summary>
public class RequireResourceCommand : Command, ICommand
{
    private RequireResourceCommandDef Params;

    public RequireResourceCommand(RequireResourceCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        bool result = false;

        if (Params.Behavior != 0)
        {
            Logger.Debug("{Command} {CommandId}: Behavior {Behavior} is not decoded, gating on quantity only", nameof(RequireResourceCommand), Params.Id, Params.Behavior);
        }

        if (context.Targets.Count > 0)
        {
            result = true;
            foreach (IAptitudeTarget target in context.Targets)
            {
                bool targetResult = target is CharacterEntity character
                                    && character.Player?.Inventory != null
                                    && character.Player.Inventory.GetResourceQuantity(Params.ResourceSdbId) >= Params.Amount;

                if (!targetResult)
                {
                    result = false;
                    break;
                }
            }
        }

        return result;
    }
}
