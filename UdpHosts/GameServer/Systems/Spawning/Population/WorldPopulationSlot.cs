using System.Numerics;

namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     One planned NPC in one planned cell: which monster row goes there, roughly where it stands,
///     which way it faces, and - once it is in the world - which entity it is. A slot is the unit
///     the streaming works in: it is filled when its cell is activated, refilled after its NPC died,
///     and emptied when its cell is deactivated.
/// </summary>
public sealed class WorldPopulationSlot
{
    public WorldPopulationSlot(WorldPopulationCell cell, WorldPopulationCandidate candidate, Vector3 anchor, Vector3 facing, int index)
    {
        Cell = cell;
        Candidate = candidate;
        Anchor = anchor;
        Facing = facing;
        Index = index;
    }

    /// <summary>The cell this slot belongs to.</summary>
    public WorldPopulationCell Cell { get; }

    /// <summary>The monster row this slot spawns.</summary>
    public WorldPopulationCandidate Candidate { get; }

    /// <summary>
    ///     The planned position: the cell's ground plus a deterministic jitter, so the NPCs of a cell
    ///     are spread over it instead of standing in one spot. The physical validation at spawn time
    ///     moves this onto the surface it belongs to and may refuse it entirely.
    /// </summary>
    public Vector3 Anchor { get; }

    /// <summary>
    ///     The horizontal direction the NPC faces when it appears. Derived from the plan, not random
    ///     per spawn, so a settlement's NPCs keep facing their settlement every time the cell is
    ///     activated.
    /// </summary>
    public Vector3 Facing { get; }

    /// <summary>Index of this slot inside its cell; part of the jitter and facing seeds.</summary>
    public int Index { get; }

    /// <summary>Entity id of the NPC that is in the world for this slot, or 0 when the slot is empty.</summary>
    public ulong EntityId { get; set; }

    /// <summary>
    ///     Shard time before which this slot may not be filled: its monster row's
    ///     <c>ai_spawn_delay_ms</c> after its cell was activated, and the respawn delay after its NPC
    ///     died. 0 when it may be filled now.
    /// </summary>
    public ulong NotBefore { get; set; }

    /// <summary>Consecutive placement failures. Reset when a spawn succeeds.</summary>
    public int Failures { get; set; }

    /// <summary>
    ///     Positions tried in the current round of <see cref="IWorldPopulationRules.MaxPlacementAttempts" />.
    ///     A round is one pass over the slot's own positions - its anchor first, then jittered ones -
    ///     and it is what decides whether the slot was refused by the ground (and is one failure
    ///     closer to being parked) or merely could not fit this time. The round may span several
    ///     population updates, because one update may only spend
    ///     <see cref="IWorldPopulationRules.PlacementAttemptsPerUpdate" /> placement attempts in
    ///     total: the slot picks the round up where it left off.
    /// </summary>
    public int AttemptsThisRound { get; set; }

    /// <summary>Whether any attempt of the current round was refused by the ground itself.</summary>
    public bool RoundRefusedByGround { get; set; }

    /// <summary>Whether any attempt of the current round was refused because something was in the way.</summary>
    public bool RoundRefusedForRoom { get; set; }

    /// <summary>
    ///     Starts a fresh placement round: the next attempt is the anchor again, and the refusal
    ///     flags are cleared. Called when a round ends (placed, refused, or given up on) and when a
    ///     slot re-enters the world after its cell or its NPC has been away.
    /// </summary>
    public void ResetPlacementRound()
    {
        AttemptsThisRound = 0;
        RoundRefusedByGround = false;
        RoundRefusedForRoom = false;
    }

    /// <summary>
    ///     Whether the slot has given up: its ground was refused often enough
    ///     (<see cref="IWorldPopulationRules.MaxPlacementFailures"/>) that retrying it is a spin
    ///     rather than a hope. Reported in the status so a zone whose plan does not fit its ground is
    ///     visible instead of silently short.
    /// </summary>
    public bool Parked { get; set; }
}
