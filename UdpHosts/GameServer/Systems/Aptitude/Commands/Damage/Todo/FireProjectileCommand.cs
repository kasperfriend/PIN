using System;
using System.Globalization;
using System.Numerics;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.PRNG;

namespace GameServer.Systems.Aptitude.Commands.Damage;

public class FireProjectileCommand : Command, ICommand
{
    private FireProjectileCommandDef Params;

    public FireProjectileCommand(FireProjectileCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var shooter = ResolveShooter(context);
        if (shooter == null)
        {
            Logger.Debug("{Command} {CommandId} could not resolve a character to fire from ({Initiator}/{Self})", nameof(FireProjectileCommand), Params.Id, context.Initiator, context.Self);
            return true;
        }

        var ammo = SDBInterface.GetAmmo(Params.Ammotype);
        if (ammo == null)
        {
            Logger.Warning("{Command} {CommandId} references unknown ammo type {AmmoType}", nameof(FireProjectileCommand), Params.Id, Params.Ammotype);
            return true;
        }

        uint time = context.Shard.CurrentTime;

        float damage = AbilitySystem.RegistryOp(context.Register, Params.Damage, (Operand)Params.DamageRegop);
        if (Params.UseWeaponDamage == 1)
        {
            // Weapon damage is resolved from item attributes by the weapon sim; the
            // fallback here keeps the ability functional when no weapon is equipped.
            Logger.Debug("{Command} {CommandId} requested UseWeaponDamage; falling back to data damage {Damage}", nameof(FireProjectileCommand), Params.Id, damage);
        }

        float range = Params.Range;
        if (Params.RangeRegop != 0)
        {
            range = AbilitySystem.RegistryOp(context.Register, range, (Operand)Params.RangeRegop);
        }

        float speed = ammo.ProjectileSpeed;
        if (speed <= 0f)
        {
            speed = 30f;
        }

        if (range <= 0f)
        {
            range = ammo.ConstLifetime > 0 ? (speed * ammo.ConstLifetime / 1000f) : 1000f;
        }

        Vector3 direction = shooter.AimDirection;
        if (direction.LengthSquared() < 0.0001f)
        {
            direction = Vector3.Transform(-Vector3.UnitY, shooter.Orientation);
        }
        else
        {
            direction = Vector3.Normalize(direction);
        }

        if (Params.AimAtTarget == 1 && context.Targets.TryPeek(out var aimTarget))
        {
            Vector3 delta = aimTarget.Position - shooter.Position;
            if (delta.LengthSquared() > 0.0001f)
            {
                direction = Vector3.Normalize(delta);
            }
        }

        if (Params.AimWithGravity == 1)
        {
            // The ammo's own simulation flag decides whether gravity applies; a
            // pre-aimed gravity arc is not modeled server side yet.
            Logger.Debug("{Command} {CommandId} requested AimWithGravity which is not implemented", nameof(FireProjectileCommand), Params.Id);
        }

        Vector3 origin = shooter.GetProjectileOrigin(direction);
        if (TryParseVector3(Params.AimOffset, out var aimOffset))
        {
            // Offsets described relative to the shooter are rotated with the entity.
            origin += Vector3.Transform(aimOffset, shooter.Orientation);
        }

        if (TryParseVector3(Params.AimOriginOffset, out var worldOffset))
        {
            origin += worldOffset;
        }

        int damageInt = (int)MathF.Round(damage);
        if (damageInt < 1)
        {
            damageInt = 1;
        }

        byte burstCount = Math.Max((byte)1, Params.Burstcount);
        for (byte bullet = 0; bullet < burstCount; bullet++)
        {
            Vector3 aimForward = direction;
            Vector3 aimRight = Vector3.Normalize(Vector3.Cross(aimForward, Vector3.UnitZ));
            Vector3 aimUp = Vector3.Normalize(Vector3.Cross(aimRight, aimForward));
            PRNG.Spread(time, (byte)Params.Hardpoint, bullet, aimForward, aimRight, aimUp, Params.Spread, Vector3.Zero, time, out Vector3 spreadDirection);
            uint trace = PRNG.Trace(time, bullet);
            context.Shard.ProjectileSim.FireProjectile(shooter, trace, origin, spreadDirection, ammo, range, speed, ammo.ImpactRadius, ammo.MaxRadius, damageInt);
        }

        return true;
    }

    private static CharacterEntity ResolveShooter(Context context)
    {
        if (context.Self is CharacterEntity selfCharacter)
        {
            return selfCharacter;
        }

        if (context.Self?.Owner is CharacterEntity selfOwner)
        {
            return selfOwner;
        }

        if (context.Initiator is CharacterEntity initiatorCharacter)
        {
            return initiatorCharacter;
        }

        return context.Initiator?.Owner;
    }

    private static bool TryParseVector3(string text, out Vector3 result)
    {
        result = Vector3.Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] parts = text.Split(',');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
            || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
            || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
        {
            return false;
        }

        result = new Vector3(x, y, z);
        return true;
    }
}
