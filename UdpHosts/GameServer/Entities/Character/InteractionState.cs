using System;

namespace GameServer.Entities.Character;

/// <summary>
///     The interaction a player is currently channelling on an interactable entity (E key held). The
///     aptitude flow around ability 187 ("Interact") records it when
///     <c>agsInteractionCompletionTimeCommandDef</c> runs inside the interaction effect's apply chain
///     and reads it back from <c>agsInteractionInProgressCommandDef</c> in the effect's duration chain,
///     so the channel completes exactly when the target's authored duration has elapsed - or is
///     cancelled early when the key is released or the target walks away.
/// </summary>
public sealed class InteractionState
{
    /// <summary>The entity id of the interactable the channel runs on, or 0 when it is unknown.</summary>
    public ulong TargetEntityId { get; set; }

    /// <summary>Shard time (ms) at which the channel started.</summary>
    public uint StartTimeMs { get; set; }

    /// <summary>Shard time (ms) at which the channel counts as completed.</summary>
    public uint CompletionTimeMs { get; set; }

    /// <summary>Whether the channel has run its full duration at the given shard time.</summary>
    public bool IsCompleted(uint now) => now >= CompletionTimeMs;

    /// <summary>
    ///     The channel's progress in percent at the given shard time, clamped to 0-100. Used by
    ///     <c>InteractionCompleted</c> when a channel is interrupted partway.
    /// </summary>
    public byte PercentAt(uint now)
    {
        if (now >= CompletionTimeMs)
        {
            return 100;
        }

        if (CompletionTimeMs <= StartTimeMs)
        {
            return 0;
        }

        long elapsed = (long)now - StartTimeMs;
        long total = (long)CompletionTimeMs - StartTimeMs;
        long percent = (elapsed * 100) / total;
        return (byte)Math.Clamp(percent, 0, 100);
    }
}
