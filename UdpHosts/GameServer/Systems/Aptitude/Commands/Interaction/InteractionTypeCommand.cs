using GameServer.Entities;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Interaction;

public class InteractionTypeCommand : Command, ICommand
{
    private InteractionTypeCommandDef Params;

    public InteractionTypeCommand(InteractionTypeCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Targets.Count > 0)
        {
            var interactionEntity = context.Targets.Peek();
            var hack = interactionEntity as BaseEntity;

            // Entities without an interaction component (players, plain mobs) can still ride the
            // interact ability's targeting when a client looks their way; they simply match no type.
            if (hack?.Interaction == null)
            {
                return false;
            }

            var type = hack.Interaction.Type;

            Logger.Debug("{Command} {CommandId} Compared {type} with {ParamsType}", nameof(InteractionTypeCommand), Params.Id, type, Params.Type);
            return (byte)type == Params.Type;
        }
        else
        {
            return false;
        }
    }
}