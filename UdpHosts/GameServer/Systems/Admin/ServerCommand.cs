using System.Numerics;
using GameServer.Entities;
using GameServer.Entities.Character;
using Serilog;

namespace GameServer.Systems.Admin;

public abstract class ServerCommand
{
    protected static readonly ILogger Logger = Log.ForContext<AdminService>();

    public abstract void Execute(string[] parameters, ServerCommandContext context);
    public virtual void SourceFeedback(string message, ServerCommandContext context)
    {
        Logger.Information("{Message}", message);
        context.SourcePlayer?.SendDebugChat(message);
    }

    public IEntity GetGunTarget(CharacterEntity character, ServerCommandContext context)
    {
        var direction = character.AimDirection;
        var origin = character.GetProjectileOrigin(direction);
        (bool hit, Vector3 loc, ulong entId) = context.Shard.Physics.TargetRayCast(origin, direction, character);
        if (hit)
        {
            return context.Shard.Entities.TryGetValue(entId, out var value) ? value : null;
        }

        return null;
    }

    public uint ParseUIntParameter(string value)
    {
        if (uint.TryParse(value, out uint result))
        {
            return result;
        }
        else
        {
            Logger.Warning("Invalid format: {Value}", value);
            return 0;
        }
    }

    public ulong ParseULongParameter(string value)
    {
        if (ulong.TryParse(value, out ulong result))
        {
            return result;
        }
        else
        {
            Logger.Warning("Invalid format: {Value}", value);
            return 0;
        }
    }

    public Vector3? ParseVector3Parameters(string[] parameters, int startIndex = 0)
    {
        if (startIndex < 0 || startIndex >= parameters.Length)
        {
            Logger.Warning("Invalid start index: {startIndex}", startIndex);
            return null;
        }

        if (startIndex + 2 < parameters.Length &&
            float.TryParse(parameters[startIndex], out float x) &&
            float.TryParse(parameters[startIndex + 1], out float y) &&
            float.TryParse(parameters[startIndex + 2], out float z))
        {
            return new Vector3(x, y, z);
        }
        else
        {
            Logger.Warning("Invalid format for Vector3 parameters");
            return null;
        }
    }

    /// <summary>
    ///     Parses a float parameter with the invariant culture (decimal dot), so cheat
    ///     commands like <c>dmg 0.5</c> behave the same regardless of the host locale.
    ///     Returns <see cref="float.NaN"/> when the value cannot be parsed, so callers can
    ///     tell "not a number" apart from a real <c>0</c>.
    /// </summary>
    public float ParseFloatParameter(string value)
    {
        if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float result))
        {
            return result;
        }

        Logger.Warning("Invalid float format: {Value}", value);
        return float.NaN;
    }
}