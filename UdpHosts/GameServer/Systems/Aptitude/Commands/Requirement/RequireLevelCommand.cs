using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

public class RequireLevelCommand : Command, ICommand
{
    private RequireLevelCommandDef Params;

    public RequireLevelCommand(RequireLevelCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // See CharacterRequirement: levels belong to characters, and the character of a deployable owned
        // chain is the player that triggered it.
        var character = CharacterRequirement.Find(context, false);

        if (character == null)
        {
            // CharacterRequirement.LogNotApplicable(Logger, nameof(RequireLevelCommand), Params.Id, context);

            return true;
        }

        bool result = false;
        {
            if (Params.FrameLevel == 1)
            {
                result = character.Character_BaseController.LevelProp >= Params.Level;
            }
            else if (Params.SessionLevel == 1)
            {
                // Session level = the level the current session scales the character to,
                // which the server models as EffectiveLevelProp (today it always tracks
                // the frame level, but staged content is free to lower it). These rows
                // used to pass unconditionally, which let level-gated encounters treat
                // a downtiered character as if it were at full frame level.
                result = character.Character_BaseController.EffectiveLevelProp >= Params.Level;
            }
        }

        return result;
    }
}