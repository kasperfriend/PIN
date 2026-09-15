using GameServer;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

public class NpcInteractionProfileTests
{
    [Fact]
    public void Resolve_VendorId_WinsOverTalkBehavior()
    {
        // Quartermaster shape: stocks a vendor terminal and also greets with holster talk.
        var monster = new Monster
        {
            VendorId = 310,
            Behavior = "AlertAndInteractive(interactionType=\"HolsterTalk\",greetingSet=310)",
        };

        var profile = NpcInteractionProfile.Resolve(monster);

        Assert.NotNull(profile);
        Assert.Equal(InteractionType.Vendor, profile.Type);
        Assert.Equal(310u, profile.VendorId);
        Assert.Equal(NpcInteractionProfile.DefaultChannelDurationMs, profile.DurationMs);
    }

    [Fact]
    public void Resolve_ExplicitVendorType_ReadsTheVendorId()
    {
        var monster = new Monster
        {
            VendorId = 625,
            Behavior = "AlertAndInteractive(factions=\"Accord\",interactionType=\"Vendor\")",
        };

        var profile = NpcInteractionProfile.Resolve(monster);

        Assert.NotNull(profile);
        Assert.Equal(InteractionType.Vendor, profile.Type);
        Assert.Equal(625u, profile.VendorId);
    }

    [Fact]
    public void Resolve_InteractionTypeNone_IsNotInteractable()
    {
        var monster = new Monster
        {
            Behavior = "AlertAndInteractive(perceptionDist=6,interactionType=\"none\",greetingSet=236, lookAtTarget=1)",
        };

        Assert.Null(NpcInteractionProfile.Resolve(monster));
    }

    [Fact]
    public void Resolve_HolsterTalk_IsCaseInsensitiveAndInstant()
    {
        var monster = new Monster
        {
            Behavior = "AlertAndInteractive(interactionType=\"holsterTalk\")",
        };

        var profile = NpcInteractionProfile.Resolve(monster);

        Assert.NotNull(profile);
        Assert.Equal(InteractionType.Holstertalk, profile.Type);
        Assert.Equal(0u, profile.DurationMs);
    }

    [Fact]
    public void Resolve_GenericSpelling_ReadsGeneric()
    {
        var monster = new Monster
        {
            Behavior = "AlertAndInteractive(factions=\"Accord\", interactionType=\"GENERIC\" )",
        };

        var profile = NpcInteractionProfile.Resolve(monster);

        Assert.NotNull(profile);
        Assert.Equal(InteractionType.Generic, profile.Type);
        Assert.Equal(NpcInteractionProfile.DefaultChannelDurationMs, profile.DurationMs);
    }

    [Fact]
    public void Resolve_UseAbilityOnInteract_CarriesTheAbilityId()
    {
        // The database writes spaces around the '=' of abilityId; the parser absorbs them.
        var monster = new Monster
        {
            Behavior = "UseAbilityOnInteract(interactionType=\"HolsterTalk\", abilityId = 140662)",
        };

        var profile = NpcInteractionProfile.Resolve(monster);

        Assert.NotNull(profile);
        Assert.Equal(InteractionType.Holstertalk, profile.Type);
        Assert.Equal(140662u, profile.CompletedAbilityId);
    }

    [Fact]
    public void Resolve_InteractiveNameWithoutType_DefaultsToHolsterTalk()
    {
        var monster = new Monster
        {
            Behavior = "AlertAndInteractive(helloScript=\"MRU - Sonar On\")",
        };

        var profile = NpcInteractionProfile.Resolve(monster);

        Assert.NotNull(profile);
        Assert.Equal(InteractionType.Holstertalk, profile.Type);
    }

    [Fact]
    public void Resolve_CombatBehavior_IsNotInteractable()
    {
        var monster = new Monster
        {
            Behavior = "Arch_MedRangedHumanoid_Attack(triggerPullTime=1500,fireRestDuration=2000,reviveOn=1)",
        };

        Assert.Null(NpcInteractionProfile.Resolve(monster));
    }

    [Fact]
    public void Resolve_NoBehavior_IsNotInteractable()
    {
        Assert.Null(NpcInteractionProfile.Resolve(new Monster()));
        Assert.Null(NpcInteractionProfile.Resolve(null));
    }
}
