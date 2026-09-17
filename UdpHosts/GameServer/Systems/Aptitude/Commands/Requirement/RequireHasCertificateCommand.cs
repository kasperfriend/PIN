using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     Whether every targeted player holds (or, negated, lacks) a certificate. The Recipe/Perk unlock
///     items guard themselves with the negated form; the Campaign Token chains count the certificates
///     already held with the plain form.
/// </summary>
public class RequireHasCertificateCommand : Command, ICommand
{
    private RequireHasCertificateCommandDef Params;

    public RequireHasCertificateCommand(RequireHasCertificateCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        bool result = false;
        var targets = context.Targets is { Count: > 0 } ? context.Targets : null;

        if (targets != null)
        {
            result = true;
            foreach (var target in targets)
            {
                if (target is not CharacterEntity character || !character.Unlocks.HasCertificate(Params.CertificateId))
                {
                    result = false;
                    break;
                }
            }
        }
        else if (context.Self is CharacterEntity self)
        {
            result = self.Unlocks.HasCertificate(Params.CertificateId);
        }

        return Params.Negate == 1 ? !result : result;
    }
}
