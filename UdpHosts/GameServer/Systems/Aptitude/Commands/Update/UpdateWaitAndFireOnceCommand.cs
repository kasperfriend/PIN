using System.Runtime.CompilerServices;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Update;

/// <summary>
///     The delayed one-shot of an effect's update loop: an effect that means "wait, then do the thing once"
///     carries this command in its <c>update_chain</c>, the wait in <c>duration</c> and the thing in
///     <c>chain</c>.
/// </summary>
/// <remarks>
///     <para>
///     Every row of the type in build prod-1962 belongs to an effect update loop: of the 940 rows, 939 sit
///     somewhere in a <c>StatusEffectData.update_chain</c> (the four that a chain only reaches through a
///     branch are in there too) and the last one is not referenced by anything at all. Not one sits in an
///     ability activation chain, so the wait is measured from the effect's own start
///     (<see cref="Context.EffectStartTime" />) rather than from a caller-provided timestamp, and the
///     carried chain fires from an update tick of an effect whose duration chain has not expired yet. The
///     rows bear that out: effect 4313 waits 2000 ms inside a 2200 ms effect, 4561 waits 3000 ms of 4000 ms,
///     6940 waits 5000 ms of 8000 ms.
///     </para>
///     <para>
///     The most consequential instances are NPC attack stages whose projectile, damage and force push live
///     behind the wait - for example monster 898's ability 35803: effect 4313 applies the wind-up animation
///     and then, after 2000 ms, fires <c>FireProjectileCommandDef</c> 673700 at the target. While this
///     command was a no-op the projectile was never fired by the server, so the attack showed its animation
///     and did nothing.
///     </para>
///     <para>
///     The fired flag is kept with the effect's context, so every application fires its own chain exactly
///     once - a second application of the same effect (even on the same target) waits and fires again, and
///     the flag disappears with the effect state that owns the context.
///     </para>
/// </remarks>
public class UpdateWaitAndFireOnceCommand : Command, ICommand
{
    private static readonly ConditionalWeakTable<Context, FiredOnce> _fired = new();

    private UpdateWaitAndFireOnceCommandDef Params;

    public UpdateWaitAndFireOnceCommand(UpdateWaitAndFireOnceCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.Chain == 0)
        {
            // "Wait and fire nothing": a row without a chain cannot do anything, but it must not fail the
            // update loop it belongs to.
            return true;
        }

        var state = _fired.GetValue(context, _ => new FiredOnce());
        if (state.Fired)
        {
            return true;
        }

        var waitMs = AbilitySystem.RegistryOp(context.Register, Params.Duration, (Enums.Operand)Params.Regop);

        // The wait is measured from the effect's own start, and "up" is inclusive because the database
        // authors waits that end exactly on an update tick: effect 4313 waits 2000 ms inside a 2200 ms
        // effect, and effect 10162 waits its whole 20000 ms lifetime, so the update that is due then has to
        // fire (the duration chain is evaluated first and keeps the effect alive at `elapsed == duration`,
        // which is what an effect update loop that owes an update at the end of its lifetime needs).
        // Signed modular subtraction also keeps a client clock lead from looking 49 days old
        // (see TimeDurationCommand).
        uint now = context.Shard.CurrentTime;
        uint baseTime = context.EffectStartTime ?? context.InitTime;
        var elapsed = unchecked((int)(now - baseTime));
        if (elapsed < waitMs)
        {
            return true;
        }

        // Only one try, whether or not the chain succeeds: the command is a one-shot, not a retry loop.
        state.Fired = true;

        var chain = context.Abilities.Factory.LoadChain(Params.Chain);
        return chain?.Execute(context) ?? true;
    }

    /// <summary>Per-application bookkeeping: whether the wait of this command has already fired.</summary>
    private sealed class FiredOnce
    {
        public bool Fired { get; set; }
    }
}
