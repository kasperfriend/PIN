using GameServer.Entities.Character;
using GameServer.Enums;

namespace GameServer.Systems.Aptitude;

/// <summary>
///     Resolves an <c>apt</c> stat id to the value a chain reads for a character. The vitals - Health,
///     MaxHealth, Shields, MaxShields, Energy, MaxEnergy - are the live pools: the data multiplies them
///     into heal and damage amounts (Health Pack: <c>LoadRegisterFromStat(MaxHealth) x 0.2 -&gt; HealDamage</c>,
///     Brontodon stomp: <c>LoadRegisterFromStat(MaxHealth) x 0.005 -&gt; InflictDamage</c>) and compares them in
///     <c>StatRequirement</c>, which only makes sense on the pools themselves. Every other stat is a
///     modifier the character's stat table holds (run speed, fire rate, ...; 1.0 unmodified).
/// </summary>
public static class AptitudeStatReader
{
    public static float Read(CharacterEntity character, AptitudeStat stat)
    {
        switch (stat)
        {
            case AptitudeStat.Health:
                return character.CurrentHealth;
            case AptitudeStat.MaxHealth:
                return character.MaxHealth.Value;
            case AptitudeStat.Shields:
                return character.CurrentShields;
            case AptitudeStat.MaxShields:
                return character.MaxShields.Value;
            case AptitudeStat.Energy:
                return EnergyState(character)?.Energy ?? 0f;
            case AptitudeStat.MaxEnergy:
                return EnergyState(character)?.MaxEnergy ?? 0f;
            default:
                return character.GetCurrentStatModifierValue((StatModifierIdentifier)stat);
        }
    }

    /// <summary>Whether the stat is one of the live pools rather than a stat-table modifier.</summary>
    public static bool IsVital(AptitudeStat stat) =>
        stat is AptitudeStat.Health or AptitudeStat.MaxHealth or AptitudeStat.Shields or AptitudeStat.MaxShields
            or AptitudeStat.Energy or AptitudeStat.MaxEnergy;

    private static AbilityState EnergyState(CharacterEntity character)
    {
        var abilities = character.Shard?.Abilities;
        if (abilities == null)
        {
            return null;
        }

        var state = abilities.GetOrAddState(character);
        state.UpdateEnergy(character.Shard.CurrentTime);
        return state;
    }
}
