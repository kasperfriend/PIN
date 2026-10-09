# NPC AI movement/combat audit — 2026-10-08

## Verdict

The previous implementation was **not healthy on movement/tactics**, despite having aggro, facing, attacks and a path API. This patch addresses concrete control-flow and planner/locomotion mismatches and adds a small ranged tactical policy. It is **not a claim that all original CAIS behavior is implemented**, or that a live zone has been verified.

Validation: latest implementation `4ac49dc` passes all six GitHub CI jobs (Linux/macOS/Windows × .NET 10/11) in [run 37845398821](https://github.com/kasperfriend/PIN/actions/runs/37845398821). Windows .NET 10 also passes publish/payload checks and GameServer/WebHostManager startup smoke tests. Locally, the complete DB census verification, Python checks, `git diff --check`, and syntax parsing of changed C# files pass. The local .NET SDK is absent; no interactive game-client/live-zone reproduction has been performed. Earlier implementation/test-count records below are historical, not counts for this revision.

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

## Follow-up: engagement and tactical-goal lifecycle

This revision fixes correctness issues in PIN's existing policy rather than inventing more CAIS defaults:

- **Damage no longer grants free attacks.** `AiBrain.Aggro` previously reset `NextAttackAt` on every hit. Same-target hits, retargeting and a rapid drop/re-engage could therefore bypass the weapon's authored cadence. Engagement memory still refreshes immediately, but a spent attack deadline survives.
- **Clear the resolved target when combat ends.** Clearing only `TargetId` left the local target/liveness inputs usable by tactical movement during the same update. Dead opponents could briefly retain a tactical goal; Return could retain module navigation inputs. Idle/Return now receive no combat target, and tactical/visibility state is cleared.
- **Revalidate held tactical endpoints.** On perception passes, cover must still occlude the current opponent; a firing position must still have static line of fire; both must remain inside weapon reach. Invalidation releases the goal without resetting the four-second search cooldown. This adds at most one static occlusion check per held goal per perception pass, not eight new path searches. Moving geometry/corridor support is still checked by normal movement.
- **Authored module approaches outrank optional tactics.** An active, closer `am*NavToDist` request cannot be displaced by optional strafing/cover until its `am*NavTimeout` expires or the approach is satisfied. Existing parser data supplies the request/watchdog; this priority is PIN integration policy, not proof of original tree ordering.

New regressions cover repeated/alternate-attacker damage, rapid re-engagement cadence, dead-target goal cleanup, leash return, module approach/watchdog priority, opponent movement around cover, lost firing LOS and out-of-range tactical goals. Implementation `48f869f` passed [CI run 37841533365](https://github.com/kasperfriend/PIN/actions/runs/37841533365): all six OS/SDK jobs succeeded with **1,468/1,468 tests per job**. Windows .NET 10 also passed both startup smoke tests. The complete database movement census and `git diff --check` passed locally. No interactive live-zone validation has been performed.


## Deep pass: authored acquisition, offensive selection and correctness

### Evidence-backed additions

The prod-1962 census was checked across **all three behavior columns of all 3,109 monsters**, not just counted for familiar key names. This revision implements the following bounded interpretations:

| Source | Verified data | Runtime change / limit |
| --- | --- | --- |
| Monsters 390/416; 415 | `SandThresher(leashDistance=100,aggroDistance=15)`; `leashDist=100,aggroDist=15` | Both explicit aggro spellings now set a 15 m proximity radius, consistently in the scanner and brain. Existing leash aliases remain intact. |
| Monsters 1045/1068 | `TorturedSoulMelee(aggroDist=15)` | Use the authored radius, rather than the generic 55 m rule. |
| Monster 700 | `EliteWanderer(...aggroDistance=120,wanderDistance=0,...)` | Allow acquisition at its explicit 120 m radius; keep zero ambient wander distance. Terrain/height/LOS gates still apply. |
| Monsters 923/2109/2115/2457/2462 | Base `am1Cooldown=3000`, offensive `am1Cooldown=8000` | Combat now selects the offensive module set if present; base-first previously discarded the authored combat cooldown. |
| Monsters 1592/1808 | Different base/offensive `am1Id`; 1808's offensive invocation omits base `am2` | Select the whole offensive set, not a synthetic merge or resurrected base module when the offensive one is cooling down. |
| Monster 2241 | Base `triggerPullTime=10000,fireRestDuration=7000`; offensive `triggerPullTime=40` (missing closing parenthesis) | The tolerant parser retains the authored offensive time. Prefer offensive timing when present, otherwise retain base timing. The existing PIN 250 ms floor still applies to the 40 ms cycle. No guessed inheritance of omitted rest from a different tree. The existing resolver also reads combat standoff from this selected invocation; it does not merge a missing distance from the base tree. |
| `aptfs::CombatFlagsCommandDef` / `CombatFlagsCommand` | `restrict_melee` is a distinct replicated flag | Suppress the direct/weapon melee attack while set, without suppressing ranged shots solely because this bit is set. Existing ability/weapon restrictions remain unchanged. |

Reading explicit aggro distances as proximity metres is supported by their names and paired leash values, but this does **not** recover the trees' detection senses or notice/alert transitions. Invalid/negative/non-finite values retain the rules fallback. A synthetic explicit zero disables proximity acquisition even at overlap; damage still bypasses proximity/LOS, but cannot bypass liveness, faction, self-target or zone checks. Offensive selection follows the engine's existing Chase/Attack mode; the original mode-switching tree remains unavailable. Module event timing, ability-facing/targeted semantics and weapon-range-independent module scheduling are **not** recovered by changing the selected invocation.

### Existing-policy correctness fixes

- **Validate retained and damage-driven targets**, not just acquisition candidates. A live player who leaves the shard's zone, or a target becoming friendly, is dropped before the next attack/movement tick, including between perception scans. Retaining a distant/occluded but still-valid target continues to use the existing pursuit/leash/lost-sight rules. Friendly/self/dead/absent damage sources cannot force combat. Non-character damage sources are not fabricated into combat targets.
- **Clear module navigation ownership on combat exit/death.** Route replans intentionally retain watchdog ownership; combat exit now clears it separately. Re-engaging the *same* target after an expired request starts a fresh request instead of inheriting the old expired `am*NavTimeout`.
- **Mix whole entity IDs for decision chance rolls.** The previous module seed used only the controller byte, so different NPCs could share all rolls at the same clock. A separate deterministic PIN decision-noise helper mixes the full body id, action id and 64-bit clock before the existing shared PRNG. Optional tactical searches use it too; probability/cooldown and mandatory pursuit behavior are unchanged. Projectile trace/spread encoding is untouched. This is PIN scheduling noise, not recovered CAIS randomness.
- **Prevent timing overflow.** Widen before adding the two signed timing values; clamp the unsigned profile cadence when converting to the signed brain tuning. Malformed extreme values no longer wrap into a fast weapon/rules fallback. Normal shipped timings are unchanged except the explicit offensive selection above.

### Investigated but not guessed

| Parameter surface | Census outcome / reason not to add a new combat policy |
| --- | --- |
| `perceptionDist` (73 invocations) | Exclusively civilian/interaction/look-at trees: 22 BasicCivilian_Stationary, 21 BasicCivilian, 18 AlertAndInteractive, 10 AlertAndLookAtPlayer, 2 PeacetimeCityWanderer. Not evidence of a hostile aggro alias. Greeting/look-at range behavior remains a separate missing feature. |
| `targetSelectDist` (7) | Special `_instattack`/`_instdefend`, OneOff_FireAtEnemy and EngineerTurretTeleporter scopes alongside one ranged base invocation. No proven shared acquisition semantics; preserved, not repurposed. |
| `leashToSpawn` (32) | Already parsed; all 32 invocations are AlertAndInteractive, whose named world interactions/routing are not supplied by this pass. Generic combat return already uses spawn home. No fabricated civilian route or arbitrary tighter radius. |
| `alwaysLeashInCombat` (2) | Only monster 1600's offensive/defensive Move Then Fire; reference point and transition conditions unknown. Generic combat leash already remains enabled. |
| `hurtAvoidance` (7) | One non-ground Mosquito false value and positive ranged variants; no recovered damage reaction event, timing or dodge sequence. Existing bounded cover policy is still an approximation. |
| `pauseDuration` (14) | Eight ability-user base invocations say zero; monsters 1438/2562 say 500 in all three columns. Which action owns the pause is unknown: not silently added to attack cadence or ambient rest. |
| `am*Facing`, `am*Targeted`, `useWeaponRange`, sniper bands, wide turns | Parameter values exist, but missing tree/action ownership makes a general new scheduler or locomotion state machine speculative. Current limitations are retained explicitly. |

No additional original CAIS definitions or authored route assignments were recovered. Previously inspected public upstream PIN/SINner remain non-evidence of original tree semantics. This pass does not claim flight, climbing, cover animations, greeting parity, mission behavior, or full original-game AI parity.

### Validation

New regressions cover the six actual shipped aggro rows, key validation and non-aliasing, wider/zero radius acquisition, damage bypass, zone/faction invalidation between scans, offensive module selection/cooldown without base resurrection, actual monster 2241 timing (including its missing parenthesis), base timing fallback, melee restriction, same-target module watchdog renewal, whole-id decision noise, and extreme timing overflow. The ambient integration fixture now uses hostile test bodies for explicit combat interruptions instead of using a never-hostile policy while forcing combat.

Local checks passed: database movement census, SdbDump synthetic round-trip, five Python unit tests, documented ability-chain placeholder audit, C# syntax parsing and `git diff --check`. The sandbox has no .NET SDK: build/test execution is delegated to the full GitHub Actions matrix. Implementation `4ac49dc` passed [CI run 37845398821](https://github.com/kasperfriend/PIN/actions/runs/37845398821): all six OS/SDK jobs passed Build, Test and Report test results. Windows .NET 10 also passed publish, payload verification and both startup smoke tests. The preceding run caught a namespace qualification error in a new test fixture, fixed before this passing run. No exact new test count is asserted: the job/step results were retrieved, but the detailed log download host was inaccessible from this sandbox. Interactive live-zone/crowd validation remains outstanding.

## Friendly NPC movement verification — 2026-10-09

The dedicated [friendly movement report](FRIENDLY_NPC_MOVEMENT_VERIFICATION.md) is generated from the actual prod-1962 monster/faction/relation tables and current repository character placements, not inferred from NPC names. `python3 Tools/SdbDump/friendly_npc_audit.py --check` is now included in CI. Friendly here means **PIN's directional faction result toward player faction 1**, not independently recovered original allegiance.

- 1,901 templates resolve friendly under that policy. Of the town/guard categories, 123 PeacetimeCityWanderer, 113 GuardCityWanderer, 21 BasicCivilian and one healing city wanderer exist. The two city-prefix rows (459/551) correctly report MissingRoute; the others have bounded ground walking, not an authored ordered patrol.
- **Only four friendly character placements exist in `character_spawn.json`**: monster 290 and monster 2407 in zones 12 and 1003. Monster 290's archetype has no resolved ambient routine. Monster 2407 has an empty invocation but references unavailable CAIS instance 362; PIN's roaming fallback is not a reconstruction of that instance.
- There are **zero placements of defined work stations**, so database `restFunction` settings cannot create real town work circuits by themselves. A matching live station can be visited, but placing debug stations is not original world-data recovery.
- Spawn registration and ambient movement are faction-independent; friendly player proximity and rejected friendly forced aggro do not suspend walking. Explicit stationary NPCs, missing route assignments, non-ground locomotion and missing collision remain non-moving where appropriate.

Added focused integration regressions using actual shipped guard/civilian/work/named-route invocations and a faction-table-backed friendly relation: reach a first destination, pause and start a second; remain Idle near a player without attacks; walk to an injected matching station and finish its authored duration; wait without navigation and resume when available; keep explicit stationary/missing-route/unknown-archetype bodies without invented patrols. The fixture supplies test speeds, immediate rest and controlled navigation; it verifies execution/control flow, **not exact original speeds or live-zone geometry**. No runtime patrol policy or invented world coordinates were added. Local census checks, seven Python tests and `git diff --check` pass; new C# regressions require the following CI run before claiming execution success.
