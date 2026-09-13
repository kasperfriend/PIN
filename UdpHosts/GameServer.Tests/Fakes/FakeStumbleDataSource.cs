using System.Collections.Generic;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.Systems.Combat;

namespace GameServer.Tests.Fakes;

/// <summary>A stumble table the tests fill themselves.</summary>
public sealed class FakeStumbleDataSource : IStumbleDataSource
{
    public Dictionary<uint, Stumble> Stumbles { get; } = [];

    public Dictionary<uint, IReadOnlyList<StumbleDirection>> Directions { get; } = [];

    IReadOnlyDictionary<uint, Stumble> IStumbleDataSource.Stumbles => Stumbles;

    IReadOnlyDictionary<uint, IReadOnlyList<StumbleDirection>> IStumbleDataSource.Directions => Directions;
}
