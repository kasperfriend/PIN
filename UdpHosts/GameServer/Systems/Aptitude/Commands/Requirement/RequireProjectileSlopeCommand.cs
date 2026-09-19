using System;
using System.Numerics;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>aptfs::RequireProjectileSlopeCommandDef</c>: the elevation angle of the character's aim
///     must sit inside [<c>Minslope</c>, <c>Maxslope</c>] before the chain's projectile may be
///     thrown. The world is Z-up (the flight sim reads Velocity.Z), so the slope is the signed
///     elevation in degrees computed from the client-reported aim direction: 0 is level, +90
///     straight up, -90 straight down. The bounds are inclusive on both ends.
/// </summary>
public class RequireProjectileSlopeCommand : Command, ICommand
{
    private RequireProjectileSlopeCommandDef Params;

    public RequireProjectileSlopeCommand(RequireProjectileSlopeCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var character = CharacterRequirement.Find(context, false);
        if (character == null)
        {
            return true;
        }

        var aim = character.AimDirection;
        float len = aim.Length();
        float slopeDeg = len > 0.0001f
            ? MathF.Asin(Math.Clamp(aim.Z / len, -1f, 1f)) * (180f / MathF.PI)
            : 0f;

        bool result = slopeDeg >= Params.Minslope && slopeDeg <= Params.Maxslope;

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }
}
