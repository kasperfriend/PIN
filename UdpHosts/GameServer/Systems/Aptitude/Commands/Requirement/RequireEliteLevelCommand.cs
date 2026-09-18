using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>aptfs::RequireEliteLevelCommandDef</c>: the character's elite level must be at least
///     the row's <c>Level</c> (the same comparison direction <c>RequireLevelCommand</c> uses for
///     frame level). The replicated answer lives on the observer view's EliteLevelProp. Note that
///     no elite-XP ledger exists on this server yet, so the prop is the static spawn value (1):
///     rows asking for Level above that fail, which is the fail-closed reading while the system
///     that would raise it is absent.
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

        // Character_ObserverView is created by InitViews in the constructor, so it is always set.
        return character.Character_ObserverView.EliteLevelProp >= Params.Level;
    }
}
