using GameServer.Entities;

namespace GameServer.Systems.Combat;

public readonly record struct ProjectileHitEvent(
    ulong TargetId,
    int DamageAmount,
    ulong SourceId,
    bool HeadShot,
    bool Crit,
    float DamageMod = -1f,
    byte DamageType = 0);

public readonly record struct ImpactHitEvent(
    IEntity Target,
    int HealAmount,
    IEntity Source);