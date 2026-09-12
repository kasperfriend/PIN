using System.Numerics;
using System.Threading;
using AeroMessages.GSS.Character;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Ai;
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
        IAiProjectileLauncher projectileLauncher = null)
    {
        var shard = new FakeShard();
        if (rules != null || monsterStats != null || projectileLauncher != null)
        {
            shard.AI = new AiEngine(
                shard,
                shard.EventBus,
                rules ?? new StandardAiRules(),
                new AlwaysHostileAiHostility(),
                shard.AiAttackFeedback,
                monsterStats ?? new FakeAiMonsterStats(),
                projectileLauncher);
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

        // Aimed at the player's chest.
        Assert.True(burst.Direction.X > 0.9f, "an NPC should aim at its target, not at the floor");
        Assert.Equal(100_000, player.CurrentHealth);

        // A burst is one attack: the weapon's own cadence, not one attack per tick.
        Tick(shard, FirstTick + (Step * 2));
        Assert.Equal(3, shots.Shots.Count);
        Tick(shard, FirstTick + 2500 + Step);
        Assert.Equal(6, shots.Shots.Count);
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
}
