using GameServer.Entities.Character;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     Forms and manages squads. The protocol's own squad path is undecoded
///     (<c>ChallengeInvitationSquadInfo</c> is <c>Unk1</c>..<c>Unk4</c>), so until it is, this is how a
///     squad comes into being — and it is what makes <c>TargetSquadmates</c>,
///     <c>RequireSquadLeader</c> and squad chat have a roster to work against.
///     <para>
///         Inviting and promoting work on the command's target, following the convention the other
///         admin commands use, rather than resolving a player by name.
///     </para>
/// </summary>
[ServerCommand("Form and manage your squad", "squad <create|invite|promote|leave|list>", "squad")]
public class SquadServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        var squad = context.Shard.Squad;

        if (squad == null)
        {
            SourceFeedback("This shard has no squad service", context);
            return;
        }

        if (context.SourcePlayer?.CharacterEntity is not CharacterEntity self)
        {
            SourceFeedback("You have no character to squad with", context);
            return;
        }

        var action = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "list";

        switch (action)
        {
            case "create":
                var created = squad.CreateSquad(self.EntityId);
                SourceFeedback(created.LeaderEntityId == self.EntityId
                    ? $"You lead squad {created.Id}. Target a player and run 'squad invite' to add them."
                    : $"You are already in squad {created.Id}.", context);
                break;

            case "invite":
                if (context.Target is not CharacterEntity invitee)
                {
                    SourceFeedback("Target a player to invite them", context);
                    return;
                }

                SourceFeedback(squad.Invite(self.EntityId, invitee.EntityId)
                    ? $"Invited {invitee.EntityId} to your squad"
                    : "Could not invite them: you must lead a squad, they must not already be in one, and the squad must have room", context);
                break;

            case "promote":
                if (context.Target is not CharacterEntity promoted)
                {
                    SourceFeedback("Target a squad member to hand leadership to them", context);
                    return;
                }

                SourceFeedback(squad.Promote(self.EntityId, promoted.EntityId)
                    ? $"{promoted.EntityId} now leads the squad"
                    : "Could not promote them: you must lead the squad and they must be in it", context);
                break;

            case "leave":
                var wasIn = squad.GetSquad(self.EntityId);
                if (wasIn == null)
                {
                    SourceFeedback("You are not in a squad", context);
                    return;
                }

                uint squadId = wasIn.Id;
                squad.Leave(self.EntityId);
                SourceFeedback($"Left squad {squadId}", context);
                break;

            case "list":
            {
                var mine = squad.GetSquad(self.EntityId);
                if (mine == null)
                {
                    SourceFeedback("You are not in a squad", context);
                    return;
                }

                var mates = squad.GetSquadmates(self.EntityId);
                SourceFeedback(
                    $"Squad {mine.Id}, leader {mine.LeaderEntityId}, {mine.Members.Count} member(s), {mates.Count} squadmate(s) besides you",
                    context);
                break;
            }

            default:
                SourceFeedback("Usage: squad <create|invite|promote|leave|list>", context);
                break;
        }
    }
}
