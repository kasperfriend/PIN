using System;
using System.Numerics;

namespace GameServer.Systems.Ai;

public enum NpcRoutineState
{
    Inactive,
    Waiting,
    Walking,
    Working,
    Returning,
    Suspended,
    MissingRoute,
}

/// <summary>A real, placed work/rest deployable, reserved for one NPC.</summary>
public readonly record struct NpcActivitySpot(
    ulong EntityId,
    Vector3 Position,
    Quaternion Orientation,
    ushort EmoteId,
    int DurationMs,
    bool Holster = false,
    ushort EndEmoteId = 0);

/// <summary>Activity locations must come from entities in the loaded world, not template coordinates.</summary>
public interface INpcActivityWorld
{
    bool TryReserve(ulong npcId, string function, Vector3 position, Vector3 home, float radius, ulong previousSpot, out NpcActivitySpot spot);
    bool IsValid(ulong npcId, in NpcActivitySpot spot);
    void Release(ulong npcId);
    void Clear();
}

/// <summary>
///     A small ambient scheduler, separate from combat. Goals are requests to the existing navigation
///     system, never teleports. Combat interrupts a routine; resuming first returns to its original
///     home. Failure, removal and death release work reservations. All times are shard milliseconds.
/// </summary>
public sealed class NpcRoutine
{
    public const float ArrivalRadius = 0.4f;
    private const float ArrivalHeight = 0.4f;
    private readonly ulong _entityId;
    private readonly INpcActivityWorld _activities;
    private uint _random;
    private ulong _nextActionAt;
    private ulong _lastProgressAt;
    private Vector3 _lastProgressPosition;
    private NpcActivitySpot? _spot;
    private ulong _lastSpotId;
    private bool _returnBeforeResuming;
    private ushort _endEmote;
    private bool _stopped;

    public NpcRoutine(ulong entityId, uint monsterId, Vector3 home, NpcRoutineProfile profile, ulong now, INpcActivityWorld activities)
    {
        _entityId = entityId;
        Home = home;
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _activities = activities;
        // Mix the whole id. Firefall entity ids' low byte is a controller discriminator, not entropy.
        _random = unchecked((uint)entityId ^ (uint)(entityId >> 32) ^ monsterId ^ 0x9E3779B9u);
        if (_random == 0)
        {
            _random = 1;
        }

        // Per-NPC deterministic EffectiveHomeRadius = base + rand(0..jitter) seeded from entityId^monsterId
        // Different swarm members get different radii, same id replays exactly. Clamped to leash safety in profile.
        float jitter = 0f;
        if (profile.MaxDistJitter > 0f)
        {
            // Use UnitRandom once for jitter, but keep _random for future rest durations - replayable because _random is seeded
            // We need a separate deterministic rand for jitter that doesn't consume the main _random sequence for rest
            // Use a hash of entityId+monsterId for jitter
            uint jitterHash = unchecked((uint)entityId ^ monsterId ^ 0x9E3779B9u ^ 0x85EBCA6Bu);
            if (jitterHash == 0) jitterHash = 1;
            jitterHash ^= jitterHash << 13;
            jitterHash ^= jitterHash >> 17;
            jitterHash ^= jitterHash << 5;
            float unit = (jitterHash >> 8) * (1f / 16777216f);
            jitter = unit * profile.MaxDistJitter;
        }

        EffectiveHomeRadius = profile.HomeRadius + jitter;
        if (profile.LeashDistance is float leash && leash > 0f)
        {
            EffectiveHomeRadius = MathF.Min(EffectiveHomeRadius, leash);
        }

        State = profile.Kind == NpcRoutineKind.ExternalRoute ? NpcRoutineState.MissingRoute
            : profile.HasRoutine ? NpcRoutineState.Waiting : NpcRoutineState.Inactive;
        _nextActionAt = AddTime(now, RestDuration());
    }

    public NpcRoutineProfile Profile { get; }
    public Vector3 Home { get; }
    public float EffectiveHomeRadius { get; }
    public Vector3? Goal { get; private set; }
    public NpcRoutineState State { get; private set; }
    public ulong NextActionAt => _nextActionAt;
    public bool IsWorking => State == NpcRoutineState.Working;
    public bool Holster => IsWorking && _spot?.Holster == true;
    public Quaternion? Facing => IsWorking ? _spot?.Orientation : null;

    /// <summary>Null leaves the monster's base emote alone; zero explicitly stops it for travel.</summary>
    public ushort? EmoteOverride => Goal.HasValue ? (ushort)0
        : IsWorking ? _spot?.EmoteId
        : _endEmote != 0 ? _endEmote : null;

    public void Update(ulong now, Vector3 position, bool idle, bool canMove, bool navigationAvailable)
    {
        if (_stopped)
        {
            return;
        }

        if (!idle)
        {
            Suspend();
            return;
        }

        if (!Profile.HasRoutine || !Finite(position) || !Finite(Home))
        {
            return;
        }

        // Despawn distance is an explicit authored limit; an NPC beyond it is outside its
        // original leash and should not keep wandering. We park it as Inactive rather than
        // invent a teleport - the world-population slot still owns its lifetime.
        if (Profile.DespawnDistance > 0f && AiVectors.HorizontalDistance(position, Home) > Profile.DespawnDistance + ArrivalRadius)
        {
            if (AiVectors.HorizontalDistance(position, Home) > EffectiveHomeRadius + Profile.DespawnDistance)
            {
                Stop();
                return;
            }
        }

        if (!navigationAvailable)
        {
            ReleaseSpot();
            Goal = null;
            State = NpcRoutineState.Waiting;
            _nextActionAt = AddTime(now, Profile.RetryMs);
            return;
        }

        if (!canMove)
        {
            // A movement restriction/aptitude slide owns the body. It is not a stuck route.
            _lastProgressAt = now;
            return;
        }

        if (State == NpcRoutineState.Suspended)
        {
            State = NpcRoutineState.Waiting;
            _nextActionAt = now;
        }

        if (_spot.HasValue && _activities?.IsValid(_entityId, _spot.Value) != true)
        {
            Blocked(now);
            return;
        }

        if (IsWorking)
        {
            if (!_spot.HasValue || !Arrived(position, _spot.Value.Position))
            {
                Blocked(now);
                return;
            }

            if (now >= _nextActionAt)
            {
                _endEmote = _spot?.EndEmoteId ?? 0;
                ReleaseSpot();
                Wait(now);
            }

            return;
        }

        if (Goal.HasValue)
        {
            if (Arrived(position, Goal.Value))
            {
                Goal = null;
                if (_spot.HasValue)
                {
                    State = NpcRoutineState.Working;
                    int duration = _spot.Value.DurationMs;
                    _nextActionAt = duration < 0 ? ulong.MaxValue : AddTime(now, duration);
                }
                else
                {
                    _returnBeforeResuming = false;
                    Wait(now);
                }
            }
            else if (AiVectors.HorizontalDistance(position, _lastProgressPosition) >= 0.1f)
            {
                _lastProgressPosition = position;
                _lastProgressAt = now;
            }
            else if (now >= AddTime(_lastProgressAt, Profile.StuckTimeoutMs))
            {
                Blocked(now);
            }

            return;
        }

        if (now < _nextActionAt)
        {
            return;
        }

        if (_returnBeforeResuming || AiVectors.HorizontalDistance(position, Home) > EffectiveHomeRadius + ArrivalRadius)
        {
            if (!Arrived(position, Home))
            {
                StartGoal(Home, position, now, returning: true);
                return;
            }

            _returnBeforeResuming = false;
        }

        _endEmote = 0;
        if (Profile.WorkFunction.Length > 0 && _activities != null && _activities.TryReserve(
            _entityId, Profile.WorkFunction, position, Home, EffectiveHomeRadius, _lastSpotId, out var spot))
        {
            _spot = spot;
            _lastSpotId = spot.EntityId;
            StartGoal(spot.Position, position, now);
            return;
        }

        if (Profile.Kind == NpcRoutineKind.Work || Profile.WanderDistance <= ArrivalRadius ||
            Profile.WanderChance <= 0f || UnitRandom() >= Profile.WanderChance)
        {
            _nextActionAt = AddTime(now, Math.Max(Profile.RetryMs, RestDuration()));
            State = NpcRoutineState.Waiting;
            return;
        }

        // A bounded random destination, not an alleged original patrol. distance limits one leg;
        // maxDistFromSpawn / nearSpawn constrain its origin. Never let an unbounded random walk
        // carry a streamed population NPC away from the slot that owns its lifetime.
        var center = Profile.NearSpawn ? Home : position;
        float angle = UnitRandom() * (2f * MathF.PI);
        float distance = Profile.WanderDistance * MathF.Sqrt(0.25f + (0.75f * UnitRandom()));
        var goal = center + new Vector3(MathF.Cos(angle) * distance, MathF.Sin(angle) * distance, 0f);
        var fromHome = goal - Home;
        fromHome.Z = 0f;
        float radius = fromHome.Length();
        if (radius > EffectiveHomeRadius && radius > 0f)
        {
            fromHome *= EffectiveHomeRadius / radius;
            goal.X = Home.X + fromHome.X;
            goal.Y = Home.Y + fromHome.Y;
        }

        // nearSpawn changes the sampling centre, not permission to take a leg twice as long
        // between opposite sides of the home circle. Keep both envelopes bounded.
        var leg = goal - position;
        leg.Z = 0f;
        float legLength = leg.Length();
        if (legLength > Profile.WanderDistance)
        {
            leg *= Profile.WanderDistance / legLength;
            goal.X = position.X + leg.X;
            goal.Y = position.Y + leg.Y;
        }

        // Navigation determines Z, relative to the NPC's current floor, not a roof above its home.
        goal.Z = position.Z;
        if (Arrived(position, goal))
        {
            Blocked(now);
            return;
        }

        StartGoal(goal, position, now);
    }

    /// <summary>Accept the navigation mesh's ground height for a generated destination, not for a workstation.</summary>
    public void ProjectGoal(Vector3 projected)
    {
        if (Goal.HasValue && !_spot.HasValue && Finite(projected) &&
            AiVectors.HorizontalDistance(Goal.Value, projected) <= ArrivalRadius)
        {
            Goal = projected;
        }
    }

    public void Blocked(ulong now)
    {
        ReleaseSpot();
        Goal = null;
        _endEmote = 0;
        // despawnWhenStuck is an explicit authored flag (32 rows). The original game could despawn
        // a stuck NPC; PIN parks it as Inactive rather than teleporting or respawning, which keeps
        // the world-population slot from spinning on an unreachable point while staying faithful to
        // the request that this NPC should not keep retrying forever.
        if (Profile.DespawnWhenStuck)
        {
            State = NpcRoutineState.Inactive;
            _stopped = true;
            _nextActionAt = ulong.MaxValue;
            return;
        }
        State = NpcRoutineState.Waiting;
        _nextActionAt = AddTime(now, Profile.RetryMs);
    }

    public void Suspend()
    {
        ReleaseSpot();
        Goal = null;
        _endEmote = 0;
        if (Profile.HasRoutine)
        {
            _returnBeforeResuming = true;
            State = NpcRoutineState.Suspended;
        }
    }

    public void Stop()
    {
        _stopped = true;
        ReleaseSpot();
        Goal = null;
        _endEmote = 0;
        State = NpcRoutineState.Inactive;
    }

    private void ReleaseSpot()
    {
        if (_spot.HasValue)
        {
            _activities?.Release(_entityId);
            _spot = null;
        }
    }

    private void StartGoal(Vector3 goal, Vector3 position, ulong now, bool returning = false)
    {
        Goal = goal;
        _lastProgressAt = now;
        _lastProgressPosition = position;
        State = returning ? NpcRoutineState.Returning : NpcRoutineState.Walking;
    }

    private void Wait(ulong now)
    {
        State = NpcRoutineState.Waiting;
        _nextActionAt = AddTime(now, RestDuration());
    }

    private int RestDuration()
    {
        int min = Math.Max(0, Profile.RestMinMs);
        int max = Math.Max(min, Profile.RestMaxMs);
        return min + (int)((max - (long)min) * UnitRandom());
    }

    private float UnitRandom()
    {
        _random ^= _random << 13;
        _random ^= _random >> 17;
        _random ^= _random << 5;
        return (_random >> 8) * (1f / 16777216f);
    }

    private static bool Arrived(Vector3 position, Vector3 goal)
        => AiVectors.HorizontalDistance(position, goal) <= ArrivalRadius &&
           AiVectors.HeightDelta(position, goal) <= ArrivalHeight;

    private static ulong AddTime(ulong now, int milliseconds)
    {
        ulong delay = (ulong)Math.Max(0, milliseconds);
        return ulong.MaxValue - now < delay ? ulong.MaxValue : now + delay;
    }

    private static bool Finite(Vector3 point)
        => float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z);
}
