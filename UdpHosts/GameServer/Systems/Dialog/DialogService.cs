using System.Collections.Generic;
using AeroMessages.Common;
using AeroMessages.GSS.Character.Event;
using AeroMessages.GSS.Generic;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.dbdialogdata;
using GameServer.Systems.Ai;

namespace GameServer.Systems.Dialog;

/// <summary>
///     Plays a <c>dbdialogdata::DialogScript</c> line. The client resolves text, sound, emote and
///     mouth movement from the id; the server sends <c>PlayDialogScriptMessage</c> (the same
///     message encounters already use) or the per-player <c>PrivateDialog</c>, applies the
///     line's <c>emote_id</c> when it names one, and walks <c>next_id</c> when the client
///     reports <c>NotifyDialogScriptComplete</c>.
/// </summary>
/// <remarks>
///     Interaction dialog is resolved from the original data in descending order of specificity:
///     an explicit behaviour <c>dialogScript=</c>, opening rows whose <c>character_type</c> is the
///     NPC, rows recorded for its <c>voice_set</c>, then a character-neutral shipped talk line.
///     This covers old greeting behaviours whose server-only greeting-set table did not survive
///     without manufacturing text or audio: every line still comes from Firefall's DialogScript.
///     The six <c>BattleChatterDescriptions</c> rows describe probabilities and a default line,
///     not which combat event plays which description, so combat chatter still requires a caller
///     to name a description id.
/// </remarks>
public sealed class DialogService
{
    private readonly IDialogDataSource _data;

    public DialogService(IDialogDataSource data)
    {
        _data = data;
    }

    /// <summary>
    ///     The production service: the loaded dialog tables.
    /// </summary>
    public static DialogService Production { get; } = new(new SdbDialogDataSource());

    /// <summary>
    ///     Plays <paramref name="dialogId" /> from <paramref name="speaker" />. A public line goes
    ///     to every client the speaker is scoped into; a private line goes to
    ///     <paramref name="listener" /> (or to every scoped client when the listener is omitted).
    ///     The speaker remembers the id so <see cref="OnScriptComplete" /> can walk
    ///     <c>next_id</c>.
    /// </summary>
    public bool Play(CharacterEntity speaker, uint dialogId, uint time, CharacterEntity listener = null)
    {
        if (speaker == null || dialogId == 0)
        {
            return false;
        }

        var script = _data?.GetScript(dialogId);
        if (script == null)
        {
            return false;
        }

        if (script.EmoteId != 0)
        {
            speaker.PerformEmote((ushort)script.EmoteId, time);
        }

        speaker.CurrentDialogId = dialogId;

        if (script.IsPublic != 0)
        {
            SendPublic(speaker, dialogId);
        }
        else
        {
            SendPrivate(speaker, dialogId, time, listener);
        }

        return true;
    }

    /// <summary>
    ///     Starts the NPC's original interaction conversation. Explicit <c>dialogScript=</c> rows
    ///     retain their exact authored line. Every other talkable NPC walks its character-specific
    ///     opening lines, then voice-set lines, then the shipped character-neutral talk lines.
    ///     Repeated interactions rotate through the available openings instead of repeating one bark.
    /// </summary>
    public bool TryPlayInteractionDialog(CharacterEntity npc, CharacterEntity listener, uint time)
    {
        if (npc == null || npc.IsPlayerControlled)
        {
            return false;
        }

        uint characterType = npc.StaticInfo.CharacterTypeId;
        uint explicitId = NpcBehaviorParams.Parse(_data?.GetMonsterBehavior(characterType)).DialogScriptId;
        if (explicitId != 0 && Play(npc, explicitId, time, listener))
        {
            RememberSpeaker(npc, listener);
            return true;
        }

        IReadOnlyList<DialogScript> characterLines = _data?.GetCharacterScripts(characterType) ?? [];
        IReadOnlyList<DialogScript> voiceLines = npc.StaticInfo.VoiceSet != 0
            ? _data?.GetVoiceSetScripts(npc.StaticInfo.VoiceSet) ?? []
            : [];
        IReadOnlyList<DialogScript> genericLines = _data?.GetGenericInteractionScripts() ?? [];

        // Speaking is preferable to a silent subtitle whenever the shipped client has a suitable
        // sound event. Preserve specificity among voiced choices, but use the original neutral talk
        // recording before falling back to character/voice rows which have text only.
        IReadOnlyList<DialogScript> candidates = HasAudibleLine(characterLines)
            ? characterLines
            : HasAudibleLine(voiceLines)
              ? voiceLines
              : HasAudibleLine(genericLines)
                ? genericLines
                : characterLines.Count != 0
                  ? characterLines
                  : voiceLines.Count != 0
                    ? voiceLines
                    : genericLines;

        var selected = SelectNextOpening(candidates, npc.LastInteractionDialogId);
        if (selected == null || !Play(npc, selected.Id, time, listener))
        {
            return false;
        }

        npc.LastInteractionDialogId = selected.Id;
        RememberSpeaker(npc, listener);
        return true;
    }

    /// <summary>Compatibility name for callers which only knew about explicit behaviour dialog.</summary>
    public bool TryPlayBehaviorDialog(CharacterEntity npc, CharacterEntity listener, uint time) =>
        TryPlayInteractionDialog(npc, listener, time);

    private static bool HasAudibleLine(IReadOnlyList<DialogScript> candidates)
    {
        if (candidates == null)
        {
            return false;
        }

        for (int index = 0; index < candidates.Count; index++)
        {
            if (candidates[index]?.SoundEventId != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static DialogScript SelectNextOpening(IReadOnlyList<DialogScript> candidates, uint currentId)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return null;
        }

        bool audibleOnly = HasAudibleLine(candidates);
        int currentIndex = -1;
        for (int index = 0; index < candidates.Count; index++)
        {
            if (candidates[index]?.Id == currentId)
            {
                currentIndex = index;
                break;
            }
        }

        for (int offset = 1; offset <= candidates.Count; offset++)
        {
            var candidate = candidates[(currentIndex + offset) % candidates.Count];
            if (candidate != null && (!audibleOnly || candidate.SoundEventId != 0))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void RememberSpeaker(CharacterEntity npc, CharacterEntity listener)
    {
        if (listener != null)
        {
            listener.CurrentDialogSpeakerId = npc.EntityId;
        }
    }

    /// <summary>
    ///     Walks <c>next_id</c> of the line the client just finished. <paramref name="completedId" />
    ///     is the first non-zero of the two unnamed uints on <c>NotifyDialogScriptComplete</c>.
    ///     No-op when that id is unknown or the line has no follow-up.
    /// </summary>
    public bool OnScriptComplete(CharacterEntity listener, uint completedId, uint time)
    {
        if (listener == null || completedId == 0)
        {
            return false;
        }

        var script = _data?.GetScript(completedId);
        if (script == null || script.NextId == 0)
        {
            return false;
        }

        CharacterEntity speaker = listener;
        if (listener.CurrentDialogSpeakerId != 0
            && listener.Shard?.Entities != null
            && listener.Shard.Entities.TryGetValue(listener.CurrentDialogSpeakerId, out var entity)
            && entity is CharacterEntity npc)
        {
            speaker = npc;
        }

        return Play(speaker, script.NextId, time, listener);
    }

    /// <summary>
    ///     Plays a battle-chatter description for one listener, honouring that listener's
    ///     relation probability and the speaker's voice set. The caller names the description;
    ///     the tables do not map combat events onto the six rows.
    /// </summary>
    public bool TryPlayChatter(
        CharacterEntity speaker,
        CharacterEntity listener,
        uint chatterId,
        BattleChatterRelation relation,
        float roll01,
        uint time)
    {
        if (speaker == null || listener == null)
        {
            return false;
        }

        var description = _data?.GetChatter(chatterId);
        if (description == null)
        {
            return false;
        }

        if (!DialogMath.Roll(DialogMath.ProbabilityFor(description, relation), roll01))
        {
            return false;
        }

        uint dialogId = DialogMath.ResolveChatterDialogId(
            description,
            speaker.StaticInfo.VoiceSet,
            _data.GetChatterSet(description.DialogScriptSetId));
        if (dialogId == 0)
        {
            return false;
        }

        listener.CurrentDialogSpeakerId = speaker.EntityId;
        return Play(speaker, dialogId, time, listener);
    }

    private static void SendPublic(CharacterEntity speaker, uint dialogId)
    {
        var message = new PlayDialogScriptMessage { DialogId = dialogId, Unk1 = [0] };
        var shard = speaker.Shard;
        if (shard?.EntityMan == null)
        {
            return;
        }

        foreach (var client in shard.Clients.Values)
        {
            if (client == null || !client.CanReceiveGSS || !shard.EntityMan.HasScopedInEntity(speaker.EntityId, client))
            {
                continue;
            }

            if (client.NetChannels.TryGetValue(ChannelType.ReliableGss, out var channel))
            {
                channel.SendMessage(message, speaker.EntityId);
            }
        }
    }

    private static void SendPrivate(CharacterEntity speaker, uint dialogId, uint time, CharacterEntity listener)
    {
        var message = new PrivateDialog
        {
            Time = time,
            Entity = speaker.AeroEntityId,
            DialogId = dialogId,
        };

        if (listener?.Player != null)
        {
            SendToPlayer(listener.Player, message, listener.EntityId);
            return;
        }

        var shard = speaker.Shard;
        if (shard?.EntityMan == null)
        {
            return;
        }

        foreach (var client in shard.Clients.Values)
        {
            if (client == null || !client.CanReceiveGSS || !shard.EntityMan.HasScopedInEntity(speaker.EntityId, client))
            {
                continue;
            }

            SendToPlayer(client, message, client.CharacterEntity?.EntityId ?? speaker.EntityId);
        }
    }

    private static void SendToPlayer(INetworkPlayer player, PrivateDialog message, ulong entityId)
    {
        if (player?.NetChannels != null && player.NetChannels.TryGetValue(ChannelType.ReliableGss, out var channel))
        {
            channel.SendMessage(message, entityId);
        }
    }
}
