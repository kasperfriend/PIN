using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

public class RequireAimModeCommand : Command, ICommand
{
    private RequireAimModeCommandDef Params;

    public RequireAimModeCommand(RequireAimModeCommandDef par)
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

        // FireMode_1.Mode == 1 means scoped in (ADS), 0 means hip fire
        bool isScoped = character.FireMode_1.Mode != 0;
        
        bool result = isScoped;

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }
}
