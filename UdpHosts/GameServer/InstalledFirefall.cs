#nullable enable
namespace GameServer;

/// <summary>
///     Describes a detected Firefall client installation and the data paths derived from it.
/// </summary>
public sealed class InstalledFirefall
{
    /// <summary>
    ///     Root directory of the Firefall installation.
    /// </summary>
    public string Root { get; init; } = string.Empty;

    /// <summary>
    ///     Path to "clientdb.sd2" inside the "system\db" folder.
    /// </summary>
    public string StaticDBPath { get; init; } = string.Empty;

    /// <summary>
    ///     Path to the "system\maps" folder.
    /// </summary>
    public string MapsPath { get; init; } = string.Empty;

    /// <summary>
    ///     Path to the "system\assetdb" folder.
    /// </summary>
    public string AssetDBPath { get; init; } = string.Empty;
}
