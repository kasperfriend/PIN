using System;
using Aero.Protocol;
using Serilog.Core;
using Serilog.Events;

namespace GameServer;

/// <summary>
///     Holds the settings for the server`
/// </summary>
public class GameServerSettings
{
    [Flags]
    public enum LogOutput
    {
        None = 0,
        Console = 1,
        Seq = 2,
        File = 4,
    }

    /// <summary>
    ///     The log level to use for the logger. Any messages below this level won't be printed to console.
    /// </summary>
    public LogEventLevel? LogLevel { get; set; } = LogEventLevel.Debug;

    /// <summary>
    ///     Comma separated list of log outputs to use: Console, Files, Seq
    /// </summary>
    public LogOutput LogOutputs { get; set; } = LogOutput.Console;

    public LoggingLevelSwitch LevelSwitch { get; set; } = new();

    /// <summary>
    ///    UDP port the game server should be listening on
    /// </summary>
    public ushort Port { get; set; } = 25001;

    /// <summary>
    ///    Firefall client version this server instance serves. Used to resolve the network protocol.
    /// </summary>
    public string ClientVersion { get; set; } = "1962";

    /// <summary>
    ///    Firefall client environment this server instance serves. Used to resolve the network protocol.
    /// </summary>
    public string ClientEnvironment { get; set; } = "production";

    /// <summary>
    ///    Firefall client branch this server instance serves. Used to resolve the network protocol.
    /// </summary>
    public string ClientBranch { get; set; } = "prod";

    /// <summary>
    ///    GSS protocol version resolved from <see cref="ClientVersion" />, <see cref="ClientBranch" />, and <see cref="ClientEnvironment" /> at startup
    /// </summary>
    public GssVersion GssProtocolVersion { get; set; } = GssVersion.V67;

    /// <summary>
    ///    Matrix protocol version resolved from <see cref="ClientVersion" />, <see cref="ClientBranch" />, and <see cref="ClientEnvironment" /> at startup
    /// </summary>
    public MatrixVersion MatrixProtocolVersion { get; set; } = MatrixVersion.V26;

    /// <summary>
    ///    Address to use to connect to RIN.InternalAPI for GRPC. If the connection fails, GRPC will not be used.
    /// </summary>
    public string GrpcChannelAddress { get; set; } = "http://localhost:5201";

    /// <summary>
    ///    File path to "clientdb.sd2" located in the "db" folder of the Firefall installation.
    ///    Configured through GameServer.config.json (or App.config / GameServer.dll.config as a fallback).
    /// </summary>
    public string StaticDBPath { get; set; } = string.Empty;

    /// <summary>
    ///    Directory path to the "maps" folder of the Firefall installation.
    ///    Configured through GameServer.config.json (or App.config / GameServer.dll.config as a fallback).
    /// </summary>
    public string MapsPath { get; set; } = string.Empty;

    /// <summary>
    ///    Directory path to the "assetdb" folder of the Firefall installation.
    ///    Configured through GameServer.config.json (or App.config / GameServer.dll.config as a fallback).
    /// </summary>
    public string AssetDBPath { get; set; } = string.Empty;

    /// <summary>
    ///    Directory path for collision cache files (.bincache, .rbcache). The cache can be pregenerated with the CollisionGenerator tool.
    /// </summary>
    public string CachePath { get; set; } = string.Empty;

    /// <summary>
    ///    The zone this shard simulates. One shard runs one zone: its collision, authored
    ///    entities, encounters, NPC AI and world population all belong to this zone, and
    ///    players anywhere else get an empty map (see Docs/SINGLE_ZONE.md). Default 448
    ///    (New Eden).
    /// </summary>
    public uint ZoneId { get; set; } = 448;

    /// <summary>
    ///    Enable loading zone collision data. Required for NPC ground snapping: without
    ///    the zone statics there is no terrain to ray cast against, so mobs stay at
    ///    their spawn height. Loading fails gracefully when <see cref="MapsPath" /> has
    ///    no zone file.
    /// </summary>
    public bool LoadMapsCollision { get; set; } = true;

    /// <summary>
    ///    Enable loading entities on zone startup
    /// </summary>
    public bool LoadZoneEntities { get; set; } = true;

    /// <summary>
    ///    Populate the zone with the monsters and NPCs the database says belong there, around the
    ///    players that are in it. Placement comes from the zone's own walkable collision, its
    ///    outposts/deployables/Melding perimeters and its chunk metadata; the roster is every
    ///    <c>dbcharacter::Monster</c> row that has something to render and a behaviour that is a
    ///    world inhabitant. Turn it off for an empty zone (only the authored
    ///    <c>character_spawn.json</c> entities remain), or at runtime with <c>\population off</c>.
    ///    See <c>Docs/WORLD_POPULATION.md</c>.
    /// </summary>
    public bool SpawnWorldPopulation { get; set; } = true;

    /// <summary>Hard ceiling on simultaneously live world-population NPCs.</summary>
    public int WorldPopulationMaxLiveNpcs { get; set; } = 150;

    /// <summary>Metres from a player within which planned cells activate.</summary>
    public float WorldPopulationActivationRadius { get; set; } = 150f;

    /// <summary>
    ///    Metres from a player beyond which active cells deactivate. A null, zero, or value no
    ///    greater than the activation radius retains the legacy 1.5x activation-radius fallback.
    /// </summary>
    public float? WorldPopulationDeactivationRadius { get; set; }

    /// <summary>Metres per planning and streaming cell.</summary>
    public float WorldPopulationCellSize { get; set; } = 32f;

    /// <summary>Maximum NPCs one planning cell may hold.</summary>
    public int WorldPopulationMaxNpcsPerCell { get; set; } = 4;

    /// <summary>Total encounter difficulty one cell may hold during density planning.</summary>
    public int WorldPopulationMaxDifficultyPerCell { get; set; } = 400;

    /// <summary>Difficulty charged to a row whose database difficulty is zero.</summary>
    public int WorldPopulationUnbudgetedDifficultyCost { get; set; } = 25;

    /// <summary>Maximum slots retained by a whole-zone population plan.</summary>
    public int WorldPopulationMaxPlannedSlots { get; set; } = 20_000;

    /// <summary>Maximum NPCs started in one population spawn-budget window.</summary>
    public int WorldPopulationSpawnBudget { get; set; } = 4;

    /// <summary>Length of a population spawn-budget window, in milliseconds.</summary>
    public int WorldPopulationSpawnBudgetWindowMs { get; set; } = 100;

    /// <summary>Minimum interval between population streaming updates, in milliseconds.</summary>
    public int WorldPopulationTickIntervalMs { get; set; } = 250;

    /// <summary>Navigation faces processed by the incremental planner per update.</summary>
    public int WorldPopulationPlanWorkPerTick { get; set; } = 20_000;

    /// <summary>Extra clearance in metres required between two population bodies.</summary>
    public float WorldPopulationMinSeparation { get; set; } = 0.5f;

    /// <summary>Minimum clearance in metres from a player before an NPC may be placed.</summary>
    public float WorldPopulationMinPlayerDistance { get; set; } = 25f;

    /// <summary>Placement positions tried by a slot during one placement round.</summary>
    public int WorldPopulationMaxPlacementAttempts { get; set; } = 6;

    /// <summary>Wait after a failed placement round, in milliseconds.</summary>
    public int WorldPopulationPlacementRetryDelayMs { get; set; } = 1_000;

    /// <summary>Failed placement rounds after which a slot is parked.</summary>
    public int WorldPopulationMaxPlacementFailures { get; set; } = 8;

    /// <summary>Base wait after an NPC dies before its slot can refill, in milliseconds.</summary>
    public int WorldPopulationRespawnDelayMs { get; set; } = 30_000;

    /// <summary>Smallest allowed Z component of a walkable surface normal.</summary>
    public float WorldPopulationMinimumWalkableNormalZ { get; set; } = 0.35f;

    /// <summary>Fallback NPC body radius in metres for database rows with an inherited radius.</summary>
    public float WorldPopulationDefaultBodyRadius { get; set; } = 0.7f;

    /// <summary>Fallback NPC body height in metres for database rows with an inherited height.</summary>
    public float WorldPopulationDefaultBodyHeight { get; set; } = 1.8f;

    /// <summary>Settlement influence radius in metres around an authored deployable.</summary>
    public float WorldPopulationDeployableInfluenceRadius { get; set; } = 25f;

    /// <summary>Melding influence radius in metres around a Melding control point.</summary>
    public float WorldPopulationMeldingInfluenceRadius { get; set; } = 120f;

    /// <summary>
    ///    Force reload zone from source files, bypassing cache.
    /// </summary>
    public bool ForceReloadZone { get; set; }

    /// <summary>
    ///    Batch multiple outgoing game messages into a single packet (up to the MTU budget).
    /// </summary>
    public bool BatchOutgoingPackets { get; set; } = true;
}