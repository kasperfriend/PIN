namespace GameServer.StaticDB.Records.dbvisualrecords;

public record class Hardpoints
{
    /// <summary>
    ///     FauFau <c>HalfMatrix4x3</c> (12 half-floats). Stored as <c>object</c> so the loader
    ///     can <c>SetValue</c> whatever type FauFau actually materializes; <c>HardpointTransform</c>
    ///     reads the translation column.
    /// </summary>
    public object Transform { get; set; }

    public string Name { get; set; }

    public uint MeshAssetId { get; set; }
}
