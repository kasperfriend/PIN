# NPC AI movement/combat audit — 2026-10-08

## Verdict

The previous implementation was **not healthy on movement/tactics**, despite having aggro, facing, attacks and a path API. This patch addresses concrete control-flow and planner/locomotion mismatches and adds a small ranged tactical policy. It is **not a claim that all original CAIS behavior is implemented**, or that a live zone has been verified.

Validation in this workspace: `git diff --check` passes; all 11 changed/new C# files parse without syntax errors using tree-sitter's C# grammar. `dotnet test UdpHosts/GameServer.Tests/GameServer.Tests.csproj --no-restore` could not execute: `dotnet: command not found`. Syntax parsing is not compilation, type checking or a passing test suite. Client map assets and a running game client were not available for a live reproduction.

## Findings and changes

| Area | Finding | Resolution |
| --- | --- | --- |
| Chase | Chase used the same horizontal standoff gate as Attack. A hidden target inside that distance caused an aggroed enemy to rotate without approaching. | Chase always requests an approach to a live target. Stop only on a final, visible, attackable approach; hidden/out-of-height targets continue pathing. |
| Obstacle detours | Every step was clamped by straight-line target distance, including sideways intermediate waypoints. Entering standoff could also clear the entire unfinished route. | Preserve and traverse intermediate corridor waypoints. Apply standoff clamping to the final approach, not tactical movement or obstacle detours. |
| Combat distances | Behavior/module stop distances could exceed weapon reach. | Clamp brain standoff and module navigation distance below weapon reach. |
| Collision-derived mesh | A disconnected shared-edge graph ended the query even when actual collision terrain was continuous. | Retry with a bounded ground-sampled grid after mesh failure. This is not a flat/direct movement fallback and never bypasses support, exclusion or static clearance checks. |
| Terrain consistency | Direct routes and simplified routes only checked endpoint height and walls. They could cross holes; total slope rise was confused with one step. Grid samples used the initial Z for the whole search. | Sample support at 0.5 m intervals; compare consecutive heights. Carry quantized surface height in the grid state and validate final floor height. Preserve exclusion checks along segments. |
| Target height | Jumping targets could invalidate otherwise ordinary ground routes. | Project destination onto nearby ground with bounded vertical reach. The agent's starting support and each traversed step remain mandatory. No long downward agent teleport. |
| Wall probes | Probe origin was overwritten on unprobed ticks, contradicting the claimed accumulated-span check. | Update the origin only when the wall probe actually runs. |
| Ranged tactics | There was no cover selection, retreat or deliberate strafing policy. Shooting from a stationary standoff was the only normal result. | Add `NpcCombatPositioning`: short, reachable local cover under fire/reloading; retreat when too close; visible firing repositioning around standoff; expire cover and resume chase/fire. |
| Search load | Combat queries were unbudgeted; moving unreachable targets could bypass retry cadence. | Shared query-count and approximate physics-work limits, cached terrain samples, failure backoff, and rotating first budget claimant. Exhausted-budget queries wait rather than treating every waiting NPC as unreachable. |
| Diagnostics | An empty path silently looked like broken AI; missing-ground logs only covered step execution. | Rate-limited debug logging for unsupported route failures. |

### Tactical policy boundaries

- Applies to ranged NPCs with real navigation surfaces. Melee enemies approach and attack; they do not adopt ranged cover behavior.
- Does not override `FixedInPlace`, movement restrictions, ability slides, leash return or death.
- At most eight candidate paths per tactical search, one tactical search admitted per movement tick, and a four-second per-NPC cooldown.
- Candidates are six metres around the current position. Reject unreachable/non-finite paths, excessive detours, positions outside the leash, and positions beyond firing reach.
- Cover requires bidirectional **static** chest-height occlusion toward the enemy. Ordinary repositioning requires an unobstructed line of fire.
- A tactical goal lasts 2.5 seconds, then normal approach/fire resumes. This is simple cover-and-reengage, **not** authored cover slots, crouch/peek animations, squad reservations or a recovered original-game behavior tree.
- In open terrain a cover search may find nothing. That is preferable to pretending an exposed location is cover.

## Other AI areas inspected

- **Perception/aggro:** acquisition checks visibility/hostility, liveness, zone and height; current engagement remains stable rather than changing targets every scan. LOS caches are target-keyed. Retargeting through damage now also resets navigation and tactical state.
- **Attack gating:** visibility, straight-line reach, melee vertical band and cadence remain in `AiBrain`. The engine respects weapon restrictions and ability/weapon damage ownership.
- **Weapons:** database weapon profiles, projectile launcher, spread, magazines/reload windows, ability modules and attack feedback already have focused test coverage. Those systems were inspected but were not independently runtime-verified here.
- **Lifecycle:** death cancels attack/reload windows and stops routines/navigation; lost targets and leash returns exit combat. Intentionally fixed bodies remain fixed.
- **Replication:** pose updates and movement states continue through the existing entity/physics/network path. Refused movement reports standing, not a fabricated walk animation.
- **Ambient routines:** successful routes remain cached; ambient work/routine logic and authored-route limitations are retained.
- **Turrets:** remain intentionally stationary and use their separate targeting/weapon logic; no NPC tactical movement is applied to them.

## Remaining risks / limitations

1. **Must compile and run tests before release.** The .NET 10 SDK is absent in this sandbox. Package restore also requires NuGet access, which this environment's outbound allowlist does not provide.
2. **Must validate real zone assets.** Fragmented collision mesh, tight passages, overlapping floors, unusual spawn offsets and steep surfaces may still legitimately fail navigation. The new grid search is bounded and can fail on complex terrain; it does not guarantee every destination is reachable.
3. **Physics budget requires load testing.** Approximate work units are conservative guards, not measured millisecond guarantees. Thousands of engaged NPCs may wait for queries. The rotating claimant avoids a fixed ordering monopoly but is not a full asynchronous path scheduler.
4. **Ground NPCs only.** Flying, climbing, jump links and unavailable authored follow routes are not implemented by this patch.
5. **Crowd avoidance is unchanged.** Static geometry is used for route clearance so kinematic NPC bodies do not deadlock navigation. Dedicated crowd separation and tactical cover reservations remain absent.
6. **Target memory remains the existing policy.** Engaged NPCs path toward the tracked target position during the target-lost grace period, not a fully modeled last-seen-position investigation behavior.
7. **Tactics are compatibility policy.** Species-specific cover eligibility, tactical animation/peek behavior and complex CAIS trees need authored data or further implementation.

## Added regression coverage (not executed here)

- `AiBrainTests`: melee/ranged hidden targets inside standoff still chase; invalid standoff cannot exceed weapon reach.
- `AiEngineTests`: intermediate detour continues after entering Attack; empty combat path never grants direct movement; moving unreachable targets respect failure backoff.
- `NpcPathfinderTests`: unsampled holes, continuous slopes, height-carrying obstacle search, cliffs and non-finite ground.
- `PhysicsGroundWindingTests`: continuous collision ground can rescue a disconnected collision-derived mesh; jumping target projection does not authorize dropping an unsupported agent.
- `NpcCombatPositioningTests`: actual reachable cover, goal expiry/search cooldown, empty paths, open terrain, retreat, leash bounds, line of fire and excessive cover detours.

## Required verification

On a machine with the .NET 10 SDK and package access:

```sh
git submodule update --init --recursive
dotnet test UdpHosts/GameServer.Tests/GameServer.Tests.csproj
```

Then exercise a populated real zone:

1. Aggro melee NPCs at 10–30 m: verify displacement, running pose, final stop and melee reach.
2. Put a low wall between player and NPC while inside standoff: verify a detour instead of stationary rotation.
3. Walk away from ranged NPCs and break LOS: verify pursuit, reacquisition and shooting after exposure.
4. Shoot/reload near usable cover: verify movement behind static cover and reengagement after the short goal expires. In open ground, verify no fake cover selection.
5. Rush ranged enemies: verify local retreat when reachable; preserve fixed-in-place NPCs.
6. Test a slope, gap, cliff, cave/stacked floors and a jumping player: verify no walking across holes or teleporting floors.
7. Test movement-restricted effects and slides: verify AI does not seize displacement from the ability system.
8. Kill/despawn targets and drag NPCs beyond leash: verify target clearing, return/death cleanup and no persistent tactical goal.
9. Load-test many engaged NPCs: record shard tick duration, route failures, retries and movement starvation. Inspect rate-limited `no supported route` diagnostics with NPC and goal coordinates.
