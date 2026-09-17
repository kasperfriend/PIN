using System;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     Compares one of the character's vitals against a threshold: <c>stat1 (op) value</c>, where the value is
///     either absolute (<c>stat2 = 0</c>) or a percentage of a second stat (<c>value % of stat2</c>). The
///     flags <c>lessthan</c>/<c>greaterthan</c>/<c>equalto</c> OR together into the comparison, so
///     <c>lessthan + equalto</c> is "at most".
///     <para>
///         Of the 762 rows, 660 compare Health (6) against MaxHealth (7): every Health and Stim Pack gates
///         its heal on "health &lt; 100% of max" and its heal-over-time effect's duration chain on the same
///         test, so a full-health player cannot spend the pack and the tick stops at full. The remaining
///         rows read Shields (8/9), Energy (11/12), Progress (25) and SinVulnerability (19); see
///         <see cref="AptitudeStatReader" /> for where each value comes from. On a target with no vitals (a
///         deployable's owner is used when it has one) the requirement passes, so an NPC or object chain is
///         not broken by a check it cannot answer.
///     </para>
/// </summary>
public class StatRequirementCommand : Command, ICommand
{
    private StatRequirementCommandDef Params;

    public StatRequirementCommand(StatRequirementCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var character = context.Self as CharacterEntity ?? (context.Self as DeployableEntity)?.Owner;
        if (character == null)
        {
            Logger.Debug(
                "{Command} {CommandId} passes because Self is not a Character; Self is {SelfType}",
                nameof(StatRequirementCommand),
                Params.Id,
                context.Self?.GetType().Name ?? "null");
            return true;
        }

        float actual = AptitudeStatReader.Read(character, (AptitudeStat)Params.Stat1);
        float value = AbilitySystem.RegistryOp(context.Register, Params.Value, (Operand)Params.ValueRegop);
        float threshold = Params.Stat2 == 0
            ? value
            : AptitudeStatReader.Read(character, (AptitudeStat)Params.Stat2) * value / 100f;

        bool result = (Params.Lessthan == 1 && actual < threshold)
                      || (Params.Greaterthan == 1 && actual > threshold)
                      || (Params.Equalto == 1 && Math.Abs(actual - threshold) < 0.001f);

        if (!result)
        {
            Logger.Debug(
                "{Command} {CommandId} fails: {Self} stat {Stat1} = {Actual}, needs {Op} {Threshold} ({Value}{Unit})",
                nameof(StatRequirementCommand),
                Params.Id,
                character,
                (AptitudeStat)Params.Stat1,
                actual,
                Describe(),
                threshold,
                value,
                Params.Stat2 == 0 ? string.Empty : $"% of {(AptitudeStat)Params.Stat2}");
        }

        return result;
    }

    private string Describe()
    {
        string op = string.Empty;
        if (Params.Lessthan == 1)
        {
            op += "<";
        }

        if (Params.Greaterthan == 1)
        {
            op += ">";
        }

        if (Params.Equalto == 1)
        {
            op += "=";
        }

        return op.Length == 0 ? "?" : op;
    }
}
