using System;
using AeroMessages.GSS.Character;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Other;

public class AwardRedBeansCommand : Command, ICommand
{
    private AwardRedBeansCommandDef Params;

    public AwardRedBeansCommand(AwardRedBeansCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Wallet awards are session-scoped by design: WalletProp replicates the new balance
        // to the owner, but the character save surface has no wallet row, so nothing here
        // can outlive the session. AwardRedBeans is only reachable from encounter rewards,
        // where a fresh-session grant matches the live-game behavior we can observe.
        var target = context.Self;

        if (target is CharacterEntity { IsPlayerControlled: true } character)
        {
            character.Character_BaseController.WalletProp =
                new WalletData()
                {
                    Beans = character.Character_BaseController.WalletProp.Beans + Params.Amount,
                    Epoch = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
        }
        else
        {
            Logger.Warning("{Command} {CommandId} fails because self is not a character (why is it running on something other than a character?)", nameof(AwardRedBeansCommand), Params.Id);
            return false;
        }

        return true;
    }
}