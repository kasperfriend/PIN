using System.Numerics;
using Aero.Protocol;
using AeroMessages.GSS.Turret.Command;
using AeroMessages.GSS.Turret.View;
using GameServer.Entities.Turret;
using GameServer.Extensions;
using GameServer.Packets;
using GameServer.Systems.Combat;
using Serilog;

namespace GameServer.Controllers.Turret;

[Typecode(GssTurretView.BaseController)]
public class BaseController : Base
{
    private ILogger _logger;

    public override void Init(INetworkClient client, IPlayer player, IShard shard, ILogger logger)
    {
        _logger = logger;
    }

    [MessageID(GssTurretCommand.PoseUpdate)]
    public void PoseUpdate(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var poseUpdate = packet.Unpack<PoseUpdate>();
        if (!client.AssignedShard.Entities.TryGetValue(entityId & 0xffffffffffffff00, out var entity) || entity is not TurretEntity turret)
        {
            return;
        }

        if (turret.ControllingPlayer == player)
        {
            turret.Turret_ObserverView.CurrentPoseProp = new CurrentPoseStruct()
                 {
                     Rotation = poseUpdate.Unk1,
                     ShortTime = client.AssignedShard.CurrentShortTime
                 };
        }
    }

    [MessageID(GssTurretCommand.FireBurst)]
    public void FireBurst(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var fireBurst = packet.Unpack<FireBurst>();
        if (!client.AssignedShard.Entities.TryGetValue(entityId & 0xffffffffffffff00, out var entity) || entity is not TurretEntity turret)
        {
            return;
        }

        turret.SetFireBurst(fireBurst.Time);
    }

    [MessageID(GssTurretCommand.FireEnd)]
    public void FireEnd(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var fireEnd = packet.Unpack<FireEnd>();
        if (!client.AssignedShard.Entities.TryGetValue(entityId & 0xffffffffffffff00, out var entity) || entity is not TurretEntity turret)
        {
            return;
        }

        turret.SetFireEnd(fireEnd.Time);
    }

    [MessageID(GssTurretCommand.FireWeaponProjectile)]
    public void FireWeaponProjectile(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var projectile = packet.Unpack<FireWeaponProjectile>();
        if (!client.AssignedShard.Entities.TryGetValue(entityId & 0xffffffffffffff00, out var entity) || entity is not TurretEntity turret)
        {
            return;
        }

        // PoseUpdate already gates on ControllingPlayer; FireBurst/FireEnd do not, and this
        // handler matches PoseUpdate: only the seated gunner may fire, and the shot is
        // attributed to their CharacterEntity (the turret has none). The gunner's equipped
        // weapon is not used; TurretWeaponFire reads dbcharacter::TurretWeapon instead.
        if (turret.ControllingPlayer != player || player.CharacterEntity == null)
        {
            return;
        }

        // Turret FireWeaponProjectile is Time + Aim + HaveMoreData/MoreData, not the
        // character packet's AimDirection / HaveShooterVelocity / ShooterVelocity.
        // MoreData is not named as velocity on the wire, so it is not invented as one.
        TurretWeaponFire.Production.Fire(turret, player.CharacterEntity, projectile.Time, projectile.Aim);
    }
}