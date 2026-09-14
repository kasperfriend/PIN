# One shard runs one zone

The GameServer simulates exactly one zone per process: the `ZoneId` it was started
with (default 448, New Eden). Everything the simulation reasons about — collision,
authored entities, encounters, NPC and turret AI, world population — belongs to that
zone. There is no multi-zone mode: the client talks to exactly one GameServer, and
playing somewhere else means changing `ZoneId` in the server config and restarting.

## What ZoneId controls

* Physics loads that zone's map (collision + navmesh) into its single simulation.
* `LoadZoneEntities` spawns that zone's authored entities, outposts and encounters.
* The world population plan is built on that zone's ground: only players *in* the
  zone activate cells and count as present.
* NPC/turret targeting and entity scoping ignore players in other zones (one shared
  gate, `ShardZone.IsPlayerInZone`): a position from another map can only coincide
  with this zone's by accident.

## Which zones have data

PIN's authored data (`StaticDB/CustomData`) covers the open-world zones unevenly:

| Zone | Name            | Outposts | Meldings | Deployables |
|------|-----------------|----------|----------|-------------|
| 162  | Diamond Head    | 14       | 5        | 0           |
| 448  | New Eden        | 24       | 16       | 469         |
| 1030 | Sertao          | 12       | 14       | 0           |

(`character_spawn` rows exist only for zones 12 and 1003.)

New Eden is the fullest experience; Sertao and Diamond Head have outposts and
meldings but no placed deployables. A zone with no authored rows still boots —
physics loads whatever map file `MapsPath` has for it — but nothing authored stands
on it. Battlelab_01 (1125) is flagged open-world but has no authored rows at all.

## Playing in another zone

The character selection screen is a zone picker, so a player can land anywhere
regardless of `ZoneId`. That zone is then unsimulated: it renders (the client owns
the map), but the server has no ground truth for it. Deliberately:

* Login warns: `Character … entered zone … but this shard runs zone …`.
* Nothing spawns there: no population, no authored entities, no encounters.
* NPCs and turrets in the shard's zone ignore cross-zone players, and shard-zone
  entities never scope to them — the wrong map can neither see you nor shoot you.
* World population says so once in the log (`are in other zones … set ZoneId to its
  id …`) instead of spawning nothing in silence; `\population status` counts the
  absent (`1 players (1 in other zones: …)`).

Still working cross-zone: respawn (uses the *player's* zone spawn and outposts) and
fall damage (driven by client-reported movement, not server physics).

## Multiplayer

Everyone who picks the shard's own zone entry lands in the same populated zone — no
zone browser needed. Players in different zones cannot meet, help, or fight each
other: they share only chat and the login.

## Known cross-zone behaviors

* Zone/ZoneLang/Say/Yell chat is shard-wide: it is heard in every zone. Deliberate
  for now — it is the only channel a lost player has to ask where everyone is.
* The kill-aura cheat's radius ignores zones. Cheat-only, left as is.
* Cross-zone players still get (kinematic) physics bodies mirroring their pose in
  the shard's simulation. They neither collide nor fall; harmless.

## Switching zones

Set `ZoneId` in `GameServer.config.json` to the wanted zone's id and restart. The
world population plan is rebuilt from scratch, which takes a while on first boot.
