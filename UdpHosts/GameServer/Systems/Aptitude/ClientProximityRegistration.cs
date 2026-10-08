namespace GameServer.Systems.Aptitude;

/// <summary>
/// A proximity trigger registered by <c>RegisterClientProximityCommand</c>: every
/// <see cref="RetryInterval"/> ms the ability system scans for entities within <see cref="Radius"/> of
/// the registering entity and fires <see cref="Chain"/> (or activates <see cref="AbilityId"/>) on up to
/// <see cref="MaxTargets"/> of them. Lives exactly as long as the effect that carried the command.
/// </summary>
public class ClientProximityRegistration
{
    public ulong OwnerEntityId;
    public uint Chain;
    public uint AbilityId;
    public uint MaxTargets;
    public uint RetryInterval;
    public float Radius;

    /// <summary>Shard time the scan last ran, so the row's retry interval is honoured instead of firing every tick.</summary>
    public ulong LastRunTime;
}
