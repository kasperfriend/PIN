# World Population Dev Notes

This document explains the world population system: how a zone gets filled with
the monsters and NPCs the database says belong in it, where their positions come
from, what keeps it from costing the server anything it should not, and how to
inspect or turn it off at runtime.

It is the answer to "the zone is empty except for the handful of entities
`character_spawn.json` authors". The server now spawns **every
`dbcharacter::Monster` row that belongs to the loaded zone**, on ground the zone
itself says an NPC can stand on, around the players who are in it.

> **Placement is a reconstruction from database/collision inputs, not recovered original spawn assignments.** The shipped `clientdb.sd2` has
> no per-zone spawn table - that lived in the live server's spawn groups, which
> never shipped - so the plan derives both halves from data that *is* there:
> *which* rows belong to which kind of ground from the row's own `behavior`,
> `faction_id` and `vendor_id` columns, and *where* that ground is from the
> zone's own baked collision (its navigation mesh), its authored outposts /
> deployables / Melding perimeters (`StaticDB/CustomData`) and its chunk metadata
> (`dbzonemetadata`). See [What the data does not contain](#8-what-the-data-does-not-contain).

> **Only where players are.** Nothing is planned, and nothing is spawned, in a
> zone without a player in it. Cells come alive inside 150 m of a player and are
> removed again beyond 225 m, so the world exists around the players instead of
> all at once.
>
> **Only the shard's zone.** One shard runs one zone (`ZoneId`, default 448):
> the plan's ground is that zone's collision, and only players *in* that zone
> count as present. A player who picked any other entry in the zone picker neither
> activates cells nor triggers planning — populating around their position would
> put New Eden's NPCs on Sertao's coordinates — and when *nobody* is in the
> shard's zone the log says so (`are in other zones … set ZoneId to its id …`)
> instead of spawning nothing in silence. The same mismatch is warned about at
> login (`entered zone … but this shard runs zone …`). To populate another zone,
> set `ZoneId` to its id and restart. See [Single Zone](SINGLE_ZONE.md) for the whole
> one-shard-one-zone model.

> **Both kinds of collision are checked before anything appears.** The physical
> one (ground probe, walkable slope, standing volume clear of the world and of
> every body the simulation knows) and the server side one (a placement grid that
> refuses a spot another planned or spawned body already holds, which no ray can
> see because that body may not exist yet).

> **The cost is bounded by four independent brakes**: a live NPC cap, a spawn
> budget per time window, an update interval, and the plan's own work budget while
> it is being built. A player sprinting into an empty corner of the zone fills it
> over a couple of seconds rather than in one tick.

---

> **What happens after spawning:** [NPC routines](NPC_ROUTINES.md) now interpret
> declared wander/work/rest settings and move through checked ground navigation.
> Unknown or missing original patrol definitions are not replaced by invented
> routes. Population placement and NPC routine execution are separate systems.

## 1. Where the code lives

```
UdpHosts/GameServer/Systems/Spawning/Population/
├── WorldPopulationService.cs           shard integration: streaming, budgets, refills, clearing
├── WorldPopulationPlanner.cs           zone + monster table -> cells of ground -> slots
├── WorldPopulationCell.cs              one 32 m patch: habitat, level, slots
├── WorldPopulationSlot.cs              one planned NPC: row, anchor, facing, entity id
├── WorldPopulationCandidate.cs         one admitted monster row + the WorldPopulationAnchor record
├── WorldPopulationHabitat.cs           Wilderness / Settlement / Melding (flags) + Accepts
├── MonsterHabitatClassifier.cs         row -> "is this world content, and which ground does it fit"
├── SpawnOccupancyGrid.cs               server side collision: spatial hash of placed bodies
├── WorldPopulationHash.cs              the deterministic pseudo-randomness the plan uses
├── PopulationCommand.cs                the `population` command's behaviour (chat + admin)
├── IWorldPopulationRules.cs            every number the system decides by itself
├── StandardWorldPopulationRules.cs     the out of the box tuning + FromSettings
├── IWorldPopulationDataSource.cs       the database seam
├── SdbWorldPopulationDataSource.cs     reads it: SDBInterface + CustomDBInterface + dbzonemetadata
├── IWorldPopulationTerrain.cs          the ground seam
├── PhysicsWorldPopulationTerrain.cs    reads it: navigation mesh, chunk refs, ray casts
├── IWorldPopulationSpawner.cs          the spawn seam
└── EntityManagerWorldPopulationSpawner.cs  spawns through EntityManager.SpawnCharacter
```

Supporting changes outside that folder:

* `Physics/PhysicsEngine.cs` — `TryGetGroundSurface` (ground probe **with the
  surface normal**), `IsStandingVolumeClear` (the body-volume check),
  `HasZoneCollision`, `WalkableFaceCount`, `TryGetWalkableFaceCentroid`,
  `ZoneChunks`, `ZoneBoundsMin/Max`, `ZonePaths` (vehicle/dropship, not NPC),
  `MeldingPerimeters`, `SubZoneRegionCount`, `EncounterNameCount`,
  `IsInsideZoneBounds`
* `Lib/Shared.Collision/ZoneLoading/ZoneLoader.cs` — `ZoneBoundsLayer` 0x21000,
  `ZonePathLayer` 0x20800, `MeldingPerimeterLayer` type 5, `SubZoneRegion`
  0x21700, `EncounterName` 0x21200, `IsInsideZoneBounds()`, plus full map file
  extraction documented in `Docs/MAP_FILES_FINDINGS.md` and
  `MAP_ANALYSIS_FOR_SERVER.md`
* `Lib/Shared.Collision/ZoneLoading/ChunkProcessor.cs` — `SubZoneGrid`
  0x40102 (64×64, 4096-8192 bytes, Ids 1-4 per cell, 7% coverage, not reliable
  habitat), LOD3 collision 0x40003
* `UdpHosts/GameServer/Systems/Spawning/Population/IWorldPopulationTerrain.cs`,
  `PhysicsWorldPopulationTerrain.cs`, `WorldPopulationService.cs` — early-out
  players and cells outside `ZoneBoundsLayer` AABB (from actual client map file)
* `Lib/Shared.Collision/Navigation/NavigationMesh.cs` — `TryGetFaceCentroid`, so
  the plan can enumerate every walkable spot the mesh baked
* `Lib/Shared.Collision/ZoneLoading/ZoneLoader.cs` — `ChunkRefs`, so a world
  position can be answered with the chunk it falls in
* `Systems/EntityManager/EntityManager.cs` — `SpawnCharacter` gained
  `snapToGround` and `scopeToNearbyClientsOnly`; `Add`/`OnAddedEntity` gained the
  distance filter behind it
* `StaticDB/SDBInterface.cs`, `StaticDB/Loaders/*` — the `dbzonemetadata::
  ZoneChunkLinker` table (which chunks a zone is built from, and which of them the
  server never simulated)
* `Shard.cs`, `IShard.cs`, `GameServerSettings.cs`, `GameServerModule.cs`,
  `App.Default.config` — construction, tick order, settings and their parsing
* `Systems/Chat/Commands/PopulationChatCommand.cs`,
  `Systems/Admin/Commands/PopulationServerCommand.cs` — the two spellings of the
  command, both thin wrappers over `PopulationCommand`

Tests: `UdpHosts/GameServer.Tests/MonsterHabitatClassifierTests.cs`,
`SpawnOccupancyGridTests.cs`, `WorldPopulationPlannerTests.cs`,
`WorldPopulationServiceTests.cs` and `Fakes/WorldPopulationFakes.cs` (see
[Testing](#10-testing)).

---

## 2. What gets spawned: the roster

`MonsterHabitatClassifier.TryClassify` reads four things off a
`dbcharacter::Monster` row and answers two questions - *may this row be spawned as
ambient world content at all*, and *which kinds of ground does it belong to*.

**Refused** (not world population):

| Rule | Why | Rows |
|------|-----|------|
| no `chassis_id` **and** no `posetype_id` | nothing to render and nothing to collide with; `CharacterEntity.LoadMonster` keeps such a row alive with a synthesized sphere, which is a debug affordance | 89 |
| `behavior` is one of the exclusion set | see below | 167 |

The exclusion set is behaviours the game attaches to something that is *not* an
inhabitant of the zone: `Null` (173 rows ask for no AI at all), the pets
(`PlayerPet`, `PassivePet`, `Pet_Earthbreaker`, `TestElfPet`, `TestFollowPlayer` -
created by their owner's ability, not by the world), the turret/teleporter props
(`EngineerTurret`, `EngineerTurretTeleporter`, `TurretTeleporterDropshipCannon`,
`TurretTeleporterTarget`), level fixtures (`Elevator`, `DoorUpInteract`) and
development leftovers (`AvoidMatt`, `CraterTest`, `Config`, `Meta`, `MRU`,
`_inst`). The behaviour name is the part before the first `(` and is read with
`NpcBehaviorParams.Parse`, the same parser the AI uses, so
`PeacetimeCityWanderer(wanderRadius=30)` and `PeacetimeCityWanderer` classify
identically.

**Habitat** of an admitted row:

| Signal | Habitat |
|--------|---------|
| `behavior` in the settlement set (`BasicCivilian`, `PeacetimeCityWanderer*`, `GuardCityWanderer`, `*Dialog`, `Stand`, `PerformEmote`, `UseAbilityOnInteract*`, `TraumaDoc`, ...) | `Settlement` |
| `vendor_id != 0` (102 rows) | `Settlement` |
| `faction_id`'s `internal_name` is `melding`, **or** `behavior` starts with `Melding` | `Melding` |
| `faction_id`'s `internal_name` is `chosen` | `Melding \| Wilderness` |
| none of the above (including an empty behaviour - 1,068 rows) | `Wilderness` |

Two of those rules exist because the data is not tidy: a couple of the Melding's
own creatures are filed under other factions (`MeldingAcolyte` under gaea,
`MeldingPuker` under chosen), and the Chosen are the Melding's army - they come
through it and patrol the field around it, so their rows fit both. `vendor_id` is
used rather than `terminal_type_name` because the latter carries its `VENDOR`
default on 3,087 of the 3,109 rows, wildlife included.

Against the shipped database this admits **2,853 of 3,109** rows: 1,023 fit
settlements, 1,731 the wilderness, 351 the Melding.

Every admitted row also carries four columns the plan uses:

| Column | Values in the shipped DB | Use |
|--------|--------------------------|-----|
| `difficulty_cost` | `0` ×2203, `20-100` ×~760, `300` ×35, `1000` ×1 | the encounter budget a cell may spend, and the row's density weight |
| `ai_spawn_delay_ms` | `2000` ×2822, `0` ×234 | delay between a cell being activated and its NPCs appearing |
| `body_radius` | `-1` (inherit) ×3103 | how much room the body needs; `-1` resolves to `0.7` m |
| `body_height` | `-1` (inherit) ×3103 | how much headroom it needs; `-1` resolves to `1.8` m |

`body_radius`/`body_height` fall back to the same numbers the AI uses for its
navigation agent, so a mob is given as much room standing as it is walking.

---

## 3. Where it gets spawned: the plan

The planner turns a loaded zone into cells of ground, each with a habitat and a
level, each holding the slots of the NPCs that belong there. It runs once per
shard, spread over several ticks, and only starts when a player is in the zone.

**Phase 1 - surfaces.** The zone's navigation mesh (`NavigationMesh`, baked from
the zone's own collision) is enumerated face by face. A face exists only if the
triangle was walkable (`normal.Z >= 0.35`) and not excluded from pathing by the
chunk metadata, so every face centroid is a spot the zone itself says an NPC can
stand on. Up to `PlanWorkPerTick` (20,000) faces are accumulated per update.

**Phase 2 - cells.** Centroids are accumulated into 32 m cells; a cell's centre is
the average of the ground that landed in it, so a cell is not a flat square of the
world but the ground the zone actually has inside that square (its Z is a height a
body can start from). Building is incremental too - a cell is classified against
every anchor near it, and on a real zone that is the expensive phase. Per cell:

* **Chunk rule** — the cell's centre is answered with the `dbzonemetadata::
  ChunkRecord` it falls in (from the chunk references the zone file was loaded
  with, measured from the zone's smallest chunk origin because a zone's origins
  are not necessarily multiples of 512 m). A chunk is refused when
  `ZoneChunkLinker.clientonly != 0` (the server never simulated it) or
  `ChunkRecord.remove_in_production != 0` (stripped from the shipped build). Coral
  Forest has 93 chunks of which 64 are server-side, so this is what keeps NPCs out
  of the client-only scenery.
* **Habitat** — from the authored anchors around the cell: outposts (with their own
  radius, 150-550 m in Coral Forest), the zone's deployables (469 of them: sized
  by `DeployableInfluenceRadius`), and every Melding perimeter control point
  (16 Meldings, 4-23 points each: sized by `MeldingInfluenceRadius`; the shipped
  knots are the spline - the planner interpolates edges every 60 m so the
  influence follows the wall instead of a dotted line, see `MAP_FILES_FINDINGS.md`).
  A settlement wins over the Melding around it - an outpost inside a Melding
  perimeter is still a place players respawn in. Anchors are bucketed into 1024 m
  squares so classification is a 3×3 bucket scan rather than a scan of all 509
  anchors.
* **Level** — the `level_band_id` of the **nearest** banded anchor, however far
  away that anchor is, resolved through `SDBUtils.ResolveNpcLevel`; the zone's own
  band when no anchor carries one. This is what reproduces the original level
  gradient: Coral Forest's outposts carry bands 1-5 at the starter outpost up to
  29-30 in its far corners, while the zone band alone says 1-30.
* **Facing** — a settlement's NPCs face their settlement, the Melding's face the
  Melding, open field gets a deterministic yaw. Each slot turns up to ~34° away
  from it so a group does not stand in one formation.

A zone with no walkable surfaces at all (no maps configured, or collision that
produced nothing standable) falls back to the authored anchor positions as its
cells - the only positions left whose height the data vouches for - and says so in
the plan's log line and in `population status`.

**Phase 3 - slots.** Two passes over the cells:

1. **Coverage** gives *every admitted row* one slot in a cell of a habitat it fits,
   so the zone's plan carries the whole roster the zone can host rather than a
   sample of it. A per-habitat cursor makes the probes land on cells the previous
   rows did not use. The difficulty budget is **not** enforced here (a row priced
   above a whole cell's budget still gets its one slot; the cell then simply has no
   room for anything else), but the per-cell count and the plan's slot ceiling are.
   A row is refused when the zone has no ground of its kind at all - a
   settlement NPC in a zone with no outpost, a Melding creature in a zone with no
   Melding - or when the plan has already hit its slot ceiling, and those rows are
   counted and **reported**, not silently dropped.
2. **Density** fills the remaining room in a scattered but deterministic order,
   picking rows by the frequency `WorldPopulationCandidate.DensityWeight` reads out
   of their `difficulty_cost` (`0` → 8, `<=25` → 6, `<=60` → 4, `<=120` → 2, else
   1) and charging that cost against the cell's `MaxDifficultyPerCell`. A row with
   no cost is charged `UnbudgetedDifficultyCost` so a cell cannot fill with
   unlimited free NPCs. The shape this reproduces is the one the original game's
   encounters had: common ambient rows in groups, expensive ones alone.

Slots sit at their cell's centre plus a deterministic jitter inside 0.4 of the cell
size, which keeps a slot in its own cell with room for the body's own radius.

**Determinism.** Roster order (ascending monster id), cell order (ascending grid
key), density order (a hash permutation of the keys), jitter, facing and density
picks all come from `WorldPopulationHash` seeded with the cell key and the slot
index. Two servers with the same database and the same zone plan the same world,
and a cell that is deactivated and reactivated shows the player the same NPCs in
the same places rather than a reshuffle.

---

## 4. Streaming around the players

`WorldPopulationService.Tick` is called from `Shard.Tick` **after** the entity
manager's tick - that is where the zone's own entities are spawned on the first
tick, and population plans around them, seeds its placement grid from them and
spawns through them. It does its work at most every `TickIntervalMs` (250 ms; the
shard itself ticks every 5 ms) and returns immediately otherwise.

Per update:

1. **No players?** Everything this service spawned is removed, the activations are
   forgotten and the update ends. The plan survives (it is only memory), so the zone
   comes back without being planned again. Turning the feature off does exactly the
   same, and touches nothing else - the zone's own entities stay. Players in other
   zones are treated as absent for this purpose (with their own announcement naming
   the zones and the `ZoneId` fix); a mixed crowd populates around the players who
   are here, and `status` counts the rest (`1 players (1 in other zones: …)`).
2. **Plan not complete?** One `Work` call, then return. Nothing spawns until the plan
   exists.
3. **Seed the placement grid** from everything already in the world (the zone's
   authored NPCs, its deployables and outposts, every player), once per plan
   lifetime. Bodies that appear later are caught by the physical check instead,
   which sees every body the simulation knows.
4. **Activation** — cells inside `ActivationRadius` (150 m) of any player that are
   not already active get their slots queued; active cells outside
   `DeactivationRadius` (225 m) of every player get despawned. The 75 m gap is
   hysteresis: a player standing on the boundary does not make the same cell spawn
   and despawn every tick.
5. **Spawns** — the queue is drained under two limits at once: `SpawnBudget` (4)
   per `SpawnBudgetWindowMs` (100 ms) and `MaxLiveNpcs` (150) minus what is alive.
   A slot is filled no earlier than its row's `ai_spawn_delay_ms` after its cell was
   activated, which both honours the column and staggers a cell's NPCs over a couple
   of seconds instead of letting them all land in one update.
6. **Reconcile** — a slot whose NPC is no longer in the world (killed, despawned by
   a mission, removed by a lifetime) gets its slot back and refills after
   `RespawnDelayMs` (30 s) plus the row's own spawn delay: a corpse is not instantly
   replaced by its successor.

Spawning goes through `EntityManager.SpawnCharacter`, the same path an authored
`character_spawn.json` entry takes, so a population NPC is an ordinary NPC
everywhere else in the server: physics body, AI brain, loot, hostility, scope. Two
differences, both deliberate:

* `snapToGround: false` — the placement already probed the ground with a short
  window, while `SpawnCharacter`'s own snap searches 10 km down and reports the
  topmost surface at that X/Y, which would move a validated spot under a bridge or
  a roof onto the structure above it.
* `scopeToNearbyClientsOnly: true` — `EntityManager` introduces a new entity to
  **every** connected client (its own comment calls that a temporary hack), which is
  fine for the handful of entities a zone loads at startup and wasteful for a system
  whose whole job is spawning hundreds of them: a mob 4 km away would be sent to a
  player who is then told to forget it at the next scope check. The filter applies
  the same rule the periodic scope check does, so an entity filtered out here is one
  the scope check would have retracted anyway.

Removal goes through `EntityManager.Remove`, and `Despawn` refuses to remove
anything that is not an NPC this system spawned - a player character is left alone
whatever id it carries.

**When something goes wrong.** The update runs inside the shard's tick and never
throws into it: an update that throws is logged and counted, and after three in a
row the service turns itself off (taking its NPCs with it) instead of throwing four
times a second for the life of the process. Spawning is guarded the same way -
`EntityManagerWorldPopulationSpawner` catches whatever `SpawnCharacter` throws for a
row the entity manager cannot build, reports that row once, and answers `0`, which
the service counts as a refusal and parks the slot after a few of those. A bad row
in the database therefore costs that row, not the shard.

---

## 5. Collisions

A planned position is checked twice, and both checks have to pass.

**Physical** (`PhysicsWorldPopulationTerrain.TryResolveStandingSpot`):

1. No physics engine (a shard without it, the test shards) → refused; nothing can be
   validated.
2. No zone collision at all (`HasZoneCollision` false: no maps configured) →
   **accepted** unchanged, which keeps such a shard working exactly as its authored
   spawns already do.
3. Ground probe 1.5 m up / 3 m down from the planned spot. The short reach is the
   point: the spot comes from a surface the zone's collision baked, so the surface it
   belongs to is at its feet, and a long reach would snap a spot under a bridge onto
   whatever is below it. 1.5 m up is also the AI's own probe distance, so a spot
   validated here is a fixed point of the AI's snap and is not moved by the first AI
   tick.
4. `|normal.Z| >= MinimumWalkableNormalZ` (0.35) - the same cutoff the navigation
   mesh was baked with, so the system never calls ground what the mesh already
   refused. Absolute because a floor and a ceiling are equally unwalkable when they
   are this steep.
5. `IsStandingVolumeClear` - horizontal static probes at ankle, waist and shoulder
   height along both axes (plus 0.1 m of slack), one vertical probe for headroom
   (plus 0.2 m), and a broad phase query for the non-static bodies (players, mobs,
   vehicles, deployables) that already overlap the box.

**Server side** (`SpawnOccupancyGrid` + player distance):

* A spatial hash of every body the plan has already placed and every entity that was
  in the world when the plan finished. A spot is refused when it comes closer to a
  registered body than the two radii plus `MinSeparation` (0.5 m) - so two 0.7 m mobs
  need 1.9 m between their centres. Distances are horizontal with a height window,
  because two NPCs at clearly different heights (a balcony and the street under it)
  are not in each other's way; the exact three dimensional answer is the physics
  check's job.
* `MinPlayerDistance` (25 m) from every player, checked both before and after the
  ground snap, so a cell the player is standing in does not materialise mobs on top
  of them.

This grid is the half no ray can do: what it catches is a body that is *planned but
not spawned yet*, which is how a cell would otherwise end up with its four NPCs
stacked in one place the instant they all appear.

**Refusal policy.** One slot tries `MaxPlacementAttempts` (6) positions per round:
its own anchor first, then jittered ones up to half a cell away (which may cross into
the neighbouring cell - better a mob 20 m from where it was planned than a cell that
never fills). Then:

| Refused by | Treated as | Consequence |
|------------|-----------|-------------|
| ground (no surface, too steep, inside the world, row not spawnable) | permanent | counts a failure; after `MaxPlacementFailures` (8) the slot is **parked** and reported, instead of being spun on forever |
| room (a player, another body) | transient | retried after `PlacementRetryDelayMs` (1 s), never parked |

A parked slot is not retried and is counted in `population status`, so a zone whose
plan does not fit its ground is visible instead of silently short.

---

## 6. Configuration

Every world-population rule is configurable in `UdpHosts/GameServer/App.config` (the
checked-in defaults are in `App.Default.config` and parsing happens in
`GameServerModule`). Values use invariant-culture numbers: use a period for decimal
values, not a comma.

| Key | Default | Meaning |
|-----|---------|---------|
| `SpawnWorldPopulation` | `true` | Master switch. Off means an empty zone (only the authored `character_spawn.json` entities remain) |
| `WorldPopulationMaxLiveNpcs` | `150` | Hard ceiling on live population NPCs, whatever the plan could hold |
| `WorldPopulationActivationRadius` | `150` | Metres from a player within which cells activate |
| `WorldPopulationDeactivationRadius` | `225` | Metres from a player beyond which an active cell is removed; must be greater than activation radius |
| `WorldPopulationCellSize` | `32` | Metres per planning / streaming cell |
| `WorldPopulationMaxNpcsPerCell` | `4` | Most NPCs one cell may hold |
| `WorldPopulationMaxDifficultyPerCell` | `400` | Total `difficulty_cost` one cell may hold during density planning |
| `WorldPopulationUnbudgetedDifficultyCost` | `25` | Difficulty charged to a row whose `difficulty_cost` is 0 |
| `WorldPopulationMaxPlannedSlots` | `20000` | Ceiling on the whole zone plan's retained slots |
| `WorldPopulationSpawnBudget` | `4` | Most NPCs spawned per budget window |
| `WorldPopulationSpawnBudgetWindowMs` | `100` | Length of the spawn budget window, in milliseconds |
| `WorldPopulationTickIntervalMs` | `250` | Milliseconds between population streaming updates |
| `WorldPopulationPlanWorkPerTick` | `20000` | Navigation faces processed by the incremental planner per update |
| `WorldPopulationMinSeparation` | `0.5` | Extra metres of gap required between two NPC bodies |
| `WorldPopulationMinPlayerDistance` | `25` | Metres of clearance from every player before an NPC may be placed |
| `WorldPopulationMaxPlacementAttempts` | `6` | Positions one slot tries in each placement round |
| `WorldPopulationPlacementRetryDelayMs` | `1000` | Wait after a failed placement round, in milliseconds |
| `WorldPopulationMaxPlacementFailures` | `8` | Failed placement rounds after which a slot is parked |
| `WorldPopulationRespawnDelayMs` | `30000` | Base wait after an NPC dies before its slot refills, in milliseconds |
| `WorldPopulationMinimumWalkableNormalZ` | `0.35` | Smallest allowed Z component of a walkable surface normal (0–1) |
| `WorldPopulationDefaultBodyRadius` | `0.7` | Body-radius fallback for a row whose `body_radius` is the `-1` sentinel |
| `WorldPopulationDefaultBodyHeight` | `1.8` | Body-height fallback for a row whose `body_height` is the `-1` sentinel |
| `WorldPopulationDeployableInfluenceRadius` | `25` | Metres around a deployable that count as settlement ground |
| `WorldPopulationMeldingInfluenceRadius` | `120` | Metres around a Melding control point that count as Melding ground |

The service validates every value independently at startup. A non-finite, negative,
or otherwise unsafe value falls back only to that rule's conservative default; a
malformed value is also logged. Zero is intentional and accepted for delays, extra
gaps, influence radii, and the two difficulty-cost values. A deactivation radius
that is absent or not greater than activation keeps the legacy `1.5 × activation`
relationship, so existing configs that only set `WorldPopulationActivationRadius`
continue to work.

> **Upgrading an existing server:** `App.config` is machine-local and intentionally
> ignored by Git, so it does not gain these keys during a source update. Copy the
> complete world-population block from `App.Default.config` into the deployed
> `App.config`, adjust the values for the target machine, and restart the server.
> `\population status` shows the active cap and radii after it starts.

---

## 7. Commands

Available in the in-game chat (with a `\` prefix) and on the Admin channel (without
it). Both spellings run the same code (`PopulationCommand`), so they cannot drift
apart.

| Command | Effect |
|---------|--------|
| `\population` / `\population status` | Four lines: the switch, the plan, the streaming state, the lifetime counters |
| `\population on` | Enables it (`enable`, `1` also work); the zone fills in around players over the next few seconds |
| `\population off` | Disables it (`disable`, `0`); every NPC it spawned is removed at the next update, the zone's own entities stay |
| `\population near [radius]` | Up to 15 live population NPCs within `radius` (default 100 m) of the caller, nearest first, with name, monster id, position and distance |

`status` answers with one chat line per report line, because the chat channel sends
one message and does not split it. The switch is per shard and not persisted.

Example:

```
\population
World population: on (live 124/150 NPCs of 2853 monster rows, 82 kinds in the world)
Plan: 9841 cells, 20000 slots, 2853 rows placed, 214 cells refused by chunk rules
Streaming: 68 active cells, 213 slots queued, 1 players, activate 150 m / deactivate 225 m
Lifetime: 422 spawned, 298 despawned, 17 lost, 214 placements refused, 9 slots parked, 143 bodies in the placement grid
```

---

## 8. Map file findings and what they mean for the server

The client maps archive (3.2 GB, 38 zones, 281 chunks, see
`Docs/MAP_FILES_FINDINGS.md` and `MAP_ANALYSIS_FOR_SERVER.md`) was fully
unpacked and parsed to see what the original client shipped that the server
can reuse without inventing data.

* **ZoneBoundsLayer 0x21000** — AABB Min/Max per zone, from the real zone file.
  Now extracted in `ZoneLoader` and exposed through `PhysicsEngine` → `IWorldPopulationTerrain`.
  `WorldPopulationService` uses it as an early-out: a player outside the AABB
  is not counted as present for activation, and cells outside it are not
  activated at all. This is the zone's own void cull, cheaper than a navmesh
  query, and it comes from the same file the navigation mesh comes from.
* **ZonePathLayer 0x20800** — vehicle/dropship splines, NOT NPC patrols.
  Documented in findings: 4-6 layers per zone, 200-600 points each, used for
  dropship flyovers and vehicle routes. NPC routines must not use these as
  patrol paths; `Docs/NPC_ROUTINES.md` explicitly calls this out. Exposed as
  `ZonePaths` for debug tooling but not used for population or AI.
* **MeldingPerimeterLayer type 5 inside ZoneMeldingLayer 0x21400** — the shipped
  knots of the Melding wall spline. The server now interpolates every 60 m
  between knots (see `SdbWorldPopulationDataSource`) so habitat classification
  follows the wall instead of a dotted line of control points.
* **SubZoneRegion 0x21700** — 3-15 regions per zone, named sub-zones (e.g.
  "Coral Forest - South"). Count exposed for `population status` diagnostics.
* **EncounterName 0x21200** — 0-12 names per zone, authored encounter labels.
  Count exposed for diagnostics; not a spawn table (no per-zone monster list
  exists in the shipped DB).
* **SubZoneGrid 0x40102** — 64×64 grid per chunk, 4096-8192 bytes, Ids 1-4 per
  cell, ~7% coverage, inspected in `ChunkProcessor`. Not reliable for habitat:
  sparse, chunk-local, and overlaps with outpost/melding anchors which already
  give better coverage. Kept documented but not used as a data source.
* **PropDoodad work stations** — inspected but not yet a separate habitat;
  settlement detection already covers deployables and outposts.

The findings doc (`MAP_FILES_FINDINGS.md`, 89 KB) contains the full breakdown
of every layer type, counts per zone, and which layers are server-usable vs
client-only. `MAP_ANALYSIS_FOR_SERVER.md` (16 KB) summarizes the server-side
application decisions.

## 9. What the shipped DB does not contain

Stated plainly, because each of these shaped a decision above:

* **There is no per-zone spawn table in `clientdb.sd2`.** The live server's spawn
  groups held "which mob, how many, where" per zone, and they never shipped. No
  table in the shipped database carries per-zone monster positions, so positions
  come from the zone's own walkable collision and the authored anchors instead.
* **`dbmissions::MissionWaypoint.location` is chunk-local** (±256), not a world
  coordinate, so it cannot be used as a spawn position without knowing which chunk
  it belongs to.
* **The faction tables carry no faction→zone link**, so "belongs to this zone" is
  answered by habitat fit (does this zone contain outposts / Melding at all?) and by
  the level band of the area, not by a faction→zone mapping.
* **`respawn_flags` is not decoded** - its bits are not recoverable from the shipped
  data - so respawning uses one delay for every row (`RespawnDelayMs` plus the row's
  `ai_spawn_delay_ms`) instead of per-row respawn semantics.
* **`body_radius`/`body_height` are the `-1` "inherit" sentinel on 3,103 of 3,109
  rows**, so almost every body is sized by the rules' defaults, which are the AI's
  navigation agent numbers.
* **Deployables and Melding control points carry no radius**, so the planner sizes
  them (`DeployableInfluenceRadius`, `MeldingInfluenceRadius`; Melding edges are
  interpolated every 60 m from the shipped spline knots so the wall is continuous);
  outposts do carry one and use their own.

What this means in practice: the system is faithful to the data that exists - every
row that can be a world inhabitant is placed, on ground the zone vouches for, at the
level its area carries, in the kind of place its behaviour and faction imply - and it
cannot be faithful to a spawn table that was never shipped.

---

## 10. Cost and limits

| Concern | Bound |
|---------|-------|
| Live NPCs | `MaxLiveNpcs` (150). A 150 m activation radius covers roughly 70 cells of 32 m (the cell keys considered are the radius' bounding box, 11×11 = 121, of which only the ones with planned ground activate), i.e. ~280 slots at the 4-per-cell cap. The 150-NPC ceiling leaves headroom for combat and reliable entity state; several players still share that cap |
| Spawn rate | 4 per 100 ms = 40/s worst case. This keeps the scope-in burst below the ordinary zone's traffic budget rather than relying on the scope queue's 800/s drain capacity |
| Update cost | One update per 250 ms: a bounding-box scan of cell keys per player, one queue drain under budget, one pass over the live slots |
| Planning cost | Spread over ticks: 20,000 mesh faces and 20,000 cells per update, so a large zone is planned in a couple of seconds of updates that each stay well under a millisecond of extra work. Planning does not start until a player is in the zone |
| Plan memory | `MaxPlannedSlots` (20,000) slots, tens of thousands of cells; the drafts are dropped as soon as the cells exist |
| Placement queries | The occupancy grid hashes at 1/4 of the cell size (min 4 m) and scans a range derived from the largest registered radius, so a query is a handful of hash cells rather than a scan of the zone |
| Idle zone | Zero. No players means no plan work, no cells, no NPCs |
| A row or a zone that misbehaves | Contained. A spawn that throws is caught per row and the slot is parked; an update that throws is caught per update and the feature turns itself off after three in a row |

The failure mode of a zone whose plan does not fit its ground is reported, not
hidden: `placements refused` and `slots parked` in `status`, `rows have no ground of
their kind in this zone` for rows the zone cannot host, and `cells refused by chunk
rules` for the client-only chunks.

---

## 11. Testing

| File | Covers |
|------|--------|
| `MonsterHabitatClassifierTests.cs` | the exclusion set, each habitat rule, behaviour arguments being stripped, an empty behaviour being eligible |
| `SpawnOccupancyGridTests.cs` | radius + separation, a body bigger than a hash cell, the height window, remove/re-add/clear, negative coordinates |
| `WorldPopulationPlannerTests.cs` | cells from ground, coverage and habitat fit, unplaceable rows, habitat/level from anchors, the level gradient, settlement over Melding, chunk refusals, the count/difficulty/slot caps, the budget-exempt expensive row, jitter bounds, the anchor fallback, work spreading, determinism |
| `WorldPopulationServiceTests.cs` | no players → nothing at all, spawning around a player, the spawn budget, the live cap, the activation radius, despawn on leave and on disable, refill after a death, the row's own spawn delay, the player clearance, parking on refused ground, body separation, the level of the area, `ListLiveNear`, `status`, the command |
| `Fakes/WorldPopulationFakes.cs` | a fixed roster/anchor/level/chunk source, a plane of walkable ground with switches for refusing a placement, and a spawner that records spawns and can kill or despawn one |

The fakes are why the plan and the streaming can be asserted on without a loaded
`clientdb.sd2` or a zone with collision: `IWorldPopulationDataSource`,
`IWorldPopulationTerrain`, `IWorldPopulationSpawner` and `IWorldPopulationRules` are
the seams.

---

## 12. Quick reference

| Action | Command |
|--------|---------|
| See what population is doing | `\population` |
| Fill the zone around you | `\population on` |
| Empty the zone again | `\population off` |
| See what is standing near you | `\population near 50` |
| Spawn one specific row by hand | `\spawn monster 1196` |
| Freeze every mob (AI, not population) | `\ai off` |
| Turn the feature off permanently | `SpawnWorldPopulation` = `false` in `App.config` |
