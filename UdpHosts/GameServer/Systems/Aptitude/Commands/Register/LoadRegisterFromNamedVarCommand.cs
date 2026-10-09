using GameServer.Enums;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Register;

/// <summary>
///     <c>apt::LoadRegisterFromNamedVarCommandDef</c> (type 240): loads the value of a named variable into the
///     aptitude register, combined with the current register through the row's <c>regop</c>.
///
///     GameServer keeps the named variables in a per-entity store on the ability system, written by
///     <c>NamedVariableAssign</c> (type 239); a row whose variable nothing has assigned takes the row's
///     <c>undecl_value</c> fallback. That is what the shared glider pad launch ability needs (chain
///     1001671, row 1001663, variable "WingFX", fallback 1.0): the register comparisons in the launch effects
///     that follow select the pad's effect level through it, and no chain in the pad's activation assigns
///     "WingFX". The command used to be a placeholder that left the register untouched, so an unset register
///     and a real level were indistinguishable — the chains always took their fallback branch and a row that
///     meant to select its level could never select it.
/// </summary>
public class LoadRegisterFromNamedVarCommand : Command, ICommand
{
    private LoadRegisterFromNamedVarCommandDef Params;

    public LoadRegisterFromNamedVarCommand(LoadRegisterFromNamedVarCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // The variable is whatever a NamedVariableAssignCommand stored on this entity under the same
        // key; the row's undecl_value is the fallback for a variable nothing has assigned yet. Both
        // sides key on name_id (member_name is empty in 156 of the 265 rows here and 119 of the 291
        // there), so the pair round-trips.
        //
        // The shared glider pad launch ability still takes the fallback and must keep doing so: chain
        // 1001671 row 1001663 reads "WingFX" with fallback 1.0, and no chain in the pad's activation
        // assigns it, so the register comparisons in the launch effects that follow still select the
        // pad's effect level through the fallback. What changed is that a chain which *does* assign the
        // variable now reads its own value instead of being indistinguishable from one that did not.
        string key = AbilitySystem.NamedVariableKey(Params.NameId, Params.MemberName);
        float value = Params.UndeclValue;

        // No ability system means no store to read from - a shard built for tests runs these chains
        // without one - and the fallback is the value the row asks for in that case.
        if (context.Abilities != null && context.Abilities.TryGetNamedVariable(context.Self, key, out var declared))
        {
            value = declared;
        }

        context.Register = AbilitySystem.RegistryOp(context.Register, value, (Operand)Params.Regop);

        return true;
    }
}
