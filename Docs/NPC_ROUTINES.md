# NPC routines: movement, work and the original-data boundary

## What this implements — and what it does not

**This is not a recovery of every original Firefall patrol.** The bundled
`prod-1962` client database contains behaviour invocations and tuning, but no
identified table of authored NPC patrol points/route assignments and no CAIS tree
or instance definitions. The map gameplay/encounter content needed to reconstruct
those routes is not in this checkout. Inventing coordinates, connecting mission
markers, or making every NPC wander would not be faithful to that data.

The [complete, reproducible census](NpcMovement/README.md) covers **all 575 tables**,
**all 3,109 monster rows**, and **all three behaviour columns**. Table names are
matched by their hashes against *all* existing record declarations, not just
`StaticDBLoader`'s loaded tables. The previous “311 unidentified tables” count was
a limitation of name harvesting, not an uninspected part of this audit.

What now runs:

- **Declared wanderers** choose bounded ground destinations, follow the existing
  collision/navigation service, pause, then choose another destination.
  **508 templates** resolve to this routine; one of them (700) explicitly specifies
  `wanderDistance=0`, so it remains still. This is **507 potentially mobile template
  types**, not 507 simultaneously spawned NPCs. Spawn admission and streaming caps
  still belong to world population.
- **Explicit stationary bodies** stay put even when the combat brain would like
  to chase. `Null` requests no AI and is not registered at all. Unspecified trees,
  vendors/posing NPCs and named-route requests do **not** receive generic patrols.
- **Work/rest visits** follow an explicit `restFunction` / `function` to a matching
  *live, placed* deployable, reserve it, walk there, face its orientation, play its
  recorded emote, finish the recorded duration, release it, and resume. Only
  `DynamicEmote`, `DynamicEmoteHolstered`, and emote-naming `PerformEmote` activities
  are executable here. Missing emotes are not substituted.
- **Combat interrupts routines** and releases their work reservations. The NPC
  returns toward its original home before resuming ambient travel. Movement
  restrictions and aptitude slides retain ownership of displacement.

### Important limit: the current checkout places no work stations

There are **109 nonempty deployable behaviour definitions** in the database, but
**zero placements of those definitions** in `StaticDB/CustomData/deployable.json`.
The activity executor is implemented and tested, including placement, reservations
and animation, but the existing world will not suddenly acquire chairs, bars or
repair consoles. A template's `visual_offset`/`aim_offset` is not a world position.

An operator can test an actual definition using the existing deployable spawn
command near a matching NPC, for example deployable **116** (`Work`,
`DynamicEmote(emote="utility",emoteDuration=3000)`) and monster **2939**
(`PeacetimeCityWanderer(restFunction="Work")`):

```
\spawn deployable 116
\spawn monster 2939
```

The point must be reachable on the same floor, within the NPC's home area, and not
owned by a player or encounter. Spawning an arbitrary prop beside an NPC does not
make it an activity. These debug commands are a test, **not original placement data**.

## Database inputs and explicit compatibility policy

`NpcRoutineProfile` resolves a base behaviour once at NPC registration. The parser
honours quoted strings and nested invocations; a child's `distance` or `emote`
parameter cannot leak into its parent's movement settings.

| Input | Runtime use |
|---|---|
| Declared wanderer names | `Wander`, `FastWander`, `FastWanderCore`, `WanderWithEmoteVocalized`, the aggressive/elite/swarm/grunt/passive/heavy/medic/suicide wanderers, city/guard wanderers, `BasicCivilian` |
| `EliteStationary(wanderDistance=10,...)` | Only its **explicit** local wander request; not a substring-based classification of every stationary tree |
| `wanderDistance`, then `distance` | Maximum generated leg distance; explicit zero prevents wandering |
| `maxDistFromSpawn` | Home envelope, also bounded by the AI leash safety limit |
| `nearSpawn`, `calmNearSpawn` | Sample around the spawn rather than the current position |
| `restDurationMin` / `restDurationMax` | Milliseconds spent waiting after a leg (zero is valid) |
| `idleEmoteMinTime` / `idleEmoteMaxTime` | The SwarmWanderer idle wait, in milliseconds |
| `walkInRoute`, `walk`, `calmWalk` | Normal-speed walking versus fast-speed running for routine travel |
| `combatWalk` | Offensive behaviour overrides the base value; applies to combat travel, not the calm routine |
| `leashWalk` | Base behaviour's return-home gait when present |
| `leashDistance`, `leashDist` | Per-NPC combat leash; absent/invalid values retain the AI rules |
| `leashToSpawn` | When true, leash is measured from spawn; stored and clamped against jittered home radius |
| `calmWanderChance` | Chance per destination decision; explicit zero never wanders |
| `maxDistJitter` | Per-NPC random add-on to `maxDistFromSpawn` (0..jitter), deterministic per entity id + monster id |
| `despawnWhenStuck`, `despawnDist` | Explicit authored limits; `despawnWhenStuck` parks the NPC instead of retrying forever, `despawnDist` is an outer authored despawn envelope beyond the jittered home radius |
| `swarmRadiusMin` / `swarmRadiusMax` | 13 swarm rows carry formation radii; stored and visible in `\ai routines`, not invented as a crowd solver |
| `stationary=true`, explicit fixed-body invocations | No AI locomotion; does not defeat external ability displacement |
| `restFunction`, `UseWorkDeployables.function` | Exact case-insensitive `DeployableFunction.name` join |
| Work deployable `behavior.emote` | `EmoteRecord.name` lookup; no made-up animation id |
| Work deployable `emoteDuration` | Milliseconds, including `-1` for indefinite; **not** the monster idle-emote seconds helper |
| `DynamicEmoteHolstered` / `endWithEmote` | Holster for work, restore the prior slot when leaving unless something else changed it, optional end emote |

**The CAIS tree defaults were not recovered.** `NpcRoutineRules` makes PIN's
compatibility choices explicit and injectable:

| Missing setting / runtime guard | PIN value |
|---|---:|
| Wander leg distance | 10 m |
| Home radius | 30 m (expanded to fit an explicit leg distance, never past the leash safety limit) |
| Rest interval | 3–7 seconds |
| Work duration when the deployable does not specify one | 15 seconds |
| Failed destination retry | 2 seconds |
| No-progress timeout | 8 seconds |
| Arrival tolerance | 0.4 m horizontal and vertical |
| New ambient route searches | At most 4 per AI movement tick |
| Catch-up displacement after a stalled/disabled shard | At most 250 ms of motion in one update |

Destination randomness is deterministic per full entity id and monster id. It is
**PIN's bounded roaming policy**, not a claim to match the original server's RNG
or patrol selection. An explicit routine radius, pause or zero is not replaced by a
compatibility default merely because it differs from that default. Malformed,
nonfinite and negative distance/time inputs do not produce invalid movement.

Movement still resolves `Monster.normal_speed` / `fast_speed` through `AiSpeeds`.
In this build **3,102 normal speeds and 3,094 fast speeds are `-1` (inherit)**, so
most use the existing `IAiRules` fallbacks (5 / 8.5 m/s). The underlying species
locomotion/pose defaults have **not** been recovered. Calling those fallback speeds
“the original NPC speeds” would be inaccurate.

## Navigation and lifecycle safety

`INpcNavigation` is shared by combat and routines; `PhysicsNpcNavigation` is the
production adapter. Routines do not generate travel without loaded ground. The
existing no-map combat development mode remains available, but is not treated as
world geometry for ambient movement.

- Use the loaded navigation mesh when available. A disconnected corridor is a
  failure, never a reason to fall back to direct movement through the map.
- Retain a successful static ambient corridor until arrival or obstruction;
  combat targets retain their moving-target replan cadence.
- Check the **whole movement step** at intervals no larger than 0.5 m. Every sample
  requires nearby ground, a walkable normal (`Z >= 0.35`), no more than 1.25 m of
  height change, no pathing exclusion and static body clearance.
- Ground probes are local to the current floor. They no longer search 10 km up
  for navigation samples or pull a moving NPC down a 100 m cliff. A hole/missing
  chunk is not imaginary flat ground when world collision is loaded.
- An unreachable/stuck routine releases its reservation and waits before retrying;
  when `despawnWhenStuck=true` (32 rows) the routine parks as `Inactive` instead of
  retrying forever - the world-population slot still owns the NPC's lifetime, so no
  guessed teleport or respawn is introduced. `despawnDist` (2 rows) is treated as an
  outer envelope beyond the jittered home radius.
- Death, unregister, entity removal/replacement and shard clear release work
  reservations. A missing/dead/moved station invalidates its reservation too.
- Work is exclusive per station; player-owned objects, vehicle-owned deployables
  and encounter-controlled interactions are excluded.
- Travelling NPCs clear their idle/work emote. Standing/work/rest poses and normal
  versus fast locomotion use the existing replicated emote/movement fields.
  Position changes update the physics body and go only to scoped observers.

This is still collision-derived navigation, **not the original runtime navmesh**.
It does not implement climbing, jumping, flying, swarm formations, moving-platform
routing or crowd steering. Per-triangle material attribution has the existing
zone-loader limitations described in [NPC AI](NPC_AI.md).

## Missing original content is kept visible

Examples that remain deliberately unresolved:

- Monster **1059 / 1218 / 1350**: `StockShootAndFollowRoute`; **3222**:
  `OneOff_FollowRoute`; **2632**: `NavigateToLocation`. No route/destination is
  supplied by these invocations.
- Monsters **459 / 551**: named `WanderPoint` / `Flee City` points are requested,
  but their placement is not supplied by the client database.
- Monster **1249**: `UseWorkDeployables(function="Rummage",groundOffset=1.6,
  inSpawnVolume=true,climber=true)`. It needs a spawn volume and climbing, not
  a ground-bound human walking routine.
- **757 / 7 / 4** base/offensive/defensive instance references are nonzero. Another
  monster with the same reference is not an executable CAIS tree definition.
- The remaining species/archetype trees, escort targets, defence transitions,
  flee rules, queue/conversation selection, greetings/chatter/healing triggers,
  and work `statusEffect`-only/ability interactions are not inferred
  from names. Their invocations/parameters remain in the census rather than being
  silently represented as implemented features. `maxDistJitter`, `despawnWhenStuck`,
  `despawnDist`, `leashToSpawn` and `swarmRadiusMin/Max` are now read and stored;
  swarm formation itself is still not a crowd solver.

Recovering exact original routines requires **original map gameplay placements,
CAIS behaviour definitions and encounter/spawn-group route assignments** for the
matching game build. There is intentionally no invented route file in this change.

## Inspect a running shard

- `\ai routines` reports routine states, whether ground is loaded, and how many
  tracked NPCs have unspecified ambient definitions.
- `\ai list` includes both the combat state and ambient routine state per entity.
- `\sdbinfo monster <id>` shows the original template and behaviour invocation.
- `\ai off` / `\ai on` still pause/resume the whole AI engine (including combat).

The same commands work on the admin channel without the leading backslash.
`MissingRoute` means the profile needs original route/named-point or non-ground
movement data; it is not a navigation failure being hidden as idle.

## Code and tests

- `Systems/Ai/NpcRoutineProfile.cs`: data interpretation and explicit PIN defaults.
- `Systems/Ai/NpcRoutine.cs`: deterministic ambient scheduling, interruption and cleanup.
- `Systems/Ai/SdbNpcActivityWorld.cs`: live activity lookup and exclusive reservations.
- `Systems/Ai/INpcNavigation.cs`, `PhysicsNpcNavigation.cs`, `NpcGroundMovement.cs`:
  shared routing and checked displacement.
- `Systems/Ai/AiEngine.cs`: registration, combat precedence, movement, emotes and replication.
- `Tools/SdbDump/npc_movement_audit.py`: full-schema and per-monster/deployable census.

Verification commands:

```sh
python Tools/SdbDump/selftest.py
python -m unittest discover -s Tools/SdbDump -p 'test_*.py'
python Tools/SdbDump/npc_movement_audit.py --check
dotnet test UdpHosts/GameServer.Tests/GameServer.Tests.csproj --configuration Release
```

The C# suite includes the complete 3,109-row reference, repeated bounded wandering,
actual parameter examples, stationary/unknown trees, walk/run selection, pauses,
work ownership/timing/holstering, interruption/cleanup, missing ground, blocked
routes, floors/cliffs, deterministic seeds, cached routes and query budgets. CI
runs the audit and full solution tests on Linux/macOS/Windows with .NET 10 and 11.
A Firefall-client playtest is still needed; these tests do not certify visual or
route parity with the original game.
