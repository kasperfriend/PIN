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
    private readonly INpcAbilityActivator _abilityActivator;
    private readonly EmoteService _emotes;
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
        IAiProjectileLauncher projectileLauncher = null,
        INpcAbilityActivator abilityActivator = null,
        EmoteService emotes = null)
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
        var (baseBehavior, offensiveBehavior) = _monsterStats.GetBehaviors(npc.StaticInfo.CharacterTypeId);
        var baseParams = NpcBehaviorParams.Parse(baseBehavior);
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
            // A weapon the database gives a magazine and a reload time fires from that magazine; everything
            // else (every melee row) has none and fires forever, exactly as before.
            Magazine = attackProfile.Reloads
                ? NpcWeaponMagazine.Loaded(attackProfile.MagazineSize)
                : NpcWeaponMagazine.None,
            IdleEmoteId = ResolveBehaviorEmote(baseParams),
            CombatEmoteId = ResolveBehaviorEmote(NpcBehaviorParams.Parse(offensiveBehavior)),
            EmoteDurationSeconds = baseParams.TryGetEmoteDurationSeconds(out int emoteSeconds) ? emoteSeconds : -1,
            AbilityModules = ResolveAbilityModules(baseParams, offensiveBehavior),
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
            CancelReload(npc, _shard.CurrentTime);
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

        ApplyDecision(npc, decision, elapsedMs, target);

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
            if (scan.Runnable)
            {
                modules.Add(new NpcAbilityModuleState
                {
                    Params = scan.Module,
                    AbilityId = scan.AbilityId,
                    DeliversDamage = scan.DeliversDamage,
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
            if (currentTime < module.NextUseAt || !module.Params.AllowsDistance(attackDistance))
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
            _ => npc.IdleEmoteId,
        };

        if (behaviorEmote != npc.BehaviorEmoteId)
        {
            // A different behaviour set is running (or a different emote inside it): a length that ran out
            // under the old one does not carry over, so the emote of the new set starts fresh.
            npc.BehaviorEmoteId = behaviorEmote;
            npc.EmotePlayedOut = false;
        }

        ushort wanted = npc.EmotePlayedOut ? EmoteService.NoEmote : behaviorEmote;
        if (wanted != npc.EmoteId && _emotes.Perform(npc.Entity, wanted, (uint)currentTime))
        {
            npc.EmoteId = wanted;
            npc.EmoteStartedAt = currentTime;
        }

        if (npc.EmoteId != EmoteService.NoEmote && npc.EmoteDurationSeconds >= 0 &&
            currentTime >= npc.EmoteStartedAt + (ulong)(npc.EmoteDurationSeconds * 1000))
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
    ///     Runs the weapon ability whose chains carry the attack's animation (and, for the weapons the data
    ///     gives one, the attack's own damage). Nothing else in the server executes an NPC's weapon
    ///     abilities: a weapon template's ability ids reach <see cref="NpcAttackProfile" /> and stop there,
    ///     which is exactly why the status effects those chains apply - the replicated data a client plays
    ///     an animation from - never reached a mob before.
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

        uint abilityId = profile.BurstAbilityId != 0 ? profile.BurstAbilityId : profile.AttackAbilityId;
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

        // A database slide owns the character's position while it runs (the dodge pair's 667 ms sidestep, the
        // Move Then Fire lunge): the AI must not drag the mob off the displacement the row declared, exactly
        // as it must not push against restrict_movement. The slide outlives the effect that declared it (667 ms
        // against 500 ms), so this reads the ability system's own slide state rather than a combat flag.
        if (goal.HasValue && !IsMovementRestricted(entity) && !IsSliding(entity))
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

    /// <summary>One behaviour-set ability module and the engine's own bookkeeping for it.</summary>
    private sealed class NpcAbilityModuleState
    {
        /// <summary>The module's parameters, exactly as the behaviour string stated them.</summary>
        public NpcAbilityModule Params;

        /// <summary>The <c>apt::AbilityData</c> the module runs, resolved through <c>dbitems::AbilityModule</c>.</summary>
        public uint AbilityId;

        /// <summary>Whether the module's own chains land the hit, i.e. whether a run of it spends the window.</summary>
        public bool DeliversDamage;

        /// <summary>The time (shard clock) the module may next be used, its <c>am*Cooldown</c> run out.</summary>
        public ulong NextUseAt;
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

        /// <summary>
        ///     Seconds the behaviour wants the emote held (<c>emoteDuration</c>), or -1 for "until the
        ///     behaviour changes" - the only value the database ships.
        /// </summary>
        public int EmoteDurationSeconds = -1;
    }
}
