using System.Collections.Generic;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbdialogdata;

namespace GameServer.Systems.Dialog;

/// <summary>
///     The dialog / battle-chatter tables as <see cref="DialogService" /> reads them. Exists so
///     the service is unit tested against a fake database.
/// </summary>
public interface IDialogDataSource
{
    /// <summary>One <c>dbdialogdata::DialogScript</c> row, or null when the id is unknown.</summary>
    DialogScript GetScript(uint dialogId);

    /// <summary>One <c>dbdialogdata::BattleChatterDescriptions</c> row, or null when the id is unknown.</summary>
    BattleChatterDescriptions GetChatter(uint chatterId);

    /// <summary>The <c>dbdialogdata::BattleChatterSetParams</c> rows of a chatter set, or empty.</summary>
    IReadOnlyList<BattleChatterSetParams> GetChatterSet(uint setId);

    /// <summary>
    ///     The <c>dbcharacter::Monster.behavior</c> string of that monster type, or empty when the
    ///     type is unknown. The seven rows that name a <c>dialogScript=</c> are the only NPCs whose
    ///     own data points at a dialog line.
    /// </summary>
    string GetMonsterBehavior(uint monsterTypeId);
}

/// <summary>The production <see cref="IDialogDataSource" />: reads the loaded static database.</summary>
public sealed class SdbDialogDataSource : IDialogDataSource
{
    public DialogScript GetScript(uint dialogId) => SDBInterface.GetDialogScript(dialogId);

    public BattleChatterDescriptions GetChatter(uint chatterId) => SDBInterface.GetBattleChatter(chatterId);

    public IReadOnlyList<BattleChatterSetParams> GetChatterSet(uint setId) => SDBInterface.GetBattleChatterSet(setId);

    public string GetMonsterBehavior(uint monsterTypeId) => SDBInterface.GetMonster(monsterTypeId)?.Behavior ?? string.Empty;
}
