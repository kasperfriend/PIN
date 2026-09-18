# Multithreading

PIN's GameServer runs a shard on one loop thread and one thread per network
socket. Everything a tick does - packets, entities, physics, AI, world
population - happens in that order, on that thread, and stays that way: Bepu's
simulation, the entity tables and a client's channels are each owned by one
thread, and handing them to another one buys nothing that is not paid for in
locks, torn state and bugs that only appear under load.

What *is* threaded is the server's own background work: the two steps that are
pure CPU over data nobody writes while they run, and that a player feels as a
wait.

| Work | Where it runs | Threads |
|------|---------------|---------|
| Zone navigation-mesh bake (`NavigationMesh` constructor) | Shard construction, before the shard accepts a client | `NavigationBakeThreads`, by default `min(cores - 1, 16)` |
| World-population plan build (`WorldPopulationPlanner`) | A background worker, off the shard's tick, once a player is in the zone | `WorldPopulationPlanThreads`, by default `min(cores / 2, 8)` |
| Chunk collision loading (`ZoneLoader` → `ChunkProcessor` → `TagfileLoader`) | Shard construction | 1 - see *What is deliberately still serial* |
| Shard tick (packets, entities, physics, AI, placement) | The shard's loop thread | 1, always |

## 1. The settings

```xml
<add key="ServerWorkerThreads" value="0"/>
<add key="NavigationBakeThreads" value="0"/>
<add key="WorldPopulationPlanThreads" value="0"/>
<add key="PhysicsThreads" value="0"/>
<add key="WorldPopulationPlanOnWorkers" value="true"/>
```

Every count is `0` = automatic, and every count is one number per piece of work:

| Key | Decides | `0` means |
|-----|---------|-----------|
| `ServerWorkerThreads` | The default for the next two, when it is set | *not* automatic: 0 defers to each piece's own rule below |
| `NavigationBakeThreads` | Threads the zone's navigation bake may use | `threads - 1`, capped at 16 (see the table below) |
| `WorldPopulationPlanThreads` | Threads the plan build may use | `threads / 2`, capped at 8 (see the table below) |
| `PhysicsThreads` | Threads the Bepu physics dispatcher runs with | `threads - 2`, capped at 4 |
| `WorldPopulationPlanOnWorkers` | *Where* the plan is built (worker or shard tick) | - (it is a boolean, `true` by default) |

A positive number is always used **as given** - including past the automatic
caps, which is the point of setting one. `1` keeps that piece of work on a
single background thread. `ServerWorkerThreads` is read only when it is greater
than 0, in which case it is the default for the bake and the plan; when it is 0
as well, each of them falls back to its own automatic rule - so a machine nobody
has configured still gets a bake spread over the whole box and a plan build that
leaves the tick some cores.

### Where the plan is built

`WorldPopulationPlanOnWorkers` decides *where* the plan is built:

- `true` (the default) starts a worker the first time the zone has a player in
  it and the plan is not ready. The worker builds the whole plan and the tick
  only watches for it, so the first NPC is placed as soon as the plan's CPU work
  finishes - typically well under a second for a zone of a few hundred thousand
  walkable faces.
- `false` keeps the incremental pacing the service used before: one
  `WorldPopulationPlanWorkPerTick` slice per `WorldPopulationTickIntervalMs`
  update, on the shard's own thread. That is the setting to use on a machine so
  busy that even a short burst on background threads is unwelcome; it costs the
  zone the wait it always had - at 20,000 faces per 250 ms, tens of seconds for a
  large zone. `WorldPopulationPlanThreads` is ignored on this path, because
  nothing is threaded any more.

### What to write, for any CPU

The bake and the plan want different numbers, because they run in different
company. The bake happens while the shard is still loading and refuses clients:
nothing else on the server is busy, so it can take almost every core. The plan
runs while players are in the zone, next to the shard's tick, the physics
dispatcher and - on the machine most people run - the game client, so it should
leave cores alone. The automatic rules are exactly that, in arithmetic:

| Piece | Automatic (`0`) | Why |
|-------|-----------------|-----|
| `NavigationBakeThreads` | `threads - 1`, capped at **16** | Everything but the shard's own core; the client is not running yet. Past ~16 the passes are memory-bound and more threads stop helping |
| `WorldPopulationPlanThreads` | `threads / 2`, capped at **8** | The other half is what the tick, the dispatcher and the client are doing while the plan is built |
| `PhysicsThreads` | `threads - 2`, capped at **4** | A 20 Hz timestep over mostly static geometry; **leave this alone** unless a big fight stutters (see below) |

So **writing nothing gives you a sane number for your CPU**. Here is what "nothing"
resolves to, per logical processor count (`lscpu`, Task Manager → Performance →
CPU → Logical processors, or `echo %NUMBER_OF_PROCESSORS%`; a 4c/8t chip reports 8):

| Threads (typical chip) | Bake (`0`) | Plan (`0`) | Physics (`0`) | Bake if the game client shares the box | Plan if the client shares the box |
|---|---|---|---|---|---|
| 2 (1c/2t) | 1 | 1 | 1 | 1 | 1 |
| 4 (2c/4t, i3) | 3 | 2 | 3 | 1 | 1 |
| 6 (3c/6t) | 5 | 3 | 4 | 2 | 1 |
| 8 (4c/8t, i7-4790, Ryzen 3) | 7 | 4 | 4 | 3 | 2 |
| 12 (6c/12t, Ryzen 5 5600X) | 11 | 6 | 4 | 5 | 3 |
| 16 (8c/16t, Ryzen 7 5800X, i7-11700) | 15 | 8 | 4 | 7 | 4 |
| 20 (10c/20t, i9-10900) | 16 | 8 | 4 | 8 | 4 |
| 24 (12c/24t, Ryzen 9 5900X) | 16 | 8 | 4 | 8 | 4 |
| 32 (16c/32t, Ryzen 9 5950X) | 16 | 8 | 4 | 8 | 4 |

The "shares the box" columns are half of the automatic ones (rounded down, never
below 1) - the server and the client are two programs competing for the same
package, and the client needs a couple of cores to keep its own load from
stuttering. Write them like this:

```xml
<!-- 8 threads, game client on the same machine -->
<add key="NavigationBakeThreads" value="3"/>
<add key="WorldPopulationPlanThreads" value="2"/>
```

A few things worth knowing before you tune:

- **You do not have to set anything.** The table above is what `0` already does.
  Every key is only there for the machine that is not typical: a dedicated server
  with more cores than the caps, a box shared with the client, or a small machine
  that should be left alone while it plays.
- **A number you write is used as given**, including past the caps: `16` in a
  dedicated 16-thread box's bake is fine, `24` is not faster and may be slower.
- **Numbers are ceilings, not reservations.** The work is cut into contiguous
  slices, so 8 threads on a 16 m² test mesh use one of them. Nothing is idle-spun.
- **The bake is disk- and memory-bound past a point, and its stacked-island pass
  is single-threaded**, so the bake never scales perfectly - doubling threads does
  not halve the load. The plan build scales better; it is pure in-memory work.
- **One number for everything:** `ServerWorkerThreads` sets both the bake and the
  plan at once (both per-piece keys still override it), which is the setting to
  use if you would rather not think about the two.
- **Restart required.** These are read from `App.config` at startup.

`PhysicsThreads` is the one knob that is not about loading or populating: it
changes how much of each tick the physics solve spreads over. Every Bepu dispatch
thread spins while it waits for work, so raising it takes CPU from everything else
on the machine - including the shard's own tick. `0` is what the engine has always
used (`threads - 2`, capped at 4) and is right for the shipped content; raise it
only for a zone with many moving bodies (a big fight), and watch the tick time
when you do.

### Where they are read

All five come from `App.config` at startup like every other setting (`App.Default.config`
ships them with these comments; see `Docs/WORLD_POPULATION.md` §6 for the rest of the
population block), and the shard logs what it resolved:

```
Threads: navigation bake 15, world population plan 8 thread(s) off the shard's tick, physics dispatcher 4 - configured ServerWorkerThreads 0, NavigationBakeThreads 0, WorldPopulationPlanThreads 0, PhysicsThreads 0 (0 = automatic; 16 logical processors)
```

The bake then says how many threads it used and how long it took:

```
Zone 448: baking the navigation mesh from 987,654 collision triangles on 4 worker thread(s)
Navigation mesh: baked 604,321 walkable faces out of 987,654 collision triangles on 4 worker thread(s)
Zone 448: navigation mesh has 987,654 source triangles and 604,321 walkable faces after 4.3s
```

## 2. The navigation bake

The bake is the last heavy step of loading a zone, and the one that decides when
a shard stops refusing clients - a zone logs `Loaded successfully` and then sits
in its own constructor until the mesh exists. It was already bounded (see
`#107`): the passes are linear, one of them is budgeted, and a zone no longer
spends hours in its own constructor. What was missing is what a bounded pass
still costs on a prod zone: millions of hashed inserts and a database lookup or
two per collision triangle, all of it per-face work over an array nothing
writes.

The passes that are now spread over the worker threads:

| Pass | Shape | Why it is safe and identical |
|------|-------|------------------------------|
| Face filter | One `NavFace?` slot per source triangle, compacted in index order afterwards | Every index writes only its own slot; the compaction walks the slots in the order the serial loop appended them |
| Duplicate faces | Parallel over hash partitions of the quantised-centroid buckets; each partition walks the faces in ascending index order | A bucket is the only state the pass keeps, and a bucket lands entirely in one partition, so every face is judged against exactly the same earlier faces as before |
| Shared-edge map | One map per contiguous slice of faces, merged back in slice order | A slice covers an ascending run of faces, so appending slices in order to the shared edge leaves the same ascending face list, and the first slice that saw an edge is the earliest face that had it - the same insert order, the same `A`/`B` points |
| Centroid index (the stacked-island pass's input) | One index per slice, merged in slice order | Same as above: each cell's face list is ascending, which is the order the passes over it iterate in |
| Spatial index | One index per slice, merged in slice order | Same again - the number of slices it is cut into does not change any list's contents |

The stacked-island pass itself (`MarkStackedIslands`) stays on one thread. Its
state is a union-find over every shared edge, a per-component area, a set of
dropped components and one shared candidate budget; the pass's outcome depends on
the order in which faces spend that budget, so threading it would mean a mesh
that depends on which thread got there first. It is also the one pass with a hard
bound (32 M comparisons) and a log line when it spends it.

Two contracts keep the parallel bake the same mesh:

- **Every index writes only what it owns.** No pass has two threads writing one
  value; where a pass needs per-thread state (the maps above), the state is
  per-slice and merged by the caller.
- **Merges happen in index order.** Slice order is index order, so anything
  merged from slices comes out with the same contents *and the same order* as the
  serial pass produced. Order is not cosmetic here: adjacency, spatial ties and
  the pathfinder's tie-breaks all read it.

`NavigationMeshTests.ABakeOnSeveralThreadsIsTheBakeOnOne` builds a grid of
ground with duplicated surfaces and canopies on it twice - once with
`maxDegreeOfParallelism: 1`, once with `8` - and asserts the two meshes are the
same face for face and route for route. If a change to the bake makes it depend
on the thread count, that test fails before a zone does.

## 3. The world-population plan

The plan went from "drip-fed to the shard's tick at 20,000 faces per update" to
"built on a worker as soon as somebody is in the zone". That is the change a
player feels: the zone used to take tens of seconds of budgeted updates before
the first NPC could be placed, and every one of those updates spent a slice of
the shard's tick.

Two phases inside the planner are threaded (`WorldPopulationPlanner`):

- **The cell build.** Each queued cell is classified against the anchors around
  it and asks the database about its chunk and its level. The cells are built in
  parallel into per-cell slots and folded into the plan in cell order afterwards,
  so the habitat lists, the refusal count and the density order come out exactly
  as the serial loop built them.
- **The deployable cluster filter.** Every deployable asks the same question of
  every other one - the plan's one quadratic pass - and the answers are
  independent: whether a prop has company cannot depend on which thread counted
  its neighbours.

The surface scan stays single-threaded: it is one dictionary append per
navigation face, and the running sum it keeps per cell would stop being the same
plan if it were split into partial sums.

The data source and the terrain are read from several threads while this
happens, so both must answer without writing shared state. The shipped
`SdbWorldPopulationDataSource` and `PhysicsWorldPopulationTerrain` do: the
database is read-only by then (its tables were loaded at startup), and the two
lazily built lookups they keep - the per-zone `clientonly` chunk map and the
zone's chunk grid - are each built once under a lock. A custom implementation of
`IWorldPopulationDataSource` or `IWorldPopulationTerrain` that mutates state
while answering is not safe to use with worker threads; set
`WorldPopulationPlanOnWorkers` to `false`, or the plan's thread count to `1`.

Parity is asserted, not assumed:
`WorldPopulationPlannerTests.Plan_IsTheSamePlanBuiltOnSeveralThreadsAsOnOne` and
`Plan_KeepsTheSamePlacesWhenTheDeployableRuleRunsOnSeveralThreads` build the
fixed plan on one thread and on eight and compare every cell, habitat, level,
facing and slot. `WorldPopulationServiceTests.Tick_BuildsTheSamePlanOnWorkersAsOnTheTick`
builds one plan on the tick and one on a worker and compares the two.

## 4. What is deliberately still serial

- **Chunk collision loading.** A `.gtchunk` file is read, decompressed and parsed
  into collision objects, and each object is added to the Bepu simulation as a
  shape and a static. The parse is pure, but the conversion interleaves with the
  simulation's shape pool, which belongs to the thread that owns the simulation;
  splitting the two would mean a two-stage loader (parse on workers, upload on
  one thread) and a second copy of every chunk's data in memory. The bakes that
  follow the load are threaded instead, and a warm `CachePath` (which is what the
  shipped `GameServer.config.json` points at) skips the parse entirely.
- **The shard tick.** Packets, entities, physics queries, AI, placement and
  despawns run in a fixed order on the shard's thread. Threading any of them
  means either a lock on state the tick owns alone, or a design in which that
  state is partitioned - a much larger change than this one, and not one a
  player's load time depends on.
- **The Bepu dispatcher.** Physics already runs on the dispatcher fleet the
  engine grew with (`clamp(cores > 4 ? cores - 2 : cores - 1, 1, 4)`); this
  change does not touch it.

## 5. Quick reference

| Question | Answer |
|----------|--------|
| Do I have to configure anything? | No. `0` means "work it out for this machine": bake `threads - 1` (capped 16), plan `threads / 2` (capped 8), physics `threads - 2` (capped 4). The full table is in §1 |
| Which key do I set on an 8-thread CPU? | None for speed. For less load while playing, `NavigationBakeThreads=3` and `WorldPopulationPlanThreads=2` |
| And on a 16-thread CPU? | None for speed (that is 15 / 8 / 4). Sharing the box with the game client: `NavigationBakeThreads=7`, `WorldPopulationPlanThreads=4` |
| What about 6c/12t, 12c/24t, …? | Find your logical processor count in the §1 table; every row is what `0` does already |
| How do I make the server use only one background thread? | `ServerWorkerThreads=1`, plus `WorldPopulationPlanOnWorkers=false` to keep the plan on the shard's tick entirely |
| How do I make it use more *at all*? | Write a number: anything above 0 is used as given, past the automatic caps |
| Why is the bake capped at 16 and the plan at 8? | The bake runs before any client exists (so it may take the box) but stops scaling past ~16 threads; the plan runs next to a live shard and the game client (so it takes half the box) |
| Does the plan change if the thread count changes? | No, and two tests assert it. Cells, slots, positions, facings and monster rows are identical |
| Does the bake change? | No, and `ABakeOnSeveralThreadsIsTheBakeOnOne` asserts it face for face, route for route |
| Does the physics get faster with more threads? | Not for the shipped content - it is mostly static geometry at 20 Hz, and the solve is not the tick's bottleneck. It is a knob for zones with many moving bodies, and it costs CPU everywhere else |
| Which log lines say what happened? | `Threads: navigation bake …`, `baking the navigation mesh from … on N worker thread(s)`, `baked … on N worker thread(s)`, `World population plan for zone … built on N worker thread(s) in …` |
| Why is my CPU still not pinned during the bake? | The stacked-island pass is single-threaded by design, and the zone cache / disk read are serial too - a bake is not a benchmark |
