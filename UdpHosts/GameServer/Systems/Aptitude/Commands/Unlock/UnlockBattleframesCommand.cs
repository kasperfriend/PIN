using System.Linq;
using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Unlock;

/// <summary>
///     Unlocks a battleframe: the character gets a garage loadout for the chassis with its stock
///     char-create gear (the same generation character creation uses), and the chassis is recorded
///     in the <c>battleframes</c> unlock group. A frame the character already owns is left alone.
/// </summary>
public class UnlockBattleframesCommand : Command, ICommand
{
    private UnlockBattleframesCommandDef Params;

    public UnlockBattleframesCommand(UnlockBattleframesCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.SdbId == 0)
        {
            Logger.Warning("{Command} {CommandId} has no battleframe authored yet (item {Item}); nothing unlocked", nameof(UnlockBattleframesCommand), Params.Id, context.AbilityModuleId);
            return true;
        }

        var character = PlayerRewards.OwnerOf(context);
        var inventory = character?.Player?.Inventory;
        if (character == null || inventory == null)
        {
            return false;
        }

        if (!PlayerRewards.ConsumeActivatingItem(context, character))
        {
            return false;
        }

        if (SDBInterface.GetBattleframe(Params.SdbId) == null)
        {
            Logger.Warning("{Command} {CommandId}: {SdbId} is not a dbitems::Battleframe", nameof(UnlockBattleframesCommand), Params.Id, Params.SdbId);
            return false;
        }

        if (inventory.GetLoadoutIdForChassis(Params.SdbId) == 0)
        {
            var createId = HardcodedCharacterData.TempCharCreateLoadouts.FirstOrDefault(pair => pair.Value == Params.SdbId).Key;
            if (createId != 0)
            {
                HardcodedCharacterData.GenerateCharCreateLoadoutAndItems(inventory, createId, Params.SdbId);
            }
            else
            {
                HardcodedCharacterData.GenerateStartingLoadout(inventory, Params.SdbId);
            }

            if (inventory.GetLoadoutIdForChassis(Params.SdbId) == 0)
            {
                Logger.Warning("{Command} {CommandId}: could not build a loadout for battleframe {SdbId}", nameof(UnlockBattleframesCommand), Params.Id, Params.SdbId);
                return false;
            }

            // The garage only learns about a new loadout from a full inventory update.
            if (inventory.EnablePartialUpdates)
            {
                inventory.SendFullInventory();
            }

            Logger.Information("{Character} unlocked battleframe {Battleframe}", character, PlayerRewards.ItemName(Params.SdbId));
        }

        PlayerRewards.Unlock(context, character, CharacterUnlocks.Battleframes, Params.SdbId);
        return true;
    }
}
