# Battleframe ability verification

How the player-facing battleframe kit fares against the server's aptitude
pipeline, verified directly against `clientdb.sd2` (prod-1962).

## What "verified" means here

For every module seated in a stock battleframe's loadout (the same
`Lib/Shared.Common/Characters/ChassisStockLoadouts.cs` table the server and
web hosts resolve, generated from `dbcharacter::CharCreateLoadout*`):

1. `dbitems::AbilityModule.ability_chain_id` must name a real
   `apt::AbilityData` row with a nonzero chain;
2. every node of that chain (`apt::BaseCommandDef`, walked `Id -> Next`) must
   classify as *runnable server-side*: environment `client` rows are no-ops by
   design, and every `server`/`both` command type must have a factory route to
   a real implementation (the chain runs exactly the commands the data
   declares);
3. `ActiveInitiation` is excluded on purpose: the combat controller that owns
   the activation already sends the acknowledgement, so the chain row is a
   documented no-op (running it would double-send).

Beyond that, this audit does **not** assert per-command parameter semantics
(direction math, effect values) — those rest on the audit fixes in the PR and
the unit-test suite; and true end-to-end requires a live client, which the
sandbox does not have.

Reproduce / keep it green:

```
python3 Tools/SdbDump/verify_ability_chains.py [--names]
python3 Tools/SdbDump/verify_ability_chains.py --check   # CI: census step
```

The `--check` mode pins the documented allowlist of remaining gaps: any new
placeholder (a lost factory route, or data starting to use an unimplemented
command type) fails CI; fixing a documented one only asks for an allowlist
cleanup.

## Scoreboard (34 chassis table, ability seats only)

| state | seats |
|---|---|
| chain fully implemented server-side | 73 |
| chain has a remaining documented gap | 51 |
| ability row with no chain (non-kit gear slots) | 18 |
| module with no ability link (gear/upgrade modules) | 80 |
| missing ability row | 0 |

All ok rows went through the full chain req/target/effect pipeline:
cooldowns (`TimeCooldown`, `InflictCooldown`, `InstantActivation`), loadout
item-stat registers (`LoadRegisterFromItemStat`), requirements
(`RequireHasEffect`, `RequireCState`, `RequireJumped`, `RequireSuperCharge`,
…), `ImpactApplyEffect`/status-effect chains, deployable/turret spawns,
`ForcePush`/`ApplyImpulse`, melee auxiliaries, med systems, SIN spawns.

## Remaining gaps (named abilities)

### Ability trigger subsystem — biggest single gap (29 abilities)
`ActivateAbilityTrigger` / `RegisterAbilityTrigger`: both command defs are
id-only, and the AeroMessages tree carries no client<->server ability-trigger
message flow, so what a "trigger" means at runtime is not derivable from the
shipped evidence. A protocol-capture of a charged activation (e.g. pressing
and releasing Overcharge on a live client) would unblock this.

Affected: **Shockwave** (Assault HKM), **Overcharge**, **Meteor Strike**,
**Afterburner**, **Absorption Bomb** (Dread HKM), **Heavy Armor**,
**Turret Mode**, **Auxiliary Weapon - Melee** (Rifle Bash / Hammer / Sword),
**Heavy Turret**, **Anti-Personnel Turret**, **Supply Station**, **Cryo
Bolt**, **Bulwark** (Electron), PvP Ground Stomp / Energy Sword; and the
frame passives using `RegisterAbilityTrigger` (**Ambush** (Nighthawk),
**Conduit** (Raptor), **Rally** (Dragonfly), **E-Tank** (Tigerclaw),
**Incinerator** (Firecat), **Necrotic Poison** (Recluse), **Personal
Shield** (Mammoth), **Radiation Leak**, **Arsenal EM Pulse**, **Equip
Assault Nanites**).

### TinyObjectCreate — 1 ability
`OLD Emergency Response` (Dragonfly). Id-only def; the tiny-object entity
family is not modeled. The row is also a superseded "OLD" ability.

### RequireItemDurability — 1 ability
`Medical System - Primary Shared Activation Effect` on the PvP test frames
(Bastion/Dreadnaught). Per-slot item wear is not modeled (only a static
char-level prop exists), so no honest answer server-side.

### Frame ability groups (passive kit) — documented exclusion
`dbcharacter::AbilityGroupModule` / `AbilityGroupPassive` load nowhere, so
a battleframe's passive group (sprint/jetpack/core triggers, e.g. core group
32) cannot be enrolled from data. Button abilities are unaffected (they
resolve through loadout modules).

## Per-frame detail

#### Accord Assaultframe
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78925 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 78518 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78512 | 35540 | Shockwave | 1577895 | **gap:** ActivateAbilityTrigger |
| 7 | 78507 | 35448 | Overcharge  | 1634270 | **gap:** ActivateAbilityTrigger |
| 8 | 78503 | 127 | ? | 1549842 | ok |
| 9 | 82745 | 35573 | Crater | 1574694 | ok |
| 10 | 78522 | 35630 | Equip Assault Nanites | 925396 | **gap:** RegisterAbilityTrigger |

#### Dreadnaught
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 6 | 125199 | 41881 | Absorption Bomb | 1634176 | **gap:** ActivateAbilityTrigger |
| 116 | 126000 | 0 | ? | - | no ability link (gear/upgrade module) |
| 121 | 90493 | 35366 | Charge | 1619015 | ok |
| 122 | 129505 | 39845 | Auxiliary Weapon - Melee, Hammer | 1410869 | **gap:** ActivateAbilityTrigger |
| 126 | 127501 | 0 | ? | - | no ability link (gear/upgrade module) |
| 127 | 128271 | 0 | ? | - | no ability link (gear/upgrade module) |
| 128 | 126731 | 0 | ? | - | no ability link (gear/upgrade module) |
| 129 | 129067 | 0 | ? | - | no ability link (gear/upgrade module) |
| 132 | 91296 | 35535 | Heavy Armor | 1634235 | **gap:** ActivateAbilityTrigger |
| 133 | 123309 | 39434 | Turret Mode  | 1605621 | **gap:** ActivateAbilityTrigger |
| 138 | 125285 | 0 | ? | - | no ability link (gear/upgrade module) |
| 139 | 91024 | 41881 | Absorption Bomb | 1634176 | **gap:** ActivateAbilityTrigger |

#### Recon
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 6 | 91770 | 35567 | Artillery Strike | 1582822 | ok |
| 116 | 126000 | 0 | ? | - | no ability link (gear/upgrade module) |
| 121 | 91662 | 39405 | Cryo Bolt | 1619445 | **gap:** ActivateAbilityTrigger |
| 122 | 129359 | 39846 | Auxiliary Weapon - Melee, Sword | 1315423 | **gap:** ActivateAbilityTrigger |
| 126 | 127501 | 0 | ? | - | no ability link (gear/upgrade module) |
| 127 | 128271 | 0 | ? | - | no ability link (gear/upgrade module) |
| 128 | 126731 | 0 | ? | - | no ability link (gear/upgrade module) |
| 129 | 129067 | 0 | ? | - | no ability link (gear/upgrade module) |
| 132 | 90285 | 35909 | Teleport Beacon | 1614911 | ok |
| 133 | 90020 | 34668 | Remote Explosive | 1606809 | ok |
| 138 | 125285 | 0 | ? | - | no ability link (gear/upgrade module) |
| 139 | 91793 | 35567 | Artillery Strike | 1582822 | ok |

#### Biotech
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 6 | 89124 | 41867 | Heroism | 1634203 | ok |
| 116 | 126000 | 0 | ? | - | no ability link (gear/upgrade module) |
| 121 | 123271 | 34753 | Healing Generator | 1608827 | ok |
| 122 | 129213 | 39844 | Auxiliary Weapon - Melee, Rifle Bash | 1315454 | **gap:** ActivateAbilityTrigger |
| 126 | 127501 | 0 | ? | - | no ability link (gear/upgrade module) |
| 127 | 128271 | 0 | ? | - | no ability link (gear/upgrade module) |
| 128 | 126731 | 0 | ? | - | no ability link (gear/upgrade module) |
| 129 | 129067 | 0 | ? | - | no ability link (gear/upgrade module) |
| 132 | 88808 | 41866 | Adrenaline Rush | 1634211 | ok |
| 133 | 123262 | 41865 | Poison Ball | 1618829 | ok |
| 138 | 125285 | 0 | ? | - | no ability link (gear/upgrade module) |
| 139 | 89147 | 41867 | Heroism | 1634203 | ok |

#### Engineer
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 6 | 91559 | 39426 | Anti-Personnel Turret  | 1591207 | **gap:** ActivateAbilityTrigger |
| 116 | 126000 | 0 | ? | - | no ability link (gear/upgrade module) |
| 121 | 91394 | 39360 | Heavy Turret | 1614550 | **gap:** ActivateAbilityTrigger |
| 122 | 129359 | 39846 | Auxiliary Weapon - Melee, Sword | 1315423 | **gap:** ActivateAbilityTrigger |
| 126 | 127501 | 0 | ? | - | no ability link (gear/upgrade module) |
| 127 | 128271 | 0 | ? | - | no ability link (gear/upgrade module) |
| 128 | 126731 | 0 | ? | - | no ability link (gear/upgrade module) |
| 129 | 129067 | 0 | ? | - | no ability link (gear/upgrade module) |
| 132 | 131211 | 41880 | Overclock | 1634332 | ok |
| 133 | 91510 | 39423 | Supply Station  | 1619632 | **gap:** ActivateAbilityTrigger |
| 138 | 125285 | 0 | ? | - | no ability link (gear/upgrade module) |
| 139 | 91582 | 39426 | Anti-Personnel Turret  | 1591207 | **gap:** ActivateAbilityTrigger |

#### Tigerclaw
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78925 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 79942 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78850 | 35637 | Supercharge | 1634220 | ok |
| 7 | 79934 | 34665 | Missile Shot | 817166 | ok |
| 8 | 79938 | 35633 | OLD Trailblaze | 1174650 | ok |
| 9 | 79930 | 35527 | Pulsar | 1619407 | ok |
| 10 | 79946 | 35582 | E-Tank (Tigerclaw Passive) | 375041 | **gap:** RegisterAbilityTrigger |

#### Firecat
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78525 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78924 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 78517 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78554 | 35627 | Fuel Air Bomb | 1384014 | ok |
| 7 | 78542 | 35521 | Immolate | 1542950 | ok |
| 8 | 78536 | 35458 | Thermal Wave | 1634242 | ok |
| 9 | 78548 | 35518 | ? | - | ability row chains nowhere |
| 10 | 78565 | 35613 | Incinerator (Firecat Passive) | 543746 | **gap:** RegisterAbilityTrigger |

#### Assault
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 6 | 88491 | 35540 | Shockwave | 1577895 | **gap:** ActivateAbilityTrigger |
| 116 | 126000 | 0 | ? | - | no ability link (gear/upgrade module) |
| 121 | 88039 | 39243 | Meteor Strike | 1613990 | **gap:** ActivateAbilityTrigger |
| 122 | 129213 | 39844 | Auxiliary Weapon - Melee, Rifle Bash | 1315454 | **gap:** ActivateAbilityTrigger |
| 126 | 127501 | 0 | ? | - | no ability link (gear/upgrade module) |
| 127 | 128271 | 0 | ? | - | no ability link (gear/upgrade module) |
| 128 | 126731 | 0 | ? | - | no ability link (gear/upgrade module) |
| 129 | 129067 | 0 | ? | - | no ability link (gear/upgrade module) |
| 132 | 88099 | 39257 | Afterburner | 1607791 | **gap:** ActivateAbilityTrigger |
| 133 | 88324 | 35448 | Overcharge  | 1634270 | **gap:** ActivateAbilityTrigger |
| 138 | 125285 | 0 | ? | - | no ability link (gear/upgrade module) |
| 139 | 88514 | 35540 | Shockwave | 1577895 | **gap:** ActivateAbilityTrigger |

#### Mammoth
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78525 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78924 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 78748 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78743 | 35367 | ? | - | ability row chains nowhere |
| 7 | 78739 | 35525 | ? | - | ability row chains nowhere |
| 8 | 78734 | 31259 | Thunderdome | 1634306 | ok |
| 9 | 78728 | 34697 | ? | - | ability row chains nowhere |
| 10 | 79965 | 35377 | Imminent Threat - Mammoth Passive | 418511 | ok |

#### Rhino
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 5 | 85206 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78779 | 34066 | Dreadfield | 1634297 | ok |
| 7 | 78768 | 35366 | Charge | 1619015 | ok |
| 8 | 78759 | 35494 | Sundering Blast | 1609739 | ok |
| 9 | 78773 | 34659 | ? | - | ability row chains nowhere |
| 10 | 82842 | 35501 | Personal Shield (Mammoth Passive) | 1342662 | **gap:** RegisterAbilityTrigger |

#### Nighthawk
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78925 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 79974 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78909 | 35453 | Eruption | 1634324 | ok |
| 7 | 78904 | 35345 | Smoke Screen | 1614188 | ok |
| 8 | 78900 | 34668 | Remote Explosive | 1606809 | ok |
| 9 | 78895 | 34671 | Execution | 1634289 | ok |
| 10 | 79978 | 35348 | Ambush (Nighthawk Passive) | 930828 | **gap:** RegisterAbilityTrigger |

#### Raptor
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78925 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 78858 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 79004 | 35618 | Overload | 1634341 | ok |
| 7 | 79000 | 35909 | Teleport Beacon | 1614911 | ok |
| 8 | 79008 | 34681 | SIN Scrambler | 1545241 | ok |
| 9 | 79012 | 35354 | ? | - | ability row chains nowhere |
| 10 | 79986 | 35465 | Conduit (Raptor Passive) | 809736 | **gap:** RegisterAbilityTrigger |

#### Dragonfly
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78925 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 79036 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78844 | 34928 | Healing Dome | 1634280 | ok |
| 7 | 79056 | 35445 | OLD Emergency Response | 1247005 | **gap:** TinyObjectCreate |
| 8 | 79046 | 34832 | ? | - | ability row chains nowhere |
| 9 | 79051 | 35040 | ? | - | ability row chains nowhere |
| 10 | 79990 | 35509 | Rally (Passive) - Dragonfly Passive | 594436 | **gap:** RegisterAbilityTrigger |

#### Recluse
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78525 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78924 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 79035 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 79076 | 35620 | Necrosis | 1634227 | ok |
| 7 | 79068 | 34691 | Kinetic Shot | - | ability row chains nowhere |
| 8 | 79063 | 34734 | Creeping Death | 1607623 | ok |
| 9 | 79072 | 34997 | ? | - | ability row chains nowhere |
| 10 | 79080 | 35621 | Necrotic Poison (Recluse Passive) | 1198968 | **gap:** RegisterAbilityTrigger |

#### Electron
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78925 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 79954 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78671 | 35583 | Electrical Storm | 1592796 | ok |
| 7 | 78666 | 35460 | Overclocking Station | 600898 | ok |
| 8 | 78660 | 35455 | Bulwark | 1634262 | **gap:** ActivateAbilityTrigger |
| 9 | 78654 | 34770 | Boomerang Shot  | 1605999 | ok |
| 10 | 79950 | 35446 | Fail-safe (Electron Passive) | 418406 | ok |

#### Bastion
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78528 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78925 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 79962 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 78706 | 35596 | Fortify | 1383638 | ok |
| 7 | 78700 | 35487 | ? | - | ability row chains nowhere |
| 8 | 78693 | 35229 | Shield Wall | 1548714 | ok |
| 9 | 78687 | 34629 | Multi Deploy Turret | 1618920 | ok |
| 10 | 78723 | 35576 | Overseer | 562571 | ok |

#### KS-G01 \
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78070 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78068 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 78418 | 0 | ? | - | no ability link (gear/upgrade module) |
| 10 | 82415 | 35879 | Radiation Leak (Graviton Passive) | 432021 | **gap:** RegisterAbilityTrigger |

#### Arsenal
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78524 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78921 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 85405 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 82486 | 35887 | ? | - | ability row chains nowhere |
| 7 | 82365 | 35841 | ? | - | ability row chains nowhere |
| 8 | 82372 | 35849 | ? | - | ability row chains nowhere |
| 9 | 82379 | 34980 | ? | - | ability row chains nowhere |
| 10 | 82490 | 36268 | Arsenal Electromagnetic Pulse | 638984 | **gap:** RegisterAbilityTrigger |

#### Archangel
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 6 | 82536 | 35888 | Fires from Heaven | 437089 | ok |
| 7 | 82521 | 34554 | Hover Mode | 426507 | ok |
| 8 | 82526 | 35844 | Evasive Maneuver | 1549883 | ok |
| 9 | 82531 | 35858 | Heavy Laser | 436950 | ok |
| 116 | 101763 | 0 | ? | - | no ability link (gear/upgrade module) |
| 117 | 90860 | 0 | ? | - | no ability link (gear/upgrade module) |
| 118 | 104882 | 0 | ? | - | no ability link (gear/upgrade module) |

#### A-43 \
| slot | module | ability | name | chain | state |
|---|---|---|---|---|---|
| 3 | 78070 | 30181 | Jetpack - Permissions | 888469 | ok |
| 4 | 78068 | 0 | ? | - | no ability link (gear/upgrade module) |
| 5 | 78078 | 0 | ? | - | no ability link (gear/upgrade module) |
| 6 | 77773 | 35540 | Shockwave | 1577895 | **gap:** ActivateAbilityTrigger |
| 7 | 77127 | 34966 | ? | - | ability row chains nowhere |
| 8 | 75273 | 34580 | ? | - | ability row chains nowhere |
| 9 | 77726 | 35518 | ? | - | ability row chains nowhere |
| 10 | 77902 | 35613 | Incinerator (Firecat Passive) | 543746 | **gap:** RegisterAbilityTrigger |
| 13 | 85729 | 0 | ? | - | no ability link (gear/upgrade module) |
