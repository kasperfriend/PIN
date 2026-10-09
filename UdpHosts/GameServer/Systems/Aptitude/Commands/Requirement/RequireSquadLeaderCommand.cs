using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>aptfs::RequireSquadLeaderCommandDef</c> (4 rows, <c>fail_none</c> set in all of them,
///     <c>negate</c> unset in all): the gate that lets only a squad leader run what follows. All four
///     rows ask the same question, so this reads the initiator - the character whose activation is
///     running - rather than the target list, which is what a requirement gate on the caster means.
/// </summary>
public class RequireSquadLeaderCommand : Command, ICommand
{
    private RequireSquadLeaderCommandDef Params;

    public RequireSquadLeaderCommand(RequireSquadLeaderCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self == null)
        {
            return Params.Negate != 0;
        }

        var squad = context.Shard.Squad;
        bool isLeader = squad != null && squad.IsLeader(context.Self.EntityId);

        if (Params.Negate != 0)
        {
            isLeader = !isLeader;
        }

        Logger.Debug("RequireSquadLeader {CommandId}: {Result}", Params.Id, isLeader);

        return isLeader;
    }
}
