using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.Enums;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Register;

/// <summary>
/// Loads one of the character's aptitude stats into the register, combined with the
/// current register through the record's Regop. For a stat modifier (fire rate,
/// cooldown modifier, ...) that is the effective multiplier, 1.0 unmodified, which is
/// how chain amounts scale with gear and active effects. For a vital (Health,
/// MaxHealth, Shields, Energy and their maxima - 343 of the 359 rows read MaxHealth
/// or Health) it is the live pool: a Health Pack is <c>LoadRegisterFromStat(MaxHealth)
/// x 0.2 -&gt; HealDamage</c>, and reading the 1.0 modifier there healed a single point.
/// See <see cref="AptitudeStatReader" />.
/// </summary>
public class LoadRegisterFromStatCommand : Command, ICommand
{
    private LoadRegisterFromStatCommandDef Params;

    public LoadRegisterFromStatCommand(LoadRegisterFromStatCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var character = context.Self as CharacterEntity ?? (context.Self as DeployableEntity)?.Owner;
        if (character == null)
        {
            Logger.Warning(
                "{Command} {CommandId} does nothing because Self is not a Character; Self is {SelfType}",
                nameof(LoadRegisterFromStatCommand),
                Params.Id,
                context.Self?.GetType().Name ?? "null");
            return true;
        }

        var stat = (AptitudeStat)Params.Stat;
        float statValue = AptitudeStatReader.Read(character, stat);
        context.Register = AbilitySystem.RegistryOp(context.Register, statValue, (Operand)Params.Regop);
        Logger.Debug(
            "{Command} {CommandId}: stat {Stat} = {StatValue}, regop {Regop} => register {Register}",
            nameof(LoadRegisterFromStatCommand),
            Params.Id,
            Params.Stat,
            statValue,
            (Operand)Params.Regop,
            context.Register);
        return true;
    }
}
