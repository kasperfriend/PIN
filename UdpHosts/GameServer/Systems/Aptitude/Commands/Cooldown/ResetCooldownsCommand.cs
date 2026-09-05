using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Cooldown;

public class ResetCooldownsCommand : Command, ICommand
{
    private ResetCooldownsCommandDef Params;

    public ResetCooldownsCommand(ResetCooldownsCommandDef par)
    : base(par)
    {
        Params = par;
    }

    // CommandType 209 ("Cooldown - Reset Abilities") carries no parameters:
    // every cooldown of the affected entity - local, category and global - is
    // cleared. The def has no target flag either, so the command applies to
    // the current target list and falls back to the entity the chain is
    // running for when the chain selected no targets.
    //
    // The client refreshes its ability timers from the AbilityCooldownsData
    // payload of the next ability-activation response, which is built after
    // the chain ran, so a resetting ability reports the cleared timers itself.
    public bool Execute(Context context)
    {
        var affected = context.Targets.ToArray();
        if (affected.Length == 0)
        {
            affected = [context.Self];
        }

        foreach (IAptitudeTarget entity in affected)
        {
            var state = context.Abilities.GetOrAddState(entity);
            int reset = state.ResetCooldowns();
            if (reset != 0)
            {
                Logger.Information(
                    "{Command} {CommandId} reset {Count} cooldowns for {Entity}",
                    nameof(ResetCooldownsCommand),
                    Params.Id,
                    reset,
                    entity);
            }
            else
            {
                Logger.Debug(
                    "{Command} {CommandId} found no cooldowns to reset for {Entity}",
                    nameof(ResetCooldownsCommand),
                    Params.Id,
                    entity);
            }
        }

        return true;
    }
}
