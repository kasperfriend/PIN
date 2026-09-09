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

    [Fact]
    public void Defaults_AttackIsMelee()
    {
        var rules = new StandardAiRules();

        // PIN has no NPC projectiles: an attack is a direct damage call, so a monster may only
        // make one once it is standing next to what it is angry at. Anything beyond a few metres of
        // reach ("the mob shot me from 40 m away") is a bug, not a tuning choice, until the
        // projectile path for NPCs exists.
        Assert.True(rules.AttackRange is >= 1f and <= 5f, $"attack range {rules.AttackRange} m is not a melee reach");
        Assert.True(rules.AttackRangeExit <= rules.AttackRange + 3f, "the exit band may not turn melee into a ranged attack");

        // The reach the database gives the monster melee weapon rows (2.6-3 m) plus the slack of
        // two body radii - i.e. still about that order, not an order of magnitude more.
        Assert.True(rules.StandoffRange < rules.AttackRange);
    }

    [Fact]
    public void Defaults_HaveAHeightBandForBothAttackingAndAcquiring()
    {
        var rules = new StandardAiRules();

        // Attacking a target straight above or below the mob is what "they hit me through the
        // floor" means, so the band has to be small (a crate, a jump) but not zero.
        Assert.True(rules.MaxAttackHeightDelta > 0f);
        Assert.True(rules.MaxAttackHeightDelta <= rules.AttackRange + 1f);

        // Acquisition is deliberately far more permissive than the attack band - a mob may well
        // notice you on the balcony above it - but not unlimited either, or it locks onto a player
        // it can never reach and stands under them until the leash expires.
        Assert.True(rules.MaxAcquisitionHeightDelta > rules.MaxAttackHeightDelta);
    }

    [Fact]
    public void Defaults_UseASmallFractionOfTheMonsterDamageRating()
    {
        var rules = new StandardAiRules();

        // dbcharacter::MonsterScaling.damage is a level's damage rating, not a per-hit amount
        // (it is half of that level's health rating on every row of the table), so a swing has to
        // commit a fraction of it. See AiAttackDamage.
        Assert.True(rules.AttackDamageFraction is > 0f and <= 1f);

        // And the fraction has to actually matter: the fallback the engine uses when the database
        // has no row is a small per-swing number of its own, not a rating-scale figure like the
        // 180 it used to carry (a fifth of a fresh frame's whole health pool per punch).
        Assert.True(rules.AttackDamage <= 50);
    }
}
