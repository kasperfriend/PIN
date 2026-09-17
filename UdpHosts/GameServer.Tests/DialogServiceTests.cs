using AeroMessages.GSS.Character;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.dbdialogdata;
using GameServer.Systems.Dialog;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Dialog lines come from <c>dbdialogdata::DialogScript</c>. Interactions prefer an explicit
///     behaviour line, then character, voice-set and neutral talk roots. Battle chatter is played
///     only when a caller names a description: the tables do not map combat events onto the six rows.
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
    public void TryPlayInteractionDialog_RotatesCharacterSpecificOpenings()
    {
        var (npc, service, data) = Create();
        npc.SetStaticInfo(new StaticInfoData { CharacterTypeId = 502, VoiceSet = 1123, DisplayName = "talker" });
        var first = new DialogScript { Id = 100, CharacterType = 502 };
        var second = new DialogScript { Id = 200, CharacterType = 502 };
        data.Scripts[first.Id] = first;
        data.Scripts[second.Id] = second;
        data.CharacterScripts[502] = [first, second];
        var listener = LivingCharacter((FakeShard)npc.Shard);

        Assert.True(service.TryPlayInteractionDialog(npc, listener, 1_000));
        Assert.Equal(100u, npc.CurrentDialogId);
        Assert.True(service.TryPlayInteractionDialog(npc, listener, 2_000));
        Assert.Equal(200u, npc.CurrentDialogId);
        Assert.True(service.TryPlayInteractionDialog(npc, listener, 3_000));
        Assert.Equal(100u, npc.CurrentDialogId);
    }

    [Fact]
    public void TryPlayInteractionDialog_FallsBackFromMissingExplicitToVoiceSet()
    {
        var (npc, service, data) = Create();
        npc.SetStaticInfo(new StaticInfoData { CharacterTypeId = 620, VoiceSet = 1039, DisplayName = "vendor" });
        data.MonsterBehaviors[620] = "AlertAndInteractive(dialogScript=999999)";
        var voiced = new DialogScript { Id = 300, VoiceSet = 1039 };
        data.Scripts[voiced.Id] = voiced;
        data.VoiceSetScripts[1039] = [voiced];

        Assert.True(service.TryPlayInteractionDialog(npc, LivingCharacter((FakeShard)npc.Shard), 1_000));
        Assert.Equal(300u, npc.CurrentDialogId);
    }

    [Fact]
    public void TryPlayInteractionDialog_PrefersVoicedSetAndNeverRotatesOntoSilentSibling()
    {
        var (npc, service, data) = Create();
        npc.SetStaticInfo(new StaticInfoData { CharacterTypeId = 620, VoiceSet = 1039, DisplayName = "vendor" });
        var characterTextOnly = new DialogScript { Id = 100, CharacterType = 620, TextId = 1 };
        var voiceTextOnly = new DialogScript { Id = 200, VoiceSet = 1039, TextId = 2 };
        var voicedA = new DialogScript { Id = 300, VoiceSet = 1039, SoundEventId = 30 };
        var voicedB = new DialogScript { Id = 400, VoiceSet = 1039, SoundEventId = 40 };
        foreach (var line in new[] { characterTextOnly, voiceTextOnly, voicedA, voicedB })
        {
            data.Scripts[line.Id] = line;
        }

        data.CharacterScripts[620] = [characterTextOnly];
        data.VoiceSetScripts[1039] = [voiceTextOnly, voicedA, voicedB];
        var listener = LivingCharacter((FakeShard)npc.Shard);

        Assert.True(service.TryPlayInteractionDialog(npc, listener, 1_000));
        Assert.Equal(voicedA.Id, npc.CurrentDialogId);
        Assert.True(service.TryPlayInteractionDialog(npc, listener, 2_000));
        Assert.Equal(voicedB.Id, npc.CurrentDialogId);
        Assert.True(service.TryPlayInteractionDialog(npc, listener, 3_000));
        Assert.Equal(voicedA.Id, npc.CurrentDialogId);
    }

    [Fact]
    public void TryPlayInteractionDialog_UnvoicedDecorativeNpcUsesShippedGenericTalk()
    {
        var (npc, service, data) = Create();
        npc.SetStaticInfo(new StaticInfoData { CharacterTypeId = 742, VoiceSet = 0, DisplayName = "worker" });
        var generic = new DialogScript { Id = 24759, CharacterType = 0, EmoteId = 1275, SoundEventId = 3_468_137_823 };
        data.Scripts[generic.Id] = generic;
        data.GenericInteractionScripts.Add(generic);

        Assert.True(service.TryPlayInteractionDialog(npc, LivingCharacter((FakeShard)npc.Shard), 1_000));
        Assert.Equal(24759u, npc.CurrentDialogId);
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
