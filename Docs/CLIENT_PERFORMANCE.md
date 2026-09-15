# Client performance — why the Firefall client pins a CPU

Nothing in this file is a PIN bug report about the server; it is a list of the
things that make `Firefall.exe` expensive on a modern machine, in the order worth
trying them, and it says which of them PIN can actually influence.

> **Read this first:** the client is a 32-bit DirectX 9-era executable from 2014.
> It does not scale past a couple of threads, so "8 cores / 16 threads" is not the
> headroom it sounds like. Task Manager showing ~13% on a 16-thread CPU can be one
> thread at 100%, and that is the normal failure mode. Judge it per-thread
> (Process Explorer → Threads, or Task Manager → Details → right click → *Set
> affinity* to see it move), not by the total.

---

## 1. What PIN controls: how many NPCs your client has to draw

This is the one item on the list that is *worse than the live game was*, and it is
the first thing to try.

PIN's world population spawns every mob and NPC the database puts in the zone,
around the players in it, on by default — up to `WorldPopulationMaxLiveNpcs`
(**150**, in `GameServer.config.json`), inside 150 m of a player. Every one of
those is an entity the client has to scope in, animate, and run its own
client-side simulation for. A live Firefall shard in 2014 had authored spawn
groups; PIN reconstructs placement from the zone's collision, and it is generous
by comparison.

Turn it down before touching anything else:

```json
{
  "SpawnWorldPopulation": true,
  "WorldPopulationMaxLiveNpcs": 40
}
```

Or switch it off entirely (`"SpawnWorldPopulation": false`) to confirm the
diagnosis — an empty zone with the same CPU load means the population is not your
problem and you can stop here.

`\population status` in chat reports what is currently alive, and
[WORLD_POPULATION.md](WORLD_POPULATION.md) lists every other brake (cell size,
NPCs per cell, activation radius, tick interval) if you want the world but not the
cost.

The **server-side** symptom of the same population is ping and connection
problems rather than low framerate: the shard's tick is one thread, and a
populated zone used to spend most of it in per-NPC physics ray casts. The AI
now casts line of sight on the perception cadence instead of every 50 ms
movement tick, the six-ray wall clearance probe runs every other movement tick
(spanning the whole gap it covers), the view-change flush runs at 50 Hz instead of
200 Hz, and a standing NPC no longer re-allocates and
re-marks its movement view on every tick — so the same `WorldPopulationMaxLiveNpcs`
now costs a fraction of what it used to on the server: line of sight is cast
four times less often for an engaged NPC, a walking NPC probes its walls half
as often, an idle NPC no longer allocates anything per tick, and every
entity's view flush runs at a quarter of the old frequency. The per-NPC floor
that the density knob could never remove is much thinner, so the knob scales
cleanly instead of sitting on it. See [NPC_AI.md](NPC_AI.md) for the two
ray-cast changes.

## 2. What the client controls

These are all client-side, all from the 2014 community, and all still true
because none of them is something a private server can change.

### Voice chat — the best-documented one

Firefall's VOIP subsystem caused multi-second freezes for a large number of
players, to the point that "disable VOIP" was the standard first fix. In
`firefall.ini`, next to the PIN block:

```ini
[VOIP]
Enabled = false
```

Worth also setting `firefall.ini` read-only afterwards: the launcher rewrites it
and will drop the section ([Steam guide, 2014][guide]; [Steam thread on
10–30 s freezes][voip-thread]).

### Texture streaming costs CPU even when it downloads nothing

Streaming is not free on the client side. From the [Steam discussion on texture
streaming][streaming-thread]: *"it uses some of your cpu usage (noticeable
lagspikes) even though it's not downloading anything. I assume it is crosschecking
your textures back to the server."*

Two consequences:

- If you do **not** have `static.vtex0`/`1`/`2` (see
  [ASSETS.md](ASSETS.md#but-i-already-put-the-vtex-files-in-assets)) you are
  paying the streamer's cost for no benefit at all — the client keeps probing a
  page table it will never fill.
- If you **do** have them locally, `Options → Network → Advanced → texture
  streaming` off is the setting that trades the streamer's CPU for disk and
  memory, which is what the 2014 guides recommended once the files were local.

### Framerate, vsync and dynamic resolution

An uncapped framerate on this engine means the main thread renders as fast as it
can, which reads as 100% CPU on one core even when the GPU is idle. Cap it:

- `Options → Video → Advanced → Dynamic Resolution` — a moving render scale is
  itself CPU work. Set it to **100%** (fixed) with a `Dynamic Resolution FPS`
  target of 60, or lower it deliberately to a fixed value.
- Failing that, cap externally (RTSS / driver-level frame limit) to 60.

If you are chasing stutter rather than load: the same 2014 threads split between
"turn vsync on" and "turn vsync off" and both are right on different machines —
try both.

### Core parking on a many-core CPU

Windows parks cores on processors with lots of them, and a game that lives on two
threads is the worst case for it: Windows is keeping 14 cores asleep while the
two that matter contend. The setting is
`HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583`
(processor core parking) — set `Attributes` to `0` to expose it, then set the
minimum parked cores to 0, or simply use the **High performance** / **Ultimate
Performance** power plan, which does the same thing supportedly
([Microsoft Q&A][core-parking]; the [2014 Firefall guide][guide] recommends the
registry route).

### The settings that were CPU-bound in 2014

From the same [optimisation guide][guide], in rough order of CPU cost: shadows
(quality *and* distance — the single biggest one in most reports), particle
quality, vegetation, animation quality, motion blur, radial blur, sun shafts,
screen-space emissive lighting, and view distance. Anisotropic filtering is
near-free on a modern GPU; view distance is not.

## 3. How to tell which one it is

Do not change five things and guess. Bisect:

1. `SpawnWorldPopulation = false`, restart the server, stand in the busiest place
   you know. Load drops → it was the population; tune
   `WorldPopulationMaxLiveNpcs` back up until it stops being smooth.
2. No change → add `[VOIP] Enabled = false` and cap the framerate to 60.
3. Still pinned → check whether it is one thread or many. One thread at 100% is
   the engine's main loop, and the answer is the render settings above. Several
   threads busy with the game at 100% total is a machine-level problem (power
   plan, thermals, background load), not a Firefall one.

## 4. What is not on this list

- **The server.** If `GameServer`, `MatrixServer` and `WebHostManager` are on the
  same machine as the client, they are competing with it — but that shows up as
  *their* processes in Task Manager, not as `Firefall.exe`.
- **Anything in `firefall.ini` beyond `[VOIP]` and the two paths.** The file only
  carries what the client reads; PIN's block is
  [in the README](../README.md#firefallini).

[guide]: https://steamcommunity.com/sharedfiles/filedetails/?id=326109686
[voip-thread]: https://steamcommunity.com/app/227700/discussions/0/616188677794980213/
[streaming-thread]: https://steamcommunity.com/app/227700/discussions/0/616189742665393129
[core-parking]: https://learn.microsoft.com/en-us/answers/questions/1524213/remove-parked-status-of-cpu
