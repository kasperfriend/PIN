using System;
using System.Collections.Generic;
using System.Globalization;

namespace GameServer.Systems.Ai;

/// <summary>
///     The parameter list of a <c>dbcharacter::Monster</c> behaviour string, parsed into
///     name/value pairs.
/// </summary>
/// <remarks>
///     <para>
///     A monster row's <c>behavior</c> column is a CAIS behaviour set invocation such as
///     <c>Arch_MedRangedHumanoid_Attack(triggerPullTime=1500,fireRestDuration=2000,reviveOn=1)</c>.
///     The name picks the client-side behaviour tree; the parameters in the parentheses are the
///     numbers the database used to tune it, and they are the only place the original game's
///     <b>AI</b> attack timing lives: the weapon template's <c>ms_per_burst</c> is the client's
///     fire animation cadence (50-100 ms for several NPC weapons), while the behaviour carries the
///     real cycle - every ranged humanoid in build prod-1962 pulls its trigger after
///     <c>triggerPullTime</c> (median 1,500 ms) and rests for <c>fireRestDuration</c> (median
///     1,000 ms) afterwards.
///     </para>
///     <para>
///     Parsing is deliberately tolerant: unknown keys are kept, repeated keys take the last value,
///     malformed fragments are dropped, and a string with no parentheses at all
///     (<c>AggressiveWanderer</c>, <c>Null</c>, ...) parses to just a name. The database also has
///     misspelled keys (<c>am1Coodown</c>), so callers must look up the keys they know instead of
///     assuming a fixed vocabulary.
///     </para>
/// </remarks>
public sealed class NpcBehaviorParams
{
    /// <summary>An empty parameter set, for monsters whose row has no behaviour string.</summary>
    public static readonly NpcBehaviorParams Empty = new(string.Empty, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private readonly Dictionary<string, string> _values;

    private NpcBehaviorParams(string name, Dictionary<string, string> values)
    {
        Name = name;
        _values = values;
    }

    /// <summary>The behaviour set name, i.e. everything before the first <c>(</c>.</summary>
    public string Name { get; }

    /// <summary>Every <c>key=value</c> pair inside the parentheses, keyed case-insensitively.</summary>
    public IReadOnlyDictionary<string, string> Values => _values;

    /// <summary>Milliseconds the behaviour waits before pulling the trigger, or 0 when it does not say.</summary>
    public int TriggerPullTimeMs => TryGetInt("triggerPullTime", out int value) ? value : 0;

    /// <summary>Milliseconds the behaviour rests after firing, or 0 when it does not say.</summary>
    public int FireRestDurationMs => TryGetInt("fireRestDuration", out int value) ? value : 0;

    /// <summary>Distance in metres the behaviour fights at, or 0 when it does not say.</summary>
    public float CombatDistance => TryGetFloat("combatDist", out float value) ? value : 0f;

    /// <summary>Distance in metres the behaviour prefers to keep from its target, or 0 when it does not say.</summary>
    public float PreferredMinimumCombatDistance => TryGetFloat("preferredMinCombatDist", out float value) ? value : 0f;

    /// <summary>Whether the behaviour carries any of the parameters that drive a ranged attack cycle.</summary>
    public bool HasAttackTiming => TriggerPullTimeMs > 0 || FireRestDurationMs > 0;

    /// <summary>Parses a behaviour string. Never returns null; an empty input gives <see cref="Empty" />.</summary>
    public static NpcBehaviorParams Parse(string behavior)
    {
        if (string.IsNullOrWhiteSpace(behavior))
        {
            return Empty;
        }

        string text = behavior.Trim();
        int open = text.IndexOf('(');
        if (open < 0)
        {
            return new NpcBehaviorParams(text, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        string name = text[..open].Trim();
        int close = text.LastIndexOf(')');
        string arguments = close > open ? text[(open + 1)..close] : text[(open + 1)..];

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string fragment in arguments.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int equals = fragment.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            string key = fragment[..equals].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            values[key] = fragment[(equals + 1)..].Trim().Trim('"');
        }

        return new NpcBehaviorParams(name, values);
    }

    /// <summary>Reads an integer parameter, returning false when it is absent or not a number.</summary>
    public bool TryGetInt(string key, out int value)
    {
        value = 0;
        return _values.TryGetValue(key, out string text)
            && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Reads a floating point parameter, returning false when it is absent or not a number.</summary>
    public bool TryGetFloat(string key, out float value)
    {
        value = 0f;
        return _values.TryGetValue(key, out string text)
            && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
