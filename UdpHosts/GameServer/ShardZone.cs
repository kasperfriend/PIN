namespace GameServer;

/// <summary>
///     The single-zone gate: one shard simulates one zone (<see cref="IShard.ZoneId"/>), so every
///     gameplay lookup that matches world positions - entity scoping, NPC and turret target
///     acquisition, world population - must ignore players whose character sits in another zone.
///     The character selection screen is a zone picker, and a position from another map can only
///     ever coincide with this zone's by accident.
/// </summary>
public static class ShardZone
{
    /// <summary>
    ///     Whether the player counts as present in the shard's simulated zone. A client with no
    ///     zone yet (null: not placed anywhere) counts as here - there is no other zone to
    ///     attribute it to - so the gate only ever excludes players positively known to be
    ///     elsewhere.
    /// </summary>
    public static bool IsPlayerInZone(IShard shard, IPlayer player)
    {
        uint? zoneId = player.CurrentZone?.ID;
        return zoneId == null || zoneId.Value == shard.ZoneId;
    }
}
