using AeroMessages.GSS.Character;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Requirement;
using GameServer.Systems.Aptitude.Commands.Target;
using GameServer.Systems.Squad;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     <c>aptfs::TargetSquadmatesCommandDef</c> (24 rows) and
///     <c>aptfs::RequireSquadLeaderCommandDef</c> (4) had no squad membership to read until
///     <see cref="SquadService" /> existed. These cover the roster and both commands.
/// </summary>
public class SquadCommandTests
{
    private static (FakeShard Shard, CharacterEntity Leader, CharacterEntity Mate) BuildSquad()
    {
        var shard = new FakeShard();
        shard.Squad = new SquadService(shard);

        var leader = new CharacterEntity(shard, shard.GetNextGuid(0));
        var mate = new CharacterEntity(shard, shard.GetNextGuid(0));
        shard.Entities.Add(leader.EntityId, leader);
        shard.Entities.Add(mate.EntityId, mate);

        leader.SetCharacterState(CharacterStateData.CharacterStatus.Living, 0);
        mate.SetCharacterState(CharacterStateData.CharacterStatus.Living, 0);

        shard.Squad.CreateSquad(leader.EntityId);
        shard.Squad.Invite(leader.EntityId, mate.EntityId);

        return (shard, leader, mate);
    }

    [Fact]
    public void TargetSquadmatesReplacesTheListWithTheOtherMembers()
    {
        var (shard, leader, mate) = BuildSquad();

        var context = new Context(shard, leader);
        var command = new TargetSquadmatesCommand(new TargetSquadmatesCommandDef { Id = 1 });

        Assert.True(command.Execute(context));
        Assert.Equal(1, context.Targets.Count);
        Assert.True(context.Targets.TryPeek(out var target));
        Assert.Equal(mate.EntityId, target.EntityId); // the other member, not the caster
    }

    [Fact]
    public void TargetSquadmatesFailNoneFailsWhenThereIsNoSquad()
    {
        var shard = new FakeShard();
        shard.Squad = new SquadService(shard);
        var loner = new CharacterEntity(shard, shard.GetNextGuid(0));
        shard.Entities.Add(loner.EntityId, loner);

        // fail_none is 1 in 11 of the 24 rows and 0 in the other 13, so both halves are reachable.
        var context = new Context(shard, loner);
        var command = new TargetSquadmatesCommand(new TargetSquadmatesCommandDef { Id = 1, FailNone = 1 });

        Assert.False(command.Execute(context));
        Assert.Equal(0, context.Targets.Count);
    }

    [Fact]
    public void TargetSquadmatesWithoutFailNoneSucceedsOnAnEmptyList()
    {
        var shard = new FakeShard();
        shard.Squad = new SquadService(shard);
        var loner = new CharacterEntity(shard, shard.GetNextGuid(0));
        shard.Entities.Add(loner.EntityId, loner);

        var context = new Context(shard, loner);
        var command = new TargetSquadmatesCommand(new TargetSquadmatesCommandDef { Id = 1, FailNone = 0 });

        Assert.True(command.Execute(context));
    }

    [Fact]
    public void TargetSquadmatesFilterDropsSquadmatesThatAreNotAlive()
    {
        var (shard, leader, mate) = BuildSquad();

        mate.SetCharacterState(CharacterStateData.CharacterStatus.Dead, 0);

        var unfiltered = new Context(shard, leader);
        Assert.True(new TargetSquadmatesCommand(new TargetSquadmatesCommandDef { Id = 1 }).Execute(unfiltered));
        Assert.Equal(1, unfiltered.Targets.Count);

        var filtered = new Context(shard, leader);
        var command = new TargetSquadmatesCommand(new TargetSquadmatesCommandDef { Id = 1, Filter = 1 });

        Assert.False(command.Execute(filtered)); // nothing survives the filter, so fail_none governs
        Assert.Equal(0, filtered.Targets.Count);
    }

    [Fact]
    public void RequireSquadLeaderPassesForTheLeaderAndFailsForAMember()
    {
        var (shard, leader, mate) = BuildSquad();

        var gate = new RequireSquadLeaderCommand(new RequireSquadLeaderCommandDef { Id = 1 });

        Assert.True(gate.Execute(new Context(shard, leader)));
        Assert.False(gate.Execute(new Context(shard, mate)));
    }

    [Fact]
    public void RequireSquadLeaderNegateInverts()
    {
        var (shard, leader, mate) = BuildSquad();

        var gate = new RequireSquadLeaderCommand(new RequireSquadLeaderCommandDef { Id = 1, Negate = 1 });

        Assert.False(gate.Execute(new Context(shard, leader)));
        Assert.True(gate.Execute(new Context(shard, mate)));
    }

    [Fact]
    public void RequireSquadLeaderFailsForACharacterInNoSquad()
    {
        var shard = new FakeShard();
        shard.Squad = new SquadService(shard);
        var loner = new CharacterEntity(shard, shard.GetNextGuid(0));
        shard.Entities.Add(loner.EntityId, loner);

        var gate = new RequireSquadLeaderCommand(new RequireSquadLeaderCommandDef { Id = 1 });

        Assert.False(gate.Execute(new Context(shard, loner)));
    }

    [Fact]
    public void InviteRefusesANonLeaderAFullSquadAndAnExistingMember()
    {
        var shard = new FakeShard();
        shard.Squad = new SquadService(shard);

        var members = new CharacterEntity[Squad.MaxMembers + 1];
        for (int i = 0; i < members.Length; i++)
        {
            members[i] = new CharacterEntity(shard, shard.GetNextGuid(0));
            shard.Entities.Add(members[i].EntityId, members[i]);
        }

        var squad = shard.Squad.CreateSquad(members[0].EntityId);
        Assert.Equal(members[0].EntityId, squad.LeaderEntityId);

        for (int i = 1; i < Squad.MaxMembers; i++)
        {
            Assert.True(shard.Squad.Invite(members[0].EntityId, members[i].EntityId));
        }

        Assert.Equal(Squad.MaxMembers, squad.Members.Count);
        Assert.False(shard.Squad.Invite(members[0].EntityId, members[Squad.MaxMembers].EntityId)); // full
        Assert.False(shard.Squad.Invite(members[1].EntityId, members[Squad.MaxMembers].EntityId)); // not the leader
        Assert.False(shard.Squad.Invite(members[0].EntityId, members[1].EntityId)); // already in a squad
    }

    [Fact]
    public void LeavingHandsLeadershipToTheNextMemberAndDisbandsTheLastOne()
    {
        var (shard, leader, mate) = BuildSquad();

        shard.Squad.Leave(leader.EntityId);

        Assert.True(shard.Squad.IsLeader(mate.EntityId));
        Assert.Null(shard.Squad.GetSquad(leader.EntityId));

        shard.Squad.Leave(mate.EntityId);

        Assert.Null(shard.Squad.GetSquad(mate.EntityId));
        Assert.False(shard.Squad.IsLeader(mate.EntityId));
    }
}
