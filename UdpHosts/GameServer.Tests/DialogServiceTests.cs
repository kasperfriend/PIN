using AeroMessages.GSS.Character;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.dbdialogdata;
using GameServer.Systems.Dialog;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Dialog lines come from <c>dbdialogdata::DialogScript</c>. Battle chatter is played only
///     when a caller names a description: the tables do not map combat events onto the six rows.
///     The seven <c>dialogScript=</c> monster rows are interaction, not an AI register pose.
/// </summary>
public class DialogServiceTests
{
    [Fact]
    public void Play_UnknownId_IsIgnored()
    {
        var (speaker, service, data) = Create();
        data.Scripts[10] = new DialogScript { Id = 10 };

        Assert.False(service.Play(speaker, 99, 1_000));
        Assert.Equal(0u, speaker.CurrentDialogId);
    }

    [Fact]
    public void Play_RemembersTheLineSoCompleteCanWalkNextId()
    {
        var (speaker, service, data) = Create();
        data.Scripts[10] = new DialogScript { Id = 10, NextId = 11 };
        data.Scripts[11] = new DialogScript { Id = 11 };

        Assert.True(service.Play(speaker, 10, 1_000));
        Assert.Equal(10u, speaker.CurrentDialogId);

        var listener = LivingCharacter((FakeShard)speaker.Shard);
        listener.CurrentDialogSpeakerId = speaker.EntityId;
        Assert.True(service.OnScriptComplete(listener, 10, 2_000));
        Assert.Equal(11u, speaker.CurrentDialogId);
    }

    [Fact]
    public void OnScriptComplete_WithoutAFollowUp_IsFalse()
    {
        var (speaker, service, data) = Create();
        data.Scripts[10] = new DialogScript { Id = 10, NextId = 0 };

        Assert.False(service.OnScriptComplete(speaker, 10, 1_000));
        Assert.False(service.OnScriptComplete(speaker, 0, 1_000));
    }

    [Fact]
    public void TryPlayBehaviorDialog_PlaysTheNamedLineForAListener()
    {
        var (npc, service, data) = Create();
        npc.SetStaticInfo(new StaticInfoData { CharacterTypeId = 612, DisplayName = "vendor" });
        data.MonsterBehaviors[612] = "AlertAndInteractive(dialogScript=10551,interactionType=1)";
        data.Scripts[10551] = new DialogScript { Id = 10551 };
        var listener = LivingCharacter((FakeShard)npc.Shard);

        Assert.True(service.TryPlayBehaviorDialog(npc, listener, 3_000));
        Assert.Equal(10551u, npc.CurrentDialogId);
        Assert.Equal(npc.EntityId, listener.CurrentDialogSpeakerId);
    }

    [Fact]
    public void TryPlayBehaviorDialog_PlayerControlled_IsIgnored()
    {
        var (speaker, service, data) = Create();
        speaker.Player = new FakeNetworkPlayer(speaker.Shard);
        data.MonsterBehaviors[0] = "AlertAndInteractive(dialogScript=10551)";
        data.Scripts[10551] = new DialogScript { Id = 10551 };

        Assert.False(service.TryPlayBehaviorDialog(speaker, speaker, 1_000));
    }

    [Fact]
    public void TryPlayChatter_HonoursProbabilityAndTheVoiceSetLine()
    {
        var (speaker, service, data) = Create();
        speaker.SetStaticInfo(new StaticInfoData { VoiceSet = 9, DisplayName = "guard" });
        data.Chatters[1] = new BattleChatterDescriptions
        {
            Id = 1,
            DefaultDialogScriptId = 44_931,
            DialogScriptSetId = 1417,
            ProbabilityTargetedPlayer = 40,
        };
        data.ChatterSets[1417] =
        [
            new BattleChatterSetParams { SetId = 1417, VoiceSetKey = ((ulong)9 << 32) | 1417, DialogId = 200 },
        ];
        data.Scripts[200] = new DialogScript { Id = 200 };
        data.Scripts[44_931] = new DialogScript { Id = 44_931 };
        var listener = LivingCharacter((FakeShard)speaker.Shard);

        Assert.False(service.TryPlayChatter(speaker, listener, 1, BattleChatterRelation.TargetedPlayer, 0.50f, 1_000));
        Assert.True(service.TryPlayChatter(speaker, listener, 1, BattleChatterRelation.TargetedPlayer, 0.10f, 1_000));
        Assert.Equal(200u, speaker.CurrentDialogId);
    }

    private static (CharacterEntity Speaker, DialogService Service, FakeDialogDataSource Data) Create()
    {
        var shard = new FakeShard();
        var speaker = LivingCharacter(shard);
        var data = new FakeDialogDataSource();
        return (speaker, new DialogService(data), data);
    }

    private static CharacterEntity LivingCharacter(FakeShard shard)
    {
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        character.SetCharacterState(CharacterStateData.CharacterStatus.Living, 0);
        shard.Entities.Add(character.EntityId, character);
        return character;
    }
}
