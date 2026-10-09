using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Target;

/// <summary>
///     <c>aptfs::TargetByDamageResponseCommandDef</c> (243 rows): keeps the targets whose damage
///     response says the row's damage type actually lands on them. It is the target-filter sibling of
///     <c>RequireDamageResponse</c> and resolves the multiplier exactly the way that gate and
///     <c>DamageSystem</c> do - the per-type row first, the table's <c>DefaultMultiplier</c>
///     otherwise, 1.0 when the table is unknown.
///     <para>
///         Which table it reads: the <b>target's own</b> <c>DamageResponseId</c>, falling back to the
///         def's <c>DamageresponseId</c> when the target has none. A filter that read only the def's
///         table would give every target the same answer and could not filter anything; and most
///         <c>dbcharacter::Monster</c> rows (2993 of them) carry <c>damage_response_id = 0</c>, so the
///         def's column is what makes those rows answerable at all. The most common def value, 4, is
///         the table literally named "Machinery (UNUSED)" with <c>default_multiplier</c> 1.0 - a
///         permissive fallback, which is consistent with that reading.
///     </para>
///     <para>
///         The test itself: <c>NotInvulnerable</c> (1 in 75 rows) asks for a nonzero multiplier and
///         <c>VulnerableTol</c> (nonzero in 1 row) raises the bar to that value. When neither is set -
///         168 of the 243 rows - the filter falls back to the question its name asks, "does this
///         damage type land at all" (multiplier &gt; 0); taking <c>RequireDamageResponse</c>'s
///         vacuous-true default here instead would leave the command a no-op on the majority of rows.
///         <c>Negate</c> (1 in 170 rows - the majority) inverts the whole answer, which is what makes
///         "only the targets this damage type does <em>not</em> touch" expressible.
///     </para>
///     <para>
///         <c>UseWeaponDamageType</c> is 0 in all 243 rows, so the weapon-driven branch stays
///         undocumented rather than guessed; the row's explicit type is used.
///     </para>
/// </summary>
public class TargetByDamageResponseCommand : Command, ICommand
{
    private TargetByDamageResponseCommandDef Params;

    public TargetByDamageResponseCommand(TargetByDamageResponseCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.UseWeaponDamageType == 1)
        {
            Logger.Debug(
                "{Command} {CommandId}: UseWeaponDamageType requested, no weapon damage-type table is loadable; using VulnerableDamagetypeId",
                nameof(TargetByDamageResponseCommand), Params.Id);
        }

        byte damageType = Params.VulnerableDamagetypeId;

        var previousTargets = context.Targets;
        var newTargets = new AptitudeTargets();

        foreach (IAptitudeTarget target in previousTargets)
        {
            byte responseId = target switch
            {
                CharacterEntity character => character.DamageResponseId,
                DeployableEntity deployable => deployable.DamageResponseId,
                _ => (byte)0,
            };

            if (responseId == 0)
            {
                responseId = (byte)Params.DamageresponseId;
            }

            var typeResponse = SDBInterface.GetDamageResponseDamageType(responseId, damageType);
            float multiplier = typeResponse?.Multiplier
                ?? SDBInterface.GetDamageResponse(responseId)?.DefaultMultiplier
                ?? 1f;

            bool match;
            if (Params.NotInvulnerable == 1 || Params.VulnerableTol > 0f)
            {
                match = true;
                if (Params.NotInvulnerable == 1)
                {
                    match = match && multiplier > 0f;
                }

                if (Params.VulnerableTol > 0f)
                {
                    match = match && multiplier >= Params.VulnerableTol;
                }
            }
            else
            {
                match = multiplier > 0f;
            }

            if (Params.Negate == 1)
            {
                match = !match;
            }

            if (match)
            {
                newTargets.Push(target);
            }
        }

        context.FormerTargets = previousTargets;
        context.Targets = newTargets;

        return true;
    }
}
