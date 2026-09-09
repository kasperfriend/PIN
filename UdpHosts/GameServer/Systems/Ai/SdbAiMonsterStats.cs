using GameServer.StaticDB;

namespace GameServer.Systems.Ai;

/// <summary>
///     Reads the combat stats of a monster out of the tables in <c>clientdb.sd2</c>: the movement
///     speeds from its <c>dbcharacter::Monster</c> row, and the attack damage rating from the
///     <c>dbcharacter::MonsterScaling</c> row of the level the NPC was spawned at (the damage curve
///     is the database's definition of how much damage a monster of that level is worth; what one
///     swing commits of it is <see cref="AiAttackDamage.Resolve"/>).
/// </summary>
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

    public int GetAttackDamage(uint characterTypeId, byte level)
    {
        if (SDBInterface.GetMonster(characterTypeId) == null)
        {
            return 0;
        }

        var scaling = SDBInterface.GetMonsterScaling(level);
        if (scaling == null)
        {
            return 0;
        }

        return (int)scaling.Damage;
    }
}
