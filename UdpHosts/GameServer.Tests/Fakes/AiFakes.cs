using System.Collections.Generic;
using GameServer.Entities;
using GameServer.Entities.Character;
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

/// <summary>Hands out fixed speeds instead of reading the monster table.</summary>
public sealed class FakeAiMonsterStats : IAiMonsterStats
{
    public FakeAiMonsterStats(float normalSpeed = 0f, float fastSpeed = 0f)
    {
        NormalSpeed = normalSpeed;
        FastSpeed = fastSpeed;
    }

    public float NormalSpeed { get; }

    public float FastSpeed { get; }

    public (float NormalSpeed, float FastSpeed) GetSpeeds(uint characterTypeId) => (NormalSpeed, FastSpeed);
}
