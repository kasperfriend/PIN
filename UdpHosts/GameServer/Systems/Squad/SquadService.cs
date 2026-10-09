using System.Collections.Generic;
using GameServer.Entities.Character;

namespace GameServer.Systems.Squad;

/// <summary>One squad: an ordered list of member characters and the one of them who leads it.</summary>
public class Squad
{
    /// <summary>Firefall squads hold five; the server had no squad model at all before this service,
    /// so the cap is a faithful default rather than a decoded one.</summary>
    public const int MaxMembers = 5;

    public uint Id;
    public List<ulong> Members = [];
    public ulong LeaderEntityId;
}

/// <summary>
///     The shard's squad roster.
///     <para>
///         Nothing in this codebase tracked squads: <c>aptfs::TargetSquadmatesCommandDef</c> (24 rows)
///         and <c>aptfs::RequireSquadLeaderCommandDef</c> (4) had no membership to read, and squad chat
///         answered "This channel is not available". The protocol's squad messages are undecoded —
///         <c>ChallengeInvitationSquadInfo</c> is <c>Unk1</c>..<c>Unk4</c> — so there is no
///         client-driven way to form one either. This service supplies the roster both commands need
///         and routes squad chat to it; membership is formed through the server's own admin commands
///         until the invitation protocol is decoded.
///     </para>
/// </summary>
public class SquadService
{
    private readonly IShard _shard;
    private readonly Dictionary<ulong, Squad> _byMember = [];
    private readonly Dictionary<uint, Squad> _byId = [];
    private readonly object _lock = new();
    private uint _nextId = 1;

    public SquadService(IShard shard)
    {
        _shard = shard;
    }

    /// <summary>The squad a character is in, or null when it is in none.</summary>
    public Squad? GetSquad(ulong characterEntityId)
    {
        lock (_lock)
        {
            return _byMember.GetValueOrDefault(characterEntityId);
        }
    }

    /// <summary>True when the character leads the squad it is in. A character in no squad leads none.</summary>
    public bool IsLeader(ulong characterEntityId)
    {
        lock (_lock)
        {
            return _byMember.TryGetValue(characterEntityId, out var squad) && squad.LeaderEntityId == characterEntityId;
        }
    }

    /// <summary>The other members of the character's squad, excluding the character itself.</summary>
    public List<CharacterEntity> GetSquadmates(ulong characterEntityId)
    {
        var result = new List<CharacterEntity>();

        lock (_lock)
        {
            if (!_byMember.TryGetValue(characterEntityId, out var squad))
            {
                return result;
            }

            foreach (var memberId in squad.Members)
            {
                if (memberId == characterEntityId)
                {
                    continue;
                }

                if (_shard.Entities.TryGetValue(memberId, out var entity) && entity is CharacterEntity character)
                {
                    result.Add(character);
                }
            }
        }

        return result;
    }

    /// <summary>Creates a squad led by <paramref name="leader" />, or returns the one it is already in.</summary>
    public Squad CreateSquad(ulong leader)
    {
        lock (_lock)
        {
            if (_byMember.TryGetValue(leader, out var existing))
            {
                return existing;
            }

            var squad = new Squad { Id = _nextId++, LeaderEntityId = leader };
            squad.Members.Add(leader);
            _byId[squad.Id] = squad;
            _byMember[leader] = squad;

            _shard.Logger.Information("[Squad] {Leader} formed squad {Squad}", leader, squad.Id);

            return squad;
        }
    }

    /// <summary>Adds a character to the squad the inviter leads. Refuses a full squad or a bad invite.</summary>
    public bool Invite(ulong inviter, ulong invitee)
    {
        lock (_lock)
        {
            if (inviter == invitee || !_byMember.TryGetValue(inviter, out var squad))
            {
                return false;
            }

            if (squad.LeaderEntityId != inviter)
            {
                return false;
            }

            if (_byMember.ContainsKey(invitee) || squad.Members.Count >= Squad.MaxMembers)
            {
                return false;
            }

            squad.Members.Add(invitee);
            _byMember[invitee] = squad;

            _shard.Logger.Information("[Squad] {Invitee} joined squad {Squad}", invitee, squad.Id);

            return true;
        }
    }

    /// <summary>Hands leadership to another member of the same squad.</summary>
    public bool Promote(ulong currentLeader, ulong newLeader)
    {
        lock (_lock)
        {
            if (!_byMember.TryGetValue(currentLeader, out var squad) || squad.LeaderEntityId != currentLeader)
            {
                return false;
            }

            if (!squad.Members.Contains(newLeader))
            {
                return false;
            }

            squad.LeaderEntityId = newLeader;

            _shard.Logger.Information("[Squad] squad {Squad} leadership passed to {Leader}", squad.Id, newLeader);

            return true;
        }
    }

    /// <summary>
    ///     Takes a character out of its squad, disbanding the squad when it was the last member and
    ///     handing leadership to the next member when it led. Called when a character leaves the shard.
    /// </summary>
    public void Leave(ulong characterEntityId)
    {
        lock (_lock)
        {
            if (!_byMember.TryGetValue(characterEntityId, out var squad))
            {
                return;
            }

            squad.Members.Remove(characterEntityId);
            _byMember.Remove(characterEntityId);

            if (squad.Members.Count == 0)
            {
                _byId.Remove(squad.Id);
                _shard.Logger.Information("[Squad] squad {Squad} disbanded", squad.Id);
                return;
            }

            if (squad.LeaderEntityId == characterEntityId)
            {
                squad.LeaderEntityId = squad.Members[0];
                _shard.Logger.Information("[Squad] squad {Squad} leadership fell to {Leader}", squad.Id, squad.LeaderEntityId);
            }
        }
    }
}
