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
**launch-pending** (`MarkServerLaunchPending`): a waiting marker plus gate grace that runs
to push time + 550 ms (a server-side wait for the client's first post-impulse pose) +
1500 ms handoff margin. While the window is open:

- `AirborneDuration` counts the character as airborne;
- `RequireMovestate` answers gliding/falling/… from the *pending launch*, not the stale
  ground pose.

The server deliberately does **not** seed the movement state nibble to glider (`0x7000`)
at push time. The client has not applied the impulse yet, so pretending the character is
gliding server-side would contradict the only client-facing evidence. The gate grace reads
the pending marker instead; anything that reads the reported movement state directly keeps
the client's actual pre-launch pose until the client reports the launch.

The most important use of the marker is **holding the authoring pose confirm**. The client
receives the `ForcedMovement` Type 5 impulse asynchronously; its first post-push
`MovementInput` can still be grounded because the impulse has not taken effect yet.
`MovementRelay` must not confirm that grounded pose back over `UnreliableGss` in the same
tick, or the client treats the grounded pose as authoritative and drops the pending launch
— the observed `[Glider] Launch handoff ... MoveState=4096 AirTime=32767 Airborne=False
VelocityZ=0` failure. While a launch is pending *and* the forced window is active *and* the
reported pose is still grounded, the authoring client's `ConfirmedPoseUpdate` is held back;
remote clients still get `CurrentPoseUpdate` and every client still gets `JumpActioned`.
Confirmation resumes as soon as the client reports airborne, or at the end of the forced
window if it never left the pad.

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

Type 5 is a one-frame velocity impulse on the shared epoch-ms clock: `Time1 = now+19`,
`Time2 = now+20` and `ShortTime = CurrentShortTime`, matching upstream PIN and the live
client (the 2016 capture's pad launches carry `Unk1=0`, `HaveUnk2=0`, a 12-byte velocity
vector and a `ShortTime` that tracks the current 16-bit clock, not the low half of
`Time1`). A previous 50–550 ms hold was a misdiagnosis — field logs then showed
`Launch handoff ... MoveState=4096 Airborne=False VelocityZ=0` (animation played, the
player never left the pad). Stamping the packet with `Time1 = now-25` (an earlier attempt
at a rewind-friendly impulse) has the same effect: the `ShortTime` also gets the old low
16 bits and the client drops the packet as stale, so it never leaves the pad. The
`[Glider] ForcePush` log includes target, strength, velocity and the window; it records
what the server sent and does not prove the client acted on it. Do not mask a failed
launch by disabling fall damage or granting gliding permanently.

## ADS state

Protocol mapping, confirmed against captures of the live game (see
[What the live servers send on the wire](#what-the-live-servers-send-on-the-wire)):

| State | Field / path |
|---|---|
| Active fire mode of the weapon in the character's hands | `FireMode_0` — written by `SelectFireMode` **and** by `UseScope` (the sights are the weapon's secondary fire mode) |
| Scoped mirror | `FireMode_1` — written by `UseScope` only, so it is the unambiguous "is scoped" flag for the aptitude side |
| Main vs. underbarrel weapon the server simulates | the mode the player selected with `SelectFireMode`, remembered separately (`CharacterEntity._selectedFireMode`) |
| Scope effect | `dbitems::WeaponScope.Statusfx` via the active weapon details |

`SetScopedState` owns both halves: the fire mode *and* the effect. Scope-out, weapon/fire-mode
switches, loadout changes, death and external effect removal cannot leave one half active.
Repeated scope-in requests do not stack/restart the effect, and stale `UseScope` messages
are ignored with a wrap-aware timestamp comparison. Scoping in never selects the underbarrel:
the replicated `FireMode_0` carries the sights, but the weapon the server simulates keeps
following the player's own `SelectFireMode` selection, and scoping out hands the field back
to that selection.

### What the live servers send on the wire

`themeldingwars/Documentation` ships three packet captures of the real game
(`Captures/*.pcapng.gz`). Decoding them is mechanical:

* UDP payload → 4-byte game socket header → datagrams of `[2-byte header: channel, resend,
  split, length]`; GSS channels are `[2-byte sequence][1-byte typecode][7-byte entity id]
  [1-byte message id][body]`.
* View/controller **update** (message id 1) bodies are `[field index][value]…`, with
  `index + 128` meaning "clear that field". Field indices are the declaration order of the
  Aero class, and **the field list changed between protocol versions** — see below.
* Keyframes (message id 4) are `[8-byte player id][nullables bitfields][every field]`, so a
  keyframe pins the layout of the build the capture was taken with.

| Capture | GSS version | CombatController layout (from its keyframe) |
|---|---|---|
| 2014-09-19 (build 1802) | 883 (`V45`) | 32 change times, 32 status effects, **15** stat multipliers, `FireMode_0` = 79, `FireMode_1` = 80 |
| 2015-05-02 (build 1869) | 17122 (`V63`) | same with **16** multipliers, `FireMode_0` = 80, `FireMode_1` = 81 |
| 2016-11-15 | 19551 (`V74`, prod-1962 — what PIN targets) | same with **17** multipliers, `FireMode_0` = 81, `FireMode_1` = 82 |

Only the 2014 capture contains a player using the sights (19 `UseScope` messages, alternating
`InScope=1`/`0`). The server's answer to `UseScope InScope=1` at client time `T`, about
120 ms later:

```
CombatController update: FireMode_0 = { Mode = 1, Time = T }        <- field 79 in that build
CombatController update: StatusEffects_5 = { Id = <scope statusfx>, Stack = 1,
                                             Initiator = <the player>, Time = T,
                                             MoreDataFlag = 0 }, change time = T & 0xFFFF
PublicCombatLog:         [SourceType = Weapon, StatusFxApplied, <scope statusfx>, T]
```

and on `UseScope InScope=0`, ~12 ms later: the status effect slot is cleared and the fire mode
is rewritten as `{ Mode = 0, Time = T }`. `FireMode_1` is **never** written in any of the
captures; `SelectFireMode` (which the 2015 and 2016 players used instead of `UseScope`) writes
`FireMode_0` = 80 / 81 in those builds — the very same field.

That is why the scope is now replicated into `FireMode_0`: the client raises the sights by
putting its weapon into its secondary fire mode and predicting that change, and the server has
to confirm **that** field. PIN used to answer with `FireMode_1` only, which left the client's
own predicted fire mode contradicted by the server — it dropped the sights again about a
second later and sent `UseScope InScope=0` by itself, which is exactly what the field logs of
the "ADS still not working" report show (scope in, then a self-initiated scope-out 0.7–1.0 s
later, over and over, with no server-side removal in between).

### Divergences that are still open

These are differences from the live captures that are *not* implemented yet. They are the next
things to try if the sights still drop in game, in this order:

1. ~~**Combat log rows.**~~ **Addressed.** The live server sends `PublicCombatLog` (and
   `PrivateCombatLog`) rows for every status effect it applies to or removes from a character —
   the scope effect included — the effect id and the same event time it writes into the status
   effect slot, with `SourceType = Weapon` for the scope effect. PIN sent none, so the client's
   predicted scope effect stayed unconfirmed and the `RequireServerConfirmed` row in its own
   duration chain (`tfRequireServerConfirmed`, effect 1313 chain 1605146) cut it roughly a second
   after the sights came up — the observed ADS flicker while RMB is held. The server now emits
   both rows to the owner for every character statusfx apply/remove, right after the force
   flush (`BaseAptitudeEntity.OnStatusEffectReplicated`/`OnStatusEffectCleared` →
   `CharacterEntity` → `GameServer.Systems.CombatLog.CombatLogSink`); `Bytes` is the byte count
   of the concatenated rows (10 bytes per statusfx row). The live server batches rows ~150 ms
   after the fact and also carries the public log on the ObserverView route; both refinements
   are unverified niceties, not sent yet.
2. ~~**Local-effects controller.**~~ **Addressed.** The 2016 capture writes
   `LocalEffectsController` entries only for effects whose initiator is *another* entity
   (always a foreign entity id, never the player's own). PIN mirrored every status effect
   into the owner's local-effects controller, self-applied ones included — so the scope
   effect, which the client predicts itself the instant RMB goes down and is self-initiated,
   arrived a second time as a server-owned instance of the same aim effect. The client
   resolves that conflict by discarding its prediction and lowering the sights, then reports
   it with a `UseScope InScope=0` the player never sent, ~1 s into the hold. That is exactly
   the signature of the latest field log: scope in at 14:10:41, self-initiated scope out at
   14:10:42, repeatedly, with no `[Effect] 1313 duration ... ended` and no
   `[Scope] ... removed externally` in between — the server never took the effect away.
   `CharacterEntity.SetStatusEffect` now writes a local-effects slot only when the status
   effect's initiator is a different entity, and the clear only touches slots that actually
   hold one, so a purely predicted effect is never invalidated by a slot the client never
   received.

Both are client-observable, so the in-game checks at the end remain the arbiter.

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
   The log should show `FireMode_0=1 FireMode_1=1` with effect 102/1313 while held, and both
   fire modes (and the effect) at zero after scope-out or a switch. There should be no
   unexpected `[Effect] ... duration ... ended` or `[Scope] ... removed externally` during a
   hold, and — the actual regression — no `UseScope InScope=0` that you did not cause: a
   scope-out that arrives while you are still holding the button means the client still
   disagrees with the replicated state. Every `Character.SetStatusEffect`/`ClearStatusEffect`
   while holding (one pair per raise/lower) is now followed by a `PublicCombatLog`/
   `PrivateCombatLog` echo to the owner confirming the same effect id and time; if the sights
   still drop despite those rows reaching the client, a packet capture is the way to tell
   whether the row ever arrives.
2. If the pose snaps back while the log still shows an active scoped state, include whether
   the client emitted `UseScope InScope=0` **before you released the button**. Capture the
   client-side diagnostic log or inbound controller updates too if available. This separates
   client cancellation from a server expiry or mismatched replicated state.
3. **Pad:** step onto it once. Inspect `[Glider] ForcePush`, the permission changes, profile
   changes and `[Effect]` expiry ages. New effects must have a fresh lifetime at each handoff;
   profile 18 must remain active after the 9495-to-3417 transition. Confirm actual airborne/
   gliding movement, not just a wing animation.
4. The `[Glider] Held authoring pose confirm` line is expected at most once while the
   client is between receiving the impulse and reporting the launch; it shows the server is
   not telling the client its grounded pose is authoritative during that window. The
   `[Glider] Launch handoff` line after each push tells which side failed: airborne /
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
  timestamps at clock wrap, the combat log confirmation rows, and the local-effects slots
  (self-applied effects stay out of them, foreign-initiated ones are mirrored and cleared).
- Existing `PermissionAndGliderProfileCommandTests`, `CombatFlagsCommandTests`,
  `RequirementServerCommandTests`, `RegisterMovementEffectCommandTests`,
  `ProximityAbilityRetriggerTests` and `ChannelReliableTests` cover the related components.

The new tests inject small effect graphs through `FakeAptitudeFactory` rather than altering
static SDB dictionaries or requiring a local Firefall installation. They do not emulate the
client's animation/physics engine.
