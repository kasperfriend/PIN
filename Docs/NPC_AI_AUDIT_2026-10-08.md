# NPC AI movement/combat audit — 2026-10-08

## Verdict

The previous implementation was **not healthy on movement/tactics**, despite having aggro, facing, attacks and a path API. This patch addresses concrete control-flow and planner/locomotion mismatches and adds a small ranged tactical policy. It is **not a claim that all original CAIS behavior is implemented**, or that a live zone has been verified.

Validation: implementation commit `1843284` passes the six GitHub CI jobs (Linux/macOS/Windows × .NET 10/11), with **1,460/1,460 tests passing per job**. Windows .NET 10 also passes publish/payload checks and GameServer/WebHostManager startup smoke tests. Locally, the complete DB census verification, `git diff --check`, and syntax parsing of all changed C# files pass. The local .NET SDK is absent; no interactive game-client/live-zone reproduction has been performed. See the final CI record below.

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
- Candidates are at most six metres around the current position, further constrained by authored `maxMove`. Reject unreachable/non-finite paths, excessive detours, positions outside the leash, and positions beyond firing reach.
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

1. **Automated validation is green; live behavior remains unverified.** CI compiles and passes all tests across six OS/SDK jobs. The sandbox lacks a local SDK and interactive client; real-zone movement and load testing are still required.
2. **Must validate real zone assets.** Fragmented collision mesh, tight passages, overlapping floors, unusual spawn offsets and steep surfaces may still legitimately fail navigation. The new grid search is bounded and can fail on complex terrain; it does not guarantee every destination is reachable.
3. **Physics budget requires load testing.** Approximate work units are conservative guards, not measured millisecond guarantees. Thousands of engaged NPCs may wait for queries. The rotating claimant avoids a fixed ordering monopoly but is not a full asynchronous path scheduler.
4. **Ground NPCs only.** Flying, climbing, jump links and unavailable authored follow routes are not implemented by this patch.
5. **Crowd avoidance is unchanged.** Static geometry is used for route clearance so kinematic NPC bodies do not deadlock navigation. Dedicated crowd separation and tactical cover reservations remain absent.
6. **Target memory remains the existing policy.** Engaged NPCs path toward the tracked target position during the target-lost grace period, not a fully modeled last-seen-position investigation behavior.
7. **Tactics are compatibility policy.** Species-specific cover eligibility, tactical animation/peek behavior and complex CAIS trees need authored data or further implementation.

## Initial regression coverage (now passing in CI)

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

## Follow-up: authored combat movement constraints (PR #127)

### Reference investigation

The complete prod-1962 census is still the strongest source available here: `Docs/NpcMovement/monsters.json`, generated by `Tools/SdbDump/npc_movement_audit.py` from the client DB (SHA-256 `de6858fd1e3028cc887a87d71d2315da4c74ed99faa11cec7ab09a7bba60ad47`). The following are **verified invocation values, not recovered tree implementations**:

| Monster IDs | Authored example | Runtime use / boundary |
| --- | --- | --- |
| 1229 | Ranged humanoid `moveChance=0.2` | Optional local tactical searches now use this probability. Required pursuit remains unconditional. |
| 1587, 1849, 1852 | `hurtAvoidance=1,moveChance=.75,maxMove=10` | Chance and maximum travel constrain fallback tactics; hurt-avoidance timing, dive-roll entry and tree sequencing are not recovered. |
| 1438, 2562 | `maxMove=15` | Keep the existing conservative 12 m route-travel cap; the authored value cannot enlarge PIN's safety cap. |
| 1034 | Move Then Fire `moveChance=10.0` in all three columns | Unsupported probability scale: retain fallback rather than silently interpreting 10 as 100%, 10%, or a cooldown. |
| 538, 2186 | `StockMelee(speedMultiplier=1.5,maxDist=0.8)` | Multiply combat walking/running speed by 1.5; do not reinterpret ambiguous `maxDist`. |
| 2354, 2431, 3190 | `GiantAranhaMiniBoss(speedMultiplier=1,...)` | Neutral combat multiplier. Egg-sack tree sequencing is not implemented here. |
| 490 | `Mosquito(grounded=false,hurtAvoidance=false)` | Exclude explicit non-ground invocations from human ground-cover tactics. This does not implement flight. |
| 2832 | `Arch_MedRangedRifleman_Base(stationary=true,calmWanderChance=0)` | Existing fixed-body handling is preserved, including during combat. |
| 1565/1566, 1702/2294, other sniper rows | `runToSnipeDist`/`snipeToRunDist` pairs (20/10, 35/20, 25/10) | Evidence suggests hysteresis, but target reference, movement direction, weapon-range integration and firing during "Run" remain unverified. No guessed sniper state machine in this revision. |

Searches of public GitHub Firefall references inspected [themeldingwars/PIN](https://github.com/themeldingwars/PIN) and [themeldingwars/SINner](https://github.com/themeldingwars/SINner). Upstream PIN's `UdpHosts/GameServer/AIEngine.cs` is an empty `Tick` stub; SINner's small repository tree yielded no CAIS implementation. Neither is evidence of original movement semantics. No original cover trees or authored route assignments were recovered. The sandbox only permits a narrow set of outbound hosts, so general video/wiki research remains outstanding.

### Implemented behavior and explicit approximations

- `NpcCombatMovementProfile` reads **base and offensive** invocations. Offensive keys take precedence individually; missing keys inherit base values. Defensive mode selection is not implemented by the engine and is not fabricated here.
- Valid finite `moveChance` values in [0,1] gate **one optional tactical search per four-second opportunity**, with one random roll per opportunity. Rejected rolls still incur the cooldown. Explicit zero disables optional tactics, never target acquisition, required pursuit, ability navigation, or leash return. This per-search cadence is PIN policy, not verified original timing.
- `maxMove` constrains both candidate displacement and complete route length (detours included). Candidate radius stays at most 6 m and route travel at most 12 m. Explicit zero disables optional movement. Treating this key as metres of route travel is a conservative approximation, not proven CAIS semantics.
- `speedMultiplier` applies only in Chase/Attack; ambient walking and Return are unchanged. Missing/invalid multipliers use 1, and the accepted safety range is (0,4]. The original tree's exact scope and defaults are unknown.
- Explicit `grounded=false` or `climber=true` disallow ground tactical searches. The existing ground-only movement limitation remains; this is not a flight/climbing implementation.
- Existing `combatWalk`, weapon standoff, fixed-body settings, emotes, module-owned movement, movement restrictions and static cover validation remain authoritative.

### CI-discovered corrections and validation

The initial PR CI run `37831771727` completed with failures across the six OS/SDK matrix jobs. Linux .NET 10 **built successfully** and executed 1,431 tests: 1,428 passed, three failed:

1. `GradualSlopeMayClimbMoreThanOneStepOverTheWholeJourney`.
2. `SearchCarriesHeightForwardOnABoundedGroundProbe`.
3. `NpcThatIsShot_AggrosEvenFromOutsideAggroRange`.

The two slope failures exposed zero-initialized `Options`: the omitted/default struct kept `MaxStepHeight=0`. Omitted options now resolve to explicit shipped defaults (1.25 m steps), while an explicitly configured zero step remains valid. The long-distance aggro failure exposed a search horizon in the explicit collision-free development mode. That mode now returns a checked flat goal directly without the terrain search limit; zones with actual collision never take that shortcut.

Additional regression tests cover authored chances and rejected-roll cooldowns, max-move detours, non-ground exclusion, multiplier pursuit, invalid values, offensive precedence, actual shipped monster invocations, and omitted versus explicit-zero step options. Local .NET execution remains unavailable. `git diff --check` and syntax parsing are useful checks but not test/compile substitutes. Follow-up GitHub CI results are recorded below.


### Final automated validation for implementation `1843284`

[CI run 37833360618](https://github.com/kasperfriend/PIN/actions/runs/37833360618) completed successfully:

| Platform | .NET 10 | .NET 11 |
| --- | --- | --- |
| Linux | Build + 1,460/1,460 tests passed | Build + 1,460/1,460 tests passed |
| macOS | Build + 1,460/1,460 tests passed | Build + 1,460/1,460 tests passed |
| Windows | Build + 1,460/1,460 tests passed | Build + 1,460/1,460 tests passed |

Windows .NET 10 additionally passed publishing, payload validation, GameServer startup (Bitter load) and WebHostManager smoke tests. Those smoke/publish steps are intentionally skipped in the other five jobs. Existing StyleCop warnings in account/character/certificate files remain; no test failures remain in this run. This is automated regression/startup validation, **not** proof of in-game AI parity, correct map traversal everywhere, or acceptable crowd performance. The real-zone checklist above is still required.
