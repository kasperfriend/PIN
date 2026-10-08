using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Weapon;

/// <summary>
///     <c>aptfs::SlotAmmoCommandDef</c> (115 rows, 13 stock-kit nodes): substitutes the ammo a weapon
///     slot fires - the "load special rounds" family. It was an unconditional <c>return true</c>, and
///     the weapon simulation carried the matching hole as an explicit
///     <c>// TODO: Handle ammo overrides</c>; this command and that hook are the two halves of the same
///     missing feature.
///     <para>
///         The substitution is stored on the character (<see cref="AmmoSlotOverride" />) rather than in
///         the weapon-details cache, because that cache is rebuilt on every loadout change and an
///         override living in it would vanish mid-ability.
///     </para>
///     <para>
///         <c>TargetWeaponSlot</c> selects the slot and <c>AltWeapon</c> the fire-mode variant of it; a
///         row that leaves both at 0 (the common case) means the weapon held right now, in the mode it
///         is held in. <c>ReplaceAmmoType</c> makes the substitution conditional on the weapon actually
///         being loaded with that ammo row, and is evaluated where the current row is known - in
///         <c>CharacterEntity.GetActiveAmmoOverride</c>. <c>WeaponDamageAdd</c> and
///         <c>WeaponDamageMult</c> offset the weapon's resolved round damage, additive first, and the
///         weapon simulation applies them. <c>RestoreOnRollback</c> registers an activation rollback so
///         a failed activation takes the substitution back off.
///     </para>
///     <para>
///         <c>CreditToAbility</c> is not honoured: which ability the rounds are credited to has no
///         representation on the server's projectile path, and guessing would attribute kills wrongly
///         rather than not at all.
///     </para>
/// </summary>
public class SlotAmmoCommand : Command, ICommand
{
    private SlotAmmoCommandDef Params;

    public SlotAmmoCommand(SlotAmmoCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character)
        {
            Logger.Debug("SlotAmmo {CommandId}: self is not a character, no weapon to reload", Params.Id);
            return true;
        }

        if (Params.AmmoType == 0)
        {
            // No substitute named: a row like this clears the slot back to the weapon's own rounds.
            byte clearSlot = Params.TargetWeaponSlot;
            byte clearMode = ResolveMode(character, Params);
            character.SetAmmoOverride(clearSlot, clearMode, null);
            return true;
        }

        byte slot = Params.TargetWeaponSlot;
        byte mode = ResolveMode(character, Params);

        var overrideInfo = new AmmoSlotOverride
        {
            AmmoType = Params.AmmoType,
            ReplaceAmmoType = Params.ReplaceAmmoType,
            WeaponDamageAdd = Params.WeaponDamageAdd,
            WeaponDamageMult = Params.WeaponDamageMult,
            AbilityId = context.AbilityId,
        };

        character.SetAmmoOverride(slot, mode, overrideInfo);

        Logger.Debug(
            "SlotAmmo {CommandId}: slot {Slot}/{Mode} now fires ammo {Ammo} (replaces {Replace}, damage +{Add} x{Mult})",
            Params.Id, slot, mode, Params.AmmoType, Params.ReplaceAmmoType, Params.WeaponDamageAdd, Params.WeaponDamageMult);

        if (Params.RestoreOnRollback != 0)
        {
            context.ActivationRollbacks.Add(() => character.ClearAmmoOverridesForAbility(overrideInfo.AbilityId));
        }

        return true;
    }

    /// <summary>
    ///     The fire-mode variant the row addresses. A row that names neither a slot nor an alt weapon
    ///     means "the weapon as I am holding it", so it follows the mode the character is in; a row that
    ///     names one addresses that variant explicitly.
    /// </summary>
    private static byte ResolveMode(CharacterEntity character, SlotAmmoCommandDef paramsDef)
    {
        if (paramsDef.TargetWeaponSlot == 0 && paramsDef.AltWeapon == 0)
        {
            return character.GetActiveFireModeIndex();
        }

        return paramsDef.AltWeapon;
    }
}
