using System.Numerics;

namespace GameServer.StaticDB.Records.customdata;
public record class CharacterSpawn
{
    public uint Id { get; set; }
    public uint ZoneId { get; set; }

    public uint Type { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Orientation { get; set; }

    /// <summary>
    ///     Authored monster level for this spawn (1..80, the <c>dbcharacter::MonsterScaling</c>
    ///     row key), mirroring how the live game's spawn flow carried the level for zones the
    ///     database did not tune. 0 = resolve from the zone (its level band, or
    ///     <c>SDBUtils.DefaultNpcLevel</c>), see <c>CharacterEntity.LoadMonster</c>.
    /// </summary>
    public byte Level { get; set; }

    // 0 = keep the entity default (see CharacterEntity.InitFields)
    public int MaxHealth { get; set; }
    public int MaxShields { get; set; }
}
