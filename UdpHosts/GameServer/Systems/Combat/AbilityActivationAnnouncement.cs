using AeroMessages.GSS.Character;
using AeroMessages.GSS.Character.Event;
using GameServer.Entities.Character;
using GameServer.Systems.Aptitude;

namespace GameServer.Systems.Combat;

/// <summary>
///     Tells watching clients that a character started an ability. <c>AbilityActivated</c> is the
///     event that makes a client run the client-environment half of the ability's chain - the
///     <c>apttf::</c> rows (<c>tfPlayAnimation</c>, <c>tfParticleEffectAsset</c>, <c>tfBeamEffect</c>,
///     <c>tfCameraShakeEffect</c>, <c>tfAbilityAnimation</c>). The server deliberately no-ops those
///     rows in <c>Factory.LoadCommand</c> because the client owns them, so a client that never hears
///     the activation plays no animation and draws no effect for it.
///     <para>
///         The character combat controller used to be the only sender, and it sent to the acting
///         player's own channel only. That left every ability invisible to everyone but its caster:
///         no cast animation on another player, no particle effect, and - because NPCs, deployables
///         and encounter objects activate through <c>AbilitySystem</c> without a combat controller
///         echo at all - an enemy's ability produced damage out of nothing. The announcement now
///         rides the same <c>EntityManager.SendToScoped</c> path <c>TookHit</c> and
///         <c>WeaponProjectileFired</c> already use.
///     </para>
///     <para>
///         <c>AbilityActivated</c> is bound to <c>GssCharacterView.CombatController</c>, so it is
///         only valid for a character entity. Vehicle activations answer with the separate
///         <c>AeroMessages.GSS.Vehicle.Event.AbilityActivated</c> from the vehicle combat controller
///         and are left alone here.
///     </para>
/// </summary>
public static class AbilityActivationAnnouncement
{
    /// <summary>
    ///     Sends <c>AbilityActivated</c> to every client the casting character is scoped into. The
    ///     acting player's own client is skipped: a player-controlled character already gets the
    ///     acknowledgement on ReliableGss from
    ///     <c>Character.CombatController.SendAbilityActivationResponse</c>, and a second copy on
    ///     UnreliableGss would run the cast animation twice. No-op when there is nobody to tell (a
    ///     test shard with no entity manager) or the caster is not a character.
    /// </summary>
    public static void SendToWatchers(IShard shard, IAptitudeTarget source, uint abilityId, uint activationTime)
    {
        if (shard?.EntityMan == null || source == null || abilityId == 0)
        {
            return;
        }

        // AbilityActivated is bound to GssCharacterView.CombatController, so it can only address a
        // character entity. A deployable or turret that activates its own chain (a turret firing,
        // a healing generator pulsing) would need the event on the deployable's entity id, which
        // this message cannot carry: attributing the cast to the owning character instead would
        // animate the wrong entity, so those are left unannounced here.
        if (source is not CharacterEntity character)
        {
            return;
        }

        // A player-controlled caster hears about its own activation from the combat controller's
        // ReliableGss acknowledgement; an NPC or a player-owned deployable/turret has no echo, so
        // its watchers (including the owner) must all be told here.
        INetworkPlayer except = character.IsPlayerControlled ? character.Player : null;

        var message = new AbilityActivated
        {
            ActivatedAbilityId = abilityId,
            ActivatedTime = activationTime,

            // The cooldown payload is the caster's own ability-bar state: the excluded owner
            // receives the real one from the combat controller, and an observer has no ability bar
            // for a character it does not control. Empty rather than null so the [AeroArray] fields
            // serialize as zero-length arrays.
            AbilityCooldownsData = new AbilityCooldownsData
            {
                ActiveCooldowns_Group1 = [],
                ActiveCooldowns_Group2 = [],
            },
        };

        shard.EntityMan.SendToScoped(character, message, except);
    }
}
