using AeroMessages.GSS.Character.Controller;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

public class RequireCAISStateCommand : Command, ICommand
{
    private RequireCAISStateCommandDef Params;

    public RequireCAISStateCommand(RequireCAISStateCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        bool result = false;

        var target = context.Self;

        if (target is not CharacterEntity character)
        {
            Logger.Warning("{Command} {CommandId} fails because target is not a Character. If this is happening, we should investigate why.", nameof(RequireCAISStateCommand), Params.Id);
            return false;
        }

        var state = character.Character_BaseController.CAISStatusProp.State;

        // No CAIS driver exists on this server yet, so every character sits at None. None is the
        // healthy baseline: a row asking for Healthy must still pass for it (emote and ambience
        // chains gate on Healthy for characters that are fine), while rows asking for Fatigued or
        // Unhealthy correctly fail until something starts driving those states.
        if (Params.None == 1)
        {
            result = state == CAISStatusData.CAISState.None;
        }

        if (Params.Fatigued == 1)
        {
            result = result || state == CAISStatusData.CAISState.Fatigued;
        }

        if (Params.Unhealthy == 1)
        {
            result = result || state == CAISStatusData.CAISState.Unhealthy;
        }

        if (Params.Healthy == 1)
        {
            result = result || state is CAISStatusData.CAISState.Healthy or CAISStatusData.CAISState.None;
        }

        return result;
    }
}