using GameServer.Data;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>Adds reputation with a faction to the owner's standing (kept in <c>CharacterUnlocks</c>), boosted by an active reputation boost.</summary>
public class AddFactionReputationCommand : Command, ICommand
{
    private AddFactionReputationCommandDef Params;

    public AddFactionReputationCommand(AddFactionReputationCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.FactionId == 0 || Params.Amount == 0)
        {
            return true;
        }

        var character = PlayerRewards.OwnerOf(context);
        if (character == null)
        {
            return false;
        }

        ulong now = PlayerRewards.UnixNow();
        int amount = (int)(Params.Amount * (1f + character.Unlocks.BoostFraction(CharacterUnlocks.BoostReputation, now)));
        character.Unlocks.AddReputation(Params.FactionId, amount);
        context.ActivationRollbacks.Add(() => character.Unlocks.AddReputation(Params.FactionId, -amount));
        context.DirtyUnlocks.Add(character);
        Logger.Information("{Character} gained {Amount} reputation with faction {Faction}", character, amount, Params.FactionId);
        return true;
    }
}
