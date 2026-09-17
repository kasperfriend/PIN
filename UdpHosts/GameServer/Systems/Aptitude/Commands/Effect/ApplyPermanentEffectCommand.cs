using System.Linq;
using GameServer.Data;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Effect;

/// <summary>
///     Applies a boost the character keeps across sessions: +N% XP, crystite or reputation for the
///     row's duration, or permanently. The boost lands in <see cref="CharacterUnlocks" /> and in the
///     character's <c>XpBoostModifier</c>/<c>ResourceBoostModifier</c>/<c>ReputationBoostModifier</c>
///     props, which the client shows in the character sheet; the chain's own <c>ImpactApplyEffect</c>
///     plays the pickup visuals. Boost rows without a type (the gift items, the enhancers) are logged
///     and skipped rather than failing the chain.
/// </summary>
public class ApplyPermanentEffectCommand : Command, ICommand
{
    private ApplyPermanentEffectCommandDef Params;

    public ApplyPermanentEffectCommand(ApplyPermanentEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        string type = Params.BoostType;
        uint percent = Params.Percent;
        if (string.IsNullOrEmpty(type) && Params.ExperienceBoost != 0)
        {
            type = CharacterUnlocks.BoostXp;
            percent = Params.ExperienceBoost;
        }

        if (string.IsNullOrEmpty(type) || percent == 0)
        {
            Logger.Warning("{Command} {CommandId} has no boost authored yet (item {Item}); nothing applied", nameof(ApplyPermanentEffectCommand), Params.Id, context.AbilityModuleId);
            return true;
        }

        var character = PlayerRewards.OwnerOf(context);
        if (character == null)
        {
            return false;
        }

        if (!PlayerRewards.ConsumeActivatingItem(context, character))
        {
            return false;
        }

        ulong now = PlayerRewards.UnixNow();
        var before = character.Unlocks.Boosts.ToList();
        var boost = character.Unlocks.AddBoost(type, percent, Params.DurationSeconds, Params.Permanent == 1, Params.EffectId, now);
        if (boost == null)
        {
            return true;
        }

        context.ActivationRollbacks.Add(() =>
        {
            character.Unlocks.RemoveBoosts(type);
            foreach (var old in before.Where(b => b.Type == type))
            {
                character.Unlocks.AddBoost(old.Type, old.Percent, old.ExpiresAt == 0 ? 0 : (uint)(old.ExpiresAt > now ? old.ExpiresAt - now : 0), old.ExpiresAt == 0, old.EffectId, now);
            }

            PlayerRewards.SyncBoosts(character, now);
        });

        context.DirtyUnlocks.Add(character);
        PlayerRewards.SyncBoosts(character, now);
        Logger.Information(
            "{Character} received a {Percent}% {Type} boost {Until}",
            character,
            percent,
            type,
            boost.ExpiresAt == 0 ? "permanently" : $"until unix {boost.ExpiresAt}");
        return true;
    }
}
