using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using GameServer.Data;
using GameServer.Entities.Character;
using Serilog;

namespace GameServer.Systems.Cheats;

/// <summary>
///     Cheat state and behaviour for testing the running shard, exposed through the
///     admin/debug commands (<c>setlevel</c>, <c>setframe</c>, <c>hp</c>, <c>dmg</c>,
///     <c>killaura</c>). Everything here is deliberately ephemeral — no persistence, no
///     ranking impact — and each player's state lives and dies with their connection.
/// </summary>
public class CheatService
{
    private const double KillAuraIntervalMs = 1000d;

    private static readonly ILogger Logger = Log.ForContext<CheatService>();

    private readonly IShard _shard;
    private readonly IDictionary<ulong, PlayerCheatState> _states = new ConcurrentDictionary<ulong, PlayerCheatState>();
    private readonly IDictionary<ulong, ulong> _nextKillAuraTickMs = new ConcurrentDictionary<ulong, ulong>();

    public CheatService(IShard shard)
    {
        _shard = shard;
    }

    /// <summary>Gets (creating on demand) the cheat state of a player.</summary>
    public PlayerCheatState GetState(INetworkPlayer player)
    {
        return _states.TryGetValue(player.PlayerId, out var state) ? state : (_states[player.PlayerId] = new PlayerCheatState());
    }

    /// <summary>Drops the cheat state when a player logs out.</summary>
    public void Forget(INetworkPlayer player)
    {
        _states.Remove(player.PlayerId);
        _nextKillAuraTickMs.Remove(player.PlayerId);
    }

    /// <summary>
    ///     Applies the player's damage cheat to an outgoing damage amount: the one-hit-kill
    ///     mode turns any damage into a guaranteed lethal hit (see
    ///     <see cref="PlayerCheatState.OneHitKill"/>), otherwise the amount is scaled by
    ///     <see cref="PlayerCheatState.DamageMultiplier"/>.
    /// </summary>
    public int ApplyOutgoingDamage(INetworkPlayer player, int damage)
    {
        if (player == null)
        {
            return damage;
        }

        var state = GetState(player);
        if (state.OneHitKill)
        {
            return int.MaxValue;
        }

        return state.DamageMultiplier == 1f ? damage : (int)MathF.Round(damage * state.DamageMultiplier);
    }

    /// <summary>
    ///     Applies the kill-aura cheat on the shard tick: once per second, every living
    ///     entity hostile to the player within the configured radius is killed through the
    ///     same damage pipeline a weapon hit uses.
    /// </summary>
    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        foreach (var player in _shard.Clients.Values)
        {
            if (player.CharacterEntity == null)
            {
                continue;
            }

            var state = GetState(player);
            if (!state.KillAura)
            {
                continue;
            }

            if (_nextKillAuraTickMs.TryGetValue(player.PlayerId, out var nextTick) && currentTime < nextTick)
            {
                continue;
            }

            _nextKillAuraTickMs[player.PlayerId] = currentTime + (ulong)KillAuraIntervalMs;

            var origin = player.CharacterEntity.Position;
            float radiusSq = state.KillAuraRadiusMeters * state.KillAuraRadiusMeters;

            foreach (var entity in _shard.Entities.Values)
            {
                if (entity is not CharacterEntity { IsPlayerControlled: false } npc)
                {
                    continue;
                }

                if (!npc.IsAlive)
                {
                    continue;
                }

                if ((npc.Position - origin).LengthSquared() > radiusSq)
                {
                    continue;
                }

                // Same faction check the faction stance fields use for hostile flags.
                if (npc.HostilityInfo.FactionId != 0
                    && player.CharacterEntity.HostilityInfo.FactionId == npc.HostilityInfo.FactionId)
                {
                    continue;
                }

                _shard.Damage.ApplyDamage(npc, npc.CurrentHealth, player.CharacterEntity);
            }
        }
    }
}
