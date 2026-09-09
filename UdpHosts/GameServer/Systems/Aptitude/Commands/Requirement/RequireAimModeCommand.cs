using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>aptfs::RequireAimModeCommandDef</c> (command type 130): true while the character of the activation
///     is aiming down sights. The scope is read from <c>FireMode_1</c>: both replicated fire mode fields carry
///     the scoped state while the sights are up, but <c>FireMode_0</c> is also the field the player's own
///     fire mode selection arrives on (<c>SelectFireMode</c>), so only <c>FireMode_1</c> answers "scoped"
///     without ambiguity (mode 1 = scoped, 0 = hip fire).
/// </summary>
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
        // Chains owned by a non character entity (a turret or a deployable running a scoped effect for the
        // player who activated it) reach this command with the deployable as Self, so resolve the character
        // of the activation like the other character requirements do.
        var character = CharacterRequirement.Find(context, false);

        if (character == null)
        {
            // No character in this activation: the requirement cannot be answered, so it cannot be violated.
            CharacterRequirement.LogNotApplicable(Logger, nameof(RequireAimModeCommand), Params.Id, context);

            return true;
        }

        bool isScoped = character.FireMode_1.Mode != 0;

        return Params.Negate == 1 ? !isScoped : isScoped;
    }
}
