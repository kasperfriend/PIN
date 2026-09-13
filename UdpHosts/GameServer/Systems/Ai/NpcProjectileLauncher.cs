using System;
using System.Numerics;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.dbitems;

namespace GameServer.Systems.Ai;

/// <summary>
///     The ranged half of an NPC attack: how the AI hands a resolved shot to the projectile system.
///     An interface because <c>ProjectileSim</c> is built against the concrete shard, so the AI tests
///     (which run on a fake shard with no projectile system) record shots through a fake instead.
/// </summary>
public interface IAiProjectileLauncher
{
    /// <summary>Fires one round of an NPC's ranged attack. <paramref name="damage" /> is the per-round damage the AI already resolved.</summary>
    void FireRangedAttack(
        CharacterEntity source,
        uint trace,
        Vector3 origin,
        Vector3 direction,
        Ammo ammo,
        float range,
        float projectileSpeed,
        float impactRadius,
        float maxRadius,
        int damage);
}

/// <summary>The production launcher: the shard's <c>ProjectileSim</c>, when the shard has one.</summary>
public sealed class ShardAiProjectileLauncher : IAiProjectileLauncher
{
    private readonly IShard _shard;

    public ShardAiProjectileLauncher(IShard shard)
    {
        _shard = shard ?? throw new ArgumentNullException(nameof(shard));
    }

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
        _shard.ProjectileSim?.FireProjectile(source, trace, origin, direction, ammo, range, projectileSpeed, impactRadius, maxRadius, damage);
    }
}
