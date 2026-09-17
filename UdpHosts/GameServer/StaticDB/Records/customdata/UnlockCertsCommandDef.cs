namespace GameServer.StaticDB.Records.customdata;

public record UnlockCertsCommandDef : ICommandDef
{
    public uint Id { get; set; }
    public uint CertificateId { get; set; }
}
