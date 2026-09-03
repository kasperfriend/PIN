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
/// Server-side tracker of cooldowns and energy for one aptitude entity.
/// <c>TimeCooldown</c>/<c>InflictCooldown</c> aptitude commands report into
/// this structure, while <see cref="AbilitySystem"/> serializes it into the
/// client <c>AbilityActivated</c>/<c>AbilityFailed</c> cooldown payloads.
/// </summary>
public class AbilityState
{
    private readonly List<AbilityCooldownEntry> _cooldowns = new();

    /// <summary>Current energy amount. Server-side approximation of the client-simulated pool.</summary>
    public float Energy { get; set; }

    public float MaxEnergy { get; set; } = 100f;

    /// <summary>Energy restored per second while the entity has a state.</summary>
    public float EnergyRegenPerSecond { get; set; } = 15f;

    /// <summary>
    /// Game time (ms) at which the last passive energy update was applied, so
    /// regen continues correctly even while an ability is draining.
    /// </summary>
    public uint LastEnergyUpdateTime { get; set; }

    public AbilityCooldownEntry[] Cooldowns => _cooldowns.ToArray();

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

    /// <summary>Advances passive energy regeneration to <paramref name="time"/> (ms).</summary>
    public void UpdateEnergy(uint time)
    {
        if (time <= LastEnergyUpdateTime || MaxEnergy <= 0f)
        {
            return;
        }

        float elapsedSec = (time - LastEnergyUpdateTime) / 1000.0f;
        if (elapsedSec > 0f)
        {
            Energy = Math.Min(MaxEnergy, Energy + (EnergyRegenPerSecond * elapsedSec));
        }

        LastEnergyUpdateTime = time;
    }
}
