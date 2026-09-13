using System.Numerics;

namespace GameServer.Systems.Aptitude;

/// <summary>
///     A displacement the database asked a character to make over time
///     (<c>aptfs::MovementSlideCommandDef</c>): from <see cref="StartPosition" /> along
///     <see cref="Offset" />, finished after <see cref="DurationMs" /> milliseconds.
/// </summary>
/// <remarks>
///     The command that starts a slide runs once, inside the apply chain of the effect it sits in, so the
///     motion it declares has to be carried by something that ticks: <see cref="AbilitySystem" /> keeps the
///     active slide per entity and advances it on the same sweep that evaluates effects
///     (<see cref="AbilitySystem.ProcessTarget" />). A slide outlives its effect by design - the dodge pair's
///     effect lasts 500 ms while the sidestep it declares runs 667 ms - so it is not stored on the effect
///     state. The endpoint is applied exactly when the slide ends, so a character lands on the offset the
///     row states rather than a tick short of it.
/// </remarks>
public sealed class MovementSlide
{
    public MovementSlide(ulong entityId, uint startTime, uint durationMs, Vector3 startPosition, Vector3 offset)
    {
        EntityId = entityId;
        StartTime = startTime;
        DurationMs = durationMs;
        StartPosition = startPosition;
        Offset = offset;
    }

    /// <summary>The character being moved.</summary>
    public ulong EntityId { get; }

    /// <summary>Server time (ms) the slide started at.</summary>
    public uint StartTime { get; }

    /// <summary>How long the displacement takes (<c>move_duration</c>, or the distance over a fixed speed).</summary>
    public uint DurationMs { get; }

    /// <summary>Where the character was when the slide started.</summary>
    public Vector3 StartPosition { get; }

    /// <summary>The world-space displacement, resolved from the row's offset once, at the start.</summary>
    public Vector3 Offset { get; }

    /// <summary>Where the slide ends: the start plus the whole offset.</summary>
    public Vector3 EndPosition => StartPosition + Offset;

    /// <summary>The server time (ms) the slide ends at.</summary>
    public ulong EndTime => StartTime + DurationMs;

    /// <summary>Whether the slide is still running at <paramref name="now" />.</summary>
    public bool IsActive(ulong now) => now < EndTime;

    /// <summary>
    ///     The position the slide puts the character at, <paramref name="now" />: a straight interpolation
    ///     between the endpoints, clamped to them.
    /// </summary>
    public Vector3 PositionAt(ulong now)
    {
        if (DurationMs == 0 || now <= StartTime)
        {
            return StartPosition;
        }

        ulong elapsed = now - StartTime;
        if (elapsed >= DurationMs)
        {
            return EndPosition;
        }

        return StartPosition + (Offset * (elapsed / (float)DurationMs));
    }
}
