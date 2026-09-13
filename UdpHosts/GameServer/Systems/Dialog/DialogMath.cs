using System.Collections.Generic;
using GameServer.StaticDB.Records.dbdialogdata;

namespace GameServer.Systems.Dialog;

/// <summary>
///     How a listener relates to the speaker of a battle-chatter bark. The three probability
///     columns of <c>dbdialogdata::BattleChatterDescriptions</c> are named for these relations.
/// </summary>
public enum BattleChatterRelation
{
    /// <summary>Same-faction ally of the speaker (<c>probability_ally</c>).</summary>
    Ally = 0,

    /// <summary>The player the speaker is fighting (<c>probability_targeted_player</c>).</summary>
    TargetedPlayer = 1,

    /// <summary>A hostile who is not the current target (<c>probability_hostile</c>).</summary>
    Hostile = 2,
}

/// <summary>
///     Pure dialog / battle-chatter rules from the <c>dbdialogdata</c> tables. The descriptions
///     name probabilities, a default line and a voice-set set; they do not name which combat
///     event plays which description.
/// </summary>
public static class DialogMath
{
    /// <summary>
    ///     The probability column of <paramref name="description" /> that matches
    ///     <paramref name="relation" />, or 0 when the description is missing.
    /// </summary>
    public static byte ProbabilityFor(BattleChatterDescriptions description, BattleChatterRelation relation)
    {
        if (description == null)
        {
            return 0;
        }

        return relation switch
        {
            BattleChatterRelation.Ally => description.ProbabilityAlly,
            BattleChatterRelation.TargetedPlayer => description.ProbabilityTargetedPlayer,
            BattleChatterRelation.Hostile => description.ProbabilityHostile,
            _ => 0,
        };
    }

    /// <summary>
    ///     Whether a bark with that probability (0-100) fires for a roll in <c>[0, 1)</c>. A
    ///     probability of 0 never fires; 100 always does.
    /// </summary>
    public static bool Roll(byte probability, float roll01)
    {
        return probability > 0 && roll01 >= 0f && roll01 < 1f && roll01 * 100f < probability;
    }

    /// <summary>
    ///     The dialog line a chatter description plays for a speaker with that voice set: a
    ///     <c>BattleChatterSetParams</c> row whose packed <c>voice_set_key</c> is
    ///     <c>(voiceSet &lt;&lt; 32) | setId</c> and whose <c>set_id</c> matches the description's
    ///     <c>dialog_script_set_id</c>, else the description's <c>default_dialog_script_id</c>.
    /// </summary>
    public static uint ResolveChatterDialogId(
        BattleChatterDescriptions description,
        uint voiceSet,
        IEnumerable<BattleChatterSetParams> setParams)
    {
        if (description == null)
        {
            return 0;
        }

        if (setParams != null && description.DialogScriptSetId != 0 && voiceSet != 0)
        {
            foreach (var row in setParams)
            {
                if (row == null || row.DialogId == 0)
                {
                    continue;
                }

                if (row.SetId == description.DialogScriptSetId && VoiceSetOf(row.VoiceSetKey) == voiceSet)
                {
                    return row.DialogId;
                }
            }
        }

        return description.DefaultDialogScriptId;
    }

    /// <summary>The high 32 bits of a packed <c>voice_set_key</c> (<c>dbcharacter::VoiceSet</c> id).</summary>
    public static uint VoiceSetOf(ulong voiceSetKey) => (uint)(voiceSetKey >> 32);

    /// <summary>The low 32 bits of a packed <c>voice_set_key</c> (the chatter set id).</summary>
    public static uint SetIdOf(ulong voiceSetKey) => (uint)voiceSetKey;
}
