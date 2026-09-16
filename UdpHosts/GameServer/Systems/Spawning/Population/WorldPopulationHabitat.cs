using System;

namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     The kind of ground a <c>dbcharacter::Monster</c> row belongs to. A cell of the world
///     population plan carries exactly one of these values; a monster row carries the set of
///     values it may be placed in (see <see cref="MonsterHabitatClassifier"/>), so a row that
///     fits more than one kind of ground can appear in any of them.
/// </summary>
/// <remarks>
///     The client database has no per-zone spawn table (that lived in the live server's spawn
///     groups, which never shipped in <c>clientdb.sd2</c>), so the habitats are the placement
///     signal that *is* in the data: what a monster's AI behaviour and faction say about where
///     the game put it. City wanderers and civilians only ever appear inside an outpost;
///     Melding creatures only ever appear at the Melding; everything else is field content.
/// </remarks>
[Flags]
public enum WorldPopulationHabitat
{
    /// <summary>No ground at all: the row is not world population (see the classifier's exclusions).</summary>
    None = 0,

    /// <summary>Open field: wildlife, wanderers, minibosses, roaming military.</summary>
    Wilderness = 1 << 0,

    /// <summary>
    ///     Inside an outpost's inhabited camp (see
    ///     <see cref="IWorldPopulationRules.OutpostSettlementRadius"/>), or at one of the zone's
    ///     authored deployables. Not the outpost's capture/control circle.
    /// </summary>
    Settlement = 1 << 1,

    /// <summary>At the Melding: its creatures, and the Chosen who come through it.</summary>
    Melding = 1 << 2,
}

/// <summary>Membership tests for <see cref="WorldPopulationHabitat"/>.</summary>
public static class WorldPopulationHabitatExtensions
{
    /// <summary>
    ///     Whether a monster row that fits <paramref name="monsterHabitats"/> may be placed in a
    ///     cell whose habitat is <paramref name="cellHabitat"/>.
    /// </summary>
    public static bool Accepts(this WorldPopulationHabitat monsterHabitats, WorldPopulationHabitat cellHabitat) =>
        cellHabitat != WorldPopulationHabitat.None && (monsterHabitats & cellHabitat) == cellHabitat;
}
