using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Effect;

/// <summary>Removes boosts: by type, by their visual effect, or - a row naming neither, the Polymorph Cleanse - all of them.</summary>
public class RemovePermanentEffectCommand : Command, ICommand
{
    private RemovePermanentEffectCommandDef Params;

    public RemovePermanentEffectCommand(RemovePermanentEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var character = PlayerRewards.OwnerOf(context);
        if (character == null)
        {
            return true;
        }

        var removed = character.Unlocks.RemoveBoosts(Params.BoostType, Params.EffectId);
        if (removed.Count == 0)
        {
            return true;
        }

        ulong now = PlayerRewards.UnixNow();
        context.ActivationRollbacks.Add(() =>
        {
            foreach (var boost in removed)
            {
                character.Unlocks.AddBoost(boost.Type, boost.Percent, boost.ExpiresAt == 0 ? 0 : (uint)(boost.ExpiresAt > now ? boost.ExpiresAt - now : 0), boost.ExpiresAt == 0, boost.EffectId, now);
            }

            PlayerRewards.SyncBoosts(character, now);
        });

        context.DirtyUnlocks.Add(character);
        PlayerRewards.SyncBoosts(character, now);
        Logger.Information("{Character} lost {Count} boost(s)", character, removed.Count);
        return true;
    }
}
