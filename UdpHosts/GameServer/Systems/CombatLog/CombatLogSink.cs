using AeroMessages.GSS;
using GameServer.Entities.Character;
using PrivateCombatLog = AeroMessages.GSS.Character.Event.PrivateCombatLog;
using PublicCombatLog = AeroMessages.GSS.Character.Event.PublicCombatLog;

namespace GameServer.Systems.CombatLog;

/// <inheritdoc cref="ICombatLogSink" />
public class CombatLogSink : ICombatLogSink
{
    /// <summary>
    ///     One statusfx row is 1 (source) + 1 (log type) + 4 (effect id) + 4 (time) bytes on the wire;
    ///     <see cref="CombatLogMessage.Bytes" /> prefixes the entries with their total byte count.
    /// </summary>
    private const ushort StatusFxRowWireBytes = 10;

    /// <inheritdoc />
    public void EmitApplyToOwner(CharacterEntity target, CombatLogRow.CombatSourceType source, uint effectId, uint time)
    {
        if (!TryGetOwnerChannel(target, out var channel))
        {
            return;
        }

        var row = new CombatLogRow
        {
            SourceType = source,
            LogType = CombatLogRow.CombatLogType.StatusFxAppliedEvent,
            Type11Data = new CombatLogType11Data { T11_StatusEffectId = effectId, T11_Time = time },
        };

        SendStatusFxRow(channel, target, row);
    }

    /// <inheritdoc />
    public void EmitRemoveFromOwner(CharacterEntity target, CombatLogRow.CombatSourceType source, uint effectId, uint serverTime)
    {
        if (!TryGetOwnerChannel(target, out var channel))
        {
            return;
        }

        var row = new CombatLogRow
        {
            SourceType = source,
            LogType = CombatLogRow.CombatLogType.StatusFxRemovedEvent,
            Type12Data = new CombatLogType12Data { T12_StatusEffectId = effectId, T12_Time = serverTime },
        };

        SendStatusFxRow(channel, target, row);
    }

    private static bool TryGetOwnerChannel(CharacterEntity target, out Channel channel)
    {
        channel = null;
        // Nobody to confirm to: NPCs, and the offline coverage whose fake players carry no channels.
        return target is { IsPlayerControlled: true } &&
               target.Player.NetChannels != null &&
               target.Player.NetChannels.TryGetValue(ChannelType.ReliableGss, out channel);
    }

    private static void SendStatusFxRow(Channel channel, CharacterEntity target, CombatLogRow row)
    {
        var message = new CombatLogMessage { Bytes = StatusFxRowWireBytes, Entries = [row] };
        channel.SendMessage(new PublicCombatLog { HaveData = 1, Data = message }, target.EntityId);
        channel.SendMessage(new PrivateCombatLog { HaveData = 1, Data = message }, target.EntityId);
    }
}
