# Spawning & Combat Dev Notes

This document explains how the PIN server loads its data, how to spawn enemies
(NPCs / mobs), and how shooting those enemies flows through the combat systems.
It is written for developers working on the GameServer.

> Spawned NPCs are not scenery: `AiEngine` registers every character that goes
> through `EntityManager.SpawnCharacter` and drives it. See
> [NPC_AI.md](NPC_AI.md) for the state machine, the aggro triggers, the attack
> path and the tunables.

---

## 1. Database tools

The compiled Firefall data lives in an SDB file (`clientdb.sd2`). The server
reads it through `StaticDBLoader` / `SDBInterface`. Runtime custom content that
is authored by this project lives as JSON under:

```
UdpHosts/GameServer/StaticDB/CustomData/
```

and is loaded through `CustomDBLoader` / `CustomDBInterface`.

The path to `clientdb.sd2` (and the `maps` / `assetdb` directories) is read from
`GameServer.config.json` next to `GameServer.exe`; edit that file instead of hardcoding
paths in source. On startup the server scans for a Firefall installation and fills in
any empty `StaticDBPath` / `MapsPath` / `AssetDBPath` automatically (it checks Steam's
install location, all Steam libraries from `libraryfolders.vdf`, then the server
executable/working directory). Set `PIN_FIREFALL_PATH` (or `PIN_STEAM_PATH`) to override
the scan.

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

### `Tools/SdbDump/`

A dependency-free Python decoder for `clientdb.sd2` that needs no Firefall
installation. Use it to browse tables, dump them as JSON, produce the mobs
report, list every spawnable row, or report how much of the file PIN reads:

```sh
python3 Tools/SdbDump/sdb_dump.py info       clientdb.sd2
python3 Tools/SdbDump/sdb_dump.py coverage   clientdb.sd2
python3 Tools/SdbDump/sdb_dump.py spawnables clientdb.sd2 -o spawnables.json

# Write the checked-in reference documents (Markdown spreadsheet + CSV per kind)
python3 Tools/SdbDump/spawn_reference.py     clientdb.sd2
```

The format itself, PIN's coverage of it, and the in-game database commands are
documented in [STATIC_DATABASE.md](STATIC_DATABASE.md). Every spawnable row,
with the exact command that creates it, is in
[SpawnReference/](SpawnReference/README.md).

---

## 2. Spawning enemies

There are four ways to spawn an enemy:

### A0. Generic database spawn command (recommended)

`spawn <kind> <id|name> [<x> <y> <z>]` works from chat and from the Admin
channel, and covers monsters, deployables, vehicles, carryables and turrets. It
accepts names as well as ids, and comes with `sdb` (browse/search) and
`sdbinfo` (inspect a row):

```
\sdb monster aranha 10        # find the ids
\sdbinfo monster 2435         # inspect Aranha Queen
\spawn monster Aranha Queen   # spawn it at your feet
\spawn npc 290 -25.5 118 492  # or by id at a position
```

Implementation: `Systems/Spawning/SDBSpawner.cs` + `StaticDB/SDBCatalog.cs`.
Full reference in [STATIC_DATABASE.md](STATIC_DATABASE.md#4-spawning-from-the-database-in-game).
Which ids/names exist for each kind — all 7,396 of them, with the exact command
per row — is in [SpawnReference/](SpawnReference/README.md).

The typed commands below still exist and behave exactly as before.

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
- `orientation` is applied to the spawned character. It is a yaw-only rotation
  about world +Z; identity stands the model upright facing +Y. If you omit it,
  the spawn faces along the entity's default aim direction instead.
- `position`'s `Z` is snapped down to the ground surface when zone collision
  data is loaded, so a placeholder `Z` of `0` spawns on the terrain.
- `max_health` / `max_shields` are optional; `0` keeps the entity default. The
  entity default itself now comes from the static database: `LoadMonster` gives
  every NPC the `dbcharacter::MonsterScaling` health of the level the zone's
  level band resolves to — or of the authored `level` when the entry carries
  one, or of the default player level (1) when the zone has no band (zones
  12/1003 are the untuned ones; see `Docs/NPC_AI.md` §"Health and attack
  damage come from the database") — so `max_health` is an override on top of a
  real per-level value, not on top of the old flat 19,192.
- `level` is optional; `0` resolves the level from the zone as above, `1`-`80`
  pins this spawn's `MonsterScaling` level directly (the emulator equivalent of
  the live game's spawn flow, which carried the level for zones the database
  did not tune).
- On shard start, `EntityManager.SpawnZoneEntities(zoneId)` runs once (gated on
  `_shard.Settings.LoadZoneEntities`) and spawns every entry for the current
  zone via `CustomDBInterface.GetZoneCharacterSpawns(zoneId)`.
- This is what the `character_spawn.json` added in this branch does for zones
  `12` and `1003`.

Known monster type ids used during testing (each verified against the SDB —
see the [Mobs & NPCs Catalog](MOBS_AND_NPCS.md) for the full database listing):

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
    -> WeaponSim.OnFireWeaponProjectile     (reads weapon + ammo + item damage, computes spread)
      -> ProjectileSim.FireProjectile       (creates an ActiveProjectile, base damage per round)
        -> [each tick] SegmentRayCast       (physics; distance travelled accumulates)
          -> ammo falloff applied           (WeaponDamageMath, by distance to the impact)
          -> PhysicsEngine.HandleProjectileImpact
            -> enqueue ProjectileHitEvent   (the resolved damage)
              -> CombatSim.OnProjectileHit  (hostility gate)
                -> DamageSystem.ApplyDamage (reduces health / shields)
                  -> HitFeedback.TookDebugHit (DealtHit / TookHit to clients)
```

The per-round damage is the weapon's database value, not a constant: the weapon
item's own `Damage Per Round` attribute (954) when it has one, else the resolved
weapon template's `damage_per_round` — see [NPC_AI.md](NPC_AI.md) §5 for the
stat pipeline and `Docs/STATIC_DATABASE.md` for how both are decoded. Whether a
shot loses damage over distance is decided by the fired **ammo** row, exactly as
the database defines it: `damage_decay = 0` means flat damage for the whole
flight; otherwise damage stays full until `damage_decay_rangefrac` of the
weapon's range and then tapers linearly to `min_damage_frac` × per-round damage
at max range (e.g. PvE assault rifle ammo: full until 70% of the range, down to
33% at the range edge).

When an NPC's health reaches 0, `DamageSystem` publishes `EntityDamagedEvent`,
which `CharacterLifecycleService` turns into a death transition
(`CharacterDiedEvent`). `NpcDeathService` then applies gib visuals and a corpse
linger.

### The other direction: NPCs shooting you

`AiEngine` runs the reverse path on the shard tick. A mob acquires you by
proximity or by being shot, walks towards you and, once you are inside its attack
range with an unobstructed line of sight, applies its database attack damage
(`dbcharacter::MonsterScaling.damage`, at the level the zone band — or the
spawn's authored `level`, or the default player level for untuned zones —
resolved it to) through the same `DamageSystem` and sends the
same `TookHit` feedback a weapon hit produces:

```
AiEngine.Tick
  -> AiBrain.Decide              (Idle / Chase / Attack / Return / Dead)
    -> DamageSystem.ApplyDamage  (shields, then health; the monster's DB damage)
      -> EntityDamagedEvent      (bleedout / death for the player)
    -> HitFeedback.TookDebugHit  (TookHit to scoped clients)
```

Attacks are hitscan, so there is nothing to dodge. Full details, the tunables and
the `\ai` commands are in [NPC_AI.md](NPC_AI.md).

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
5. **Damage value.** Per-hit weapon damage is database driven: the weapon item's
   `Damage Per Round` attribute (954) — falling back to the resolved weapon
   template's `damage_per_round` — and the ammo's distance falloff, so different
   weapons kill at different rates (see §3 above). The only remaining flat
   `1337` is `ProjectileSim.LegacyPlaceholderDamage`, used when a weapon row has
   neither attribute nor template damage. NPC health is database driven too:
   every monster spawns with the `dbcharacter::MonsterScaling` health of its
   level (the zone's level band, or 45 on zones without one — see
   [NPC_AI.md](NPC_AI.md) §5). Example (build `prod-1962`): the Accord Assault
   plasma cannon deals 100 per round with no falloff, so a no-buff level-45 mob
   (27,869 HP) dies after ~279 direct hits — a mob's health is scaled for the
   zone while the level-45 starter weapons are not endgame items yet (fewer
   hits when the spawn's `max_health` sets a lower value).

---

## 5. Quick reference

| Action                            | Command                                  |
|-----------------------------------|------------------------------------------|
| Spawn mob at player              | `\npc 1196` or `\spawn monster 1196`     |
| Spawn mob at position            | `\npc 528 5 15 0`                        |
| Spawn mob (Admin channel)        | `npc 2342 7 15 0`                        |
| Spawn anything by name           | `\spawn monster Aranha Queen`            |
| Spawn a deployable / vehicle     | `\spawn deployable 395` / `\spawn vehicle Cobra XLR` |
| Spawn a carryable / turret       | `\spawn carryable 26` / `\spawn turret Minigun Turret` |
| Search the static database       | `\sdb monster chosen 20`                 |
| Inspect a database row           | `\sdbinfo deployable 395`                |
| Look up any spawnable id/name    | [Docs/SpawnReference/](SpawnReference/README.md) |
| List chat commands               | `\help`                                  |
| Spawn mob automatically per zone | add a row to `CustomData/character_spawn.json` |
| Show your vitals                 | `\health`                                |
| Damage / heal yourself           | `\hurt 5000` / `\heal 5000`              |
| Simulate a fall landing          | `\fall 30`                               |
| Bleedout / revive / die / respawn | `\down` / `\revive` / `\kill` / `\respawn` |
| Show NPC AI status                 | `\ai`                                     |
| Freeze / unfreeze every mob        | `\ai off` / `\ai on`                      |
| List mobs with their AI state      | `\ai list`                                |

The health, damage, death, respawn and fall damage pipeline is documented in
[HEALTH_SYSTEM.md](HEALTH_SYSTEM.md).
