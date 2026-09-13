using System.Collections.Generic;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.apt;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Ai;
// The enum that names a command's kind, not the apt::CommandType table's row record of the same name.
using AptCommandType = GameServer.Systems.Aptitude.CommandType;

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

    public Dictionary<uint, AbilityData> Abilities { get; } = [];

    /// <summary><c>dbitems::AbilityModule.id</c> -> the <c>apt::AbilityData</c> id its <c>ability_chain_id</c> names.</summary>
    public Dictionary<uint, uint> AbilityModules { get; } = [];

    public Dictionary<uint, BaseCommandDef> Commands { get; } = [];

    public Dictionary<uint, ConditionalBranchCommandDef> Branches { get; } = [];

    public Dictionary<uint, CallCommandDef> Calls { get; } = [];

    public Dictionary<uint, ImpactApplyEffectCommandDef> ImpactApplyEffects { get; } = [];

    public Dictionary<uint, UpdateWaitAndFireOnceCommandDef> WaitAndFireOnces { get; } = [];

    public Dictionary<uint, LogicAndChainCommandDef> LogicAnds { get; } = [];

    public Dictionary<uint, LogicOrChainCommandDef> LogicOrs { get; } = [];

    public Dictionary<uint, LogicNegateCommandDef> LogicNegates { get; } = [];

    public Dictionary<uint, WhileLoopCommandDef> WhileLoops { get; } = [];

    public Dictionary<uint, ImpactToggleEffectCommandDef> ImpactToggleEffects { get; } = [];

    public Dictionary<uint, StatusEffectData> StatusEffects { get; } = [];

    public AbilityData GetAbility(uint abilityId) => Abilities.GetValueOrDefault(abilityId);

    public uint ResolveAbilityModule(uint moduleId) => AbilityModules.GetValueOrDefault(moduleId);

    /// <summary>Writes one <c>dbitems::AbilityModule</c> row: the module id and the ability it runs.</summary>
    /// <param name="moduleId">The module id a behaviour string's <c>am*Id</c> names.</param>
    /// <param name="abilityId">The <c>apt::AbilityData</c> id the module's <c>ability_chain_id</c> points at.</param>
    /// <returns>The fake, for chaining.</returns>
    public FakeNpcAttackDataSource WithAbilityModule(uint moduleId, uint abilityId)
    {
        AbilityModules[moduleId] = abilityId;
        return this;
    }

    public BaseCommandDef GetCommand(uint commandId) => Commands.GetValueOrDefault(commandId);

    public ConditionalBranchCommandDef GetConditionalBranch(uint commandId) => Branches.GetValueOrDefault(commandId);

    public CallCommandDef GetCall(uint commandId) => Calls.GetValueOrDefault(commandId);

    public ImpactApplyEffectCommandDef GetImpactApplyEffect(uint commandId) =>
        ImpactApplyEffects.GetValueOrDefault(commandId);

    public UpdateWaitAndFireOnceCommandDef GetUpdateWaitAndFireOnce(uint commandId) =>
        WaitAndFireOnces.GetValueOrDefault(commandId);

    public LogicAndChainCommandDef GetLogicAndChain(uint commandId) => LogicAnds.GetValueOrDefault(commandId);

    public LogicOrChainCommandDef GetLogicOrChain(uint commandId) => LogicOrs.GetValueOrDefault(commandId);

    public LogicNegateCommandDef GetLogicNegate(uint commandId) => LogicNegates.GetValueOrDefault(commandId);

    public WhileLoopCommandDef GetWhileLoop(uint commandId) => WhileLoops.GetValueOrDefault(commandId);

    public ImpactToggleEffectCommandDef GetImpactToggleEffect(uint commandId) =>
        ImpactToggleEffects.GetValueOrDefault(commandId);

    public StatusEffectData GetStatusEffect(uint effectId) => StatusEffects.GetValueOrDefault(effectId);

    /// <summary>
    ///     The command subtypes the client executes and the server does not (<c>apt::CommandType.environment</c>
    ///     = <c>client</c> in the database; the <c>apttf::</c> table family).
    ///     Seeded with the client's feedback family, so a test only adds a subtype when it writes a command
    ///     the seed does not cover.
    /// </summary>
    public HashSet<uint> ClientSubtypes { get; } =
    [
        (uint)AptCommandType.AudioFeedback,
        (uint)AptCommandType.PlayAnimation,
        (uint)AptCommandType.SetAnimCtrlParam,
        (uint)AptCommandType.ParticleEffectAsset,
        (uint)AptCommandType.AbilityAnimation,
        (uint)AptCommandType.SwitchMaterial,
        (uint)AptCommandType.PerformEmote,
        (uint)AptCommandType.LocalParticleEffect,
        (uint)AptCommandType.AudioStateChange,
    ];

    public bool IsClientCommand(uint commandSubtype) => ClientSubtypes.Contains(commandSubtype);

    /// <summary>
    ///     Writes one command of <paramref name="subtype" /> into the fake's tables: the shared
    ///     <c>apt::BaseCommandDef</c> row plus whatever the walker needs from the command's own type.
    /// </summary>
    public FakeNpcAttackDataSource WithCommand(
        uint commandId,
        ushort subtype,
        uint next = 0,
        uint effectId = 0,
        uint calledAbilityId = 0,
        uint ifChain = 0,
        uint thenChain = 0,
        uint elseChain = 0,
        uint waitChain = 0,
        uint andChain = 0,
        uint orChain = 0,
        uint negateChain = 0,
        uint bodyChain = 0,
        uint conditionChain = 0,
        uint preApplyChain = 0)
    {
        Commands[commandId] = new BaseCommandDef { Id = commandId, Subtype = subtype, Next = next };

        switch ((AptCommandType)subtype)
        {
            case AptCommandType.ImpactApplyEffect:
                ImpactApplyEffects[commandId] = new ImpactApplyEffectCommandDef { Id = commandId, EffectId = effectId };
                break;
            case AptCommandType.Call:
                Calls[commandId] = new CallCommandDef { Id = commandId, AbilityId = calledAbilityId };
                break;
            case AptCommandType.ConditionalBranch:
                Branches[commandId] = new ConditionalBranchCommandDef
                {
                    Id = commandId,
                    IfChain = ifChain,
                    ThenChain = thenChain,
                    ElseChain = elseChain,
                };
                break;
            case AptCommandType.UpdateWaitAndFireOnce:
                WaitAndFireOnces[commandId] = new UpdateWaitAndFireOnceCommandDef { Id = commandId, Chain = waitChain };
                break;
            case AptCommandType.LogicAndChain:
                LogicAnds[commandId] = new LogicAndChainCommandDef { Id = commandId, AndChain = andChain };
                break;
            case AptCommandType.LogicOrChain:
                LogicOrs[commandId] = new LogicOrChainCommandDef { Id = commandId, OrChain = orChain };
                break;
            case AptCommandType.LogicNegate:
                LogicNegates[commandId] = new LogicNegateCommandDef { Id = commandId, NegateChain = negateChain };
                break;
            case AptCommandType.WhileLoop:
                WhileLoops[commandId] = new WhileLoopCommandDef
                {
                    Id = commandId,
                    BodyChain = bodyChain,
                    ConditionChain = conditionChain,
                };
                break;
            case AptCommandType.ImpactToggleEffect:
                ImpactToggleEffects[commandId] = new ImpactToggleEffectCommandDef
                {
                    Id = commandId,
                    EffectId = effectId,
                    PreApplyChain = preApplyChain,
                };
                break;
        }

        return this;
    }

    /// <summary>Writes an <c>apt::AbilityData</c> row pointing at <paramref name="chainId" />.</summary>
    public FakeNpcAttackDataSource WithAbility(uint abilityId, uint chainId)
    {
        Abilities[abilityId] = new AbilityData { Id = abilityId, Chain = chainId };
        return this;
    }

    /// <summary>Writes an <c>apt::StatusEffectData</c> row with the chains the scan follows.</summary>
    public FakeNpcAttackDataSource WithStatusEffect(
        uint effectId,
        uint applyChain = 0,
        uint removeChain = 0,
        uint updateChain = 0,
        uint durationChain = 0)
    {
        StatusEffects[effectId] = new StatusEffectData
        {
            Id = effectId,
            ApplyChain = applyChain,
            RemoveChain = removeChain,
            UpdateChain = updateChain,
            DurationChain = durationChain,
        };

        return this;
    }

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
