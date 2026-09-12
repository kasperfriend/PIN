# NPC AI Dev Notes

This document explains the server side NPC AI: what a spawned mob actually does,
where the code lives, how to tune it and how to exercise it from the game client.

It replaces the old `AIEngine` stub, which was an empty `Tick`. Spawned mobs now
pick a target, walk towards it, swing at it once they are within reach of it, and
give up when they are dragged too far from where they spawned.

> **NPC attacks come from the database.** Each mob fights with the weapon its
> `dbcharacter::Monster` row names: the `dbitems::Weapons` item, its
> `WeaponTemplates` mode, its `AttributeRange` damage/range rows and, for ranged
> rows, its `dbitems::Ammo` projectile - fired through the same `ProjectileSim`
> a player shot uses. Mob melee is still a direct damage call, but only the rows
> the data says are melee. See
> [What an attack actually does](#3-what-an-attack-actually-does) and
> [Weapon attacks come from the database](#weapon-attacks-come-from-the-database).

> **Mobs animate their attacks.** Each attack is marked in the combat view with the
> weapon's own burst timing, and the walk/run states come from the monster's two
> speed columns - see
> [What an NPC animates](#what-an-npc-animates).

> Character origins sit at the feet (the muzzle offset is `(0.2, 0, 1.62)` in
> `CalculateProjectileOrigin`, i.e. chest height above the origin) and the body
> orientation is a yaw-only rotation about world +Z, confirmed against the
> spawn points in `StaticDB/CustomData/outpost.json`. Ground snapping is **on by
> default** - see [Tuning](#5-tuning).

---

## 1. Where the code lives

```
UdpHosts/GameServer/Systems/Ai/
├── AiEngine.cs                 shard integration: targets, movement, attacks, events
├── AiBrain.cs                  the state machine (no shard/entity/physics dependency)
├── AiBrainState.cs             Idle / Chase / Attack / Return / Dead
├── AiPerception.cs             what the engine tells a brain about the world
├── AiDecision.cs               what a brain tells the engine to do
├── IAiRules.cs                 tunables
├── StandardAiRules.cs          the shipped defaults
├── AiCombatTuning.cs           per-NPC attack reach/cadence overrides from its weapon
├── AiSpeeds.cs                 monster row speed -> metres per second
├── AiAttackDamage.cs           monster damage rating -> what one swing is worth (fallback)
├── AiVectors.cs                horizontal / straight-line distance + character facing maths
├── NpcAttackProfile.cs         everything one NPC attack needs, resolved from the DB
├── NpcAttackResolver.cs        monster -> weapon -> template/attributes/ammo -> profile
├── NpcAttackDamageMath.cs      the pure damage + cadence rules
├── NpcAttackAnimation.cs       the attack animation window (burst markers)
├── NpcBehaviorParams.cs        parser for the behaviour string's attack parameters
├── NpcProjectileLauncher.cs    fires a resolved shot through ProjectileSim
├── INpcAttackDataSource.cs     the DB surface the resolver reads (fakeable in tests)
├── IAiHostility.cs             who counts as an enemy
├── FactionAiHostility.cs       SDB faction table implementation
├── IAiMonsterStats.cs          where movement speeds, the damage rating and the weapon profile come from
├── SdbAiMonsterStats.cs        reads dbcharacter::Monster + MonsterScaling + NpcAttackResolver
├── IAiAttackFeedback.cs        cosmetic hit messages
└── HitFeedbackAttackFeedback.cs routes them through CombatSim.HitFeedback
```

`Shard` owns the engine as `Shard.AI` and ticks it first in `Shard.Tick`, before
`Physics` and `EntityManager`. Every character that goes through
`EntityManager.SpawnCharacter` is registered automatically, and
`EntityManager.Remove` unregisters it. Player controlled characters are refused by
`AiEngine.Register`.

The split matters for testing: `AiBrain` is pure decision logic fed an
`AiPerception` snapshot, so the whole state machine is covered by unit tests in
`GameServer.Tests/AiBrainTests.cs` with no shard, entity or physics involved.
`AiEngineTests.cs` drives the real engine against the in-memory `FakeShard`.

---

## 2. The state machine

```
                target inside aggro radius
        ┌─────────────────────────────────────────────┐
        │                                             ▼
     ┌──────┐  dragged past leash  ┌────────┐     ┌───────┐
     │ Idle │ ───────────────────► │ Return │     │ Chase │
     └──────┘                      └────────┘     └───────┘
        ▲                             │  arrived      │  ▲
        │  target lost / died         │  home         │  │ out of range
        └─────────────────────────────┴───────────────┘  │ or no line of sight
                                            ▼            │
                                        ┌────────┐       │
                                        │ Attack │ ──────┘
                                        └────────┘
```

* **Idle** - no target. The NPC stands where it was spawned.
* **Chase** - a hostile player was acquired; the NPC walks towards them until it is
  inside `StandoffRange`.
* **Attack** - the target is inside the NPC's **attack volume** **and** visible:
  `AttackRange` measured straight-line (including the height difference) and
  `MaxAttackHeightDelta` of vertical slack. The NPC faces the target, closes to
  `StandoffRange` and swings every `AttackCooldownMs`.
* **Return** - the NPC was pulled more than `LeashRadius` from its spawn point. It
  drops its target and walks home, then goes back to Idle.
* **Dead** - terminal. Set from the `CharacterDiedEvent`.

Details worth knowing:

* **Chasing is flat, attacking is not.** Every movement decision (and the leash)
  uses the horizontal distance, because the mob walks over terrain and cannot fly.
  The attack gate uses the 3D distance plus an explicit height band, because
  `AiVectors.HorizontalDistance` alone makes a player standing on the crate, the
  ledge or the floor *above* a mob "0.5 m away" - which is how a mob ended up
  hitting a player through the ground it was standing on.
* **Attacks are per weapon.** The reach and the mode come from the mob's own
  `dbcharacter::Monster` weapon row (`AiCombatTuning.FromProfile`): a mob melee
  row swings (`range` 2.6 m for "NPC Melee Medium (Spyder)") and a ranged row
  fires its `dbitems::Ammo` projectile at the weapon's `range` - the 3.5 m
  rules reach is only what a mob with no resolvable weapon uses. The height band
  widens to the weapon's own range for a ranged weapon, so a rifle can shoot a
  target standing above the mob where a punch could not.
* **Entry and exit ranges differ.** A target is acquired at the NPC's
  `AttackRange` but only released at its `AttackRangeExit` (3.5 m in, 5 m out for
  a weaponless mob; a ranged weapon's is its range and range × 1.15). Without that
  hysteresis a player walking along the edge of the range flips the state every
  tick.
* **Giving up is time based.** Losing line of sight does not drop the target
  immediately - the NPC keeps hunting for `TargetLostTimeoutMs` and only then
  forgets. Dying or despawning drops it at once.
* **Re-engaging needs a sighting.** Acquiring a target out of Idle requires line of
  sight, not merely a live target in range. Without that, the give-up path above
  would drop the target and the acquisition step would re-adopt it in the very same
  decision, so a mob would forget and rediscover a player standing behind cover
  every `TargetLostTimeoutMs` instead of ever giving up. Getting shot bypasses this
  (`Aggro` moves straight to Chase), so you cannot hide from a mob you just hit.

### Aggro triggers

1. **Proximity.** Every `PerceptionIntervalMs` the engine scans the connected
   clients for the closest hostile, alive player character inside `AggroRadius`
   (an already engaged NPC scans `AggroRadius * 1.5` so it does not drop a target
   that just stepped out of the ring). The scan is a squashed sphere, not a
   cylinder: a candidate further than `MaxAcquisitionHeightDelta` above or below
   the NPC is skipped, so a mob on the ground does not lock onto a player standing
   on the roof over it and then stand there with nothing to do.
2. **Being shot.** `AiEngine` subscribes to `EntityDamagedEvent` and forces the
   damaged NPC onto its attacker regardless of distance. This is why you cannot
   snipe a mob from across the map for free: it will come, and a mob with a ranged
   weapon will start shooting back as soon as you are inside that weapon's range -
   only the melee rows still have to walk all the way to you.

The target is sticky: as long as the current target entity still exists the scan
is skipped, so a mob does not ping pong between two players standing side by side.

### Hostility

`FactionAiHostility` uses the same policy `CombatSim` applies to projectile hits:
only an explicit `Friendly` or `Self` stance is non attackable. Neutral and
unresolved faction pairs are treated as hostile on purpose - SDB faction relation
coverage is incomplete for some monster factions, and the alternative is whole
monster types that never fight back. Accord Assault (290) is Friendly to the
default player faction and will not aggro.

---

## 3. What an attack actually does

```
AiEngine.UpdateBrain
  -> AiPerception          flat distance (chasing) + straight-line distance and
                          height delta (attacking), line of sight
  -> AiBrain.Decide        Idle / Chase / Attack / Return / Dead
       Attack gate: AttackDistance <= the NPC's AttackRange (its weapon's,
                    or the rules 3.5 m when it has none)
                   and HeightDeltaToTarget <= MaxAttackHeightDelta
                    (2.5 m, or the weapon's own range for a ranged weapon)
  -> AiEngine.ResolveAttack
       melee  -> IShard.Damage.ApplyDamage(target, perRoundDamage, npcEntity)
       ranged -> IAiProjectileLauncher.FireRangedAttack(  (ShardAiProjectileLauncher)
                    npc, PRNG trace, muzzle origin, aim direction,
                    ammo, range, speed, impactRadius, maxRadius, damage)
                    -> ProjectileSim.FireProjectile  (one call per round)
                         -> flight, gravity, bounces, falloff, impact
                              -> ProjectileHitEvent -> DamageSystem.ApplyDamage
       -> IAiAttackFeedback.OnAttack                       (melee only)
            -> CombatSim.HitFeedback.TookDebugHit -> TookHit to scoped clients
```

Two things gate the attack itself, both of them in the brain so they are covered
by `AiBrainTests` without a shard:

* **Reach.** The attack is measured over the straight-line distance
  (`AiVectors.Distance`), not the flat one chasing is planned on. A mob 3 m under
  a player is not "3 m away in range", it is 3 m of air away from a swing.
* **Height band.** `MaxAttackHeightDelta` is the vertical slack of the attack. It
  is a separate test on purpose: the 3.5 m sphere already covers most cases, but a
  player standing directly over the mob's head is 1 m away in every horizontal
  measure, and a melee hit that lands there reads as "the mob hit me through the
  floor". A ranged weapon keeps the band at its own range instead, because a
  bullet does not care about the floor between the two of you.

A melee attack is a hitscan: no projectile, no spread, and nothing to dodge while
you are inside that reach; the victim gets the same `TookHit` message a weapon hit
produces, so damage numbers and the health bar behave normally. A ranged attack is
a real projectile carrying the per-round damage the database resolved, so it can
miss, be dodged, fall off with distance (the ammo's `damage_decay`) and use the
ammo's own gravity and bounce behaviour. Neither mode crits or counts as a
headshot.

Movement is applied by writing the entity position/orientation, pushing the new
pose into the physics body with `PhysicsEngine.UpdateEntity` (so the mob stays
hittable where it now stands) and broadcasting a
`AeroMessages.GSS.Character.Event.CurrentPoseUpdate` on the unreliable GSS channel
- the same message `MovementRelay` uses to show one player's movement to another.

Before stepping, a short forward ray cast checks for a wall; if the way is
blocked the NPC holds position instead of walking through it. With no collision
data loaded (`LoadMapsCollision` off) nothing can block and nothing occludes, so
every target counts as visible.

### What an NPC animates

A mob's animation is not a server-side asset choice - the client owns the animation
graphs and picks them from replicated state. Five pieces of that state are the
server's job, and all five are now driven by the database:

| What | Replicated where | Where the value comes from |
|---|---|---|
| Attack / fire | `CombatView.WeaponBurstFired`, then `WeaponBurstEnded` (or `WeaponBurstCancelled`) | `dbitems::WeaponTemplates.ms_burst_duration` (else `ms_per_burst`), clamped to the NPC's own attack cadence |
| Reload | `CombatView.WeaponReloaded` (then the magazine refills, or `WeaponReloadCancelled` when it dies mid-reload) | `dbitems::WeaponTemplates.base_clip_size` (else the item's attribute 956), `ammo_per_burst` (else `rounds_per_burst`) and `reload_time`; the client plays the template's `anim_reload_type` |
| Locomotion | `CurrentPoseUpdate` / `MovementView` movement state | `dbcharacter::Monster.normal_speed` (the walk, `0x5004`, while attacking) and `fast_speed` (the run, `0x2004`, while chasing) |
| Armed pose + weapon animation set | `CurrentEquipment` (the weapon item's template id) | `dbitems::WeaponTemplates.anim_armed_id` / `anim_armed_priority` / `anim_fire_type` / `anim_reload_type` / `anim_charge_type` |
| Charge / swing animation (a named animation from a chain) | the character's `StatusEffects_0..31` fields (`EffectApply` / `EffectRemove`), which the client plays the effect's own `tf*` commands from | `dbitems::WeaponTemplates.attack_ability_id` / `burst_ability_id` -> `apt::AbilityData.chain` -> `apt::ImpactApplyEffectCommandDef.effect_id` -> `apt::StatusEffectData.apply_chain` / `remove_chain` |

**Attack animation.** A player's is driven by the client itself: it sends
`FireBurst` when a burst starts and `FireEnd`/`FireCancel` when it stops, and the
server only relays those three times to everyone else, who play the animation of
the weapon the replicated equipment says the character holds. An NPC has no client
to send them, which is why mobs used to stand still while hitting you. `AiEngine`
now produces the same three markers: `FireBurst` at the attack, `FireEnd` at the
weapon's own burst timing, and `FireCancel` if the mob dies in the middle of one
(the client then drops the attack pose for the death). The window is the
template's `ms_burst_duration` (the burst is fired over time) else `ms_per_burst`
(the fire cycle), clamped so an animation can never outlive the attack cycle that
started it - a rifle's 100 ms volley inside a 2,500 ms behaviour cycle, a melee
Spyder's 1,600 ms swing inside its 2,000 ms one. A monster the database gives no
weapon at all swings with a documented 500 ms default (the only animation length
here that is not a row), so weaponless mobs animate too.

**Reload animation.** A player's reload is client driven too - the client sends
`ReloadWeapon` (or `CancelReload` to abort), the server relays the two times and
every watching client plays the reload of the weapon the replicated equipment says
the character holds. An NPC has no client to send them, which is why mobs used to
fire forever without ever reloading; `AiEngine` now keeps the weapon's magazine
itself (`NpcWeaponMagazine`, pure and unit tested) and produces the same markers:
`WeaponReloaded` at the moment the magazine runs dry, no fire for the template's
`reload_time` (that window is the reload animation), and `WeaponReloadCancelled` if
the mob dies in the middle of one. The magazine is the weapon item's attribute 956
(`WeaponMagazineSize`) when the item carries it, else the template's
`base_clip_size`; one attack spends `ammo_per_burst` when the template carries it,
else `rounds_per_burst` (a shotgun's 16 pellets are one shell, not 16 reloads). The
reload starts as soon as the burst empties the magazine, so `reload_time` overlaps
the rest of the weapon's cadence instead of stacking on top of the next shot, and a
shot the reload does block goes out as soon as the magazine is full. An attack wants
a whole burst's worth of rounds, so a magazine that cannot pay for one waits for the
reload even if it is not literally empty - which the build's data makes reachable
once (`NPC Juggernaut Beam Cannon`: 110 rounds a burst out of a 65,535 round clip). The marker is
also what the aptitude `RequireReload` condition tests
(`RequireReloadCommand`: `CombatView.WeaponReloaded > InitTime`), so a mob's own
chains can satisfy a reload check the way a player's do.

80 of the 85 templates the build's monsters use reload, covering 1,510 of the 1,861
monster weapon slots - and 747 of those slots are on templates that carry an
`anim_reload_type` for the client to play. That includes the single-round weapons,
which the database means to reload after every shot: the NPC Charge Sniper Rifle's
`base_clip_size` is 1 with a 1,000 ms `reload_time` (39 monster slots), and the NPC
Ranged Default row is the same shape (37). The five that do not reload are the rows
that are not projectile weapons: the Spyder's melee clip (`reload_time` 0, 334
monster slots) and four rows whose `default_ammo_id` resolves to no
`dbitems::Ammo` row at all (visual-only and trigger-only templates). A weapon the
database gives no magazine fires forever, exactly as it did before.

**Chain animations (the charge and the swing).** The database's *named* animations
live in aptitude chains: a `tfPlayAnimationCommandDef` ("MeleeAttack", "Shoot",
"roar", ...) or a `tfAbilityAnimationCommandDef` (an animation-graph state) is a node
in a chain, and the chain that holds it belongs to a status effect, so applying the
effect is what makes every client with the same database play it. Those commands are
client-side by design - `Factory.LoadCommand` turns them into `CustomNOOPCommand`,
because the client runs them from the replicated effect - which means the server's
whole job is to apply the effect. Nothing did: a weapon template's
`attack_ability_id` / `burst_ability_id` reached `NpcAttackProfile` and stopped
there, so the 2 mobs and 14 mobs whose weapons carry the build's only two animating
chains never applied their effects. `NpcWeaponAbilities.Scan` now walks those chains
once, at profile resolution (through `INpcAttackDataSource`, so it is unit tested
without a database), and reports two things: whether an animation is reachable
(`ChainAnimates`) and whether the chain delivers the hit itself
(`ChainDeliversDamage`). `AiEngine` runs the ability for the weapons that animate,
through the shard's own `AbilitySystem.HandleActivateAbility`.

The two weapons, command by command:

* **Melee - Shadowstrike** (`burst_ability_id` 188, 2 monster slots). The chain
  targets a cone in front of the mob and the hostiles inside it, applies effect 270
  (its update chain inflicts 100 damage over a 5 m splash, and it stacks to 3),
  plays a particle and a 100 damage `InflictDamage`, then applies effect 176 to
  itself: a `tfPlayAnimationCommandDef` with the animation `MeleeAttack` for a
  900 ms `TimeDuration` (`RequireCState` keeps it alive only while the mob lives).
  The swing you see is the replicated effect; the damage is the chain's own.
* **NPC Charge Up and Channel Fire** (`attack_ability_id` 39249, 14 monster slots).
  The chain applies effect 10496 to itself and then runs
  `ReplenishEffectDurationCommandDef`: the charge effect is a `tfAbilityAnimationCommandDef`
  (the charge pose) plus the charge audio plus `restrict_movement` /
  `restrict_abilities` / `restrict_melee`, and its duration chain is
  `RequireCState` + `ReplenishableDurationCommandDef` - so the charge lasts as long
  as the value the activation hands it. When it runs out, the removal chain is the
  shot: the release animation, a `FireProjectileCommandDef` (ammo 1036, 100 damage,
  100 m, hardpoint 2) and the firing audio.

Three details make that work, and each is a place the data forced a decision:

* **The charge time is the register.** A weapon with `ms_chargeup` (2,000 ms for the
  charge-up weapon, 0 for the Shadowstrike) hands the chain its charge time as the
  ability activation's register, in seconds - the unit `ReplenishableDurationCommand`
  reads (a value below 1000 is seconds, and the monster trees have charge times of
  750, 2,000, 2,500 and 4,000 ms, so milliseconds would be read as seconds on the
  short ones). The charge effect therefore lasts exactly as long as the weapon
  describes, and the shot leaves at the end of it. A weapon with no charge time
  passes no register and every duration command falls back to its own default.
* **`ReplenishEffectDuration` is what carries it into the effect.**
  `aptgss::ReplenishEffectDurationCommandDef` (type 294) was a placeholder that
  always returned true, and its definition table is server-only (absent from
  `clientdb.sd2`), so the build's copy of the command is a reconstruction from the
  chains that use it: it hands the activation's register to the effects the
  activation applies - recording it on the context for the ones still to come
  (consumed by `AbilitySystem.DoApplyEffect`) and writing it into the contexts of the
  ones already applied. The database has both orders: `39249` and the sniper's
  `34894` run it after the apply, the Phason Thrower's `40266` runs it before. Without
  it, `ImpactApplyEffect.pass_register` is 0 on every monster weapon row, so the
  applied effect's context is copied with no register and its replenishable duration
  would fall back to the command's 30 s default instead of the charge time.
* **A chain that delivers the hit replaces the AI's own.** For the two animating
  weapons the chain is the attack as well - a cone swing that inflicts its own
  damage, a charge that ends in its own projectile - so the engine does not add the
  melee hit or the volley it would otherwise fire on top of it (`ChainDeliversDamage`
  is what says so). If the activation does not run (no aptitude system, an unknown
  ability, a chain requirement that rejects it), the engine falls back to its own
  attack, so a data or runtime gap cannot leave a mob harmless. Every other weapon
  keeps the AI attack it had: the chains of the other 12 templates with ability ids
  apply effects that carry no animation (the sniper's charge state 2759, the
  flamethrower's cone effect 8785, the phason's 12513, the Vorrax beam's 356/358,
  the missile launcher's 251, an overcharge stat modifier 11326) and running them
  changes combat numbers rather than what a client draws, so they are the next step
  rather than this one.
* **A death mid-charge releases the shot.** The charge effect's duration chain starts
  with `RequireCState(living=1)`, so the effect ends the moment the mob stops living -
  and the chain that ends an effect is its removal chain, which for this effect is the
  release: animation, projectile, audio. A corpse therefore fires the shot it had
  charged. That is the data's own structure (the release runs on any removal) and the
  build does not special-case it.
* **The weapon fire markers stay as they are.** Both animating templates carry
  `anim_fire_type` 0 and `anim_charge_type` 0 - the database gives them no weapon fire
  or charge animation of its own, which is exactly why its own animation lives in the
  effect chains instead. A client's `WeaponBurstFired` handling has nothing to play for
  a type of 0; the chain's `tfPlayAnimation` / `tfAbilityAnimation` is what it plays.
* **The flags the charge sets are honoured.** A charge-up effect restricts the
  character it sits on, and the client shows that; the AI reads the same replicated
  flags (`CharacterEntity.HasCombatFlag`) rather than inventing its own charge state,
  so a mob running a `restrict_movement` effect holds position (it still turns to
  face its target) and one under `restrict_weapon` / `restrict_abilities` does not
  fire. `CombatFlagsCommand` snapshots and restores those bits per effect, so the
  restriction ends exactly when the effect that set it does.

**Weapon animation parameters.** `SDBUtils.GetDetailedWeaponTemplateInfo` used to
drop the `anim_*` columns ("stuff that is presumably client side like
animations"); they are now resolved through the same item/slot modifier cascade as
every other template field and carried on `NpcAttackProfile`
(`ArmedAnimationId`, `ArmedAnimationPriority`, `FireAnimationType`,
`ReloadAnimationType`, `ChargeAnimationType`, `BurstDurationMs`). Of the 85
templates the build's mobs actually use, all 85 carry `anim_armed_id` (1 on 76 of
them, 4 on the other 9) and `anim_armed_priority` 100, 47 carry a non-zero
`anim_fire_type`, 53 an `anim_reload_type` (the reload animation the marker above
plays, on 747 of the 1,510 monster weapon slots that reload) and 2 an
`anim_charge_type`; the client gets them from the item's template id, which the
equipment replication already carries.

**Locomotion.** The database gives a mob two speeds and the client has two
locomotion animations to match: `normal_speed` is the walk used while it
repositions inside its attack (the `Attack` state's speed) and `fast_speed` the
run it chases with, so the engine broadcasts `Walking` (`0x5004`) while attacking
and `Running` (`0x2004`) while chasing, and `Standing` (`0x1000`) when it holds
still. `AiSpeeds` picks which of the two rows is trusted; the movement state that
goes out with every pose is the DB-driven selection on top of it.

**Death.** The state change (`CharacterState.Dead`) plus `NpcDeathService`'s gib
visuals and corpse linger are what the client plays its death and gib animation
from; an attack animation in flight is cancelled first (above). The gib visuals
id is the chassis battleframe's `gibset_id`, resolved by `GibVisualsResolution`,
and - the point of that type - a `gibset_id` of **0 is a value, not an absence**:
it is `dbcharacter::GibVisuals` row 0, the database's default row, which 804 of
the 1,676 battleframes and 3,816 of the 3,902 deployables use. The deployable
death path has always reported it as-is (`DamageSystem`), while the character path
used to drop it, so 1,806 monsters stayed silent at death and logged
`No gib visuals id available`; they now report the default row at their death
time like everything else. Only a chassis the database has no battleframe row for
(112 monster rows, 89 of them with `chassis_id` 0) has no gib set to report.

**What is *not* driven yet.** The database's animation surface is larger than
these three pieces, and the line currently sits here:

| Static DB | Rows | Describes | State in PIN |
|---|---|---|---|
| `dbitems::WeaponTemplates` / `WeaponTemplateModifiers` `anim_*`, `ms_burst_duration`, `ms_per_burst`, `base_clip_size`, `ammo_per_burst`, `rounds_per_burst`, `reload_time` | 318 / 5,387 | the weapon's animation selectors, its burst timing and its magazine/reload | used (above) |
| `dbcharacter::Monster.normal_speed` / `fast_speed` | 3,109 | the two locomotion speeds the walk/run animation matches | used (above) |
| `dbcharacter::GibVisuals` (`death_anim_index`, `blast_impulse_strength`, `direct_vrec_id`) via `dbitems::Battleframe.gibset_id` | 128 | the row a corpse reports at death: the death animation variant and the gib visuals | used: `NpcDeathService` reports the battleframe's `gibset_id` at the death time, 0 included (see below), covering the 1,137 monsters with an explicit gib set and the 1,806 whose battleframe names the default row. 112 monster rows have no battleframe to read it from (89 of them `chassis_id` 0, legacy entries), and 54 name a `GibVisuals` id this build does not ship - the id is reported as the row states it and the client resolves it against its own copy |
| `dbcharacter::Stumble` (`anim_index`, `duration`, `cooldown_ms`, `distance`, `only_once`) | 39 | hit-reaction ("stumble") rows: which animation, which status effect, how long, how often | unused, and not implementable from this build (see below) |
| `dbcharacter::StumbleDirection` (`anim_substate` 0-3, `direction_in`/`direction_out`, `threshold_in`, `stumble_id`) | 120 | which stumble plays for a hit from each direction (references the 32 directional rows) | unused - no row points at it |
| `dbcharacter::EmoteRecord` (`animation_name`, `anim_override_id`, `head_anim_override_id`, `statuseffect`) | 382 | emotes: the animation ids the client resolves from the emote id, and on 4 rows the status effect whose chain draws the emote | used for the emote lifecycle: `PerformEmote` is validated against the table (an id outside it is ignored), emote 0 clears the emote, and the 4 rows apply their effect so the emote animates for every client watching, not only for the performer (`Docs/EMOTES.md` §1-2). NPCs still have no emote of their own: 530 `tfPerformEmote` commands and the 359 effects holding them are reachable from no monster weapon in this build |
| `dbcharacter::MonsterMood` / `MonsterMoodName` | 2,268 / 6 | mood -> portrait id (`Neutral`, `Excited`, `Thinking`, `Angry`, `Happy`, `Sad`) | unused - a UI portrait with no field in the character views, so there is nothing for the server to replicate (`Docs/EMOTES.md` §5) |
| `apttf::tfPlayAnimationCommandDef` (122 distinct names: `AttackSingle`, `Shoot`, `MeleeAttack`, `Idle`, `Injured*`, `Death`, `roar`, `sleep`, ... plus `on_targets`) and `tfAbilityAnimationCommandDef` | 642 / 1,898 | the animation commands of the original game's chains, and the only place an animation is named | placeholder records, because the client runs them from the replicated effect they sit in; the two monster weapons below reach one through the effect their ability applies, and the rest of these rows belong to player abilities and to effects no monster weapon applies |
| `dbitems::Weapons.first_person_animnet_id` / `third_person_animnet_id`, `dbcharacter::Head.animnet_id`, `dbitems::BattleframeVisuals.animnetwork_id`, `dbcharacter::Deployable.animnetwork` | 6,789 / 67 / 2,786 / 3,902 | animation-network (animation graph) asset ids | client side: the client picks the graph from the item/visual id the server already replicates, so there is nothing for the server to send |

**Where the animations actually live.** Probing the aptitude chains settles what
the database can and cannot drive for a mob, and why the two pieces above are
documented rather than wired:

- *Animations ride status effects.* A `tfPlayAnimationCommandDef` is a node in a
  chain, and the chains that hold animations are the ones under
  `apt::StatusEffectData.apply_chain` / `remove_chain` / `duration_chain`: applying
  the effect is what plays the animation, for every client that has the same
  database. Effects are replicated on the character (`CombatView.StatusEffects_*`),
  so this is the one DB-declared animation path that reaches an observer of an NPC.
  The 2,540 animation commands are owned by status effects, `dbitems::AbilityModule`
  chains, one `WeaponTemplateModifiers.burst_ability_id`, one `Ammo.ability_id`, one
  `Ammo.touch_ability_id` and one `dbcharacter::Deployable.spawn_abilityid`; only
  **7** abilities in the whole build have a chain that reaches an animation command.
- *Monster weapons barely have abilities at all.* Of the 85 weapon templates the
  build's monsters use, 14 carry an `attack_ability_id` or a `burst_ability_id`
  (102 monster slots), and none of those abilities' own chains contains an animation
  command - even following `apt::CallCommandDef` into called abilities. Two of them
  apply a status effect whose chain does carry one: effect 176 (animation
  `MeleeAttack`, applied by template 21 "Melee - Shadowstrike", 2 monsters) and
  effect 10496 (`tfAbilityAnimationCommandDef` + `FireProjectileCommandDef`, applied
  by template 12143 "NPC Charge Up and Channel Fire", 14 monsters). Applying those
  effects is the faithful way to animate those 16 mobs, and §3 now does it: the
  charge effect's restrictions, its `ReplenishableDurationCommandDef` and the
  `ReplenishEffectDurationCommandDef` that hands the charge time to it are all
  part of the same change. The other 12 templates (86 monster slots) carry ids too,
  but their chains apply effects without an animation, so their mobs still animate
  from the AI's own attack only.
- *Hit reactions have no trigger in this build.* The stumble data is complete -
  `dbcharacter::Stumble` 9452 is the effect a stumble applies, and that effect's
  own chain is a stumble in full: `RequireHasEffectTag`, then
  `tfPlayAnimationCommandDef` `DamageHeavy`, then `CombatFlagsCommandDef` with
  `restrict_movement` + `restrict_weapon` + `restrict_abilities` + `restrict_melee`
  + `restrict_interaction`, for `TimeDurationCommandDef` 2,000 ms. The one table
  that says *when* it fires is the hardpoint table (`hardpoint_name`,
  `pfx_asset_id`, `stumble_id`) and it has **0 rows**, no weapon, ammo, damage type
  or ability row references a stumble id, and the only other chain in the build
  that applies a stumble effect is ability 34039 ("on impact, apply 901 to self"),
  granted by a single `WeaponTemplateModifiers.burst_ability_id` - player gear.
  The victim-facing event is BaseController-scoped too (`Stumble`: `ushort`,
  `ushort`, `byte`), so an NPC's stumble has no observer channel but the effect.
  Picking a trigger would mean inventing the rule, so it is left out.

The protocol's only animation-specific observer message is the GSS character
event `AnimationUpdated` (`ushort` + `byte`, both fields unnamed in
`AeroMessages`);
PIN has never sent it, so stumbles and the chain animations above have no observer
channel today. `AbilityActivated`/`AbilityFailed` are `CombatController` events,
i.e. addressed to the controlling player, and so cannot carry an NPC's animation
either. The attack animation described above is what an observer of an NPC gets.

---

## 4. Commands

Available in the in-game chat (with a `\` prefix) and on the Admin channel
(without it):

| Command          | Effect                                                     |
|------------------|------------------------------------------------------------|
| `\ai` / `\ai status` | Reports whether AI is on and how many NPCs are tracked |
| `\ai on`         | Enables the engine (`enable`, `1` also work)               |
| `\ai off`        | Disables it; NPCs freeze where they stand (`disable`, `0`) |
| `\ai list`       | Lists every tracked entity id with its current state       |

The switch is per shard and not persisted.

---

## 5. Tuning

Everything is an `IAiRules` property. `AiEngine` takes an optional instance; pass
`null` to get `StandardAiRules`:

| Property             | Default | Meaning                                                          |
|----------------------|---------|------------------------------------------------------------------|
| `Enabled`            | `true`  | Master switch                                                    |
| `AggroRadius`        | `55`    | Metres at which an idle NPC notices a hostile player             |
| `MaxAcquisitionHeightDelta` | `12` | Metres of height difference the notice scan accepts (`0` = unlimited, i.e. a cylinder) |
| `AttackRange`        | `3.5`   | Metres of straight-line reach an attack needs; used by an NPC with no resolvable weapon (a mob with one uses its weapon's range) |
| `AttackRangeExit`    | `5`     | Metres at which attacking falls back to chasing (weapon overrides, see above) |
| `MaxAttackHeightDelta` | `2.5` | Metres of height difference one attack may span; a ranged weapon widens it to the weapon's range |
| `StandoffRange`      | `2`     | Metres the NPC tries to keep from its target; a ranged weapon uses its behaviour's `combatDist` instead |
| `LeashRadius`        | `120`   | Metres from the spawn point before it gives up                   |
| `HomeArrivalRadius`  | `2`     | Metres from home that counts as arrived                          |
| `AttackCooldownMs`   | `1200`  | Delay between two attacks by the same NPC; used by an NPC with no resolvable weapon (a mob with one uses its behaviour/weapon cadence) |
| `AttackDamageFraction` | `0.1` | Share of the monster's per-level damage rating one attack commits |
| `AttackDamage`       | `5`     | Fallback damage per attack when the DB has no scaling row for the NPC's level |
| `TargetLostTimeoutMs`| `6000`  | How long an unseen target is still hunted                        |
| `PerceptionIntervalMs`| `200`  | Target scan / line of sight interval                             |
| `MovementIntervalMs` | `50`    | Movement + pose broadcast interval (20 Hz)                       |
| `DefaultMoveSpeed`   | `5`     | m/s when the monster row has no usable `normal_speed`            |
| `DefaultChaseSpeed`  | `8.5`   | m/s when the monster row has no usable `fast_speed`              |
| `MinTrustedSpeed`    | `0.25`  | Lower bound for trusting an SDB speed                            |
| `MaxTrustedSpeed`    | `35`    | Upper bound for trusting an SDB speed                            |
| `SnapToGround`       | `true`  | Pull moving NPCs onto the ground with a downward ray cast         |
| `GroundOffset`       | `0`     | Metres to add to the ground surface when snapping                |

### Movement speeds come from the database

`SdbAiMonsterStats` reads `dbcharacter::Monster.normal_speed` into the walk speed
and `fast_speed` into the chase speed. Plenty of rows are `0` and some look like
they are expressed in a different unit, so `AiSpeeds.Resolve` only trusts values
inside `[MinTrustedSpeed, MaxTrustedSpeed]` and falls back to the configured
defaults otherwise. That is what keeps a bad row from producing frozen or
teleporting mobs.

### Health and attack damage come from the database

The `dbcharacter::Monster` row itself carries no health or damage — those live on
the per-level curve in `dbcharacter::MonsterScaling` (level 1-80 -> health /
damage). A monster row only exists as a type, so the level that picks the curve
comes from where it spawns:

- `CharacterEntity.LoadMonster` resolves the shard zone's level band
  (`dbzonemetadata::ZoneRecord.level_band` -> `dbitems::LevelBand`) and takes the
  top of the band (`SDBUtils.ResolveNpcLevel`); open ended bands (`max == 255`,
  the "no upper bound" sentinel) resolve to their floor, and everything is capped
  at level 80 because the scaling table stops there.
- Zones without a band fall back to `SDBUtils.DefaultNpcLevel`. That is the
  default player level — a fresh battleframe starts at progression level 1 and
  PIN has no XP economy yet — so mobs in untuned zones fight on the player's
  terms (level-1 mobs: 100 max health, 5 damage a swing in build `prod-1962`). The
  zones the database never tuned are exactly the ones whose difficulty the live
  game set through its server-side mission/spawn flow rather than zone rows
  (the PIN test zone `12` "Nothing", Crash Down `1003`, the first story
  missions, operations and the raid have no `level_band`; PvP maps have no PvE
  at all). For those, `character_spawn.json` entries can carry an authored
  `level` (1-80) that becomes the spawn's `MonsterScaling` level — the emulator
  equivalent of that flow.
- The scaling row then sets the NPC's `MaxHealth` (`ScalingTable.health`), and
  `AiEngine` resolves the monster's weapon at the NPC's level when it registers:
  `SdbAiMonsterStats.GetAttackProfile` (see
  [Weapon attacks come from the database](#weapon-attacks-come-from-the-database))
  gives the per-round damage, and only when that resolves to nothing does the
  engine spend the rating: `SdbAiMonsterStats.GetAttackDamage(typeId, level)` for
  the damage **rating** of the NPC's level (`ScalingTable.damage`), which
  `AiAttackDamage.Resolve` turns into what one swing is worth -
  `AttackDamageFraction` (a tenth) of it - because the rating is not a per-hit
  amount. The rules' `AttackDamage` is only the fallback for a monster or level
  the database has no row for, and it is already a per-swing number, so it is not
  fractioned a second time.
- A per-spawn `max_health` in `character_spawn.json` still overrides the database
  health for that one spawn (`EntityManager.SpawnZoneEntities` applies it after
  `LoadMonster`), and a per-spawn `level` overrides the level it is looked up at.

### Why the damage column is a rating and not a per-hit number

`dbcharacter::MonsterScaling` has exactly three columns, and on all 80 rows
`damage` is precisely `round(health / 2)` - level 1 is 100 health / 50 damage,
level 20 is 3,900 / 1,950, level 45 is 27,869 / 13,934, level 80 is 153,726 /
76,863. A column that is the monster's own health divided by two is a balance
figure ("what a monster of this level is worth"), the twin of the health column -
not the damage of one punch. Read as a per-hit amount it means a level-45 mob
deletes a level-45 player (whose pool in this build is 19-21k) in one or two
swings, which is exactly the "enemies attack way too hard" report; and since the
mob curve grows far faster than the player's health curve, "divide by a bigger
number at high levels" is not the fix either - one fixed fraction keeps the fight
ratio roughly constant across the whole table:

| NPC level | `MonsterScaling.damage` | per hit (× 0.1) | level-matched player pool | swings to kill that player (rounded up) |
|---|---|---|---|---|
| 1  | 50    | 5    | 361   | 73 |
| 10 | 373   | 37   | 661   | 18 |
| 20 | 1,950 | 195  | 2,191 | 12 |
| 30 | 5,057 | 506  | 6,151 | 13 |
| 40 | 10,918 | 1,092 | 12,721 | 12 |
| 45 | 13,934 | 1,393 | 19,441 | 14 |
| 50 | 17,784 | 1,778 | 29,701 | 17 |
| 80 | 76,863 | 7,686 | 29,701 (level 50 cap) | 4 |

The pool column is the health rule itself (`Docs/PLAYER_STATS_AND_HEALTH.md` §4:
starter item Health 361 + `dbitems::LevelItemAttributes` attribute 6 × 3), so the
last column is a fully database-derived property of the build, not a taste call:
a mob in your own level band takes a dozen or so swings to finish you, a mob
several bands above you is lethal, and the fresh-frame zones without a band (mob
level 1) are the survivable start they are meant to be. `AttackDamageFraction` is
the knob for tuning that up or down.

Example: a level-45 zone (Diamond Head's band is 44-45) spawns every monster with
27,869 max health hitting for 1,393 per attack in build `prod-1962`; a zone
without a band resolves its NPCs at the default player level (1, see above)
instead, hitting for 5. In a level-40 zone (Sertao's open-world band is 39-40) the
same monster type spawns with 21,836 health and hits for 1,092.

### Weapon attacks come from the database

`NpcAttackResolver` walks the database's own chain when a monster is registered
and hands the engine an `NpcAttackProfile` - the mode, per-round damage, rounds
per burst, cadence, reach, and the ammo row to fire:

```
dbcharacter::Monster.weapon1_id (else weapon2_id)
  -> dbitems::Weapons                      (the per-monster item)
  -> dbitems::WeaponTemplates              (via weapon_type_id; the item's
                                            WeaponTemplateModifiers and the
                                            weapon-slot ability modules are
                                            applied by SDBUtils.GetDetailedWeaponInfo)
  + dbitems::AttributeRange rows of the item (954 damage, 957 range, 1145 modifier,
                                              the ammo stat attributes)
  + dbcharacter::MonsterAttributeRange row of the monster (1144 damage modifier)
  + dbcharacter::Monster.behavior string   (triggerPullTime, fireRestDuration,
                                             combatDist, preferredMinCombatDist)
```

**Mode.** A row is ranged when its template carries an `ammo` id that resolves
*and* its range is past arm's length (4 m); everything else - the melee ability
vehicles with 2-3 m ranges, rows whose ammo does not resolve, and every monster
the database gives no weapon at all - melees. Of the 3,109 monsters in build
`prod-1962`, 1,727 carry a weapon; 596 of the weapon items are used by monsters
and 85 distinct templates are involved.

**Damage.** The two damage statements are mutually exclusive on monster weapons
(0 of the 596 items carry both):

1. `AttributeRange` 954 (Damage Per Round) - the literal per-round damage of a
   level-matched item (the shared PvE weapon families carry it). Used as-is.
2. `AttributeRange` 1145 (Creature Weapon Damage Modifier) - the creature item's
   own modifier on the monster's per-level damage rating
   (`dbcharacter::MonsterScaling.damage` × 1145). This is how a level-45 mob hits
   like a level-45 mob while its weapon item is a level-1 row: the rating is the
   level term, the modifier is the weapon term (0.0217 on the common guards, 0.15
   on the elite ones).
3. Neither (the ~103 melee/ability-only items, and every weaponless monster) -
   the rating share the engine has always used: `rating × AttackDamageFraction`
   (a tenth), with the rules' flat `AttackDamage` for a monster or level the
   database has no rating for.

Every branch is then multiplied by the monster's own
`MonsterAttributeRange` 1144 (Creature Damage Modifier, 0.95-1.5 on the 81 named
monsters that carry one, 1 otherwise).

**Cadence.** The behaviour string is where the original game put its AI attack
timing: `triggerPullTime` (median 1,500 ms across the 248 rows that carry it) plus
`fireRestDuration` (median 1,000 ms). The weapon template's `ms_per_burst` is the
client's fire animation cadence (50-100 ms on several NPC weapons), so it is only
used when the row has no behaviour timing, clamped at 250 ms (one 50 ms AI tick's
worth of sanity), and `AttackCooldownMs` is the last resort. Rounds per burst come
from the template, so one attack can be a burst of projectiles.

**Range and standoff.** A ranged weapon's effective range is its item's 957 row
when it has one, else the template's `range`; the exit range is that × 1.15. A
melee row swings at the tuned 3.5 m reach rather than its own template `range`
(2.6 m for the Spyder's "NPC Melee Medium", where the template value is an
ability radius), and a ranged row whose ammo does not resolve falls back to that
same reach instead of becoming a hitscan sniper at its own range. A ranged row
keeps its behaviour's `preferredMinCombatDist` (or `combatDist`) as its standoff,
so guards stop at the distance the data gives them instead of walking into melee.

**Muzzle.** The shot leaves from `dbcharacter::Monster.projectile_offset` rotated
into world space the same way `CharacterEntity.CalculateProjectileOrigin` rotates
a character's own muzzle; a row with no offset uses the chest height (1.62 m).

### Ground snapping

`SnapToGround` is on. The character origin sits at the feet, so `GroundOffset` is
`0`: the downward ray cast pulls the NPC's origin onto the top surface of the
static geometry and the mob walks along the terrain instead of floating or
sinking. Spawned mobs are snapped the same way before they are scoped in
(`PhysicsEngine.FindGround`), so zone entries with a placeholder `Z` of `0` land
on the ground instead of spawning deep under it.

The probe only tests static geometry (a nearby player or mob cannot be mistaken
for the ground), and the wall check ray is raised to torso height so it does not
graze the terrain the mob is standing on. When no zone collision data is loaded
(`LoadMapsCollision` off, or no map files) both probes are no-ops and movement
stays horizontal, exactly as before.

---

## 6. Known gaps

* **Animation is the attack + reload + locomotion + death markers, plus the two
  weapons whose chains animate.** The engine drives the attack burst markers, the
  weapon reload markers, the walk/run/stand movement state and the death state, and
  it runs a weapon's ability when the database puts an animation in the effect that
  ability applies (16 monster slots: the Shadowstrike swing and the charge-up
  weapon's charge/release - see
  [What an NPC animates](#what-an-npc-animates), which lists the animation rows that
  are and are not used). Everything else in the animation surface is untouched: the
  engine does not run a monster's behaviour tree, so there are no idle, taunt or
  wander animations, and no NPC emote has a data path of its own: the emote table is
  wired to `PerformEmote` and to the 4 rows that apply a status effect
  (`Docs/EMOTES.md`), and the 39,261 dialog rows - 356 of them with an emote - are
  not played because the trigger rules for them are not in the database (6 chatter
  rows describe the probabilities, not the events); the other 12
  weapon templates with ability ids apply effects that carry no animation, and their
  effects (charge states, cone effects, stat modifiers) are a separate step; the
  protocol's `AnimationUpdated` observer event is never sent, so the `apttf::tf*`
  animations only reach a client through the status effect the effect-data chain
  replicates; and stumble/hit-reaction animations (`dbcharacter::Stumble`,
  `dbcharacter::StumbleDirection`) are left out because the build has no trigger for
  them at all (the one table that links a stumble to what causes it has 0 rows, and
  no weapon, ammo, damage type or ability row references a stumble id; see
  [What an NPC animates](#what-an-npc-animates)).
* **No accuracy model.** NPC shots aim at the target's chest with the weapon's
  own spread profile left unused: there is no per-NPC spread state, no
  `MinSpread`/`MaxSpread` handling and no aim error, so a ranged mob hits for as
  long as line of sight holds. The database's behaviour strings also carry attack
  *chance* parameters (`am1Chance`, `am1Cooldown`) that name aptitude abilities
  the original game fired at intervals; those ability chains are not modelled, so
  only the weapon's own attack is fired.
* **Weapon damage rows are placeholder where the data is placeholder.** A few
  templates (not per-monster items) carry `damage_per_round` 1; those rows still
  resolve through the 954/1145 chain above, so the placeholder only survives where
  a weapon item has neither attribute row - the rating share covers it.
* **No pathfinding, no climbing.** Movement is a straight line towards the goal
  plus a wall check, always at the spawn's own height band. A mob behind a low
  obstacle will stand there until the leash or the give-up timer fires, and a mob
  you are standing over - on a roof, on a ledge, on top of a vehicle - simply
  cannot reach you. It does not jump, climb or walk around the height difference;
  it circles at its own ground level until the leash drags it home.
* **No SDB behaviour trees.** `Monster.Behavior`, `BehaviorOffensive` and
  `BehaviorDefensive` name the live game's AI behaviour assets; PIN ignores them
  and runs one state machine for every monster type.
* **Broadcast is not scope filtered.** Pose updates go to every playing client
  like `MovementRelay` does, not just the clients the entity is scoped into.

---

## 7. Quick reference

| Action                              | Command                                  |
|-------------------------------------|------------------------------------------|
| Spawn something to fight            | `\spawn monster 1196`                    |
| Spawn a mob that will not fight back| `\spawn monster 290` (Accord, friendly)  |
| See how many NPCs are simulated     | `\ai`                                    |
| Freeze every mob                    | `\ai off`                                |
| List mobs with their current state  | `\ai list`                               |
| Check your vitals while being shot  | `\health`                                |
