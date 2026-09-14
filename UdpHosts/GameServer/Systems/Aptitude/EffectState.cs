using System.Collections.Generic;

namespace GameServer.Systems.Aptitude;

public class EffectState
{
    public Effect Effect;
    public Context Context;

    // AddEffect coalesces compatible stacks into one replicated status-effect slot. Each
    // application can still own active command state (temporary permissions, profiles,
    // stat modifiers, registrations), so retain the later contexts and unwind them all
    // when that shared effect state is removed. Otherwise a second stacked glider grant
    // would never receive OnRemove and leave its permission behind indefinitely.
    public List<Context> StackedContexts { get; } = [];
    public byte Index;
    public uint Time;
    public ulong LastUpdateTime;
    public byte Stacks = 1;
    public bool MaxStacksExceeded;

    // A tick iterates a snapshot. A removal chain can remove another effect in that snapshot and reuse its
    // slot; never execute or remove the old state a second time.
    public bool Removed;
}