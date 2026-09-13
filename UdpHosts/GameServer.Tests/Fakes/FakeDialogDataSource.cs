using System.Collections.Generic;
using GameServer.StaticDB.Records.dbdialogdata;
using GameServer.Systems.Dialog;

namespace GameServer.Tests.Fakes;

/// <summary>Dialog / battle-chatter tables the tests fill themselves.</summary>
public sealed class FakeDialogDataSource : IDialogDataSource
{
    public Dictionary<uint, DialogScript> Scripts { get; } = [];

    public Dictionary<uint, BattleChatterDescriptions> Chatters { get; } = [];

    public Dictionary<uint, List<BattleChatterSetParams>> ChatterSets { get; } = [];

    public Dictionary<uint, string> MonsterBehaviors { get; } = [];

    public DialogScript GetScript(uint dialogId) => Scripts.GetValueOrDefault(dialogId);

    public BattleChatterDescriptions GetChatter(uint chatterId) => Chatters.GetValueOrDefault(chatterId);

    public IReadOnlyList<BattleChatterSetParams> GetChatterSet(uint setId) =>
        ChatterSets.TryGetValue(setId, out var rows) && rows != null ? rows : [];

    public string GetMonsterBehavior(uint monsterTypeId) => MonsterBehaviors.GetValueOrDefault(monsterTypeId) ?? string.Empty;
}
