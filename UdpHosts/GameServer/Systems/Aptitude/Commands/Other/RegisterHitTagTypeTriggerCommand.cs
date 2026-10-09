using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     <c>customdata::RegisterHitTagTypeTriggerCommandDef</c>: installs an ability trigger meant to be
///     discriminated by hit-tag type. The def is id-only, so no tag can be read from the row; it is
///     installed as a plain ability trigger and the command's own id is recorded as the tag, which is
///     all the available data supports. What that means in practice is that it fires with the other
///     triggers on the entity rather than only for a particular hit tag — documented rather than
///     guessed at, since inventing a tag value would make it fire for the wrong hits.
/// </summary>
public class RegisterHitTagTypeTriggerCommand : AbilityTriggerCommandBase
{
    public RegisterHitTagTypeTriggerCommand(RegisterHitTagTypeTriggerCommandDef par)
        : base(par)
    {
    }

    protected override bool Timed => false;
}
