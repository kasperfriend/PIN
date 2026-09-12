using System.Numerics;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Ai;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class NpcAttackResolverTests
{
    private const uint MonsterId = 276;
    private const uint WeaponId = 30_025;
    private const ushort AmmoId = 922;

    private static NpcAttackResolver CreateResolver(FakeNpcAttackDataSource data, IAiRules rules = null)
        => new(data, rules ?? new StandardAiRules());

    private static WeaponTemplateResult RangedTemplate() => new()
    {
        DebugName = "Main 30025 (Type 30 - NPC Guard Rifle)",
        FireType = 1,
        Range = 30f,
        DamagePerRound = 40,
        MsPerBurst = 100,
        RoundsPerBurst = 4,
        AmmoId = AmmoId,
        AnimArmedId = 1,
        AnimArmedPriority = 100,
        AnimFireType = 1,
        AnimReloadType = 7,
    };

    private static WeaponTemplateResult MeleeTemplate() => new()
    {
        DebugName = "Main 85189 (Type 9 - NPC Melee Medium (Spyder))",
        Range = 2.6f,
        DamagePerRound = 225,
        MsPerBurst = 1600,
        RoundsPerBurst = 1,
        AmmoId = 0,
        AnimArmedId = 1,
        AnimArmedPriority = 100,
        AnimFireType = 2,
        AnimReloadType = 4,
    };

    /// <summary>
    ///     A monster row, a weapon item and the level-45 scaling row. The muzzle offset is left at the
    ///     row's default (zero) here: <c>NpcAttackResolver</c> converts it from the static database's own
    ///     vector type, which is exercised by the ranged engine test that asserts the chest fallback.
    /// </summary>
    private static FakeNpcAttackDataSource DataWith(WeaponTemplateResult template)
    {
        var data = new FakeNpcAttackDataSource();
        data.Monsters[MonsterId] = new Monster
        {
            Id = MonsterId,
            Weapon1Id = WeaponId,
            Behavior = "Arch_MedRangedHumanoid_Attack(triggerPullTime=1500,fireRestDuration=1000,combatDist=20)",
        };
        data.WithWeapon(WeaponId, template);
        data.WeaponTemplateIds[WeaponId] = 30;
        data.Scalings[45] = new MonsterScaling { Level = 45, Health = 27_869, Damage = 13_934 };
        data.Ammos[AmmoId] = new Ammo { Id = AmmoId, ProjectileSpeed = 40f, ImpactRadius = 0.5f, MaxRadius = 1.5f, Flags = 1 };
        return data;
    }

    [Fact]
    public void Resolve_UnknownMonster_IsUnarmed()
    {
        var profile = CreateResolver(new FakeNpcAttackDataSource()).Resolve(999, 1);

        Assert.Same(NpcAttackProfile.Unarmed, profile);
        Assert.False(profile.HasWeapon);
    }

    [Fact]
    public void Resolve_WeaponWithoutTemplateRow_IsUnarmed()
    {
        var data = DataWith(null);
        data.Weapons.Remove(WeaponId);

        Assert.False(CreateResolver(data).Resolve(MonsterId, 45).HasWeapon);
    }

    [Fact]
    public void Resolve_FallsBackToTheSecondaryWeaponSlot()
    {
        var data = DataWith(MeleeTemplate());
        data.Monsters[MonsterId].Weapon1Id = 0;
        data.Monsters[MonsterId].Weapon2Id = WeaponId;

        var profile = CreateResolver(data).Resolve(MonsterId, 45);

        Assert.True(profile.HasWeapon);
        Assert.Equal(WeaponId, profile.WeaponId);
        Assert.Equal(30u, profile.WeaponTypeId);
    }

    [Fact]
    public void Resolve_RangedWeapon_ReadsDamageCadenceRangeAndAmmo()
    {
        var data = DataWith(RangedTemplate());
        data.WithAttribute(WeaponId, 1145, 0.0217f);

        var profile = CreateResolver(data).Resolve(MonsterId, 45);

        Assert.Equal(NpcAttackMode.Ranged, profile.Mode);
        Assert.Equal(302, profile.DamagePerRound);                 // 13,934 x 0.0217
        Assert.Equal(4, profile.RoundsPerBurst);
        Assert.Equal(2500u, profile.AttackIntervalMs);             // triggerPullTime + fireRestDuration
        Assert.Equal(30f, profile.Range);
        Assert.Equal(AmmoId, profile.AmmoId);
        Assert.NotNull(profile.Ammo);
        Assert.Equal(40f, profile.ProjectileSpeed);
        Assert.Equal(0.5f, profile.ImpactRadius);
        Assert.Equal(1.5f, profile.MaxRadius);
        Assert.Equal(20f, profile.StandoffRange);
        Assert.Equal(30f, profile.AttackRange);
        Assert.Equal(34.5f, profile.AttackRangeExit, 3);
        Assert.Equal("Main 30025 (Type 30 - NPC Guard Rifle)", profile.WeaponName);

        // The weapon's animation data, straight from the template's anim_* columns: the window the
        // engine marks its attack animation with (ms_per_burst here) and the client's animation selectors.
        Assert.Equal(100u, profile.BurstDurationMs);
        Assert.Equal(1, profile.ArmedAnimationId);
        Assert.Equal(100, profile.ArmedAnimationPriority);
        Assert.Equal(1, profile.FireAnimationType);
        Assert.Equal(7, profile.ReloadAnimationType);
        Assert.Equal(0, profile.ChargeAnimationType);
    }

    [Fact]
    public void Resolve_BurstDuration_PrefersMsBurstDurationOverMsPerBurst()
    {
        // 7 of the 85 templates NPCs use carry ms_burst_duration (the burst is fired over time); it is
        // the animation window there, ms_per_burst only the fallback.
        var data = DataWith(RangedTemplate());
        data.Weapons[WeaponId].Main.MsBurstDuration = 120;

        var profile = CreateResolver(data).Resolve(MonsterId, 45);

        Assert.Equal(120u, profile.BurstDurationMs);
    }

    [Fact]
    public void Resolve_MeleeSwing_AnimatesForTheWeaponsBurstCycle()
    {
        var data = DataWith(MeleeTemplate());

        var profile = CreateResolver(data).Resolve(MonsterId, 45);

        Assert.Equal(1600u, profile.BurstDurationMs);
        Assert.Equal(2, profile.FireAnimationType);
    }

    [Fact]
    public void Resolve_MeleeWeapon_KeepsTheTunedReachAndNeedsNoAmmo()
    {
        var data = DataWith(MeleeTemplate());
        data.WithAttribute(WeaponId, 1145, 0.05f);
        data.Monsters[MonsterId].Behavior = "Arch_AdditiveMelee_Base(triggerPullTime=1000,fireRestDuration=1000)";

        var profile = CreateResolver(data).Resolve(MonsterId, 45);

        Assert.Equal(NpcAttackMode.Melee, profile.Mode);
        Assert.Equal(697, profile.DamagePerRound);                 // 13,934 x 0.05
        Assert.Equal(2.6f, profile.Range);
        Assert.Equal(3.5f, profile.AttackRange);                   // the rules melee reach is slack on top
        Assert.Equal(5f, profile.AttackRangeExit);
        Assert.Equal(2000u, profile.AttackIntervalMs);
        Assert.Equal(0u, profile.AmmoId);
        Assert.Null(profile.Ammo);
    }

    [Fact]
    public void Resolve_ItemDamagePerRound_OverridesTheCreatureRating()
    {
        // A monster carrying a level-matched PvE weapon item: attribute 954 already encodes the level.
        var data = DataWith(RangedTemplate());
        data.WithAttribute(WeaponId, 954, 177f);

        var profile = CreateResolver(data).Resolve(MonsterId, 45);

        Assert.Equal(177, profile.DamagePerRound);
    }

    [Fact]
    public void Resolve_CreatureDamageModifier_MultipliesTheWeaponDamage()
    {
        var data = DataWith(RangedTemplate());
        data.WithAttribute(WeaponId, 1145, 0.0217f);
        data.MonsterAttributes[(MonsterId, 1144)] = new MonsterAttributeRange { MonsterId = MonsterId, AttributeId = 1144, Base = 1.5f };

        var profile = CreateResolver(data).Resolve(MonsterId, 45);

        Assert.Equal(1.5f, profile.CreatureDamageModifier);
        Assert.Equal(454, profile.DamagePerRound);
    }

    [Fact]
    public void Resolve_RangeAttribute_OverridesTheTemplateRange()
    {
        var data = DataWith(RangedTemplate());
        data.WithAttribute(WeaponId, 957, 80f);

        var profile = CreateResolver(data).Resolve(MonsterId, 45);

        Assert.Equal(80f, profile.Range);
        Assert.Equal(80f, profile.AttackRange);
        Assert.Equal(92f, profile.AttackRangeExit, 3);
    }

    [Fact]
    public void Resolve_AmmoStatAttribute_OverridesTheAmmoDefaults()
    {
        var data = DataWith(RangedTemplate());
        data.Ammos[AmmoId].ProjectileSpeedStat = 1644;
        data.WithAttribute(WeaponId, 1644, 120f);

        var profile = CreateResolver(data).Resolve(MonsterId, 45);

        Assert.Equal(120f, profile.ProjectileSpeed);
    }

    [Fact]
    public void Resolve_RangedWeaponWithoutAnAmmoRow_FightsAtMeleeReach()
    {
        var data = DataWith(RangedTemplate());
        data.Ammos.Remove(AmmoId);

        var profile = CreateResolver(data).Resolve(MonsterId, 45);

        Assert.Equal(NpcAttackMode.Melee, profile.Mode);
        Assert.Equal(0u, profile.AmmoId);
    }

    [Fact]
    public void Resolve_ShortRangeWeapon_IsMeleeEvenWithAnAmmoRow()
    {
        var data = DataWith(MeleeTemplate());
        data.Weapons[WeaponId].Main.AmmoId = AmmoId;

        var profile = CreateResolver(data).Resolve(MonsterId, 45);

        Assert.Equal(NpcAttackMode.Melee, profile.Mode);
    }

    [Fact]
    public void Resolve_WithoutBehaviourTiming_UsesTheWeaponCadence()
    {
        var data = DataWith(RangedTemplate());
        data.Monsters[MonsterId].Behavior = "GuardCityWanderer";

        var profile = CreateResolver(data).Resolve(MonsterId, 45);

        Assert.Equal(250u, profile.AttackIntervalMs);              // ms_per_burst 100 raised to the floor
    }

    [Fact]
    public void Resolve_BehaviourOnTheOffensiveColumn_IsReadToo()
    {
        var data = DataWith(RangedTemplate());
        data.Monsters[MonsterId].Behavior = "GuardCityWanderer";
        data.Monsters[MonsterId].BehaviorOffensive = "Arch_MedRangedHumanoid_Attack(triggerPullTime=2000,fireRestDuration=500)";

        var profile = CreateResolver(data).Resolve(MonsterId, 45);

        Assert.Equal(2500u, profile.AttackIntervalMs);
        Assert.Equal("Arch_MedRangedHumanoid_Attack", profile.Behavior.Name);
    }

    [Fact]
    public void Resolve_WithoutAMonsterScalingRow_UsesTheRulesFallbackDamage()
    {
        var data = DataWith(RangedTemplate());
        data.Scalings.Clear();
        data.WithAttribute(WeaponId, 1145, 0.0217f);
        var rules = new StandardAiRules { AttackDamage = 76 };

        var profile = CreateResolver(data, rules).Resolve(MonsterId, 45);

        // No rating to scale, so the flat per-attack fallback is used as-is.
        Assert.Equal(76, profile.DamagePerRound);
    }

    [Fact]
    public void Resolve_DefaultMuzzleOffset_IsZeroSoTheEngineUsesChestHeight()
    {
        var data = DataWith(RangedTemplate());

        var profile = CreateResolver(data).Resolve(MonsterId, 45);

        Assert.Equal(Vector3.Zero, profile.MuzzleOffset);
    }
}
