using System;
using System.Collections.Generic;
using System.Numerics;

namespace GameServer.Systems.Aptitude;

public class Context
{
    public Context(IShard shard, IAptitudeTarget initiator)
    {
        Shard = shard;
        Initiator = initiator;
        Self = initiator;
        Abilities = shard.Abilities;
        Targets = new AptitudeTargets();
        FormerTargets = new AptitudeTargets();
        InitPosition = initiator.Position;
        ExecutionId = Guid.NewGuid();
    }

    public uint ChainId { get; set; }
    public uint AbilityId { get; set; }
    public bool Success { get; set; }
    public IShard Shard { get; set; }
    public AbilitySystem Abilities { get; set; }
    public IAptitudeTarget Self { get; set; }
    public IAptitudeTarget Initiator { get; set; }
    public AptitudeTargets Targets { get; set; }
    public AptitudeTargets FormerTargets { get; set; }
    public Stack<AptitudeTargets> TargetStack { get; set; } = new();
    public float Register { get; set; }
    public float FormerRegister { get; set; }
    public int Bonus { get; set; }
    public uint InitTime { get; set; }
    public Vector3 InitPosition { get; set; }
    public ExecutionHint ExecutionHint { get; set; }
    public Guid ExecutionId { get; set; }

    public Dictionary<ICommand, ICommandActiveContext> Actives { get; set; } = [];

    /// <summary>
    /// Cooldowns queued by activation commands while the chain runs. The
    /// AbilitySystem starts them once the whole chain has succeeded, so a
    /// chain that fails a later requirement (e.g. not enough energy) does not
    /// consume the cooldown.
    /// </summary>
    public List<AbilityCooldownRequest> PendingCooldowns { get; set; } = [];

    public static Context CopyContext(Context original)
    {
        return new Context(original.Shard, original.Initiator)
        {
            ChainId = original.ChainId,
            AbilityId = original.AbilityId,
            Success = original.Success,
            Shard = original.Shard,
            Abilities = original.Abilities,
            Self = original.Self,
            Initiator = original.Initiator,
            Targets = original.Targets,
            FormerTargets = original.FormerTargets,
            Register = original.Register,
            Bonus = original.Bonus,
            InitTime = original.InitTime,
            InitPosition = original.InitPosition,
            ExecutionHint = original.ExecutionHint,
            ExecutionId = original.ExecutionId,
            PendingCooldowns = original.PendingCooldowns,
        };
    }

    /*
    public uint NamedVar;
    public uint Interaction;
    public uint SourceContext;
    public uint SourceEffect;
    */
}