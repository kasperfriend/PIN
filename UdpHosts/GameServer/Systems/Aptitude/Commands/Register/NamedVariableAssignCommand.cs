using GameServer.Enums;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Register;

/// <summary>
///     <c>apt::NamedVariableAssignCommandDef</c> (type 239, 291 rows): writes a named scripting
///     variable on the entity the chain is running on, combined with whatever it already holds through
///     the row's <c>regop</c> (<c>ASSIGN</c> in 146 rows, <c>ADD</c> in 110, <c>MULTIPLY</c> in 35).
///     It was a placeholder, which is why its reader <c>LoadRegisterFromNamedVarCommand</c> (type 240)
///     could only ever take its <c>undecl_value</c> fallback: nothing ever declared a variable, so a
///     chain that set one and then branched on it always took the unset branch.
///     <para>
///         The variable is stored per entity (<see cref="AbilitySystem.SetNamedVariable" />) under the
///         row's <c>name_id</c>, with <c>member_name</c> folded into the key when present. The id is the
///         only key both tables always carry - the string is empty in 119 of the 291 assign rows and
///         156 of the 265 read rows - and the two share one vocabulary ("WingFX", "damage",
///         "FuseLength", "teslacount", "heat"), which is what makes them a matched pair.
///     </para>
///     <para>
///         An assignment to a variable that does not exist yet stores the row's <c>Value</c> whatever
///         the regop says, by handing <c>RegistryOp</c> a NaN "previous": you cannot add to or multiply
///         something that was never declared, and treating the first write as an add would make the
///         result depend on how many times the chain had run.
///     </para>
///     <para>
///         <c>VarSrctype</c> is recorded but not modelled: the codebase has no enum for it (1 in 279 of
///         the 291 assign rows, 0 in 12, and the reader table agrees with the same skew), and the store
///         deliberately keys on the entity executing the command on both sides, which is what makes the
///         assign/read pair round-trip. Modelling the column would only matter for sharing a variable
///         <em>across</em> entities, and guessing which entity it means would break the pairs that work.
///     </para>
/// </summary>
public class NamedVariableAssignCommand : Command, ICommand
{
    private NamedVariableAssignCommandDef Params;

    public NamedVariableAssignCommand(NamedVariableAssignCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self == null)
        {
            Logger.Debug("NamedVariableAssign {CommandId}: no self to hold the variable", Params.Id);
            return true;
        }

        var key = AbilitySystem.NamedVariableKey(Params.NameId, Params.MemberName);

        // NaN as the previous value makes RegistryOp return Params.Value for every regop, which is the
        // only sane reading of a first write.
        float previous = context.Abilities.TryGetNamedVariable(context.Self, key, out var existing)
            ? existing
            : float.NaN;

        float value = AbilitySystem.RegistryOp(previous, Params.Value, (Operand)Params.Regop);
        context.Abilities.SetNamedVariable(context.Self, key, value);

        Logger.Debug(
            "NamedVariableAssign {CommandId}: {Name} on {Entity} = {Value} (was {Previous}, regop {Regop})",
            Params.Id, key, context.Self.EntityId, value,
            float.IsNaN(previous) ? "(undeclared)" : previous.ToString(), Params.Regop);

        return true;
    }
}
