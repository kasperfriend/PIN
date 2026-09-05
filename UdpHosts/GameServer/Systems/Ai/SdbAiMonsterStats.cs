using GameServer.StaticDB;

namespace GameServer.Systems.Ai;

/// <summary>Reads the movement speeds straight out of the monster table in <c>clientdb.sd2</c>.</summary>
public class SdbAiMonsterStats : IAiMonsterStats
{
    public (float NormalSpeed, float FastSpeed) GetSpeeds(uint characterTypeId)
    {
        var monster = SDBInterface.GetMonster(characterTypeId);
        if (monster == null)
        {
            return (0f, 0f);
        }

        return (monster.NormalSpeed, monster.FastSpeed);
    }
}
