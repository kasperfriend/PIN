using GameServer.Entities;

namespace GameServer.Systems.Ai;

/// <summary>
///     Decides which entities an NPC considers worth attacking. Abstracted so the
///     engine can be exercised without a loaded static database.
/// </summary>
public interface IAiHostility
{
    bool IsHostile(IEntity attacker, IEntity target);
}
