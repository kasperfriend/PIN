using System.Threading;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.Enums;
using GameServer.StaticDB;
using GameServer.Systems.NpcDeath;
using GameServer.Systems.SystemEvents;
using Serilog;

namespace GameServer.Systems.Combat;

public class DamageSystem
{
    private static readonly ILogger Logger = Log.ForContext<DamageSystem>();

    private readonly IEventBus _eventBus;
    private readonly IShard _shard;
    private readonly INpcDeathRules _rules;

    public DamageSystem(IEventBus eventBus, IShard shard, INpcDeathRules rules)
    {
        _eventBus = eventBus;
        _shard = shard;
        _rules = rules;
    }

    /// <summary>
    /// Applies incoming damage and returns the post-defense amount that reached
    /// the target. A zero return means that the hit was rejected, ignored, or
    /// fully mitigated.
    /// </summary>
    public int ApplyDamage(IEntity target, int amount, IEntity source = null, byte damageType = 0)
    {
        if (target == null || amount <= 0)
        {
            return 0;
        }

        // Debug cheats: a player-controlled source scales outgoing damage (dmg <mult>,
        // dmg -1 = one hit kill). Self inflicted damage (hurtme, fall damage, bleedout)
        // is excluded so the cheats only ever boost what the player does to others.
        if (source != target && source is CharacterEntity { IsPlayerControlled: true } playerSource && playerSource.Player != null)
        {
            amount = _shard.Cheats.ApplyOutgoingDamage(playerSource.Player, amount);
            if (amount <= 0)
            {
                return 0;
            }
        }

        int appliedAmount;
        if (target is CharacterEntity character)
        {
            appliedAmount = ApplyDamageToCharacter(character, amount, damageType);
        }
        else if (target is DeployableEntity deployable)
        {
            appliedAmount = ApplyDamageToDeployable(deployable, amount, damageType);
        }
        else
        {
            Logger.Warning("ApplyDamage called on non-damageable entity {EntityId}, ignoring", target.EntityId);
            return 0;
        }

        if (appliedAmount > 0)
        {
            // Publish the post-defense amount. AI aggro and hit feedback should
            // agree with the damage that actually reached the target.
            _eventBus.Publish(new EntityDamagedEvent(target, appliedAmount, source));
        }

        return appliedAmount;
    }

    public void ApplyHeal(IEntity target, int amount, IEntity source = null)
    {
        if (target == null || amount <= 0)
        {
            return;
        }

        bool applied;
        if (target is CharacterEntity character)
        {
            applied = ApplyHealToCharacter(character, amount, source);
        }
        else if (target is DeployableEntity deployable)
        {
            applied = ApplyHealToDeployable(deployable, amount, source);
        }
        else
        {
            Logger.Warning("ApplyHeal called on non-damageable entity {EntityId}, ignoring", target.EntityId);
            return;
        }

        if (applied)
        {
            _eventBus.Publish(new EntityHealedEvent(target, amount, source));
        }
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
    }

    private int ApplyDamageToCharacter(CharacterEntity character, int amount, byte damageType)
    {
        if (!character.IsAlive)
        {
            // Corpses, characters in bleedout and everyone still spawning can
            // not be damaged. Bleedout damage is handled by the lifecycle
            // service on its own.
            Logger.Debug("{Name} is in state {State}, ignoring damage", character, character.CharacterState.State);
            return 0;
        }

        var response = SDBInterface.GetDamageResponse(character.DamageResponseId);
        var typeResponse = SDBInterface.GetDamageResponseDamageType(character.DamageResponseId, damageType);
        int mitigated = DamageMitigationMath.Apply(
            amount,
            character.GetCurrentStatModifierValue(StatModifierIdentifier.DamageTaken),
            response?.DefaultMultiplier ?? 1f,
            typeResponse?.Multiplier);
        if (mitigated <= 0)
        {
            Logger.Debug("{Name} ignored {Damage} damage because its response makes it immune", character, amount);
            return 0;
        }

        int remaining = mitigated;

        if (character.CurrentShields > 0)
        {
            int shieldAbsorb = int.Min(remaining, character.CurrentShields);
            character.SetCurrentShields(character.CurrentShields - shieldAbsorb);
            remaining -= shieldAbsorb;
        }

        if (remaining > 0)
        {
            character.SetCurrentHealth(character.CurrentHealth - remaining);
        }

        return mitigated;
    }

    private bool ApplyHealToCharacter(CharacterEntity character, int amount, IEntity source)
    {
        if (!character.IsAlive)
        {
            Logger.Debug("{Name} is in state {State}, ignoring heal", character, character.CharacterState.State);
            return false;
        }

        character.SetCurrentHealth(character.CurrentHealth + amount);
        return true;
    }

    private int ApplyDamageToDeployable(DeployableEntity deployable, int amount, byte damageType)
    {
        if (deployable.IsDead)
        {
            return 0;
        }

        var response = SDBInterface.GetDamageResponse(deployable.DamageResponseId);
        var typeResponse = SDBInterface.GetDamageResponseDamageType(deployable.DamageResponseId, damageType);
        int mitigated = DamageMitigationMath.Apply(
            amount,
            damageTakenMultiplier: 1f,
            defaultResponseMultiplier: response?.DefaultMultiplier ?? 1f,
            damageTypeMultiplier: typeResponse?.Multiplier);
        if (mitigated <= 0)
        {
            Logger.Debug("{Name} ignored {Damage} damage because its response makes it immune", deployable, amount);
            return 0;
        }

        deployable.SetCurrentHealth(deployable.CurrentHealth - mitigated);

        if (deployable.CurrentHealth > 0)
        {
            return mitigated;
        }

        deployable.MarkDead();

        var deployableInfo = SDBInterface.GetDeployable(deployable.Type);
        deployable.SetGibVisuals(deployableInfo?.GibsetId ?? 0);
        Logger.Information("{Name} destroyed", deployable);

        _shard.EntityMan.SetRemainingLifetime(deployable, (uint)_rules.CorpseLingerMs);
        return mitigated;
    }

    private bool ApplyHealToDeployable(DeployableEntity deployable, int amount, IEntity source)
    {
        if (deployable.IsDead)
        {
            return false;
        }

        deployable.SetCurrentHealth(deployable.CurrentHealth + amount);
        return true;
    }
}
