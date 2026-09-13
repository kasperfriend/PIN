namespace GameServer.Systems.Ai;

/// <summary>
///     One ability module of a monster behaviour set: the <c>am1</c>/<c>am2</c> parameter group of a
///     <c>dbcharacter::Monster.behavior</c> string, e.g.
///     <c>Arch_MedRangedHumanoid_Base(triggerPullTime=5000,am1Id = 33833, am1Cooldown = 1700,
///     am1Chance = 0.65, am1Timeout = 1000, am2Id = 33812, am2Cooldown = 1700, am2Chance = 0.65)</c>.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="ModuleId" /> is a <c>dbitems::AbilityModule</c> id, not an ability id: the module row's
///         <c>ability_chain_id</c> is the <c>apt::AbilityData</c> the module runs (see
///         <see cref="NpcBehaviorAbilities" />). The database spells the parameters with
///         spaces around the <c>=</c> as often as without, and the cooldown key is misspelled on nine
///         parameter occurrences (<c>am1Coodown=3000</c>, on three monsters' three behaviour columns each);
///         both spellings are read.
///     </para>
///     <para>
///         Of the build's 3,109 monster rows, 60 name a module in one of their three behaviour strings (215
///         module references in all, 26 distinct module ids); the database states the module's gates and
///         nothing about the event that fires it. The engine therefore runs a module in the attack decision
///         window its brain already has: the module is that window's action, and its chains are the
///         database's alternative to firing - the effect a module applies is what a client plays the
///         animation out of, and one module (86132, the Move Then Fire set) performs the <c>roar</c> emote.
///     </para>
///     <para>
///         The module's navigation fields are part of the record, not discarded parser noise:
///         <c>am*NavToDist</c> is the distance at which CAIS wants the NPC to navigate before the
///         module can run, and <c>am*NavTimeout</c> is its watchdog in milliseconds. The server uses
///         these values as navigation-query inputs; it does not pretend that a generic attack-range
///         check is equivalent to the original module navigation.
///     </para>
/// </remarks>
/// <param name="ModuleId">The <c>dbitems::AbilityModule</c> id the string names (<c>am1Id</c>).</param>
/// <param name="Chance">
///     Probability of using the module when it is off cooldown and in range (<c>am1Chance</c>, 0.65-1.0 on
///     the shipped rows); 1 when the string states none.
/// </param>
/// <param name="CooldownMs">
///     Milliseconds the module is locked out for after it runs (<c>am1Cooldown</c>, 1,700-20,000 on the
///     shipped rows); 0 when the string states none.
/// </param>
/// <param name="MinDistance">
///     Closest the NPC's target may be for the module to be usable (<c>am1MinDist</c>), 0 when absent.
/// </param>
/// <param name="MaxDistance">
///     Farthest the NPC's target may be for the module to be usable (<c>am1MaxDist</c>),
///     <see cref="float.MaxValue" /> when absent.
/// </param>
/// <param name="NavToDistance">
///     CAIS navigation stop distance (<c>am1NavToDist</c>), or 0 when the module does not request
///     navigation.
/// </param>
/// <param name="NavTimeoutMs">
///     CAIS navigation watchdog (<c>am1NavTimeout</c>), in milliseconds, or 0 when absent.
/// </param>
public readonly record struct NpcAbilityModule(
    uint ModuleId,
    float Chance,
    int CooldownMs,
    float MinDistance,
    float MaxDistance,
    float NavToDistance = 0f,
    int NavTimeoutMs = 0)
{
    /// <summary>The module prefixes the database spells: <c>am1</c> is the first module, <c>am2</c> the second.</summary>
    public static readonly string[] Prefixes = ["am1", "am2"];

    /// <summary>Whether a distance to the NPC's target is inside the module's own band.</summary>
    /// <param name="distance">The distance the module is being considered at, in metres.</param>
    /// <returns>Whether the module may be used at that distance.</returns>
    public bool AllowsDistance(float distance)
    {
        return distance >= MinDistance && distance <= MaxDistance;
    }
}
