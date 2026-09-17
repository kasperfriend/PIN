using GameServer.Data;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Unlock;

/// <summary>
///     A generic content unlock: by default a certificate (the Campaign Token chains run it after their
///     <c>RequireHasCertificate</c> ladder), else whatever <see cref="UnlockContentCommandDef.UnlockType" /> names.
/// </summary>
public class UnlockContentCommand : Command, ICommand
{
    private UnlockContentCommandDef Params;

    public UnlockContentCommand(UnlockContentCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        uint id = Params.UnlockId != 0 ? Params.UnlockId : Params.CertificateId;
        if (id == 0)
        {
            Logger.Warning("{Command} {CommandId} has no unlock id authored yet (item {Item}); nothing unlocked", nameof(UnlockContentCommand), Params.Id, context.AbilityModuleId);
            return true;
        }

        var character = PlayerRewards.OwnerOf(context);
        if (character == null)
        {
            return false;
        }

        if (!PlayerRewards.ConsumeActivatingItem(context, character))
        {
            return false;
        }

        var group = string.IsNullOrEmpty(Params.UnlockType) ? CharacterUnlocks.Certificates : Params.UnlockType;
        PlayerRewards.Unlock(context, character, group, id);
        return true;
    }
}
