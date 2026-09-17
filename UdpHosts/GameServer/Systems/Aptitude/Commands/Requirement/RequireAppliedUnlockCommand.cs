using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>Whether the owner has applied an unlock (see <c>ApplyUnlockCommand</c>).</summary>
public class RequireAppliedUnlockCommand : Command, ICommand
{
    private RequireAppliedUnlockCommandDef Params;

    public RequireAppliedUnlockCommand(RequireAppliedUnlockCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.UnlockId == 0)
        {
            return true;
        }

        bool result = context.Self is CharacterEntity character && character.Unlocks.Has(CharacterUnlocks.Applied, Params.UnlockId);
        return Params.Negate == 1 ? !result : result;
    }
}
