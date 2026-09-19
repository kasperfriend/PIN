using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>aptfs::RequireEliteLevelCommandDef</c>: the character's elite level must be at least
///     the row's <c>Level</c> (the same comparison direction <c>RequireLevelCommand</c> uses for
///     frame level). The replicated answer lives on the base controller's EliteLevelProp (the
///     equipment view carries a second copy that stays 0). Note that no elite-XP ledger exists
///     on this server yet, so the prop is the static init value (1): rows asking for Level above
///     that fail, which is the fail-closed reading while the system that would raise it is absent.
/// </summary>
public class RequireEliteLevelCommand : Command, ICommand
{
    private RequireEliteLevelCommandDef Params;

    public RequireEliteLevelCommand(RequireEliteLevelCommandDef par)
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

        // The base controller only exists once the character has been made observable
        // (InitControllers); a character nobody has seen yet has no elite level anyone can
        // verify, so the gate fails instead of guessing.
        return (character.Character_BaseController?.EliteLevelProp ?? 0) >= Params.Level;
    }
}
