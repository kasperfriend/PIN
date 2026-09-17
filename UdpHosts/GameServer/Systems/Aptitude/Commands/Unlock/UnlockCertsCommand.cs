using GameServer.Data;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Unlock;

/// <summary>Grants a <c>dbitems::Certificate</c>: a recipe, a perk purchase, a boss unlock, a campaign token flag.</summary>
public class UnlockCertsCommand : Command, ICommand
{
    private UnlockCertsCommandDef Params;

    public UnlockCertsCommand(UnlockCertsCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.CertificateId == 0)
        {
            Logger.Warning("{Command} {CommandId} has no unlock id authored yet (item {Item}); nothing unlocked", nameof(UnlockCertsCommand), Params.Id, context.AbilityModuleId);
            return true;
        }

        var character = PlayerRewards.OwnerOf(context);
        if (character == null)
        {
            Logger.Warning("{Command} {CommandId}: no player character to unlock for", nameof(UnlockCertsCommand), Params.Id);
            return false;
        }

        if (!PlayerRewards.ConsumeActivatingItem(context, character))
        {
            return false;
        }

        PlayerRewards.Unlock(context, character, CharacterUnlocks.Certificates, Params.CertificateId);
        return true;
    }
}
