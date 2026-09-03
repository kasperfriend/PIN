#nullable enable
using System.Text.Json.Serialization;

namespace GameServer;

/// <summary>
///     User-editable file-based settings loaded from GameServer.config.json.
///     Values here override the legacy App.config settings for installation paths.
/// </summary>
public sealed class GameServerConfigFile
{
    /// <summary>
    ///     File path to "clientdb.sd2" located in the "db" folder of the Firefall installation.
    /// </summary>
    [JsonPropertyName("StaticDBPath")]
    public string? StaticDBPath { get; set; }

    /// <summary>
    ///     Directory path to the "maps" folder of the Firefall installation.
    /// </summary>
    [JsonPropertyName("MapsPath")]
    public string? MapsPath { get; set; }

    /// <summary>
    ///     Directory path to the "assetdb" folder of the Firefall installation.
    /// </summary>
    [JsonPropertyName("AssetDBPath")]
    public string? AssetDBPath { get; set; }

    /// <summary>
    ///     Directory path for collision cache files (.bincache, .rbcache).
    /// </summary>
    [JsonPropertyName("CachePath")]
    public string? CachePath { get; set; }
}
