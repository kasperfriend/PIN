using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.ProjectileSim;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class AmmoAbilityHooksTests
{
    [Fact]
    public void Columns_MapToTheNamedTriggers()
    {
        var ammo = new Ammo
        {
            AbilityId = 10,
            TouchAbilityId = 11,
            AirburstAbilityId = 12,
            PeriodAbilityId = 13,
            PeriodAbilityMs = 250,
        };

        Assert.Equal(10u, AmmoAbilityHooks.ImpactAbility(ammo));
        Assert.Equal(11u, AmmoAbilityHooks.TouchAbility(ammo));
        Assert.Equal(12u, AmmoAbilityHooks.AirburstAbility(ammo));
    }

    [Fact]
    public void TryPeriod_TicksEveryPeriodMsAndCatchesUp()
    {
        var ammo = new Ammo { PeriodAbilityId = 13, PeriodAbilityMs = 250 };
        uint last = 0;

        Assert.False(AmmoAbilityHooks.TryPeriod(ammo, 249, ref last, out _));
        Assert.True(AmmoAbilityHooks.TryPeriod(ammo, 250, ref last, out uint first));
        Assert.Equal(13u, first);
        Assert.Equal(250u, last);

        Assert.True(AmmoAbilityHooks.TryPeriod(ammo, 800, ref last, out uint second));
        Assert.Equal(500u, last);
        Assert.True(AmmoAbilityHooks.TryPeriod(ammo, 800, ref last, out uint third));
        Assert.Equal(750u, last);
        Assert.False(AmmoAbilityHooks.TryPeriod(ammo, 800, ref last, out _));
        Assert.Equal(13u, second);
        Assert.Equal(13u, third);
    }

    [Fact]
    public void TryPeriod_WithoutAPeriod_NeverFires()
    {
        uint last = 0;
        Assert.False(AmmoAbilityHooks.TryPeriod(new Ammo { PeriodAbilityId = 13 }, 1_000, ref last, out _));
        Assert.False(AmmoAbilityHooks.TryPeriod(new Ammo { PeriodAbilityMs = 250 }, 1_000, ref last, out _));
        Assert.False(AmmoAbilityHooks.TryPeriod(null, 1_000, ref last, out _));
    }

    [Fact]
    public void Activate_WithoutAnAbilitySystem_IsANoOp()
    {
        var shard = new FakeShard();
        var source = FakeCharacterFactory.Create(shard);

        AmmoAbilityHooks.Activate(shard, source, 10);
        AmmoAbilityHooks.Activate(null, source, 10);
        AmmoAbilityHooks.Activate(shard, null, 10);
        AmmoAbilityHooks.Activate(shard, source, 0);
    }
}
