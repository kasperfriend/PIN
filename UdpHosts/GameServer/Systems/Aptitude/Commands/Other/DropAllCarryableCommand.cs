using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     <c>aptgss::DropAllCarryableCommandDef</c> (id only): the character drops every carryable
///     object it holds. The carryable system on this server is limited to the three replicated
///     inventory slots on the character's base controller and observer view; nothing picks a
///     carryable up yet, so for any character today this is a state refresh. When one is held,
///     the slots clear (the entity itself is left to its own lifecycle, the same way a respawn
///     leaves a spawned carryable where it was).
/// </summary>
public class DropAllCarryableCommand : Command, ICommand
{
    private DropAllCarryableCommandDef Params;

    public DropAllCarryableCommand(DropAllCarryableCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character)
        {
            Logger.Warning("{Command} {CommandId}: self is not a Character (self={SelfType})", nameof(DropAllCarryableCommand), Params.Id, context.Self?.GetType().Name ?? "null");
            return false;
        }

        var baseController = character.Character_BaseController;
        var observerView = character.Character_ObserverView;
        if (baseController == null || observerView == null)
        {
            // BaseController only exists once the character has been made observable.
            return false;
        }

        bool hadAny = baseController.CarryableObjects_0Prop != null
                   || baseController.CarryableObjects_1Prop != null
                   || baseController.CarryableObjects_2Prop != null;

        if (hadAny)
        {
            Logger.Information("{Command} {CommandId}: dropping carryables of {CharacterId}", nameof(DropAllCarryableCommand), Params.Id, character.EntityId);
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
