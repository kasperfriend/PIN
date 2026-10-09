namespace GameServer.Entities.Character;

/// <summary>
///     A destructible body installed on a character by <c>aptfs::AddPhysicsCommandDef</c> (63 rows):
///     the frame-borne barrier/shield family, put up by a toggled ability
///     (<c>AbilityToggled → AddPhysics → ParticleEffectAsset → AudioFeedback → SetAnimCtrlParam</c>).
///     Incoming damage is absorbed by this pool before it reaches the character, and the body goes away
///     when the pool is spent or the effect that installed it ends.
/// </summary>
public class AbilityPhysicsBody
{
    /// <summary>The <c>dbphysicspostypes::PoseType</c> row id the body collides with.</summary>
    public ushort PoseTypeId;

    /// <summary>What the body absorbs, and how much of it is left.</summary>
    public uint Hitpoints;
    public float CurrentHitpoints;

    /// <summary>The <c>dbcharacter::DamageResponse</c> row used when the body is hit.</summary>
    public uint DamageResponse;

    /// <summary>Set when the body is fixed in the world rather than carried by the character.</summary>
    public bool Static;

    /// <summary>Set when the body blocks hostile fire only, not friendly.</summary>
    public bool BlockEnemiesOnly;

    /// <summary>Set when the body turns to face the character's aim.</summary>
    public bool AimOrient;

    /// <summary>Set when the body takes the character's current scale.</summary>
    public bool InheritScale;
}
