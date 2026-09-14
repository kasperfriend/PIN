using System;
using System.Collections.Generic;
using System.Numerics;

namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     One patch of the zone's ground in the world population plan: a square of
///     <see cref="IWorldPopulationRules.CellSize"/> metres, with the habitat and level the data gave
///     it and the slots the planner filled it with. Cells are also the streaming unit - the service
///     activates the ones near a player and deactivates the ones nobody is near any more.
/// </summary>
public sealed class WorldPopulationCell
{
    public WorldPopulationCell(long key, int x, int y)
    {
        Key = key;
        X = x;
        Y = y;
    }

    /// <summary>Packed grid key (<see cref="MakeKey"/>), which is how the service indexes cells.</summary>
    public long Key { get; }

    /// <summary>Cell coordinate on the zone's X axis.</summary>
    public int X { get; }

    /// <summary>Cell coordinate on the zone's Y axis.</summary>
    public int Y { get; }

    /// <summary>
    ///     Centre of the walkable ground that landed in the cell, i.e. the average of its navigation
    ///     mesh face centroids. A cell is not a flat square of the world - it is the ground the zone
    ///     actually has inside that square - so its centre is the ground's centre, and its Z is a
    ///     height a body can start from.
    /// </summary>
    public Vector3 Center { get; set; }

    /// <summary>How many walkable surface points landed in the cell.</summary>
    public int SurfaceCount { get; set; }

    /// <summary>The kind of ground the anchors around the cell made it.</summary>
    public WorldPopulationHabitat Habitat { get; set; } = WorldPopulationHabitat.Wilderness;

    /// <summary>
    ///     Level the NPCs of this cell get: the level band of the area they are in, resolved like
    ///     every other NPC level in the server (<see cref="StaticDB.SDBUtils.ResolveNpcLevel"/>).
    /// </summary>
    public byte Level { get; set; }

    /// <summary>
    ///     The <c>dbzonemetadata::ChunkRecord</c> the cell's ground falls in, kept so a cell can be
    ///     refused by the chunk rules the zone was loaded with.
    /// </summary>
    public uint ChunkRecordId { get; set; }

    /// <summary>Difficulty the cell's slots have spent so far (see <see cref="IWorldPopulationRules.MaxDifficultyPerCell"/>).</summary>
    public int SpentDifficulty { get; set; }

    /// <summary>Whether the cell is currently activated, i.e. its NPCs are in the world.</summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     The horizontal direction the cell's NPCs face by default: towards the place that made the
    ///     cell what it is (a settlement's NPCs face their settlement, the Melding's face the
    ///     Melding), and a deterministic yaw for open field, where there is nothing to face. Each
    ///     slot turns a little away from it so a group does not stand in one formation.
    /// </summary>
    public Vector3 BaseFacing { get; set; } = Vector3.UnitY;

    /// <summary>The cell's slots, in the order the planner created them.</summary>
    public List<WorldPopulationSlot> Slots { get; } = [];

    /// <summary>Packs cell coordinates into a single key. Negative coordinates are fine.</summary>
    public static long MakeKey(int x, int y) => ((long)x << 32) | (uint)y;

    /// <summary>The X coordinate of a packed key.</summary>
    public static int KeyX(long key) => (int)(key >> 32);

    /// <summary>The Y coordinate of a packed key.</summary>
    public static int KeyY(long key) => unchecked((int)key);

    /// <summary>
    ///     The cell coordinate a world position falls in, for a grid of
    ///     <paramref name="cellSize"/> metre cells.
    /// </summary>
    public static (int X, int Y) CellIndexOf(Vector3 position, float cellSize) => (
        (int)MathF.Floor(position.X / cellSize),
        (int)MathF.Floor(position.Y / cellSize));
}
