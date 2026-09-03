using System.Numerics;

namespace GameServer.StaticDB.Records.customdata;
public record class CharacterSpawn
{
    public uint Id { get; set; }
    public uint ZoneId { get; set; }

    public uint Type { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Orientation { get; set; }

    // 0 = keep the entity default (see CharacterEntity.InitFields)
    public int MaxHealth { get; set; }
    public int MaxShields { get; set; }
}
