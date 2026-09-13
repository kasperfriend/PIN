using System.Numerics;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Ai;
using GameServer.Systems.Aptitude;
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
        BaseClipSize = 20,
        AmmoPerBurst = 4,
        ReloadTime = 700,
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
        BaseClipSize = 1,
        ReloadTime = 0,
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
    public void Resolve_CarriesTheWeaponsEmptyClipAbilityAndWhatItsChainDoes()
    {
        // Template 12132 (Tesla Rifle 2.0) names 39239 as its clip_empty_ability; that ability applies effect
        // 10480, whose apply chain is a tfAudioFeedback and two tfParticleEffectAsset commands - so the hook is
        // something a client plays and the engine activates it. The trigger is not a guess: it is the moment the
        // magazine runs dry, which is the state the column is named for and the AI already tracks.
        var template = RangedTemplate();
        template.EmptyAbility = 39_239;
        template.BaseClipSize = 2;
        template.AmmoPerBurst = 1;
        var data = DataWith(template)
            .WithAbility(39_239, 1_263_441)
            .WithCommand(1_263_441, (ushort)CommandType.ActiveInitiation, next: 1_263_440)
            .WithCommand(1_263_440, (ushort)CommandType.ImpactApplyEffect, effectId: 10_480)
            .WithStatusEffect(10_480, applyChain: 1_263_445)
            .WithCommand(1_263_445, (ushort)CommandType.AudioFeedback);

        var profile = CreateResolver(data).Resolve(MonsterId, 45);

        Assert.Equal(39_239u, profile.ClipEmptyAbilityId);
        Assert.True(profile.ClipEmptyClientFeedback);
    }

    [Fact]
    public void Resolve_AServerOnlyEmptyClipAbility_IsCarriedButNotRunnable()
    {
        // Templates 11975 and 11971 name 35842, whose chain is RegisterTimedTriggerCommandDef alone.
        var template = RangedTemplate();
        template.EmptyAbility = 35_842;
        template.BaseClipSize = 5;
        template.AmmoPerBurst = 1;
        var data = DataWith(template)
            .WithAbility(35_842, 1_370_713)
            .WithCommand(1_370_713, (ushort)CommandType.RegisterTimedTrigger);

        var profile = CreateResolver(data).Resolve(MonsterId, 45);

        Assert.Equal(35_842u, profile.ClipEmptyAbilityId);
        Assert.False(profile.ClipEmptyClientFeedback);
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

        // The magazine the reload animation is announced from: the template's base_clip_size, the rounds one
        // attack spends (ammo_per_burst, not rounds_per_burst) and the reload_time the client plays it in.
        Assert.Equal(20, profile.MagazineSize);
        Assert.Equal(4, profile.AmmoPerBurst);
        Assert.Equal(700u, profile.ReloadTimeMs);
        Assert.True(profile.Reloads);

        // A template the test does not give a spread: the cone is 0 and the burst stays on the aim.
        Assert.Equal(0f, profile.SpreadPct);
        Assert.Equal(0f, profile.MinSpread);
        Assert.Equal(0f, profile.MaxSpread);
        Assert.Equal(0f, profile.StartingSpread);
    }

    [Fact]
    public void Resolve_RangedWeapon_CarriesTheWeaponsFirstShotCone()
    {
        // NPC Guard Rifle shape: min 2, max 8, starting 0.5, no attribute 958. The standing first-shot
        // cone is the midpoint of the band, the same number a player would get from the same row.
        var template = RangedTemplate();
        template.MinSpread = 2f;
        template.MaxSpread = 8f;
        template.StartingSpread = 0.5f;
        template.SlotIndex = 2;

        var profile = CreateResolver(DataWith(template)).Resolve(MonsterId, 45);

        Assert.Equal(2, profile.SlotIndex);
        Assert.Equal(2f, profile.MinSpread);
        Assert.Equal(8f, profile.MaxSpread);
        Assert.Equal(0.5f, profile.StartingSpread);
        Assert.Equal(5f, profile.SpreadPct);
    }

    [Fact]
    public void Resolve_WeaponSpreadAttribute_ScalesTheCone()
    {
        // Attribute 958 (Weapon Spread) of the weapon item scales both terms the way
        // WeaponSpreadProfile.Build scales them for a player: attr 4 against max 8 is a 0.5 scale.
        var template = RangedTemplate();
        template.MinSpread = 2f;
        template.MaxSpread = 8f;
        template.StartingSpread = 0.5f;
        var data = DataWith(template).WithAttribute(WeaponId, 958, 4f);

        Assert.Equal(2.5f, CreateResolver(data).Resolve(MonsterId, 45).SpreadPct);
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
    public void Resolve_MagazineSize_PrefersTheItemsMagazineAttribute()
    {
        // Attribute 956 (Weapon Magazine Size) of the weapon item overrides the template's base_clip_size,
        // the same way 957 overrides the template's range.
        var data = DataWith(RangedTemplate());
        data.WithAttribute(WeaponId, 956, 60);

        Assert.Equal(60, CreateResolver(data).Resolve(MonsterId, 45).MagazineSize);
    }

    [Fact]
    public void Resolve_MeleeSwing_DoesNotReload()
    {
        // A single-round clip without a reload_time: nothing for the NPC to announce.
        var profile = CreateResolver(DataWith(MeleeTemplate())).Resolve(MonsterId, 45);

        Assert.Equal(1, profile.MagazineSize);
        Assert.Equal(0u, profile.ReloadTimeMs);
        Assert.False(profile.Reloads);
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
        Assert.Equal(0f, profile.SpreadPct);                       // a melee row has no projectile to scatter
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

    [Fact]
    public void Resolve_WalksTheWeaponsAbilityChainsIntoTheProfile()
    {
        // NPC Charge Up and Channel Fire's shape: the attack ability applies the charge effect, whose
        // apply chain is the animation. The profile has to carry that, or the engine would never run the
        // ability and the mob would charge (and animate) nothing.
        var template = RangedTemplate();
        template.AttackAbility = 39_249;
        template.MsChargeUp = 2_000;
        var data = DataWith(template)
            .WithAbility(39_249, 1_038_403)
            .WithCommand(1_038_403, (ushort)CommandType.ImpactApplyEffect, next: 1_038_402, effectId: 10_496)
            .WithCommand(1_038_402, (ushort)CommandType.ReplenishEffectDuration)
            .WithStatusEffect(10_496, applyChain: 1_274_905, removeChain: 1_274_909)
            .WithCommand(1_274_905, (ushort)CommandType.AbilityAnimation)
            .WithCommand(1_274_909, (ushort)CommandType.FireProjectile);

        var profile = CreateResolver(data).Resolve(MonsterId, 45);

        Assert.Equal(39_249u, profile.AttackAbilityId);
        Assert.Equal(2_000u, profile.ChargeUpMs);
        Assert.True(profile.ChainClientFeedback);
        Assert.True(profile.ChainDeliversDamage);
    }

    [Fact]
    public void Resolve_WeaponWithoutAbilities_ReportsNoChainBehaviour()
    {
        // The Spyder's row (and every other weapon without ability ids): nothing to run, so the AI's own
        // attack stays the mob's only one.
        var profile = CreateResolver(DataWith(MeleeTemplate())).Resolve(MonsterId, 45);

        Assert.Equal(0u, profile.AttackAbilityId);
        Assert.Equal(0u, profile.BurstAbilityId);
        Assert.Equal(0u, profile.ChargeUpMs);
        Assert.False(profile.ChainClientFeedback);
        Assert.False(profile.ChainDeliversDamage);
    }
}
