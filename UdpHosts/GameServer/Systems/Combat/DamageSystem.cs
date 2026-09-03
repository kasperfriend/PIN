using System.Threading;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
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

    public void ApplyDamage(IEntity target, int amount, IEntity source = null)
    {
        if (target == null || amount <= 0)
        {
            return;
        }

        bool applied;
        if (target is CharacterEntity character)
        {
            applied = ApplyDamageToCharacter(character, amount, source);
        }
        else if (target is DeployableEntity deployable)
        {
            applied = ApplyDamageToDeployable(deployable, amount, source);
        }
        else
        {
            Logger.Warning("ApplyDamage called on non-damageable entity {EntityId}, ignoring", target.EntityId);
            return;
        }

        if (applied)
        {
            _eventBus.Publish(new EntityDamagedEvent(target, amount, source));
        }
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

    private bool ApplyDamageToCharacter(CharacterEntity character, int amount, IEntity source)
    {
        if (!character.IsAlive)
        {
            // Corpses, characters in bleedout and everyone still spawning can
            // not be damaged. Bleedout damage is handled by the lifecycle
            // service on its own.
            Logger.Debug("{Name} is in state {State}, ignoring damage", character, character.CharacterState.State);
            return false;
        }

        int remaining = amount;

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

        return true;
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

    private bool ApplyDamageToDeployable(DeployableEntity deployable, int amount, IEntity source)
    {
        if (deployable.IsDead)
        {
            return false;
        }

        deployable.SetCurrentHealth(deployable.CurrentHealth - amount);

        if (deployable.CurrentHealth > 0)
        {
            return true;
        }

        deployable.MarkDead();

        var deployableInfo = SDBInterface.GetDeployable(deployable.Type);
        deployable.SetGibVisuals(deployableInfo?.GibsetId ?? 0);
        Logger.Information("{Name} destroyed", deployable);

        _shard.EntityMan.SetRemainingLifetime(deployable, (uint)_rules.CorpseLingerMs);
        return true;
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
