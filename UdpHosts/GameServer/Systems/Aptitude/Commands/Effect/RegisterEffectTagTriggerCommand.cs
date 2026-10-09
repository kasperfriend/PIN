using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Aptitude.Commands.Other;

namespace GameServer.Systems.Aptitude.Commands.Effect;

/// <summary>
///     <c>customdata::RegisterEffectTagTriggerCommandDef</c>: installs an ability trigger meant to be
///     discriminated by effect tag. As with the hit-tag variant the def is id-only, so no tag is
///     readable; it is installed as a plain ability trigger and fires with the entity's other triggers.
/// </summary>
public class RegisterEffectTagTriggerCommand : AbilityTriggerCommandBase
{
    public RegisterEffectTagTriggerCommand(RegisterEffectTagTriggerCommandDef par)
        : base(par)
    {
    }

    protected override bool Timed => false;
}
