using System;
using System.Collections.Generic;
using AeroMessages.GSS.Character;

namespace GameServer.Systems.Aptitude;

/// <summary>
/// The kind of ability cooldown an <see cref="AbilityCooldownEntry"/> represents.
/// </summary>
public enum AbilityCooldownKind
{
    /// <summary>Cooldown that applies to one specific ability.</summary>
    Local,

    /// <summary>Cooldown that applies to every ability sharing a category.</summary>
    Category,

    /// <summary>Cooldown that applies to every ability of the entity.</summary>
    Global,
}

/// <summary>
/// A single active ability cooldown. Time values are game time in ms as
/// returned by <c>IShard.CurrentTime</c>.
/// </summary>
public class AbilityCooldownEntry
{
    public AbilityCooldownKind Kind { get; set; }

    /// <summary>Ability the cooldown applies to (<c>0</c> for category/global cooldowns).</summary>
    public uint AbilityId { get; set; }

    /// <summary>Cooldown category (<c>0</c> when the ability has no category).</summary>
    public uint Category { get; set; }

    public uint ActivatedTime { get; set; }

    public uint ReadyAgainTime { get; set; }

    /// <summary>True while the entry still prevents activation at <paramref name="time"/>.</summary>
    public bool IsActive(uint time) => ReadyAgainTime > time;

    public ActiveCooldown ToActiveCooldown()
    {
        return new ActiveCooldown
        {
            AbilityId = AbilityId,
            Activated_Time = ActivatedTime,
            ReadyAgain_Time = ReadyAgainTime,
            Unk = new byte[5],
        };
    }
}

/// <summary>
/// A cooldown queued by an activation command. The cooldown is started by
/// <see cref="AbilitySystem"/> only after the whole ability chain has
/// succeeded, so a chain that fails a later requirement (for example not
/// enough energy) does not consume the ability's cooldown.
/// </summary>
public class AbilityCooldownRequest
{
    public AbilityCooldownKind Kind { get; set; }

    /// <summary>Ability the cooldown applies to (<c>0</c> for category/global cooldowns).</summary>
    public uint AbilityId { get; set; }

    /// <summary>Cooldown category (<c>0</c> when the ability has no category).</summary>
    public uint Category { get; set; }

    public uint DurationMs { get; set; }
}

/// <summary>
/// A snapshot of the mutable part of an ability entity's energy state.
/// </summary>
public readonly struct AbilityEnergySnapshot
{
    public AbilityEnergySnapshot(
        float energy,
        uint lastEnergySpendTime,
        uint lastEnergyUpdateTime)
    {
        Energy = energy;
        LastEnergySpendTime = lastEnergySpendTime;
        LastEnergyUpdateTime = lastEnergyUpdateTime;
    }

    public float Energy { get; }
    public uint LastEnergySpendTime { get; }
    public uint LastEnergyUpdateTime { get; }
}

/// <summary>
/// Transaction shared by every context belonging to one root ability
/// activation. Energy commands can mutate more than one entity (for example a
/// target drain), so the transaction snapshots each touched state once and
/// restores all of them when a later command rejects the activation.
/// </summary>
public sealed class AbilityEnergyTransaction
{
    private readonly Dictionary<AbilityState, AbilityEnergySnapshot> _snapshots = [];

    public bool IsActive { get; private set; } = true;

    public void Capture(AbilityState state)
    {
        if (!_snapshots.ContainsKey(state))
        {
            _snapshots.Add(state, state.CaptureEnergy());
        }
    }

    public AbilityEnergyCheckpoint CreateCheckpoint()
    {
        var values = new Dictionary<AbilityState, AbilityEnergySnapshot>();
        foreach (var state in _snapshots.Keys)
        {
            values.Add(state, state.CaptureEnergy());
        }

        return new AbilityEnergyCheckpoint(values, new HashSet<AbilityState>(_snapshots.Keys));
    }

    public void RollbackTo(AbilityEnergyCheckpoint checkpoint)
    {
        foreach (var pair in checkpoint.Values)
        {
            pair.Key.RestoreEnergy(pair.Value);
        }

        // A nested call may be the first activation to touch a state. Restore
        // it to the snapshot it took and stop the outer transaction from
        // treating that failed nested call as a successful spend.
        var nestedStates = new List<AbilityState>();
        foreach (var pair in _snapshots)
        {
            if (!checkpoint.TrackedStates.Contains(pair.Key))
            {
                pair.Key.RestoreEnergy(pair.Value);
                nestedStates.Add(pair.Key);
            }
        }

        foreach (var state in nestedStates)
        {
            _snapshots.Remove(state);
        }
    }

    public void Rollback()
    {
        foreach (var pair in _snapshots)
        {
            pair.Key.RestoreEnergy(pair.Value);
        }

        IsActive = false;
    }

    public void Commit()
    {
        _snapshots.Clear();
        IsActive = false;
    }
}

/// <summary>A savepoint used to isolate a nested ability call within a root transaction.</summary>
public sealed class AbilityEnergyCheckpoint
{
    internal AbilityEnergyCheckpoint(
        Dictionary<AbilityState, AbilityEnergySnapshot> values,
        HashSet<AbilityState> trackedStates)
    {
        Values = values;
        TrackedStates = trackedStates;
    }

    internal Dictionary<AbilityState, AbilityEnergySnapshot> Values { get; }
    internal HashSet<AbilityState> TrackedStates { get; }
}

/// <summary>
/// Server-side tracker of cooldowns and energy for one aptitude entity.
/// <c>TimeCooldown</c>/<c>InflictCooldown</c> aptitude commands report into
/// this structure, while <see cref="AbilitySystem"/> serializes it into the
/// client <c>AbilityActivated</c>/<c>AbilityFailed</c> cooldown payloads.
/// <para>
/// The energy pool mirrors the client-simulated pool described by
/// <c>EnergyParamsData</c>: regen waits <see cref="EnergyRegenDelayMs"/> after
/// the last spend and an overcharged (negative) pool keeps recharging back
/// through zero.
/// </para>
/// </summary>
public class AbilityState
{
    private readonly List<AbilityCooldownEntry> _cooldowns = new();

    /// <summary>Current energy amount. Server-side approximation of the client-simulated pool.</summary>
    public float Energy { get; set; }

    /// <summary>True after the pool has been initialized from the entity's energy parameters.</summary>
    public bool EnergyPoolInitialized { get; set; }

    public float MaxEnergy { get; set; } = 100f;

    /// <summary>Energy restored per second while the entity has a state.</summary>
    public float EnergyRegenPerSecond { get; set; } = 15f;

    /// <summary>Time (ms) after the last spend before the pool starts recharging.</summary>
    public uint EnergyRegenDelayMs { get; set; } = 300;

    /// <summary>Game time (ms) at which the pool was last spent (ability cost/drain).</summary>
    public uint LastEnergySpendTime { get; set; }

    /// <summary>
    /// Game time (ms) at which the last passive energy update was applied, so
    /// regen continues correctly even while an ability is draining.
    /// </summary>
    public uint LastEnergyUpdateTime { get; set; }

    public AbilityCooldownEntry[] Cooldowns => _cooldowns.ToArray();

    /// <summary>Returns a snapshot suitable for an activation transaction.</summary>
    public AbilityEnergySnapshot CaptureEnergy()
    {
        return new AbilityEnergySnapshot(Energy, LastEnergySpendTime, LastEnergyUpdateTime);
    }

    /// <summary>Restores the energy values captured before an activation mutated this state.</summary>
    public void RestoreEnergy(AbilityEnergySnapshot snapshot)
    {
        Energy = snapshot.Energy;
        LastEnergySpendTime = snapshot.LastEnergySpendTime;
        LastEnergyUpdateTime = snapshot.LastEnergyUpdateTime;
    }

    /// <summary>Returns the still-active cooldown that applies to the given ability, or <c>null</c>.</summary>
    public AbilityCooldownEntry GetActiveCooldown(uint abilityId, uint category, uint time)
    {
        AbilityCooldownEntry best = null;
        foreach (var entry in _cooldowns)
        {
            if (!entry.IsActive(time))
            {
                continue;
            }

            bool applies = entry.Kind switch
            {
                AbilityCooldownKind.Local => entry.AbilityId == abilityId,
                AbilityCooldownKind.Category => category != 0 && entry.Category == category,
                AbilityCooldownKind.Global => true,
                _ => false,
            };

            if (!applies)
            {
                continue;
            }

            if (best == null || entry.ReadyAgainTime > best.ReadyAgainTime)
            {
                best = entry;
            }
        }

        return best;
    }

    public bool IsAbilityBlocked(uint abilityId, uint time)
    {
        return GetActiveCooldown(abilityId, 0, time) != null;
    }

    public bool IsCategoryBlocked(uint category, uint time)
    {
        return GetActiveCooldown(0, category, time) != null;
    }

    public bool IsGlobalBlocked(uint time)
    {
        return GetActiveCooldown(0, 0, time) != null;
    }

    /// <summary>
    /// Applies or extends a cooldown and returns the resulting
    /// <see cref="AbilityCooldownEntry"/>. Pass <c>0</c> for fields that do not apply.
    /// </summary>
    public AbilityCooldownEntry StartCooldown(AbilityCooldownKind kind, uint abilityId, uint category, uint durationMs, uint time)
    {
        if (durationMs == 0)
        {
            return null;
        }

        foreach (var entry in _cooldowns)
        {
            bool matches = entry.Kind switch
            {
                AbilityCooldownKind.Local => kind == AbilityCooldownKind.Local && entry.AbilityId == abilityId,
                AbilityCooldownKind.Category => kind == AbilityCooldownKind.Category && entry.Category == category,
                AbilityCooldownKind.Global => kind == AbilityCooldownKind.Global,
                _ => false,
            };

            if (!matches)
            {
                continue;
            }

            uint readyAgain = time + durationMs;
            if (readyAgain > entry.ReadyAgainTime)
            {
                entry.ActivatedTime = time;
                entry.ReadyAgainTime = readyAgain;
            }

            return entry;
        }

        var created = new AbilityCooldownEntry
        {
            Kind = kind,
            AbilityId = abilityId,
            Category = category,
            ActivatedTime = time,
            ReadyAgainTime = time + durationMs,
        };
        _cooldowns.Add(created);
        return created;
    }

    /// <summary>Removes every cooldown that is no longer active at <paramref name="time"/>.</summary>
    public void Prune(uint time)
    {
        for (int i = _cooldowns.Count - 1; i >= 0; i--)
        {
            if (!_cooldowns[i].IsActive(time))
            {
                _cooldowns.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Gets the energy value that would be visible at <paramref name="time"/>
    /// without mutating the state. This is used for atomic multi-target
    /// preflight checks.
    /// </summary>
    public float GetProjectedEnergy(uint time)
    {
        if (time <= LastEnergyUpdateTime || MaxEnergy <= 0f)
        {
            return Energy;
        }

        if (time - LastEnergySpendTime < EnergyRegenDelayMs)
        {
            return Energy;
        }

        float elapsedSec = (time - LastEnergyUpdateTime) / 1000.0f;
        return Math.Min(MaxEnergy, Energy + (EnergyRegenPerSecond * elapsedSec));
    }

    /// <summary>Advances passive energy regeneration to <paramref name="time"/> (ms).</summary>
    public void UpdateEnergy(uint time)
    {
        if (time <= LastEnergyUpdateTime || MaxEnergy <= 0f)
        {
            return;
        }

        float elapsedSec = (time - LastEnergyUpdateTime) / 1000.0f;
        if (elapsedSec > 0f && time - LastEnergySpendTime >= EnergyRegenDelayMs)
        {
            // An overcharged pool may be negative; the client keeps recharging
            // through zero, so the debt is not blocking regen.
            Energy = Math.Min(MaxEnergy, Energy + (EnergyRegenPerSecond * elapsedSec));
        }

        LastEnergyUpdateTime = time;
    }

    /// <summary>
    /// Deducts <paramref name="amount"/> from the energy pool and returns the
    /// remaining energy. When <paramref name="allowOvercharge"/> is set the
    /// pool may go negative (overcharge debt); otherwise it is clamped at zero.
    /// Callers that need an activation gate should use
    /// <see cref="AbilitySystem.TrySpendEnergy"/> instead of calling this
    /// method directly.
    /// </summary>
    public float SpendEnergy(float amount, uint time, bool allowOvercharge)
    {
        if (amount <= 0f)
        {
            return Energy;
        }

        UpdateEnergy(time);
        Energy = allowOvercharge ? Energy - amount : Math.Max(0f, Energy - amount);
        LastEnergySpendTime = time;
        return Energy;
    }
}
