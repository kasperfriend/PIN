using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>aptfs::RequireItemDurabilityCommandDef</c> (9 rows): the gate that stops an ability being used
///     through worn-out gear — <c>ActiveInitiation → RequireItemDurability → TimeCooldown →
///     StatRequirement → ImpactApplyEffect</c>, i.e. it is the first thing checked, before the ability
///     commits its cooldown.
///     <para>
///         The three comparison columns are <b>OR</b>ed, not ANDed, and the data is what settles it:
///         <c>greater_than</c> is set in all 9 rows while <c>equal_to</c> is set in 5 of them, all with
///         <c>durability_amount</c> 400. Read as a conjunction those 5 rows would ask for a durability
///         that is both equal to 400 and greater than 400 — impossible, so the ability could never be
///         used. Read as a disjunction they ask for "at least 400", and the remaining 4 rows
///         (<c>greater_than</c> with amount 0) ask for "any durability left". Both are gates worth
///         having; only one reading makes the table consistent.
///     </para>
///     <para>
///         A row that sets none of the three asks no question and so passes. An empty slot fails: there
///         is no item whose durability could satisfy the row.
///     </para>
/// </summary>
public class RequireItemDurabilityCommand : Command, ICommand
{
    private RequireItemDurabilityCommandDef Params;

    public RequireItemDurabilityCommand(RequireItemDurabilityCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.EqualTo == 0 && Params.LessThan == 0 && Params.GreaterThan == 0)
        {
            return true;
        }

        if (context.Self is not CharacterEntity character)
        {
            return false;
        }

        if (character.Player?.Inventory == null
            || !character.Player.Inventory.TryGetEquippedItemInSlot((LoadoutSlotType)Params.SlotType, out var item))
        {
            Logger.Debug("RequireItemDurability {CommandId}: nothing equipped in slot {Slot}", Params.Id, Params.SlotType);
            return false;
        }

        bool satisfied = (Params.EqualTo != 0 && item.Durability == Params.DurabilityAmount)
            || (Params.LessThan != 0 && item.Durability < Params.DurabilityAmount)
            || (Params.GreaterThan != 0 && item.Durability > Params.DurabilityAmount);

        Logger.Debug(
            "RequireItemDurability {CommandId}: slot {Slot} item {Item} durability {Durability} vs {Amount} -> {Result}",
            Params.Id, Params.SlotType, item.SdbId, item.Durability, Params.DurabilityAmount, satisfied);

        return satisfied;
    }
}
