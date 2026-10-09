using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Target;

/// <summary>
///     <c>aptfs::TargetSquadmatesCommandDef</c> (24 rows): replaces the target list with the caster's
///     squadmates - the other members of its squad. Squad-area heals, buffs and revives route through
///     this, and it had no membership to read until <see cref="Systems.Squad.SquadService" /> existed.
///     <para>
///         <c>FailNone</c> (1 in 11 of the 24 rows, 0 in 13) fails the chain when the character has no
///         squadmates, so an ability that only makes sense with a squad present stops there instead of
///         running its remaining nodes against an empty list.
///     </para>
///     <para>
///         <c>Filter</c> is 1 in only 2 of the 24 rows and its predicate is not decodable from an
///         id-only pair of columns, so it is read as the narrowing it plainly is: keep only squadmates
///         that are alive. With the row unset (22 rows) every member is taken. This is a judgement call
///         and the only place in this command where one was needed.
///     </para>
/// </summary>
public class TargetSquadmatesCommand : Command, ICommand
{
    private TargetSquadmatesCommandDef Params;

    public TargetSquadmatesCommand(TargetSquadmatesCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        context.Targets.Clear();

        if (context.Self is not CharacterEntity character)
        {
            Logger.Debug("TargetSquadmates {CommandId}: self is not a character, no squad to draw from", Params.Id);
            return Params.FailNone == 0;
        }

        var squad = context.Shard.Squad;
        if (squad == null)
        {
            // A shard with no roster (the minimal test shards) has no squadmates to offer.
            return Params.FailNone == 0;
        }

        foreach (var mate in squad.GetSquadmates(character.EntityId))
        {
            if (Params.Filter != 0 && !mate.IsAlive)
            {
                continue;
            }

            context.Targets.Push(mate);
        }

        Logger.Debug("TargetSquadmates {CommandId}: {Count} squadmate(s)", Params.Id, context.Targets.Count);

        if (context.Targets.Count == 0)
        {
            return Params.FailNone == 0;
        }

        return true;
    }
}
