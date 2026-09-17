using System;
using System.Collections.Generic;
using System.Numerics;

namespace GameServer.Systems.Aptitude;

public class Context
{
    private readonly SharedActivationState _shared;

    public Context(IShard shard, IAptitudeTarget initiator)
        : this(shard, initiator, new SharedActivationState())
    {
    }

    private Context(IShard shard, IAptitudeTarget initiator, SharedActivationState shared)
    {
        _shared = shared;
        Shard = shard;
        Initiator = initiator;
        ActivationInitiator = initiator;
        Self = initiator;
        Abilities = shard.Abilities;
        Targets = new AptitudeTargets();
        FormerTargets = new AptitudeTargets();
        InitPosition = initiator.Position;
        InitTime = shard.CurrentTime;
        ExecutionId = Guid.NewGuid();
    }

    public uint ChainId { get; set; }
    public uint AbilityId { get; set; }

    /// <summary>
    /// The <c>dbitems::AbilityModule</c> that activated the chain (when
    /// resolved from the loadout slot). Used by
    /// <c>LoadRegisterFromModulePowerCommand</c> to load the ability's power
    /// rating, which scales data amounts like energy cost and damage.
    /// </summary>
    public uint AbilityModuleId { get; set; }

    public bool Success { get; set; }
    public IShard Shard { get; set; }
    public AbilitySystem Abilities { get; set; }
    public IAptitudeTarget Self { get; set; }
    public IAptitudeTarget Initiator { get; set; }

    /// <summary>
    /// The immutable caster identity for the root activation. <see cref="Initiator"/>
    /// can be overridden by effect commands for aptitude semantics, so
    /// activation cooldowns must use this value instead.
    /// </summary>
    public IAptitudeTarget ActivationInitiator { get; set; }

    public AptitudeTargets Targets { get; set; }
    public AptitudeTargets FormerTargets { get; set; }
    public Stack<AptitudeTargets> TargetStack { get; set; } = new();
    public float Register { get; set; } = float.NaN;
    public float FormerRegister { get; set; } = float.NaN;
    public int Bonus { get; set; }
    public uint InitTime { get; set; }

    /// <summary>
    /// Server time at which this effect was applied. Duration checks must not use the root activation's
    /// (possibly client-predicted) InitTime: effects created by a later remove/update chain have a new lifetime.
    /// Null for contexts that are not running an effect.
    /// </summary>
    public uint? EffectStartTime { get; set; }

    /// <summary>
    /// Timestamp to give effects emitted by a duration/update/removal event. Keep this separate from InitTime
    /// so time/reload requirements in the source effect's own chains still refer to its original initiation.
    /// </summary>
    public uint? EffectApplicationTime { get; set; }

    public Vector3 InitPosition { get; set; }
    public ExecutionHint ExecutionHint { get; set; }
    public Guid ExecutionId { get; set; }

    public Dictionary<ICommand, ICommandActiveContext> Actives { get; set; } = [];

    /// <summary>
    ///     Set by <c>ReturnCommand</c>: every chain still running on this context stops after its current
    ///     command, all the way up to the outermost one (a Return inside a ConditionalBranch's else chain
    ///     ends the ability, not just the branch). <see cref="Chain" /> clears it when the outermost chain
    ///     exits. Contexts copied for a called ability or an applied effect start with their own flag, so a
    ///     Return there ends only that ability or effect chain.
    /// </summary>
    public bool ReturnRequested { get; set; }

    /// <summary>How many <see cref="Chain.Execute" /> frames are running on this context right now.</summary>
    public int ChainDepth { get; set; }

    /// <summary>
    ///     Undo steps for state a chain command already changed, run in reverse order when the root
    ///     activation fails (see <c>AbilitySystem.ExecuteAbilityActivation</c>). ConsumeItem uses it to hand
    ///     the consumable back when a later node of the chain - typically InstantActivation on a running
    ///     cooldown - rejects the activation. Shared with called abilities like the pending cooldowns.
    /// </summary>
    public List<Action> ActivationRollbacks { get; set; } = [];

    /// <summary>
    /// Cooldowns queued by activation commands while the chain runs. The
    /// AbilitySystem starts them once the whole chain has succeeded, so a
    /// chain that fails a later requirement (e.g. not enough energy) does not
    /// consume the cooldown.
    /// </summary>
    public List<AbilityCooldownRequest> PendingCooldowns { get; set; } = [];

    /// <summary>
    /// When set, every effect the chain applies is recorded here. The proximity handling uses it to know
    /// whether a previous activation of the same client proximity command is still in effect, so a client
    /// that keeps re-sending the success message while the player stands on the trigger does not re-run the
    /// whole chain (re-applying effects, spawning projectiles and flushing status effect fields to everyone
    /// in range) several times a second.
    /// </summary>
    public List<AppliedEffectRecord> AppliedEffects { get; set; }

    /// <summary>
    ///     Lifetime a <c>ReplenishEffectDurationCommand</c> in this activation hands to the effects the
    ///     activation applies, in the register unit the duration commands read (a value below 1000 is
    ///     seconds). NaN - the default, and every player activation, which has no register of its own -
    ///     leaves the effect's lifetime to its own chains.
    /// </summary>
    public float AppliedEffectDuration { get; set; } = float.NaN;

    /// <summary>
    ///     What the activation has handed the player so far - the items a GrantOwnerItem/SpawnLoot/UnpackItem
    ///     node granted - so a ShowRewardScreen node later in the same chain can list them. Shared with
    ///     copied contexts like the rollbacks, so loot rolled inside an applied effect (the booster packs
    ///     roll from their effect's apply chain) shows on the screen too.
    /// </summary>
    public List<AwardedItem> AwardedItems { get; set; } = [];

    /// <summary>
    ///     The characters whose <see cref="Entities.Character.CharacterEntity.Unlocks" /> a node of this
    ///     activation changed (unlocks, boosts, account groups, reputation). The ability system persists
    ///     their state through GRPC once the root activation has succeeded - not per node, since a later
    ///     node may still fail the chain. Shared with copied contexts like the rollbacks.
    /// </summary>
    public HashSet<IAptitudeTarget> DirtyUnlocks { get; set; } = [];

    /// <summary>
    ///     Whether the consumable this activation came from (<see cref="AbilityModuleId" />) has been
    ///     spent already - by a ConsumeItem node, or by a reward node that spends it itself because its
    ///     chain has none (most unlock kits, boosts and rental contracts are authored that way). Keeps
    ///     a chain with both from charging twice. Shared with copied contexts.
    /// </summary>
    public bool ActivatingItemConsumed { get => _shared.ActivatingItemConsumed; set => _shared.ActivatingItemConsumed = value; }

    public static Context CopyContext(Context original)
    {
        return new Context(original.Shard, original.Initiator, original._shared)
        {
            ChainId = original.ChainId,
            AbilityId = original.AbilityId,
            AbilityModuleId = original.AbilityModuleId,
            Success = original.Success,
            Shard = original.Shard,
            Abilities = original.Abilities,
            Self = original.Self,
            Initiator = original.Initiator,
            ActivationInitiator = original.ActivationInitiator,
            Targets = original.Targets,
            FormerTargets = original.FormerTargets,
            TargetStack = original.TargetStack,
            Register = original.Register,
            FormerRegister = original.FormerRegister,
            Bonus = original.Bonus,
            InitTime = original.InitTime,
            EffectStartTime = original.EffectStartTime,
            EffectApplicationTime = original.EffectApplicationTime,
            InitPosition = original.InitPosition,
            ExecutionHint = original.ExecutionHint,
            ExecutionId = original.ExecutionId,
            PendingCooldowns = original.PendingCooldowns,
            ActivationRollbacks = original.ActivationRollbacks,
            AppliedEffects = original.AppliedEffects,
            AppliedEffectDuration = original.AppliedEffectDuration,
            AwardedItems = original.AwardedItems,
            DirtyUnlocks = original.DirtyUnlocks,
        };
    }

    /*
    public uint NamedVar;
    public uint Interaction;
    public uint SourceContext;
    public uint SourceEffect;
    */
}

/// <summary>Flags one activation shares with every context copied from it.</summary>
internal sealed class SharedActivationState
{
    public bool ActivatingItemConsumed;
}

/// <summary>An item an activation granted, for the reward screen.</summary>
public sealed record AwardedItem(uint SdbId, uint Quantity);
