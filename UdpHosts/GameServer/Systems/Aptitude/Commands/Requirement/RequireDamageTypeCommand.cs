using GameServer.Entities.Character;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>apt::RequireDamageTypeCommandDef</c>: gates on the damage type of the character's most
///     recent damage interaction. The stamps <c>DamageSystem</c> keeps per character cover both
///     sides (dealt and taken); the gate passes when EITHER side's last event was the row's type -
///     "was type X involved in my latest combat" reads closest to how such rows are used
///     (element-specific pros and cons), and it cannot tell the two directions apart when only one
///     kind ever happened to the character.
/// </summary>
public class RequireDamageTypeCommand : Command, ICommand
{
    private RequireDamageTypeCommandDef Params;

    public RequireDamageTypeCommand(RequireDamageTypeCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var character = CharacterRequirement.Find(context, false);
        if (character == null)
        {
            return true;
        }

        bool result = (character.LastDamageTakenTime != 0 && character.LastDamageTakenType == Params.Damagetype)
                   || (character.LastDamageDealtTime != 0 && character.LastDamageDealtType == Params.Damagetype);

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }
}
