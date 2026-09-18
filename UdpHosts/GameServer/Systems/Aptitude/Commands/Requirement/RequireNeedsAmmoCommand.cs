using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

public class RequireNeedsAmmoCommand : Command, ICommand
{
    private RequireNeedsAmmoCommandDef Params;

    public RequireNeedsAmmoCommand(RequireNeedsAmmoCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        bool result = false;

        // NOTE: Investigate target handling
        var target = context.Self;

        if (target is CharacterEntity character)
        {
            // Controller props rechecked: the combat view carries TWO ammo slots
            // (Ammo_0/AltAmmo_0 for weapon index 1, Ammo_1/AltAmmo_1 for index 2) and
            // the gate has to ask about the weapon the character currently holds;
            // reading slot 0 unconditionally mis-evaluates every off-hand weapon.
            //
            // Known limitation: those props are initialized at view creation (88/52)
            // and the fire/reload path never rewrites them, so until a server-side
            // ammo simulation lands (the burst/reload events in Systems/Combat are
            // the natural hook) this gate always answers "has ammo".
            var weaponIndex = character.WeaponIndex.Index;
            var (clip, altClip) = weaponIndex >= 2
                ? (character.Character_CombatController.Ammo_1Prop, character.Character_CombatController.AltAmmo_1Prop)
                : (character.Character_CombatController.Ammo_0Prop, character.Character_CombatController.AltAmmo_0Prop);

            if (Params.CheckPrimary == 1)
            {
                result = clip == 0;
            }

            if (Params.CheckSecondary == 1)
            {
                result = result || altClip == 0;
            }
        }

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }
}