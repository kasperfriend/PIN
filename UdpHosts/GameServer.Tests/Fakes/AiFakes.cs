using System.Collections.Generic;
using System.Numerics;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Ai;

namespace GameServer.Tests.Fakes;

/// <summary>
///     Treats every entity pair as hostile. Lets the AI tests run without a loaded
///     static database, which is what <see cref="FactionAiHostility" /> needs.
/// </summary>
public sealed class AlwaysHostileAiHostility : IAiHostility
{
    public bool IsHostile(IEntity attacker, IEntity target)
    {
        return attacker != null && target != null && attacker.EntityId != target.EntityId;
    }
}

/// <summary>Treats every entity pair as friendly, so nothing ever aggros.</summary>
public sealed class NeverHostileAiHostility : IAiHostility
{
    public bool IsHostile(IEntity attacker, IEntity target) => false;
}

/// <summary>Records the attacks an NPC landed so tests can assert on them.</summary>
public sealed class RecordingAiAttackFeedback : IAiAttackFeedback
{
    public List<(ulong SourceId, ulong TargetId, int Damage)> Attacks { get; } = [];

    public void OnAttack(CharacterEntity source, CharacterEntity target, int damage)
    {
        Attacks.Add((source.EntityId, target.EntityId, damage));
    }
}

/// <summary>Hands out fixed monster stats instead of reading the static database.</summary>
public sealed class FakeAiMonsterStats : IAiMonsterStats
{
    public FakeAiMonsterStats(float normalSpeed = 0f, float fastSpeed = 0f, int attackDamage = 0)
    {
        NormalSpeed = normalSpeed;
        FastSpeed = fastSpeed;
        AttackDamage = attackDamage;
    }

    public float NormalSpeed { get; }

    public float FastSpeed { get; }

    /// <summary>Damage GetAttackDamage returns; 0 simulates a monster/level the DB has no row for.</summary>
    public int AttackDamage { get; }

    /// <summary>Every GetAttackDamage request, as (characterTypeId, level), for assertions.</summary>
    public List<(uint CharacterTypeId, byte Level)> AttackDamageRequests { get; } = [];

    /// <summary>The profile GetAttackProfile hands out. Unarmed (the default) keeps the rating based attack.</summary>
    public NpcAttackProfile AttackProfile { get; set; } = NpcAttackProfile.Unarmed;

    /// <summary>Every GetAttackProfile request, as (characterTypeId, level), for assertions.</summary>
    public List<(uint CharacterTypeId, byte Level)> AttackProfileRequests { get; } = [];

    /// <summary>
    ///     The monster's base <c>behavior</c> string, i.e. the behaviour set it runs while idle. Empty by
    ///     default, which is what 2,902 of the build's 3,109 monster rows carry or come close to.
    /// </summary>
    public string Behavior { get; set; } = string.Empty;

    /// <summary>The monster's <c>behavior_offensive</c> string, i.e. the set it fights in.</summary>
    public string OffensiveBehavior { get; set; } = string.Empty;

    public (float NormalSpeed, float FastSpeed) GetSpeeds(uint characterTypeId) => (NormalSpeed, FastSpeed);

    public (string Base, string Offensive) GetBehaviors(uint characterTypeId) => (Behavior, OffensiveBehavior);

    public int GetAttackDamage(uint characterTypeId, byte level)
    {
        AttackDamageRequests.Add((characterTypeId, level));
        return AttackDamage;
    }

    public NpcAttackProfile GetAttackProfile(uint characterTypeId, byte level)
    {
        AttackProfileRequests.Add((characterTypeId, level));
        return AttackProfile;
    }
}

/// <summary>
///     Records the ranged shots an NPC attack produced, so the engine tests can assert that a ranged
///     mob fires its weapon's ammo at the resolved damage instead of applying damage directly.
/// </summary>
public sealed class RecordingAiProjectileLauncher : IAiProjectileLauncher
{
    public List<RecordedShot> Shots { get; } = [];

    public void FireRangedAttack(
        CharacterEntity source,
        uint trace,
        Vector3 origin,
        Vector3 direction,
        Ammo ammo,
        float range,
        float projectileSpeed,
        float impactRadius,
        float maxRadius,
        int damage)
    {
        Shots.Add(new RecordedShot(
            source.EntityId,
            trace,
            origin,
            direction,
            ammo != null ? ammo.Id : 0,
            range,
            projectileSpeed,
            impactRadius,
            maxRadius,
            damage));
    }

    public readonly record struct RecordedShot(
        ulong SourceId,
        uint Trace,
        Vector3 Origin,
        Vector3 Direction,
        uint AmmoId,
        float Range,
        float ProjectileSpeed,
        float ImpactRadius,
        float MaxRadius,
        int Damage);
}

/// <summary>
///     Records the weapon abilities an NPC runs, and pretends the shard's aptitude system accepted them.
///     The AI's own attack stands down for a chain that delivers its own damage, so the result is what
///     decides whether an NPC an engine has no ability system for still swings.
/// </summary>
public sealed class FakeNpcAbilityActivator : INpcAbilityActivator
{
    /// <summary>Whether <see cref="Activate" /> reports the chain ran, i.e. what an aptitude system says.</summary>
    public bool Result { get; set; } = true;

    /// <summary>Every activation the engine asked for, in order.</summary>
    public List<(CharacterEntity Npc, uint AbilityId, uint Time, float Register)> Activations { get; } = [];

    public bool Activate(CharacterEntity npc, uint abilityId, uint time, float register)
    {
        Activations.Add((npc, abilityId, time, register));
        return Result;
    }
}
