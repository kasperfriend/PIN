using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Register;

public class LoadRegisterFromResourceCommand : Command, ICommand
{
    private LoadRegisterFromResourceCommandDef Params;

    public LoadRegisterFromResourceCommand(LoadRegisterFromResourceCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // The register gets the player's current quantity of the row's resource,
        // combined with the previous register through the row's regop. The
        // RegisterVal_0..10 columns are not decoded: they carry no values in
        // the rows that run server side, and with only ResourceId + Regop the
        // quantity load is unambiguous.
        if (context.Self is not CharacterEntity character)
        {
            return false;
        }

        context.Register = AbilitySystem.RegistryOp(
            context.Register,
            character.Player.Inventory.GetResourceQuantity(Params.ResourceId),
            (Operand)Params.Regop);

        return true;
    }
}