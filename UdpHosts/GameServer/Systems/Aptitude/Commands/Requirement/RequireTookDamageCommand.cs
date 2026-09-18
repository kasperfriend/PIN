using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>aptfs::RequireTookDamageCommandDef</c>: "did the character take damage recently". Every
///     post-mitigation damage event reaching the character is stamped by <c>DamageSystem</c> (see
///     <see cref="CharacterEntity.LastDamageTakenTime"/>). With <c>Inittime</c> set the question is
///     "since this chain began" (the same reading <c>RequireReload</c> gives its Inittime flag);
///     otherwise "within the last <c>Timeoffset</c> ms"; a zero Timeoffset means "ever since spawn".
/// </summary>
public class RequireTookDamageCommand : Command, ICommand
{
    private RequireTookDamageCommandDef Params;

    public RequireTookDamageCommand(RequireTookDamageCommandDef par)
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

        if (character.LastDamageTakenTime != 0)
        {
            if (Params.Inittime == 1)
            {
                // The shard stamp is compared against the effect's own shard-time baseline when one
                // exists; on a plain client-initiated activation InitTime is the client clock, which
                // can skew from the shard's by seconds - the same accepted reading RequireReload makes.
                uint baseline = context.EffectApplicationTime ?? context.InitTime;
                result = character.LastDamageTakenTime >= baseline;
            }
            else if (Params.Timeoffset > 0)
            {
                result = unchecked(context.Shard.CurrentTime - character.LastDamageTakenTime) <= Params.Timeoffset;
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
