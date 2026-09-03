# Spawning & Combat Dev Notes

This document explains how the PIN server loads its data, how to spawn enemies
(NPCs / mobs), and how shooting those enemies flows through the combat systems.
It is written for developers working on the GameServer.

> NOTE: `README.md` still lists "There is no combat, projectile or damage
> simulation" — that is **stale**. The branch now wires `WeaponSim`,
> `ProjectileSim`, `CombatSim`, `DamageSystem`, and `HitFeedback` into the
> `Shard` (`Shard.cs`). Enemies just need to exist and be targetable.

---

## 1. Database tools

The compiled Firefall data lives in an SDB file (`clientdb.sd2`). The server
reads it through `StaticDBLoader` / `SDBInterface`. Runtime custom content that
is authored by this project lives as JSON under:

```
UdpHosts/GameServer/StaticDB/CustomData/
```

and is loaded through `CustomDBLoader` / `CustomDBInterface`.

### `Tools/MinimalSDB/`

Reads a full `clientdb.sd2`, keeps only the tables the server loader actually
references, and writes a trimmed `.sd2` file.

Configure it in `Tools/MinimalSDB/config.json`:

```json
{
  "input": "C:\\Program Files\\Steam\\steamapps\\common\\Firefall\\system\\db\\clientdb.sd2",
  "output": "C:\\Program Files\\Steam\\steamapps\\common\\Firefall\\system\\db\\clientdb_minimal.sd2"
}
```

Then from `Tools/MinimalSDB/` run:

```sh
dotnet run
```

The tool greps `StaticDBLoader.cs` for table names and prunes everything else.

### `Tools/CollisionGenerator/`

Builds the physics collision / pose shape data that the server needs in order
to resolve projectile impacts. Without generated collision + pose data, shots
will not register hits (see gating conditions below).

---

## 2. Spawning enemies

There are three ways to spawn an enemy:

### A. Runtime chat command

Typed in the in-game chat with a `\` prefix. Works from any chat channel:

```
\npc 290
\npc 290 5 15 0
\npc 1196
\spawn_npc 2407 10 15 0
```

Implemented by `UdpHosts/GameServer/Systems/Chat/Commands/SpawnNpcChatCommand.cs`.
Aliases: `npc`, `character`, `monster`, `spawn_npc`, `spawn_character`,
`spawn_monster`.

- `\npc <characterTypeId>` spawns at the player's current position.
- `\npc <characterTypeId> <x> <y> <z>` spawns at the given position.
- Prints confirmation back to the player's debug chat.
- Rejects typeIds that are not present in the SDB monster table
  (`SDBInterface.GetMonster`).

### B. Runtime admin command (Admin channel)

The `npc` **ServerCommand** (`Systems/Admin/Commands/SpawnCharacterServerCommand.cs`)
is invoked by sending a message on the **Admin** chat channel. It has the same
syntax and aliases as the chat command above:

```
npc 290
npc 290 5 15 0
```

The Admin channel path is `ChatService.CharacterPerformTextChat` →
`_shard.Admin.ExecuteCommand(...)`.

### C. StaticDB custom data

Add a record to `UdpHosts/GameServer/StaticDB/CustomData/character_spawn.json`
keyed by zone (`zone_id`):

```json
{
  "id": 13,
  "zone_id": 12,
  "type": 290,
  "position": { "X": 1.5, "Y": 15, "Z": 0 },
  "orientation": { "IsIdentity": true, "W": 1, "X": 0, "Y": 0, "Z": 0 },
  "max_health": 5000,
  "max_shields": 0
}
```

- `type` must be a monster typeId present in the SDB monster table.
- `max_health` / `max_shields` are optional; `0` keeps the entity default.
- On shard start, `EntityManager.SpawnZoneEntities(zoneId)` runs once (gated on
  `_shard.Settings.LoadZoneEntities`) and spawns every entry for the current
  zone via `CustomDBInterface.GetZoneCharacterSpawns(zoneId)`.
- This is what the `character_spawn.json` added in this branch does for zones
  `12` and `1003`.

Known monster type ids used during testing:

| id   | name                 |
|------|----------------------|
| 290  | Accord Assault       |
| 1196 | Chosen Fiend         |
| 528  | Melded Aranha        |
| 2342 | Aranha               |
| 2407 | Tanken Saboteur      |
| 1304 | Black Hills Bandit   |

---

## 3. How shooting works

When a player fires:

```
CombatController.FireWeaponProjectile      (client fire packet)
  -> NetworkPlayer.HandleFireWeaponProjectile
    -> WeaponSim.OnFireWeaponProjectile     (reads weapon + ammo, computes spread)
      -> ProjectileSim.FireProjectile       (creates an ActiveProjectile)
        -> [each tick] SegmentRayCast       (physics)
          -> PhysicsEngine.HandleProjectileImpact
            -> enqueue ProjectileHitEvent   (damage is hardcoded 1337)
              -> CombatSim.OnProjectileHit  (hostility gate)
                -> DamageSystem.ApplyDamage (reduces health / shields)
                  -> HitFeedback.TookDebugHit (DealtHit / TookHit to clients)
```

When an NPC's health reaches 0, `DamageSystem` publishes `EntityDamagedEvent`,
which `CharacterLifecycleService` turns into a death transition
(`CharacterDiedEvent`). `NpcDeathService` then applies gib visuals and a corpse
linger.

### Faction / hostility gate

`CombatSim.OnProjectileHit` blocks a hit only when the stance is `Friendly` or
`Self`:

```csharp
var stance = _hostility.GetStance(source.HostilityInfo, target.HostilityInfo);
if (stance == HostilityStance.Friendly || stance == HostilityStance.Self)
    return;
```

Player faction defaults to `1` (Accord). So:
- `290` Accord Assault is Friendly → **can't** be shot.
- Chosen / Melding / Aranha / Bandit types are hostile → take damage.
- Unknown faction relations default to `Neutral`, which passes the gate
  (intentional, because SDB faction-relation coverage may be incomplete).

---

## 4. Gating conditions for hits to land

1. **Weapon + ammo in SDB.** `WeaponSim.OnFireWeaponProjectile` bails if the
   active weapon can't be resolved. The player needs a battleframe loadout with
   a slotted weapon whose ammo exists in the SDB.
2. **Physics body.** `SpawnCharacter` creates a kinetic body
   (`CollidableMobility.Kinematic`), so spawned mobs are hittable.
3. **Pose shape data.** `PhysicsEngine.HandleProjectileImpact` only enqueues a
   hit if `TryGetActivePoseShapeData` resolves the body's pose compound. This is
   produced by `Tools/CollisionGenerator/`. If it's missing, the hit is dropped.
4. **Faction / hostility.** Friendly and Self targets are ignored; hostile and
   unknown/Neutral targets are damaged.
5. **Damage value.** `HandleProjectileImpact` hardcodes `1337` damage. Default
   NPC health is `19192`, so a no-buff mob dies after roughly 15 hits (fewer if
   `max_health` is set lower on the spawn).

---

## 5. Quick reference

| Action                            | Command                                  |
|-----------------------------------|------------------------------------------|
| Spawn mob at player              | `\npc 1196`                              |
| Spawn mob at position            | `\npc 528 5 15 0`                        |
| Spawn mob (Admin channel)        | `npc 2342 7 15 0`                        |
| List chat commands               | `\help`                                  |
| Spawn mob automatically per zone | add a row to `CustomData/character_spawn.json` |
