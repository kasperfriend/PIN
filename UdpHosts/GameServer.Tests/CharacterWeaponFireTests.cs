using System.Numerics;
using AeroMessages.GSS.Character;
using GameServer.Entities.Character;
using GameServer.Entities.Turret;
using GameServer.Systems.Combat;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     A seated turret gunner already fires through <c>TurretWeaponFire</c>. The character
///     CombatController still receives the client's <c>FireWeaponProjectile</c> for the gun
///     in their hands; answering that used to spawn a second round from the equipped weapon.
/// </summary>
public class CharacterWeaponFireTests
{
    [Fact]
    public void UnattachedCharacter_MayFireTheEquippedWeapon()
    {
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));

        Assert.True(CharacterWeaponFire.ShouldFireEquippedWeapon(character));
    }

    [Fact]
    public void TurretGunner_MustNotFireTheEquippedWeapon()
    {
        var shard = new FakeShard();
        var parent = new CharacterEntity(shard, shard.GetNextGuid(0));
        var turret = new TurretEntity(shard, shard.GetNextGuid(0), type: 21, parent, 0, 0, 0, Vector3.Zero);
        var gunner = new CharacterEntity(shard, shard.GetNextGuid(0));
        gunner.SetAttachedTo(
            new AttachedToData { Role = AttachedToData.AttachmentRoleType.Turret },
            turret,
            0,
            Vector3.Zero);

        Assert.False(CharacterWeaponFire.ShouldFireEquippedWeapon(gunner));
    }

    [Fact]
    public void NullCharacter_MustNotFire()
    {
        Assert.False(CharacterWeaponFire.ShouldFireEquippedWeapon(null));
    }
}
