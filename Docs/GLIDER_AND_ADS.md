# Gliding and aiming down sights: server state and diagnostics

This is the server-side map for glider pads and ADS. Passing server tests does not establish
that the Firefall client accepted a forced-movement packet or kept its first-person aim
animation. The in-game checks at the end are still necessary.

Related: [GLIDER_PAD_CONNECTION_PROBLEM.md](GLIDER_PAD_CONNECTION_PROBLEM.md).

## What the 17:05–17:06 log establishes

### ADS

`UseScope InScope=1` applies effect **102** or **1313**, including its movement penalties and
`restrict_sprint`. The reported removals follow `UseScope InScope=0`. Unlike the earlier
report, this log does **not** show the apply chain immediately rejecting the scope effect.
Treating the `BattleFrameDuration` placeholder or client audio/animation no-ops as proof of
that earlier failure would be misleading.

There was still a separate state-consistency bug: weapon/fire-mode changes called
`SetScopedState(false)`, which removed the effect but did not reset `FireMode_1`. An external
removal also left the cached scope effect id behind, preventing a subsequent application.
Those paths now clear both halves of ADS. A visual snap-back **while both remain active**
needs client-side/payload investigation, not another speculative server command stub.

### Gliders

The pad activation reaches `ForcePush`, grants effects and reaches `SetGliderParameters`.
The log then exposes two lifetime problems:

- Effect **3419** starts at short time `14690` and is removed at `15532`. Its removal applies
  **3418** and its descendants with the old time `14690`. The new permission's 500 ms grace
  period has already elapsed before it exists. Every later stage keeps inheriting the launch
  timestamp instead of starting its own lifetime.
- Another launch is applied with short time `23291` and cleared at `23224`, a 67 ms clock
  lead. Unsigned elapsed-time subtraction interprets that as `4294967229` ms, not negative
  67 ms. Multiple fresh effects are therefore treated as expired in the same tick.

The flight-profile handoff had a further bug even when the character did become airborne:
**9495** expires, its removal chain applies **3417**, and *then* the old effect's `OnRemove`
restores its previous profile. That overwrites the profile the successor just granted.
Also, a failing optional removal-chain tail (e.g. `RequireHasItem` at command **1508711**)
could skip cleanup entirely, leaving modifiers or flags behind.

## Effect clocks and cleanup

The runtime now keeps these values distinct:

| Value | Purpose |
|---|---|
| `Context.InitTime` / replicated `EffectState.Time` | Timestamp of the event applying the effect. Direct scope applications retain `UseScope.Time`, including zero at uint wrap. |
| `Context.EffectStartTime` | Server application time used for this effect's duration. Never inherited as the lifetime of a child effect. |
| `Context.EffectApplicationTime` | Fresh event time for effects emitted by duration/update/removal chains, without changing the source effect's own time/reload requirements. |
| `StatusEffectsChangeTime_N` | Existing wire convention: the applying event time (16 bits), and server time on removal. Direct ADS timestamps are preserved rather than guessing at new client reconciliation semantics. |
| `EffectState.LastUpdateTime` | Starts at application, so the first duration/update check respects the effect's update frequency. |

`TimeDuration` uses signed modular elapsed milliseconds. A slightly future event does not
instantly expire, and crossing the uint clock boundary does not break short durations.
Removal and update chains supply a fresh event timestamp for effects they create, while
retaining activation identity, deferred cooldowns and proximity-effect bookkeeping.

Removal marks the old state removed, frees its slot, unwinds its active commands in reverse
order, and **then** runs the removal chain. A failed removal-chain requirement cannot suppress
that cleanup. A tick's snapshot skips states already removed by another effect; an old state
cannot clear a replacement that reused its slot. A missing/zero `SetGliderParameters` value
does not register a restoration snapshot: removing that no-op must not overwrite a valid
profile granted later by another effect.

`ImpactApplyEffect` also honours `PassRegister`, `PassBonus` and `InheritInitPos` instead of
copying those payloads unconditionally. Activation identity and cooldown bookkeeping still
follow the original caster.

## Glider state

| State | Field / source | Writer |
|---|---|---|
| Flight profile | `Character_CombatController.GliderProfileIdProp`, a `dbcharacter::GliderParameters` id | `SetGliderParameters` |
| Wings permission | `PermissionFlags.glider` | `ModifyPermission` |
| Glider HUD | `PermissionFlags.glider_hud` | `ModifyPermission` |
| Movement state | `MovementStateContainer`; glider nibble is 7 (`0x7000` in the full state) | Client `MovementInput` |
| Provisional launch window | `CharacterEntity.ServerLaunchPendingSince/UntilTime` | `ForcePush` (opens), first decisive `MovementInput` or expiry (closes) |
| Landing damage exemption | Actual glider/jetpack movement during the fall | `FallDamageSystem` |

The relevant `prod-1962` graph for shared pad ability **35181**, chain **1001671**:

1. **8097** applies the launch restriction and runs `ForcePush` **1509142**. The row has
   strength 30 and additive register operation 1; the module checks can add launch strength.
2. **3419** waits 750 ms (duration command **1508715**).
3. Its removal chain **1508714** applies **3418**, **3758** and **11686** before its optional
   item/projectile tail. These effects now start at the handoff, not at the original launch.
4. **3418** grants wings and HUD permissions. Its duration is an OR chain: falling/gliding/
   glider-thruster/stall movement, **or** its first 500 ms (commands **1508823**, **1508822**).
5. Its profile effect **9495** uses a 2000 ms duration plus `AirborneDuration`, then applies
   **3417**. The latter lasts until landing. The recovered/custom parameter rows used here
   both select profile 18; cleanup of 9495 must not overwrite 3417's grant.

`RegisterMovementEffect` rows **1508976/1508977** have `on_client=1, on_server=0`. Their
flight audio/particle effect **723** is intentionally registered by the client, not applied
by the server. Those debug no-ops are not evidence that glider permission was denied.

## The launch window: why the server must not judge a launch it just commanded

Movement is client-authoritative, so every gate that keeps the launch chain alive
(`AirborneDuration` on 9495/3417, `RequireMovestate` on 3418's OR chain) reads the pose the
client last *reported*. The client cannot report a post-launch pose before it has played
the forced movement the server just sent it, but the chain's first duration ticks run at
+250/+500 ms — inside that window. In the field logs this tore the launch apart
deterministically: 9495 expired ~260 ms after the handoff, 3417 ~258 ms, 3418 ~518 ms, and
the pad re-triggered from proximity again ~2 s later. `RegisterMovementEffect`-based
workarounds cannot fix this: the failing gates are durations/requirements, not effect
registration, and replicating client-only effect 723 server-side pollutes the shared status
effect slots.

So when `ForcePush` sends a launch it also marks the character as
**launch-pending** (`MarkServerLaunchPending`): it seeds the movement state nibble to
glider (`0x7000`) and opens a window that runs to push time + 550 ms (the forced movement
it commanded) + 1500 ms handoff margin. While the window is open:

- `AirborneDuration` counts the character as airborne;
- `RequireMovestate` answers gliding/falling/… from the *pending launch*, not the stale
  ground pose;
- the seeded glider movement state also serves anything else that reads the container
  directly (movement-effect registrations, collision shape selection).

The window is provisional and self-limiting — it never grants permanent gliding. A
`MovementInput` closes it as soon as the pose can answer the question the window exists
for: the pose reports the character airborne (launch confirmed, client truth takes over),
or the pose arrives after the commanded forced window ended (the client's own state again).
If no pose ever arrives the window simply expires at its deadline. Each close logs
`[Glider] Launch handoff: first MovementInput after server push MoveState=... AirTime=...
Airborne=... VelocityZ=...` — that line is the diagnostic that separates "the client never
left the pad" from "the server tore the launch down". A standing `MoveState` with positive
air time on the first post-push input means the launch itself failed client-side and no
server gate was at fault; falling/glider with negative air time means the launch worked and
the chain must stay up (regression-test the gates if it does not).

The existing `ForcedMovement` type-5 launch window is still 50 ms ahead through 550 ms
ahead. Both endpoints and the packet's short time now use one clock snapshot, and the
`[Glider] ForcePush` log includes target, strength, velocity and the window. This records
what the server sent; it does not prove the client acted on it. Do not mask a failed launch
by disabling fall damage or granting gliding permanently.

## ADS state

PIN's existing protocol mapping is:

| State | Field / path |
|---|---|
| Main vs. underbarrel fire mode | `FireMode_0`, from `SelectFireMode` |
| Scoped mode | `FireMode_1`, from `UseScope` |
| Scope effect | `dbitems::WeaponScope.Statusfx` via the active weapon details |

`SetScopedState` now owns both the scoped mode and the effect. Scope-out, weapon/fire-mode
switches, loadout changes, death and external effect removal cannot leave one half active.
Repeated scope-in requests do not stack/restart the effect, and stale `UseScope` messages
are ignored with a wrap-aware timestamp comparison. Scoping in never selects the underbarrel.

The server continues to replicate effect slots on the combat controller/view and the
owner's local-effects controller, retaining the client event time for direct ADS application.
The new `[Scope]` line reports **actual resulting state**, not just the requested boolean:
weapon index, both fire modes, effect id/time, movement state and combat flags.

`RequireAimModeCommandDef` (command type 130, environment `both`) is wired in the aptitude
`Factory` instead of being a permanent `return true` stub. It resolves the character of the
activation like the other character requirements (deployable-owned chains included), passes
when the activation has no character at all, and answers from `FireMode_1`: mode 1 = scoped
passes, 0 = hip fire fails, with `Negate` inverting. Only five rows exist in prod-1962
(43383, 139930, 420492, 1309995, 1315149), mostly as short scope-gated chains; the aim
pose/camera itself remains client work, so the in-game checks below are still the arbiter.

## Remaining limitations

- `BattleFrameDuration` is still a placeholder. ADS is explicitly cleared on loadout changes;
  this patch does not claim to implement that command for every other ability.
- Many custom server-only definitions, including the pad's `SetScopeBubble` and several
  `ImpactRemoveEffect` rows, contain only ids. They remain non-destructive no-ops until the
  missing fields are recovered. `ScopeBubbleInfo` is not established to be a weapon zoom
  control; do not invent a layer to repair ADS.
- Independent, overlapping effects that overwrite the same permission/profile still use
  snapshots rather than a general ownership stack. The sequential glider handoff and
  reverse-order cleanup within one effect are covered here, not every overlap scenario.
- Client-only `PlayAnimation`, `SetAnimCtrlParam`, audio and camera commands remain client
  work. Server logs and field tests alone cannot validate their rendering or reconciliation.

## Checking in game

Use one continuous log covering scope-in/launch through scope-out/landing:

1. **ADS:** on a rifle and a second weapon, hold aim for at least three seconds without
   releasing it, then release. Repeat, and switch weapons/fire modes once while scoped.
   Check that the aim pose holds, zoom clears on release/switch, and sprint restrictions clear.
   The log should show `FireMode_1=1` with effect 102/1313 while held, and mode/effect zero
   after scope-out or a switch. There should be no unexpected `[Effect] ... duration ... ended`
   or `[Scope] ... removed externally` during a hold.
2. If the pose snaps back while the log still shows an active scoped state, include whether
   the client emitted `UseScope InScope=0` **before you released the button**. Capture the
   client-side diagnostic log or inbound controller updates too if available. This separates
   client cancellation from a server expiry or mismatched replicated state.
3. **Pad:** step onto it once. Inspect `[Glider] ForcePush`, the permission changes, profile
   changes and `[Effect]` expiry ages. New effects must have a fresh lifetime at each handoff;
   profile 18 must remain active after the 9495-to-3417 transition. Confirm actual airborne/
   gliding movement, not just a wing animation.
4. The `[Glider] Launch handoff` line after each push tells which side failed: airborne /
   negative air time means the launch reached the client and the chain must survive it
   (regression); a standing / positive air time handoff while still on the pad means the
   client never started the forced movement, and the retrigger cadence (was ~2 s) plus the
   post-handoff `[Effect]` ages are the numbers to report.
4. Land, then reuse the pad. Permissions and profile must reset on landing, and one launch
   must not leave the next launch disabled or continuously retrigger while still active.

## Regression tests

```sh
dotnet test UdpHosts/GameServer.Tests/GameServer.Tests.csproj -c Release
```

- `EffectLifecycleTests`: future/uint-wrap timestamps, separate prediction/lifetime
  clocks, the timed 3419 → 3418/9495 → 3417 graph through landing, failed removal tails,
  reverse-order cleanup, slot reuse, update-created effects and optional payload inheritance.
- `ScopedStateTests`: real `UseScope`/weapon/fire-mode handlers, a sustained scope effect,
  replicated controller fields, scope-out, death/external cleanup, stale packets and zero
  timestamps at clock wrap.
- Existing `PermissionAndGliderProfileCommandTests`, `CombatFlagsCommandTests`,
  `RequirementServerCommandTests`, `RegisterMovementEffectCommandTests`,
  `ProximityAbilityRetriggerTests` and `ChannelReliableTests` cover the related components.

The new tests inject small effect graphs through `FakeAptitudeFactory` rather than altering
static SDB dictionaries or requiring a local Firefall installation. They do not emulate the
client's animation/physics engine.
