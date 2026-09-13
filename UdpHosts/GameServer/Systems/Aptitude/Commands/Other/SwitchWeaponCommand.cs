using AeroMessages.GSS.Character;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     <c>aptfs::SwitchWeaponCommandDef</c> (command type 97): puts the character's replicated weapon
///     selection on the row's <c>TargetWeaponSlot</c> - the swap behind every "hold this weapon while the
///     ability is out" effect in the build.
/// </summary>
/// <remarks>
///     <para>
///     The command sat behind a commented-out factory case with a <c>return true</c> placeholder body, so all
///     of it swapped nothing. The rows are real: 343 of them, slots 1-3 (never 0, never negative - the index
///     semantics of <c>LoadMonster</c>/<c>SelectWeapon</c>: 1 = <c>Weapon1Id</c>, 2 = <c>Weapon2Id</c>,
///     3 = the slot an ability-granted weapon occupies; the server resolves no item for index 3, the client
///     does from the same row), 202 rows state <c>restore_on_rollback</c>, 204 <c>play_animation</c>, 156
///     <c>forced</c>. 299 status effects carry an instance in their apply chain, 4 in their remove chain
///     (switch away when the effect ends), 2 in their update chain, and one ability chain holds one directly
///     (ability 9, a legacy row with no localization).
///     </para>
///     <para>
///     The switch happens in <see cref="Execute" />, not in an active's OnApply: the apply chains of effects
///     are the only place the ability system calls OnApply, and the command also rides ability, remove and
///     update chains. The trade is inherent to that placement: a chain that switches and then fails a later
///     requirement keeps the switch, because the effect being torn down never unwinds the actives of its
///     failed apply context (the same way every other non-active command's work survives a failed chain).
///     The rollback for the normal path is the effect's removal: like <c>CombatFlags</c> and
///     <c>SetGliderParameters</c>, the command registers an active that remembers the selection from before
///     the effect applied and writes it back on removal - and only then. A weapon the player chose themselves
///     while the effect ran is not overwritten with the snapshot: the restore fires only while the weapon is
///     still the one this command switched to, the same "only undo what this row owns" rule the flags
///     command applies to its bits. The two instances riding a remove or update chain with
///     <c>restore_on_rollback</c> 1 (1096279 in effect 11282's update chain, 1311358 in effect 13141's
///     remove chain) switch without a rollback - an active registered in a removal context is cleared
///     with it, so there is no later removal to hand the weapon back in.
///     </para>
///     <para>
///     <c>play_animation</c> writes the combat view's <c>EquipmentLoadTime</c> at the swap - the one
///     equip-timing field on the wire, written once at spawn until now. The client's draw/swap animation is
///     client-side; this timestamp is the only thing the server can hand it, and the row's flag is what says
///     the swap should be animated at all. A row without the flag changes the selection silently.
///     <c>forced</c> has no distinguishable server-side gate - the selection is plain replicated state and
///     both values of the column switch it - so it is left to the client-side presentation the server does
///     not own, exactly like the unset movement-and-facing columns of the behaviour modules.
///     </para>
/// </remarks>
public class SwitchWeaponCommand : Command, ICommand
{
    private SwitchWeaponCommandDef Params;

    public SwitchWeaponCommand(SwitchWeaponCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character)
        {
            // Deployable owned chains reach this command with the deployable as self. A deployable has no
            // weapon selection to swap, and failing the command would take the whole effect down with it,
            // so step over it the same way the other actives do.
            Logger.Debug("[{Command} {CommandId}] does nothing because self is {SelfType}",
                nameof(SwitchWeaponCommand), Params.Id, context.Self?.GetType().Name ?? "nothing");
            return true;
        }

        // The row's slot is a signed byte and every row of the build states 1-3; a negative slot (a form of
        // "no change" the data never uses) is stepped over rather than wrapped onto a bogus index.
        if (Params.TargetWeaponSlot < 0)
        {
            return true;
        }

        byte slot = (byte)Params.TargetWeaponSlot;
        if (character.WeaponIndex.Index == slot)
        {
            return true;
        }

        var previous = character.WeaponIndex;
        uint now = context.Shard.CurrentTime;

        // Switching weapons never keeps the sights of the previous one - the same rule the player's own
        // SelectWeapon packet is handled with: a scope effect whose removal got lost must not hold the zoom
        // over the weapon the character is now holding.
        character.SetScopedState(false);

        character.SetWeaponIndex(new WeaponIndexData
        {
            Index = slot,
            Unk1 = 1,
            Unk2 = 0,
            Time = now,
        });

        if (Params.PlayAnimation == 1)
        {
            character.SetEquipmentLoadTime(now);
        }

        if (Params.RestoreOnRollback == 1)
        {
            context.Actives.Add(this, new SwitchWeaponActiveContext
            {
                Previous = previous,
                SwitchedTo = slot,
            });
        }

        return true;
    }

    public void OnRemove(Context context, ICommandActiveContext activeCommandContext)
    {
        if (activeCommandContext is not SwitchWeaponActiveContext active)
        {
            return;
        }

        if (context.Self is not CharacterEntity character)
        {
            return;
        }

        // Only the selection this command wrote is rolled back, and only while it is still in place: a
        // weapon the player (or another effect) chose while this effect ran is theirs, not the snapshot's.
        if (character.WeaponIndex.Index != active.SwitchedTo)
        {
            return;
        }

        uint now = context.Shard.CurrentTime;
        character.SetScopedState(false);
        character.SetWeaponIndex(new WeaponIndexData
        {
            Index = active.Previous.Index,
            Unk1 = active.Previous.Unk1,
            Unk2 = active.Previous.Unk2,
            Time = now,
        });

        if (Params.PlayAnimation == 1)
        {
            character.SetEquipmentLoadTime(now);
        }
    }

    /// <summary>
    ///     The selection the character held before the effect, and the slot this command switched to.
    /// </summary>
    private class SwitchWeaponActiveContext : ICommandActiveContext
    {
        public WeaponIndexData Previous;
        public byte SwitchedTo;
    }
}
