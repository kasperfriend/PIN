using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Target;

/// <summary>
///     <c>customdata::TargetByNPCCommandDef</c> (385 rows): narrows the target list to the NPCs in it. The
///     def is id-only, but the chain shapes say plainly what it is — it is a filter that runs after a
///     selector, not a selector itself: <c>TargetClear → TargetPBAE → TargetByNPC →
///     TargetByCharacterState → …</c> in 385 rows, i.e. take everything in the area, then keep only the
///     NPCs among them. Its sibling <c>TargetByCharacterState</c> sits beside it doing exactly that kind
///     of narrowing.
///     <para>
///         This codebase has no separate NPC entity type: monsters are <c>CharacterEntity</c> instances
///         with no controlling player, which is the same test <c>ForcePushCommand</c> uses to skip them.
///     </para>
/// </summary>
public class TargetByNPCCommand : Command, ICommand
{
    private TargetByNPCCommandDef Params;

    public TargetByNPCCommand(TargetByNPCCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var survivors = new AptitudeTargets();

        foreach (var target in context.Targets)
        {
            if (target is CharacterEntity { IsPlayerControlled: false })
            {
                survivors.Push(target);
            }
        }

        int kept = survivors.Count;
        int dropped = context.Targets.Count - kept;

        context.Targets.Clear();
        foreach (var survivor in survivors)
        {
            context.Targets.Push(survivor);
        }

        if (dropped != 0)
        {
            Logger.Debug("TargetByNPC {CommandId}: kept {Kept} NPC(s), dropped {Dropped} non-NPC(s)",
                Params.Id, kept, dropped);
        }

        return true;
    }
}
