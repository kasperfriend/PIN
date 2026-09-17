using AeroMessages.GSS.Character.Controller;
using AeroMessages.GSS.Character.Event;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Entities.Vehicle;
using GameServer.Systems.Dialog;
using GameServer.Systems.Encounters;

namespace GameServer.Systems.Aptitude.Commands.Interaction;

public class EndInteractionCommand : ICommand
{
    /// <summary>
    ///     The terminal type the client opens a vendor window for (see the
    ///     <c>aptgss::AuthorizeTerminalCommandDef</c> census: "Invite Luau Larry UI. Vendor id 10
    ///     based on UI."). The terminal id carries the vendor terminal the NPC stocks
    ///     (<c>dbcharacter::Monster.vendor_id</c>).
    /// </summary>
    private const byte VendorTerminalType = 7;

    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext<EndInteractionCommand>();

    public EndInteractionCommand(uint id)
    {
        Id = id;
    }

    public uint Id { get; set; }

    public bool Execute(Context context)
    {
        if (context.Targets.Count == 0 || context.Self is not CharacterEntity character)
        {
            return false;
        }

        var interactionEntity = (BaseEntity)context.Targets.Peek();
        uint now = context.Shard.CurrentTime;

        // Completion bookkeeping: the channel recorded by agsInteractionCompletionTimeCommandDef
        // decides between a finished interaction (content fires) and an interrupted one (no
        // content). Flows that never record a channel - vehicles, doctor pads and transports call
        // ability 181 straight from their apply chain - count as completed, which keeps the
        // vehicle-boarding flow intact.
        var state = character.ActiveInteraction;
        character.ActiveInteraction = null;

        // A target that died during the channel cannot hand out its content.
        bool targetAlive = interactionEntity is not CharacterEntity { IsAlive: false };

        bool completed = (state == null || state.IsCompleted(now)) && targetAlive;
        byte percent = completed ? (byte)100 : state.PercentAt(now);

        if (character.IsPlayerControlled)
        {
            var message = new InteractionCompleted { Percent = percent };
            character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(message, character.EntityId);
        }

        if (!completed)
        {
            Logger.Information(
                "EndInteraction: {Player} cancelled the interaction on {Target} at {Percent}% - no content",
                character,
                interactionEntity,
                percent);
            return true;
        }

        if (interactionEntity.Encounter is { Instance: IInteractionHandler encounter })
        {
            encounter.OnInteraction(character, interactionEntity);
        }

        if (interactionEntity.Encounter is { SpawnDef: { } spawnData })
        {
            context.Shard.EncounterMan.Factory.SpawnEncounter(spawnData, character);
        }

        // Start the original character/voice-set conversation for every NPC interaction. The
        // explicit dialogScript= rows still win; older greetingSet=/helloScript behaviours fall
        // through to the indexed DialogScript rows instead of completing silently.
        bool dialogPlayed = false;
        if (interactionEntity is CharacterEntity npc
            && npc.Interaction?.Type is InteractionType.Holstertalk or InteractionType.Generic or InteractionType.Vendor)
        {
            dialogPlayed = DialogService.Production.TryPlayInteractionDialog(npc, character, context.InitTime);
        }

        var interaction = interactionEntity.Interaction;
        bool vendorAuthorized = false;
        uint completedAbilityId = 0;
        if (interaction != null)
        {
            uint abilityId = interaction.CompletedAbilityId;
            completedAbilityId = abilityId;
            if (abilityId != 0)
            {
                context.Shard.Abilities.HandleActivateAbility(
                    context.Shard,
                    (IAptitudeTarget)interactionEntity,
                    abilityId,
                    context.Shard.CurrentTime,
                    new AptitudeTargets(character),
                    context.ExecutionId);
            }

            if (interaction.Type == InteractionType.Vendor && interaction.VendorId != 0 && character.IsPlayerControlled)
            {
                // Shopkeepers and quartermasters: completing the channel authorizes the vendor
                // terminal, which makes the client open the vendor UI (and ask for the products
                // with VendorProductRequest).
                character.SetAuthorizedTerminal(new AuthorizedTerminalData
                {
                    TerminalType = VendorTerminalType,
                    TerminalId = interaction.VendorId,
                    TerminalEntityId = interactionEntity.AeroEntityId.Backing,
                });
                vendorAuthorized = true;

                // The BaseController replication carries the authorized terminal; flush it now
                // instead of waiting for the next 20 ms entity sweep so the shop UI opens
                // on the same tick the channel completes.
                character.Shard.EntityMan.FlushChanges(character);

                Logger.Information(
                    "EndInteraction: {Player} completed the vendor interaction on {Target} - authorized terminal type {TerminalType} id {VendorId}",
                    character,
                    interactionEntity,
                    VendorTerminalType,
                    interaction.VendorId);
            }

            if (interaction.Type == InteractionType.Doctor && abilityId == 0)
            {
                // The doctor interaction has no authored content of its own (the one live
                // deployable with the type carries no completed ability either), so the trauma
                // docs simply patch the player back up.
                character.SetCurrentHealth(character.MaxHealth.Value);
            }
        }

        var interactionType = interaction?.Type ?? 0;

        // if (hack is DeployableEntity { Turret: not null } deployable)
        // {
        //     var character = initiator as CharacterEntity;
        //
        //     deployable.Turret.SetControllingPlayer(character.Player);
        // }
        if (interactionType == InteractionType.Vehicle && interactionEntity is VehicleEntity vehicle)
        {
            vehicle.AddOccupant(character);
        }

        // Non-character interactables can legitimately have no content. NPCs normally resolve an
        // original character, voice-set or generic talk line now; if even that data is absent, this
        // log makes the incomplete interaction distinguishable from a failed channel.
        // Vehicles (boarding) and doctors (the heal above) always fire their content.
        if (!dialogPlayed && !vendorAuthorized && completedAbilityId == 0
            && interactionType != InteractionType.Vehicle && interactionType != InteractionType.Doctor)
        {
            Logger.Information(
                "EndInteraction: {Player} completed the {InteractionType} interaction on {Target} - no dialog, vendor or ability content fired",
                character,
                interactionType,
                interactionEntity);
        }

        return true;
    }
}
