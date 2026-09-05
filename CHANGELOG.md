# Changelog

## [Unreleased]

### Added

- Implement server side NPC AI, replacing the empty `AIEngine` stub that did nothing on tick. Every character spawned through `EntityManager.SpawnCharacter` now gets a brain that runs an Idle / Chase / Attack / Return / Dead state machine: it scans for the closest hostile player inside an aggro radius (55 m), walks towards them at the monster row's `fast_speed`, and once they are inside 45 m with a clear line of sight applies flat damage through `DamageSystem` and sends the regular `TookHit` feedback. Being shot aggros the mob on its attacker regardless of distance, losing line of sight keeps it hunting for 6 s, and dragging it more than 120 m from its spawn point makes it drop the target and walk home. Movement is pushed into the physics body so mobs stay hittable where they now stand and broadcast as `CurrentPoseUpdate`, the same message that relays player movement. New `Systems/Ai/` namespace, documented in `Docs/NPC_AI.md`
- Add the `ai` command (chat with `\`, and on the Admin channel): `status` reports how many NPCs are simulated, `on`/`off` toggle the engine per shard, `list` dumps every tracked entity with its current state
- Split NPC AI into a pure decision half (`AiBrain`, fed an `AiPerception` snapshot, no shard/entity/physics dependency) and a shard integration half (`AiEngine`), with the tunables behind `IAiRules`, hostility behind `IAiHostility`, monster speeds behind `IAiMonsterStats` and hit feedback behind `IAiAttackFeedback` so every part is replaceable in tests
- Add `Docs/NPC_AI.md` covering the state machine, aggro triggers, the attack path, every tunable and the known gaps
- Add `AiBrainTests`, `AiEngineTests`, `AiSpeedsTests`, `AiVectorsTests` and `StandardAiRulesTests` to `GameServer.Tests` (state transitions, cooldowns, leash, aggro on damage, death, the kill switch, speed resolution and the character facing maths)
- Add `Docs/SpawnReference/`, the full catalog of everything PIN can spawn: one Markdown spreadsheet per kind (`MOBS.md` 3,109 rows, `DEPLOYABLES.md` 3,902, `VEHICLES.md` 173, `CARRYABLES.md` 105, `TURRETS.md` 107 - 7,396 rows in total) where every row lists its resolved name, faction/category/class and the exact `\spawn <kind> <id>` command, plus an index document with the command syntax, kind aliases, the faction table and the monster scaling table
- Add `Docs/SpawnReference/csv/`, the same rows as spreadsheets with **every** raw SDB column plus resolved foreign keys (chassis, weapons, loot tables, deployable category/function, vehicle class, granted ability, titles) for filtering in Excel/LibreOffice or diffing between builds
- Add `Tools/SdbDump/spawn_reference.py`, the generator for that folder: it imports the `sdb_dump.py` decoder, joins the spawnable tables against the lookup tables and decrypts only the `dblocalization::LocalizedText` rows those tables reference, so a full regeneration takes ~30 s and needs nothing but Python 3
- Deployables are catalogued for the first time (all 3,902 rows, 2,088 of them named, grouped by `DeployableCategory` / `DeployableFunction`)
- Implement fall damage: `FallDamageSystem` tracks each player's airborne samples from the client authoritative movement inputs and applies damage on landing based on the fastest downward speed of the fall (water landings, thruster/glider use, knockdown falls and the `immune_falldamage` combat flag negate it; lethal at very high impact speeds)
- Add a `GameServer.Tests` xUnit project covering `FallDamageMath`, `FallDamageSystem`, `DamageSystem`, `CharacterLifecycleService` and the `CharacterEntity` vital clamping; CI now runs `dotnet test`
- Add player facing health debug commands to the in-game chat: `\health`, `\hurt <amount>`, `\heal <amount>`, `\fall <speed>`, `\down`, `\revive`, `\kill`, `\respawn`, so the health system can be exercised without enemies
- Load `dblocalization::LocalizedText` (175,293 rows) and `dbcharacter::MonsterScaling` from the static database, exposed as `SDBInterface.GetLocalizedString(id)` and `SDBInterface.GetMonsterScaling(level)`, so the server can resolve display names instead of only ids
- Add a generic static-database spawn command available both in chat and on the Admin channel: `spawn <monster|deployable|vehicle|carryable|turret> <id|name> [<x> <y> <z>]`, resolving rows by numeric id **or** by (multi-word, case-insensitive) localized name, backed by the new `StaticDB/SDBCatalog.cs` and `Systems/Spawning/SDBSpawner.cs`. Turret spawning from a command is new; monsters/deployables/vehicles/carryables keep their existing typed commands too
- Add `sdb` (browse/search the ~7,400 spawnable rows) and `sdbinfo` (dump a single row's gameplay fields) commands, both in chat and on the Admin channel
- Add `spawnables` and `coverage` subcommands to `Tools/SdbDump`, mirroring the in-game catalog offline and reporting how much of `clientdb.sd2` PIN's loader actually reads (240 of 575 tables, 53.5% of rows for build `prod-1962`)
- Add `Docs/STATIC_DATABASE.md` documenting the `.sd2` format, PIN's table coverage, the unread tables that are the obvious next targets, and the new spawn/browse commands; extend `Docs/MOBS_AND_NPCS.md` with the vehicle and carryable catalogs
- Add `Tools/SdbDump`, a standalone decoder for `clientdb.sd2` (port of the FauFau StaticDB format logic PIN loads data through, verified by a round-trip self-test), and `Docs/MOBS_AND_NPCS.md` cataloging every mob/NPC in the database (build `prod-1962`: 3,109 monster rows, 1,772 of them named, grouped by faction) plus turrets and the per-level scaling table
- Add support for StyleCop & .NET Analyzers
- Use RIN via gRPC for the player management
- Add many items to the game
  - Deployables
  - Battlestation
  - Vehicles
  - Gliders
  - Abilities
  - Thumbers
  - Turrets
  - Melding Repulsor
- Add Bepu as physics engine
- Auto-detect the Firefall installation and populate `GameServer.config.json` paths on startup (via Steam libraries, `libraryfolders.vdf`, or `PIN_FIREFALL_PATH` / `PIN_STEAM_PATH`)
- Implement the server side of the gRPC `GameServerAPI` (`WebHost.GameServerApi`, hosted by `WebHostManager` on port 5201), so the GameServer can actually load character data instead of always falling back
- Persist characters to `characters.json`, shared by the character selection screen and the GameServer
- Persist the battleframe picked in-game, so switching frames survives a relogin and shows up in the character selection screen

### Changed

- `Tools/SdbDump`: `spawnables` now decrypts only the localized strings the printed rows reference instead of building the whole 175k-row `dblocalization::LocalizedText` map, which takes the run from ~15 min down to ~15 s (output is byte-identical)
- Update build pipeline to support .NET 8 & 9 and the latest macOS version
- Update most dependencies

### Fixed

- Fix the first hit after a respawn instantly downing the character again: `NetworkPlayer.Respawn` wrote the reset health only into the replicated controller props while the entity stayed at 0 HP; it now resets vitals through `CharacterEntity.SetMaxHealth`/`SetCurrentShields` so server state and client view agree
- Keep ability energy client-simulated like the live game: abilities do not cost (or get gated by) the regular energy pool - only the jetpack/thrust drains it. Client-environment aptitude energy commands (`RequireEnergy`, `ConsumeEnergy`, `ConsumeEnergyOverTime`, `RequireEnergyByRange`) load as no-ops on the server again, and the hardcoded per-activation fallback costs were removed
- Derive the replicated jetpack energy parameters (`EnergyParams`: max, recharge rate, recharge delay) from the equipped battleframe's SDB record whenever a loadout is applied
- `DamageSystem` no longer damages or heals characters that are not in the `Living` state (corpses and bleeding out characters were fully damageable before), and `EntityDamagedEvent`/`EntityHealedEvent` are only published when the damage/heal actually applied
- `CharacterLifecycleService` only transitions out of the `Living` state now, so stray damage events can no longer skip the bleedout phase or double-fire death transitions
- Respawn and teleport reset the fall damage tracker, so stale fall speeds cannot deal damage at the destination
- Wire the aptitude register pipeline so data-driven amounts are computed correctly: `LoadRegisterFromStat` (aptitude stat modifiers, e.g. energy/cooldown), `LoadRegisterFromModulePower` (ability module power rating), and `PushRegister`/`PopRegister`/`PeekRegister` are now implemented and resolved in `Factory`; chains that multiply an SDB amount by a loaded value no longer collapse to 0
- Mirror the client recharge model in the server `AbilityState`: regeneration waits `EnergyParams.Delay` after the last spend, an overcharged (negative) pool keeps recharging back through zero, and `EnergyToDamage` converts the actual tracked pool instead of assuming a full one
- Implement the `TargetDifference` aptitude command (type 101, "Target - Difference", 21 chain nodes in build `prod-1962`): it loaded as a placeholder that only shuffled the two target lists around without ever subtracting anything, so both chain shapes that use it kept their full target list. It now drops every target that appears in both the current and the former list, honouring `SwapCurrentFormer` (subtract the current list from the former one instead, for the layered `TargetConeAE` blasts that need the ring between an outer and an inner volume) and `ReplaceFormer` (keep the unfiltered list as the former one). Periodic area chains that pop the previously hit set into the former list therefore act on the entities that *entered* the area since the last tick instead of re-applying to everybody standing in it, and ring blasts no longer hit their inner volume once per ring. Covered by unit tests in `GameServer.Tests`
- Implement the `RequireInRange` aptitude command (type 81, "Requirement - In Range"): it loaded as an always-succeed placeholder, so chains gated on staying near their target (tethered buffs, beam-style heal/repair effects, interaction and NPC follow-up chains) kept running no matter how far the target ran off; it now fails when any current target is further than the def's `Range` from the entity running the chain, honours the `Negate` flag like the other requirement commands, leaves the target list untouched (that is `TargetFilterByRange`'s job) and keeps succeeding when the chain has no targets. Covered by unit tests in `GameServer.Tests`
- Implement the `TargetByExists` aptitude command (type 134, "Target - Filter Existing Objects", 135 chain instances in build `prod-1962`): it loaded as a placeholder that kept every target, so chains holding a target over time (effect update/duration chains, deployables, called-down vehicles, NPC chains) went on acting on entities that had already despawned; it now keeps only the targets still registered with the shard, moving the pre-filter list to the former targets like the other target filters
- Implement the `ResetCooldowns` aptitude command (type 209, "Cooldown - Reset Abilities", 18 chain instances in build `prod-1962`): it loaded as a placeholder, so items/abilities that reset ability cooldowns did nothing; it now clears every tracked local, category and global cooldown of the current targets (falling back to the entity running the chain), and the client timers refresh with the next ability-activation response since that payload is built after the chain ran
- Start activation cooldowns only after the whole ability chain succeeded (queued by `InstantActivation`, committed by `AbilitySystem.HandleActivateAbility`), so an ability that fails a later requirement does not go on cooldown
- Fix activated abilities (Raptor and every other battleframe) staying purely cosmetic: `InstantActivation`, the chain node that carries each ability's cooldown configuration, was an empty stub, so no cooldown was ever started, the client got an empty cooldown payload and the ability could be re-pressed forever
- Track the aptitude cooldown category per ability (learned from its activation command) instead of using `AbilityModule.UiCategory`, which is a UI grouping and never matched a real cooldown category
- Report category cooldowns to the client (`ActiveCooldowns_Group2`) and express the global cooldown window in shard time, so the client-side ability timers no longer jump
- Log aptitude chain nodes that are still unimplemented server-side, so an ability that plays its animation but does nothing can be traced to the exact placeholder command
- Fix the character selection screen and the in-game character not matching (selection showed a female Raptor while the game spawned a male Mammoth, because the two were built from separate hardcoded blobs and the gRPC lookup that was meant to reconcile them had no server implementation)
- Fix Firefall auto-detection failing for every Steam library listed in `libraryfolders.vdf` (Steam stores the library root folder such as `D:\SteamLibrary`, not the `steamapps` folder, so all entries were rejected)
- Find Steam installations outside `Program Files` through the Windows registry, and standalone Firefall installs under `Program Files`
- Stop empty values in `GameServer.config.json` from overriding paths configured in the legacy `App.config`
- Print a readable startup error instead of an unhandled Autofac exception when no Firefall installation is found and `StaticDBPath` is not configured
- Make the server handling code more robust
- Fix WebHostManager startup crash (missing `Serilog.Enrichers.Context` and other Serilog extension assemblies when the servers are published into a single folder) by referencing the shared Serilog packages
- Many improvements in the entity definitions

## [1.2.0] - 2023-06-02

### Added

- Add support for calling down a LGV
- Add documentation to explain the architecture
- Use Autofac for dependency injection in UdpHosts
- Add basic endpoint for character creation handling
- Add or extend the API endpoints
  - api/v2/accounts/current/status
  - api/v2/accounts/character_slots
  - api/v3/characters/{character_id}/garage_slots
  - api/v3/ui_actions
  - api/v1/characters/{character_id}/data
  - api/v3/characters/{character_id}/inventories/bag
  - api/v3/characters/{character_id}/inventories/gear/items
  - api/v1/zones/queue_ids
  - api/v2/zone_settings
  - api/v1/item_display_attributes
  - api/v1/market_categories
  - api/v1/characters/validate_name
  - api/v3/characters/{characterId}/titles
  - api/v3/characters/{characterId}/garage_slots/{frameId}/perks
  - ...and more
- Use AeroMessages as submodule instead of a binary reference
- Use range indexer
- Modernize code base with support from Rider auto format and clean up
- Handling of the Steam user id. Currently, it is only held internally and not persisted.
- Basic GitHub Action to ensure continuous integration
- Individual .bat files for each game service pointing directly to the respective `bin\debug` folder.
- Configuration for the game server. You can edit the `App.config` file in the `GameServer` project root for local settings and add working defaults in `App.Default.config`.
  `App.config` will be generated from `App.Default.config` if not present before build.
  Currently, the only option present is the Serilog log level.
- CLI options parsing. Currently, the only option is the log level. Specifying wrong options will not stop the server from starting.
- 404 handling to the web server pipeline which prints the contents of 404-producing requests as warnings to see what's missing.
- ClientEventController with a corresponding endpoint to receive the events from the client. Currently, only the client uptime seems to be posted on exit of the game

### Changed

- Jets rendering correctly
- Use AeroMessages for nearly all packets
- Clean up some of the code flow, for easier understanding
- Use long speaking names for variables
- Started transition to AeroMessages, this is an incremental process
- Replace SharedAssemblyInfo with a targets file
- Update to .NET 6
- Changed 'missing MSGid' logging to include the details (1st message) in log level warning instead of verbose
- Update documentation regarding the usage of web hosts
- Turn Firefall specific location finder to a common location provider

### Fixed

- Fix IndexOutOfRangeException being thrown by the Matrix server
- Fix string deserialization and corrected Matrix Login packet

## [1.1.0] - 2021-10-09

### Added

- Characters for each available zone with usable spawn location

## [1.0.0] - 2021-10-06

### Added

- MatrixServer to handle client connection establishment and hand off to GameServer
  - Supports all five packets: ABRT, HEHE, HUGG, KISS, POKE
- GameServer to handle map zoning and basic character movement
  - Zone into New Eden
  - Spawn on a Watch Tower
  - Have a pre-defined set of Visuals
  - Use your Primary and Secondary Weapon (sometimes it doesn't work)
  - Run and sprint around the whole map
- WebHostManager to deal with standard web requests from the client through different WebHosts
  - Handle login requests via hardcoded Oracle ticket
  - Provide hardcoded account details
  - Serve the necessary Host Information
  - Return static assets when provided by the user
