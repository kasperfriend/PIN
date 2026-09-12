using System.Collections.Generic;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Ai;

namespace GameServer.Tests.Fakes;

/// <summary>
///     In-memory <see cref="INpcAttackDataSource" /> for the attack resolver tests: the same lookup
///     chain the server walks, with rows the test writes by hand instead of a 32 MB clientdb.sd2.
/// </summary>
public sealed class FakeNpcAttackDataSource : INpcAttackDataSource
{
    public Dictionary<uint, Monster> Monsters { get; } = [];

    public Dictionary<byte, MonsterScaling> Scalings { get; } = [];

    public Dictionary<(uint MonsterId, ushort AttributeId), MonsterAttributeRange> MonsterAttributes { get; } = [];

    public Dictionary<uint, WeaponInfoResult> Weapons { get; } = [];

    public Dictionary<uint, Dictionary<ushort, AttributeRange>> ItemAttributes { get; } = [];

    public Dictionary<uint, Ammo> Ammos { get; } = [];

    public Dictionary<uint, uint> WeaponTemplateIds { get; } = [];

    public Monster GetMonster(uint characterTypeId) => Monsters.GetValueOrDefault(characterTypeId);

    public MonsterScaling GetMonsterScaling(byte level) => Scalings.GetValueOrDefault(level);

    public MonsterAttributeRange GetMonsterAttribute(uint monsterId, ushort attributeId) =>
        MonsterAttributes.GetValueOrDefault((monsterId, attributeId));

    public WeaponInfoResult GetWeaponInfo(uint weaponId) => Weapons.GetValueOrDefault(weaponId);

    public uint GetWeaponTemplateId(uint weaponId) => WeaponTemplateIds.GetValueOrDefault(weaponId);

    public Dictionary<ushort, AttributeRange> GetItemAttributeRange(uint itemId) =>
        ItemAttributes.TryGetValue(itemId, out var rows) ? rows : [];

    public Ammo GetAmmo(uint ammoId) => Ammos.GetValueOrDefault(ammoId);

    /// <summary>Adds an item attribute row (creates the item's dictionary on first use).</summary>
    public FakeNpcAttackDataSource WithAttribute(uint itemId, ushort attributeId, float value)
    {
        if (!ItemAttributes.TryGetValue(itemId, out var rows))
        {
            rows = [];
            ItemAttributes[itemId] = rows;
        }

        rows[attributeId] = new AttributeRange { ItemId = itemId, AttributeId = attributeId, Base = value };
        return this;
    }

    /// <summary>Adds a weapon item whose main firing mode is <paramref name="main" />.</summary>
    public FakeNpcAttackDataSource WithWeapon(uint weaponId, WeaponTemplateResult main)
    {
        Weapons[weaponId] = new WeaponInfoResult { Main = main };
        WeaponTemplateIds[weaponId] = 0;
        return this;
    }
}
