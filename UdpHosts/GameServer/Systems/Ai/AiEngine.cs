using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using BepuUtilities;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Systems.CharacterLifecycle;
using GameServer.Systems.SystemEvents;
using Serilog;
using AiPrng = GameServer.Systems.PRNG.PRNG;

namespace GameServer.Systems.Ai;

/// <summary>
///     Server side NPC AI. Tracks every spawned mob, gives it a target, walks it around
///     and lets it swing at whatever it can actually reach. Replaces the previous no-op
///     <c>AIEngine</c> stub and is ticked from <c>Shard.Tick</c> alongside the other systems.
/// </summary>
/// <remarks>
///     The decision making lives in <see cref="AiBrain" />, which knows nothing about
///     entities. This class is the boring part: target selection, line of sight, applying
///     movement to the entity and physics body, broadcasting the pose to clients and
///     routing damage through <c>IShard.Damage</c>.
///     <para>
///     Attacks come from the monster's own database weapon row (see <see cref="NpcAttackProfile" />
///     and <see cref="NpcAttackResolver" />): a melee row applies its damage directly, a ranged row
///     fires its <c>dbitems::Ammo</c> row through <see cref="IAiProjectileLauncher" /> exactly like a
///     player shot. A mob the database gives no weapon to keeps the tuned rules melee. The reach and
///     height gates that decide whether anything is actually in range live in <see cref="AiBrain" />.
///     </para>
/// </remarks>
public class AiEngine
{
    /// <summary>How much further than the aggro radius an already engaged NPC will chase.</summary>
    private const float _chaseSlackMultiplier = 1.5f;

    /// <summary>Raise the trace above the feet so the ground probe does not start inside the body.</summary>
    private const float _groundProbeUp = 1.5f;

    /// <summary>
    ///     How far below the feet the ground probe reaches before we give up. Large so the
    ///     mob follows the terrain down ledges and slopes instead of floating off them.
    /// </summary>
    private const float _groundProbeDown = 100f;

    /// <summary>
    ///     Height above the feet used for the wall/obstacle ray. Keeping the ray off the
    ///     ground stops it from grazing the terrain the mob is standing on and reporting a
    ///     false "blocked" every step.
    /// </summary>
    private const float _wallCheckHeight = 1f;

    /// <summary>Chest height, used for both the line of sight trace and the aim direction.</summary>
    private const float _eyeHeight = 1.4f;

    /// <summary>Chest height a shot leaves from when the monster row carries no muzzle offset.</summary>
    private const float _muzzleHeight = 1.62f;

    private static readonly short _movementStateIdle = (short)((ushort)Movestate.Standing << 8);

    /// <summary>
    ///     Moving state an NPC carries while it is fighting: the walk. The two locomotion animations a
    ///     mob has are the database's own two speeds - <c>dbcharacter::Monster.normal_speed</c> (the
    ///     walk, used while closing on a target it is already attacking) and <c>fast_speed</c> (the
    ///     run, used to chase) - see <see cref="AiSpeeds" />.
    /// </summary>
    private static readonly short _movementStateWalking = (short)(((ushort)Movestate.Walking << 8) | (ushort)MovementFlags.Movement);

    /// <summary>Moving state a NPC carries while it runs its target down: the chase speed's animation.</summary>
    private static readonly short _movementStateRunning = (short)(((ushort)Movestate.Running << 8) | (ushort)MovementFlags.Movement);

    private readonly ConcurrentDictionary<ulong, NpcBrain> _brains = new();
    private readonly ILogger _logger;
    private readonly IAiRules _rules;
    private readonly IAiHostility _hostility;
    private readonly IAiAttackFeedback _feedback;
    private readonly IAiMonsterStats _monsterStats;
    private readonly IAiProjectileLauncher _projectiles;
    private readonly IShard _shard;
    private ulong _lastPerceptionAt;
    private ulong _lastMovementAt;

    public AiEngine(
        IShard shard,
        IEventBus eventBus,
        IAiRules rules = null,
        IAiHostility hostility = null,
        IAiAttackFeedback feedback = null,
        IAiMonsterStats monsterStats = null,
        IAiProjectileLauncher projectileLauncher = null)
    {
        _shard = shard ?? throw new ArgumentNullException(nameof(shard));
        _rules = rules ?? new StandardAiRules();
        _hostility = hostility ?? new FactionAiHostility();
        _feedback = feedback ?? new HitFeedbackAttackFeedback(shard);

        // The monster stats resolve weapon profiles against the same rules the engine runs with, so a
        // custom rules object (server tuning or a test fake) also drives the melee fallbacks inside
        // the profile.
        _monsterStats = monsterStats ?? new SdbAiMonsterStats(_rules);
        _projectiles = projectileLauncher ?? new ShardAiProjectileLauncher(shard);
        _logger = shard.Logger?.ForContext<AiEngine>() ?? Log.ForContext<AiEngine>();

        // The bus is injected like every other system's: IShard does not expose it.
        eventBus?.Subscribe<EntityDamagedEvent>(OnEntityDamaged);
        eventBus?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
    }

    /// <summary>Runtime kill switch, toggled by the <c>ai</c> chat/admin command.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Number of NPCs currently being simulated.</summary>
    public int TrackedCount => _brains.Count;

    /// <summary>Behaviour state of a tracked NPC, or null when it is not tracked.</summary>
    public AiBrainState? GetState(ulong entityId)
    {
        return _brains.TryGetValue(entityId, out var npc) ? new AiBrainState?(npc.Brain.State) : null;
    }

    /// <summary>Whether this entity currently has an AI brain.</summary>
    public bool IsTracked(ulong entityId) => _brains.ContainsKey(entityId);

    /// <summary>Snapshot of the entity ids currently being simulated. Mainly for debugging.</summary>
    public IReadOnlyCollection<ulong> GetTrackedEntityIds() => _brains.Keys.ToArray();

    /// <summary>Starts simulating an NPC. Player controlled characters are ignored.</summary>
    public bool Register(CharacterEntity npc)
    {
        if (npc == null || npc.IsPlayerControlled)
        {
            return false;
        }

        var (normalSpeed, fastSpeed) = _monsterStats.GetSpeeds(npc.StaticInfo.CharacterTypeId);
        int dbDamageRating = _monsterStats.GetAttackDamage(npc.StaticInfo.CharacterTypeId, npc.MonsterLevel);
        var attackProfile = _monsterStats.GetAttackProfile(npc.StaticInfo.CharacterTypeId, npc.MonsterLevel) ?? NpcAttackProfile.Unarmed;

        // The weapon row's own damage when it resolves (see NpcAttackDamageMath), otherwise the rating
        // based swing: a fraction of the monster's dbcharacter::MonsterScaling damage rating for the
        // level it was spawned at, with the rules value as the fallback for a monster or level the
        // static database has no row for. See AiAttackDamage.
        int attackDamage = attackProfile.DamagePerRound > 0
            ? attackProfile.DamagePerRound
            : AiAttackDamage.Resolve(dbDamageRating, _rules.AttackDamage, _rules.AttackDamageFraction);

        var brain = new NpcBrain
        {
            EntityId = npc.EntityId,
            Entity = npc,
            Home = npc.Position,
            Brain = new AiBrain(_rules, _shard.CurrentTimeLong, AiCombatTuning.FromProfile(attackProfile)),
            MoveSpeed = AiSpeeds.Resolve(normalSpeed, _rules.DefaultMoveSpeed, _rules),
            ChaseSpeed = AiSpeeds.Resolve(fastSpeed, _rules.DefaultChaseSpeed, _rules),
            AttackDamage = attackDamage,
            Profile = attackProfile,
        };

        return _brains.TryAdd(npc.EntityId, brain);
    }

    /// <summary>Stops simulating an NPC. Safe to call for unknown ids.</summary>
    public bool Unregister(ulong entityId) => _brains.TryRemove(entityId, out _);

    /// <summary>Forces an NPC to engage whoever shot it.</summary>
    public void Aggro(ulong npcEntityId, ulong attackerEntityId)
    {
        if (attackerEntityId == 0 || !_brains.TryGetValue(npcEntityId, out var npc))
        {
            return;
        }

        npc.TargetId = attackerEntityId;
        npc.Brain.Aggro(_shard.CurrentTimeLong);
    }

    /// <summary>Drops every brain. Used when a zone is torn down.</summary>
    public void Clear() => _brains.Clear();

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (_brains.IsEmpty || ct.IsCancellationRequested)
        {
            return;
        }

        if (!Enabled || !_rules.Enabled)
        {
            return;
        }

        if (currentTime < _lastMovementAt + (ulong)_rules.MovementIntervalMs)
        {
            return;
        }

        // deltaTime is the time since the previous shard tick, but we only act once per
        // movement interval, so step by the time actually elapsed since we last moved.
        ulong elapsedMs = _lastMovementAt == 0 ? (ulong)_rules.MovementIntervalMs : currentTime - _lastMovementAt;
        _lastMovementAt = currentTime;

        bool perceive = currentTime >= _lastPerceptionAt + (ulong)_rules.PerceptionIntervalMs;
        if (perceive)
        {
            _lastPerceptionAt = currentTime;
        }

        foreach (var entry in _brains)
        {
            UpdateBrain(entry.Value, elapsedMs, currentTime, perceive);
        }
    }

    private void OnEntityDamaged(EntityDamagedEvent evt)
    {
        if (evt.Target == null || evt.Source == null || evt.Source.EntityId == 0)
        {
            return;
        }

        // Only NPCs are tracked, so a player taking damage is a no-op here.
        Aggro(evt.Target.EntityId, evt.Source.EntityId);
    }

    private void OnCharacterDied(CharacterDiedEvent evt)
    {
        if (evt.Target != null && _brains.TryGetValue(evt.Target.EntityId, out var npc))
        {
            // A burst that was in flight when the NPC died is cancelled, not ended: the client has to
            // drop the attack animation and play the death (the gib visuals and corpse linger come from
            // NpcDeathService, which listens to the same event).
            CancelAttackAnimation(npc);
            npc.Brain.OnDeath();
            npc.TargetId = 0;
        }
    }

    private void UpdateBrain(NpcBrain npc, ulong elapsedMs, ulong currentTime, bool perceive)
    {
        var entity = npc.Entity;
        if (entity == null || entity.IsPlayerControlled ||
            !_shard.Entities.TryGetValue(npc.EntityId, out var registered) ||
            !ReferenceEquals(registered, entity))
        {
            _brains.TryRemove(npc.EntityId, out _);
            return;
        }

        if (npc.Brain.State == AiBrainState.Dead || !entity.IsAlive)
        {
            npc.Brain.OnDeath();
            npc.TargetId = 0;
            return;
        }

        // The attack animation (CombatView burst markers) is a window: close the one that has run out
        // before deciding anything new, so a client sees Fire... then Ended in the same order the DB
        // timing describes.
        EndAttackAnimationIfElapsed(npc, currentTime);

        if (perceive)
        {
            RefreshTarget(npc);
        }

        CharacterEntity target = null;
        bool targetAlive = false;
        if (npc.TargetId != 0 &&
            _shard.Entities.TryGetValue(npc.TargetId, out var targetEntity) &&
            targetEntity is CharacterEntity targetCharacter)
        {
            target = targetCharacter;
            targetAlive = targetCharacter.IsAlive;
        }
        else
        {
            npc.TargetId = 0;
        }

        float distanceToTarget = target != null ? AiVectors.HorizontalDistance(entity.Position, target.Position) : float.MaxValue;

        // Chasing is planned on the flat plane (the mob walks, it does not fly), an attack is
        // measured straight-line: a player on the rock above the mob is 3 m away, not 0.3 m.
        float attackDistance = target != null ? AiVectors.Distance(entity.Position, target.Position) : float.MaxValue;
        float heightDelta = target != null ? AiVectors.HeightDelta(entity.Position, target.Position) : 0f;
        bool visible = targetAlive && HasLineOfSight(entity, target);

        var perception = new AiPerception(
            npc.TargetId,
            targetAlive,
            visible,
            distanceToTarget,
            attackDistance,
            heightDelta,
            AiVectors.HorizontalDistance(entity.Position, npc.Home),
            currentTime);

        var decision = npc.Brain.Decide(perception);

        if (!npc.Brain.WantsTarget)
        {
            npc.TargetId = 0;
        }

        if (decision.Attack && targetAlive)
        {
            ResolveAttack(npc, target);
            StartAttackAnimation(npc, currentTime);
        }

        ApplyDecision(npc, decision, elapsedMs, target);
    }

    private void RefreshTarget(NpcBrain npc)
    {
        if (npc.Brain.State == AiBrainState.Dead)
        {
            return;
        }

        // Keep the current target while it still exists. Re-scanning every perception pass
        // would make a mob ping pong between two players standing side by side.
        if (npc.TargetId != 0 && _shard.Entities.ContainsKey(npc.TargetId))
        {
            return;
        }

        npc.TargetId = 0;

        float radius = npc.Brain.WantsTarget ? _rules.AggroRadius * _chaseSlackMultiplier : _rules.AggroRadius;
        var origin = npc.Entity.Position;

        ulong bestId = 0;
        float bestDistance = float.MaxValue;
        float bestHeightDelta = 0f;

        foreach (var client in _shard.Clients.Values)
        {
            var candidate = client?.CharacterEntity;
            if (candidate == null || !candidate.IsAlive)
            {
                continue;
            }

            if (!_hostility.IsHostile(npc.Entity, candidate))
            {
                continue;
            }

            float distance = AiVectors.HorizontalDistance(origin, candidate.Position);
            if (distance > radius || distance >= bestDistance)
            {
                continue;
            }

            // A player directly above or below the mob is inside a flat radius but outside the
            // volume an NPC can actually fight in - and with no pathfinding the mob would stand
            // under them forever. Zones the designers wanted vertical about (a mob guarding the
            // ramp below a platform) set a bigger band; 0 turns the test off entirely.
            float heightDelta = AiVectors.HeightDelta(origin, candidate.Position);
            if (_rules.MaxAcquisitionHeightDelta > 0f && heightDelta > _rules.MaxAcquisitionHeightDelta)
            {
                continue;
            }

            bestDistance = distance;
            bestHeightDelta = heightDelta;
            bestId = candidate.EntityId;
        }

        if (bestId != 0)
        {
            npc.TargetId = bestId;

            // The height difference belongs in this line: "acquired a target 40 m away that is
            // 8 m above me" is the signature of a mob about to chase something it can never
            // reach, which is exactly what the attack volume gate in the brain refuses to hit.
            _logger.Debug("{Name} acquired target {TargetId} at {Distance:F1}m, {HeightDelta:F1}m of height between them", npc.Entity, bestId, bestDistance, bestHeightDelta);
        }
    }

    private bool HasLineOfSight(CharacterEntity source, CharacterEntity target)
    {
        var physics = _shard.Physics;
        if (physics == null)
        {
            // No collision data loaded, so nothing can occlude. Treat everything as visible.
            return true;
        }

        var from = source.Position + new Vector3(0f, 0f, _eyeHeight);
        var to = target.Position + new Vector3(0f, 0f, _eyeHeight);
        var hit = physics.SegmentRayCast(from, to, source.EntityId);

        return !hit.Hit || hit.HitEntityId == target.EntityId;
    }

    /// <summary>
    ///     Starts the attack animation of one attack: the <c>FireBurst</c> marker every watching client
    ///     plays the weapon's attack animation from, plus the window this engine closes with
    ///     <c>FireEnd</c>. The window is the weapon's own burst timing (see
    ///     <see cref="NpcAttackAnimation.ResolveDurationMs" />) - for a mob with a rifle that is the
    ///     template's <c>ms_per_burst</c> volley, for a melee row its swing.
    /// </summary>
    private void StartAttackAnimation(NpcBrain npc, ulong currentTime)
    {
        var entity = npc.Entity;
        if (entity == null)
        {
            return;
        }

        uint duration = NpcAttackAnimation.ResolveDurationMs(
            npc.Profile?.BurstDurationMs ?? 0,
            (uint)Math.Max(0, npc.Brain.AttackCooldownMs));

        entity.SetFireBurst(unchecked((uint)currentTime));
        npc.Animation = NpcAttackAnimation.Start(currentTime, duration);
    }

    /// <summary>Closes an attack animation whose window has run out, telling clients when it ended.</summary>
    private void EndAttackAnimationIfElapsed(NpcBrain npc, ulong currentTime)
    {
        var animation = npc.Animation;
        if (animation == null || animation.Value.IsActive(currentTime))
        {
            return;
        }

        npc.Animation = null;
        npc.Entity?.SetFireEnd(unchecked((uint)animation.Value.EndTime));
    }

    /// <summary>
    ///     Drops an attack animation that did not run to its end - the NPC died (or was despawned) in
    ///     the middle of it. The marker is the one the client's own <c>FireCancel</c> produces, so the
    ///     animation stops instead of being played out over whatever happens next.
    /// </summary>
    private void CancelAttackAnimation(NpcBrain npc)
    {
        if (npc.Animation == null)
        {
            return;
        }

        npc.Animation = null;
        npc.Entity?.SetFireCancel(_shard.CurrentTime);
    }

    private void ResolveAttack(NpcBrain npc, CharacterEntity target)
    {
        var profile = npc.Profile;
        if (profile != null && profile.IsRanged && profile.Ammo != null)
        {
            FireRangedAttack(npc, target, profile);
            return;
        }

        // Melee (and every row the database gives no weapon to): the damage lands directly.
        int damage = npc.AttackDamage;
        _shard.Damage?.ApplyDamage(target, damage, npc.Entity);
        _feedback?.OnAttack(npc.Entity, target, damage);
    }

    /// <summary>
    ///     Fires one attack's worth of projectiles at the target, using the weapon's own ammo row:
    ///     rounds per burst projectiles, each carrying the per-round damage, muzzle speed and radii the
    ///     database resolved. The projectile simulation owns everything after that (flight, gravity,
    ///     bounces, impact damage and falloff), exactly as it does for a player shot.
    /// </summary>
    private void FireRangedAttack(NpcBrain npc, CharacterEntity target, NpcAttackProfile profile)
    {
        var entity = npc.Entity;
        if (entity == null)
        {
            return;
        }

        var origin = entity.Position + MuzzleOffset(entity, profile);
        var aimPoint = target.Position + new Vector3(0f, 0f, _eyeHeight);
        var direction = aimPoint - origin;

        if (direction.LengthSquared() <= 0.0001f)
        {
            // Target exactly on the muzzle (or a degenerate position): fall back to where the NPC faces.
            direction = entity.AimDirection;
            if (direction.LengthSquared() <= 0.0001f)
            {
                return;
            }
        }

        direction = Vector3.Normalize(direction);

        byte rounds = profile.RoundsPerBurst > 0 ? profile.RoundsPerBurst : (byte)1;
        for (byte round = 0; round < rounds; round++)
        {
            uint trace = AiPrng.Trace(_shard.CurrentTime, round);
            _projectiles.FireRangedAttack(
                entity,
                trace,
                origin,
                direction,
                profile.Ammo,
                profile.Range,
                profile.ProjectileSpeed,
                profile.ImpactRadius,
                profile.MaxRadius,
                npc.AttackDamage);
        }
    }

    /// <summary>
    ///     The muzzle position of a shot: the monster row's own <c>projectile_offset</c> (a local-space
    ///     offset), or the character's chest-height offset when the row does not carry one. The local
    ///     offset is rotated into world space exactly the way <c>CharacterEntity</c> rotates a
    ///     character's own muzzle, so an NPC and a player standing at the same spot fire from the same
    ///     place.
    /// </summary>
    private static Vector3 MuzzleOffset(CharacterEntity entity, NpcAttackProfile profile)
    {
        var offset = profile.MuzzleOffset;
        if (offset.LengthSquared() <= 0.0001f)
        {
            offset = new Vector3(0f, 0f, _muzzleHeight);
        }

        return QuaternionEx.Transform(offset, QuaternionEx.Inverse(entity.Orientation));
    }

    private void ApplyDecision(NpcBrain npc, in AiDecision decision, ulong elapsedMs, CharacterEntity target)
    {
        var entity = npc.Entity;

        Vector3? goal = decision.Movement switch
        {
            AiMovementIntent.TowardTarget => new Vector3?(target?.Position ?? npc.Home),
            AiMovementIntent.TowardHome => new Vector3?(npc.Home),
            _ => null,
        };

        bool moved = false;
        if (goal.HasValue)
        {
            moved = MoveToward(npc, goal.Value, decision.State, elapsedMs);
        }

        if (decision.FaceTarget && target != null)
        {
            var facing = target.Position - entity.Position;
            if (facing.LengthSquared() > 0.0001f)
            {
                entity.SetOrientation(AiVectors.OrientationFacing(facing));

                // Only update the aim when the horizontal projection is
                // non-degenerate.  When the target is directly above or
                // below the NPC the XY vector has near-zero length and
                // Normalize would produce NaN, which later crashes the
                // pose serializer with ArithmeticException.
                var aimFlat = new Vector3(facing.X, facing.Y, 0f);
                if (aimFlat.LengthSquared() > 0.0001f)
                {
                    entity.AimDirection = Vector3.Normalize(aimFlat);
                }
            }
        }

        // Two locomotion animations come straight from the database's two speeds: the walk while the
        // NPC repositions at combat speed (normal_speed, the Attack state's MoveSpeed) and the run
        // while it runs a target down (fast_speed, the Chase/Return speed).
        short movementState = !moved
            ? _movementStateIdle
            : decision.State == AiBrainState.Attack ? _movementStateWalking : _movementStateRunning;
        entity.MovementState = movementState;
        BroadcastPose(entity, movementState);
    }

    private bool MoveToward(NpcBrain npc, Vector3 goal, AiBrainState state, ulong elapsedMs)
    {
        var entity = npc.Entity;

        var delta = goal - entity.Position;
        delta.Z = 0f;

        float horizontal = delta.Length();
        if (horizontal < 0.05f)
        {
            return false;
        }

        var direction = delta / horizontal;
        float speed = state == AiBrainState.Attack ? npc.MoveSpeed : npc.ChaseSpeed;
        float step = speed * (elapsedMs / 1000f);
        if (step > horizontal)
        {
            step = horizontal;
        }

        var candidate = entity.Position + (direction * step);
        if (IsBlocked(entity.Position, candidate, entity.EntityId))
        {
            return false;
        }

        candidate = ProbeGround(entity, candidate);

        entity.SetPosition(candidate);
        entity.SetOrientation(AiVectors.OrientationFacing(direction));
        entity.AimDirection = direction;
        _shard.Physics?.UpdateEntity(entity);

        return true;
    }

    private bool IsBlocked(Vector3 from, Vector3 to, ulong selfEntityId)
    {
        var physics = _shard.Physics;
        if (physics == null)
        {
            return false;
        }

        // Raise the ray to torso height so it does not graze the ground the NPC is
        // standing on (which would read as an obstacle after ground snapping).
        var raised = new Vector3(0f, 0f, _wallCheckHeight);
        var hit = physics.SegmentRayCast(from + raised, to + raised, selfEntityId);
        if (!hit.Hit)
        {
            return false;
        }

        // A graze right at the destination should not stop the NPC dead in its tracks.
        return hit.T < (Vector3.Distance(from, to) - 0.05f);
    }

    private Vector3 ProbeGround(CharacterEntity entity, Vector3 candidate)
    {
        var physics = _shard.Physics;
        if (!_rules.SnapToGround || physics == null)
        {
            return candidate;
        }

        var from = new Vector3(candidate.X, candidate.Y, candidate.Z + _groundProbeUp);
        var to = new Vector3(candidate.X, candidate.Y, candidate.Z - _groundProbeDown);

        // Only static geometry counts as ground; another mob or player standing
        // nearby must not be mistaken for terrain.
        var hit = physics.SegmentRayCast(from, to, entity.EntityId, staticOnly: true);
        if (!hit.Hit)
        {
            return candidate;
        }

        return new Vector3(candidate.X, candidate.Y, hit.HitPosition.Z + _rules.GroundOffset);
    }

    private void BroadcastPose(CharacterEntity entity, short movementState)
    {
        var pose = new AeroMessages.GSS.Character.Event.CurrentPoseUpdate
        {
            Data = new AeroMessages.GSS.CurrentPoseUpdateData
            {
                Flags = 0x00,
                ShortTime = _shard.CurrentShortTime,
                UnkAlwaysPresent = 0x79,
                MovementState = (ushort)movementState,
                Position = entity.Position,
                Rotation = entity.Orientation,
                Aim = entity.AimDirection,
            },
        };

        // Same delivery path MovementRelay uses for player movement: every playing client,
        // regardless of scope. Narrowing this to EntityMan.HasScopedInEntity would cut the
        // packet count on a busy shard, but a player whose scope-in is still queued would see
        // a frozen NPC, so stay on the path that is known to work.
        foreach (var client in _shard.Clients.Values)
        {
            if (client.Status.Equals(IPlayer.PlayerStatus.Playing) &&
                client.NetChannels.TryGetValue(ChannelType.UnreliableGss, out var channel))
            {
                channel.SendMessage(pose, entity.EntityId);
            }
        }
    }

    private sealed class NpcBrain
    {
        public ulong EntityId;
        public CharacterEntity Entity;
        public AiBrain Brain;
        public ulong TargetId;
        public Vector3 Home;
        public float MoveSpeed;
        public float ChaseSpeed;
        public int AttackDamage;

        /// <summary>The weapon this NPC attacks with, as the static database describes it.</summary>
        public NpcAttackProfile Profile;

        /// <summary>The attack animation currently being shown, or null when none is running.</summary>
        public NpcAttackAnimation? Animation;
    }
}
