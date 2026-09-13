using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

public class NpcWeaponOverchargeTests
{
    [Fact]
    public void ShouldActivate_WhenTheChargeCrossesTheDelay()
    {
        // Plasma 12129: 4000 ms charge, 2500 ms delay.
        Assert.True(NpcWeaponOvercharge.ShouldActivate(12_129, 2500, 4000));
        Assert.True(NpcWeaponOvercharge.ShouldActivate(12_129, 2500, 2500));
    }

    [Fact]
    public void ShouldActivate_IsFalseWhenTheChargeIsShortOrTheRowDoesNotOvercharge()
    {
        Assert.False(NpcWeaponOvercharge.ShouldActivate(12_129, 2500, 2499));
        Assert.False(NpcWeaponOvercharge.ShouldActivate(12_129, 0, 4000));
        Assert.False(NpcWeaponOvercharge.ShouldActivate(0, 2500, 4000));
    }
}
