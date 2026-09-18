using GameServer.StaticDB;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>aptfs::RequireDamageResponseCommandDef</c>: asks how a <c>dbcharacter::DamageResponse</c>
///     table reacts to a damage type. The def names the response table explicitly
///     (<c>DamageresponseId</c>), so the gate reads that table directly rather than guessing which
///     entity it belongs to; the multiplier resolution mirrors <c>DamageSystem</c> (per-type row
///     first, the table's DefaultMultiplier otherwise, 1.0 when the table is unknown).
///     <c>NotInvulnerable</c> requires the multiplier to be non-zero;
///     <c>VulnerableTol</c> requires it to reach the tolerance.
/// </summary>
public class RequireDamageResponseCommand : Command, ICommand
{
    private RequireDamageResponseCommandDef Params;

    public RequireDamageResponseCommand(RequireDamageResponseCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.UseWeaponDamageType == 1)
        {
            // No weapon -> damage-type mapping is loadable from the SDB tables we
            // have, so the weapon-driven ask falls back to the row's explicit type.
            Logger.Debug(
                "{Command} {CommandId}: UseWeaponDamageType requested, no weapon damage-type table is loadable; using VulnerableDamagetypeId",
                nameof(RequireDamageResponseCommand), Params.Id);
        }

        byte damageType = Params.VulnerableDamagetypeId;

        var typeResponse = SDBInterface.GetDamageResponseDamageType((byte)Params.DamageresponseId, damageType);
        float multiplier = typeResponse?.Multiplier
            ?? SDBInterface.GetDamageResponse((byte)Params.DamageresponseId)?.DefaultMultiplier
            ?? 1f;

        bool result = true;

        if (Params.NotInvulnerable == 1)
        {
            result = result && multiplier > 0f;
        }

        if (Params.VulnerableTol > 0f)
        {
            result = result && multiplier >= Params.VulnerableTol;
        }

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }
}
