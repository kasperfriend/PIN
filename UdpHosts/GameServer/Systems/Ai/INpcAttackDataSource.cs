using System.Collections.Generic;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.apt;
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

    /// <summary>An <c>apt::AbilityData</c> row (its chain id), or null.</summary>
    AbilityData GetAbility(uint abilityId);

    /// <summary>An <c>apt::BaseCommandDef</c> row (its type and its <c>next</c> node), or null.</summary>
    BaseCommandDef GetCommand(uint commandId);

    /// <summary>An <c>apt::ConditionalBranchCommandDef</c> row (its if/then/else chains), or null.</summary>
    ConditionalBranchCommandDef GetConditionalBranch(uint commandId);

    /// <summary>An <c>apt::CallCommandDef</c> row (the ability it calls), or null.</summary>
    CallCommandDef GetCall(uint commandId);

    /// <summary>An <c>apt::ImpactApplyEffectCommandDef</c> row (the effect it applies), or null.</summary>
    ImpactApplyEffectCommandDef GetImpactApplyEffect(uint commandId);

    /// <summary>An <c>apt::StatusEffectData</c> row (its four chains), or null.</summary>
    StatusEffectData GetStatusEffect(uint effectId);

    /// <summary>
    ///     Whether a command subtype is one the client runs. The database splits the aptitude commands by
    ///     who executes them - <c>apt::CommandType.sdb_fullname</c> names the table a command's parameters
    ///     live in - and the <c>apttf::</c> tables are the client's: animations
    ///     (<c>tfPlayAnimationCommandDef</c>, <c>tfAbilityAnimationCommandDef</c>), emotes, material
    ///     switches, particles and audio feedback. The server does not run them (this build wires them to
    ///     a no-op command on purpose); the client plays them out of the replicated effect whose chain
    ///     carries them, so a chain holding one draws nothing at all until that effect is applied.
    /// </summary>
    /// <param name="commandSubtype">An <c>apt::BaseCommandDef.subtype</c>.</param>
    /// <returns>Whether the command's definition table is one of the client's (<c>apttf::</c>).</returns>
    bool IsClientCommand(uint commandSubtype);
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

    public AbilityData GetAbility(uint abilityId) => SDBInterface.GetAbilityData(abilityId);

    public BaseCommandDef GetCommand(uint commandId) => SDBInterface.GetBaseCommandDef(commandId);

    public ConditionalBranchCommandDef GetConditionalBranch(uint commandId) => SDBInterface.GetConditionalBranchCommandDef(commandId);

    public CallCommandDef GetCall(uint commandId) => SDBInterface.GetCallCommandDef(commandId);

    public ImpactApplyEffectCommandDef GetImpactApplyEffect(uint commandId) => SDBInterface.GetImpactApplyEffectCommandDef(commandId);

    public StatusEffectData GetStatusEffect(uint effectId) => SDBInterface.GetStatusEffectData(effectId);

    public bool IsClientCommand(uint commandSubtype)
    {
        return SDBInterface.GetCommandType(commandSubtype)?.SdbFullname?.StartsWith("apttf::") == true;
    }
}
