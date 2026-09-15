using System;
using GameServer.StaticDB.Records.dbcharacter;

namespace GameServer.Systems.Ai;

/// <summary>
///     Resolves how a <c>dbcharacter::Monster</c> row reacts to the player's E key. The client asks
///     whether a character is interactable through <c>ClientQueryInteractionStatus</c> and the server
///     answers from the entity's <see cref="Entities.InteractionComponent" />; this class is the one
///     place that decides which component an NPC spawns with.
/// </summary>
/// <remarks>
///     <para>
///     The decision is driven by two columns of the monster row. The <c>behavior</c> string names the
///     interaction explicitly (<c>AlertAndInteractive(interactionType="HolsterTalk",...)</c>), and
///     <c>vendor_id</c> marks the row as a vendor terminal. The audit of all 3,109 <c>dbcharacter::Monster</c>
///     rows in build prod-1962 shows: 505 rows carry an interaction marker in <c>behavior</c>
///     (<c>HolsterTalk</c> 264, <c>Generic</c>/<c>GENERIC</c> 76, <c>Vendor</c> 12, <c>none</c> 6, a name
///     like <c>AlertAndInteractive</c> without a type 147), and 102 rows carry a non-zero
///     <c>vendor_id</c> - quartermasters, supply officers, the ARC job board and the like. Most of those
///     vendor rows also say <c>HolsterTalk</c> in their behaviour; the vendor id wins, because an NPC the
///     database stocks with a vendor terminal is a shopkeeper first and a talker second.
///     </para>
///     <para>
///     Durations are calibrated on the <c>dbitems::Deployable</c> rows of the same build, which carry the
///     authored interaction timings for every other interactable: generic channels run 0-2,500 ms (the
///     modal value is 0, most chatter uses 500), vendor terminals 0. An NPC without an authored number
///     gets a short 500 ms channel for <c>Generic</c> and <c>Vendor</c> prompts; <c>HolsterTalk</c>
///     completes instantly on the server because its aptitude effect (<c>StatusEffectData</c> 7228)
///     carries its own fixed 2-second lifetime before the conversation starts.
///     </para>
/// </remarks>
public static class NpcInteractionProfile
{
    /// <summary>The short channel a generic/vendor NPC interaction runs when the database gives no duration.</summary>
    public const uint DefaultChannelDurationMs = 500;

    /// <summary>
    ///     Builds the interaction component an NPC spawns with, or returns null when the monster is not
    ///     interactable at all.
    /// </summary>
    /// <param name="monster">The monster row the character loads from.</param>
    /// <returns>The component, or null for monsters the player cannot use the E key on.</returns>
    public static Entities.InteractionComponent Resolve(Monster monster)
    {
        if (monster == null)
        {
            return null;
        }

        var behavior = NpcBehaviorParams.Parse(monster.Behavior);

        // interactionType="none" is an explicit opt-out: the row is otherwise interactive-shaped
        // (perception, greetings) but the player must not get a prompt.
        if (behavior.InteractionTypeName.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Shopkeepers, quartermasters, the ARC job board: a vendor terminal id makes the row a
        // vendor no matter what the behaviour string says about talking.
        if (monster.VendorId != 0)
        {
            return new Entities.InteractionComponent
            {
                Type = InteractionType.Vendor,
                VendorId = monster.VendorId,
                DurationMs = DefaultChannelDurationMs,
                CompletedAbilityId = behavior.InteractAbilityId,
            };
        }

        if (!behavior.IsInteractiveBehaviorName)
        {
            return null;
        }

        if (TryResolveType(behavior.InteractionTypeName, out InteractionType type))
        {
            return new Entities.InteractionComponent
            {
                Type = type,
                DurationMs = ResolveDurationMs(type),
                CompletedAbilityId = behavior.InteractAbilityId,
            };
        }

        // Interactive-named behaviour without an interactionType parameter (147 rows, almost all
        // greeting/hello-script town NPCs): they talk when approached.
        return new Entities.InteractionComponent
        {
            Type = InteractionType.Holstertalk,
            DurationMs = 0,
            CompletedAbilityId = behavior.InteractAbilityId,
        };
    }

    /// <summary>Maps the behaviour string's <c>interactionType</c> value onto the client's enum.</summary>
    /// <param name="name">The raw value from the behaviour string (already known to be non-empty).</param>
    /// <param name="type">The resolved type when the value is a known one.</param>
    /// <returns>Whether the value named a known interaction type.</returns>
    public static bool TryResolveType(string name, out InteractionType type)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            type = default;
            return false;
        }

        // The database spells the values HolsterTalk/holsterTalk, Generic/GENERIC, Vendor and none.
        if (Enum.TryParse<InteractionType>(name.Trim(), ignoreCase: true, out type))
        {
            return true;
        }

        type = default;
        return false;
    }

    /// <summary>
    ///     The channel duration the client's interaction prompt counts down for this type, calibrated on
    ///     the authored <c>dbitems::Deployable</c> timings (see the type remarks).
    /// </summary>
    /// <param name="type">The interaction type the NPC registered.</param>
    /// <returns>Milliseconds the interaction channels before it completes.</returns>
    public static uint ResolveDurationMs(InteractionType type)
    {
        return type switch
        {
            // Vendor terminals complete at once in the deployable data; the NPC keeps a short
            // channel so the prompt reads like the other "use" interactions.
            InteractionType.Vendor => DefaultChannelDurationMs,
            InteractionType.Generic => DefaultChannelDurationMs,

            // The holstertalk aptitude effect (7228) carries a fixed 2-second lifetime before the
            // dialog starts, so the server-side completion time is immediate.
            InteractionType.Holstertalk => 0,

            // Collectible-style channels on interactables run 500 ms in the deployable data.
            InteractionType.Collect => 500,

            _ => DefaultChannelDurationMs,
        };
    }
}
