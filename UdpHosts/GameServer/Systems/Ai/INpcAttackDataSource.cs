using System.Collections.Generic;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.StaticDB.Records.dbitems;

namespace GameServer.Systems.Ai;

/// <summary>
///     The subset of <c>clientdb.sd2</c> the NPC attack resolver reads. Exists so the resolver can be
///     unit tested against a fake database - the production implementation is a thin wrapper around
///     <see cref="SDBInterface" /> and <see cref="SDBUtils" />.
/// </summary>
public interface INpcAttackDataSource
{
    /// <summary>A <c>dbcharacter::Monster</c> row, or null.</summary>
    Monster GetMonster(uint characterTypeId);

    /// <summary>A <c>dbcharacter::MonsterScaling</c> row (the per-level damage/health rating), or null.</summary>
    MonsterScaling GetMonsterScaling(byte level);

    /// <summary>A <c>dbcharacter::MonsterAttributeRange</c> row for one monster attribute, or null.</summary>
    MonsterAttributeRange GetMonsterAttribute(uint monsterId, ushort attributeId);

    /// <summary>The fully resolved weapon template (modifiers and slot modules applied), or null.</summary>
    WeaponInfoResult GetWeaponInfo(uint weaponId);

    /// <summary>The <c>dbitems::Weapons.weapon_type_id</c> of a weapon item, or 0 when it has no row.</summary>
    uint GetWeaponTemplateId(uint weaponId);

    /// <summary>Every <c>dbitems::AttributeRange</c> row of an item, keyed by attribute id.</summary>
    Dictionary<ushort, AttributeRange> GetItemAttributeRange(uint itemId);

    /// <summary>An <c>dbitems::Ammo</c> row, or null.</summary>
    Ammo GetAmmo(uint ammoId);
}

/// <summary>The production <see cref="INpcAttackDataSource" />: reads the loaded static database.</summary>
public sealed class SdbNpcAttackDataSource : INpcAttackDataSource
{
    public Monster GetMonster(uint characterTypeId) => SDBInterface.GetMonster(characterTypeId);

    public MonsterScaling GetMonsterScaling(byte level) => SDBInterface.GetMonsterScaling(level);

    public MonsterAttributeRange GetMonsterAttribute(uint monsterId, ushort attributeId) =>
        SDBInterface.GetMonsterAttributeRange(monsterId, attributeId);

    public WeaponInfoResult GetWeaponInfo(uint weaponId) => SDBUtils.GetDetailedWeaponInfo(weaponId);

    public uint GetWeaponTemplateId(uint weaponId) => SDBInterface.GetWeapon(weaponId)?.WeaponTypeId ?? 0;

    public Dictionary<ushort, AttributeRange> GetItemAttributeRange(uint itemId) => SDBInterface.GetItemAttributeRange(itemId);

    public Ammo GetAmmo(uint ammoId) => SDBInterface.GetAmmo(ammoId);
}
