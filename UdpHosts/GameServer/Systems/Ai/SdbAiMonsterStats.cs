using GameServer.StaticDB;

namespace GameServer.Systems.Ai;

/// <summary>
///     Reads the combat stats of a monster out of the tables in <c>clientdb.sd2</c>: the movement
///     speeds from its <c>dbcharacter::Monster</c> row, the attack damage rating from the
///     <c>dbcharacter::MonsterScaling</c> row of the level the NPC was spawned at, and the weapon it
///     fights with (see <see cref="NpcAttackResolver" />).
/// </summary>
/// <remarks>
///     The damage rating is the database's definition of how much damage a monster of that level is
///     worth; a monster whose weapon resolves attacks with the weapon's own damage instead (see
///     <see cref="NpcAttackProfile" />), and the rating is what the engine falls back to when it does
///     not.
/// </remarks>
public class SdbAiMonsterStats : IAiMonsterStats
{
    private readonly NpcAttackResolver _attackResolver;

    public SdbAiMonsterStats(IAiRules rules = null)
    {
        _attackResolver = new NpcAttackResolver(new SdbNpcAttackDataSource(), rules);
    }

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

    public NpcAttackProfile GetAttackProfile(uint characterTypeId, byte level)
    {
        return _attackResolver.Resolve(characterTypeId, level);
    }
}
