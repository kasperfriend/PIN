using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using AeroMessages.GSS.Character;
using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Ai;
using GameServer.Systems.Emotes;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class AiEngineTests
{
    private const ulong FirstTick = 60_000;
    private const ulong Step = 50; // one movement interval of StandardAiRules

    private static readonly CharacterStateData.CharacterStatus Living = CharacterStateData.CharacterStatus.Living;

    private static (FakeShard Shard, CharacterEntity Npc, CharacterEntity Player) CreateWorld(
        Vector3 npcPosition,
        Vector3 playerPosition,
        IAiRules rules = null,
        FakeAiMonsterStats monsterStats = null,
        byte npcLevel = 0,
        IAiProjectileLauncher projectileLauncher = null,
        INpcAbilityActivator abilityActivator = null,
        EmoteService emotes = null)
    {
        var shard = new FakeShard();
        if (rules != null || monsterStats != null || projectileLauncher != null || abilityActivator != null || emotes != null)
        {
            shard.AI = new AiEngine(
                shard,
                shard.EventBus,
                rules ?? new StandardAiRules(),
                new AlwaysHostileAiHostility(),
                shard.AiAttackFeedback,
                monsterStats ?? new FakeAiMonsterStats(),
                projectileLauncher,
                abilityActivator,
                emotes);
        }

        var npc = CreateLivingCharacter(shard, npcPosition);
        npc.MonsterLevel = npcLevel;
        shard.Entities[npc.EntityId] = npc;
        Assert.True(shard.AI.Register(npc));

        var player = CreateLivingCharacter(shard, playerPosition);
        shard.Entities[player.EntityId] = player;

        var client = new FakeNetworkPlayer(shard) { CharacterEntity = player };
        shard.Clients[client.SocketId] = client;

        return (shard, npc, player);
    }

    private static CharacterEntity CreateLivingCharacter(FakeShard shard, Vector3 position)
    {
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        character.SetCharacterState(Living, 0);
        character.SetMaxHealth(100_000, resetCurrent: true);
        character.SetPosition(position);
        return character;
    }

    private static void AssertState(FakeShard shard, CharacterEntity npc, AiBrainState expected)
    {
        Assert.Equal(new AiBrainState?(expected), shard.AI.GetState(npc.EntityId));
    }

    private static EmoteService CreateEmoteService(RecordingEmoteEffectApplier effects)
    {
        return new EmoteService(new FakeEmoteDataSource(), effects);
    }

    private static void Tick(FakeShard shard, ulong currentTime)
    {
        shard.CurrentTimeLong = currentTime;
        shard.AI.Tick(Step, currentTime, CancellationToken.None);
    }

    [Fact]
    public void Register_TracksNpcAndRecordsSpawnPointAsHome()
    {
        var (shard, npc, _) = CreateWorld(new Vector3(5f, 6f, 7f), new Vector3(500f, 0f, 0f));

        Assert.True(shard.AI.IsTracked(npc.EntityId));
        Assert.Equal(1, shard.AI.TrackedCount);
        AssertState(shard, npc, AiBrainState.Idle);
    }

    [Fact]
    public void Register_IgnoresPlayerControlledCharacters()
    {
        var (shard, _, player) = CreateWorld(Vector3.Zero, Vector3.Zero);
        player.Player = new FakeNetworkPlayer(shard);

        Assert.False(shard.AI.Register(player));
        Assert.False(shard.AI.Register(null));
    }

    [Fact]
    public void Unregister_StopsTracking()
    {
        var (shard, npc, _) = CreateWorld(Vector3.Zero, new Vector3(10f, 0f, 0f));

        Assert.True(shard.AI.Unregister(npc.EntityId));
        Assert.False(shard.AI.IsTracked(npc.EntityId));
        Assert.Equal(0, shard.AI.TrackedCount);
        Assert.Null(shard.AI.GetState(npc.EntityId));
        Assert.False(shard.AI.Unregister(npc.EntityId));
    }

    [Fact]
    public void IdleNpc_WithoutPlayersAround_DoesNotMove()
    {
        var (shard, npc, _) = CreateWorld(Vector3.Zero, new Vector3(500f, 0f, 0f));

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Equal(Vector3.Zero, npc.Position);
        AssertState(shard, npc, AiBrainState.Idle);
        Assert.Empty(shard.AiAttackFeedback.Attacks);
    }

    [Fact]
    public void IdleNpc_PlayerInsideAggroRange_ChasesThem()
    {
        var (shard, npc, _) = CreateWorld(Vector3.Zero, new Vector3(20f, 0f, 0f));

        Tick(shard, FirstTick);

        AssertState(shard, npc, AiBrainState.Chase);

        // Default chase speed is 8.5 m/s, one 50ms step is 0.425m towards the player.
        Assert.Equal(0.425f, npc.Position.X, 3);
        Assert.Equal(0f, npc.Position.Y, 3);
    }

    [Fact]
    public void IdleNpc_PlayerInAnotherZone_IgnoresThem()
    {
        var (shard, npc, _) = CreateWorld(Vector3.Zero, new Vector3(20f, 0f, 0f));
        shard.Clients.Values.Single().CurrentZone = new Zone { ID = 1030, Name = "Sertao" };

        Tick(shard, FirstTick);

        AssertState(shard, npc, AiBrainState.Idle);
        Assert.Equal(Vector3.Zero, npc.Position);
    }

    [Fact]
    public void ChasingNpc_ReachingMeleeRange_StartsDamagingThePlayer()
    {
        var rules = new StandardAiRules { AttackDamage = 180 };
        var (shard, npc, player) = CreateWorld(Vector3.Zero, new Vector3(3f, 0f, 0f), rules);

        Tick(shard, FirstTick); // Idle -> Chase
        Assert.Empty(shard.AiAttackFeedback.Attacks);

        Tick(shard, FirstTick + Step); // Chase -> Attack, first hit lands
        AssertState(shard, npc, AiBrainState.Attack);
        Assert.Equal(99_820, player.CurrentHealth);
        Assert.Single(shard.AiAttackFeedback.Attacks);
        Assert.Equal((npc.EntityId, player.EntityId, 180), shard.AiAttackFeedback.Attacks[0]);
    }

    [Fact]
    public void ChasingNpc_ThatCannotReachThePlayer_NeverDamagesThem()
    {
        // Regression: the shipped attack range was 45 m, i.e. a mob opened its (hitscan, no
        // projectile at all) attack the moment it noticed you. PIN has no NPC projectiles yet, so
        // a monster only lands a hit once it is within its melee reach.
        var rules = new StandardAiRules { AttackDamage = 180 };
        var (shard, npc, player) = CreateWorld(Vector3.Zero, new Vector3(40f, 0f, 0f), rules);

        for (ulong time = FirstTick; time < FirstTick + (Step * 10); time += Step)
        {
            Tick(shard, time);
        }

        Assert.True(npc.Position.X > 0f, "the mob should be closing in on the player it aggroed");
        Assert.True(npc.Position.X < 8f, "and getting somewhere, but it has not arrived yet");
        Assert.Equal(new AiBrainState?(AiBrainState.Chase), shard.AI.GetState(npc.EntityId));
        Assert.Empty(shard.AiAttackFeedback.Attacks);
        Assert.Equal(100_000, player.CurrentHealth);
    }

    [Fact]
    public void NpcStandingUnderAPlayer_DoesNotHitThemThroughTheFloor()
    {
        // The other half of "they attack me when I am above them". The player is 1.5 m off to the
        // side and 3 m up: the straight-line distance (3.35 m) is inside the mob's reach, so this
        // is the height band alone refusing to let a swing travel up through the platform.
        var rules = new StandardAiRules { AttackDamage = 180 };
        var (shard, npc, player) = CreateWorld(Vector3.Zero, new Vector3(1.5f, 0f, 3f), rules);

        for (ulong time = FirstTick; time < FirstTick + (Step * 10); time += Step)
        {
            Tick(shard, time);
        }

        // It is already inside its standoff distance (1.5 m), so it has nowhere left to walk - the
        // only thing standing between the player and 180 damage per second is the height band.
        Assert.NotEqual(new AiBrainState?(AiBrainState.Attack), shard.AI.GetState(npc.EntityId));
        Assert.Empty(shard.AiAttackFeedback.Attacks);
        Assert.Equal(100_000, player.CurrentHealth);
    }

    [Fact]
    public void NpcDoesNotAcquireAPlayerFarAboveIt()
    {
        // A player 30 m straight up is inside the 55 m aggro *radius* but nowhere near a fight;
        // the acquisition volume is squashed vertically so the mob does not lock on and then stand
        // under them forever with nothing to do.
        var (shard, npc, _) = CreateWorld(Vector3.Zero, new Vector3(2f, 0f, 30f));

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        AssertState(shard, npc, AiBrainState.Idle);
        Assert.Equal(Vector3.Zero, npc.Position);
    }

    [Fact]
    public void AttackingNpc_UsesDatabaseDamageForItsLevelOverTheRulesFallback()
    {
        // The rules value (180) must lose to the monster stat source's rating for the NPC's level,
        // and the rating is only worth its per-attack fraction of that: 500 x 1 = 500 here, with
        // the fraction pinned to 1 so this test is about *which source wins*, not about the scale.
        var rules = new StandardAiRules { AttackDamage = 180, AttackDamageFraction = 1f };
        var stats = new FakeAiMonsterStats(attackDamage: 500);
        var (shard, npc, player) = CreateWorld(Vector3.Zero, new Vector3(3f, 0f, 0f), rules, stats, npcLevel: 37);

        // Registration asked the stat source for damage at the NPC's own level.
        var request = Assert.Single(stats.AttackDamageRequests);
        Assert.Equal(37, request.Level);

        Tick(shard, FirstTick); // Idle -> Chase
        Tick(shard, FirstTick + Step); // Chase -> Attack, first hit lands
        Assert.Equal(100_000 - 500, player.CurrentHealth);
        Assert.Equal((npc.EntityId, player.EntityId, 500), Assert.Single(shard.AiAttackFeedback.Attacks));
    }

    [Fact]
    public void AttackingNpc_CommitsItsFractionOfTheDatabaseDamageRating()
    {
        // The shipped 0.1: a level's MonsterScaling.damage is a rating, not one swing. At the
        // level-45 row (13,934) that is 1,393 per hit instead of 13,934 - which is the difference
        // between "a mob kills a same-level player in one or two hits" and a fight.
        var rules = new StandardAiRules { AttackDamage = 180, AttackDamageFraction = 0.1f };
        var stats = new FakeAiMonsterStats(attackDamage: 13_934);
        var (shard, _, player) = CreateWorld(Vector3.Zero, new Vector3(3f, 0f, 0f), rules, stats, npcLevel: 45);

        Tick(shard, FirstTick); // Idle -> Chase
        Tick(shard, FirstTick + Step); // Chase -> Attack, first hit lands
        Assert.Equal(100_000 - 1_393, player.CurrentHealth);
        Assert.Equal(1_393, Assert.Single(shard.AiAttackFeedback.Attacks).Damage);
    }

    [Fact]
    public void AttackingNpc_FallsBackToRulesDamage_WhenTheDatabaseHasNoRowForItsLevel()
    {
        // The fallback is already a per-swing number: applying the fraction to it too would make a
        // monster with no database row hit for a tenth of a tenth.
        var rules = new StandardAiRules { AttackDamage = 76, AttackDamageFraction = 0.1f };
        var (shard, _, player) = CreateWorld(Vector3.Zero, new Vector3(3f, 0f, 0f), rules, npcLevel: 5);

        Tick(shard, FirstTick); // Idle -> Chase
        Tick(shard, FirstTick + Step); // first hit lands
        Assert.Equal(100_000 - 76, player.CurrentHealth);
        var attack = Assert.Single(shard.AiAttackFeedback.Attacks);
        Assert.Equal(player.EntityId, attack.TargetId);
        Assert.Equal(76, attack.Damage);
    }

    [Fact]
    public void AttackingNpc_RespectsItsCooldown()
    {
        var rules = new StandardAiRules { AttackDamage = 100, AttackCooldownMs = 1000 };
        var (shard, _, player) = CreateWorld(Vector3.Zero, new Vector3(3f, 0f, 0f), rules);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step); // first hit
        int healthAfterFirstHit = player.CurrentHealth;

        Tick(shard, FirstTick + (Step * 2)); // still on cooldown
        Assert.Equal(healthAfterFirstHit, player.CurrentHealth);

        Tick(shard, FirstTick + 1000 + Step); // cooldown elapsed
        Assert.Equal(healthAfterFirstHit - 100, player.CurrentHealth);
        Assert.Equal(2, shard.AiAttackFeedback.Attacks.Count);
    }

    [Fact]
    public void NpcThatIsShot_AggrosEvenFromOutsideAggroRange()
    {
        var (shard, npc, player) = CreateWorld(Vector3.Zero, new Vector3(300f, 0f, 0f));

        Tick(shard, FirstTick);
        AssertState(shard, npc, AiBrainState.Idle);

        shard.Damage.ApplyDamage(npc, 50, player);

        AssertState(shard, npc, AiBrainState.Chase);

        Tick(shard, FirstTick + Step);
        Assert.True(npc.Position.X > 0f, "aggroed NPC should start closing in on its attacker");
        AssertState(shard, npc, AiBrainState.Chase);
    }

    [Fact]
    public void NpcDraggedPastItsLeash_WalksBackToSpawn()
    {
        var rules = new StandardAiRules
        {
            AggroRadius = 200f,
            AttackRange = 15f,
            AttackRangeExit = 20f,
            LeashRadius = 30f,
        };
        var (shard, npc, _) = CreateWorld(Vector3.Zero, new Vector3(60f, 0f, 0f), rules);

        // Register captured (0,0,0) as home, then the NPC gets dragged away from it.
        npc.SetPosition(new Vector3(50f, 0f, 0f));

        Tick(shard, FirstTick);

        AssertState(shard, npc, AiBrainState.Return);
        Assert.True(npc.Position.X < 50f, "NPC should head back towards its spawn point");
        Assert.Empty(shard.AiAttackFeedback.Attacks);
    }

    [Fact]
    public void DeadNpc_StopsMovingAndAttacking()
    {
        var (shard, npc, player) = CreateWorld(Vector3.Zero, new Vector3(10f, 0f, 0f));
        shard.CharacterLifecycle.OnCharacterCreated(npc);
        int healthBefore = player.CurrentHealth;

        shard.CharacterLifecycle.ForceDeath(npc);
        AssertState(shard, npc, AiBrainState.Dead);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Equal(Vector3.Zero, npc.Position);
        Assert.Equal(healthBefore, player.CurrentHealth);
        Assert.Empty(shard.AiAttackFeedback.Attacks);
    }

    [Fact]
    public void DisabledEngine_LeavesNpcsAlone()
    {
        var (shard, npc, player) = CreateWorld(Vector3.Zero, new Vector3(10f, 0f, 0f));
        int healthBefore = player.CurrentHealth;

        shard.AI.Enabled = false;
        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Equal(Vector3.Zero, npc.Position);
        Assert.Equal(healthBefore, player.CurrentHealth);
        AssertState(shard, npc, AiBrainState.Idle);

        shard.AI.Enabled = true;
        Tick(shard, FirstTick + (Step * 2));

        AssertState(shard, npc, AiBrainState.Chase);
    }

    [Fact]
    public void FriendlyNpc_NeverAggros()
    {
        var shard = new FakeShard();
        shard.AI = new AiEngine(shard, shard.EventBus, new StandardAiRules(), new NeverHostileAiHostility(), shard.AiAttackFeedback, new FakeAiMonsterStats());

        var npc = CreateLivingCharacter(shard, Vector3.Zero);
        shard.Entities[npc.EntityId] = npc;
        shard.AI.Register(npc);

        var player = CreateLivingCharacter(shard, new Vector3(5f, 0f, 0f));
        shard.Entities[player.EntityId] = player;
        var client = new FakeNetworkPlayer(shard) { CharacterEntity = player };
        shard.Clients[client.SocketId] = client;

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        AssertState(shard, npc, AiBrainState.Idle);
        Assert.Equal(Vector3.Zero, npc.Position);
        Assert.Empty(shard.AiAttackFeedback.Attacks);
    }

    [Fact]
    public void MonsterRowSpeeds_OverrideTheConfiguredDefaults()
    {
        var rules = new StandardAiRules { DefaultMoveSpeed = 1f, DefaultChaseSpeed = 1f };
        var (shard, npc, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            rules,
            new FakeAiMonsterStats(normalSpeed: 2f, fastSpeed: 20f));

        Tick(shard, FirstTick);

        // fast_speed 20 m/s over one 50ms step.
        Assert.Equal(1f, npc.Position.X, 3);
    }

    [Fact]
    public void UnusableMonsterRowSpeeds_FallBackToTheConfiguredDefaults()
    {
        var rules = new StandardAiRules { DefaultMoveSpeed = 4f, DefaultChaseSpeed = 6f };
        var (shard, npc, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            rules,
            new FakeAiMonsterStats(normalSpeed: 0f, fastSpeed: 0f));

        Tick(shard, FirstTick);

        Assert.Equal(0.3f, npc.Position.X, 3); // 6 m/s over 50ms
    }

    [Fact]
    public void BrainIsDroppedWhenTheEntityLeavesTheShard()
    {
        var (shard, npc, _) = CreateWorld(Vector3.Zero, new Vector3(10f, 0f, 0f));

        shard.Entities.Remove(npc.EntityId);
        Tick(shard, FirstTick);

        Assert.False(shard.AI.IsTracked(npc.EntityId));
        Assert.Equal(0, shard.AI.TrackedCount);
    }

    [Fact]
    public void Clear_DropsEveryBrain()
    {
        var shard = new FakeShard();
        var first = CreateLivingCharacter(shard, Vector3.Zero);
        var second = CreateLivingCharacter(shard, new Vector3(1f, 0f, 0f));
        shard.Entities[first.EntityId] = first;
        shard.Entities[second.EntityId] = second;
        shard.AI.Register(first);
        shard.AI.Register(second);
        Assert.Equal(2, shard.AI.TrackedCount);

        shard.AI.Clear();

        Assert.Equal(0, shard.AI.TrackedCount);
        Assert.Empty(shard.AI.GetTrackedEntityIds());
    }

    /// <summary>A ranged monster profile, as <c>NpcAttackResolver</c> would build it from the database.</summary>
    private static NpcAttackProfile RangedProfile(ushort ammoId = 922, int damage = 302) => new()
    {
        Mode = NpcAttackMode.Ranged,
        MonsterId = 276,
        WeaponId = 30_025,
        Range = 25f,
        AttackRange = 25f,
        AttackRangeExit = 28.75f,
        StandoffRange = 20f,
        AttackIntervalMs = 2500,
        BurstDurationMs = 100,
        DamagePerRound = damage,
        RoundsPerBurst = 3,
        AmmoId = ammoId,
        Ammo = new Ammo { Id = ammoId, ProjectileSpeed = 40f, ImpactRadius = 0.5f, MaxRadius = 1.5f, Flags = 1 },
        ProjectileSpeed = 40f,
        ImpactRadius = 0.5f,
        MaxRadius = 1.5f,
    };

    [Fact]
    public void RangedNpc_FiresItsWeaponsProjectilesInsteadOfApplyingDamage()
    {
        var stats = new FakeAiMonsterStats { AttackProfile = RangedProfile() };
        var shots = new RecordingAiProjectileLauncher();
        var (shard, npc, player) = CreateWorld(Vector3.Zero, new Vector3(20f, 0f, 0f), monsterStats: stats, projectileLauncher: shots);

        Tick(shard, FirstTick);              // Idle -> Chase, closes on the player
        Tick(shard, FirstTick + Step);       // in weapon range -> Attack, first burst

        AssertState(shard, npc, AiBrainState.Attack);
        Assert.Equal(3, shots.Shots.Count);  // rounds_per_burst
        Assert.Empty(shard.AiAttackFeedback.Attacks);

        var burst = shots.Shots[0];
        Assert.Equal(npc.EntityId, burst.SourceId);
        Assert.Equal(922u, burst.AmmoId);
        Assert.Equal(302, burst.Damage);
        Assert.Equal(25f, burst.Range);
        Assert.Equal(40f, burst.ProjectileSpeed);
        Assert.Equal(0.5f, burst.ImpactRadius);
        Assert.Equal(1.5f, burst.MaxRadius);

        // The muzzle is the character's chest offset when the monster row has none.
        Assert.Equal(npc.Position.X, burst.Origin.X, 3);
        Assert.Equal(npc.Position.Y, burst.Origin.Y, 3);
        Assert.Equal(npc.Position.Z + 1.62f, burst.Origin.Z, 3);

        // Aimed at the player's chest. The default profile carries no spread, so every round of the
        // burst lands on the same ray - the behaviour a weapon the database gives no spread asked for.
        Assert.True(burst.Direction.X > 0.9f, "an NPC should aim at its target, not at the floor");
        Assert.Equal(shots.Shots[0].Direction, shots.Shots[1].Direction);
        Assert.Equal(shots.Shots[0].Direction, shots.Shots[2].Direction);
        Assert.Equal(100_000, player.CurrentHealth);

        // A burst is one attack: the weapon's own cadence, not one attack per tick.
        Tick(shard, FirstTick + (Step * 2));
        Assert.Equal(3, shots.Shots.Count);
        Tick(shard, FirstTick + 2500 + Step);
        Assert.Equal(6, shots.Shots.Count);
    }

    [Fact]
    public void RangedNpc_WithASpreadCone_ScattersEachRoundOfTheBurst()
    {
        // The weapon's own first-shot cone, applied once per round: a shotgun's pellets (or a rifle's
        // 3-round burst) scatter instead of stacking on a single chest-aimed ray. The default profile
        // above carries no spread and keeps the stacked-ray behaviour a 0 row asked for.
        var stats = new FakeAiMonsterStats { AttackProfile = RangedProfile() with { SpreadPct = 6f, SlotIndex = 2 } };
        var shots = new RecordingAiProjectileLauncher();
        var (shard, _, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: shots);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Equal(3, shots.Shots.Count);
        Assert.NotEqual(shots.Shots[0].Direction, shots.Shots[1].Direction);
        Assert.NotEqual(shots.Shots[0].Direction, shots.Shots[2].Direction);
        Assert.NotEqual(shots.Shots[1].Direction, shots.Shots[2].Direction);

        var aim = Vector3.Normalize(shots.Shots[0].Direction);
        Assert.True(Vector3.Dot(Vector3.Normalize(shots.Shots[1].Direction), aim) > 0.9f);
        Assert.True(Vector3.Dot(Vector3.Normalize(shots.Shots[2].Direction), aim) > 0.9f);
    }

    [Fact]
    public void RangedNpc_DoesNotCloseToMeleeRangeWhileItCanShoot()
    {
        var stats = new FakeAiMonsterStats { AttackProfile = RangedProfile() };
        var (shard, npc, _) = CreateWorld(Vector3.Zero, new Vector3(20f, 0f, 0f), monsterStats: stats, projectileLauncher: new RecordingAiProjectileLauncher());

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        // standoff 20 m: the NPC is already at its preferred distance and holds position.
        Assert.Equal(0f, npc.Position.X, 3);
    }

    [Fact]
    public void MeleeNpc_SwingsWithItsWeaponsDamageInsteadOfTheRating()
    {
        var stats = new FakeAiMonsterStats(attackDamage: 13_934)
        {
            AttackProfile = new NpcAttackProfile
            {
                Mode = NpcAttackMode.Melee,
                MonsterId = 279,
                WeaponId = 85_189,
                Range = 2.6f,
                AttackRange = 3.5f,
                AttackRangeExit = 5f,
                StandoffRange = 2f,
                AttackIntervalMs = 2000,
                BurstDurationMs = 1600,
                DamagePerRound = 697,
                RoundsPerBurst = 1,
                FireAnimationType = 2,
            },
        };
        var shots = new RecordingAiProjectileLauncher();
        var (shard, _, player) = CreateWorld(Vector3.Zero, new Vector3(3f, 0f, 0f), monsterStats: stats, projectileLauncher: shots);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);       // first swing

        Assert.Empty(shots.Shots);
        Assert.Equal(100_000 - 697, player.CurrentHealth);

        Tick(shard, FirstTick + (Step * 2)); // weapon cadence 2000 ms, still resting
        Assert.Equal(100_000 - 697, player.CurrentHealth);
        Tick(shard, FirstTick + 2000 + Step);
        Assert.Equal(100_000 - (697 * 2), player.CurrentHealth);
    }

    [Fact]
    public void MeleeNpc_AttackAnimation_RunsForTheWeaponsBurstCycle()
    {
        // The melee Spyder's template: ms_per_burst 1,600 (its swing) against a 2,000 ms behaviour
        // cadence, so the animation is the shorter of the two.
        var stats = new FakeAiMonsterStats(attackDamage: 13_934)
        {
            AttackProfile = new NpcAttackProfile
            {
                Mode = NpcAttackMode.Melee,
                AttackRange = 3.5f,
                AttackRangeExit = 5f,
                StandoffRange = 2f,
                AttackIntervalMs = 2000,
                BurstDurationMs = 1600,
                DamagePerRound = 697,
            },
        };
        var (shard, npc, _) = CreateWorld(Vector3.Zero, new Vector3(3f, 0f, 0f), monsterStats: stats);
        uint untouched = npc.Character_CombatView.WeaponBurstEndedProp;

        Tick(shard, FirstTick);               // Idle -> Chase
        Tick(shard, FirstTick + Step);        // swing at 60,050

        Assert.Equal(60_050u, npc.Character_CombatView.WeaponBurstFiredProp);
        Assert.Equal(untouched, npc.Character_CombatView.WeaponBurstEndedProp);

        Tick(shard, 61_600);                  // still inside the swing
        Assert.Equal(untouched, npc.Character_CombatView.WeaponBurstEndedProp);

        Tick(shard, 61_650);                  // 60,050 + 1,600
        Assert.Equal(61_650u, npc.Character_CombatView.WeaponBurstEndedProp);
    }

    [Fact]
    public void RangedNpc_BurstAnimation_UsesTheWeaponsBurstTiming()
    {
        // NPC Guard Rifle: ms_per_burst 100 while the behaviour's cycle is 2,500 ms.
        var stats = new FakeAiMonsterStats { AttackProfile = RangedProfile() };
        var (shard, npc, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: new RecordingAiProjectileLauncher());
        uint untouched = npc.Character_CombatView.WeaponBurstEndedProp;

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);        // burst at 60,050

        Assert.Equal(60_050u, npc.Character_CombatView.WeaponBurstFiredProp);
        Tick(shard, 60_100);
        Assert.Equal(untouched, npc.Character_CombatView.WeaponBurstEndedProp);

        Tick(shard, 60_150);                  // 60,050 + 100
        Assert.Equal(60_150u, npc.Character_CombatView.WeaponBurstEndedProp);
    }

    [Fact]
    public void WeaponlessNpc_SwingsWithTheUnarmedAnimation()
    {
        // No weapon row at all: the swing is the AI's documented default, clamped by the 1,000 ms
        // rules cadence it fights on.
        var rules = new StandardAiRules { AttackDamage = 100, AttackCooldownMs = 1000 };
        var (shard, npc, _) = CreateWorld(Vector3.Zero, new Vector3(3f, 0f, 0f), rules);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Equal(60_050u, npc.Character_CombatView.WeaponBurstFiredProp);

        Tick(shard, 60_550);                  // 60,050 + NpcAttackAnimation.DefaultDurationMs
        Assert.Equal(60_550u, npc.Character_CombatView.WeaponBurstEndedProp);
    }

    [Fact]
    public void NpcKilledMidBurst_CancelsTheAttackAnimation()
    {
        var stats = new FakeAiMonsterStats { AttackProfile = RangedProfile() };
        var (shard, npc, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: new RecordingAiProjectileLauncher());
        shard.CharacterLifecycle.OnCharacterCreated(npc);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);        // burst at 60,050, ends 60,150
        uint deathTime = unchecked((uint)shard.CurrentTimeLong);

        shard.CharacterLifecycle.ForceDeath(npc);

        // Cancelled, not ended: the client drops the attack animation for the death.
        Assert.Equal(deathTime, npc.Character_CombatView.WeaponBurstCancelledProp);
        Assert.NotEqual(60_150u, npc.Character_CombatView.WeaponBurstEndedProp);
    }

    [Fact]
    public void ChasingNpcRuns_AttackingNpcWalks()
    {
        // The two locomotion animations are the database's two speeds: the chase runs at fast_speed,
        // the Attack state repositions at normal_speed.
        var (chasing, chasingNpc, _) = CreateWorld(Vector3.Zero, new Vector3(20f, 0f, 0f));

        Tick(chasing, FirstTick);

        Assert.Equal((short)0x2004, chasingNpc.MovementState);  // Running | Movement

        var stats = new FakeAiMonsterStats { AttackProfile = RangedProfile() };
        var (shard, npc, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(25f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: new RecordingAiProjectileLauncher());

        Tick(shard, FirstTick);               // Idle -> Chase, still running
        Assert.Equal((short)0x2004, npc.MovementState);

        Tick(shard, FirstTick + Step);        // in range but past the standoff: Attack, walking

        AssertState(shard, npc, AiBrainState.Attack);
        Assert.Equal((short)0x5004, npc.MovementState);  // Walking | Movement
    }

    /// <summary>A ranged monster whose weapon has a magazine: NPC Guard Rifle's 20-round clip would take far
    /// too long to empty in a test, so this is the same shape with two rounds and a one-second reload.</summary>
    private static NpcAttackProfile MagazineProfile() => RangedProfile() with
    {
        MagazineSize = 2,
        AmmoPerBurst = 1,
        ReloadTimeMs = 1000,
    };

    [Fact]
    public void ArmedNpc_DryMagazine_ReloadsAndFiresItsFullBurstAgain()
    {
        var stats = new FakeAiMonsterStats { AttackProfile = MagazineProfile() };
        var shots = new RecordingAiProjectileLauncher();
        var (shard, npc, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: shots);

        // The values the view was built with: anything but these is a reload the engine announced.
        uint reloadedAtSpawn = npc.Character_CombatView.WeaponReloadedProp;
        uint cancelledAtSpawn = npc.Character_CombatView.WeaponReloadCancelledProp;

        Tick(shard, FirstTick);                   // Idle -> Chase
        Tick(shard, FirstTick + Step);            // burst 1 at 60,050: 2 rounds -> 1
        Assert.Equal(3, shots.Shots.Count);
        Assert.Equal(reloadedAtSpawn, npc.Character_CombatView.WeaponReloadedProp);

        Tick(shard, FirstTick + 2500 + Step);     // burst 2 at 62,550: the last round -> dry
        Assert.Equal(6, shots.Shots.Count);

        // The magazine ran dry with that burst, so the reload starts at once and the client is told: the
        // marker is the weapon's anim_reload_type, and the window it plays in is the template's reload_time.
        Assert.Equal(62_550u, npc.Character_CombatView.WeaponReloadedProp);

        Tick(shard, 63_000);                      // mid-reload: not a second marker, no shot
        Assert.Equal(6, shots.Shots.Count);
        Assert.Equal(62_550u, npc.Character_CombatView.WeaponReloadedProp);

        Tick(shard, 63_550);                      // 62,550 + 1,000: the magazine is full again
        Assert.Equal(6, shots.Shots.Count);

        Tick(shard, 65_050);                      // the weapon's own cadence: the full burst fires again
        Assert.Equal(9, shots.Shots.Count);
        Assert.Equal(cancelledAtSpawn, npc.Character_CombatView.WeaponReloadCancelledProp);
    }

    /// <summary>
    ///     A magazine profile whose weapon names the empty-clip ability the build's Tesla Rifle 2.0 (template
    ///     12132, 5 monster slots) carries: ability 39239, which applies effect 10480 - the dry-fire sound, its
    ///     muzzle particles and <c>restrict_weapon</c> for the 1.5 s the effect lives.
    /// </summary>
    private static NpcAttackProfile ClipEmptyProfile() => MagazineProfile() with
    {
        ClipEmptyAbilityId = 39_239,
        ClipEmptyClientFeedback = true,
    };

    [Fact]
    public void ArmedNpc_EmptyingTheMagazine_RunsTheWeaponsEmptyClipAbility()
    {
        var stats = new FakeAiMonsterStats { AttackProfile = ClipEmptyProfile() };
        var abilities = new FakeNpcAbilityActivator();
        var (shard, _, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: new RecordingAiProjectileLauncher(),
            abilityActivator: abilities);

        Tick(shard, FirstTick);                   // Idle -> Chase
        Tick(shard, FirstTick + Step);            // burst 1: 2 rounds -> 1

        // Nothing is empty yet, so the weapon's own empty hook has not run.
        Assert.Empty(abilities.Activations);

        Tick(shard, FirstTick + 2500 + Step);     // burst 2 empties the magazine

        var empty = Assert.Single(abilities.Activations);
        Assert.Equal(39_239u, empty.AbilityId);
        Assert.Equal(62_550u, empty.Time);

        // Fired once per empty magazine, not once per tick: the reload it triggered comes first, and the next
        // burst (after the reload) refills the clip without running the hook again.
        Tick(shard, 63_000);
        Tick(shard, 63_550);
        Assert.Single(abilities.Activations);

        // The magazine is full again, so the next burst spends it down to one round - still not empty.
        Tick(shard, 65_050);
        Assert.Single(abilities.Activations);

        Tick(shard, 67_550);
        Assert.Equal(2, abilities.Activations.Count);
    }

    /// <summary>
    ///     A magazine profile whose weapon names a reload ability with client feedback: the sibling of
    ///     the empty-clip hook, fired when the reload starts rather than when the magazine runs dry.
    /// </summary>
    private static NpcAttackProfile ReloadAbilityProfile() => MagazineProfile() with
    {
        ReloadAbilityId = 40_001,
        ReloadClientFeedback = true,
    };

    [Fact]
    public void ArmedNpc_StartingAReload_RunsTheWeaponsReloadAbility()
    {
        // The template's reload_ability is the chain the database names for the reload window. The
        // WeaponReloaded marker is still what the client plays anim_reload_type from; this is the extra
        // chain the row names for that same moment, gated like clip_empty (client feedback, once per
        // reload).
        var stats = new FakeAiMonsterStats { AttackProfile = ReloadAbilityProfile() };
        var abilities = new FakeNpcAbilityActivator();
        var (shard, _, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: new RecordingAiProjectileLauncher(),
            abilityActivator: abilities);

        Tick(shard, FirstTick);                   // Idle -> Chase
        Tick(shard, FirstTick + Step);            // burst 1: 2 rounds -> 1

        Assert.Empty(abilities.Activations);

        Tick(shard, FirstTick + 2500 + Step);     // burst 2 empties the magazine and starts the reload

        var reload = Assert.Single(abilities.Activations);
        Assert.Equal(40_001u, reload.AbilityId);
        Assert.Equal(62_550u, reload.Time);

        Tick(shard, 63_000);
        Tick(shard, 63_550);
        Assert.Single(abilities.Activations);

        Tick(shard, 65_050);
        Assert.Single(abilities.Activations);

        Tick(shard, 67_550);
        Assert.Equal(2, abilities.Activations.Count);
    }

    [Fact]
    public void ReloadAbilityThatCarriesNoClientCommand_IsNotRun()
    {
        var stats = new FakeAiMonsterStats
        {
            AttackProfile = MagazineProfile() with { ReloadAbilityId = 40_002, ReloadClientFeedback = false },
        };
        var abilities = new FakeNpcAbilityActivator();
        var (shard, _, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: new RecordingAiProjectileLauncher(),
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);
        Tick(shard, FirstTick + 2500 + Step);

        Assert.Empty(abilities.Activations);
    }

    [Fact]
    public void EmptyClipAbilityThatCarriesNoClientCommand_IsNotRun()
    {
        // The other two rows that name a clip_empty_ability (templates 11975 and 11971, 2 slots) point at 35842,
        // whose chain is a RegisterTimedTriggerCommandDef and nothing else: server-side, so the engine leaves it
        // exactly like a server-only burst chain.
        var stats = new FakeAiMonsterStats
        {
            AttackProfile = MagazineProfile() with { ClipEmptyAbilityId = 35_842, ClipEmptyClientFeedback = false },
        };
        var abilities = new FakeNpcAbilityActivator();
        var (shard, _, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: new RecordingAiProjectileLauncher(),
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);
        Tick(shard, FirstTick + 2500 + Step);     // the magazine runs dry here

        Assert.Empty(abilities.Activations);
    }

    /// <summary>
    ///     A ranged profile whose weapon names the overcharge hook plasma 12129 carries: 4000 ms charge,
    ///     2500 ms delay, a chain a client plays. The engine runs it with the attack when the charge is
    ///     long enough, gated like clip_empty (client feedback).
    /// </summary>
    private static NpcAttackProfile OverchargeProfile() => RangedProfile() with
    {
        OverchargeAbilityId = 12_129,
        MsOverchargeDelay = 2500,
        ChargeUpMs = 4000,
        OverchargeClientFeedback = true,
    };

    [Fact]
    public void ArmedNpc_ChargingPastTheOverchargeDelay_RunsTheWeaponsOverchargeAbility()
    {
        var stats = new FakeAiMonsterStats { AttackProfile = OverchargeProfile() };
        var abilities = new FakeNpcAbilityActivator();
        var (shard, _, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: new RecordingAiProjectileLauncher(),
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        var overcharge = Assert.Single(abilities.Activations);
        Assert.Equal(12_129u, overcharge.AbilityId);
        Assert.Equal(60_050u, overcharge.Time);
        Assert.True(float.IsNaN(overcharge.Register));

        Tick(shard, FirstTick + 2500 + Step);
        Assert.Equal(2, abilities.Activations.Count);
        Assert.Equal(12_129u, abilities.Activations[1].AbilityId);
    }

    [Fact]
    public void OverchargeAbilityThatCarriesNoClientCommand_IsNotRun()
    {
        var stats = new FakeAiMonsterStats
        {
            AttackProfile = OverchargeProfile() with { OverchargeClientFeedback = false },
        };
        var abilities = new FakeNpcAbilityActivator();
        var (shard, _, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: new RecordingAiProjectileLauncher(),
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Empty(abilities.Activations);
    }

    [Fact]
    public void OverchargeAbility_IsNotRunWhenTheChargeIsShorterThanTheDelay()
    {
        var stats = new FakeAiMonsterStats
        {
            AttackProfile = OverchargeProfile() with { ChargeUpMs = 1000 },
        };
        var abilities = new FakeNpcAbilityActivator();
        var (shard, _, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: new RecordingAiProjectileLauncher(),
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Empty(abilities.Activations);
    }

    [Fact]
    public void ArmedNpc_ReloadOutlastingTheCadence_FiresAsSoonAsTheMagazineIsFull()
    {
        // The charge sniper rifle's shape: a single round and a reload_time (3,000 ms) longer than the
        // 2,500 ms cadence, so the weapon is dry after every burst and the next burst waits out the reload.
        var stats = new FakeAiMonsterStats
        {
            AttackProfile = MagazineProfile() with { MagazineSize = 1, ReloadTimeMs = 3000 },
        };
        var shots = new RecordingAiProjectileLauncher();
        var (shard, npc, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: shots);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);            // burst 1 at 60,050: the single round is spent
        Assert.Equal(3, shots.Shots.Count);
        Assert.Equal(60_050u, npc.Character_CombatView.WeaponReloadedProp);

        Tick(shard, FirstTick + 2500 + Step);     // 62,550: its cadence slot, but it is still reloading
        Assert.Equal(3, shots.Shots.Count);

        // 63,050 (60,050 + 3,000): full, so the burst it could not fire goes out at once instead of
        // waiting out another cadence - the reload was the wait.
        Tick(shard, 63_050);
        Assert.Equal(6, shots.Shots.Count);
        Assert.Equal(63_050u, npc.Character_CombatView.WeaponReloadedProp);
    }

    [Fact]
    public void ReloadingNpc_ThatDies_CancelsTheReload()
    {
        var stats = new FakeAiMonsterStats { AttackProfile = MagazineProfile() };
        var (shard, npc, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: new RecordingAiProjectileLauncher());
        shard.CharacterLifecycle.OnCharacterCreated(npc);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);
        Tick(shard, FirstTick + 2500 + Step);     // dry at 62,550, reloading until 63,550
        Tick(shard, 63_000);                      // dies mid-reload
        uint deathTime = unchecked((uint)shard.CurrentTimeLong);

        shard.CharacterLifecycle.ForceDeath(npc);

        // Cancelled, not finished: nothing is refilled and the client drops the reload animation.
        Assert.Equal(deathTime, npc.Character_CombatView.WeaponReloadCancelledProp);
        Assert.Equal(62_550u, npc.Character_CombatView.WeaponReloadedProp);
    }


    /// <summary>
    ///     The behaviour string of the 11 monster rows that dodge with the two ability modules, spelled
    ///     exactly as the database writes it - with the spaces around the second pair's <c>=</c>.
    /// </summary>
    private const string DodgeBehavior =
        "Arch_MedRangedHumanoid_Base(triggerPullTime=5000,am1Id = 33833, am1Cooldown = 1700, am1Chance = 0.65, am2Id = 33812, am2Cooldown = 1700, am2Chance = 0.65)";

    private const string DodgeSetName = "Arch_MedRangedHumanoid_Base";

    /// <summary>The melee set whose module key is misspelled, as the data writes it.</summary>
    private const string MeleeModuleSetName = "Arch_FullbodyMelee_Base";

    private const string OffensiveSetName = "Arch_MedRangedAbilityUser_Attack";

    [Fact]
    public void BehaviorModuleThatDeliversTheHit_RunsTheAbilityTheModuleRowNames()
    {
        // 86132 is the Move Then Fire module (the data's own example: monster 548), and it is a module id,
        // not an ability id: dbitems::AbilityModule 86132 names ability 36817, whose chains draw animation 28,
        // perform the roar emote and land their own damage. The engine activates the ability, not the module,
        // and that chain is the window's attack, so no volley goes out on top of it.
        var stats = new FakeAiMonsterStats { AttackProfile = FeedbackOnlyProfile() };
        stats.Behavior = "Arch_MoveThenFire_Base(am1Id=86132,am1Cooldown=8000,combatDist=30)";
        stats.AbilityModulesByBehavior["Arch_MoveThenFire_Base"] = [BehaviorModule(86_132, abilityId: 36_817, deliversDamage: true)];
        var abilities = new FakeNpcAbilityActivator();
        var shots = new RecordingAiProjectileLauncher();
        var (shard, npc, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: shots,
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        var activation = Assert.Single(abilities.Activations);
        Assert.Same(npc, activation.Npc);
        Assert.Equal(36_817u, activation.AbilityId);
        Assert.Equal(60_050u, activation.Time);

        // No register: the string's am*Timeout is a watchdog, while the durations the module's own effect
        // lasts come from the database's TimeDuration commands, not from the behaviour string.
        Assert.Equal(0f, activation.Register);
        Assert.Empty(shots.Shots);
        Assert.Empty(shard.AiAttackFeedback.Attacks);
    }

    [Fact]
    public void AnimatingOnlyBehaviorModule_LetsTheMobsOwnAttackGoOutWithIt()
    {
        // 82621 is the melee set's module (15 references, the second most-named): its chains draw animation 4
        // and land no damage of their own, so the swing is what the module adds and the mob's own attack is
        // still the hit.
        var stats = new FakeAiMonsterStats { AttackProfile = FeedbackOnlyProfile() };
        stats.Behavior = "Arch_FullbodyMelee_Base(combatDist=4,am1Id=82621,am1Cooldown=3000)";
        stats.AbilityModulesByBehavior[MeleeModuleSetName] = [BehaviorModule(82_621, abilityId: 35_942)];
        var abilities = new FakeNpcAbilityActivator();
        var shots = new RecordingAiProjectileLauncher();
        var (shard, _, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: shots,
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        // The module, then the weapon's own ability, in the same window - and the volley fired.
        Assert.Equal(2, abilities.Activations.Count);
        Assert.Equal(35_942u, abilities.Activations[0].AbilityId);
        Assert.Equal(34_894u, abilities.Activations[1].AbilityId);
        Assert.NotEmpty(shots.Shots);
    }

    [Fact]
    public void BehaviorModuleWhoseEffectRestrictsTheWeapon_SpendsTheWindow()
    {
        // The dodge pair's chains set restrict_weapon (CombatFlagsCommand) for the 500 ms they run, so the
        // window the module took fires nothing: the engine re-reads the flag the module's own effect set.
        var stats = new FakeAiMonsterStats { AttackProfile = FeedbackOnlyProfile() };
        stats.Behavior = DodgeBehavior;
        stats.AbilityModulesByBehavior[DodgeSetName] = [BehaviorModule(33_833, abilityId: 33_833)];
        var abilities = new FakeNpcAbilityActivator();
        var shots = new RecordingAiProjectileLauncher();
        var (shard, npc, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: shots,
            abilityActivator: abilities);
        abilities.OnActivate = (entity, _) => entity.SetCombatFlags(new CombatFlagsData
        {
            Value = CombatFlagsData.CharacterCombatFlags.restrict_weapon,
            Time = (uint)shard.CurrentTimeLong,
        });

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Equal(33_833u, Assert.Single(abilities.Activations).AbilityId);
        Assert.Empty(shots.Shots);
        Assert.True(npc.HasCombatFlag(CombatFlagsData.CharacterCombatFlags.restrict_weapon));
    }

    [Fact]
    public void BehaviorModuleOnCooldown_LeavesTheWindowToTheWeapon()
    {
        // The most-named module (88159, 26 references) lands its own damage, so a run of it spends the
        // window. am*Cooldown is the module's own lockout: 3,000 ms against the weapon's 2,500 ms cadence
        // means every second window is the weapon's.
        var stats = new FakeAiMonsterStats { AttackProfile = FeedbackOnlyProfile() };
        stats.Behavior = DodgeBehavior;
        stats.AbilityModulesByBehavior[DodgeSetName] = [BehaviorModule(88_159, abilityId: 37_359, cooldownMs: 3000, deliversDamage: true)];
        var abilities = new FakeNpcAbilityActivator();
        var (shard, _, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);            // 60,050: the module, locked until 63,050
        Tick(shard, FirstTick + 2500 + Step);     // 62,550: still locked, so the weapon fires
        Tick(shard, FirstTick + 5000 + Step);     // 65,050: the module again

        Assert.Equal(3, abilities.Activations.Count);
        Assert.Equal(37_359u, abilities.Activations[0].AbilityId);
        Assert.Equal(34_894u, abilities.Activations[1].AbilityId);
        Assert.Equal(37_359u, abilities.Activations[2].AbilityId);
    }

    [Fact]
    public void BehaviorModuleThatFailsItsChanceRoll_LeavesTheWindowToTheWeapon()
    {
        // am*Chance is the module's own roll and nothing is spent when it fails: a module that never
        // passes its roll never stops the weapon from firing.
        var stats = new FakeAiMonsterStats { AttackProfile = FeedbackOnlyProfile() };
        stats.Behavior = DodgeBehavior;
        stats.AbilityModulesByBehavior[DodgeSetName] = [BehaviorModule(88_159, abilityId: 37_359, chance: 0f, deliversDamage: true)];
        var abilities = new FakeNpcAbilityActivator();
        var (shard, _, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);
        Tick(shard, FirstTick + 2500 + Step);

        Assert.Equal(2, abilities.Activations.Count);
        Assert.Equal(34_894u, abilities.Activations[0].AbilityId);
        Assert.Equal(34_894u, abilities.Activations[1].AbilityId);
    }

    [Fact]
    public void BehaviorModuleOutsideItsDistanceBand_LeavesTheWindowToTheWeapon()
    {
        // am*MinDist/am*MaxDist are the module's own band, measured like the attack is: a minimum of 25 m
        // against a target 20 m away is out of it.
        var stats = new FakeAiMonsterStats { AttackProfile = FeedbackOnlyProfile() };
        stats.Behavior = DodgeBehavior;
        stats.AbilityModulesByBehavior[DodgeSetName] = [BehaviorModule(88_159, abilityId: 37_359, minDistance: 25f, deliversDamage: true)];
        var abilities = new FakeNpcAbilityActivator();
        var (shard, _, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Equal(34_894u, Assert.Single(abilities.Activations).AbilityId);
    }

    [Fact]
    public void BehaviorModule_IsHeldBackWhileTheDatabaseRestrictsAbilities()
    {
        // A module is an ability, so the same restrict_abilities flag that holds back a weapon holds back
        // the module: a mob under a charge-up or a stun does not dodge out of it.
        var stats = new FakeAiMonsterStats { AttackProfile = FeedbackOnlyProfile() };
        stats.Behavior = DodgeBehavior;
        stats.AbilityModulesByBehavior[DodgeSetName] = [BehaviorModule(88_159, abilityId: 37_359, cooldownMs: 3000, deliversDamage: true)];
        var abilities = new FakeNpcAbilityActivator();
        var (shard, npc, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);
        Assert.Equal(37_359u, Assert.Single(abilities.Activations).AbilityId);

        npc.SetCombatFlags(new CombatFlagsData
        {
            Value = CombatFlagsData.CharacterCombatFlags.restrict_abilities,
            Time = (uint)shard.CurrentTimeLong,
        });

        Tick(shard, FirstTick + 2500 + Step);     // the module is off cooldown, the flag is not

        Assert.Single(abilities.Activations);
    }

    [Fact]
    public void ServerSideBehaviorModule_IsNotRun()
    {
        // A module whose chains carry nothing a client draws or plays: the engine leaves it and fires the
        // weapon, the same rule a server-only weapon chain gets. Every one of the build's 26 module ids
        // reaches a client command - 120937's animations 22 and 26 included, once the walk follows the chains
        // the engine runs - so this row is the shape the gate has to handle rather than a shipped one. The
        // module's own key is the misspelled am1Coodown the data ships.
        var stats = new FakeAiMonsterStats { AttackProfile = FeedbackOnlyProfile() };
        stats.Behavior = "Arch_FullbodyMelee_Base(combatDist=4,am1Id=254001,am1Facing=true,am1Coodown=3000)";
        stats.AbilityModulesByBehavior[MeleeModuleSetName] = [BehaviorModule(254_001, abilityId: 254_002, clientFeedback: false)];
        var abilities = new FakeNpcAbilityActivator();
        var (shard, _, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Equal(34_894u, Assert.Single(abilities.Activations).AbilityId);
    }

    [Fact]
    public void BehaviorModulesSpelledOnlyOffensively_AreResolvedFromTheOffensiveSet()
    {
        // 12 of the 60 module-bearing rows configure theirs only in behavior_offensive: the base set is
        // read first, and only when it names no module does the offensive one supply them.
        var stats = new FakeAiMonsterStats { AttackProfile = FeedbackOnlyProfile() };
        stats.Behavior = "AggressiveWanderer";
        stats.OffensiveBehavior = "Arch_MedRangedAbilityUser_Attack(am2Id = 86100, am2Cooldown = 20000, am2MinDist = 1.5)";
        stats.AbilityModulesByBehavior[OffensiveSetName] =
            [BehaviorModule(86_100, abilityId: 34_770, cooldownMs: 20_000, minDistance: 1.5f, deliversDamage: true)];
        var abilities = new FakeNpcAbilityActivator();
        var (shard, _, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Equal(34_770u, Assert.Single(abilities.Activations).AbilityId);

        // Both sets were asked, the base one first.
        Assert.Equal(new[] { "AggressiveWanderer", OffensiveSetName }, stats.AbilityModuleRequests.ToArray());
    }

    /// <summary>
    ///     One behaviour-set ability module, as the resolver reports it: the module id and gates the string
    ///     states, the ability <c>dbitems::AbilityModule</c> resolves it to, and what the chains carry
    ///     (see <see cref="NpcAbilityModuleScan" />).
    /// </summary>
    private static NpcAbilityModuleScan BehaviorModule(
        uint moduleId,
        uint abilityId,
        float chance = 1f,
        int cooldownMs = 0,
        float minDistance = 0f,
        float maxDistance = float.MaxValue,
        bool clientFeedback = true,
        bool deliversDamage = false)
    {
        return new NpcAbilityModuleScan(
            new NpcAbilityModule(moduleId, chance, cooldownMs, minDistance, maxDistance),
            abilityId,
            clientFeedback,
            deliversDamage);
    }

    /// <summary>
    ///     A ranged weapon whose chains carry something a client draws, like the NPC Charge Up and Channel
    ///     Fire template: the charge effect the attack ability applies is what a client draws the charge
    ///     from.
    /// </summary>
    private static NpcAttackProfile AnimatingProfile() => RangedProfile() with
    {
        AttackAbilityId = 39_249,
        ChargeUpMs = 2_000,
        ChainClientFeedback = true,
        ChainDeliversDamage = true,
    };

    /// <summary>
    ///     A melee weapon that fills only melee_ability_id: the fallback of the attack chain, used when
    ///     neither burst nor attack is named.
    /// </summary>
    private static NpcAttackProfile MeleeOnlyAbilityProfile() => AnimatingMeleeProfile() with
    {
        BurstAbilityId = 0,
        MeleeAbilityId = 188,
    };

    /// <summary>
    ///     A melee weapon whose chain is the swing itself, like Melee - Shadowstrike: the chain lands the
    ///     damage, so the AI must not add its own on the same target.
    /// </summary>
    private static NpcAttackProfile AnimatingMeleeProfile() => new()
    {
        Mode = NpcAttackMode.Melee,
        AttackRange = 3.5f,
        AttackRangeExit = 5f,
        StandoffRange = 2f,
        AttackIntervalMs = 2000,
        BurstDurationMs = 1600,
        DamagePerRound = 697,
        BurstAbilityId = 188,
        ChainClientFeedback = true,
        ChainDeliversDamage = true,
    };

    /// <summary>
    ///     A ranged weapon whose chains carry client feedback but no damage of their own, like the 39-slot
    ///     NPC Charge Sniper Rifle (template 51): the attack ability applies the charge state - a muzzle
    ///     flash, the charge sound and the combat flags - while the hit stays the AI's own volley.
    /// </summary>
    private static NpcAttackProfile FeedbackOnlyProfile() => RangedProfile() with
    {
        AttackAbilityId = 34_894,
        ChargeUpMs = 2_500,
        ChainClientFeedback = true,
    };

    [Fact]
    public void AnimatingNpc_RunsItsWeaponAbility_AndHandsItsChargeTimeToTheChain()
    {
        // The template's ms_chargeup is the charge the weapon describes, and the database's duration
        // commands read the register in seconds: 2,000 ms goes in as 2.0 so the charge effect the chain
        // applies lasts 2,000 ms rather than the command's fallback.
        var stats = new FakeAiMonsterStats { AttackProfile = AnimatingProfile() };
        var abilities = new FakeNpcAbilityActivator();
        var (shard, npc, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        var activation = Assert.Single(abilities.Activations);
        Assert.Same(npc, activation.Npc);
        Assert.Equal(39_249u, activation.AbilityId);
        Assert.Equal(60_050u, activation.Time);
        Assert.Equal(2f, activation.Register);
    }

    [Fact]
    public void MeleeNpc_WithOnlyAMeleeAbility_RunsThatAbility()
    {
        // melee_ability_id is the fallback of the attack chain: a weapon that fills neither burst nor
        // attack still animates from the melee hook, and a chain that delivers the hit still replaces
        // the AI's own swing.
        var stats = new FakeAiMonsterStats { AttackProfile = MeleeOnlyAbilityProfile() };
        var abilities = new FakeNpcAbilityActivator();
        var (shard, _, player) = CreateWorld(
            Vector3.Zero,
            new Vector3(3f, 0f, 0f),
            monsterStats: stats,
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Equal(188u, Assert.Single(abilities.Activations).AbilityId);
        Assert.Empty(shard.AiAttackFeedback.Attacks);
        Assert.Equal(100_000, player.CurrentHealth);
    }

    [Fact]
    public void MeleeNpc_PrefersBurstOverMelee()
    {
        // A leftover melee id must not steal the window from the burst the template names as the
        // attack (Shadowstrike's 188). Burst, then attack, then melee.
        var stats = new FakeAiMonsterStats
        {
            AttackProfile = AnimatingMeleeProfile() with { MeleeAbilityId = 999 },
        };
        var abilities = new FakeNpcAbilityActivator();
        var (shard, _, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(3f, 0f, 0f),
            monsterStats: stats,
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Equal(188u, Assert.Single(abilities.Activations).AbilityId);
    }

    [Fact]
    public void WeaponChainThatDeliversTheHit_ReplacesTheAiHit()
    {
        // The chain applies the effect and inflicts its own damage, so the AI's direct hit would be a
        // second one on the same target: the mob's damage is the DB chain's, not the engine's.
        var stats = new FakeAiMonsterStats { AttackProfile = AnimatingMeleeProfile() };
        var abilities = new FakeNpcAbilityActivator();
        var (shard, _, player) = CreateWorld(
            Vector3.Zero,
            new Vector3(3f, 0f, 0f),
            monsterStats: stats,
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Equal(188u, Assert.Single(abilities.Activations).AbilityId);
        Assert.Empty(shard.AiAttackFeedback.Attacks);
        Assert.Equal(100_000, player.CurrentHealth);
    }

    [Fact]
    public void WeaponChainThatFailsToRun_LeavesTheAiItsOwnHit()
    {
        // A shard whose aptitude system rejects the ability (or no ability system at all) must not turn
        // the mob harmless: the engine falls back to the damage it would have applied on its own.
        var stats = new FakeAiMonsterStats { AttackProfile = AnimatingMeleeProfile() };
        var abilities = new FakeNpcAbilityActivator { Result = false };
        var (shard, _, player) = CreateWorld(
            Vector3.Zero,
            new Vector3(3f, 0f, 0f),
            monsterStats: stats,
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Single(abilities.Activations);
        Assert.Equal(100_000 - 697, player.CurrentHealth);
        Assert.Equal(697, Assert.Single(shard.AiAttackFeedback.Attacks).Damage);
    }

    [Fact]
    public void FeedbackOnlyChain_RunsTheAbility_AndKeepsTheAiItsOwnHit()
    {
        // The chain draws the weapon's charge (particles, audio, the charge state) but delivers no damage,
        // so the ability runs and the AI's own volley goes out as well: the mob both charges and shoots.
        var stats = new FakeAiMonsterStats { AttackProfile = FeedbackOnlyProfile() };
        var abilities = new FakeNpcAbilityActivator();
        var shots = new RecordingAiProjectileLauncher();
        var (shard, _, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: shots,
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Equal(34_894u, Assert.Single(abilities.Activations).AbilityId);
        Assert.Equal(3, shots.Shots.Count);
    }

    [Fact]
    public void WeaponWithoutClientFeedback_RunsNoAbility()
    {
        // The chains of the database's other weapons carry nothing a client draws (stat modifiers, charge
        // states with no feedback, requirements), and their functional effects are not this engine's
        // business yet - the AI's own attack stays exactly as it was.
        var stats = new FakeAiMonsterStats { AttackProfile = RangedProfile() };
        var abilities = new FakeNpcAbilityActivator();
        var shots = new RecordingAiProjectileLauncher();
        var (shard, _, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(20f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: shots,
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Empty(abilities.Activations);
        Assert.Equal(3, shots.Shots.Count);

        // The shots are the AI's own, exactly as before this change (the recording launcher stands in for
        // ProjectileSim, so the player takes no damage here - the same shot count the ranged test asserts).
        Assert.Empty(shard.AiAttackFeedback.Attacks);
    }

    [Fact]
    public void RestrictedCharacter_DoesNotMoveOrUseItsWeapon()
    {
        // A charge-up effect sets restrict_movement, restrict_abilities and restrict_melee while it
        // lasts, and the AI reads those replicated flags rather than inventing a charge state of its own:
        // the mob holds position (inside its range but past its standoff, so it was walking) and the
        // attack window that comes up while it is restricted does not fire.
        var stats = new FakeAiMonsterStats { AttackProfile = AnimatingProfile() };
        var abilities = new FakeNpcAbilityActivator();
        var shots = new RecordingAiProjectileLauncher();
        var (shard, npc, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(24f, 0f, 0f),
            monsterStats: stats,
            projectileLauncher: shots,
            abilityActivator: abilities);

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);            // first attack at 60,050, then walking to the standoff
        var walkingPosition = npc.Position;
        Assert.Equal((short)0x5004, npc.MovementState); // Walking | Movement
        Assert.Single(abilities.Activations);

        npc.SetCombatFlags(new CombatFlagsData
        {
            Value = CombatFlagsData.CharacterCombatFlags.restrict_movement
                    | CombatFlagsData.CharacterCombatFlags.restrict_abilities
                    | CombatFlagsData.CharacterCombatFlags.restrict_melee,
            Time = (uint)shard.CurrentTimeLong,
        });

        Tick(shard, FirstTick + (Step * 2));
        Assert.Equal(walkingPosition, npc.Position);
        Assert.Equal((short)0x1000, npc.MovementState); // Standing: held in place by the effects

        Tick(shard, FirstTick + 2_500 + Step);    // the weapon's next cadence: still restricted
        Assert.Equal(walkingPosition, npc.Position);

        // The attack it could not fire is gone, not queued: the flags are the only thing stopping it,
        // exactly as they stop a player.
        Assert.Single(abilities.Activations);
        Assert.Empty(shots.Shots);
    }

    [Fact]
    public void MeleeNpc_WithNoMagazine_NeverReloads()
    {
        // The Spyder's template is a one-round melee clip: nothing to empty, so the mob swings forever and
        // never announces a reload it could not play.
        var stats = new FakeAiMonsterStats
        {
            AttackProfile = new NpcAttackProfile
            {
                Mode = NpcAttackMode.Melee,
                AttackRange = 3.5f,
                AttackRangeExit = 5f,
                StandoffRange = 2f,
                AttackIntervalMs = 2000,
                BurstDurationMs = 1600,
                DamagePerRound = 697,
                MagazineSize = 1,
                AmmoPerBurst = 1,
                ReloadTimeMs = 0,
            },
        };
        var (shard, npc, player) = CreateWorld(Vector3.Zero, new Vector3(3f, 0f, 0f), monsterStats: stats);
        uint reloadedAtSpawn = npc.Character_CombatView.WeaponReloadedProp;
        uint cancelledAtSpawn = npc.Character_CombatView.WeaponReloadCancelledProp;

        for (ulong time = FirstTick; time < FirstTick + 10_000; time += Step)
        {
            Tick(shard, time);
        }

        Assert.True(player.CurrentHealth < 100_000, "the melee NPC should have kept swinging");
        Assert.Equal(reloadedAtSpawn, npc.Character_CombatView.WeaponReloadedProp);
        Assert.Equal(cancelledAtSpawn, npc.Character_CombatView.WeaponReloadCancelledProp);
    }

    /// <summary>
    ///     A monster whose behaviour string names an emote holds it: <c>dbcharacter::Monster.behavior</c> is
    ///     where 207 of the build's 3,109 rows name one (<c>AlertAndInteractive(emote="calm")</c> on 28 of
    ///     them, emote 1062), never a table column, and the emote is the same <c>EmoteRecord</c> row a
    ///     player performs, replicated on the NPC's own views so every client in range plays it.
    /// </summary>
    [Fact]
    public void NpcWithABehaviorEmote_PosesTheNamedEmote()
    {
        var effects = new RecordingEmoteEffectApplier();
        var (shard, npc, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(500f, 0f, 0f),
            monsterStats: new FakeAiMonsterStats { Behavior = "AlertAndInteractive(emote=\"calm\")" },
            emotes: CreateEmoteService(effects));

        Tick(shard, FirstTick);

        Assert.Equal((ushort)1062, npc.Emote.Id);
        Assert.Equal((uint)FirstTick, npc.Emote.Time);
        Assert.Empty(effects.Applied);
    }

    [Fact]
    public void NpcWithABehaviorEmote_DropsItWhenItFights()
    {
        var (shard, npc, player) = CreateWorld(
            Vector3.Zero,
            new Vector3(300f, 0f, 0f),
            monsterStats: new FakeAiMonsterStats { Behavior = "AlertAndInteractive(emote=\"calm\")" },
            emotes: CreateEmoteService(new RecordingEmoteEffectApplier()));

        Tick(shard, FirstTick);
        Assert.Equal((ushort)1062, npc.Emote.Id);

        // The behaviour set changes when the NPC engages, and the emote belongs to the idle set: the
        // offensive string of this monster (and of every monster but one) names none.
        shard.Damage.ApplyDamage(npc, 50, player);
        Tick(shard, FirstTick + Step);

        AssertState(shard, npc, AiBrainState.Chase);
        Assert.Equal((ushort)0, npc.Emote.Id);
    }

    [Fact]
    public void NpcWithATimedBehaviorEmote_HoldsItForTheBehaviorsOwnDuration()
    {
        var (shard, npc, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(500f, 0f, 0f),
            monsterStats: new FakeAiMonsterStats { Behavior = "AlertAndInteractive(emote=\"calm\",emoteDuration=2)" },
            emotes: CreateEmoteService(new RecordingEmoteEffectApplier()));

        Tick(shard, FirstTick);
        Assert.Equal((ushort)1062, npc.Emote.Id);

        Tick(shard, FirstTick + 1_000);
        Assert.Equal((ushort)1062, npc.Emote.Id);

        // Two seconds are up: the emote ends, and the set that still asks for it does not start it over.
        Tick(shard, FirstTick + 2_000);
        Assert.Equal((ushort)0, npc.Emote.Id);

        Tick(shard, FirstTick + 5_000);
        Assert.Equal((ushort)0, npc.Emote.Id);
    }

    [Fact]
    public void NpcWhoseBehaviorNamesAnEmoteTheTableDoesNotHave_PosesNothing()
    {
        // Monster 999's row names "waterplant01" and the emote table has "DELETEwaterplant01Delete" (1138)
        // - the shipped data's own typo. Resolving the name is where that ends: no substitution, no emote.
        var effects = new RecordingEmoteEffectApplier();
        var (shard, npc, _) = CreateWorld(
            Vector3.Zero,
            new Vector3(500f, 0f, 0f),
            monsterStats: new FakeAiMonsterStats { Behavior = "AlertAndLookAtPlayer(emote=\"waterplant01\", greetingSet=1197)" },
            emotes: CreateEmoteService(effects));

        Tick(shard, FirstTick);
        Tick(shard, FirstTick + Step);

        Assert.Equal((ushort)0, npc.Emote.Id);
        Assert.Empty(effects.Applied);
    }

    /// <summary>Records the emote effects an NPC's emote applied, so a test can assert on them.</summary>
    private sealed class RecordingEmoteEffectApplier : IEmoteEffectApplier
    {
        public List<(ulong EntityId, uint EffectId, uint Time)> Applied { get; } = [];

        public bool Apply(CharacterEntity target, uint effectId, uint time)
        {
            Applied.Add((target.EntityId, effectId, time));
            return true;
        }
    }

}
