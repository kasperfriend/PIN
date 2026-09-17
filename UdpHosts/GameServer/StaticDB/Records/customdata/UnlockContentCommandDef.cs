namespace GameServer.StaticDB.Records.customdata;

/// <summary>A generic unlock: <see cref="UnlockType" /> is an <c>UnlocksUpdate</c> group key ("certificate" by default).</summary>
public record UnlockContentCommandDef : ICommandDef
{
    public uint Id { get; set; }
    public string UnlockType { get; set; } = "certificate";
    public uint UnlockId { get; set; }
    public uint CertificateId { get; set; }
}
