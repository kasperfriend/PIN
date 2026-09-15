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

    /// <summary>
    ///     The <c>dbdialogdata::DialogScript</c> id the behaviour names (<c>dialogScript=10551</c>),
    ///     or 0 when it names none. Seven monster rows in prod-1962 carry one; it is the line those
    ///     interactive NPCs say, not an animation.
    /// </summary>
    public uint DialogScriptId => TryGetInt("dialogScript", out int value) && value > 0 ? (uint)value : 0;

    /// <summary>
    ///     The emote the behaviour has the NPC hold (<c>emote="calm"</c>, <c>emote="townstand4"</c>,
    ///     <c>emote="dance"</c>, ...), or an empty string when it names none. It is a
    ///     <c>dbcharacter::EmoteRecord.name</c>, never an id, and 207 of the build's 3,109 monster rows
    ///     carry one - 62 distinct names, of which 60 are rows of the emote table, from <c>calm</c> (28
    ///     rows, emote 1062) and <c>officer</c> (22, 1105) down to <c>dance</c> (1) and <c>cover</c> (1456).
    ///     Two rows name an emote the table does not have (<c>waterplant01</c> on monster 999,
    ///     <c>townstand04</c> on 2481), so callers resolve the name and get nothing rather than a
    ///     substitution. Every emote in the data sits in the base <c>behavior</c> column; the same three
    ///     rows repeat it in <c>behavior_offensive</c> and <c>behavior_defensive</c>.
    /// </summary>
    public string EmoteName => _values.TryGetValue("emote", out string value) ? value.Trim() : string.Empty;

    /// <summary>
    ///     One ability module of the behaviour, read from its parameter group (<c>am1Id</c>,
    ///     <c>am1Cooldown</c>, <c>am1Chance</c>, <c>am1MinDist</c>, <c>am1MaxDist</c>), or false when the
    ///     set configures none under that prefix. The database writes the pairs with spaces around the
    ///     <c>=</c> as often as without and misspells one module's cooldown key (<c>am1Coodown</c>), which
    ///     the parser absorbs; see <see cref="NpcAbilityModule" /> for the columns' census and what the
    ///     engine does with them.
    /// </summary>
    /// <param name="prefix">The module's parameter prefix, <c>am1</c> or <c>am2</c>.</param>
    /// <param name="module">The parsed module; the default value when the method returns false.</param>
    /// <returns>Whether the behaviour configures that module.</returns>
    public bool TryGetAbilityModule(string prefix, out NpcAbilityModule module)
    {
        module = default;
        if (string.IsNullOrEmpty(prefix) || !TryGetInt(prefix + "Id", out int moduleId) || moduleId <= 0)
        {
            return false;
        }

        if (!TryGetInt(prefix + "Cooldown", out int cooldown))
        {
            // Nine occurrences spell it am1Coodown: the module's cooldown is the number either way.
            TryGetInt(prefix + "Coodown", out cooldown);
        }

        module = new NpcAbilityModule(
            (uint)moduleId,
            TryGetFloat(prefix + "Chance", out float chance) ? chance : 1f,
            cooldown,
            TryGetFloat(prefix + "MinDist", out float minDistance) ? minDistance : 0f,
            TryGetFloat(prefix + "MaxDist", out float maxDistance) ? maxDistance : float.MaxValue,
            TryGetFloat(prefix + "NavToDist", out float navToDistance) ? navToDistance : 0f,
            TryGetInt(prefix + "NavTimeout", out int navTimeoutMs) ? navTimeoutMs : 0);
        return true;
    }

    /// <summary>
    ///     Seconds the behaviour wants that emote to last (<c>emoteDuration</c>), or false when it does not
    ///     say. Every monster row that says it says <c>-1</c>, "until the behaviour changes": an NPC that
    ///     poses, works or dances holds the emote until its behaviour set does.
    /// </summary>
    /// <param name="seconds">The parsed duration in seconds (a negative value means indefinite).</param>
    /// <returns>Whether the behaviour carries an <c>emoteDuration</c>.</returns>
    public bool TryGetEmoteDurationSeconds(out int seconds) => TryGetInt("emoteDuration", out seconds);

    /// <summary>
    ///     The interaction kind the behaviour registers the NPC as (<c>interactionType="HolsterTalk"</c>,
    ///     <c>interactionType="Vendor"</c>, ...), or an empty string when it does not say. The database
    ///     spells it <c>HolsterTalk</c>/<c>holsterTalk</c>, <c>Generic</c>/<c>GENERIC</c>, <c>Vendor</c> and
    ///     <c>none</c> (the last one explicitly disables the E-key prompt), so callers compare
    ///     case-insensitively. 505 of the build's 3,109 monster rows carry one.
    /// </summary>
    public string InteractionTypeName => _values.TryGetValue("interactionType", out string value) ? value.Trim() : string.Empty;

    /// <summary>
    ///     The ability the behaviour has the NPC cast when a player finishes interacting with it
    ///     (<c>abilityId=140662</c>, the database writes spaces around the <c>=</c>), or 0 when it names
    ///     none. The <c>UseAbilityOnInteract</c> and <c>UseAbilityOnInteract_Dialog</c> behaviour sets use
    ///     it; it becomes the NPC's <see cref="Entities.InteractionComponent.CompletedAbilityId" />.
    /// </summary>
    public uint InteractAbilityId => TryGetInt("abilityId", out int value) && value > 0 ? (uint)value : 0;

    /// <summary>
    ///     Whether the behaviour set's name marks the NPC as player-interactable. Four names in prod-1962
    ///     do: <c>AlertAndInteractive</c> (the generic town-NPC set), <c>InteractiveWithEmote</c> (a
    ///     posing NPC), and <c>UseAbilityOnInteract</c>/<c>UseAbilityOnInteract_Dialog</c> (the interaction
    ///     casts an ability). A monster can still be interactable without one of these names - a
    ///     <c>vendor_id</c> makes shopkeepers and quartermasters vendors either way.
    /// </summary>
    public bool IsInteractiveBehaviorName =>
        Name.Equals("AlertAndInteractive", StringComparison.OrdinalIgnoreCase)
        || Name.Equals("InteractiveWithEmote", StringComparison.OrdinalIgnoreCase)
        || Name.Equals("UseAbilityOnInteract", StringComparison.OrdinalIgnoreCase)
        || Name.Equals("UseAbilityOnInteract_Dialog", StringComparison.OrdinalIgnoreCase);

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
        foreach (string fragment in SplitArguments(arguments))
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

    // CAIS invocations can contain quoted dialogue names and nested behaviour invocations. Splitting
    // on every comma leaks a child's parameters into its parent (e.g. ChainWithPush.behaviorB's
    // maxDistance), which is especially dangerous when those parameters decide where a body moves.
    private static IEnumerable<string> SplitArguments(string arguments)
    {
        int start = 0;
        int depth = 0;
        bool quoted = false;
        bool escaped = false;
        for (int i = 0; i < arguments.Length; i++)
        {
            char ch = arguments[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (quoted && ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (ch == '"')
            {
                quoted = !quoted;
            }
            else if (!quoted)
            {
                if (ch == '(')
                {
                    depth++;
                }
                else if (ch == ')' && depth > 0)
                {
                    depth--;
                }
                else if (ch == ',' && depth == 0)
                {
                    yield return arguments[start..i].Trim();
                    start = i + 1;
                }
            }
        }

        yield return arguments[start..].Trim();
    }

    /// <summary>Reads the two boolean spellings used by CAIS: true/false and 1/0.</summary>
    public bool TryGetBool(string key, out bool value)
    {
        value = false;
        if (!_values.TryGetValue(key, out string text))
        {
            return false;
        }

        if (text == "1" || text == "0")
        {
            value = text == "1";
            return true;
        }

        return bool.TryParse(text, out value);
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
