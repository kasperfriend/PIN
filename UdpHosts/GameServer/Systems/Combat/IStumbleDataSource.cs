using System.Collections.Generic;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbcharacter;

namespace GameServer.Systems.Combat;

/// <summary>
///     The stumble tables as <see cref="StumbleService" /> reads them. Exists so the service
///     is unit tested against a fake database: the production implementation is a thin wrapper
///     around <see cref="SDBInterface" />.
/// </summary>
public interface IStumbleDataSource
{
    /// <summary>Every loaded <c>dbcharacter::Stumble</c> row, keyed by id. Empty when the table is not loaded.</summary>
    IReadOnlyDictionary<uint, Stumble> Stumbles { get; }

    /// <summary>
    ///     The <c>dbcharacter::StumbleDirection</c> rows of each stumble, keyed by
    ///     <c>stumble_id</c>. Empty when the table is not loaded.
    /// </summary>
    IReadOnlyDictionary<uint, IReadOnlyList<StumbleDirection>> Directions { get; }
}

/// <summary>The production <see cref="IStumbleDataSource" />: reads the loaded static database.</summary>
public sealed class SdbStumbleDataSource : IStumbleDataSource
{
    public IReadOnlyDictionary<uint, Stumble> Stumbles => SDBInterface.GetStumbles() ?? new Dictionary<uint, Stumble>();

    public IReadOnlyDictionary<uint, IReadOnlyList<StumbleDirection>> Directions => SDBInterface.GetStumbleDirections() ?? new Dictionary<uint, IReadOnlyList<StumbleDirection>>();
}
