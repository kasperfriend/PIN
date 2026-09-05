using System;

namespace GameServer.Systems.Ai;

/// <summary>
///     The decision making half of an NPC. Deliberately free of any shard, entity or
///     physics dependency: it is fed an <see cref="AiPerception" /> snapshot and returns
///     an <see cref="AiDecision" />, which keeps the whole state machine unit testable.
/// </summary>
/// <remarks>
///     Target selection lives in <see cref="AiEngine" />. The brain is told which target
///     to reason about and only owns behaviour state plus the give-up / cooldown timers.
///     After <see cref="Decide" /> the caller should read <see cref="WantsTarget" /> and
///     drop its own target reference when it comes back false.
/// </remarks>
public class AiBrain
{
    private readonly IAiRules _rules;

    public AiBrain(IAiRules rules, ulong now)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        LastTargetSeenAt = now;
        NextAttackAt = now;
    }

    /// <summary>Current behaviour state.</summary>
    public AiBrainState State { get; private set; } = AiBrainState.Idle;

    /// <summary>Server time the target was last seen. Drives the give-up timer.</summary>
    public ulong LastTargetSeenAt { get; private set; }

    /// <summary>Server time at which the next attack is allowed.</summary>
    public ulong NextAttackAt { get; private set; }

    /// <summary>
    ///     Whether the brain still wants to be handed a target. False in Idle, Return and
    ///     Dead - callers use this to know when to clear their own target reference.
    /// </summary>
    public bool WantsTarget => State is AiBrainState.Chase or AiBrainState.Attack;

    /// <summary>
    ///     Makes the NPC engage immediately, bypassing the proximity scan. Used when it is
    ///     shot: being attacked is a far stronger aggro trigger than walking past it.
    /// </summary>
    public void Aggro(ulong now)
    {
        if (State == AiBrainState.Dead)
        {
            return;
        }

        LastTargetSeenAt = now;
        NextAttackAt = now;

        if (State is AiBrainState.Idle or AiBrainState.Return)
        {
            State = AiBrainState.Chase;
        }
    }

    /// <summary>Drops back to idle from a combat state. Idle and Return are left alone.</summary>
    public void OnTargetLost()
    {
        if (State is AiBrainState.Chase or AiBrainState.Attack)
        {
            State = AiBrainState.Idle;
        }
    }

    /// <summary>Moves the brain into its terminal state. Dead NPCs never decide again.</summary>
    public void OnDeath()
    {
        State = AiBrainState.Dead;
    }

    /// <summary>Runs one decision step and returns what the body should do.</summary>
    public AiDecision Decide(in AiPerception perception)
    {
        if (State == AiBrainState.Dead)
        {
            return new AiDecision(AiBrainState.Dead, AiMovementIntent.None, false, false);
        }

        var now = perception.CurrentTime;
        bool engaged = perception.TargetId != 0 && perception.TargetAlive;

        if (engaged && perception.TargetVisible)
        {
            LastTargetSeenAt = now;
        }

        // A target that died, despawned or has been out of sight for too long is dropped.
        // The leash check below sends the NPC home on the following decision.
        // Guarded subtraction: LastTargetSeenAt is stamped from the shard clock and can in
        // principle sit slightly ahead of the time a decision is evaluated with.
        ulong unseenForMs = now > LastTargetSeenAt ? now - LastTargetSeenAt : 0;
        bool targetLost = !engaged || unseenForMs > (ulong)_rules.TargetLostTimeoutMs;
        if (targetLost)
        {
            OnTargetLost();
        }

        // Leash: an NPC dragged too far from where it spawned gives up and walks home.
        if (State != AiBrainState.Return && perception.DistanceToHome > _rules.LeashRadius)
        {
            State = AiBrainState.Return;
        }

        State = State switch
        {
            // Acquiring from Idle needs an actual sighting, not just a live target. Without
            // the visibility check the give-up path above would drop the target and this arm
            // would immediately re-adopt it, so a mob would "forget and rediscover" a player
            // standing behind cover every TargetLostTimeoutMs instead of ever giving up.
            AiBrainState.Idle when engaged && perception.TargetVisible && perception.DistanceToTarget <= _rules.AggroRadius
                => AiBrainState.Chase,
            AiBrainState.Chase when perception.TargetVisible && perception.DistanceToTarget <= _rules.AttackRange
                => AiBrainState.Attack,
            AiBrainState.Attack when !perception.TargetVisible || perception.DistanceToTarget > _rules.AttackRangeExit
                => AiBrainState.Chase,
            AiBrainState.Return when perception.DistanceToHome <= _rules.HomeArrivalRadius
                => AiBrainState.Idle,
            _ => State,
        };

        bool wantsToClose = perception.DistanceToTarget > _rules.StandoffRange;
        var movement = State switch
        {
            AiBrainState.Chase when wantsToClose => AiMovementIntent.TowardTarget,
            AiBrainState.Attack when wantsToClose => AiMovementIntent.TowardTarget,
            AiBrainState.Return => AiMovementIntent.TowardHome,
            _ => AiMovementIntent.None,
        };

        bool faceTarget = engaged && State is AiBrainState.Chase or AiBrainState.Attack;

        bool attack = false;
        if (State == AiBrainState.Attack && engaged && perception.TargetVisible && now >= NextAttackAt)
        {
            NextAttackAt = now + (ulong)_rules.AttackCooldownMs;
            attack = true;
        }

        return new AiDecision(State, movement, faceTarget, attack);
    }
}
