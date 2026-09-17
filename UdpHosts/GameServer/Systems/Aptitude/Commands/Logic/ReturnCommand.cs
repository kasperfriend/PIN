using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Logic;

/// <summary>
///     Ends the chain the activation is running - not just the sub-chain the command sits in, but every
///     chain frame up to the ability's (or the effect's) root, the way a <c>return</c> leaves a function
///     from inside nested blocks. The data uses it in two shapes:
///     <list type="bullet">
///         <item>
///             As the last node of a branch that already did the alternative work, e.g. every glider pad
///             and calldown: <c>ConditionalBranch(if AirborneDuration; else ImpactApplyEffect(notification),
///             InstantActivation, Return)</c> followed by <c>ConsumeItem, DeployableSpawn, ...</c>. Without
///             the return the root chain went on to spend the consumable and spawn the pad after telling
///             the player it could not - a grounded pad use ate the item.
///         </item>
///         <item>
///             As the last node of a chain (246 abilities, 36 effect duration chains) where it is a no-op
///             terminator; <c>return_success</c> marks the duration chains that keep their effect alive.
///         </item>
///     </list>
///     The chain frames the return unwinds report success: the branch that reached it ran to completion,
///     and the cooldown an <c>InstantActivation</c> in that branch queued is meant to start. Five NPC
///     chains carry nodes after a mid-chain return (35815, 35925, 37474, 37853: an InflictCooldown or
///     InstantActivation after the return); those are skipped, as the data says. <c>return_halt</c> and
///     <c>return_yield</c> (update-chain loop control) and <c>return_status</c> (3 on one test ability)
///     have no separate server meaning yet and are treated as the plain return.
/// </summary>
public class ReturnCommand : Command, ICommand
{
    private ReturnCommandDef Params;

    public ReturnCommand(ReturnCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        context.ReturnRequested = true;
        Logger.Debug(
            "{Command} {CommandId}: leaving chain {ChainId} of ability {AbilityId} (success {Success}, halt {Halt}, yield {Yield}, status {Status})",
            nameof(ReturnCommand),
            Params.Id,
            context.ChainId,
            context.AbilityId,
            Params.ReturnSuccess,
            Params.ReturnHalt,
            Params.ReturnYield,
            Params.ReturnStatus);
        return true;
    }
}
