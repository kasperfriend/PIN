# Health System Dev Notes

This document explains how character health, damage, healing, death and fall
damage work on the PIN GameServer, and how to test all of it without enemies.

---

## 1. The pipeline

```
Damage source (projectile / fall damage / admin command)
  -> DamageSystem.ApplyDamage          (Systems/Combat/DamageSystem.cs)
       1. refuses targets that are not in the Living state
       2. shields absorb first, then health
       3. publishes EntityDamagedEvent
  -> CharacterLifecycleService         (Systems/CharacterLifecycle/)
       EntityDamagedEvent with health <= 0 transitions a Living character into
       Bleedout (if allowed) or Dead, and publishes
       CharacterEnteredBleedoutEvent / CharacterDiedEvent
  -> PlayerRespawnService              (Systems/PlayerRespawn/)
       Dead players are auto respawned after DeadDurationMs (2.5s by default)
       through NetworkPlayer.Respawn
  -> NpcDeathService                   (Systems/NpcDeath/)
       Dead NPCs get gib visuals and a corpse linger lifetime
```

`DamageSystem.ApplyHeal` works the same way in reverse and is refused for
characters that are not `Living`. Healing does not revive downed characters,
use the revive command (see below).

### Vitals sync to the client

`CharacterEntity.SetCurrentHealth` / `SetCurrentShields` / `SetMaxHealth` /
`SetMaxShields` are the only ways to change vitals. They update the server
side value **and** mark the replicated `BaseController`/`ObserverView` props
dirty. `EntityManager.FlushChanges` pushes them to the owning client (reliable
GSS) and to scoped players (unreliable GSS) every ~5ms.

> Never write `baseController.CurrentHealthProp` directly: that only changes
> what the client sees and desyncs the server state. This was the reason the
> first hit after a respawn used to instantly down the character again.

### Lifecycle states

`CharacterStateData.CharacterStatus` (replicated) and the internal
`CharacterLifecycleState` (server only) move together:

| Server state | Replicated status | Damage? | Heal? | Notes                          |
|--------------|-------------------|---------|-------|--------------------------------|
| Living       | `Living`          | yes     | yes   | normal gameplay                |
| Bleedout     | `Incapacitated`   | no      | no    | drains 100 HP/s, 15s duration  |
| Dead         | `Dead`            | no      | no    | auto respawn after 2.5s        |

---

## 2. Fall damage

Movement in Firefall is client authoritative, so fall damage is detected from
the movement inputs the client streams to the server:

```
MovementInput packet
  -> BaseController.MovementInput      (alive gate)
    -> MovementRelay.CharacterMovementInput
      -> FallDamageSystem.OnMovementInput
```

`FallDamageSystem` (Systems/Combat/FallDamageSystem.cs) tracks, per player
character:

- the **fastest downward speed** (`-Velocity.Z`) seen while airborne,
- the **total air time** (from `GroundTimePositiveAirTimeNegative`),
- whether **thrusters/gliders** were used during the fall,
- whether the fall was a **knockdown** fall.

When the first grounded movestate arrives, that state is evaluated by the pure
`FallDamageMath.Evaluate` (Systems/Combat/FallDamageMath.cs):

- Impact speed <= `SafeImpactSpeed` (12 u/s, ~9u drop at gravity -8): nothing.
- Impact speed >= `LethalImpactSpeed` (48 u/s): exactly lethal damage.
- Otherwise: `(impactSpeed - safe) * DamagePerSpeed (130)` damage, scaled by
  the character's `DamageTaken` stat modifier.
- Falls shorter than `MinAirTimeMs` (250ms) never deal damage.
- Falls are **negated** by water landings, thruster/glider use during the
  fall, knockdown falls and the `immune_falldamage` combat flag.

All tuning lives in `StandardFallDamageRules`; the thresholds are in world
units/second like the physics simulation (gravity is `(0, 0, -8)`).

Safety rails so teleports, respawns and vehicles cannot fake landings:

- A landing is only trusted within `LandingGraceMs` (150ms) of the last
  airborne sample.
- `Respawn` and the `tp`/`teleport` command reset the tracker
  (`FallDamageSystem.ResetFor`).
- Samples with the `Occupant` movestate (in a vehicle) are ignored entirely.
- Falling damage lands in the normal `DamageSystem` pipeline, so it hits
  shields first, can trigger bleedout/death and sends a `TookHit` feedback
  message to nearby clients plus a debug chat line to the victim.

---

## 3. Testing without enemies

All of this works with zero NPCs in the zone. Type these into the in-game
chat with a `\` prefix:

| Command              | Effect                                                        |
|----------------------|---------------------------------------------------------------|
| `\health`            | Print current health/shields/lifecycle state                  |
| `\hurt <amount>`     | Take `<amount>` damage (shields first)                        |
| `\heal <amount>`     | Heal `<amount>`                                               |
| `\fall <speed>`      | Simulate a landing at `<speed>` u/s (e.g. `\fall 30`)         |
| `\down`              | Enter bleedout                                                |
| `\revive`            | Revive from bleedout (restores 25% health)                    |
| `\kill`              | Die instantly                                                 |
| `\respawn`           | Force a respawn at the nearest outpost                        |

The same commands exist on the Admin chat channel as server commands
(`hurtme`, `healme`, `downme`, `killme`, `revive`, `respawn`, `tp`).

Suggested smoke test:

1. `\health` — should show `19192/19192`.
2. `\hurt 5000` — health bar drops, shields stay 0.
3. `\heal 5000` — health bar back to full.
4. `\fall 30` — `2340` fall damage; `\health` confirms; `\heal 99999`.
5. `\fall 60` — lethal: bleedout screen, then auto respawn at an outpost.
6. `\health` right after the respawn — must show full health again (the
   respawn vitals reset).
7. Jump off a tall cliff for the real thing: small drops do nothing, big
   drops hurt, a jetpack tap mid fall negates the damage.

`FallDamageMath`, `FallDamageSystem`, `DamageSystem`,
`CharacterLifecycleService` and the `CharacterEntity` vital clamping are all
covered by unit tests in `UdpHosts/GameServer.Tests` (`dotnet test`).
