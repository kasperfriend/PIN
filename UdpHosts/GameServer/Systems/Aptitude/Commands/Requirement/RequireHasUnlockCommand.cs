using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     Whether every targeted player has (or, negated, lacks) an unlock: the unlock consumables guard
///     with the negated form so a warpaint or hat cannot be bought twice; the LGV race and Broken
///     Peninsula chains ask for certificates with the plain form.
/// </summary>
public class RequireHasUnlockCommand : Command, ICommand
{
    private RequireHasUnlockCommandDef Params;

    public RequireHasUnlockCommand(RequireHasUnlockCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        bool result = false;
        var targets = context.Targets is { Count: > 0 } ? context.Targets : null;

        if (targets != null)
        {
            result = true;
            foreach (var target in targets)
            {
                if (target is not CharacterEntity character || !character.Unlocks.Has(Params.UnlockType, Params.UnlockId))
                {
                    result = false;
                    break;
                }
            }
        }
        else if (context.Self is CharacterEntity self)
        {
            result = self.Unlocks.Has(Params.UnlockType, Params.UnlockId);
        }

        return Params.Negate == 1 ? !result : result;
    }
}
