using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using BepuUtilities;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Entities.Turret;
using GameServer.Enums;
using GameServer.StaticDB;
using GameServer.Systems.CharacterLifecycle;
using GameServer.Systems.Combat;
using GameServer.Systems.Emotes;
using GameServer.Systems.SystemEvents;
using Serilog;
using AiPrng = GameServer.Systems.PRNG.PRNG;
using CharacterCombatFlags = AeroMessages.GSS.Character.CombatFlagsData.CharacterCombatFlags;

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

    /// <summary>Chest height, used for both the line of sight trace and the aim direction.</summary>
    private const float _eyeHeight = 1.4f;

    /// <summary>Chest height a shot leaves from when the monster row carries no muzzle offset.</summary>
    private const float _muzzleHeight = 1.62f;

    /// <summary>Fallback radius when a monster row does not provide its body radius.</summary>
    private const float _navigationAgentRadius = 0.7f;

    /// <summary>Fallback body height when a monster row does not provide its body height.</summary>
    private const float _navigationAgentHeight = 1.8f;

    /// <summary>Do not rebuild a path for every 20 Hz movement update.</summary>
    private const ulong _navigationReplanIntervalMs = 750;

    /// <summary>Replan when a moving target has made the current endpoint stale.</summary>
    private const float _navigationGoalRefreshDistance = 2.5f;

    /// <summary>
    ///     How often a moving NPC's wall clearance probe may run. The probe is six ray casts
    ///     against the zone's static geometry; at the 50 ms movement cadence it was most of
    ///     what a walking NPC cost on a populated zone. Probing every 100 ms and spanning the
    ///     whole gap since the last probe (see <see cref="NpcNavigationAgent.WallProbeOrigin" />)
    ///     catches the same wall, because at chase speed one 100 ms gap is under a metre of
    ///     movement — shorter than the probe's own lateral offsets.
    /// </summary>
    private const ulong WallProbeIntervalMs = 100;

    private static readonly NpcPathfinder.Options _navigationOptions = new(
        CellSize: 2f,
        MaxStepHeight: 1.25f,
        MaxSearchDistance: 64f,
        MaxExpandedNodes: 4096,
        WaypointTolerance: 0.35f);

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

    /// <summary>State used while a database movement slide owns the NPC's displacement.</summary>
    private static readonly short _movementStateSliding = (short)(((ushort)Movestate.Sliding << 8) | (ushort)MovementFlags.Movement);

    private readonly ConcurrentDictionary<ulong, NpcBrain> _brains = new();
    private readonly ILogger _logger;
    private readonly IAiRules _rules;
    private readonly IAiHostility _hostility;
    private readonly IAiAttackFeedback _feedback;
    private readonly IAiMonsterStats _monsterStats;
    private readonly IAiProjectileLauncher _projectiles;
    private readonly INpcAbilityActivator _abilityActivator;
    private readonly EmoteService _emotes;
    private readonly IShard _shard;
    private readonly INpcNavigation _navigation;
    private readonly INpcActivityWorld _activities;
    private readonly NpcRoutineRules _routineRules;
    private int _routinePathQueriesLeft;
    private const int RoutinePathQueriesPerTick = 4;
    private ulong _lastPerceptionAt;
    private ulong _lastMovementAt;

    public AiEngine(
        IShard shard,
        IEventBus eventBus,
        IAiRules rules = null,
        IAiHostility hostility = null,
        IAiAttackFeedback feedback = null,
        IAiMonsterStats monsterStats = null,
        IAiProjectileLauncher projectileLauncher = null,
        INpcAbilityActivator abilityActivator = null,
        EmoteService emotes = null,
        TurretWeaponFire turretFire = null,
        INpcNavigation navigation = null,
        INpcActivityWorld activities = null,
        NpcRoutineRules routineRules = null)
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

        // Weapon abilities run through the shard's own aptitude system: it is the only thing that can
        // apply a status effect, and the animation of an attack lives in the effect's chains. The activator
        // looks the system up when it is used rather than here, because a Shard builds its AI before its
        // AbilitySystem; a shard without one (a test shard) then simply reports that nothing ran, and the
        // AI's own attack stays the mob's attack.
        _abilityActivator = abilityActivator ?? new ShardAbilityActivator(_shard);

        // The idle emote of a monster row's behaviour string (behavior=AlertAndInteractive(emote="calm"))
        // is performed through the same emote table the players' emotes come from, so an NPC's pose is a
        // row lookup and a replicated EmoteData, not a private animation path.
        _emotes = emotes ?? new EmoteService(new SdbEmoteDataSource(), new AbilitySystemEmoteEffectApplier());
        _logger = shard.Logger?.ForContext<AiEngine>() ?? Log.ForContext<AiEngine>();
        _routineRules = routineRules ?? new NpcRoutineRules();
        _navigation = navigation ?? new PhysicsNpcNavigation(shard, _rules);
        _activities = activities ?? new SdbNpcActivityWorld(shard, _emotes, _routineRules);

        // Unmanned turrets share the engine's hostility and the injected projectile launcher (so a
        // test that records NPC shots also records turret shots). Production looks the turret's
        // weapons up from the static database; a test injects TurretWeaponFire instead.
        Turrets = new TurretAi(
            shard,
            _hostility,
            turretFire ?? new TurretWeaponFire(
                SDBInterface.GetTurretWeapons,
                new NpcAttackResolver(new SdbNpcAttackDataSource()),
                _projectiles,
                SDBInterface.GetHardpointOffset));

        // The bus is injected like every other system's: IShard does not expose it.
        eventBus?.Subscribe<EntityDamagedEvent>(OnEntityDamaged);
        eventBus?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
    }

    /// <summary>Unmanned turret fire, ticked with the rest of the engine.</summary>
    public TurretAi Turrets { get; }

    /// <summary>Runtime kill switch, toggled by the <c>ai</c> chat/admin command.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Number of NPCs currently being simulated.</summary>
    public int TrackedCount => _brains.Count;

    /// <summary>Behaviour state of a tracked NPC, or null when it is not tracked.</summary>
    public AiBrainState? GetState(ulong entityId)
    {
        return _brains.TryGetValue(entityId, out var npc) ? new AiBrainState?(npc.Brain.State) : null;
    }

    /// <summary>Ambient activity, distinct from the combat brain's Idle state.</summary>
    public NpcRoutineState? GetRoutineState(ulong entityId)
        => _brains.TryGetValue(entityId, out var npc) ? npc.Routine.State : null;

    /// <summary>The data-backed routine (including missing-route diagnostics) of a tracked NPC.</summary>
    public NpcRoutineProfile GetRoutineProfile(ulong entityId)
        => _brains.TryGetValue(entityId, out var npc) ? npc.Routine.Profile : null;

    /// <summary>Small operator-facing census; it does not present unknown trees as running routines.</summary>
    public string DescribeRoutines()
    {
        var snapshot = _brains.Values.ToArray();
        string states = string.Join(", ", snapshot.GroupBy(npc => npc.Routine.State)
            .OrderBy(group => group.Key).Select(group => $"{group.Key}={group.Count()}"));
        int unspecified = snapshot.Count(npc => npc.Routine.Profile.Kind == NpcRoutineKind.Unspecified);
        return $"NPC routines | ground={(_navigation.SupportsRoutines ? "loaded" : "missing (ambient travel disabled)")} | " +
            $"{(states.Length > 0 ? states : "no NPCs")} | unspecified ambient definitions={unspecified}";
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
        var (bodyRadius, bodyHeight) = _monsterStats.GetBodyDimensions(npc.StaticInfo.CharacterTypeId);
        var (baseBehavior, offensiveBehavior) = _monsterStats.GetBehaviors(npc.StaticInfo.CharacterTypeId);
        var baseParams = NpcBehaviorParams.Parse(baseBehavior);
        if (baseParams.Name.Equals("Null", StringComparison.OrdinalIgnoreCase))
        {
            // This CAIS invocation explicitly requests no AI, not a generic melee brain.
            return false;
        }

        var offensiveParams = NpcBehaviorParams.Parse(offensiveBehavior);
        var routineProfile = NpcRoutineProfile.Resolve(baseParams, offensiveParams, _routineRules, _rules.LeashRadius);
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
            Brain = new AiBrain(_rules, _shard.CurrentTimeLong, AiCombatTuning.FromProfile(attackProfile), routineProfile.LeashDistance),
            Routine = new NpcRoutine(npc.EntityId, npc.StaticInfo.CharacterTypeId, npc.Position,
                routineProfile, _shard.CurrentTimeLong, _activities),
            MoveSpeed = AiSpeeds.Resolve(normalSpeed, _rules.DefaultMoveSpeed, _rules),
            ChaseSpeed = AiSpeeds.Resolve(fastSpeed, _rules.DefaultChaseSpeed, _rules),
            NavigationRadius = float.IsFinite(bodyRadius) && bodyRadius > 0f
                ? bodyRadius
                : _navigationAgentRadius,
            NavigationHeight = float.IsFinite(bodyHeight) && bodyHeight > 0f
                ? bodyHeight
                : _navigationAgentHeight,
            AttackDamage = attackDamage,
            Profile = attackProfile,
            // A weapon the database gives a magazine and a reload time fires from that magazine; everything
            // else (every melee row) has none and fires forever, exactly as before.
            Magazine = attackProfile.Reloads
                ? NpcWeaponMagazine.Loaded(attackProfile.MagazineSize)
                : NpcWeaponMagazine.None,
            IdleEmoteId = ResolveBehaviorEmote(baseParams),
            CombatEmoteId = ResolveBehaviorEmote(offensiveParams),
            EmoteDurationSeconds = baseParams.TryGetEmoteDurationSeconds(out int emoteSeconds) ? emoteSeconds : -1,
            AbilityModules = ResolveAbilityModules(baseParams, offensiveBehavior),
        };

        return _brains.TryAdd(npc.EntityId, brain);
    }

    /// <summary>Starts simulating a turret. Safe to call twice for the same id.</summary>
    public bool RegisterTurret(TurretEntity turret) => Turrets.Register(turret);

    /// <summary>Stops simulating an NPC or turret. Safe to call for unknown ids.</summary>
    public bool Unregister(ulong entityId)
    {
        bool npc = _brains.TryRemove(entityId, out var removed);
        removed?.Routine.Stop();
        bool turret = Turrets.Unregister(entityId);
        return npc || turret;
    }

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

    /// <summary>Drops every brain and turret. Used when a zone is torn down.</summary>
    public void Clear()
    {
        foreach (var npc in _brains.Values)
        {
            npc.Routine.Stop();
        }

        _brains.Clear();
        _activities.Clear();
        Turrets.Clear();
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return;
        }

        if (!Enabled || !_rules.Enabled)
        {
            return;
        }

        // Unmanned turrets are not NPC brains: a shard with no mobs still has to fire them, so
        // this runs before the empty-brains early return and before the movement-interval gate.
        Turrets.Tick(currentTime);

        if (_brains.IsEmpty)
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
        // A stalled shard or toggled AI must not turn wall-clock downtime into a teleport.
        elapsedMs = Math.Min(elapsedMs, 250UL);
        _lastMovementAt = currentTime;
        _routinePathQueriesLeft = RoutinePathQueriesPerTick;

        bool perceive = currentTime >= _lastPerceptionAt + (ulong)_rules.PerceptionIntervalMs;
        if (perceive)
        {
            _lastPerceptionAt = currentTime;
        }

        // The players an acquisition scan may consider are the same for every brain on one
        // pass, so the client map is enumerated once per pass instead of once per brain: a
        // zone of 150 NPCs used to re-walk the clients (and re-test every player's liveness
        // and zone) 150 times on every perception tick.
        List<CharacterEntity> acquisitionCandidates = perceive ? CollectAcquisitionCandidates() : null;

        foreach (var entry in _brains)
        {
            UpdateBrain(entry.Value, elapsedMs, currentTime, perceive, acquisitionCandidates);
        }
    }

    /// <summary>
    ///     The players an acquisition scan may consider: live player characters in the shard's
    ///     own zone. Hostility is not applied here — it depends on the NPC scanning — only the
    ///     checks that are identical for every scanner.
    /// </summary>
    private List<CharacterEntity> CollectAcquisitionCandidates()
    {
        var candidates = new List<CharacterEntity>();

        foreach (var client in _shard.Clients.Values)
        {
            if (client?.CharacterEntity is not { IsAlive: true } candidate)
            {
                continue;
            }

            // One shard simulates one zone: a player in another zone stands on ground this
            // simulation knows nothing about, so their position can only coincide with an
            // NPC's by accident. Never aggro across that boundary.
            if (!ShardZone.IsPlayerInZone(_shard, client))
            {
                continue;
            }

            candidates.Add(candidate);
        }

        return candidates;
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
            CancelReload(npc, _shard.CurrentTime);
            npc.Brain.OnDeath();
            npc.Routine.Stop();
            npc.TargetId = 0;
        }
    }

    private void UpdateBrain(NpcBrain npc, ulong elapsedMs, ulong currentTime, bool perceive, List<CharacterEntity> acquisitionCandidates)
    {
        var entity = npc.Entity;
        if (entity == null || entity.IsPlayerControlled ||
            !_shard.Entities.TryGetValue(npc.EntityId, out var registered) ||
            !ReferenceEquals(registered, entity))
        {
            Unregister(npc.EntityId);
            return;
        }

        if (npc.Brain.State == AiBrainState.Dead || !entity.IsAlive)
        {
            // Death can be observed here before the event bus flushes its
            // CharacterDiedEvent. Close both client animation windows on this
            // defensive path as well, so a corpse never keeps a fire or reload
            // pose alive until the next network update.
            CancelAttackAnimation(npc);
            CancelReload(npc, currentTime);
            npc.Brain.OnDeath();
            npc.Routine.Stop();
            npc.TargetId = 0;
            npc.Navigation.Reset();
            SyncBehaviorEmote(npc, currentTime);
            return;
        }

        // The attack animation (CombatView burst markers) and the reload (CombatView reload markers) are
        // windows: close the ones that have run out before deciding anything new, so a client sees Fire...
        // then Ended, and Reloaded... then the magazine refilled, in the order the DB timing describes.
        EndAttackAnimationIfElapsed(npc, currentTime);
        EndReloadIfElapsed(npc, currentTime);

        if (perceive)
        {
            RefreshTarget(npc, acquisitionCandidates);
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
        // Line of sight is the costliest question this loop can ask: a full ray cast against the
        // zone's static geometry, and a populated zone made every engaged NPC ask it on every
        // 50 ms movement tick. The answer only changes when something moves, so the ray goes
        // out on the perception cadence (and on the first pass, before any verdict exists) and
        // the last verdict stands in between. The six second target-lost timeout tolerates the
        // staleness with margin, and a mob that loses sight of its target finds out a perception
        // pass later, not a movement tick later.
        bool visible = targetAlive && TargetVisibleNow(npc, target, perceive);

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

        npc.Routine.Update(currentTime, entity.Position, decision.State == AiBrainState.Idle,
            !IsMovementRestricted(entity) && !IsSliding(entity), _navigation.SupportsRoutines);
        SyncRoutineWeapon(npc, currentTime);
        // Stop a work/idle pose BEFORE an attack chain can start its own emote.
        SyncBehaviorEmote(npc, currentTime);

        if (decision.Attack && targetAlive)
        {
            var profile = npc.Profile;
            if (IsWeaponRestricted(entity))
            {
                // The database's own effect says the character may not use its weapon right now - the
                // charge-up of a charging weapon, a knock-down, a stun. The brain has already spent this
                // attack window, so the mob does nothing until the next one, which is what the flag asks
                // for; the effect that set the flag is what ends the restriction. The same flag
                // (restrict_abilities) holds back the behaviour set's own ability module, which is why
                // this test comes first.
            }
            else
            {
                // The behaviour set's own ability module is this window's action when it lands the hit: the
                // module row names the ability whose chains draw the move (the dodge pair's sidestep, Move
                // Then Fire's roar), and 12 of the build's 26 module ids deliver their own damage. A module
                // that only animates leaves the attack to the weapon below - the melee swing modules are the
                // mob's own hit with an animation on top - unless the module's own effect restricts the
                // weapon meanwhile, which the dodge pair's does for the 500 ms it runs.
                bool windowSpent = UseAbilityModule(npc, currentTime, attackDistance);

                if (!windowSpent && !IsWeaponRestricted(entity))
                {
                    if (CanFire(npc, currentTime))
                    {
                        // Run the weapon's own ability first: the database puts the attack's animation in
                        // the chains of that ability, and for some weapons the attack itself (see
                        // NpcWeaponAbilities).
                        bool abilityRan = ActivateWeaponAbility(npc, currentTime);
                        ActivateOverchargeAbility(npc, currentTime);

                        if (!abilityRan || profile is not { ChainDeliversDamage: true })
                        {
                            // The chains did not run (no animating ability, no aptitude system, or the chain
                            // rejected the activation) or they do not deliver the hit: the AI's own damage -
                            // the monster's melee damage or its projectile - is the attack. A chain that does
                            // deliver the hit is the attack, and the mob must not land a second one on the
                            // same target.
                            ResolveAttack(npc, target);
                        }

                        StartAttackAnimation(npc, currentTime);

                        // One attack spends one burst's worth of rounds: ammo_per_burst when the template
                        // carries it, else rounds_per_burst - a weapon the database gives no magazine (every
                        // melee row) spends nothing, its magazine is None.
                        int magazineCost = profile?.MagazineCost ?? 0;
                        npc.Magazine = npc.Magazine.Spend(magazineCost);

                        // The burst left the magazine dry: reload straight away, so the template's
                        // reload_time overlaps the rest of the weapon's cadence (the wait a player has
                        // between bursts) rather than stacking on top of the next shot. A weapon the
                        // database gives no magazine - every melee row, and the ranged rows without a reload
                        // time - never gets here.
                        if (profile is { Reloads: true } && !npc.Magazine.CanFire(magazineCost, currentTime))
                        {
                            // ...and the weapon's own empty-clip ability runs first: the dry-fire sound and
                            // the muzzle particles of effect 10480 for the 1.5 s it lives, which is the
                            // database's own answer to "the magazine just ran out".
                            ActivateClipEmptyAbility(npc, currentTime);
                            StartReload(npc, currentTime);
                        }
                    }
                    else
                    {
                        // The weapon was empty, so the shot waited out the reload instead of being fired.
                        // That wait is the delay: the reload does not stack on top of the cadence the brain
                        // just spent, and the mob fires the moment its magazine is full again.
                        npc.Brain.AllowAttackAt(npc.Magazine.ReloadEndTime);
                    }
                }
            }
        }

        ApplyDecision(npc, decision, elapsedMs, currentTime, target);

        // The emote belongs to the behaviour set the brain is running: the base behaviour while it has no
        // target, the offensive one while it fights (see NpcBehaviorParams.EmoteName). The walk back home
        // after a leash pull is still the base behaviour, a corpse has none.
        SyncBehaviorEmote(npc, currentTime);
    }

    /// <summary>
    ///     The emote id a behaviour string's <c>emote=</c> names, or 0 when it names none or the name is not
    ///     one of the emote table's (two monster rows name emotes build prod-1962 does not ship: this is
    ///     where that ends as "this NPC has no emote" rather than as a guess).
    /// </summary>
    /// <param name="behavior">A parsed <c>dbcharacter::Monster</c> behaviour string.</param>
    /// <returns>The emote id, or 0.</returns>
    private ushort ResolveBehaviorEmote(NpcBehaviorParams behavior)
    {
        return behavior.EmoteName.Length > 0 ? _emotes.ResolveEmoteName(behavior.EmoteName) : EmoteService.NoEmote;
    }

    /// <summary>
    ///     The ability modules a behaviour set configures, resolved into the ones the engine can run. The
    ///     base <c>behavior</c> is the set an NPC spends its life in and the one 48 monster rows configure
    ///     their modules in; 12 more configure them only in <c>behavior_offensive</c>, so that set is read
    ///     when the base one names none. Modules whose chains carry nothing a client draws or plays are
    ///     dropped, exactly like a weapon's (see <see cref="NpcAbilityModuleScan.Runnable" />): one of the
    ///     build's 26 module ids (120937) is a server-side chain alone, and running it would change the
    ///     fight without changing what a client shows.
    /// </summary>
    /// <param name="baseBehavior">The monster's parsed base behaviour string.</param>
    /// <param name="offensiveBehavior">Its raw <c>behavior_offensive</c> string.</param>
    /// <returns>The modules to run, in the order am1, am2; empty for the 3,049 rows that configure none.</returns>
    private List<NpcAbilityModuleState> ResolveAbilityModules(NpcBehaviorParams baseBehavior, string offensiveBehavior)
    {
        var scans = _monsterStats.GetAbilityModules(baseBehavior);
        if (scans.Count == 0)
        {
            scans = _monsterStats.GetAbilityModules(NpcBehaviorParams.Parse(offensiveBehavior));
        }

        var modules = new List<NpcAbilityModuleState>();
        foreach (var scan in scans)
        {
            // A module may have no client-visible chain and still own a CAIS navigation
            // request. Keep that request even though it is not an activatable ability; dropping
            // it here was the reason am*NavToDist/am*NavTimeout had no runtime effect.
            if (scan.Runnable || scan.Module.NavToDistance > 0f)
            {
                modules.Add(new NpcAbilityModuleState
                {
                    Params = scan.Module,
                    AbilityId = scan.AbilityId,
                    DeliversDamage = scan.DeliversDamage,
                    Runnable = scan.Runnable,
                });
            }
        }

        return modules;
    }

    /// <summary>
    ///     Rolls the NPC's ability modules for one decision window and runs the first one that is off
    ///     cooldown, in the module's own distance band and past its own chance roll, activating the ability
    ///     the module row names (see <see cref="NpcBehaviorAbilities" />).
    /// </summary>
    /// <remarks>
    ///     The database gives the modules' gates and no event that fires them, so the window the brain
    ///     asked to attack in is the trigger: a module is that window's action, <c>am1</c> is preferred over
    ///     <c>am2</c> (the order the row spells them in) and a module that ran is locked out for its own
    ///     <c>am*Cooldown</c>. A roll that fails leaves the module available for the next window - the
    ///     chance is per attempt, not a cooldown - and the next module in the row is offered the same
    ///     window. Modules whose chains carry nothing a client draws or plays were dropped when the NPC was
    ///     registered (<see cref="NpcAbilityModuleScan.Runnable" />).
    /// </remarks>
    /// <param name="npc">The NPC whose modules are being rolled.</param>
    /// <param name="currentTime">The shard's current time, in milliseconds.</param>
    /// <param name="attackDistance">The straight-line distance to the NPC's target, in metres.</param>
    /// <returns>
    ///     Whether the module that ran delivers the hit itself, i.e. whether this window is spent: a module
    ///     whose chains land the damage is the attack, while one that only animates leaves the mob's own
    ///     attack to go out with it.
    /// </returns>
    private bool UseAbilityModule(NpcBrain npc, ulong currentTime, float attackDistance)
    {
        List<NpcAbilityModuleState> modules = npc.AbilityModules;
        if (_abilityActivator == null || modules == null || modules.Count == 0)
        {
            return false;
        }

        foreach (var module in modules)
        {
            if (!module.Runnable || module.AbilityId == 0 ||
                currentTime < module.NextUseAt || !module.Params.AllowsDistance(attackDistance))
            {
                continue;
            }

            if (module.Params.Chance < 1f && !RollModuleChance(npc, module, currentTime))
            {
                continue;
            }

            if (!_abilityActivator.Activate(npc.Entity, module.AbilityId, (uint)currentTime, 0f))
            {
                continue;
            }

            module.NextUseAt = currentTime + (ulong)Math.Max(0, module.Params.CooldownMs);
            return module.DeliversDamage;
        }

        return false;
    }

    /// <summary>
    ///     Rolls one module's <c>am*Chance</c> with the shard's own seeded noise, keyed by the NPC, the
    ///     module and the moment, so the same fight replays the same way.
    /// </summary>
    /// <param name="npc">The NPC the module belongs to.</param>
    /// <param name="module">The module being rolled.</param>
    /// <param name="currentTime">The shard's current time, in milliseconds.</param>
    /// <returns>Whether the roll passed: true for a <c>Chance</c> of 1, never for 0.</returns>
    private static bool RollModuleChance(NpcBrain npc, NpcAbilityModuleState module, ulong currentTime)
    {
        uint seed = AiPrng.Trace((uint)currentTime, (byte)(npc.EntityId & 0xFF)) ^ module.AbilityId;
        return AiPrng.Float(seed) < module.Params.Chance;
    }

    /// <summary>
    ///     Keeps the NPC's emote in step with the behaviour set its brain is running, and honours the
    ///     <c>emoteDuration</c> the set asks for: an emote the database gives a length is cleared when that
    ///     many seconds have passed, and the <c>-1</c> every shipped row carries holds it until the
    ///     behaviour changes. Nothing is sent for the monsters that name no emote (every one of them but
    ///     207), and a name the emote table does not have leaves the NPC with none.
    /// </summary>
    /// <param name="npc">The NPC whose behaviour emote is being synced.</param>
    /// <param name="currentTime">The shard's current time, in milliseconds.</param>
    private void SyncBehaviorEmote(NpcBrain npc, ulong currentTime)
    {
        ushort behaviorEmote = npc.Brain.State switch
        {
            AiBrainState.Dead => EmoteService.NoEmote,
            AiBrainState.Chase or AiBrainState.Attack => npc.CombatEmoteId,
            AiBrainState.Return => EmoteService.NoEmote,
            _ => npc.Routine.EmoteOverride ?? npc.IdleEmoteId,
        };

        bool activityChanged = npc.WorkingEmote != npc.Routine.IsWorking;
        npc.WorkingEmote = npc.Routine.IsWorking;
        if (behaviorEmote != npc.BehaviorEmoteId || activityChanged)
        {
            // A different behaviour set is running (or a different emote inside it): a length that ran out
            // under the old one does not carry over, so the emote of the new set starts fresh.
            npc.BehaviorEmoteId = behaviorEmote;
            npc.EmotePlayedOut = false;
        }

        ushort wanted = npc.EmotePlayedOut ? EmoteService.NoEmote : behaviorEmote;
        if ((wanted != npc.EmoteId || activityChanged) && _emotes.Perform(npc.Entity, wanted, (uint)currentTime))
        {
            npc.EmoteId = wanted;
            npc.EmoteStartedAt = currentTime;
        }

        if (!npc.Routine.IsWorking && npc.EmoteId != EmoteService.NoEmote && npc.EmoteDurationSeconds >= 0 &&
            currentTime >= npc.EmoteStartedAt + ((ulong)npc.EmoteDurationSeconds * 1000UL))
        {
            // The length the behaviour asked for ("pose for 30 seconds") has run out: the emote ends, and
            // the set that still asks for it does not start it over. A `-1` - every value the database
            // ships - holds the emote until the behaviour set changes.
            npc.EmotePlayedOut = true;
            if (_emotes.Perform(npc.Entity, EmoteService.NoEmote, (uint)currentTime))
            {
                npc.EmoteId = EmoteService.NoEmote;
                npc.EmoteStartedAt = currentTime;
            }
        }
    }

    private static void SyncRoutineWeapon(NpcBrain npc, ulong currentTime)
    {
        var entity = npc.Entity;
        if (npc.Routine.Holster)
        {
            if (!npc.RoutineWeapon.HasValue && entity.WeaponIndex.Index != 0)
            {
                npc.RoutineWeapon = entity.WeaponIndex;
                var holstered = entity.WeaponIndex;
                holstered.Index = 0;
                holstered.Time = unchecked((uint)currentTime);
                entity.SetWeaponIndex(holstered);
            }
        }
        else if (npc.RoutineWeapon.HasValue)
        {
            // Do not overwrite an equipment change made by an ability while the NPC was working.
            if (entity.WeaponIndex.Index == 0)
            {
                var restored = npc.RoutineWeapon.Value;
                restored.Time = unchecked((uint)currentTime);
                entity.SetWeaponIndex(restored);
            }

            npc.RoutineWeapon = null;
        }
    }

    private void RefreshTarget(NpcBrain npc, List<CharacterEntity> candidates)
    {
        if (npc.Brain.State == AiBrainState.Dead)
        {
            return;
        }

        // Keep a combat target while it still exists. Re-scanning every perception pass
        // would make a mob ping pong between two players standing side by side. An idle
        // NPC is different: acquisition requires a sighting, so do not pin an unseen
        // candidate into the brain before it has ever engaged it.
        if (npc.TargetId != 0 && _shard.Entities.TryGetValue(npc.TargetId, out var currentTarget))
        {
            if (npc.Brain.WantsTarget ||
                (currentTarget is CharacterEntity currentCharacter &&
                 currentCharacter.IsAlive &&
                 HasLineOfSight(npc.Entity, currentCharacter)))
            {
                return;
            }
        }

        npc.TargetId = 0;

        float radius = npc.Brain.WantsTarget ? _rules.AggroRadius * _chaseSlackMultiplier : _rules.AggroRadius;
        var origin = npc.Entity.Position;

        ulong bestId = 0;
        float bestDistance = float.MaxValue;
        float bestHeightDelta = 0f;

        // Liveness, zone and null checks are done once per pass in
        // CollectAcquisitionCandidates; hostility is the per-NPC half and stays here.
        foreach (var candidate in candidates)
        {
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
            // volume an NPC can actually fight in - and without a climb/jump the mob would
            // stand under them forever. Zones the designers wanted vertical about (a mob
            // guarding the ramp below a platform) set a bigger band; 0 turns the test off entirely.
            float heightDelta = AiVectors.HeightDelta(origin, candidate.Position);
            if (_rules.MaxAcquisitionHeightDelta > 0f && heightDelta > _rules.MaxAcquisitionHeightDelta)
            {
                continue;
            }

            // The brain intentionally requires a sighting when it acquires from Idle. Keep
            // that rule at the scan boundary too; otherwise an unseen player would become a
            // sticky target and could never be reconsidered after the first failed decision.
            if (!HasLineOfSight(npc.Entity, candidate))
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
    ///     The line of sight verdict the current decision uses. Cast on the perception cadence
    ///     (or the first time a brain reasons about a target, or when the target changed since
    ///     the last cast) and cached in between, so a combat tick costs a boolean instead of a
    ///     ray cast. See the call site in <see cref="UpdateBrain" /> for why the staleness is
    ///     safe.
    /// </summary>
    private bool TargetVisibleNow(NpcBrain npc, CharacterEntity target, bool perceive)
    {
        if (!perceive && npc.HasCheckedVisibility && npc.VisibilityCheckedFor == target.EntityId)
        {
            return npc.TargetVisible;
        }

        npc.TargetVisible = HasLineOfSight(npc.Entity, target);
        npc.VisibilityCheckedFor = target.EntityId;
        npc.HasCheckedVisibility = true;
        return npc.TargetVisible;
    }

    /// <summary>
    ///     Runs the weapon ability whose chains carry the attack's animation (and, for the weapons the data
    ///     gives one, the attack's own damage). The id is <see cref="NpcAttackProfile.AttackChainAbilityId" />:
    ///     burst, else attack, else melee - the three columns the template names for this window. Nothing
    ///     else in the server executes an NPC's weapon abilities: a weapon template's ability ids reach
    ///     <see cref="NpcAttackProfile" /> and stop there, which is exactly why the status effects those
    ///     chains apply - the replicated data a client plays an animation from - never reached a mob before.
    /// </summary>
    /// <remarks>
    ///     A charging weapon hands the chain its charge time as the register, in seconds: the database's
    ///     duration commands read a value below 1000 as seconds (see <c>ReplenishableDurationCommand</c>),
    ///     and the charge effect's own duration chain is a replenishable duration - so the charge the
    ///     weapon's row describes is how long its charge state lasts, it is released when it runs out, and
    ///     the weapon's <c>restrict_movement</c>/<c>restrict_abilities</c>/<c>restrict_melee</c> flags hold
    ///     the mob for exactly that long. A weapon with no charge time runs the ability at the shot, with no
    ///     register, and every command that reads one falls back to the database's own default.
    /// </remarks>
    private bool ActivateWeaponAbility(NpcBrain npc, ulong currentTime)
    {
        var profile = npc.Profile;
        if (_abilityActivator == null || profile == null)
        {
            return false;
        }

        // Only the abilities whose chains carry something the client draws or plays are run. A chain of
        // server-side commands alone (a stat modifier, a damage-over-time tick) needs no activation here:
        // the AI's own attack already delivers the hit, and running such a chain would change the fight
        // without changing what a client shows.
        if (!profile.ChainClientFeedback)
        {
            return false;
        }

        // Burst, then attack, then melee: the three ids the weapon template names for this window, in
        // the order a player weapon already prefers. A melee row that only fills melee_ability_id still
        // animates; a ranged row that fills burst (Shadowstrike) still prefers that over a leftover melee
        // id. See NpcAttackProfile.AttackChainAbilityId.
        uint abilityId = profile.AttackChainAbilityId;
        if (abilityId == 0)
        {
            return false;
        }

        float register = profile.ChargeUpMs > 0 ? profile.ChargeUpMs / 1000f : float.NaN;
        return _abilityActivator.Activate(npc.Entity, abilityId, (uint)currentTime, register);
    }

    /// <summary>
    ///     Runs the weapon's own empty-clip ability, the hook the database gives the moment a magazine runs
    ///     dry (<c>dbitems::WeaponTemplates.clip_empty_ability</c>). Gated exactly like the burst's ability: a
    ///     hook whose chains carry nothing a client executes is left alone, and a shard with no aptitude
    ///     system simply does not run it (see <see cref="INpcAbilityActivator" />). Nothing is passed as the
    ///     register - the rows state no charge for this hook - and the hook is fired once per empty burst,
    ///     from the same branch that starts the reload.
    /// </summary>
    private void ActivateClipEmptyAbility(NpcBrain npc, ulong currentTime)
    {
        var profile = npc.Profile;
        if (profile == null || !profile.ClipEmptyClientFeedback || profile.ClipEmptyAbilityId == 0)
        {
            return;
        }

        _abilityActivator.Activate(npc.Entity, profile.ClipEmptyAbilityId, (uint)currentTime, float.NaN);
    }

    /// <summary>
    ///     Whether the database's own combat flags say the character may not use its weapon: a charge-up
    ///     effect sets <c>restrict_abilities</c> and <c>restrict_melee</c> while it lasts, and other effects
    ///     set <c>restrict_weapon</c> (see <c>CombatFlagsCommand</c>, which snapshots and restores them per
    ///     effect).
    /// </summary>
    private static bool IsWeaponRestricted(CharacterEntity entity)
    {
        return entity.HasCombatFlag(CharacterCombatFlags.restrict_weapon)
               || entity.HasCombatFlag(CharacterCombatFlags.restrict_abilities);
    }

    /// <summary>Whether the database's own combat flags pin the character in place (a charge-up, a knock-down).</summary>
    private static bool IsMovementRestricted(CharacterEntity entity)
    {
        return entity.HasCombatFlag(CharacterCombatFlags.restrict_movement);
    }

    /// <summary>
    ///     Whether the ability system is currently moving this character along a slide an aptitude row
    ///     declared. Resolved per call: a shard builds its ability system after the AI engine.
    /// </summary>
    private bool IsSliding(CharacterEntity entity)
    {
        return _shard.Abilities?.IsMovementSliding(entity.EntityId) == true;
    }

    /// <summary>
    ///     Whether the NPC can fire its weapon now: a weapon the database gives no magazine (every melee row)
    ///     always can, an armed one only while it holds the rounds an attack spends and is not reloading. An
    ///     empty magazine whose reload has not started yet (the burst did not dry it, so nothing announced
    ///     one) starts here, so a client sees the reload animation rather than a silent gap in the fire.
    /// </summary>
    private bool CanFire(NpcBrain npc, ulong currentTime)
    {
        var profile = npc.Profile;
        if (profile == null || !profile.Reloads)
        {
            return true;
        }

        if (npc.Magazine.CanFire(profile.MagazineCost, currentTime))
        {
            return true;
        }

        if (!npc.Magazine.IsReloading(currentTime))
        {
            StartReload(npc, currentTime);
        }

        return false;
    }

    /// <summary>
    ///     Starts the weapon's reload: <c>WeaponReloaded</c> at the time it began - the marker the client plays
    ///     <c>anim_reload_type</c> from, the same one a player's own <c>ReloadWeapon</c> command produces - and
    ///     the template's <c>reload_time</c> as the window the NPC cannot fire in.
    /// </summary>
    private void StartReload(NpcBrain npc, ulong currentTime)
    {
        npc.Magazine = npc.Magazine.StartReload(currentTime, npc.Profile?.ReloadTimeMs ?? 0);
        npc.Entity?.SetWeaponReloaded(unchecked((uint)currentTime));
        ActivateReloadAbility(npc, currentTime);
    }

    /// <summary>
    ///     Runs the weapon's own reload ability, the hook the database gives the moment a reload starts
    ///     (<c>dbitems::WeaponTemplates.reload_ability</c>). Gated exactly like the empty-clip hook: a chain
    ///     that carries nothing a client executes is left alone, and a shard with no aptitude system simply
    ///     does not run it. The <c>WeaponReloaded</c> marker above is still what the client plays
    ///     <c>anim_reload_type</c> from; this is the extra chain the row names for that same window.
    /// </summary>
    private void ActivateReloadAbility(NpcBrain npc, ulong currentTime)
    {
        var profile = npc.Profile;
        if (profile == null || !profile.ReloadClientFeedback || profile.ReloadAbilityId == 0)
        {
            return;
        }

        _abilityActivator.Activate(npc.Entity, profile.ReloadAbilityId, (uint)currentTime, float.NaN);
    }

    /// <summary>
    ///     Runs the weapon's own overcharge ability, the hook the database gives a charge that has
    ///     been held past <c>ms_overcharge_delay</c> (<c>dbitems::WeaponTemplates.overcharge_ability</c>).
    ///     Gated exactly like the empty-clip hook: a chain that carries nothing a client executes is
    ///     left alone, and there is no hold-past-max event in this build — an NPC charges for
    ///     <c>ms_chargeup</c> and then fires, so the hook runs with the attack when that charge is
    ///     long enough. Nothing is passed as the register; the overcharge VFX effect carries its own
    ///     duration.
    /// </summary>
    private void ActivateOverchargeAbility(NpcBrain npc, ulong currentTime)
    {
        var profile = npc.Profile;
        if (profile == null
            || !profile.OverchargeClientFeedback
            || !NpcWeaponOvercharge.ShouldActivate(profile.OverchargeAbilityId, profile.MsOverchargeDelay, profile.ChargeUpMs))
        {
            return;
        }

        _abilityActivator.Activate(npc.Entity, profile.OverchargeAbilityId, (uint)currentTime, float.NaN);
    }

    /// <summary>Refills the magazine once the reload window the database gives the weapon has run out.</summary>
    private void EndReloadIfElapsed(NpcBrain npc, ulong currentTime)
    {
        if (!npc.Magazine.HasFinishedReloading(currentTime))
        {
            return;
        }

        npc.Magazine = npc.Magazine.FinishReload(npc.Profile?.MagazineSize ?? 0);
    }

    /// <summary>
    ///     Drops a reload that did not run to its end - the NPC died (or was despawned) while reloading: the
    ///     marker is the one the client's own <c>CancelReload</c> produces, and nothing is refilled.
    /// </summary>
    private void CancelReload(NpcBrain npc, ulong currentTime)
    {
        if (!npc.Magazine.IsReloading(currentTime))
        {
            return;
        }

        npc.Magazine = npc.Magazine.CancelReload();
        npc.Entity?.SetWeaponReloadCancelled(unchecked((uint)currentTime));
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
        AnimationUpdatedAnnouncement.SendToWatchers(
            entity.Shard,
            entity,
            npc.Profile?.FireAnimationType ?? 0,
            AnimationUpdatedAnnouncement.BurstStarted);
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
        AnimationUpdatedAnnouncement.SendToWatchers(
            npc.Entity?.Shard,
            npc.Entity,
            npc.Profile?.FireAnimationType ?? 0,
            AnimationUpdatedAnnouncement.BurstEnded);
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

        // The middle of the model the shot will hit (the target's current collision volume),
        // led for the target's movement, and launched on the drop-compensated parabola for
        // the rows the sim actually drops. See NpcAttackAim for the rules and the fallbacks.
        var aimPoint = NpcAttackAim.AimPoint(_shard.Physics, target);
        float drop = profile.Ammo is { } ammo &&
                     new AmmoFlags(ammo.Flags).Simulation == AmmoFlags.SimulationMode.Parabolic
            ? ammo.Gravity
            : 0f;
        var direction = NpcAttackAim.ShotDirection(origin, aimPoint, target.Velocity, profile.ProjectileSpeed, drop, out _);

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

        // The weapon's own first-shot cone, applied once per round so a shotgun's pellets scatter
        // instead of stacking on a single chest-aimed ray. A 0 cone (every melee row, and the ranged
        // rows the database gives no spread) is a no-op and the shot stays on the aim. There is no
        // per-NPC heat to keep: the behaviour's fireRestDuration outlasts the weapon's spread return,
        // so each burst opens at the standing first-shot cone. See NpcAttackSpreadMath.
        uint time = _shard.CurrentTime;
        float spreadPct = profile.SpreadPct;
        Vector3 lastSpreadDirection = Vector3.Zero;
        byte rounds = profile.RoundsPerBurst > 0 ? profile.RoundsPerBurst : (byte)1;
        for (byte round = 0; round < rounds; round++)
        {
            Vector3 shotDirection = NpcAttackSpreadMath.Apply(
                direction,
                spreadPct,
                time,
                profile.SlotIndex,
                round,
                lastSpreadDirection,
                time);
            lastSpreadDirection = shotDirection;

            uint trace = AiPrng.Trace(time, round);
            _projectiles.FireRangedAttack(
                entity,
                trace,
                origin,
                shotDirection,
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

    private void ApplyDecision(NpcBrain npc, in AiDecision decision, ulong elapsedMs, ulong currentTime, CharacterEntity target)
    {
        var entity = npc.Entity;

        float navigationStopDistance = target != null
            ? ResolveNavigationStopDistance(npc, currentTime)
            : npc.Brain.StandoffRange;
        bool moduleWantsApproach = target != null &&
            (decision.State is AiBrainState.Chase or AiBrainState.Attack) &&
            AiVectors.HorizontalDistance(entity.Position, target.Position) > navigationStopDistance;

        Vector3? goal = moduleWantsApproach
            ? new Vector3?(target.Position)
            : decision.Movement switch
            {
                AiMovementIntent.TowardTarget => new Vector3?(target?.Position ?? npc.Home),
                AiMovementIntent.TowardHome => new Vector3?(npc.Home),
                _ => decision.State == AiBrainState.Idle ? npc.Routine.Goal : null,
            };
        bool routineMovement = decision.State == AiBrainState.Idle && npc.Routine.Goal.HasValue;
        if (npc.Routine.Profile.FixedInPlace)
        {
            goal = null;
        }

        bool moved = false;
        bool sliding = goal.HasValue && IsSliding(entity);

        // A database slide owns the character's position while it runs (the dodge pair's 667 ms sidestep, the
        // Move Then Fire lunge): the AI must not drag the mob off the displacement the row declared, exactly
        // as it must not push against restrict_movement. The slide outlives the effect that declared it (667 ms
        // against 500 ms), so this reads the ability system's own slide state rather than a combat flag.
        if (goal.HasValue && !IsMovementRestricted(entity) && !IsSliding(entity))
        {
            moved = MoveToward(npc, goal.Value, decision.State, elapsedMs, currentTime, routineMovement);
        }
        else if (!goal.HasValue)
        {
            // A stale route must not be reused when a target is lost or the NPC reaches home.
            npc.Navigation.Reset();
        }

        if (npc.Routine.Facing.HasValue && !IsMovementRestricted(entity) && !IsSliding(entity))
        {
            entity.SetOrientation(npc.Routine.Facing.Value);
            entity.AimDirection = Vector3.Transform(Vector3.UnitY, Quaternion.Conjugate(entity.Orientation));
            _shard.Physics?.UpdateEntity(entity);
        }

        if (decision.FaceTarget && target != null)
        {
            var facing = target.Position - entity.Position;
            if (facing.LengthSquared() > 0.0001f)
            {
                entity.SetOrientation(AiVectors.OrientationFacing(facing));

                // Point the weapon at the middle of the model, the same point the shot is fired
                // at (NpcAttackAim.AimPoint), so the muzzle, the tracer and the impact agree.
                // The flat line to the feet stays as the fallback when the model point is
                // degenerate: when the target is directly above or below the NPC the XY vector
                // has near-zero length and Normalize would produce NaN, which later crashes the
                // pose serializer with ArithmeticException.
                var aimAtModel = NpcAttackAim.AimPoint(_shard.Physics, target) - entity.Position;
                if (aimAtModel.LengthSquared() > 0.0001f)
                {
                    entity.AimDirection = Vector3.Normalize(aimAtModel);
                }
                else
                {
                    var aimFlat = new Vector3(facing.X, facing.Y, 0f);
                    if (aimFlat.LengthSquared() > 0.0001f)
                    {
                        entity.AimDirection = Vector3.Normalize(aimFlat);
                    }
                }
            }
        }

        // Two locomotion animations come straight from the database's two speeds: the walk while the
        // NPC repositions at combat speed (normal_speed, the Attack state's MoveSpeed) and the run
        // while it runs a target down (fast_speed, the Chase/Return speed). A database slide
        // owns its own Sliding state. A failed route reports Standing rather than a movement
        // state, so the client does not play a walk cycle against a wall.
        short movementState = sliding
            ? _movementStateSliding
            : !moved
                ? _movementStateIdle
                : UsesWalking(npc, decision.State, routineMovement) ? _movementStateWalking : _movementStateRunning;
        entity.SetMovementState(movementState);
        BroadcastPoseIfChanged(npc, entity, movementState);
    }

    private bool MoveToward(NpcBrain npc, Vector3 goal, AiBrainState state, ulong elapsedMs, ulong currentTime, bool routineMovement)
    {
        var entity = npc.Entity;
        var waypoint = GetNavigationWaypoint(npc, goal, state, currentTime, routineMovement);
        if (!waypoint.HasValue)
        {
            return false;
        }

        var delta = waypoint.Value - entity.Position;
        delta.Z = 0f;
        float horizontal = delta.Length();
        if (!float.IsFinite(horizontal))
        {
            return false;
        }

        if (horizontal <= _navigationOptions.WaypointTolerance)
        {
            npc.Navigation.Advance();
            waypoint = GetNavigationWaypoint(npc, goal, state, currentTime, routineMovement);
            if (!waypoint.HasValue)
            {
                return false;
            }

            delta = waypoint.Value - entity.Position;
            delta.Z = 0f;
            horizontal = delta.Length();
            if (horizontal <= _navigationOptions.WaypointTolerance)
            {
                return false;
            }
        }

        var direction = delta / horizontal;
        float speed = UsesWalking(npc, state, routineMovement) ? npc.MoveSpeed : npc.ChaseSpeed;
        float step = speed * (elapsedMs / 1000f);

        // The path ends at the target's ground point, but combat movement must stop at the
        // weapon's standoff distance. Clamping here avoids stepping through a target on the
        // final tick and keeps ranged NPCs at their database-defined combat distance.
        if (!routineMovement && state != AiBrainState.Return)
        {
            float targetDistance = AiVectors.HorizontalDistance(entity.Position, goal);
            float stopDistance = ResolveNavigationStopDistance(npc, currentTime);
            step = MathF.Min(step, MathF.Max(0f, targetDistance - stopDistance));
        }

        step = MathF.Min(step, horizontal);
        if (step <= 0.001f)
        {
            return false;
        }

        var desired = entity.Position + (direction * step);

        // The wall clearance probe is six static ray casts; see WallProbeIntervalMs for why it
        // runs on every other movement tick. The probe spans from the position it last covered,
        // so the skipped tick's movement is checked by the next probe, not skipped.
        bool probeWalls = !npc.Navigation.HasWallProbeOrigin || currentTime >= npc.Navigation.NextWallProbeAt;
        var agent = new NpcNavigationAgent(entity.EntityId, npc.NavigationRadius, npc.NavigationHeight)
        {
            ProbeWalls = probeWalls,
            HasWallProbeOrigin = npc.Navigation.HasWallProbeOrigin,
            WallProbeOrigin = npc.Navigation.WallProbeOrigin,
        };
        if (!_navigation.TryStep(entity.Position, desired, agent, out var candidate) || !NpcGroundMovement.Finite(candidate))
        {
            // A cached route can become obstructed. No direct-line or long downward fallback:
            // abandon ambient goals with backoff; combat retries on its normal replan cadence.
            // The probe origin stays where it was: the span the unmade step covered has to be
            // checked again, not skipped.
            npc.Navigation.Waypoints.Clear();
            npc.Navigation.WaypointIndex = 0;
            npc.Navigation.NextReplanAt = currentTime + _navigationReplanIntervalMs;
            if (routineMovement)
            {
                npc.Routine.Blocked(currentTime);
            }

            return false;
        }

        entity.SetPosition(candidate);
        entity.SetOrientation(AiVectors.OrientationFacing(direction));
        entity.AimDirection = direction;
        _shard.Physics?.UpdateEntity(entity);

        npc.Navigation.WallProbeOrigin = candidate;
        npc.Navigation.HasWallProbeOrigin = true;
        if (probeWalls)
        {
            npc.Navigation.NextWallProbeAt = currentTime + WallProbeIntervalMs;
        }

        return true;
    }

    private static bool UsesWalking(NpcBrain npc, AiBrainState state, bool routineMovement)
    {
        if (routineMovement)
        {
            return npc.Routine.Profile.Walk;
        }

        return state == AiBrainState.Return
            ? npc.Routine.Profile.LeashWalk ?? false
            : npc.Routine.Profile.CombatWalk ?? state == AiBrainState.Attack;
    }

    /// <summary>
    ///     Resolves the CAIS module's requested navigation stop distance. The old implementation
    ///     parsed <c>am*NavToDist</c>/<c>am*NavTimeout</c> and then silently ignored both fields,
    ///     which made a module's movement disagree with its original data. A timed request owns the
    ///     approach until its watchdog expires; after that the normal weapon standoff is restored.
    /// </summary>
    private float ResolveNavigationStopDistance(NpcBrain npc, ulong currentTime)
    {
        if (npc.TargetId == 0 || npc.AbilityModules == null)
        {
            npc.Navigation.NavModuleId = 0;
            npc.Navigation.NavTargetId = 0;
            npc.Navigation.NavDeadline = 0;
            return npc.Brain.StandoffRange;
        }

        NpcAbilityModuleState requested = null;
        foreach (var module in npc.AbilityModules)
        {
            if (module.Params.NavToDistance > 0f &&
                (requested == null || module.Params.NavToDistance < requested.Params.NavToDistance))
            {
                requested = module;
            }
        }

        if (requested == null)
        {
            npc.Navigation.NavModuleId = 0;
            npc.Navigation.NavTargetId = 0;
            npc.Navigation.NavDeadline = 0;
            return npc.Brain.StandoffRange;
        }

        if (npc.Navigation.NavModuleId != requested.Params.ModuleId ||
            npc.Navigation.NavTargetId != npc.TargetId)
        {
            npc.Navigation.NavModuleId = requested.Params.ModuleId;
            npc.Navigation.NavTargetId = npc.TargetId;
            npc.Navigation.NavDeadline = requested.Params.NavTimeoutMs > 0
                ? currentTime + (ulong)requested.Params.NavTimeoutMs
                : 0;
        }

        if (npc.Navigation.NavDeadline != 0 && currentTime >= npc.Navigation.NavDeadline)
        {
            return npc.Brain.StandoffRange;
        }

        return requested.Params.NavToDistance;
    }

    private Vector3? GetNavigationWaypoint(
        NpcBrain npc,
        Vector3 goal,
        AiBrainState state,
        ulong currentTime,
        bool routineMovement)
    {
        var intent = routineMovement ? AiMovementIntent.TowardRoutine
            : state == AiBrainState.Return ? AiMovementIntent.TowardHome : AiMovementIntent.TowardTarget;
        var navigation = npc.Navigation;
        bool goalMoved = navigation.HasGoal &&
            AiVectors.HorizontalDistance(navigation.Goal, goal) > (routineMovement ? _navigationOptions.WaypointTolerance : _navigationGoalRefreshDistance);
        // A stationary ambient goal keeps its successful corridor until arrival/obstruction. It
        // does not need the moving combat target's 750 ms replan loop.
        bool expired = currentTime >= navigation.NextReplanAt &&
            (!routineMovement || navigation.Waypoints.Count == 0);
        if (!navigation.HasAttempted || navigation.Intent != intent || goalMoved || expired)
        {
            if (routineMovement && _routinePathQueriesLeft-- <= 0)
            {
                return null;
            }

            navigation.Reset();
            navigation.Intent = intent;
            navigation.Goal = goal;
            navigation.HasAttempted = true;

            var agent = new NpcNavigationAgent(npc.EntityId, npc.NavigationRadius, npc.NavigationHeight);
            var path = _navigation.FindPath(npc.Entity.Position, goal, agent) ?? Array.Empty<Vector3>();

            if (path.Count == 0 || path.Any(point => !NpcGroundMovement.Finite(point)))
            {
                navigation.NextReplanAt = currentTime + _navigationReplanIntervalMs;
                if (routineMovement)
                {
                    npc.Routine.Blocked(currentTime);
                }

                return null;
            }

            if (routineMovement)
            {
                npc.Routine.ProjectGoal(path[^1]);
            }

            navigation.Waypoints.AddRange(path);
            navigation.NextReplanAt = currentTime + _navigationReplanIntervalMs;
        }

        while (navigation.WaypointIndex < navigation.Waypoints.Count &&
               AiVectors.HorizontalDistance(npc.Entity.Position, navigation.Waypoints[navigation.WaypointIndex]) <= _navigationOptions.WaypointTolerance)
        {
            navigation.WaypointIndex++;
        }

        return navigation.WaypointIndex < navigation.Waypoints.Count
            ? navigation.Waypoints[navigation.WaypointIndex]
            : null;
    }

    /// <summary>
    ///     Broadcasts the NPC's pose when it changed since the last one that went out. A mob standing at
    ///     home with no target produces a byte-identical pose every movement tick; skipping those removes
    ///     the idle share of the pose stream, which with a populated zone is most NPCs most of the time.
    ///     The first pose after registration is always sent, so a client scoping in to an NPC that has not
    ///     moved since still receives an update from this stream on top of the MovementView keyframe the
    ///     scope-in itself delivers.
    /// </summary>
    private void BroadcastPoseIfChanged(NpcBrain npc, CharacterEntity entity, short movementState)
    {
        bool changed = !npc.HasBroadcastPose
            || npc.LastBroadcastMovementState != movementState
            || npc.LastBroadcastPosition != entity.Position
            || npc.LastBroadcastOrientation != entity.Orientation
            || npc.LastBroadcastAim != entity.AimDirection;
        if (!changed)
        {
            return;
        }

        BroadcastPose(entity, movementState);

        npc.LastBroadcastPosition = entity.Position;
        npc.LastBroadcastOrientation = entity.Orientation;
        npc.LastBroadcastAim = entity.AimDirection;
        npc.LastBroadcastMovementState = movementState;
        npc.HasBroadcastPose = true;
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

        // Deliver only to the players scoped to this NPC. Sending every pose to every connected client
        // was O(NPCs x clients) per movement tick - with a populated zone the dominant upstream traffic
        // source and a real contributor to the periodic connection problem. A late scope-in cannot see a
        // frozen NPC: ScopeIn sends the character's MovementView keyframe (the current pose) over the
        // reliable channel, and this stream takes over from there.
        _shard.EntityMan.SendToScoped(entity, pose);
    }

    /// <summary>One behaviour-set ability module and the engine's own bookkeeping for it.</summary>
    private sealed class NpcAbilityModuleState
    {
        /// <summary>The module's parameters, exactly as the behaviour string stated them.</summary>
        public NpcAbilityModule Params;

        /// <summary>The <c>apt::AbilityData</c> the module runs, resolved through <c>dbitems::AbilityModule</c>.</summary>
        public uint AbilityId;

        /// <summary>Whether the module's own chains land the hit, i.e. whether a run of it spends the window.</summary>
        public bool DeliversDamage;

        /// <summary>Whether its ability chain has a client-visible action and may be activated.</summary>
        public bool Runnable;

        /// <summary>The time (shard clock) the module may next be used, its <c>am*Cooldown</c> run out.</summary>
        public ulong NextUseAt;
    }

    private sealed class NpcNavigationState
    {
        public readonly List<Vector3> Waypoints = [];
        public AiMovementIntent Intent;
        public Vector3 Goal;
        public ulong NextReplanAt;
        public int WaypointIndex;
        public bool HasAttempted;

        /// <summary>The CAIS module whose navigation request currently owns the stop distance.</summary>
        public uint NavModuleId;

        /// <summary>The target for which that request was started.</summary>
        public ulong NavTargetId;

        /// <summary>Absolute shard time at which that request's <c>am*NavTimeout</c> expires.</summary>
        public ulong NavDeadline;

        /// <summary>Shard time at which the wall clearance probe may next run (see <c>WallProbeIntervalMs</c>).</summary>
        public ulong NextWallProbeAt;

        /// <summary>The position the last wall probe covered up to; the next probe spans from here.</summary>
        public Vector3 WallProbeOrigin;

        /// <summary>Whether <see cref="WallProbeOrigin" /> is a position the agent has actually probed from.</summary>
        public bool HasWallProbeOrigin;

        public bool HasGoal => Intent != AiMovementIntent.None;

        public void Advance()
        {
            if (WaypointIndex < Waypoints.Count)
            {
                WaypointIndex++;
            }
        }

        public void Reset()
        {
            Waypoints.Clear();
            WaypointIndex = 0;
            Intent = AiMovementIntent.None;
            Goal = Vector3.Zero;
            NextReplanAt = 0;
            HasAttempted = false;
            // A new route starts in a direction nothing was probed for, so the first step
            // of it probes rather than trusting a stale origin.
            NextWallProbeAt = 0;
            WallProbeOrigin = Vector3.Zero;
            HasWallProbeOrigin = false;
        }
    }

    private sealed class NpcBrain
    {
        public ulong EntityId;
        public CharacterEntity Entity;
        public AiBrain Brain;
        public NpcRoutine Routine;
        public AeroMessages.GSS.Character.WeaponIndexData? RoutineWeapon;
        public ulong TargetId;
        public Vector3 Home;
        public float MoveSpeed;
        public float ChaseSpeed;
        public float NavigationRadius;
        public float NavigationHeight;
        public int AttackDamage;

        /// <summary>The weapon this NPC attacks with, as the static database describes it.</summary>
        public NpcAttackProfile Profile;

        /// <summary>The attack animation currently being shown, or null when none is running.</summary>
        public NpcAttackAnimation? Animation;

        /// <summary>
        ///     The magazine the NPC fires from and its running reload, or <see cref="NpcWeaponMagazine.None" />
        ///     for a weapon that does not reload.
        /// </summary>
        public NpcWeaponMagazine Magazine;

        /// <summary>
        ///     The behaviour set's ability modules the engine runs, or an empty list for a monster whose
        ///     behaviour names none (3,049 of the build's 3,109 rows).
        /// </summary>
        public List<NpcAbilityModuleState> AbilityModules;

        /// <summary>The current collision-aware route, retained between movement ticks.</summary>
        public NpcNavigationState Navigation = new();

        /// <summary>The pose fields last broadcast for this NPC, so unchanged poses are not re-sent.</summary>
        public Vector3 LastBroadcastPosition;
        public Quaternion LastBroadcastOrientation;
        public Vector3 LastBroadcastAim;
        public short LastBroadcastMovementState;
        public bool HasBroadcastPose;

        /// <summary>The emote the monster's base behaviour string names, or 0 when it names none.</summary>
        public ushort IdleEmoteId;

        /// <summary>The emote its <c>behavior_offensive</c> string names, or 0 (the usual case).</summary>
        public ushort CombatEmoteId;

        /// <summary>The emote the running behaviour set asks for, whether or not it is on the character yet.</summary>
        public ushort BehaviorEmoteId;

        /// <summary>The emote performed on the character right now, as far as the engine knows.</summary>
        public ushort EmoteId;

        /// <summary>The time <see cref="EmoteId" /> was performed at, for the behaviour's own duration.</summary>
        public ulong EmoteStartedAt;

        /// <summary>Whether <see cref="BehaviorEmoteId" /> has already played out its own duration.</summary>
        public bool EmotePlayedOut;
        public bool WorkingEmote;

        /// <summary>
        ///     Seconds the behaviour wants the emote held (<c>emoteDuration</c>), or -1 for "until the
        ///     behaviour changes" - the only value the database ships.
        /// </summary>
        public int EmoteDurationSeconds = -1;

        /// <summary>The target the last line of sight verdict was cast against.</summary>
        public ulong VisibilityCheckedFor;

        /// <summary>Whether <see cref="TargetVisible" /> is a cast result rather than the default.</summary>
        public bool HasCheckedVisibility;

        /// <summary>Line of sight to the target, from the last perception pass (see <see cref="TargetVisibleNow" />).</summary>
        public bool TargetVisible;
    }
}
