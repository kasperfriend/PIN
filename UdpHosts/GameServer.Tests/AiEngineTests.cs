using System.Numerics;
using System.Threading;
using AeroMessages.GSS.Character;
using GameServer.Entities.Character;
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
        byte npcLevel = 0)
    {
        var shard = new FakeShard();
        if (rules != null || monsterStats != null)
        {
            shard.AI = new AiEngine(
                shard,
                shard.EventBus,
                rules ?? new StandardAiRules(),
                new AlwaysHostileAiHostility(),
                shard.AiAttackFeedback,
                monsterStats ?? new FakeAiMonsterStats());
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
}
