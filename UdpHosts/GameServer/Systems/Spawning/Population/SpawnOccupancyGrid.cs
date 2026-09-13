using System;
using System.Collections.Generic;
using System.Numerics;

namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     The server-side half of spawn collision checking: a spatial hash of the bodies that are
///     already placed, so a planned position can be refused before anything is spawned.
/// </summary>
/// <remarks>
///     <para>
///         The physical half (does a body of this size fit at this spot without ending up inside the
///         terrain or inside another entity) is answered by
///         <see cref="Physics.PhysicsEngine.IsStandingVolumeClear"/>, which sees every body the
///         simulation knows. What it cannot see is a body that is planned but not spawned yet, and
///         asking it about a spot that the plan already handed to another slot is how a cell ends up
///         with its four NPCs stacked in one place the instant they all appear. This grid is that
///         bookkeeping: it is written when a slot is filled and cleared when the NPC goes away.
///     </para>
///     <para>
///         Distances are measured horizontally with a height window: two NPCs at clearly different
///         heights (a balcony and the street under it) are not in each other's way, and the exact
///         three dimensional answer is the physics check's job anyway.
///     </para>
/// </remarks>
public sealed class SpawnOccupancyGrid
{
    private readonly float _cellSize;
    private readonly Dictionary<(int X, int Y), List<Occupant>> _cells = [];
    private readonly Dictionary<ulong, Occupant> _byEntity = [];

    /// <summary>Largest radius registered so far, which is how far a query has to look.</summary>
    private float _maxRadius;

    /// <param name="cellSize">
    ///     Side length of the hash cells in metres. Want it in the same order as the distances being
    ///     asked about (a couple of body radii), not the planning cell size: too large and every
    ///     query walks a crowd, too small and it walks a map.
    /// </param>
    public SpawnOccupancyGrid(float cellSize)
    {
        _cellSize = cellSize > 0f ? cellSize : 1f;
    }

    /// <summary>How many bodies are registered.</summary>
    public int Count => _byEntity.Count;

    /// <summary>Registers a body. Re-registering an id moves it.</summary>
    public void Add(ulong entityId, Vector3 position, float radius)
    {
        if (entityId == 0)
        {
            return;
        }

        _ = Remove(entityId);

        var occupant = new Occupant(entityId, position, MathF.Max(radius, 0f));
        _byEntity[entityId] = occupant;
        _maxRadius = MathF.Max(_maxRadius, occupant.Radius);

        var key = CellIndex(position);
        if (!_cells.TryGetValue(key, out var occupants))
        {
            occupants = [];
            _cells[key] = occupants;
        }

        occupants.Add(occupant);
    }

    /// <summary>Unregisters a body. Returns whether it was registered.</summary>
    public bool Remove(ulong entityId)
    {
        if (entityId == 0 || !_byEntity.TryGetValue(entityId, out var occupant))
        {
            return false;
        }

        _byEntity.Remove(entityId);

        var key = CellIndex(occupant.Position);
        if (_cells.TryGetValue(key, out var occupants))
        {
            // The list holds copies of a readonly struct, so this is an identity search by entity id.
            for (int i = occupants.Count - 1; i >= 0; i--)
            {
                if (occupants[i].EntityId == entityId)
                {
                    occupants.RemoveAt(i);
                }
            }

            if (occupants.Count == 0)
            {
                _cells.Remove(key);
            }
        }

        return true;
    }

    /// <summary>
    ///     Whether a body of <paramref name="radius"/> can be placed at <paramref name="position"/>
    ///     without coming closer to a registered body than the two radii plus
    ///     <paramref name="minSeparation"/>.
    /// </summary>
    public bool IsAreaFree(Vector3 position, float radius, float minSeparation)
    {
        if (_byEntity.Count == 0)
        {
            return true;
        }

        int span = (int)MathF.Ceiling((radius + minSeparation + _maxRadius) / _cellSize);
        var center = CellIndex(position);

        for (int x = center.X - span; x <= center.X + span; x++)
        {
            for (int y = center.Y - span; y <= center.Y + span; y++)
            {
                if (!_cells.TryGetValue((x, y), out var occupants))
                {
                    continue;
                }

                foreach (var occupant in occupants)
                {
                    float required = radius + occupant.Radius + minSeparation;
                    float dx = occupant.Position.X - position.X;
                    float dy = occupant.Position.Y - position.Y;
                    if ((dx * dx) + (dy * dy) >= required * required)
                    {
                        continue;
                    }

                    // Close enough horizontally; only a body at a similar height is really in the way.
                    float heightWindow = (MathF.Max(radius, occupant.Radius) * 2f) + 1f;
                    if (MathF.Abs(occupant.Position.Z - position.Z) <= heightWindow)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>Forgets every body. Used when a plan is dropped.</summary>
    public void Clear()
    {
        _cells.Clear();
        _byEntity.Clear();
        _maxRadius = 0f;
    }

    private (int X, int Y) CellIndex(Vector3 position) => (
        (int)MathF.Floor(position.X / _cellSize),
        (int)MathF.Floor(position.Y / _cellSize));

    private readonly record struct Occupant(ulong EntityId, Vector3 Position, float Radius);
}
