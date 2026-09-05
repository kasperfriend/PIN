using GameServer.Systems.Aptitude;
using Xunit;

namespace GameServer.Tests;

public class AbilityStateTests
{
    private const uint Now = 60_000;

    [Fact]
    public void StartCooldown_BlocksAbilityUntilReadyAgain()
    {
        var state = new AbilityState();

        state.StartCooldown(AbilityCooldownKind.Local, 75709, 0, 1000, Now);

        Assert.True(state.IsAbilityBlocked(75709, Now));
        Assert.False(state.IsAbilityBlocked(75709, Now + 1000));
    }

    [Fact]
    public void ResetCooldowns_ClearsLocalCategoryAndGlobalCooldowns()
    {
        var state = new AbilityState();
        state.StartCooldown(AbilityCooldownKind.Local, 75709, 0, 1000, Now);
        state.StartCooldown(AbilityCooldownKind.Category, 0, 9, 2000, Now);
        state.StartCooldown(AbilityCooldownKind.Global, 0, 0, 3000, Now);
        Assert.True(state.IsAbilityBlocked(75709, Now));
        Assert.True(state.IsCategoryBlocked(9, Now));
        Assert.True(state.IsGlobalBlocked(Now));

        int removed = state.ResetCooldowns();

        Assert.Equal(3, removed);
        Assert.Empty(state.Cooldowns);
        Assert.False(state.IsAbilityBlocked(75709, Now));
        Assert.False(state.IsCategoryBlocked(9, Now));
        Assert.False(state.IsGlobalBlocked(Now));
    }

    [Fact]
    public void ResetCooldowns_WithoutTrackedCooldowns_IsNoOp()
    {
        var state = new AbilityState();

        int removed = state.ResetCooldowns();

        Assert.Equal(0, removed);
        Assert.Empty(state.Cooldowns);
    }

    [Fact]
    public void ResetCooldowns_AllowsNewCooldownsToStartFresh()
    {
        var state = new AbilityState();
        state.StartCooldown(AbilityCooldownKind.Local, 75709, 0, 10_000, Now);
        Assert.True(state.IsAbilityBlocked(75709, Now));

        state.ResetCooldowns();
        var entry = state.StartCooldown(AbilityCooldownKind.Local, 75709, 0, 5000, Now + 1000);

        Assert.NotNull(entry);
        Assert.Single(state.Cooldowns);
        Assert.Equal(Now + 1000u, entry.ActivatedTime);
        Assert.Equal(Now + 6000u, entry.ReadyAgainTime);
        Assert.True(state.IsAbilityBlocked(75709, Now + 1000));
        Assert.False(state.IsAbilityBlocked(75709, Now + 6000));
    }
}
