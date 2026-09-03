using GameServer.Entities;
using GameServer.Systems.SystemEvents;
using Serilog;

namespace GameServer.Systems.Combat;

public class CombatSim
{
    private static readonly ILogger _logger = Log.ForContext<CombatSim>();

    private readonly Shard _shard; // Time in HitFeedback, EntityManager doesnt have Entities
    private readonly IEventBus _eventBus;
    private readonly EntityManager.EntityManager _entityMan;
    private readonly DamageSystem _damage;
    private readonly HitFeedback _feedback;
    private readonly HostilityResolver _hostility;

    public CombatSim(IEventBus eventBus, EntityManager.EntityManager entityMan, DamageSystem damage, Shard shard, HostilityResolver hostility = null)
    {
        _eventBus = eventBus;
        _entityMan = entityMan;
        _damage = damage;
        _shard = shard;
        _hostility = hostility ?? new HostilityResolver();

        _feedback = new HitFeedback(_shard);

        _eventBus.Subscribe<ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnProjectileHit(ProjectileHitEvent evt)
    {
        // TODO: Damage defense calcs and stuff
        _shard.Entities.TryGetValue(evt.TargetId, out IEntity target);
        _shard.Entities.TryGetValue(evt.SourceId, out IEntity source);

        // Bail out early on an unresolved target/source instead of falling through to
        // DamageSystem/HitFeedback, both of which dereference these without a null check
        // and will throw a NullReferenceException on the shard's tick thread.
        if (target == null)
        {
            _logger.Warning("Dropping ProjectileHitEvent hit because could not get target {targetId}", evt.TargetId);
            return;
        }

        if (source == null)
        {
            _logger.Warning("Dropping ProjectileHitEvent hit because could not get source {targetId}", evt.SourceId);
            return;
        }

        // Faction/hostility check. Only block on an explicit Friendly stance for now: SDB
        // faction-relation coverage may be incomplete for some monster factions, and treating
        // an unresolved Neutral stance as non-damageable would silently break hits that
        // currently work. Watch for "Failed to get relation" warnings from FactionHostility
        // if legitimate hits start getting dropped here.
        var stance = _hostility.GetStance(source.HostilityInfo, target.HostilityInfo);
        if (stance == HostilityStance.Friendly || stance == HostilityStance.Self)
        {
            _logger.Debug("Dropping ProjectileHitEvent hit: {Source} is {Stance} towards {Target}", source, stance, target);
            return;
        }

        var dmg = evt.DamageAmount;
        _damage.ApplyDamage(target, dmg, source);
        _feedback.TookDebugHit(target, source, dmg, evt.HeadShot, evt.Crit);
    }
}