using GameServer.Entities;

namespace GameServer.Systems.Aptitude.Commands.Interaction;

public class BeginInteractionCommand : ICommand
{
    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext<BeginInteractionCommand>();

    public BeginInteractionCommand(uint id)
    {
        Id = id;
    }

    public uint Id { get; set; }

    public bool Execute(Context context)
    {
        if (context.Targets.Count == 0)
        {
            return false;
        }

        var interactionEntity = context.Targets.Peek();

        // Targets without an interaction component have no start ability to cast.
        if (interactionEntity is not BaseEntity entity)
        {
            Logger.Debug(
                "{Command} {CommandId} has a non-entity target ({Target}), nothing to interact with",
                nameof(BeginInteractionCommand), Id, interactionEntity);
            return true;
        }

        if (entity.Interaction == null)
        {
            return true;
        }

        var abilityId = entity.Interaction.StartedAbilityId;
        if (abilityId != 0)
        {
            var actingEntity = context.Self;
            context.Shard.Abilities.HandleActivateAbility(
                context.Shard,
                interactionEntity,
                abilityId,
                context.Shard.CurrentTime,
                new AptitudeTargets(actingEntity),
                context.ExecutionId);
        }

        return true;
    }
}
