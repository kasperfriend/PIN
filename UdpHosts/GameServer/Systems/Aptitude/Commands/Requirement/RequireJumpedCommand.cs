using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>aptfs::RequireJumpedCommandDef</c>: "did the character jump recently". The movement relay
///     stamps <see cref="CharacterEntity.LastJumpTime"/> (shard clock) every time the client's
///     jump counter resets - which by client design includes server-commanded launches, so glider
///     pad launches read as jumps here exactly like the client reads them. With <c>Inittime</c> the
///     question is "since this chain began", otherwise "within the last <c>Timeoffset</c> ms"; a
///     zero Timeoffset means any jump since spawn. Only player-driven characters stamp jumps, so
///     for an NPC the gate is answered with "no jump recorded".
/// </summary>
public class RequireJumpedCommand : Command, ICommand
{
    private RequireJumpedCommandDef Params;

    public RequireJumpedCommand(RequireJumpedCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        bool result = false;

        var character = CharacterRequirement.Find(context, false);
        if (character == null)
        {
            return true;
        }

        if (character.LastJumpTime != 0)
        {
            if (Params.Inittime == 1)
            {
                uint baseline = context.EffectApplicationTime ?? context.InitTime;
                result = character.LastJumpTime >= baseline;
            }
            else if (Params.Timeoffset > 0)
            {
                result = unchecked(context.Shard.CurrentTime - character.LastJumpTime) <= Params.Timeoffset;
            }
            else
            {
                result = true;
            }
        }

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }
}
