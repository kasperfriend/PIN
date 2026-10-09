# Friendly NPC movement verification

Generated with `python3 Tools/SdbDump/friendly_npc_audit.py`; use `--check` to verify.

- Build: **prod-1962**; **1,901** of **3,109** monster templates resolve friendly toward player faction 1 under **PIN's current faction policy**.
- This is not original allegiance recovery: friendly/neutral depend on directional wildcard/default handling in `FactionHostility`. Factionless templates are excluded, not assumed hostile or friendly by their behavior name.
- **4 friendly placements** in `character_spawn.json`; template availability does not mean world placement.

## Civilian/guard movement inputs

| Base behavior | Friendly templates | Current placements | Runtime interpretation |
|---|---:|---:|---|
| `PeacetimeCityWanderer` | 123 | 0 | Bounded walking; two named-point requests (459/551) stay MissingRoute |
| `PeacetimeCityWandererCore` | 0 | 0 | Bounded walking when present |
| `PeacetimeCityWandererWithHealing` | 1 | 0 | Bounded walking; healing tree not recovered |
| `GuardCityWanderer` | 113 | 0 | Bounded walking; not an authored ordered patrol |
| `BasicCivilian` | 21 | 0 | Bounded walking; greetings not recovered |
| `BasicCivilian_Stationary` | 34 | 0 | Explicitly fixed |
| `StationaryCivilianDialog` | 8 | 0 | Explicitly fixed |
| `AlertAndInteractive` | 409 | 0 | No inferred patrol; base interactions/emotes are separate |
| `AlertAndLookAtPlayer` | 137 | 0 | No inferred patrol; look-at tree not recovered |
| `(empty)` | 438 | 2 | PIN bounded-roaming fallback, NOT authored route evidence |

## Static JSON friendly placements (not total runtime population)

| Spawn id | Zone id | Monster id | Base invocation | Result |
|---|---:|---:|---|---|
| 1 | 12 | 290 | `Arch_MedRangedHumanoid_Base(triggerPullTime=1500,fireRestDuration=1000)` | No ambient routine for this archetype; no route assignment |
| 5 | 12 | 2407 | `(empty)` | Fallback roam, requires loaded reachable ground |
| 7 | 1003 | 290 | `Arch_MedRangedHumanoid_Base(triggerPullTime=1500,fireRestDuration=1000)` | No ambient routine for this archetype; no route assignment |
| 11 | 1003 | 2407 | `(empty)` | Fallback roam, requires loaded reachable ground |

## Friendly explicit route/follow/unsupported locomotion requests

Base invocations only; offensive/defensive references remain in the full movement census.

| Monster id | Base invocation |
|---|---|
| 459 | `PeacetimeCityWanderer(city_prefix="WanderPoint", flee_prefix="Flee City")` |
| 490 | `Mosquito(grounded=false, hurtAvoidance=false)` |
| 551 | `PeacetimeCityWanderer(city_prefix="WanderPoint", flee_prefix="Flee City")` |
| 557 | `ProtectVehicle(vehicleType=35)` |
| 1059 | `StockShootAndFollowRoute` |
| 1218 | `StockShootAndFollowRoute` |
| 1249 | `UseWorkDeployables(function="Rummage",groundOffset=1.6, inSpawnVolume=true, climber=true) ` |
| 1350 | `StockShootAndFollowRoute` |
| 1360 | `TestFollowPlayer(maxSpeedChange=100)` |
| 2632 | `NavigateToLocation` |

## Verified boundary

- The four placements above cover **only `character_spawn.json`**. `SdbWorldPopulationDataSource` and `MonsterHabitatClassifier` also admit eligible represented settlement templates (vendors and explicit route/prop/unsupported-locomotion requirements are filtered from automatic population); `WorldPopulationPlanner` generates cells/slots around settlement anchors, and `EntityManagerWorldPopulationSpawner` registers them through the same spawn path. Population is enabled by default but constrained by terrain, streaming and caps. Hard-coded/debug/ability spawns are also outside this count. No total live-zone NPC count is asserted.
- `EntityManager.SpawnCharacter` registers NPCs irrespective of faction. `AiEngine` runs ambient routines while Idle; friendly player proximity does not require a combat target to make them walk.
- `NpcRoutineProfile` honors explicit stationary/zero-distance settings; named route, climbing, spawn-volume and non-ground requirements do not acquire invented ground patrols.
- `NpcRoutine` requests destinations, pauses on arrival, chooses subsequent legs and can visit a matching placed work deployable. `PhysicsNpcNavigation.SupportsRoutines` requires loaded collision; rejected paths/steps do not move the body.
- **No authored ordered NPC patrol assignments were identified** in the complete client schema or repository spawn records. Mission waypoints are not NPC routes. Map path layers have no recovered ground-NPC assignment; do not repurpose them as patrol loops.
- **Zero placements** of the 109 nonempty-behavior work deployable definitions are in `deployable.json` (see the full movement census). Work visits require a live, matching, reachable station; templates alone do not place one.
- Placed monster 2407 has an empty invocation **and nonzero behavior_instance_id=362**. PIN does not resolve that CAIS instance; its roaming is a fallback, not evidence that the original NPC had no behavior.
- Factionless monster 2939 requests `PeacetimeCityWanderer(restFunction="Work")`; it is not in the friendly count. Its interaction-like name does not establish friendly faction semantics.
- Exact tree defaults, route assignments, greeting/healing/flee/escort logic, original locomotion speeds and live-zone reachability remain unverified. Bounded roaming is PIN compatibility policy, not proof of original patrol parity.

See [NPC routines](NPC_ROUTINES.md), [complete movement census](NpcMovement/README.md), and [AI audit](NPC_AI_AUDIT_2026-10-08.md).
