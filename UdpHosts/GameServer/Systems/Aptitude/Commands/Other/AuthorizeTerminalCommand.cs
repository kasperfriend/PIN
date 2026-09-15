using System;
using AeroMessages.GSS.Character.Controller;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Other;

public class AuthorizeTerminalCommand : Command, ICommand
{
    private AuthorizeTerminalCommandDef Params;

    public AuthorizeTerminalCommand(AuthorizeTerminalCommandDef par)
: base(par)
    {
        Params = par;
    }

    // self is terminal, target is interacting player
    public bool Execute(Context context)
    {
        var terminal = context.Self;
        if (context.Targets.Count == 0)
        {
            Logger.Warning("{Command} {CommandId} fails because there are no targets (There should be a target?)", nameof(AuthorizeTerminalCommand), Params.Id);
            return false;
        }

        var target = context.Targets.Peek();

        if (target is CharacterEntity character)
        {
            if (!character.IsPlayerControlled)
            {
                Logger.Information("{Command} {CommandId} skips because target is not a player (should this really be happening, why did we target an NPC with this?)", nameof(AuthorizeTerminalCommand), Params.Id);
                return true;
            }

            // The hand-authored table marks unidentified terminals with -1 (see
            // aptgss_AuthorizeTerminalCommandDef.json): authorizing type/id 255 would hand the
            // client a terminal that does not exist, so those rows stay silent.
            if (Params.TerminalType < 0)
            {
                Logger.Debug("{Command} {CommandId} skips because terminal type is unidentified ({TerminalType})", nameof(AuthorizeTerminalCommand), Params.Id, Params.TerminalType);
                return true;
            }

            Logger.Information("{Command} {CommandId} Authorized terminal {TerminalType} id {TerminalId} ({terminal})", nameof(AuthorizeTerminalCommand), Params.Id, Params.TerminalType, Params.TerminalId, terminal);

            character.SetAuthorizedTerminal(new AuthorizedTerminalData
            {
                TerminalType = (byte)Params.TerminalType,
                TerminalId = (uint)Math.Max(Params.TerminalId, 0),
                TerminalEntityId = terminal.AeroEntityId.Backing
            });

            return true;
        }

        Logger.Warning("{Command} {CommandId} fails because target is not a character (why is it running on something other than a character?)", nameof(AuthorizeTerminalCommand), Params.Id);
        return false;
    }
}