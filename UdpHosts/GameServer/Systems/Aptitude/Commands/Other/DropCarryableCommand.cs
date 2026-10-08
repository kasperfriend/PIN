using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     <c>aptgss::DropCarryableCommandDef</c> (id only): the character lets go of the carryable it is
///     holding. It sits at the tail of throw-the-object chains - ability 39590 runs
///     <c>FireProjectile -> ImpactApplyEffect -> ConsumeSuperCharge -> DropCarryable</c>, i.e. throw
///     what you are carrying, then stop carrying it.
///     <para>
///         The carryable system on this server is the three replicated inventory slots on the
///         character's base controller and observer view; nothing picks a carryable up yet, and a
///         character holds at most one, so dropping "the" carryable and dropping every carryable are
///         the same state transition. That makes this <c>DropAllCarryableCommand</c>'s body with the
///         singular reading documented, not a second copy of it by accident: when observed-carryable
///         pickup lands, this is the command that has to select the specific object instead of
///         clearing the slots.
///     </para>
/// </summary>
public class DropCarryableCommand : Command, ICommand
{
    private DropCarryableCommandDef Params;

    public DropCarryableCommand(DropCarryableCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character)
        {
            Logger.Debug(
                "{Command} {CommandId}: self is not a Character (self={SelfType})",
                nameof(DropCarryableCommand), Params.Id, context.Self?.GetType().Name ?? "null");
            return true;
        }

        var baseController = character.Character_BaseController;
        var observerView = character.Character_ObserverView;
        if (baseController == null || observerView == null)
        {
            // BaseController only exists once the character has been made observable.
            return true;
        }

        bool hadAny = baseController.CarryableObjects_0Prop != null
                   || baseController.CarryableObjects_1Prop != null
                   || baseController.CarryableObjects_2Prop != null;

        if (hadAny)
        {
            Logger.Information(
                "{Command} {CommandId}: {CharacterId} dropped its carryable",
                nameof(DropCarryableCommand), Params.Id, character.EntityId);
        }

        baseController.CarryableObjects_0Prop = null;
        baseController.CarryableObjects_1Prop = null;
        baseController.CarryableObjects_2Prop = null;

        observerView.CarryableObjects_0Prop = null;
        observerView.CarryableObjects_1Prop = null;
        observerView.CarryableObjects_2Prop = null;

        return true;
    }
}
