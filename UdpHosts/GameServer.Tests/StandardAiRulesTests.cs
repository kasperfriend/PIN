using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

public class StandardAiRulesTests
{
    [Fact]
    public void Defaults_AreInternallyConsistent()
    {
        var rules = new StandardAiRules();

        // A target standing on the edge of the attack range must not flip the state
        // machine every tick, so the exit band has to sit outside the entry range.
        Assert.True(rules.AttackRangeExit >= rules.AttackRange);

        // Otherwise an NPC could be ordered to close in on a target it is not allowed to hit.
        Assert.True(rules.AttackRange >= rules.StandoffRange);
        Assert.True(rules.AggroRadius >= rules.StandoffRange);

        // A target noticed at the aggro radius must be reachable before the leash gives up.
        Assert.True(rules.LeashRadius >= rules.AggroRadius);
        Assert.True(rules.HomeArrivalRadius > 0f);
        Assert.True(rules.HomeArrivalRadius < rules.LeashRadius);

        Assert.True(rules.AttackDamage > 0);
        Assert.True(rules.AttackCooldownMs > 0);
        Assert.True(rules.TargetLostTimeoutMs > 0);
        Assert.True(rules.PerceptionIntervalMs > 0);
        Assert.True(rules.MovementIntervalMs > 0);

        Assert.True(AiSpeeds.IsTrusted(rules.DefaultMoveSpeed, rules));
        Assert.True(AiSpeeds.IsTrusted(rules.DefaultChaseSpeed, rules));
        Assert.True(rules.MinTrustedSpeed < rules.MaxTrustedSpeed);
        Assert.True(rules.Enabled);
    }
}
