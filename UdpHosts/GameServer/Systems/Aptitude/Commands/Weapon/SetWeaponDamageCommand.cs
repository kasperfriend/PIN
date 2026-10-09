using System;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Weapon;

/// <summary>
///     <c>aptfs::SetWeaponDamageCommandDef</c> (79 rows): substitutes the damage the active weapon
///     deals. The def carries no weapon-slot column, so it applies to whatever weapon is held.
///     <para>
///         The value comes from a lerp the row describes rather than from a single number. Two columns
///         name what drives it — <c>Lerpfallheight</c> (22 rows) and <c>Lerpenergy</c> (11) — and
///         <c>Lerpminvalue</c>/<c>Lerpmaxvalue</c> give that driver's scale, which is how the height
///         rows reach 250: they are metres of drop, not air time. <c>Clamplerp</c> (32 rows) clamps the
///         factor to the range instead of letting it extrapolate past the ends. The lerped factor then
///         interpolates <c>Dmgminvalue</c>..&gt;<c>Dmgmaxvalue</c>, and <c>Multiply</c> (28 rows) scales
///         the weapon's own damage by the result where <c>Set</c> (52) replaces it through
///         <c>DamageRegop</c>.
///     </para>
///     <para>
///         With neither driver set (46 rows) the factor is taken as 1, so the weapon takes the row's
///         <c>Dmgmaxvalue</c> — the value the row headlines when nothing is modulating it.
///     </para>
///     <para>
///         <c>Lerpfallheight</c> reads a real fall height, added to <c>FallDamageSystem</c> for this
///         command: it tracks the Z of the first airborne sample and the greatest drop below it. Air
///         time, which the tracker already had, cannot stand in — the rows scale to 250 and a
///         millisecond count would saturate the lerp at 1 on every fall.
///     </para>
/// </summary>
public class SetWeaponDamageCommand : Command, ICommand
{
    private SetWeaponDamageCommandDef Params;

    public SetWeaponDamageCommand(SetWeaponDamageCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character)
        {
            Logger.Debug("SetWeaponDamage {CommandId}: self is not a character, no weapon to retune", Params.Id);
            return true;
        }

        float factor = 1f; // no driver: the row's own maximum is the damage.

        if (Params.Lerpfallheight != 0 || Params.Lerpenergy != 0)
        {
            float driver = Params.Lerpenergy != 0 ? ReadEnergy(context, character) : ReadFallHeight(context, character);

            float span = Params.Lerpmaxvalue - Params.Lerpminvalue;
            factor = span != 0f ? (driver - Params.Lerpminvalue) / span : 1f;

            if (Params.Clamplerp != 0)
            {
                factor = Math.Clamp(factor, 0f, 1f);
            }
        }

        float damage = MathF.Round(Params.Dmgminvalue + ((Params.Dmgmaxvalue - Params.Dmgminvalue) * factor), 4);

        character.SetWeaponDamageOverride(new WeaponDamageOverride
        {
            Damage = damage,
            Multiply = Params.Multiply != 0,
            Regop = Params.DamageRegop,
        });

        Logger.Debug(
            "SetWeaponDamage {CommandId}: factor {Factor} over [{Min}..{Max}] -> {Damage} ({Mode}, regop {Regop})",
            Params.Id, factor, Params.Dmgminvalue, Params.Dmgmaxvalue, damage,
            Params.Multiply != 0 ? "multiply" : "set", Params.DamageRegop);

        return true;
    }

    private static float ReadFallHeight(Context context, CharacterEntity character)
    {
        return context.Shard.FallDamage?.GetFallHeight(character) ?? 0f;
    }

    private float ReadEnergy(Context context, CharacterEntity character)
    {
        if (context.Abilities == null)
        {
            return 0f;
        }

        var state = context.Abilities.GetOrAddState(character);
        state.UpdateEnergy(context.Shard.CurrentTime);

        return state.Energy;
    }
}
