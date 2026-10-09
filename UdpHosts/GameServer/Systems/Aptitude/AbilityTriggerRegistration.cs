namespace GameServer.Systems.Aptitude;

/// <summary>
///     An ability trigger registered by one of the <c>Register*TriggerCommand</c> family: a condition
///     installed on an entity that <c>ActivateAbilityTriggerCommand</c> can fire, and that a timed
///     trigger fires by itself when the effect that installed it runs out. Lives exactly as long as the
///     effect that carried the registering command.
///     <para>
///         Every def in this family is id-only in both the clientdb and PIN's own
///         <c>customdata</c> records, so the only thing a row can say is <em>that</em> a trigger is
///         registered, never what it does. What it does is therefore taken from the activation that
///         installed it: firing the trigger activates that activation's ability. The chain evidence says
///         that is the right reading — <c>RegisterAbilityTrigger</c> is terminal in 343 of its 492 rows
///         (a registration ends a chain rather than continuing one), and <c>ActivateAbilityTrigger</c>
///         follows <c>TimeCooldown</c> in 42 of its 169 rows, which only makes sense if activating a
///         trigger starts an ability activation that needs cooldown protection.
///     </para>
/// </summary>
public class AbilityTriggerRegistration
{
    public ulong OwnerEntityId;

    /// <summary>The ability activation to run when the trigger fires.</summary>
    public uint AbilityId;

    public uint AbilityModuleId;

    /// <summary>The chain that installed this, recorded so a fired trigger can be traced back.</summary>
    public uint ChainId;

    /// <summary>Set for <c>RegisterTimedTrigger</c>: the trigger also fires when the carrying effect ends.</summary>
    public bool Timed;

    /// <summary>
    ///     The discriminator for the tagged variants (<c>RegisterEffectTagTrigger</c>,
    ///     <c>RegisterHitTagTypeTrigger</c>). The id-only defs cannot supply one, so it is recorded as
    ///     the registering command's own id and kept only so a future table can distinguish them.
    /// </summary>
    public uint Tag;
}
