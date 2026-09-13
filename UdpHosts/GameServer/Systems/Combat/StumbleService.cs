using System.Collections.Generic;
using System.Numerics;
using AeroMessages.GSS.Character.Event;
using GameServer.Entities.Character;
using GameServer.Systems.Emotes;
using CharacterCombatFlags = AeroMessages.GSS.Character.CombatFlagsData.CharacterCombatFlags;

namespace GameServer.Systems.Combat;

/// <summary>
///     Applies a named directional stumble. The tables name the animation, the status
///     effect, the cooldown and the four hit-direction substates; they do not name which
///     weapon or damage type causes a stumble, so a caller must pass an explicit
///     <c>dbcharacter::Stumble</c> id. Damage does not pick one.
/// </summary>
/// <remarks>
///     The victim-facing <c>Stumble</c> event is BaseController-scoped (ushort, ushort, byte
///     — unnamed in AeroMessages) and is sent only to the victim player. An NPC has no
///     BaseController, so its stumble is the status effect alone, which is what every
///     watching client plays the hit reaction from. <c>restrict_stumble</c> skips the whole
///     path.
/// </remarks>
public sealed class StumbleService
{
    private readonly IStumbleDataSource _data;
    private readonly IEmoteEffectApplier _effects;

    public StumbleService(IStumbleDataSource data, IEmoteEffectApplier effects = null)
    {
        _data = data;
        _effects = effects;
    }

    /// <summary>
    ///     The production service: the loaded stumble tables and the shard's ability system
    ///     for the status effect a stumble applies.
    /// </summary>
    public static StumbleService Production { get; } = new(new SdbStumbleDataSource(), new AbilitySystemEmoteEffectApplier());

    /// <summary>
    ///     Applies stumble <paramref name="stumbleId" /> to <paramref name="victim" /> if the
    ///     tables allow it. No-op when the victim is not a living character, the id is unknown,
    ///     <c>restrict_stumble</c> is set, or the cooldown has not run out.
    /// </summary>
    public bool TryStumble(CharacterEntity victim, Vector3 sourcePos, uint stumbleId, uint time)
    {
        if (victim == null || !victim.IsAlive || stumbleId == 0)
        {
            return false;
        }

        if (victim.HasCombatFlag(CharacterCombatFlags.restrict_stumble))
        {
            return false;
        }

        if (_data?.Stumbles == null || !_data.Stumbles.TryGetValue(stumbleId, out var stumble) || stumble == null)
        {
            return false;
        }

        if (!StumbleMath.CanApply(time, victim.LastStumbleTime, stumble.CooldownMs, stumble.OnlyOnce != 0, victim.HasStumbled(stumble.Id)))
        {
            return false;
        }

        byte substate = StumbleMath.AnimSubstate(victim.AimDirection, sourcePos - victim.Position);
        IReadOnlyList<StaticDB.Records.dbcharacter.StumbleDirection> directions = null;
        _data.Directions?.TryGetValue(stumble.Id, out directions);
        var direction = StumbleMath.DirectionFor(directions, substate);

        victim.MarkStumbled(stumble.Id, time);

        if (stumble.StatusfxId != 0)
        {
            _effects?.Apply(victim, stumble.StatusfxId, time);
        }

        SendVictimEvent(victim, stumble.AnimIndex, DurationOf(stumble, direction), direction?.AnimSubstate ?? substate);
        return true;
    }

    /// <summary>
    ///     The victim-facing <c>Stumble</c> event. AeroMessages leaves the three fields unnamed;
    ///     they are packed from the table columns that match the wire widths: <c>anim_index</c>,
    ///     <c>duration</c>, <c>anim_substate</c>.
    /// </summary>
    private static void SendVictimEvent(CharacterEntity victim, uint animIndex, uint duration, byte animSubstate)
    {
        var player = victim.Player;
        if (player?.NetChannels == null || !player.NetChannels.TryGetValue(ChannelType.ReliableGss, out var channel))
        {
            return;
        }

        channel.SendMessage(
            new Stumble
            {
                Unk1 = (ushort)animIndex,
                Unk2 = (ushort)duration,
                Unk3 = animSubstate,
            },
            victim.EntityId);
    }

    private static uint DurationOf(StaticDB.Records.dbcharacter.Stumble stumble, StaticDB.Records.dbcharacter.StumbleDirection direction)
    {
        if (direction != null && direction.Duration != 0)
        {
            return direction.Duration;
        }

        return stumble?.Duration ?? 0;
    }
}
