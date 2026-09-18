using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>aptfs::RequireItemAttributeCommandDef</c>: gates on the character's gear carrying the
///     row's item attribute at all. The loadout already computes the merged attribute totals of
///     every slotted item (weapon attributes plus their built-in modules, backpack modules, gear),
///     exactly the pool the item-stat chain commands balance against, so the gate reads it: any
///     non-zero contribution of the attribute satisfies the requirement.
/// </summary>
public class RequireItemAttributeCommand : Command, ICommand
{
    private RequireItemAttributeCommandDef Params;

    public RequireItemAttributeCommand(RequireItemAttributeCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        bool result = false;

        var character = CharacterRequirement.Find(context, false);
        if (character?.CurrentLoadout != null
            && Params.AttributeId <= ushort.MaxValue)
        {
            result = character.CurrentLoadout.ItemAttributes.TryGetValue((ushort)Params.AttributeId, out var value)
                && value != 0f;
        }

        return result;
    }
}
