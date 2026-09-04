# Mobs & NPCs — Database Catalog

This document is the reference for every mob / NPC definition available in
Firefall's static database (`clientdb.sd2`) and how PIN turns those rows into
living entities. The *mechanics* of spawning (chat commands, per-zone JSON,
combat gating) live in [SPAWNING_AND_COMBAT.md](SPAWNING_AND_COMBAT.md); the
file format, PIN's coverage of it and the generic in-game database commands
live in [STATIC_DATABASE.md](STATIC_DATABASE.md). This document is about
**the data itself**.

Anything listed here can be spawned directly by name or id:

```
\sdb monster aranha           # search the catalog in-game
\sdbinfo monster 2435         # inspect the row
\spawn monster Aranha Queen   # spawn it
```

> Decoded from Firefall build **prod-1962** with `Tools/SdbDump` (see §4.7).

---

## 1. Where mob data lives

All NPC definitions live in the SDB file the server loads at startup
(`StaticDBPath` in `GameServer.config.json`, normally
`Firefall/system/db/clientdb.sd2`). The relevant tables are:

| Table                          | Contents                                                        |
|--------------------------------|-----------------------------------------------------------------|
| `dbcharacter::Monster`         | **The mob/NPC catalog.** One row per character type.             |
| `dbcharacter::MonsterScaling`  | Per-level health/damage scaling rows referenced by a monster.    |
| `dbcharacter::MonsterAttributeRange` | Attribute curves (base / per-level / module effect).       |
| `dbcharacter::MonsterTitle`    | Title ids a monster row can reference.                           |
| `dbcharacter::MonsterMood` / `MonsterMoodName` | Ambient mood/animation sets.                     |
| `dbcharacter::MonsterVisualOption(s)` | Random visual variants (heads, colors) per monster.      |
| `dbcharacter::Faction`         | Faction ids (`internal_name`, localized display name).           |
| `dbcharacter::FactionRelations`| Stance matrix between factions (hostile/friendly/neutral).       |
| `dbcharacter::Turret`          | Placeable turret characters (and `TurretWeapon` their guns).     |
| `dbcharacter::Deployable`      | Deployables (battleframe station, thumper beacons, ...).         |
| `dblocalization::LocalizedText`| `id -> English` strings; monster/faction names resolve here.     |
| `dbitems::Battleframe`         | Chassis records — monster rows point at one via `chassis_id`.    |
| `dbitems::WeaponTemplates`     | Weapons referenced by `weapon1_id` / `weapon2_id`.               |

A monster row itself carries no plain-text name: `localized_name_id` is a key
into `dblocalization::LocalizedText`, and `faction_id` keys `dbcharacter::Faction`
(which again points into localization for its display name).

## 2. Anatomy of a `dbcharacter::Monster` row

Mirrors `UdpHosts/GameServer/StaticDB/Records/dbcharacter/Monster.cs` (column
names are the snake_case forms PIN's loader looks up):

| Column (group)          | Meaning                                                              |
|-------------------------|----------------------------------------------------------------------|
| `id`                    | The character type id used everywhere (`\npc <id>`, spawn JSON).      |
| `localized_name_id`     | Key into `dblocalization::LocalizedText` for the display name.        |
| `faction_id`            | Faction row; drives the hostility stance against players.             |
| `race`, `gender`        | Census bytes (`gender` is the ASCII `'M'`/`'F'`).                     |
| `chassis_id`            | `dbitems::Battleframe` chassis: body/visuals and the jetpack energy params (max/recharge/delay) PIN replicates from it. |
| `backpack_id`           | Backpack visual slot.                                                 |
| `weapon1_id`, `weapon2_id` | `dbitems::WeaponTemplates` entries in loadout slots Primary/Secondary. |
| `head_id`, `eyes_id`, `head_acc1_id`, `head_acc2_id`, `charinfo_id` | Head/face visual build. |
| `*_color` (skin, lip, eye, hair, facial hair) | Color ids for the visuals block.                  |
| `*_warpaint_palette_id` (fullbody, armor, bodysuit, glow) | Warpaint palettes (`dbvisualrecords::WarpaintPalette`). |
| `ornaments_map_group_id_1..4` | Ornament groups (event hats, etc.).                            |
| `visual_options_id`, `visuals_group_id` | Random visual variant sets.                          |
| `behavior`, `behavior_offensive`, `behavior_defensive` (+ `*_instance_id`) | CAIS behavior set names/ids (combat AI). |
| `health_regen`          | Out-of-combat health regeneration.                                    |
| `scaling_table_id`      | `dbcharacter::MonsterScaling` set: level -> health/damage.            |
| `loot_table_id`, `loot_table2_id` | Loot rolls on death.                                        |
| `xp_resource_id`, `xpreward_type` | Reward grants on kill.                                      |
| `normal_speed`, `fast_speed`, `body_radius`, `body_mass`, `body_height` | Movement / physics shape. |
| `min_rand_scale`, `max_rand_scale` | Random model scale range.                                  |
| `ai_spawn_delay_ms`     | Delay before AI activates after spawn.                                |
| `respawn_flags`, `gravity`, `is_componented`, `damage_response_id`, `posetype_id`, `voice_set`, `title`, `vendor_id`, `terminal_type_name`, `crafting_type_id`, `network_fidelity`, `difficulty_cost`, `projectile_offset` | Misc simulation/display knobs. |

## 3. How PIN turns a row into an entity

```
CustomData/character_spawn.json | \npc 290 | admin "npc 290"
  -> EntityManager.SpawnCharacter(typeId, position)
       -> CharacterEntity.LoadMonster(typeId)        (CharacterEntity.cs)
            SDBInterface.GetMonster(typeId)          (dbcharacter::Monster row)
            SDBUtils.GetChassisWarpaint(...)         (visual palettes)
            CharacterLoadout { chassis, backpack, weapons }
            SetStaticInfo   { NameLocalizationId, Race, Gender, TargetFlags.IsNPC, ... }
            SetHostilityInfo{ FactionId }            (stance vs. players)
            ApplyLoadout    (replicates visuals + battleframe energy params)
       -> physics kinetic body, CharacterLifecycle.OnCharacterCreated
```

- The name shown client-side resolves through `NameLocalizationId`; the server
  itself never needs the string.
- The chassis lookup (`SDBInterface.GetBattleframe`) is also what feeds the
  replicated **jetpack** `EnergyParams` (max / recharge / delay) — the only
  energy pool in the game; abilities do not consume it.
- On death: `DamageSystem` -> `CharacterLifecycleService` (`CharacterDiedEvent`)
  -> `NpcDeathService` (gib visuals, 10 s corpse linger by default).
- Display names are now resolvable server-side:
  `SDBInterface.GetLocalizedString(monster.LocalizedNameId)` reads
  `dblocalization::LocalizedText`, which is what the `sdb` / `sdbinfo` /
  `spawn <kind> <name>` commands use.
- NPC behavior strings (`behavior*`) are **not simulated yet** — spawned mobs
  stand idle until shot; there is no chase/attack AI server-side.

## 4. What the database actually contains (catalog)

Decoded from the real `clientdb.sd2` of Firefall build **prod-1962** — the exact file layout the PIN server loads (`dbcharacter::Monster` + friends). Totals:

| Metric | Value |
|--------|-------|
| `dbcharacter::Monster` rows | **3,109** |
| Rows with a display name (via `LocalizedText`) | **1,772** |
| Rows without a name (tooling/placeholder variants) | 1,337 |
| Factions represented (named rows) | 17 |
| Turrets (`dbcharacter::Turret`) | 107 |
| Deployables (`dbcharacter::Deployable`) | 3,902 (2,088 named) |
| Vehicles (`vcs::VehicleInfo`) | 173 (all named) |
| Carryables (`dbitems::CarryableObject`) | 105 (71 named) |
| Levels of monster scaling (`dbcharacter::MonsterScaling`) | 80 |

### 4.1 Race legend

| Race byte | Meaning (from representative rows) |
|-----------|------------------------------------|
| 0 | Human |
| 2 | Chosen |
| 6 | Misc (drones, targets, Necronus) |
| 7 | Companion/critter |
| 8 | Melded creature |
| 9 | Wildlife (bugs) |
| 10 | Humanoid outlaw |
| 11 | Large wildlife |
| other | unset / unused for that row |

### 4.2 Mobs & NPCs by faction

Every named monster row, grouped by its `Faction.internal_name`. IDs are the character type ids used by `\npc <id>` and `character_spawn.json`.

#### accord — 1042 named (+689 unnamed)

| id | name | race |
|----|------|------|
| 2 | NO MONSTER | Human |
| 4 | Yellow Shirt | Human |
| 143 | Battlebot | Human |
| 200 | Accord SIN Amplifier | Human |
| 207 | Typhon | Human |
| 208 | Mourningstar | Human |
| 242 | Accord Recon | Human |
| 283 | Accord Recon | Human |
| 285 | CORAL - Accord Black M Engineer - Variation 1 | Human |
| 288 | Accord Engineer | Human |
| 290 | Accord Assault | Human |
| 291 | CORAL - Accord Black M Assault - Variation 1 | Human |
| 292 | CORAL - Accord Brazilian M Assault - Variation 1 | Human |
| 293 | Accord Assault | Human |
| 294 | CORAL - Accord Asian F Assault - Variation 1 | Human |
| 295 | CORAL - Accord Black F Assault - Variation 1 | Human |
| 297 | Accord Assault | Human |
| 299 | CORAL - Resident Black M - Variation 1 | Human |
| 302 | CORAL - Resident Asian F - Variation 1 | Human |
| 303 | CORAL - Resident Black F - Variation 1 | Human |
| 304 | CORAL - Resident Brazilian F - Variation 1 | Human |
| 305 | CORAL - Resident White F - Variation 1 | Human |
| 315 | CORAL - Accord Asian F Engineer - Variation 1 | Human |
| 316 | Accord Engineer | Human |
| 319 | Accord Engineer | Human |
| 320 | CORAL - Resident Black M - Variation 3 | Human |
| 356 | Aero | Human |
| 358 | Oilspill | Human |
| 364 | CORAL - Accord Asian F Medic - Variation 1 | Human |
| 369 | CORAL - Resident Asian F - Variation 3 | Human |
| 371 | CORAL - Resident Brazilian F - Variation 3 | Human |
| 372 | CORAL - Resident White F - Variation 3 | Human |
| 393 | Engineer Turret Gunner | Human |
| 397 | Battleframe OS | Human |
| 497 | Accord Dreadnaught | Human |
| 502 | Ratchet | Human |
| 503 | Accord Soldier | Human |
| 510 | Lt. Shanafelt | Human |
| 511 | Sergeant Choi | Human |
| 515 | Ikinya | Human |
| 516 | Consul Nostromo | Human |
| 556 | Accord Command | Human |
| 557 | CDR. Price | Human |
| 566 | Accord Soldier | Human |
| 570 | Luau Larry | Human |
| 605 | Science OFC Barness | Human |
| 606 | Frost | Human |
| 607 | El Terremoto | Human |
| 608 | Dynamo | Human |
| 610 | Holmgang Security | Human |
| 620 | Marcelo | Human |
| 621 | Dick Allen | Human |
| 624 | Marcia the Flame | Human |
| 629 | Corporal Garland | Human |
| 630 | Claudia Fonseca | Human |
| 646 | Accord Recruitment | Human |
| 647 | Copa Power Supply | Human |
| 648 | Mayor Palmeiro | Human |
| 649 | ARES Team | Human |
| 650 | Arclight Rescue | Human |
| 651 | Dismantling the Arclight | Human |
| 652 | Admiral Nostromo | Human |
| 653 | Missing Shipment | Human |
| 654 | The Chosen | Human |
| 655 | SIN Hacking | Human |
| 658 | Ol' Man Bill | Human |
| 659 | Shady Ad | Human |
| 660 | Trolling Oilspill | Human |
| 661 | Thumper History | Human |
| 662 | Oilspill | Human |
| 663 | The Aegis | Human |
| 664 | Mustang's Memo | Human |
| 665 | Holmgang Show | Human |
| 666 | Omnidyne Commercial | Human |
| 667 | Captain's Log | Human |
| 668 | Earth First | Human |
| 669 | Scientific Progress | Human |
| 670 | Sabotage | Human |
| 671 | Global Warming | Human |
| 672 | Walking Tour | Human |
| 673 | Pickle's Diary | Human |
| 674 | Brontodon Poem | Human |
| 675 | Sloshy Stan | Human |
| 676 | Poaching | Human |
| 677 | SIN Imprint Conspiracy | Human |
| 678 | Ricardo's Concerns | Human |
| 679 | Quarantine Measures | Human |
| 680 | Monohan's Rebuttal | Human |
| 681 | Outbreak | Human |
| 682 | Security Lockdown | Human |
| 683 | SIN Excision | Human |
| 684 | Smuggling Affinites | Human |
| 685 | Identity Gift | Human |
| 686 | Rebellion | Human |
| 687 | Affinite Bounty | Human |
| 688 | Brody | Human |
| 701 | Heavy Turret - Gunner | Human |
| 712 | Female Tutorial Player Character | Human |
| 721 | Control Worker | Human |
| 722 | Control Worker | Human |
| 723 | Accord Guard | Human |
| 724 | Researcher | Human |
| 725 | Researcher | Human |
| 726 | Researcher | Human |
| 727 | Researcher | Human |
| 728 | Researcher | Human |
| 729 | Researcher Engineer | Human |
| 730 | Dock Foreman | Human |
| 731 | Dock Foreman | Human |
| 732 | Drill Sergeant | Human |
| 733 | Nostromo Body Guard | Human |
| 734 | Recruit | Human |
| 735 | Battlelab Guard | Human |
| 736 | Soldier | Human |
| 737 | Soldier | Human |
| 738 | Senior Officer | Human |
| 739 | Officer Sr. Assistant | Human |
| 741 | Lazy Engineer | Human |
| 743 | Battlelab Engineer | Human |
| 744 | Battlelab Working Engineer | Human |
| 745 | Officer Jr. | Human |
| 746 | Officer Jr. | Human |
| 747 | Friendly Target Drone | Human |
| 748 | Simulated Incapacitated Friendly | Human |
| 749 | Simulated Incapacitated Friendly | Human |
| 750 | Inventory Taker | Human |
| 755 | Technician engineer | Human |
| 756 | Mechanic | Human |
| 757 | Technician | Human |
| 758 | lazy mechanic | Human |
| 766 | Oilspill | Human |
| 767 | Corporal Jaso | Human |
| 768 | Amancio Rios | Human |
| 769 | Axel | Human |
| 770 | Dross | Human |
| 772 | Hawking | Human |
| 773 | Ol' Man Bill | Human |
| 774 | Jun Mori | Human |
| 777 | Snorri V. | Human |
| 778 | Mule | Human |
| 779 | Hamid Nejem | Human |
| 780 | Mitch "Alligator" Freise | Human |
| 781 | Gus Walker | Human |
| 782 | Sergio | Human |
| 783 | Iolanda | Human |
| 784 | Chartreuse | Human |
| 786 | Corporal Rakes | Human |
| 787 | Sgt. Maxine Hammer | Human |
| 788 | Captain Fredericks | Human |
| 789 | Trippy | Human |
| 791 | Dr. Beatrix Jardine | Human |
| 792 | Nurse Franco | Human |
| 793 | Susan Bartle | Human |
| 795 | Lieutenant Dale Truman | Human |
| 796 | Otter | Human |
| 797 | Chief Nigel Lewis | Human |
| 798 | Vitor Martin | Human |
| 799 | Lieutenant Namgung | Human |
| 800 | Consul Nostromo | Human |
| 801 | Commander Samuel Burke | Human |
| 802 | Admiral Jason Archinaco | Human |
| 803 | Major Paulo Silva | Human |
| 804 | Commodore Annabel Mundy | Human |
| 805 | Staff Sergeant Thane Fisher | Human |
| 807 | Corporal Brice Raines | Human |
| 809 | Riptide | Human |
| 810 | Blanca Gomes | Human |
| 811 | Ines Belo | Human |
| 813 | Corporal Gavin Butler | Human |
| 815 | Lieutenant Tim Daniels | Human |
| 816 | Sergeant Zeus McClellan | Human |
| 818 | Corporal Fiona Boyle | Human |
| 819 | Lieutenant Sarah Chevelle | Human |
| 822 | Indra Rodrigues | Human |
| 824 | Corporal Leticia Delgado | Human |
| 826 | Christobel | Human |
| 831 | Teobalde Palmeiro | Human |
| 832 | Jose Vargas | Human |
| 833 | Joana Medeiros | Human |
| 835 | Alex Sundal | Human |
| 836 | Spicy Al | Human |
| 841 | Sergeant Cleve Wolfe | Human |
| 842 | Lieutenant Jim Davies | Human |
| 844 | Olivia Ferro | Human |
| 845 | Rubin Gallagher | Human |
| 846 | Sergeant Sam White | Human |
| 847 | Sapphire Gallagher | Human |
| 848 | Dwayne Tucker | Human |
| 850 | Norma Tilda | Human |
| 851 | Private Donovan Davis | Human |
| 852 | Lieutenant Donald Abram | Human |
| 853 | Dustin Baloc | Human |
| 854 | Private Cecilia Abello | Human |
| 855 | Rafaela Silva | Human |
| 857 | Turan | Human |
| 858 | Mustang | Human |
| 859 | Marco Machado | Human |
| 860 | The Duque | Human |
| 861 | Georgio Germaine | Human |
| 862 | Cal Denman | Human |
| 863 | Veneno | Human |
| 865 | Eleuterio Crespo | Human |
| 867 | Captain Heliodoro Ribeiro | Human |
| 868 | Charlie Bravo | Human |
| 869 | Nestor | Human |
| 870 | Rudolfo | Human |
| 871 | Mayor Luciana Serafim | Human |
| 872 | Seti | Human |
| 873 | Antonia Campos | Human |
| 874 | Jurgen | Human |
| 876 | Corporal Greyson Cadwaller | Human |
| 877 | Corporal Jon Joyner | Human |
| 878 | Lieutenant Abel Costa | Human |
| 879 | Crispino Largo | Human |
| 881 | Lieutenant Maria Garcia | Human |
| 882 | Kia Sofia | Human |
| 883 | Atlas | Human |
| 885 | Private Yu | Human |
| 900 | Reactor PA System | Human |
| 907 | Lieutenant Chelsea Maclean | Human |
| 908 | Private Shelby Gladwyn | Human |
| 909 | Sergeant Dominick Atkinson | Human |
| 911 | Abducted Civilian | Human |
| 914 | Vacationer | Human |
| 916 | Vacationer | Human |
| 917 | Vacationer | Human |
| 918 | Vacationer | Human |
| 921 | Vacationer | Human |
| 922 | Vacationer | Human |
| 945 | Vacationer | Human |
| 946 | Vacationer | Human |
| 947 | Copacabana Injured Accord Soldier - Pathing | Human |
| 953 | Supply Officer Tomas | Human |
| 954 | Supply Officer McFinn | Human |
| 956 | Supply Officer Cross | Human |
| 957 | Supply Officer Hooker | Human |
| 958 | Supply Officer "Jabs" | Human |
| 960 | Blackwater Anomaly | Human |
| 961 | Sergeant Choi | Human |
| 966 | InfoBot | Human |
| 973 | Arcfold Security | Human |
| 974 | Arcfold Security | Human |
| 975 | Arcfold Engineer | Human |
| 976 | Arcfold Engineer | Human |
| 977 | Arcfold Security | Human |
| 978 | Arcfold Security | Human |
| 979 | Arcfold Security | Human |
| 980 | Arcfold Security | Human |
| 987 | Arcfold Engineer | Human |
| 988 | Arcfold Engineer | Human |
| 1006 | Omnidyne-M Rep | Human |
| 1018 | Jerrod Langley | Human |
| 1019 | Brandon Reeve | Human |
| 1020 | Samuel Burrell | Human |
| 1021 | Pearl Terry | Human |
| 1022 | Cyndi Gaye | Human |
| 1023 | Alicia Breckinridge | Human |
| 1025 | Anna Kristel | Human |
| 1032 | Decoy NPC | Human |
| 1052 | Adm. Curtis Mokiao | Human |
| 1053 | Landing Pad Engineer | Human |
| 1054 | Landing Pad Engineer | Human |
| 1055 | Landing Pad Engineer | Human |
| 1059 | Oilspill | Human |
| 1062 | Capt. Hudson Fuller | Human |
| 1067 | Statue NPC | Human |
| 1174 | Accord Merit Quartermaster | Human |
| 1175 | Accord Merit Quartermaster | Human |
| 1176 | Accord Merit Quartermaster | Human |
| 1177 | Accord Merit Quartermaster | Human |
| 1190 | Vic the Crow | Human |
| 1194 | Rico | Human |
| 1195 | Accord Dropship Pilot | Human |
| 1205 | Devilhawk Paratrooper | Human |
| 1206 | Rico | Human |
| 1207 | Joe | Human |
| 1208 | Mara | Human |
| 1209 | Hobbes | Human |
| 1214 | Accord Officer | Human |
| 1215 | Accord Officer | Human |
| 1216 | Accord Officer | Human |
| 1217 | Accord Officer | Human |
| 1218 | Sgt. Torres | Human |
| 1221 | Rivers | Human |
| 1223 | Horus | Human |
| 1250 | Frank | Human |
| 1251 | Oilspill | Human |
| 1253 | Father Wintertide | Humanoid outlaw |
| 1256 | Chiba | Human |
| 1257 | Fade | Human |
| 1267 | Mustang | Human |
| 1268 | Lieutenant Sharpe | Human |
| 1318 | BattleCruiser Turret Gunner Main Cannon | Human |
| 1321 | Science OFC Nakamura | Human |
| 1324 | Courier Alpha | Human |
| 1326 | Accord Scientist Team Leader | Human |
| 1332 | Accord Quartermaster | Human |
| 1333 | Accord Quartermaster | Human |
| 1334 | Accord Quartermaster | Human |
| 1335 | Accord Quartermaster | Human |
| 1352 | Dropship Pilot | Human |
| 1353 | Oilspill | Human |
| 1354 | Vic the Crow | Human |
| 1355 | The Ringer | Human |
| 1357 | The Indexer | Human |
| 1358 | Dexter Greer | Human |
| 1359 | The Meddler | Human |
| 1361 | Ol' Man Bill | Human |
| 1362 | Hawking | Human |
| 1363 | Atlas | Human |
| 1368 | Doctor Abrams | Human |
| 1369 | Sarah | Human |
| 1370 | Derkas | Human |
| 1371 | Sergeant Wilcox | Human |
| 1372 | Accord Coroner | Human |
| 1374 | Sergeant Lewis | Human |
| 1389 | Capt. Patel | Human |
| 1391 | Accord Soldier | Human |
| 1394 | Captain Abrams | Humanoid outlaw |
| 1395 | Accord Guard | Human |
| 1406 | Convoy Driver | Human |
| 1407 | Arturs the Mechanic | Human |
| 1412 | Missing Daughter | Human |
| 1413 | Distraught Father | Human |
| 1416 | Security Chief Delgado | Human |
| 1424 | Dross | Human |
| 1428 | Accord Paratrooper | Human |
| 1429 | Crank | Human |
| 1431 | Omnidyne-M Rep | Human |
| 1433 | Lorenzo's Sister | Human |
| 1434 | Lorenzo's Mother | Human |
| 1435 | Robby | Human |
| 1436 | Accord Soldier | Human |
| 1439 | Grizli | Human |
| 1440 | Distraught Wife | Human |
| 1441 | Carter | Human |
| 1445 | Courier Gamma | Human |
| 1447 | Carlo Fonseca | Human |
| 1451 | Civilian | Human |
| 1452 | Civilian | Human |
| 1453 | Civilian | Human |
| 1454 | Civilian | Human |
| 1455 | Civilian | Human |
| 1456 | Civilian | Human |
| 1457 | Civilian | Human |
| 1458 | Civilian | Human |
| 1459 | Civilian | Human |
| 1460 | Civilian | Human |
| 1461 | Capt. Hudson Fuller | Human |
| 1462 | Accord Soldier | Human |
| 1463 | Accord Soldier | Human |
| 1464 | Accord Soldier | Human |
| 1470 | Remigio Coelho | Human |
| 1472 | Eduardo Coelho | Human |
| 1473 | Ricardo Coelho | Human |
| 1474 | Bernardo Coelho | Human |
| 1475 | Steve Coelho | Human |
| 1478 | Escaped Hostage | Human |
| 1479 | Freed Civilian | Human |
| 1481 | Accord Engineer | Human |
| 1482 | Adrita | Human |
| 1484 | Hobo Nick | Human |
| 1485 | Nutretic Receiver Worker | Human |
| 1486 | Zellick | Human |
| 1487 | Angry Citizen | Human |
| 1488 | Angry Citizen | Human |
| 1489 | Angry Citizen | Human |
| 1496 | Coruja | Human |
| 1498 | Captain Simonis | Human |
| 1499 | Luiz Belo | Human |
| 1501 | Treasure Hunter | Human |
| 1502 | Deana | Human |
| 1503 | Gretchen | Human |
| 1504 | Bjorn | Human |
| 1511 | Capt. Hudson Fuller | Human |
| 1524 | Mustang | Human |
| 1541 | Salvador | Human |
| 1542 | Dado | Human |
| 1543 | Lacidar | Human |
| 1544 | Trevor | Human |
| 1546 | Bryce | Human |
| 1552 | Accord Assistant | Human |
| 1553 | Holmgang Target Drone | Human |
| 1554 | Holmgang Announcer | Human |
| 1556 | Glass Mo | Human |
| 1558 | Holmgang Fan | Human |
| 1561 | Holmgang Fan | Human |
| 1562 | Holmgang Fan | Human |
| 1563 | Holmgang Fan | Human |
| 1564 | Holmgang Fan | Human |
| 1565 | Julius Marks | Human |
| 1566 | Vincent Marks | Human |
| 1567 | Van Uzi | Human |
| 1568 | Juan | Human |
| 1569 | Conus | Human |
| 1570 | Reporter | Human |
| 1571 | Holmgang Fan | Human |
| 1572 | Holmgang Fan | Human |
| 1573 | Holmgang Fan | Human |
| 1574 | Holmgang Fan | Human |
| 1575 | Holmgang Fan | Human |
| 1578 | Astrek Agent | Human |
| 1581 | Adrita | Human |
| 1584 | Scarlet | Human |
| 1586 | Grieving Husband | Human |
| 1587 | El Terremoto | Human |
| 1588 | Sydney | Human |
| 1589 | Echo 81 | Human |
| 1590 | Major Desselhoff | Human |
| 1593 | Mr. Akiyama | Human |
| 1594 | Artis the Mechanic | Human |
| 1600 | Zanmato | Human |
| 1601 | Alex | Human |
| 1602 | Jon | Human |
| 1603 | Chris | Human |
| 1604 | Steph | Human |
| 1606 | Mitty the Gambler | Human |
| 1607 | Sheriff Fairuza Nasseri | Human |
| 1608 | The Buzzard | Human |
| 1610 | Cognac | Human |
| 1611 | Rico | Human |
| 1612 | Rooster | Human |
| 1613 | Dr. Delacroix | Human |
| 1614 | Maria Cantos | Human |
| 1616 | The Scout | Human |
| 1621 | Anton Hall | Human |
| 1622 | Buzzard | Human |
| 1623 | Jerry | Human |
| 1624 | Bob | Human |
| 1625 | Axel | Human |
| 1627 | Jessica | Human |
| 1629 | Jaxson | Human |
| 1632 | Albert Finch | Human |
| 1633 | Dulles | Human |
| 1634 | Kazuo Mori | Human |
| 1635 | Buzzard Pointman | Human |
| 1636 | Yuki Lin | Human |
| 1637 | Vibol Soun | Human |
| 1640 | ARES Soldier | Human |
| 1641 | ARES Commander | Human |
| 1642 | ARES Soldier | Human |
| 1644 | ARES Soldier | Human |
| 1645 | ARES Soldier | Human |
| 1646 | Davis Royer | Human |
| 1647 | ARES Soldier | Human |
| 1648 | Rina Joshi | Human |
| 1649 | Consigliere Sparanzo | Human |
| 1650 | Lt. Hornsby | Human |
| 1652 | Major O'Brien | Human |
| 1660 | Alpha | Human |
| 1661 | Beta | Human |
| 1663 | Tanken Defector | Human |
| 1667 | Nikodemus | Human |
| 1668 | Koralia | Human |
| 1671 | Hal Edwards | Human |
| 1672 | Dr. Calmack | Human |
| 1674 | Accord Scientist | Human |
| 1675 | Accord Scientist | Human |
| 1676 | Accord Scientist | Human |
| 1677 | Accord Scientist | Human |
| 1681 | Omnidyne-M Scientist | Human |
| 1687 | Doctor Abrams | Human |
| 1688 | Tanken Lieutenant | Human |
| 1689 | Commander Kimbase | Human |
| 1690 | Special Agent Hunter | Human |
| 1692 | Tanken Ally | Human |
| 1694 | Tanken Ally | Human |
| 1695 | Zed | Human |
| 1696 | Commander Price | Human |
| 1697 | Dr. Farraday | Human |
| 1699 | Colonel Havel | Human |
| 1701 | Corporal Linhold | Human |
| 1703 | Omnidyne-M Employee | Human |
| 1705 | June Harper | Human |
| 1706 | Billy the Bullet | Human |
| 1708 | Omnidyne-m Heavy Turret | Human |
| 1709 | Supply Officer Booker | Human |
| 1710 | Supply Officer Cook | Human |
| 1725 | Albert Smith | Human |
| 1726 | Phil Mason | Human |
| 1727 | Lexie Smith | Human |
| 1731 | SFC Terry Sugarman | Human |
| 1792 | Astrek Courier | Human |
| 1800 | ARES Soldier | Human |
| 1801 | Turner Jones | Human |
| 1802 | Razor | Human |
| 1805 | Sergeant James Mansfield | Human |
| 1806 | Lieutenant Julian Friese | Human |
| 1807 | Sergeant Burton | Human |
| 1814 | Sergeant Rice | Human |
| 1820 | Jace Jackson | Human |
| 1821 | Supply Officer Chan | Human |
| 1822 | Supply Officer Stocks | Human |
| 1823 | Supply Officer "Shadow" | Human |
| 1825 | Private Richard Furtado | Human |
| 1826 | Private Lee Palmer | Human |
| 1827 | Lieutenant Kimberley Lyons | Human |
| 1828 | Private Janet Moore | Human |
| 1829 | Private Benny Blair | Human |
| 1830 | Lieutenant Irene Smith | Human |
| 1831 | Private Maria North | Human |
| 1832 | Private Christopher Carter | Human |
| 1833 | Lieutenant Neil Gooding | Human |
| 1834 | SIN Hijacker | Human |
| 1835 | Colonel Havel | Human |
| 1836 | Scientist Alvarez | Human |
| 1840 | Astrek Scientist | Human |
| 1841 | Astrek Scientist | Human |
| 1842 | Astrek Scientist | Human |
| 1855 | Jacques Voclain | Human |
| 1856 | Greasy Hank | Human |
| 1857 | Captain Wallach | Human |
| 1858 | Private Milani | Human |
| 1864 | Shanty Town Civilian | Human |
| 1865 | Commander Volkov | Human |
| 1867 | ARES Pilot | Human |
| 1868 | ARES Pilot | Human |
| 1869 | Stavrevski | Human |
| 1871 | Corporal Rhodes | Human |
| 1872 | Private Harper | Human |
| 1873 | Private Morine | Human |
| 1874 | Private McNeill | Human |
| 1887 | Lieutenant Sadiq | Human |
| 1888 | Commander Auttenberg | Human |
| 1889 | Captain Park | Human |
| 1890 | Master Sgt. Rask | Human |
| 1892 | First Lieutenant Avakian | Human |
| 1893 | Corporal Kawaguchi | Human |
| 1894 | Private Hans | Human |
| 1895 | Anomalous Collections Officer | Human |
| 1896 | Mourningstar | Human |
| 1897 | Typhon | Human |
| 1898 | Scarlet | Human |
| 1899 | Wiley | Human |
| 1900 | Natalia Fedorov | Human |
| 1901 | Jackie "Juice" Greene | Human |
| 1902 | FOB Harpoon Quartermaster | Human |
| 1903 | Crossroads Quartermaster | Human |
| 1904 | Research Station Quartermaster | Human |
| 1905 | Camp Jasper Quartermaster | Human |
| 1906 | Stronghold Quartermaster | Human |
| 1907 | Forest Watch Quartermaster | Human |
| 1909 | Accord Soldier | Human |
| 1910 | Accord Soldier | Human |
| 1911 | Accord Soldier | Human |
| 1912 | Accord Soldier | Human |
| 1913 | Accord Soldier | Human |
| 1914 | Accord Soldier | Human |
| 1920 | Nutretic Technician | Human |
| 1921 | Fulton Kwok | Human |
| 1923 | Ikinya | Human |
| 1924 | Cognac | Human |
| 1925 | _Accord Assault | Human |
| 1926 | Sheriff Fairuza Nasseri | Human |
| 1932 | _Accord Engineer | Human |
| 1933 | Supply Officer Jones | Human |
| 1934 | Supply Officer Schwartz | Human |
| 1935 | Supply Officer "Raptor" | Human |
| 1936 | Supply Officer Bender | Human |
| 1937 | Supply Officer White | Human |
| 1938 | Accord Guard | Human |
| 1939 | Supply Officer Owens | Human |
| 1940 | Supply Officer Wachi | Human |
| 1941 | Supply Officer Smith | Human |
| 1942 | Supply Officer Reed | Human |
| 1943 | Supply Officer Bryson | Human |
| 1944 | Supply Officer Mandel | Human |
| 1945 | Supply Officer Wyland | Human |
| 1946 | Dredge Bartender | Human |
| 1958 | Datascanner Operator 2 - Civilian | Human |
| 1959 | Datascanner Operator 1- Civilian | Human |
| 1960 | Injured ARES Pilot | Human |
| 1961 | _Diamond Head Male Accord Soldier Randomized | Human |
| 1962 | _Diamond Head Female Accord Soldier Randomized | Human |
| 1965 | Dredge Accord Guard | Human |
| 1978 | Adm. Curtis Mokiao | Human |
| 1982 | Capt. Patel | Human |
| 1985 | Scanbot | Human |
| 1986 | Vic the Crow | Human |
| 1987 | Heavy Turret II - Gunner | Human |
| 1988 | Accord VIP | Human |
| 1990 | Shanty Town Civilian | Human |
| 1991 | FOB Sagan Civilian - Male | Human |
| 1992 | Accord Lieutenant | Human |
| 1994 | Shanty Town Shepherd | Human |
| 1996 | FOB Sagan Civilian - Female | Human |
| 2003 | Accord Soldier | Human |
| 2004 | Accord Officer | Human |
| 2005 | Accord Officer | Human |
| 2006 | Accord Engineer | Human |
| 2008 | Accord Soldier | Human |
| 2009 | Accord Soldier | Human |
| 2011 | Accord Guard | Human |
| 2017 | Konaloa Research Base Civilian - Male | Human |
| 2018 | Konaloa Research Base Civilian - Female | Human |
| 2019 | Stronghold Civilian - Male | Human |
| 2020 | Stronghold Civilian - Female | Human |
| 2021 | Accord Medic | Human |
| 2024 | Chosen Scarecrow | Human |
| 2034 | Biosphere Civilian - Male | Human |
| 2035 | Biosphere Civilian - Female | Human |
| 2041 | Foston | Human |
| 2043 | Dr. Francesca Tellano | Human |
| 2045 | ARES Pilot | Human |
| 2047 | Research Assistant | Human |
| 2055 | Civilian | Human |
| 2056 | Civilian | Human |
| 2061 | Accord Scientist | Human |
| 2071 | Accord Soldier | Human |
| 2072 | Accord Soldier | Human |
| 2073 | Accord Soldier | Human |
| 2075 | Civilian Hostage | Human |
| 2076 | Civilian Hostage | Human |
| 2087 | The Witch | Human |
| 2088 | Doctor Lyon | Human |
| 2089 | Sergeant Freeman | Human |
| 2090 | Private Harrison | Human |
| 2093 | Flamestriker Squad Member | Human |
| 2094 | Emerick | Human |
| 2095 | Claire | Human |
| 2096 | Zed | Human |
| 2101 | Christine | Human |
| 2102 | Christobel | Human |
| 2105 | Prisoner | Human |
| 2106 | Prisoner | Human |
| 2118 | Kisuton Rep | Human |
| 2131 | Auto AntiVehicle NPC | Human |
| 2133 | Headless Horseman | Human |
| 2140 | Thanks, Nostromo | Human |
| 2149 | Accord Soldier | Human |
| 2150 | Accord Soldier | Human |
| 2151 | Accord Soldier | Human |
| 2152 | Accord Soldier | Human |
| 2154 | Accord Soldier | Human |
| 2164 | CharacterSelect_Accord Recon | Human |
| 2165 | CharacterSelect_Accord Engineer | Human |
| 2166 | CharacterSelect_Accord Assault | Human |
| 2167 | CharacterSelect_Accord Biotech | Human |
| 2168 | CharacterSelect_Accord Dreadnaught | Human |
| 2169 | Turret | Human |
| 2174 | Omnidyne Representative | Human |
| 2177 | Supply Officer Balanon | Human |
| 2183 | Accord Assault | Human |
| 2184 | Accord Dreadnaught | Human |
| 2191 | ARES Pilot - Assault - Gun | Human |
| 2192 | Accord Soldier | Human |
| 2194 | Accord Soldier | Human |
| 2195 | Accord Soldier | Human |
| 2201 | ARES Pilot - Biotech - Gun | Human |
| 2202 | ARES Pilot - Recon - Gun | Human |
| 2203 | ARES Pilot - Engineer - Gun | Human |
| 2204 | ARES Pilot - Dreadnaught - Gun | Human |
| 2205 | ARES Pilot - Biotech - no Gun | Human |
| 2206 | ARES Pilot - Assault - no Gun | Human |
| 2207 | ARES Pilot - Recon - no Gun | Human |
| 2208 | ARES Pilot - Dreadnaught - no Gun | Human |
| 2209 | Kangbot | Human |
| 2239 | Field Medic | Human |
| 2242 | [PH] Hometree Engineer | Human |
| 2260 | Yukiko Akiyama | Human |
| 2261 | Holmgang Recruiter | Human |
| 2264 | Astrek Associations | Human |
| 2267 | Lt. Stephen "Saint" Muray | Human |
| 2269 | Corporal Belle | Human |
| 2270 | Lt. Amanda Bloom | Human |
| 2271 | Warrant Officer Liu | Human |
| 2274 | Wintertide Vendor | Human |
| 2275 | Lt. Amanda Bloom | Human |
| 2281 | Corporal Belle | Human |
| 2282 | Warrant Officer Liu | Human |
| 2291 | Kara | Human |
| 2292 | Dr. Bathsheba | Human |
| 2302 | Scarlet | Human |
| 2353 | Accord Scientist | Human |
| 2359 | Accord Scientist | Human |
| 2399 | Lt. Cmdr. Kara Novan | Human |
| 2408 | Capt. Hudson Fuller | Human |
| 2409 | Mason Chen | Human |
| 2422 | Technician | Human |
| 2425 | Accord Marine | Human |
| 2426 | Accord Ranger | Human |
| 2427 | Accord Assault | Human |
| 2428 | Accord Dreadnaught | Human |
| 2429 | Accord Nighthawk | Human |
| 2469 | Accord Dropship Pilot | Human |
| 2471 | Lieutenant Cato | Human |
| 2473 | Accord Soldier | Human |
| 2474 | Accord Soldier | Human |
| 2477 | Accord Soldier | Human |
| 2478 | Lieutenant Draper | Human |
| 2479 | Chief Engineer Mullen | Human |
| 2480 | Commander Harrelson | Human |
| 2484 | Spicy Sal | Human |
| 2492 | Accord Private | Human |
| 2493 | Sergeant Bell | Human |
| 2494 | Shared Turret Gunner | Misc (drones, targets, Necronus) |
| 2495 | B.E.L.A.A. | Human |
| 2500 | Sergeant Townsend | Human |
| 2501 | Sergeant Gunner | Human |
| 2502 | Sergeant Skyler | Human |
| 2503 | Mechanic | Human |
| 2527 | [OBSOLETE] Elite Accord Ranger | Human |
| 2557 | Sergeant Goodspeed | Human |
| 2560 | Milo Quattrocchi | Human |
| 2565 | Operations Agent | Human |
| 2570 | Lieutenant Sands | Human |
| 2571 | Eben Faraday | Human |
| 2572 | Operations Agent | Human |
| 2573 | Iona Rhodes | Human |
| 2574 | Corporal Rhodes | Human |
| 2578 | Sergeant Levy | Human |
| 2579 | Accord Soldier | Human |
| 2580 | Accord Soldier | Human |
| 2583 | Theo Pascal | Human |
| 2585 | Corporal Platt | Human |
| 2587 | Sal | Human |
| 2588 | Gal | Human |
| 2589 | Freya | Human |
| 2590 | Ed | Human |
| 2591 | Fran | Human |
| 2592 | Bran | Human |
| 2593 | Jackson | Human |
| 2594 | Robertson | Human |
| 2595 | Leia | Human |
| 2596 | Luke | Human |
| 2597 | Han | Human |
| 2598 | Carrie | Human |
| 2599 | Stella | Human |
| 2600 | Dan | Human |
| 2601 | Krueger | Human |
| 2602 | Luger | Human |
| 2603 | Gary | Human |
| 2604 | Harry | Human |
| 2605 | Pam | Human |
| 2606 | Sam | Human |
| 2607 | Citizen | Human |
| 2608 | Alvaro Cordozo | Human |
| 2609 | Copacabana Accord Soldier - PerformEmote | Human |
| 2611 | Copacabana Civilian - PerformEmote | Human |
| 2612 | Copacabana swimsuit - PerformEmote | Human |
| 2613 | Copacabana Accord Soldier - Ambient - No gun - PerformEmote | Human |
| 2614 | Hydrocore Accord Soldier - Ambient | Human |
| 2616 | Hydrocore ARES Pilot - Ambient - Assault | Human |
| 2617 | Hydrocore - Scientist | Human |
| 2619 | Hydrocore Worker - Ambient Barker | Human |
| 2620 | Copacabana Resort Staff | Human |
| 2622 | Engineer Mechanic | Human |
| 2623 | Omnidyne-M Information Officer | Human |
| 2624 | Hydrocore ARES Pilot - Ambient - Biotech | Human |
| 2625 | Accord Tech Operative | Human |
| 2626 | Hisser Hatchling | Companion/critter |
| 2627 | El Lute | Human |
| 2628 | Harry Pendel | Human |
| 2630 | Accord Soldier - Ambient Pathing | Human |
| 2631 | Nutretic Technician | Human |
| 2646 | Thump Dump Personnel | Human |
| 2648 | Nutretic Technician | Human |
| 2664 | Accord Lieutenant | Human |
| 2672 | Thump Dump Personnel with Scanner | Human |
| 2677 | Broken Shores Technician | Human |
| 2692 | Copacabana Accord Soldier - BasicCivilian_Stationary | Human |
| 2693 | Copacabana Injured Accord Soldier - BasicCivilian_Stationary | Human |
| 2694 | Copacabana Civilian - BasicCivilian_Stationary | Human |
| 2695 | Copacabana Swimsuits - BasicCivilian_Stationary | Human |
| 2696 | Copacabana Accord Soldier - Ambient - No gun - BasicCivilian_Stationary | Human |
| 2723 | Distraught Wife | Human |
| 2724 | Injured Husband | Human |
| 2725 | Parrot Bomber | Humanoid outlaw |
| 2730 | SIN Hack Dealer | Human |
| 2733 | Corporal Garland | Human |
| 2734 | Claudia Fonseca | Human |
| 2735 | Astrek Corporate Representative | Human |
| 2736 | Omnidyne-M Corporate Representative | Human |
| 2738 | Grizli | Human |
| 2739 | Accord Engineer | Human |
| 2741 | Civilian | Human |
| 2742 | Omnidyne Representative | Human |
| 2745 | Curtis the Fisherman | Human |
| 2746 | Derkas | Human |
| 2749 | Fulton Kwok | Human |
| 2750 | Salvador | Human |
| 2751 | Duncan | Human |
| 2752 | Capt. Hudson Fuller | Human |
| 2753 | Mason Chen | Human |
| 2754 | Soldier | Human |
| 2755 | Accord Coroner | Human |
| 2756 | Sergeant Wilcox | Human |
| 2757 | Sarah | Human |
| 2758 | Science OFC Nakamura | Human |
| 2759 | Omnidyne-M Rep | Human |
| 2760 | Captain Leah Tallon | Human |
| 2761 | Petty Officer Third Class Mike P. Patton | Human |
| 2763 | Rebel Lieutenant | Humanoid outlaw |
| 2770 | Rebel Lieutenant | Humanoid outlaw |
| 2773 | Luiz Belo | Human |
| 2776 | Scarlet | Human |
| 2779 | Accord Heavy Marine | Human |
| 2782 | June Harper | Human |
| 2784 | Barfly | Human |
| 2785 | Lt. Cmdr. Kara Novan | Human |
| 2786 | Bartender | Human |
| 2787 | Lieutenant Prince | Human |
| 2788 | Omnidyne-M Rep | Human |
| 2789 | Progress | Human |
| 2790 | Progress | Human |
| 2792 | The Indexer | Human |
| 2793 | Sergeant Cortese | Human |
| 2794 | Commander Finch | Human |
| 2795 | Lt. Maria Garcia | Human |
| 2796 | LeRoy | Human |
| 2799 | Scarlet | Human |
| 2802 | Copacabana Swimsuit - Ambient Pathing | Human |
| 2803 | Copacabana Swimsuit - Ambient Pathing - Slow | Human |
| 2804 | Astrek Agent | Human |
| 2806 | Dr. Bathsheba | Human |
| 2807 | Nostromo | Human |
| 2809 | Serkan | Human |
| 2810 | Mason Chen | Human |
| 2811 | Artis the Mechanic | Human |
| 2812 | Arturs the Mechanic | Human |
| 2813 | Mason Chen | Human |
| 2814 | Steve Coelho | Human |
| 2815 | Copacabana Civilian - Ambient Pathing | Human |
| 2816 | ARES Assault - Ambient Pathing | Human |
| 2817 | ARES Engineer - Ambient Pathing | Human |
| 2818 | ARES Recon - Ambient Pathing | Human |
| 2819 | ARES Dreadnaught - Ambient Pathing | Human |
| 2820 | ARES Biotech - Ambient Pathing | Human |
| 2821 | Civilian Mechanic - Ambient Pathing | Human |
| 2822 | Bernardo Coelho | Human |
| 2823 | Ricardo Coelho | Human |
| 2824 | Eduardo Coelho | Human |
| 2825 | Civilian Scientist - Ambient Pathing | Human |
| 2826 | Remigio Coelho | Human |
| 2827 | Security Chief Delgado | Human |
| 2828 | Doctor Abrams | Human |
| 2829 | Bernardo Coelho | Human |
| 2830 | Ricardo Coelho | Human |
| 2831 | Eduardo Coelho | Human |
| 2832 | Accord Soldier - Turret Defender | Human |
| 2833 | Sergeant Choi | Human |
| 2834 | Civilian Nutretic Worker - Ambient Pathing | Human |
| 2835 | Captain Abrams | Humanoid outlaw |
| 2837 | Panther of Hope | Human |
| 2839 | Accord Soldier | Human |
| 2842 | Meat Shield | Human |
| 2844 | Thump Dump Civilian - Ambient Pathing | Human |
| 2845 | Caldon | Human |
| 2846 | Coruja | Human |
| 2847 | Adrita | Human |
| 2848 | Mission013 Stabilizer | Human |
| 2849 | Grieving Husband | Human |
| 2850 | Sydney | Human |
| 2851 | Lieutenant Song | Human |
| 2852 | Major Desselhoff | Human |
| 2853 | Wiley | Human |
| 2855 | Dr. Bathsheba Husk | Human |
| 2856 | Ensign Julian Basurto | Human |
| 2857 | Sullivan Wade | Human |
| 2858 | Old Woman Andrita | Human |
| 2859 | Mason Chen | Human |
| 2860 | Chet Linwood | Human |
| 2861 | Harry Kingsley | Human |
| 2862 | Capt. Hudson Fuller | Human |
| 2865 | Shanty Town Civilian - Ambient Pathing | Human |
| 2868 | Accord Officer - Ambient Pathing | Human |
| 2869 | Wiley | Human |
| 2870 | Sunken Harbor Civilian - Ambient Pathing | Human |
| 2873 | Skydock Supply Officer | Human |
| 2874 | Warrant Officer Liu | Human |
| 2875 | Lt. Amanda Bloom | Human |
| 2876 | Corporal Belle | Human |
| 2877 | Carlo Fonseca | Human |
| 2878 | Lieutenant Sands | Human |
| 2879 | Capt. Patel | Human |
| 2880 | Sergeant Lewis | Human |
| 2881 | Trans Hub Officer | Human |
| 2882 | TransHub Accord Soldier - Ambient - No gun - BasicCivilian_Stationary | Human |
| 2883 | Trans Hub Worker - Ambient Barker | Human |
| 2889 | Sunken Harbor Civilian - BasicCivilian_Stationary | Human |
| 2890 | Sunken Harbor Resident - BasicCivilian_Stationary | Human |
| 2903 | Mason Chen | Human |
| 2930 | Dr. Bathsheba | Human |
| 2933 | Prisoner | Human |
| 2934 | Prisoner | Human |
| 2935 | Arsenal, Female | Human |
| 2936 | Arsenal, Male | Human |
| 2937 | Accord Dropship | Human |
| 2940 | Pet Ankylot | Companion/critter |
| 2954 | Rescue Drone | Human |
| 2960 | Lt. Cmdr. Kara Novan | Human |
| 2961 | Capt. Hudson Fuller | Human |
| 2964 | Albert Sung | Human |
| 2968 | Anton Hall | Human |
| 2969 | Dr. Ignacio Alvarez | Human |
| 2970 | Sand Man | Human |
| 2971 | Missing Daughter | Human |
| 2972 | Sheriff Fairuza Nasseri | Human |
| 2973 | Kazuo Mori | Human |
| 2974 | Cognac | Human |
| 2975 | Costa | Human |
| 2976 | Samira | Human |
| 2977 | Marius | Human |
| 2978 | Sergeant O'Neil | Human |
| 2981 | Private Davies | Human |
| 2982 | Archfold Mission019 | Human |
| 2983 | Garett Murdock | Human |
| 2990 | Edward Koro | Human |
| 2991 | Roughneck Lieutenant | Human |
| 3000 | Recluse Expert Macabee | Human |
| 3001 | Buzzard Fugitive | Humanoid outlaw |
| 3002 | Electron Expert Macabee | Human |
| 3003 | Raptor Expert Macabee | Human |
| 3004 | Firecat Expert Macabee | Human |
| 3005 | Rhino Expert Macabee | Human |
| 3006 | Capt. Venus Dunai | Human |
| 3007 | Bastion Expert Harris | Human |
| 3009 | Dragonfly Expert Harris | Human |
| 3010 | Nighthawk Expert Harris | Human |
| 3011 | Mammoth Expert Harris | Human |
| 3012 | Tigerclaw Expert Harris | Human |
| 3013 | Kisuton Vendor | Human |
| 3014 | Deputy Singh | Human |
| 3015 | Ekrem Demir | Human |
| 3016 | Sergeant Attah | Human |
| 3017 | Captain Hailwic | Human |
| 3018 | Doctor Zaira | Human |
| 3023 | Layla | Human |
| 3024 | Davis Royer | Human |
| 3025 | Rina Joshi | Human |
| 3026 | The Indexer | Human |
| 3027 | Scarlet | Human |
| 3028 | Wiley | Human |
| 3033 | Yuki Lin | Human |
| 3034 | Mourningstar | Human |
| 3035 | Dr. Enya Vane | Human |
| 3036 | Lt. Gunnar Nash | Human |
| 3037 | Petty Officer Justin Lai | Human |
| 3038 | Scarlet | Human |
| 3039 | Wiley | Human |
| 3040 | Camille Booker | Human |
| 3041 | Alex Kamara | Human |
| 3042 | Lt. Aaron Yip | Human |
| 3043 | Radamel Mumford | Human |
| 3048 | SIN Vendor | Human |
| 3050 | Scientist Intern | Human |
| 3051 | Kim Armitage | Human |
| 3054 | ARES 88 | Human |
| 3055 | Ekrem Demir | Human |
| 3056 | Momma Metal | Human |
| 3057 | Lost Girl | Human |
| 3062 | The Quail | Human |
| 3067 | Captive Civilian | Human |
| 3068 | Captive Civilian | Human |
| 3070 | Trojan | Human |
| 3085 | Operations Agent | Human |
| 3086 | Corporal Rasheed | Human |
| 3088 | Astrek Security - Ambient Pathing | Human |
| 3089 | Sgt. Cobb | Human |
| 3090 | Chimera - Ambient Pathing | Human |
| 3091 | Chimera Foot Soldier - Ambient Pathing | Human |
| 3092 | ARES Pilot | Human |
| 3097 | Derek Waters | Human |
| 3100 | Dropship Pilot - POI Placement | Human |
| 3101 | Accord Marine | Human |
| 3102 | Ikinya | Human |
| 3103 | Buzzard Lieutenant | Human |
| 3107 | Tanken Civilian - Ambient Pathing | Human |
| 3108 | Dredge Civilian - Ambient Pathing | Human |
| 3109 | Buzzard Civilian - Ambient Pathing | Human |
| 3111 | Nostromo Second Version To use only in Cinemas | Human |
| 3112 | Bruce Gulliver | Human |
| 3113 | Chimera Soldier - Ambient | Human |
| 3114 | Tanken Soldier - Ambient | Human |
| 3115 | Mayor Yamada | Human |
| 3116 | Joan Linkletter | Human |
| 3117 | Requisition Officer Bowen | Human |
| 3118 | Tina Tandy | Human |
| 3119 | Chloe | Human |
| 3120 | Cuttle | Human |
| 3121 | Pvt. Conrad Freeman | Human |
| 3123 | Dredge Town Civilian | Human |
| 3126 | Mourningstar | Human |
| 3128 | Accord Scientist | Human |
| 3130 | Bethany Cooper | Human |
| 3131 | Irena | Human |
| 3132 | Bob Smith | Human |
| 3139 | Accord Engineer | Human |
| 3140 | Laser Drill Operator | Human |
| 3143 | Field Researcher | Human |
| 3146 | Quentin Quinn | Human |
| 3147 | Jodene Sparks | Human |
| 3148 | Marco Marquez | Human |
| 3149 | Hazel Murphy | Human |
| 3150 | Hesh Hipplewhite | Human |
| 3153 | Dr. Joshua Tilden | Human |
| 3154 | Lt. Wallace Abernathy | Human |
| 3155 | Capt. Zachary Wallach | Human |
| 3157 | Master Sgt. Lina Rask | Human |
| 3158 | Kelly Edison | Human |
| 3159 | Lou Tavek | Human |
| 3160 | Sgt. Chanming Lum | Human |
| 3161 | Lt. Eileen Ripley | Human |
| 3162 | Pvt. Alton Huxley | Human |
| 3163 | Pvt. Luke "Badger" Exeter | Human |
| 3164 | Honey Scour | Human |
| 3166 | Capt. Celia Malkovitch | Human |
| 3167 | Lt. Elliot Falstaff | Human |
| 3168 | Cmdr. Franz Auttenberg | Human |
| 3169 | Capt. Oliver Spinner | Human |
| 3180 | Field Researcher | Human |
| 3185 | Accord Ranger | Human |
| 3187 | Mobile Repulsion Unit | Human |
| 3188 | Doctor Aurelius Fong | Human |
| 3191 | Accord Engineer | Human |
| 3192 | Honey Scour | Human |
| 3193 | Widow Wary | Human |
| 3194 | ARES 91 | Human |
| 3196 | Doctor Aurelius Fong | Human |
| 3198 | Devil's Tusk Accord Soldier - Ambient Pathing | Human |
| 3202 | Mission022_ReactorDoor | Human |
| 3203 | Mission022_CoreDoor | Human |
| 3204 | Healing Target | Human |
| 3206 | Capt. Hudson Fuller | Human |
| 3207 | 1st Sergeant Christina Vargas | Human |
| 3208 | Doctor Demarius Sifton | Human |
| 3209 | Tech Sergeant Jon Gosse | Human |
| 3210 | ARES 22 | Human |
| 3211 | Supply Officer Finley Spiegel | Human |
| 3213 | Echo Tracking Drone | Misc (drones, targets, Necronus) |
| 3214 | Cmdr. Rolan Volkov | Human |
| 3215 | Jacques Voclain | Human |
| 3216 | Gunship Pilot | Human |
| 3217 | Jacques Voclain | Human |
| 3218 | Orca Obscure | Human |
| 3224 | Capt. Hudson Fuller Death Pose | Human |
| 3225 | Doctor Aurelius Fong Death Pose for head | Human |
| 3239 | Accord Dropship Pilot | Human |
| 3245 | SIN Lion | Wildlife (bugs) |
| 3259 | ARES 88 | Human |
| 3261 | Accord Dropship Pilot | Human |
| 3262 | Desmond Poe | Human |

#### gaea — 189 named (+242 unnamed)

| id | name | race |
|----|------|------|
| 6 | Small Salamander | Wildlife (bugs) |
| 7 | Small Salamander | Wildlife (bugs) |
| 8 | Small Salamander | Wildlife (bugs) |
| 239 | OBSOLETE Aranha Worker | Wildlife (bugs) |
| 241 | Culex | Wildlife (bugs) |
| 243 | OBSOLETE Aranha Sieger | Wildlife (bugs) |
| 244 | Terrorclaw | Wildlife (bugs) |
| 347 | Whiptail Thresher | Wildlife (bugs) |
| 348 | Massive Culex | Wildlife (bugs) |
| 386 | OBSOLETE Hisser | Wildlife (bugs) |
| 387 | OBSOLETE Shell-less Hisser | Wildlife (bugs) |
| 430 | OBSOLETE Explosive Aranha | Wildlife (bugs) |
| 433 | OBSOLETE Icy Aranha | Wildlife (bugs) |
| 436 | OBSOLETE Geyser Aranha | Wildlife (bugs) |
| 448 | OBSOLETE Aranha Stormer | Wildlife (bugs) |
| 449 | OBSOLETE Toxic Aranha | Wildlife (bugs) |
| 505 | Wargrim | Wildlife (bugs) |
| 506 | Spitting Thresher | Wildlife (bugs) |
| 507 | Reckless Thresher | Wildlife (bugs) |
| 585 | OBSOLETE Duke Skiver | Wildlife (bugs) |
| 588 | OBSOLETE Nautilus | Wildlife (bugs) |
| 596 | Argonaut | Wildlife (bugs) |
| 635 | Ancient Brontodon | Large wildlife |
| 641 | Alpha Wargrim | Wildlife (bugs) |
| 642 | Herdmaster Brontodon | Large wildlife |
| 693 | OBSOLETE Skiver | Wildlife (bugs) |
| 753 | Aggressive Target Drone | Wildlife (bugs) |
| 763 | OBSOLETE Giant Aranha | Wildlife (bugs) |
| 884 | Giant Nautilus | Wildlife (bugs) |
| 888 | Toxic Sandshark | Wildlife (bugs) |
| 897 | OBSOLETE Crystite Aranha | Wildlife (bugs) |
| 898 | Crystite Aranha Crystal | Wildlife (bugs) |
| 923 | Ash Dragon | Wildlife (bugs) |
| 927 | Toxic Varant | Wildlife (bugs) |
| 928 | OBSOLETE Armored Scorcher | Wildlife (bugs) |
| 931 | OBSOLETE Scorcher | Wildlife (bugs) |
| 935 | Wooly Brontodon | Wildlife (bugs) |
| 937 | Scorpion Culex | Wildlife (bugs) |
| 938 | Firejacket | Wildlife (bugs) |
| 940 | Rimehunter | Wildlife (bugs) |
| 941 | White Wargrim | Wildlife (bugs) |
| 963 | Junk Yard Dog | Wildlife (bugs) |
| 1011 | Rageclaw | Wildlife (bugs) |
| 1029 | Hisser Queen | Wildlife (bugs) |
| 1030 | Hisser Broodling | Wildlife (bugs) |
| 1034 | Doomstalker | Wildlife (bugs) |
| 1179 | Akreatrix the Untame | Wildlife (bugs) |
| 1184 | Small Crystite Aranha Worker | Wildlife (bugs) |
| 1187 | Kalidor the Giant | Wildlife (bugs) |
| 1188 | Vomica the Noxious | Wildlife (bugs) |
| 1189 | Culex Swarmer | Wildlife (bugs) |
| 1197 | Goliath | Wildlife (bugs) |
| 1198 | Crystite Aranha Sieger | Wildlife (bugs) |
| 1201 | Massive Culex | Wildlife (bugs) |
| 1298 | Rockhorn Bull | Wildlife (bugs) |
| 1313 | Hell Slinger | Wildlife (bugs) |
| 1319 | Firewhip | Wildlife (bugs) |
| 1401 | Bark Slinger | Wildlife (bugs) |
| 1403 | Harpy | Wildlife (bugs) |
| 1404 | Centauri Tick | Wildlife (bugs) |
| 1410 | [OBSOLETE] Rasper | Wildlife (bugs) |
| 1414 | Dire Fox | Wildlife (bugs) |
| 1415 | Rockhog | Wildlife (bugs) |
| 1417 | Centauri Spitter | Wildlife (bugs) |
| 1419 | Titanomoth | Wildlife (bugs) |
| 1420 | Alpha Direfox | Wildlife (bugs) |
| 1421 | Gurgon | Wildlife (bugs) |
| 1422 | Gremlin | Wildlife (bugs) |
| 1425 | Strideviper | Wildlife (bugs) |
| 1426 | Shield Crawler | Wildlife (bugs) |
| 1430 | [OBSOLETE] Queen Rasper | Wildlife (bugs) |
| 1442 | Young Storm Kestrel | Wildlife (bugs) |
| 1548 | Hate-Bot | Wildlife (bugs) |
| 1597 | Infected Storm Kestrel | Wildlife (bugs) |
| 1609 | Terrorclaw Prime | Wildlife (bugs) |
| 1620 | Nightmare Toxic Sandshark | Wildlife (bugs) |
| 1666 | Super Striped Gremlin | Wildlife (bugs) |
| 1719 | OBSOLETE Bull Skiver | Wildlife (bugs) |
| 1720 | OBSOLETE Spitting Skiver | Wildlife (bugs) |
| 1721 | OBSOLETE King Skiver | Wildlife (bugs) |
| 1749 | Guard Rasper | Wildlife (bugs) |
| 1750 | Hyena Scavenger | Wildlife (bugs) |
| 1754 | Centauri Tick Soldier | Wildlife (bugs) |
| 1755 | Centauri Tick Spinner | Wildlife (bugs) |
| 1774 | Rockhorn Slinger | Wildlife (bugs) |
| 1775 | Rockhorn Guardian | Wildlife (bugs) |
| 1777 | Direhornet | Wildlife (bugs) |
| 1782 | Firecat | Wildlife (bugs) |
| 1784 | The Scorpion King | Wildlife (bugs) |
| 1791 | Flamethrower Firejacket | Wildlife (bugs) |
| 1793 | Volatile Scorcher | Wildlife (bugs) |
| 1794 | OBSOLETE Hellfire Scorcher | Wildlife (bugs) |
| 1795 | Large Firejacket | Wildlife (bugs) |
| 1797 | Firebreath Scorcher | Wildlife (bugs) |
| 2049 | OBSOLETE Toxic Aranha | Wildlife (bugs) |
| 2050 | OBSOLETE Toxic Aranha Sieger | Wildlife (bugs) |
| 2051 | OBSOLETE Toxic Explosive Aranha | Wildlife (bugs) |
| 2058 | Ragequeen | Wildlife (bugs) |
| 2077 | Brontodon King | Large wildlife |
| 2104 | Pterothan | Wildlife (bugs) |
| 2110 | Hydrus | Wildlife (bugs) |
| 2112 | Haken | Wildlife (bugs) |
| 2113 | Strix | Wildlife (bugs) |
| 2117 | Garm | Wildlife (bugs) |
| 2121 | Scald | Wildlife (bugs) |
| 2145 | Kojo | Wildlife (bugs) |
| 2146 | Kruger | Wildlife (bugs) |
| 2158 | Tornado | Wildlife (bugs) |
| 2160 | Magaera the Fury | Wildlife (bugs) |
| 2161 | Scota the Breaker | Wildlife (bugs) |
| 2162 | Iwanci the Fetid | Wildlife (bugs) |
| 2163 | Shisa the Prowler | Wildlife (bugs) |
| 2255 | Swamp Hisser | Wildlife (bugs) |
| 2256 | Matriarch Hisser | Wildlife (bugs) |
| 2257 | Myrmidon Hisser | Wildlife (bugs) |
| 2258 | Hunter Hisser | Wildlife (bugs) |
| 2259 | Ichor Hisser | Wildlife (bugs) |
| 2297 | Dread Nautilus | Wildlife (bugs) |
| 2323 | Skiver | Wildlife (bugs) |
| 2324 | Skiver Spitter | Wildlife (bugs) |
| 2325 | King Skiver | Wildlife (bugs) |
| 2326 | Skiver Brood Matron | Wildlife (bugs) |
| 2327 | Skiverling | Wildlife (bugs) |
| 2329 | Skiver Drone | Wildlife (bugs) |
| 2336 | Scorcher | Wildlife (bugs) |
| 2337 | Hellfire Scorcher | Wildlife (bugs) |
| 2338 | Armored Scorcher | Wildlife (bugs) |
| 2342 | Aranha | Wildlife (bugs) |
| 2343 | Spitting Aranha | Wildlife (bugs) |
| 2344 | Aranha Soldier | Wildlife (bugs) |
| 2345 | Aranha Worker | Wildlife (bugs) |
| 2346 | Giant Aranha | Wildlife (bugs) |
| 2358 | Hisser | Wildlife (bugs) |
| 2360 | Spitting Hisser | Wildlife (bugs) |
| 2361 | Hisser Soldier | Wildlife (bugs) |
| 2362 | Metamorphic Hisser | Wildlife (bugs) |
| 2363 | Hisser Queen | Wildlife (bugs) |
| 2368 | Crab Spider Ranged | Wildlife (bugs) |
| 2369 | Crab Spider Blaster | Wildlife (bugs) |
| 2370 | Crab Spider Soldier | Wildlife (bugs) |
| 2371 | Crab Spider Spinner | Wildlife (bugs) |
| 2372 | Shield Crawler | Wildlife (bugs) |
| 2376 | Rasper Spitter | Wildlife (bugs) |
| 2377 | Rasper Blaster | Wildlife (bugs) |
| 2378 | Rasper Soldier | Wildlife (bugs) |
| 2379 | Rasper Heavy | Wildlife (bugs) |
| 2380 | Raspernaut | Wildlife (bugs) |
| 2405 | Crystite Aranha Turret Gunner | Wildlife (bugs) |
| 2406 | Crystite Aranha | Wildlife (bugs) |
| 2412 | Storm Kestrel | Wildlife (bugs) |
| 2413 | Nautilus | Wildlife (bugs) |
| 2431 | Special Aranha | Wildlife (bugs) |
| 2434 | Special Hisser | Wildlife (bugs) |
| 2435 | Aranha Queen | Wildlife (bugs) |
| 2444 | Icy Aranha | Wildlife (bugs) |
| 2445 | Icy Aranha Spitter | Wildlife (bugs) |
| 2446 | Freezing Aranha | Wildlife (bugs) |
| 2447 | Icy Shield Aranha | Wildlife (bugs) |
| 2448 | Blizzard Aranha | Wildlife (bugs) |
| 2449 | Infected Kestrel Stormer | Wildlife (bugs) |
| 2450 | Infected Kestrel Duster | Wildlife (bugs) |
| 2457 | Special Brujo the Breaker | Wildlife (bugs) |
| 2458 | Special Hydrus - Menace of the Mire | Wildlife (bugs) |
| 2459 | Special Haken | Wildlife (bugs) |
| 2460 | Special Strix | Wildlife (bugs) |
| 2462 | Special Gobler the Gorger | Wildlife (bugs) |
| 2463 | Special Drorgan the Maneater | Wildlife (bugs) |
| 2466 | Special Kojo the Murderer | Wildlife (bugs) |
| 2467 | Special Kruger the Rampager | Wildlife (bugs) |
| 2485 | Toxic Aranha | Wildlife (bugs) |
| 2486 | Toxic Spitting Aranha | Wildlife (bugs) |
| 2487 | Toxic Aranha Defiler | Wildlife (bugs) |
| 2488 | Toxic Aranha Slinger | Wildlife (bugs) |
| 2489 | Toxic Biohazard Aranha | Wildlife (bugs) |
| 2499 | Metamorphic Hisser | Wildlife (bugs) |
| 2505 | [OBSOLETE] Elite Aranha | Wildlife (bugs) |
| 2506 | [OBSOLETE] Elite Spitting Aranha | Wildlife (bugs) |
| 2535 | [OBSOLETE] Elite Scorcher | Wildlife (bugs) |
| 2536 | [OBSOLETE] Elite Hellfire Scorcher | Wildlife (bugs) |
| 2542 | Elite Matriarch Hisser | Wildlife (bugs) |
| 2551 | [OBSOLETE] Elite Hate-Bot | Wildlife (bugs) |
| 2569 | Aranha Hatchling | Wildlife (bugs) |
| 2697 | Rasper Shielder | Wildlife (bugs) |
| 2711 | Centauri Mite | Wildlife (bugs) |
| 2838 | Monstrous King Skiver | Wildlife (bugs) |
| 2854 | IceClaw | Wildlife (bugs) |
| 2895 | Culex Matron | Wildlife (bugs) |
| 2942 | Pet Harpy | Companion/critter |
| 3142 | Arch Scorcher | Wildlife (bugs) |

#### bandit — 164 named (+85 unnamed)

| id | name | race |
|----|------|------|
| 512 | Bandit Pointman | Humanoid outlaw |
| 513 | Bandit Grenadier | Humanoid outlaw |
| 514 | Bandit Gunman | Humanoid outlaw |
| 529 | Bandit Assault | Humanoid outlaw |
| 531 | Tanken Buster Corpse | Humanoid outlaw |
| 591 | Pirate Burster | Humanoid outlaw |
| 592 | Pirate Grenadier | Humanoid outlaw |
| 593 | Pirate Gunner | Humanoid outlaw |
| 594 | Pirate Assault | Humanoid outlaw |
| 595 | Pirate Dreadnaught | Humanoid outlaw |
| 625 | Bandit Explosives Vendor | Humanoid outlaw |
| 761 | Ophanim Agent | Humanoid outlaw |
| 762 | Bandit Overclocked | Humanoid outlaw |
| 889 | Raider Baron | Humanoid outlaw |
| 894 | Bandit Hound | Humanoid outlaw |
| 969 | Bandit Dreadnaught | Humanoid outlaw |
| 970 | Elite Bandit Gunner | Humanoid outlaw |
| 1063 | Outpost Heavy Turret Gunner | Humanoid outlaw |
| 1180 | Wounded Bandit | Humanoid outlaw |
| 1181 | Big Brother | Humanoid outlaw |
| 1182 | Blood Kings Commander | Humanoid outlaw |
| 1226 | Elite Blood King Assault | Humanoid outlaw |
| 1227 | Elite Blood King Dreadnaught | Humanoid outlaw |
| 1228 | Blood King Assault | Humanoid outlaw |
| 1229 | Blood King Biotech | Humanoid outlaw |
| 1230 | Blood King Assault | Humanoid outlaw |
| 1231 | Blood King Trooper | Humanoid outlaw |
| 1232 | Blood King Soldier | Humanoid outlaw |
| 1236 | Blood King Dreadnaught | Humanoid outlaw |
| 1238 | Blood King Sniper | Humanoid outlaw |
| 1252 | Pirate Santa | Humanoid outlaw |
| 1292 | Skull Burster | Humanoid outlaw |
| 1293 | Skull Grenadier | Humanoid outlaw |
| 1294 | Skull Gunner | Humanoid outlaw |
| 1295 | Skull Assault | Humanoid outlaw |
| 1296 | Skull Dreadnaught | Humanoid outlaw |
| 1356 | Gorou Kagame | Humanoid outlaw |
| 1390 | Smuggler | Humanoid outlaw |
| 1392 | Cerrado Norte Bandit | Humanoid outlaw |
| 1393 | Sergeant Kang | Humanoid outlaw |
| 1432 | Lorenzo | Humanoid outlaw |
| 1446 | Lodo | Humanoid outlaw |
| 1448 | Conus | Humanoid outlaw |
| 1449 | Leon | Humanoid outlaw |
| 1466 | The Cortador | Humanoid outlaw |
| 1467 | Anjo Olhos Boy (Corpse) | Humanoid outlaw |
| 1471 | Poacher | Humanoid outlaw |
| 1476 | Poacher | Humanoid outlaw |
| 1477 | Bandit Assassin | Humanoid outlaw |
| 1495 | Gang Leader | Humanoid outlaw |
| 1497 | Rebel | Humanoid outlaw |
| 1510 | Buzzard Pointman | Humanoid outlaw |
| 1522 | Black Hills Dreadnaught | Humanoid outlaw |
| 1523 | Reaper Chief | Humanoid outlaw |
| 1526 | Rebel Support | Humanoid outlaw |
| 1527 | Ophanim Chief | Humanoid outlaw |
| 1528 | Tanken Chief | Humanoid outlaw |
| 1545 | Bandit Contact | Humanoid outlaw |
| 1547 | Shady Civilian | Humanoid outlaw |
| 1549 | Separatist Leader | Humanoid outlaw |
| 1555 | Charon | Humanoid outlaw |
| 1576 | SIN Hack Dealer | Humanoid outlaw |
| 1596 | The Traitor | Humanoid outlaw |
| 1599 | Van Pistol | Human |
| 1618 | Gunmetal Jack | Human |
| 1657 | Tanken Enforcer | Humanoid outlaw |
| 1662 | Rogue Criminal | Humanoid outlaw |
| 1665 | Tanken Agent | Humanoid outlaw |
| 1669 | Enhanced Marauder | Humanoid outlaw |
| 1670 | Nutretic Liaison | Humanoid outlaw |
| 1673 | SIN Hacker | Humanoid outlaw |
| 1678 | Omnidyne-M Guard | Humanoid outlaw |
| 1679 | Chimera Dealer | Humanoid outlaw |
| 1682 | Bandit Recruiter | Humanoid outlaw |
| 1683 | Bandit Recruiter | Humanoid outlaw |
| 1684 | Bandit Recruiter | Humanoid outlaw |
| 1685 | Bandit Recruiter | Humanoid outlaw |
| 1691 | SIN Phantom | Humanoid outlaw |
| 1700 | Private Durst | Humanoid outlaw |
| 1714 | Omnidyne-M Agent | Humanoid outlaw |
| 1715 | Omnidyne-M Agent | Humanoid outlaw |
| 1716 | Enyo Drone | Humanoid outlaw |
| 1717 | Phobos Bot+B82 | Humanoid outlaw |
| 1723 | Rocket Pod Combat Drone | Humanoid outlaw |
| 1733 | Buzzard Biker | Humanoid outlaw |
| 1735 | Buzzard Brawler | Humanoid outlaw |
| 1737 | Buzzard Hellion | Humanoid outlaw |
| 1741 | Bandit Punk | Humanoid outlaw |
| 1742 | Bandit Enforcer | Humanoid outlaw |
| 1743 | Bandit Grenadier | Humanoid outlaw |
| 1744 | Chimera Addict | Humanoid outlaw |
| 1745 | Chimera Smuggler | Humanoid outlaw |
| 1747 | Chimera Hacker | Humanoid outlaw |
| 1756 | Blackhat Leader | Humanoid outlaw |
| 1757 | Tanken Assassin | Humanoid outlaw |
| 1761 | Tanken Master | Humanoid outlaw |
| 1796 | Buzzard Contact | Humanoid outlaw |
| 1809 | SIN Virus | Humanoid outlaw |
| 1810 | SIN Phantom | Humanoid outlaw |
| 1811 | Traitorous Dreadnaught | Humanoid outlaw |
| 1812 | SIN Phantom | Humanoid outlaw |
| 1818 | Cerrado Norte Bandit | Humanoid outlaw |
| 1824 | Mickey the Wrench | Humanoid outlaw |
| 1837 | Traitorous Marine | Humanoid outlaw |
| 1838 | Traitorous Assault | Humanoid outlaw |
| 1839 | SIN Phantom | Humanoid outlaw |
| 1849 | Speedy Steve | Humanoid outlaw |
| 1850 | Twitchy Ted | Humanoid outlaw |
| 1851 | Bouncing Betty | Humanoid outlaw |
| 1877 | Buzzard Chaingunner | Humanoid outlaw |
| 1879 | Remus | Humanoid outlaw |
| 1883 | Tanken Shogun | Humanoid outlaw |
| 1884 | Chimera Grenadier | Humanoid outlaw |
| 1885 | Chimera Dreadnaught | Humanoid outlaw |
| 1886 | Bandit Dreadnaught | Humanoid outlaw |
| 2053 | Wintertide Elf | Humanoid outlaw |
| 2063 | Grumbly Bumbly | Humanoid outlaw |
| 2120 | Huntmaster | Humanoid outlaw |
| 2314 | Buzzard Shotgunner | Humanoid outlaw |
| 2315 | Buzzard Assault Rifleman | Humanoid outlaw |
| 2322 | Blood King Contractor | Humanoid outlaw |
| 2392 | Chimera Sniper | Humanoid outlaw |
| 2393 | Buzzard Sniper | Humanoid outlaw |
| 2394 | Bandit Sniper | Humanoid outlaw |
| 2438 | SIN Phantom | Humanoid outlaw |
| 2439 | SIN Phantom | Humanoid outlaw |
| 2440 | Traitorous Ranger | Humanoid outlaw |
| 2441 | Traitorous Sniper | Humanoid outlaw |
| 2452 | Poacher | Humanoid outlaw |
| 2453 | Poacher Game Hunter | Humanoid outlaw |
| 2454 | Poacher Grenadier | Humanoid outlaw |
| 2455 | Poacher Heavy Gunner | Humanoid outlaw |
| 2456 | Poacher Sniper | Humanoid outlaw |
| 2515 | [OBSOLETE] Elite Bandit Punk | Humanoid outlaw |
| 2523 | [OBSOLETE] Elite Blood King Contractor | Humanoid outlaw |
| 2524 | [OBSOLETE] Elite Blood King Soldier | Humanoid outlaw |
| 2529 | Master Poacher Game Hunter | Humanoid outlaw |
| 2582 | Barone | Humanoid outlaw |
| 2661 | Bandit Hoodlum | Humanoid outlaw |
| 2665 | Chimera Blaster | Humanoid outlaw |
| 2666 | Poacher Blaster | Humanoid outlaw |
| 2713 | Chimera Rocketeer | Humanoid outlaw |
| 2775 | Bandit Bully | Humanoid outlaw |
| 2781 | Poacher Pro Hunter | Humanoid outlaw |
| 2800 | Shady Civilian | Humanoid outlaw |
| 2866 | Cultist | Humanoid outlaw |
| 2946 | Buzzard Ultratrooper | Humanoid outlaw |
| 2984 | Zero Face | Humanoid outlaw |
| 2993 | Dr. Glacier | Humanoid outlaw |
| 2994 | The Giggler | Humanoid outlaw |
| 2995 | Leola Rex | Humanoid outlaw |
| 2997 | Maynard | Humanoid outlaw |
| 2998 | Anzu | Humanoid outlaw |
| 3020 | Brick | Humanoid outlaw |
| 3021 | Sinker | Humanoid outlaw |
| 3022 | Cueball | Humanoid outlaw |
| 3030 | Foo Dog | Humanoid outlaw |
| 3031 | Infected Intern | Humanoid outlaw |
| 3046 | Chimera Rescue Drone | Human |
| 3049 | The GOAT | Humanoid outlaw |
| 3052 | Sin Infected Punk | Humanoid outlaw |
| 3053 | Kevin | Humanoid outlaw |
| 3058 | Deikrast | Chosen |
| 3071 | TEST Jumpjet NPC | Human |

#### chosen — 122 named (+148 unnamed)

| id | name | race |
|----|------|------|
| 5 | Red Shirt | Chosen |
| 10 | Chosen Target Dummy | Chosen |
| 22 | Necronus | Misc (drones, targets, Necronus) |
| 150 | Puker | Chosen |
| 212 | Grunt Cannoneer | Chosen |
| 281 | Chosen Sniper | Chosen |
| 326 | Chosen Shock Trooper | Chosen |
| 327 | Juggernaut | Chosen |
| 392 | Juggernaut | Chosen |
| 409 | OBSOLETE Grunt Bruiser | Chosen |
| 453 | Grunt Raider | Chosen |
| 533 | Engineer | Chosen |
| 543 | Chosen Drone | Chosen |
| 545 | Grunt Roaster | Chosen |
| 548 | OBSOLETE Siegebreaker | Chosen |
| 571 | Chosen Assault | Chosen |
| 572 | Engineer | Chosen |
| 598 | Melding Acolyte | Chosen |
| 640 | Melding Acolyte | Chosen |
| 708 | Carcass | Chosen |
| 709 | Vile Carcass | Chosen |
| 720 | Turret Controller - Artillery Turret | Chosen |
| 906 | OBSOLETE Elite Juggernaut | Chosen |
| 910 | Elite Siegebreaker | Chosen |
| 1012 | Vorgoth | Chosen |
| 1013 | _Shock Trooper | Chosen |
| 1031 | Chosen Guardian | Chosen |
| 1060 | Chosen Dropship Turret Gunner Main Cannon | Chosen |
| 1061 | Chosen Dropship Turret Gunner Small Side Cannon | Chosen |
| 1196 | Chosen Fiend | Chosen |
| 1203 | Campaign 5 Power Grab Turret Gunner | Chosen |
| 1225 | Putrid Carcass | Chosen |
| 1305 | Chosen Shield Drone | Chosen |
| 1306 | The Kodiak | Chosen |
| 1365 | Elite Shock Trooper | Chosen |
| 1375 | Chosen Obliterator | Chosen |
| 1377 | Elite Chosen Chaingunner | Chosen |
| 1385 | Chosen Archon | Chosen |
| 1540 | Chosen Earthbreaker | Chosen |
| 1560 | Bald Bully | Human |
| 1580 | Captain Abrams | Chosen |
| 1658 | Serkan | Chosen |
| 1664 | Chosen Commander | Chosen |
| 1808 | Chosen Archon | Chosen |
| 1984 | Howie | Chosen |
| 2057 | Headless Horseman | Chosen |
| 2066 | Chosen Juggernaut | Chosen |
| 2097 | Chosen Lieutenant | Chosen |
| 2100 | Elite Devastator | Chosen |
| 2108 | Bio Engineer | Chosen |
| 2129 | Chosen Annihilator | Chosen |
| 2136 | Chosen Siegebreaker | Chosen |
| 2156 | Chosen Engineer Shield Drone | Human |
| 2157 | Chosen Engineer | Chosen |
| 2159 | Rashnu | Chosen |
| 2171 | Chosen Mosquito Drone | Chosen |
| 2173 | Chosen Dropship | Chosen |
| 2175 | Chosen Assault Drone | Chosen |
| 2216 | Chosen Heavy Turret Operator | Chosen |
| 2265 | Scout | Chosen |
| 2272 | Ogrix | Chosen |
| 2276 | Agrievan | Chosen |
| 2285 | Chosen Support Drone | Chosen |
| 2286 | Aliyan | Chosen |
| 2287 | Tarantus | Chosen |
| 2289 | Chosen Healing Drone | Chosen |
| 2298 | Militus | Chosen |
| 2313 | Chosen Turret Bot | Chosen |
| 2320 | Chosen Devastator | Chosen |
| 2332 | Executioner | Chosen |
| 2410 | Chosen Shield Drone | Chosen |
| 2417 | Chosen Defiler | Chosen |
| 2420 | [OBSOLETE] Elite Chosen Infantry | Chosen |
| 2421 | [OBSOLETE] Elite Chosen Shock Trooper | Chosen |
| 2423 | Pantheon Event Boss | Chosen |
| 2424 | Captive Chosen Stasis Field | Chosen |
| 2525 | Mendraxus | Chosen |
| 2555 | Grunt Soldier | Chosen |
| 2556 | Shock Trooper | Chosen |
| 2586 | Attack Drone | Chosen |
| 2668 | Chosen Ultratrooper | Chosen |
| 2669 | Chosen Assassin | Chosen |
| 2712 | Ranok | Chosen |
| 2728 | Serkan | Chosen |
| 2729 | Mendraxus | Chosen |
| 2732 | Overseer Slugg | Chosen |
| 2748 | Ranok | Chosen |
| 2771 | Chosen Infantry | Chosen |
| 2791 | Tarantus | Chosen |
| 2805 | Agrievan | Chosen |
| 2808 | Overseer | Chosen |
| 2836 | Agrievan | Chosen |
| 2867 | Overseer Slugg | Chosen |
| 2894 | Serkan | Chosen |
| 2904 | Agrievan | Chosen |
| 2911 | Necronus | Misc (drones, targets, Necronus) |
| 2957 | Aliyan | Chosen |
| 2962 | Skoraith | Chosen |
| 2963 | Ikrus | Chosen |
| 2965 | Vorkal | Chosen |
| 2985 | The Dragon | Chosen |
| 2987 | Serkan | Chosen |
| 3008 | Chosen Thermal Drone | Chosen |
| 3019 | Chosen 35 | Chosen |
| 3029 | Serkan | Chosen |
| 3044 | Chosen Healing Drone | Chosen |
| 3061 | Tarantus | Chosen |
| 3064 | Aliyan | Chosen |
| 3072 | Tarantus | Chosen |
| 3073 | Tarantus | Chosen |
| 3105 | Weak Necronus | Misc (drones, targets, Necronus) |
| 3110 | Mendraxus | Chosen |
| 3133 | Septix | Chosen |
| 3138 | Turret | Chosen |
| 3145 | Pyrexion | Chosen |
| 3152 | Chosen Devils Tusk Commander | Chosen |
| 3222 | Fireball | Wildlife (bugs) |
| 3251 | Player Chosen Fiend | Chosen |
| 3252 | Player Chosen Shock Trooper | Chosen |
| 3254 | Chosen Devastator Polymorph | Chosen |
| 3256 | Emissary Assistant | Chosen |
| 3257 | Emissary Quartermaster | Chosen |

#### friendly — 83 named (+31 unnamed)

| id | name | race |
|----|------|------|
| 196 | Miniature Brontodon | Companion/critter |
| 345 | Turret Bot | Companion/critter |
| 567 | Merch | Companion/critter |
| 568 | Flesh Reaper | Companion/critter |
| 569 | Boon Boon | Companion/critter |
| 578 | Oi | Companion/critter |
| 579 | Nanu | Companion/critter |
| 618 | Straw Hat Thresher | Companion/critter |
| 623 | Automated Teller Bot | Companion/critter |
| 645 | Wintertide Elf | Companion/critter |
| 752 | Target Drone - Friendly | Companion/critter |
| 892 | T.E.X. | Companion/critter |
| 959 | Deimos | Companion/critter |
| 1010 | T.O.P. | Companion/critter |
| 1024 | Tiny Tempest | Companion/critter |
| 1064 | Target Drone - Friendly Invincible | Companion/critter |
| 1249 | _Oilspill's Dropship Engineer Drone | Companion/critter |
| 1380 | Dronificus | Companion/critter |
| 1398 | Wiley | Companion/critter |
| 1438 | Cuno | Companion/critter |
| 1444 | Bandit Dreadnaught | Companion/critter |
| 1465 | Injured ARES Pilot | Companion/critter |
| 1480 | Little Claw | Companion/critter |
| 1605 | Sliver | Companion/critter |
| 1651 | Baby Red Panda | Companion/critter |
| 1654 | Horned Fox | Companion/critter |
| 1863 | Echo Tracking Drone | Companion/critter |
| 1908 | Glitch | Companion/critter |
| 1983 | Nightmare Nanu | Companion/critter |
| 1989 | Camera Bot | Companion/critter |
| 2046 | Tiny Brontodon | Companion/critter |
| 2052 | Tame Toxic Aranha | Companion/critter |
| 2107 | Infected Soldier | Human |
| 2132 | Earthbreaker, Pet Hologram | Large wildlife |
| 2135 | Lil' King | Companion/critter |
| 2213 | Chosen Chibi Earthbreaker, Pet Hologram | Large wildlife |
| 2217 | Chosen Assault, Pet Hologram | Large wildlife |
| 2218 | Chosen Chibi Assault, Pet Hologram | Large wildlife |
| 2219 | Chosen Engineer, Pet Hologram | Large wildlife |
| 2220 | Chosen Chibi Engineer, Pet Hologram | Large wildlife |
| 2221 | Chosen Executioner, Pet Hologram | Large wildlife |
| 2222 | Chosen Chibi Executioner, Pet Hologram | Large wildlife |
| 2223 | Chosen Archon, Pet Hologram | Large wildlife |
| 2224 | Chosen Chibi Archon, Pet Hologram | Large wildlife |
| 2225 | Melded Trapjaw, Pet Hologram | Large wildlife |
| 2226 | Vile Carcass, Pet Hologram | Large wildlife |
| 2227 | Tiny Typhon, Pet Hologram | Large wildlife |
| 2228 | Chibi Typhoon, Pet Hologram | Large wildlife |
| 2229 | Mourningstar, Pet Hologram | Large wildlife |
| 2230 | Chibi Mourningstar, Pet Hologram | Large wildlife |
| 2231 | Oilspill, Pet Hologram | Large wildlife |
| 2232 | Chibi Oilspill, Pet Hologram | Large wildlife |
| 2233 | Aero, Pet Hologram | Large wildlife |
| 2234 | Chibi Aero, Pet Hologram | Large wildlife |
| 2235 | Captain Fuller, Pet Hologram | Large wildlife |
| 2236 | Chibi Captain Fuller, Pet Hologram | Large wildlife |
| 2240 | Battlelab Aero, Pet Hologram | Large wildlife |
| 2273 | Spirit of Flight, Pet Hologram | Large wildlife |
| 2280 | Mr. Green | Companion/critter |
| 2347 | Salvage Bot AI | Companion/critter |
| 2349 | Repulsor AI | Companion/critter |
| 2436 | GiGi | Companion/critter |
| 2504 | Invisible_Man | Human |
| 2562 | Radic | Companion/critter |
| 2567 | Brontodon | Companion/critter |
| 2568 | Elder Brontodon | Companion/critter |
| 2843 | Injured Driver | Companion/critter |
| 2901 | Robotic Bunny | Companion/critter |
| 2931 | Double 11 Bot | Large wildlife |
| 2932 | Ed the Zombie | Large wildlife |
| 2979 | Injured Soldier | Companion/critter |
| 3047 | Panda | Companion/critter |
| 3087 | RL-18 Support Drone | Companion/critter |
| 3095 | Melding Puppy | Companion/critter |
| 3104 | Ho'ou | Companion/critter |
| 3195 | Little Kahuna | Companion/critter |
| 3200 | Brinewyrm | Companion/critter |
| 3201 | Nian Jr. | Companion/critter |
| 3220 | Tiny General | Companion/critter |
| 3221 | Rookie Grinder | Companion/critter |
| 3246 | Zodiac Monkey | Companion/critter |
| 3250 | 9 Tailed Fox | Companion/critter |
| 3253 | Chosen Emissary | Companion/critter |

#### melding — 45 named (+77 unnamed)

| id | name | race |
|----|------|------|
| 1 | Melded Wyrm | Melded creature |
| 19 | Melded Varant - OLD | Melded creature |
| 68 | Melding Tornado | Melded creature |
| 270 | Melded Hisser | Melded creature |
| 314 | Melded Surger | Melded creature |
| 487 | Trapjaw | Melded creature |
| 528 | Melded Aranha | Melded creature |
| 538 | Melded Spawnling | Melded creature |
| 552 | Melding Tornado Funnel | Melded creature |
| 553 | Melding Shard | Melded creature |
| 554 | Melded Culex | Melded creature |
| 559 | Melding Shard 2 | Melded creature |
| 560 | Melding Shard 3 | Melded creature |
| 561 | Melding Shard 4 | Melded creature |
| 562 | Explosive Melded Aranha | Melded creature |
| 565 | Melding Shard | Melded creature |
| 599 | Baneclaw | Melded creature |
| 600 | Hellclaw | Melded creature |
| 691 | Acolyte Diseased Culex | Melded creature |
| 692 | Acolyte Hisser | Melded creature |
| 991 | Melded Varant | Melded creature |
| 1186 | Melded Wargrim | Melded creature |
| 1258 | Alpha Trapjaw | Melded creature |
| 1427 | Ankylot | Melded creature |
| 1730 | Enraged Ankylot | Melded creature |
| 2111 | Anubis | Melded creature |
| 2153 | Massive Melded Varant | Melded creature |
| 2186 | Ash Larva | Melded creature |
| 2303 | Melding Core | Melded creature |
| 2384 | Melding Carcass | Melded creature |
| 2385 | Melding Culex | Melded creature |
| 2386 | Melding Wyrm | Melded creature |
| 2388 | Melding Vorrax | Melded creature |
| 2389 | Melding Vile Carcass | Melded creature |
| 2468 | Special Anubis the Gravedigger | Melded creature |
| 2512 | [OBSOLETE] Elite Melding Culex | Melded creature |
| 2840 | Melding Ultra Culex | Melded creature |
| 2896 | Tortured Soul | Melded creature |
| 3106 | Nian | Melded creature |
| 3144 | Melding Shard | Melded creature |
| 3156 | Nian Meldling | Wildlife (bugs) |
| 3165 | _Copy of Melding Tornado Funnel | Melded creature |
| 3170 | Nian Worshipper Grenadier | Humanoid outlaw |
| 3171 | Nian Worshipper Enforcer | Humanoid outlaw |
| 3172 | Nian Worshipper Dreadnaught | Humanoid outlaw |

#### neutral — 26 named (+16 unnamed)

| id | name | race |
|----|------|------|
| 582 | Brontodon | Large wildlife |
| 584 | Brinewyrm | Large wildlife |
| 586 | OBSOLETE Storm Kestrel | Large wildlife |
| 587 | Crab | Large wildlife |
| 622 | Signal | Large wildlife |
| 628 | Bike Voice | Large wildlife |
| 633 | Young Brontodon | Large wildlife |
| 634 | Elder Brontodon | Large wildlife |
| 887 | Scavenger Bot | Large wildlife |
| 1009 | Wreck Scavenger Drone | Large wildlife |
| 1330 | Slingshot | Large wildlife |
| 1423 | Rhinadon | Large wildlife |
| 2064 | Large Brinewyrm | Large wildlife |
| 2074 | Scavenger Bot | Large wildlife |
| 2143 | Danko | Large wildlife |
| 2144 | Nymero | Wildlife (bugs) |
| 2178 | Ash Drinker | Large wildlife |
| 2180 | Sand Drinker | Large wildlife |
| 2430 | Dead Reaper Raider | Humanoid outlaw |
| 2432 | Dead Reaper Rifleman | Humanoid outlaw |
| 2433 | Dead Reaper Cannoneer | Humanoid outlaw |
| 2464 | Special Danko the Stalker | Large wildlife |
| 2465 | Special Nymero the Widowmaker | Wildlife (bugs) |
| 3059 | Unknown Man | Large wildlife |
| 3060 | Unknown Girl | Large wildlife |
| 3178 | Core Stability | Chosen |

#### Rebels — 24 named (+11 unnamed)

| id | name | race |
|----|------|------|
| 1507 | Rebel Fighter | Humanoid outlaw |
| 1516 | Rebel Pyro | Humanoid outlaw |
| 1559 | Cerberus | Humanoid outlaw |
| 1582 | Rebel Leader | Humanoid outlaw |
| 1585 | Gerald Greenway | Humanoid outlaw |
| 1765 | Rebel Rioter | Humanoid outlaw |
| 1880 | Rebel Guerilla | Humanoid outlaw |
| 1881 | Rebel Leader | Humanoid outlaw |
| 1995 | Rebel Technician | Humanoid outlaw |
| 2290 | Rebel Rioter | Humanoid outlaw |
| 2397 | Rebel Sniper | Humanoid outlaw |
| 2517 | [OBSOLETE] Elite Rebel Fighter | Humanoid outlaw |
| 2518 | [OBSOLETE] Elite Rebel Guerilla | Humanoid outlaw |
| 2663 | Rebel Grenadier | Humanoid outlaw |
| 2778 | Rebel Resister | Humanoid outlaw |
| 2841 | Rebel Lieutenant | Humanoid outlaw |
| 2927 | Warden Raul Moreno | Humanoid outlaw |
| 2949 | Battleframe Thief | Humanoid outlaw |
| 2950 | Battleframe Thief | Humanoid outlaw |
| 2951 | Battleframe Thief | Humanoid outlaw |
| 2952 | Battleframe Thief | Humanoid outlaw |
| 2953 | Battleframe Thief | Humanoid outlaw |
| 2967 | Rebel Guerilla | Humanoid outlaw |
| 3074 | Battleframe Thief | Humanoid outlaw |

#### Black Hills Bandits — 22 named (+6 unnamed)

| id | name | race |
|----|------|------|
| 1248 | Boss Singh | Humanoid outlaw |
| 1304 | Black Hills Bandit | Humanoid outlaw |
| 1411 | Black Hills Lieutenant | Humanoid outlaw |
| 1450 | Auer | Humanoid outlaw |
| 1512 | Black Hills Grenadier | Humanoid outlaw |
| 1533 | Black Hills Leader | Humanoid outlaw |
| 1686 | Black Hills Lieutenant | Humanoid outlaw |
| 1766 | Black Hills Outlaw | Humanoid outlaw |
| 1767 | Black Hills Hound | Humanoid outlaw |
| 1768 | Black Hills Hound Wrangler | Humanoid outlaw |
| 1993 | The Cortador | Humanoid outlaw |
| 1997 | Black Hills Recruiter | Humanoid outlaw |
| 2396 | Black Hills Sniper | Humanoid outlaw |
| 2563 | Litch | Humanoid outlaw |
| 2564 | Cade | Humanoid outlaw |
| 2577 | Sergeant Kang | Humanoid outlaw |
| 2581 | Radic | Companion/critter |
| 2584 | Mitch | Humanoid outlaw |
| 2657 | Black Hills Shotgunner | Humanoid outlaw |
| 2658 | Black Hills Marauder | Humanoid outlaw |
| 2996 | Von Laar | Humanoid outlaw |
| 3247 | Hunting Kestrel | Humanoid outlaw |

#### Reapers — 16 named (+7 unnamed)

| id | name | race |
|----|------|------|
| 1506 | Reaper Privateer | Humanoid outlaw |
| 1514 | Reaper Raider | Humanoid outlaw |
| 1515 | Reaper Cannoneer | Humanoid outlaw |
| 1763 | Reaper Captain | Humanoid outlaw |
| 1764 | Reaper Parrot | Humanoid outlaw |
| 2294 | Reaper Lookout | Humanoid outlaw |
| 2295 | Reaper Powder Monkey | Humanoid outlaw |
| 2299 | Reaper Captain Cherise | Humanoid outlaw |
| 2335 | Reaper Bombardier | Humanoid outlaw |
| 2395 | Reaper Sniper | Humanoid outlaw |
| 2419 | Reaper Captain Cherise's Shield | Humanoid outlaw |
| 2662 | Reaper Brigand | Humanoid outlaw |
| 2700 | Reaper Corsair | Humanoid outlaw |
| 2703 | Reaper Grenadier | Humanoid outlaw |
| 2740 | Reaper | Humanoid outlaw |
| 2762 | Reaper SIN Hack Dealer | Humanoid outlaw |

#### Ophanim — 13 named (+3 unnamed)

| id | name | race |
|----|------|------|
| 1508 | Ophanim Soldier | Humanoid outlaw |
| 1520 | Ophanim Plasma Caster | Humanoid outlaw |
| 1521 | Ophanim Engineer | Humanoid outlaw |
| 1529 | Ophanim Commander | Humanoid outlaw |
| 1550 | Horkos | Humanoid outlaw |
| 1762 | Ophanim Barrier Drone | Humanoid outlaw |
| 2306 | Ophanim Trooper | Humanoid outlaw |
| 2308 | Ophanim Sniper | Humanoid outlaw |
| 2780 | Ophanim Commando | Humanoid outlaw |
| 3173 | Ophanim Lasher | Humanoid outlaw |
| 3174 | Ultra Ophanim | Humanoid outlaw |
| 3175 | Ophanim Kamikaze Bot | Wildlife (bugs) |
| 3176 | Duke Luka Zorin | Humanoid outlaw |

#### ? — 9 named (+8 unnamed)

| id | name | race |
|----|------|------|
| 821 | Fatima Belo | Human |
| 823 | Rosa Costa | Human |
| 825 | Old Felix Simoes | Human |
| 827 | Biff Meister | Human |
| 828 | Crotchety Earl | Human |
| 1397 | Accord Medical Officer | Human |
| 2002 | Accord Soldier | Human |
| 2939 | Ratman | Human |
| 2966 | Ratman | Human |

#### Tanken — 8 named

| id | name | race |
|----|------|------|
| 1758 | Tanken Gunman | Humanoid outlaw |
| 1760 | Tanken Sniper | Humanoid outlaw |
| 1882 | Tanken Mobster | Humanoid outlaw |
| 2311 | Tanken Hitman | Humanoid outlaw |
| 2312 | Tanken Cyborg Samurai | Humanoid outlaw |
| 2407 | Tanken Saboteur | Humanoid outlaw |
| 2513 | [OBSOLETE] Elite Tanken Gunman | Humanoid outlaw |
| 2514 | [OBSOLETE] Elite Tanken Mobster | Humanoid outlaw |

#### monster — 5 named (+12 unnamed)

| id | name | race |
|----|------|------|
| 715 | Target Drone | Misc (drones, targets, Necronus) |
| 1192 | L-98 | Misc (drones, targets, Necronus) |
| 1264 | Depreciate - Experiment gone bad | Misc (drones, targets, Necronus) |
| 2958 | Melding Tendril | Melded creature |
| 3205 | Target Drone | Misc (drones, targets, Necronus) |

#### Corporation - Omnidyne-M — 2 named

| id | name | race |
|----|------|------|
| 2999 | Karina Sokoloff | Human |
| 3099 | Broker Ochoa | Human |

#### Corporation - Astrek Association — 1 named

| id | name | race |
|----|------|------|
| 3098 | Agent Clarise | Human |

#### Corporation - Kisuton — 1 named

| id | name | race |
|----|------|------|
| 3096 | Dealer Augustus | Human |

### 4.3 Unnamed monster rows

The 1337 rows without a `localized_name_id` are mostly duplicate spawn variants, test/placeholder entries and per-instance copies. Their distribution by faction:

| faction | unnamed rows |
|---------|--------------|
| accord | 689 |
| gaea | 242 |
| chosen | 148 |
| bandit | 85 |
| melding | 77 |
| friendly | 31 |
| neutral | 16 |
| monster | 12 |
| Rebels | 11 |
| ? | 8 |
| Reapers | 7 |
| Black Hills Bandits | 6 |
| Ophanim | 3 |
| Civilian | 1 |
| Blackhats | 1 |

### 4.4 Turrets

`dbcharacter::Turret` carries plain-text names (107 rows):

| id | name |
|----|------|
| 1 | Minigun Turret |
| 2 | Mounted Turret 01 |
| 3 | Quad Cannon |
| 4 | Tank Turret |
| 5 | Mounted Turret 02 |
| 6 | Dropship Turret |
| 7 | Engineer Turret - I |
| 8 | Engineer Turret - II |
| 9 | Dropship MiniTurret |
| 10 | Tech Rocket Turret |
| 11 | Chosen Guard Tower |
| 12 | Multi Turret - I |
| 13 | Tech Flamethrower Turret |
| 14 | Tech AA Rocket Turret |
| 15 | Tech Riot Gun Turret |
| 16 | Hostile NPC Turret |
| 17 | Chosen Pillar Turret |
| 18 | Chosen Artillery Turret |
| 19 | Heavy Turret I (Engi Ability) |
| 20 | Heavy Turret II |
| 21 | UNUSED |
| 22 | Engineer Anti-Personnel Turret |
| 23 | Chosen Watchtower Turret |
| 24 | Tanken Turret I |
| 25 | H.A.W.K. Turret |
| 26 | Heavy Turret II (Rockets) |
| 27 | DEBUG chen's test turret |
| 28 | Outpost Heavy Turret I |
| 128 | Chosen Heavy Turret |
| 129 | Chosen Heavy Thumper Turret |
| 130 | DiamondHead Warfront Darkslip Mi |
| 131 | DiamondHead Warfront Darkslip Ma |
| 132 | MGV Miniguns |
| 133 | NPE Precursor Turret |
| 134 | LGV 1x Minigun |
| 135 | LGV missiles |
| 136 | Accord BattleCruiser MiniTurret |
| 137 | Accord Battlecruiser LargeTurret |
| 138 | LGV Machine Gun |
| 139 | Deprecated - Please Reuse This I |
| 140 | Omnidyn-m Heavy Turret |
| 141 | Chosen Heavy Turret 2 - Thumper |
| 142 | Heavy Turret I (Rocket) |
| 143 | Bloodkings Turret |
| 144 | Operation Mounted Turret 01 |
| 145 | Operation Mining Turret |
| 146 | Chosen Mortar |
| 147 | [OWPVP] Anti Personnel Turret |
| 148 | [OWPVP] Anti Air EMP Turret |
| 149 | [OWPVP] Anti Vehicle Turret |
| 150 | TEST Chosen Transport Gun |
| 151 | Chosen Warzone Mounted Turret |
| 152 | NotBeingUsed |
| 153 | Operation Beam Turret |
| 154 | Chosen Warzone Chosen Turret |
| 155 | Chosen Turret Core Mission 3 |
| 156 | Tower Defense Tech Flamethrower |
| 157 | Tower Defense Tech AA Rocket Tur |
| 158 | Tower Defense Tech Sniper Turret |
| 159 | Tower Defense Chemical Sprayer T |
| 160 | [OWPVP] Anti Vehicle Turret Upgr |
| 161 | [OWPVP] Anti Personnel Turret U |
| 162 | Heavy Chosen Pillar Turret |
| 163 | Heavy Chosen Watchtower Turret |
| 164 | Reaper Manable Turret |
| 165 | Crystite Aranha Turret |
| 166 | Sniper Mounted Turret 01 |
| 167 | Tower Defense Tech Riot Cryo Tur |
| 168 | NPE - Mushroom Rock Turret |
| 169 | [UNUSED] |
| 170 | Heavy Turret |
| 171 | Chosen Heavy Turret |
| 172 | CM5 Engineer Turret - I |
| 173 | Chosen Light Turret |
| 174 | Tower Defense Heavy Turret |
| 175 | Sniper Turret (Engi Ability) |
| 176 | Operation 002 - Reaper Gunship M |
| 177 | Core Mission 05 Flamethrow [CM5] |
| 178 | Chosen LGV Weapon Core Mission 3 |
| 179 | Missile Command Turret |
| 180 | Defense of Dredge AA Turret |
| 181 | Defense of Dredge Defense Turret |
| 182 | Gate Crasher - Anti-Air Turret |
| 183 | Operation Boss Beam Turret |
| 184 | [DoD] Defense of Dredge Defense |
| 185 | TEST Fog Finger |
| 186 | Operation Tesla Beam Turret - El |
| 187 | NPC Multi Turret |
| 188 | Operation 003 - Final Boss Beam |
| 189 | MGV missiles |
| 190 | [TEST] jwoe - Ship Turret Type |
| 191 | Melding Nano Missile Turret |
| 192 | [DTZE] Anti-Air Flak |
| 193 | [ZONE EVENT] DT Darkslip Shield |
| 194 | Devils' Tusk Chosen Mortar |
| 195 | [ZONE EVENT] DT Accord Gunship R |
| 196 | [DT Zone Event] Test rocket turr |
| 197 | [ZONE EVENT] DT Accord Gunship L |
| 198 | [ZONE EVENT] DT Accord Gunship M |
| 199 | Prototype Gunship Turret (Bottom |
| 200 | Prototype Gunship Turret (Top) |
| 201 | Leviathan Swivel Turret |
| 202 | Prototype Leviathan Turret |
| 203 | [DTZE] Anti-Air Flak #2 - More P |
| 204 | Accord Fighter Turret |
| 205 | obsolete |
| 206 | Chosen Medium Thumper Turret |

### 4.5 Monster scaling table

`dbcharacter::MonsterScaling` (80 rows) maps a monster level to its base
health/damage; monsters reference a row through `scaling_table_id`
(`dbcharacter::MonsterAttributeRange` adds per-attribute curves on top):

| level | health | damage |
|-------|--------|--------|
| 1 | 100 | 50 |
| 2 | 125 | 63 |
| 3 | 156 | 78 |
| 4 | 195 | 98 |
| 5 | 244 | 122 |
| 6 | 305 | 153 |
| 7 | 381 | 191 |
| 8 | 477 | 238 |
| 9 | 596 | 298 |
| 10 | 745 | 373 |
| 11 | 879 | 440 |
| 12 | 1,037 | 519 |
| 13 | 1,224 | 612 |
| 14 | 1,445 | 722 |
| 15 | 1,705 | 852 |
| 16 | 2,011 | 1,006 |
| 17 | 2,373 | 1,187 |
| 18 | 2,801 | 1,400 |
| 19 | 3,305 | 1,652 |
| 20 | 3,900 | 1,950 |
| 21 | 4,289 | 2,145 |
| 22 | 4,718 | 2,359 |
| 23 | 5,190 | 2,595 |
| 24 | 5,709 | 2,855 |
| 25 | 6,280 | 3,140 |
| 26 | 6,908 | 3,454 |
| 27 | 7,599 | 3,800 |
| 28 | 8,359 | 4,179 |
| 29 | 9,195 | 4,597 |
| 30 | 10,114 | 5,057 |
| 31 | 10,923 | 5,462 |
| 32 | 11,797 | 5,899 |
| 33 | 12,741 | 6,371 |
| 34 | 13,760 | 6,880 |
| 35 | 14,861 | 7,431 |
| 36 | 16,050 | 8,025 |
| 37 | 17,334 | 8,667 |
| 38 | 18,721 | 9,360 |
| 39 | 20,219 | 10,109 |
| 40 | 21,836 | 10,918 |
| 41 | 22,928 | 11,464 |
| 42 | 24,074 | 12,037 |
| 43 | 25,278 | 12,639 |
| 44 | 26,542 | 13,271 |
| 45 | 27,869 | 13,934 |
| 46 | 29,262 | 14,631 |
| 47 | 30,726 | 15,363 |
| 48 | 32,262 | 16,131 |
| 49 | 33,875 | 16,937 |
| 50 | 35,569 | 17,784 |
| 51 | 37,347 | 18,674 |
| 52 | 39,214 | 19,607 |
| 53 | 41,175 | 20,588 |
| 54 | 43,234 | 21,617 |
| 55 | 45,396 | 22,698 |
| 56 | 47,665 | 23,833 |
| 57 | 50,049 | 25,024 |
| 58 | 52,551 | 26,276 |
| 59 | 55,179 | 27,589 |
| 60 | 57,938 | 28,969 |
| 61 | 60,834 | 30,417 |
| 62 | 63,876 | 31,938 |
| 63 | 67,070 | 33,535 |
| 64 | 70,424 | 35,212 |
| 65 | 73,945 | 36,972 |
| 66 | 77,642 | 38,821 |
| 67 | 81,524 | 40,762 |
| 68 | 85,600 | 42,800 |
| 69 | 89,880 | 44,940 |
| 70 | 94,374 | 47,187 |
| 71 | 99,093 | 49,546 |
| 72 | 104,048 | 52,024 |
| 73 | 109,250 | 54,625 |
| 74 | 114,713 | 57,356 |
| 75 | 120,448 | 60,224 |
| 76 | 126,471 | 63,235 |
| 77 | 132,794 | 66,397 |
| 78 | 139,434 | 69,717 |
| 79 | 146,405 | 73,203 |
| 80 | 153,726 | 76,863 |

### 4.6 Known PIN test ids, cross-checked

| id | DB name | faction | note |
|----|---------|---------|------|
| 290 | Accord Assault | accord | Friendly (Accord) — unshootable by players |
| 528 | Melded Aranha | melding | Hostile melded bug |
| 1196 | Chosen Fiend | chosen | Hostile Chosen |
| 1304 | Black Hills Bandit | Black Hills Bandits | Hostile bandit |
| 2342 | Aranha | gaea | Hostile bug |
| 2407 | Tanken Saboteur | Tanken | Hostile Tanken |
| 356 | Aero | accord | Named NPC (New Eden test spawn) |

### 4.8 Vehicles (`vcs::VehicleInfo`) — 173 rows, all named

Spawn with `\vehicle <id>` or `\spawn vehicle <id|name> [<x> <y> <z>]`.
`class` is `vcs::VehicleClass`: 1 = ground vehicle, 2 = cart/utility, 3 = air.

| id | name | faction | class |
|----|------|---------|-------|
| 13 | Accord Dropship | accord | 3 |
| 26 | Chosen Darkslip | chosen | 3 |
| 28 | Cobra Cycle | accord | 1 |
| 35 | Resonator Bomb | accord | 1 |
| 36 | Locust Chopper | accord | 1 |
| 37 | Triton Cycle | accord | 1 |
| 38 | Courier Cycle | accord | 1 |
| 41 | Chosen Cycle | — | 1 |
| 43 | Vespa Cycle | accord | 1 |
| 47 | Cobra P-39 | accord | 1 |
| 48 | Terromoto Cobra Cycle | accord | 1 |
| 49 | Oilspill's Dropship | neutral | 3 |
| 50 | Cobra K-12 | accord | 1 |
| 51 | Oilspill's Dropship | neutral | 3 |
| 52 | Accord Dropship | accord | 3 |
| 53 | Thumper Cart | accord | 2 |
| 54 | Cobra R-54 | accord | 1 |
| 56 | ThumperCart Thumper | accord | 1 |
| 57 | ThumperCart Cart | friendly | 1 |
| 59 | Omnidyne-M LGV | accord | 1 |
| 60 | Holo Whale | accord | 1 |
| 64 | Accord Black Ops Drop Ship | accord | 3 |
| 66 | Convoy | accord | 0 |
| 71 | Jaguar K-17 MGV | accord | 5 |
| 80 | Cobra Cycle | accord | 1 |
| 81 | _Chosen Darkslip | chosen | 3 |
| 82 | Chosen Darkslip | chosen | 3 |
| 83 | Vapor Cycle | accord | 1 |
| 86 | Oilspill's Dropship | accord | 3 |
| 87 | Oilspill's Dropship | accord | 3 |
| 88 | Vortex Cycle | accord | 1 |
| 89 | Accord Armored Dropship | accord | 3 |
| 90 | U.A.S. Vanguard | accord | 6 |
| 91 | Accord Armored Dropship | accord | 3 |
| 92 | Harbinger Shields | chosen | 3 |
| 93 | Bloodkings Dropship | bandit | 3 |
| 94 | Oilspill's Dropship | accord | 3 |
| 95 | Zephyr Cycle | accord | 1 |
| 97 | Convoy Mobile AA | accord | 0 |
| 99 | Chosen General Zod's Super Darkslip | chosen | 3 |
| 100 | Interceptor Assault  | accord | 1 |
| 101 | Chosen Darkslip | chosen | 3 |
| 102 | Arclight Missile | accord | 3 |
| 103 | Chosen Darkslip | chosen | 3 |
| 104 | Accord Gunship | accord | 3 |
| 105 | Accord Armored Dropship | accord | 3 |
| 106 | Chosen Darkslip | chosen | 3 |
| 107 | Lancer M3 | accord | 1 |
| 108 | DH Armored Dropship | accord | 3 |
| 109 | Chosen Darkslip | chosen | 3 |
| 110 | Cobra P-1 | accord | 1 |
| 111 | Accord Dropship | accord | 3 |
| 112 | Ranger All-Terrain MGV | accord | 5 |
| 113 | Blitz Assault LGV | accord | 1 |
| 114 | Convoy (NonDrivable) | accord | 0 |
| 115 | U.A.S. Vanguard | accord | 6 |
| 116 | Cobra XLR | accord | 1 |
| 117 | Bumblebee | accord | 5 |
| 118 | Wasteland Armored Dropship | accord | 3 |
| 119 | Thumper Cart  | accord | 2 |
| 121 | BSU Test Vehicle | accord | 5 |
| 122 | Convoy MGV | accord | 5 |
| 123 | TEST Convoy MGV | accord | 5 |
| 124 | Rental Cobra R-54 | accord | 1 |
| 125 | Red Line Accord Dropship | accord | 3 |
| 126 | Blue Line Accord Dropship | accord | 3 |
| 127 | RX1 Resource Hauler | accord | 0 |
| 128 | Cheetah Model S | accord | 1 |
| 129 | Cobra Turbo LGV | accord | 1 |
| 130 | Locust Turbo Chopper | accord | 1 |
| 131 | Cobra Turbo P-39 | accord | 1 |
| 132 | Terromoto Turbo Cobra | accord | 1 |
| 133 | Cobra Turbo K-12 | accord | 1 |
| 134 | Omnidyne-M Turbo | accord | 1 |
| 135 | Cobra Turbo P-1 | accord | 1 |
| 136 | Cobra Turbo XLR | accord | 1 |
| 137 | Cobra Turbo R-54 | accord | 1 |
| 138 | Vapor Turbo Cycle | accord | 1 |
| 139 | Vortex Turbo Cycle | accord | 1 |
| 140 | Zephyr Turbo Cycle | accord | 1 |
| 141 | TEST Repulsor Convoy MGV | accord | 5 |
| 142 | TEST Convoy MGV No Cargo | accord | 5 |
| 143 | Chosen Darkslip | chosen | 3 |
| 144 | Accord Cargo Ship | accord | 2 |
| 145 | TEST Operation Vehicle | accord | 5 |
| 146 | OLD Chosen Cycle | — | 1 |
| 147 | Brahma Transport | accord | 3 |
| 148 | Chosen Sweeper | chosen | 1 |
| 149 | Vanilla MGV | accord | 5 |
| 150 | TS-RC8 "Snowsquall" MGV | accord | 5 |
| 151 | Jaguar K-27 Turbo Assault Vehicle | accord | 5 |
| 152 | RX2 Re-enforced Resource Hauler | accord | 0 |
| 153 | Interceptor RX Turbo Assault | accord | 1 |
| 154 | Blitz Assault Turbo LGV | accord | 1 |
| 155 | Chosen Darkslip | chosen | 3 |
| 156 | Reaper Armored Dropship | Reapers | 3 |
| 157 | Surf Board | accord | 1 |
| 159 | Reaper Armored Gunship | Reapers | 3 |
| 160 | Reaper APC - Old Test | bandit | 0 |
| 161 | Weekly Convoy MGV | accord | 5 |
| 162 | Chosen Cycle | — | 1 |
| 163 | MGV_MoneyBomb_Base | accord | 5 |
| 164 | MGV_MoneyBomb_VIP | accord | 5 |
| 165 | Chosen Dropship | chosen | 3 |
| 167 | Mine Cart | accord | 2 |
| 168 | Repulsor Generator | accord | 2 |
| 169 | Wooden Barrel | accord | 2 |
| 170 | Test Derby LGV | accord | 1 |
| 171 | Test Brontodon | accord | 1 |
| 172 | Operation Test MGV | accord | 5 |
| 173 | Test Brontodon - Flight Path | accord | 1 |
| 174 | APC | accord | 0 |
| 175 | Drivable APC | accord | 0 |
| 176 | Reaper APC | Reapers | 0 |
| 178 | Grasshopper K-18 | accord | 1 |
| 179 | Lancer Gold | accord | 1 |
| 180 | Elevator  | accord | 4 |
| 181 | [PVP] TDM Respawner | accord | 3 |
| 182 | TransHub Ship | accord | 6 |
| 183 | AA Turret Cart | friendly | 1 |
| 184 | Agrievan | chosen | 2 |
| 185 | ARES-Team Transport | accord | 0 |
| 186 | Abe's test LGV | accord | 1 |
| 187 | Accord Supply Dropship | accord | 3 |
| 189 | Accord A-10 Mamba LGV | accord | 1 |
| 190 | Communications Array | accord | 2 |
| 191 | ARES-Team Transport Non-Drivable | accord | 0 |
| 192 | A_LGVCycle04 | accord | 1 |
| 194 | Oilspill's Dropship | accord | 3 |
| 195 | MoonFestival LGV | accord | 1 |
| 197 | Blazing Hope LGV | accord | 1 |
| 198 | Blazing Hope MGV | accord | 5 |
| 199 | Operation Test - Doesnt Work.  | accord | 0 |
| 200 | Mobile AA | accord | 0 |
| 201 | A1 Hauler | accord | 0 |
| 202 | _M05_Mi751_CM4_No_Exit - Accord Dropship | accord | 3 |
| 203 | Bsu_TestLGV | accord | 1 |
| 204 | Infinite LGV | accord | 1 |
| 205 | Forester MGV | accord | 5 |
| 206 | Fury Monster MGV | accord | 5 |
| 207 | Tarantula LGV | accord | 1 |
| 208 | Accord Dropship | accord | 3 |
| 209 | Phoenix LGV | accord | 1 |
| 210 | Sonic Wave LGV | accord | 1 |
| 211 | Death Rose LGV | accord | 1 |
| 212 | Wood Pecker LGV | accord | 1 |
| 213 | Celebrity MGV | accord | 5 |
| 214 | Rising Star LGV | accord | 1 |
| 215 | Top Dog LGV | accord | 1 |
| 216 | Ace LGV | accord | 1 |
| 218 | Omnidyne-M Flagship MGV | accord | 5 |
| 219 | Hummingbird Racer | accord | 1 |
| 220 | Mosquito Racer | accord | 1 |
| 221 | Kestrel Racer | accord | 1 |
| 222 | Black Widow Racer | accord | 1 |
| 223 | Cherub LGV | accord | 1 |
| 224 | Blue Kestrel (level 50 epic mgv) | accord | 5 |
| 225 | [TEST] jwoe - DT gunship | accord | 3 |
| 226 | Accord Disintegrator Gunship | accord | 3 |
| 227 | Chosen Darkslip | chosen | 3 |
| 228 | Devil's Tusk Zone Event - Accord Dropship | accord | 3 |
| 229 | [Raid] Trolly | friendly | 1 |
| 230 | Prototype Assault Gunship | accord | 3 |
| 231 | Chosen Cycle | — | 1 |
| 232 | Chosen Raptor | chosen | 1 |
| 233 | Accord Liberator Class Dropship | accord | 3 |
| 234 | U.A.S. Victory | accord | 6 |
| 235 | Chosen Gunship (level 50 epic) | chosen | 3 |
| 335 | Accord Player Fighter | accord | 3 |
| 336 | Accord Armored Dropship | accord | 3 |
| 337 | Accord Medivac Dropship | accord | 3 |
| 338 | Chosen Raptor | chosen | 1 |
| 339 | Copy of Jaguar K-17 MGV TESTING | accord | 5 |

### 4.9 Carryables (`dbitems::CarryableObject`) — 105 rows, 71 named

Spawn with `\carryable <id>` or `\spawn carryable <id|name> [<x> <y> <z>]`.
The 34 unnamed rows (ids 2–9, 12–20, …) are internal placeholders and are only
reachable by id.

| id | name | type | pickup radius |
|----|------|------|----------------|
| 10 | Crashed Thumper Part | 1 | 5.0 |
| 11 | Ball | 2 | 2.0 |
| 21 | SIN Liquid Canister | 1 | 2.0 |
| 22 | Red Resonator | 2 | 2.0 |
| 23 | White Resonator | 2 | 2.0 |
| 24 | Black Resonator | 2 | 2.0 |
| 26 | Accord Datapad | 1 | 2.0 |
| 27 | Drill Parts | 1 | 2.0 |
| 29 | Tainted Crystite | 1 | 2.0 |
| 30 | Crystite Core | 1 | 2.0 |
| 31 | Chosen Energy Source | 1 | 2.0 |
| 32 | Civilian Personal Effects | 1 | 2.0 |
| 33 | Medical Supplies | 1 | 2.0 |
| 37 | Jetball | 2 | 2.0 |
| 38 | Explosives | 1 | 1.0 |
| 39 | Thumper Repair Unit | 1 | 2.0 |
| 40 | Disposable Jetball | 2 | 2.0 |
| 141 | Harvester Part | 1 | 2.0 |
| 142 | Bandit Datapad | 1 | 2.0 |
| 144 | Bridge Parts | 1 | 1.0 |
| 145 | Server Key A | 1 | 0.0 |
| 146 | Server Key B | 1 | 0.0 |
| 147 | Server Key C | 1 | 0.0 |
| 152 | Biotoxin Sample | 1 | 2.0 |
| 155 | Anti Personnel Turret | 2 | 2.0 |
| 156 | Accord Gate Key | 1 | 2.0 |
| 157 | BUD | 2 | 3.0 |
| 158 | Delirium Engine Core | 1 | 2.0 |
| 159 | The One-of-Many Ring | 1 | 2.0 |
| 161 | Headless Horseman's Head | 2 | 2.0 |
| 162 | Coolant | 1 | 2.0 |
| 163 | Present | 1 | 2.0 |
| 164 | Nutrepaste | 1 | 2.0 |
| 165 | Green Crystal | 1 | 2.0 |
| 166 | Red Crystal | 1 | 2.0 |
| 167 | Yellow Crystal | 1 | 2.0 |
| 168 | Datapad | 1 | 2.0 |
| 169 | Resonance Accelerator | 1 | 2.0 |
| 170 | Repulsor Parts | 1 | 1.0 |
| 175 | Cargo | 1 | 1.0 |
| 176 | Cargo | 1 | 1.0 |
| 177 | Cargo | 1 | 1.0 |
| 178 | Scrambler Grenade | 1 | 3.0 |
| 180 | Accord Weapon Supplies | 2 | 2.0 |
| 181 | Door Access Keycard | 1 | 2.0 |
| 185 | Crashed Sleigh Part | 1 | 5.0 |
| 189 | Nautilus Bait | 1 | 2.0 |
| 191 | Explosive Charge | 1 | 3.0 |
| 193 | Jetball | 2 | 4.0 |
| 194 | Keycard | 1 | 2.0 |
| 196 | Disarmed Proximity Mine | 1 | 2.0 |
| 197 | Disarmed Proximity Mine | 1 | 2.0 |
| 198 | Generator Repair Parts | 1 | 1.0 |
| 199 | Tissue Sample | 1 | 2.0 |
| 200 | Poison Vial | 1 | 2.0 |
| 201 | Relic Container | 1 | 2.0 |
| 202 | Encryption Codes | 1 | 2.0 |
| 203 | Datacrypt | 1 | 2.0 |
| 204 | Box of Gadgets | 1 | 2.0 |
| 205 | Box of Supplies | 1 | 2.0 |
| 206 | Chosen Implant | 1 | 2.0 |
| 207 | Drone Component | 1 | 2.0 |
| 208 | Datakey | 1 | 2.0 |
| 209 | Chosen Transmitter | 1 | 2.0 |
| 210 | Stolen Crystite | 1 | 2.0 |
| 211 | Torque Ring | 1 | 2.0 |
| 212 | Weapons Crate | 1 | 2.0 |
| 213 | Ball | 2 | 2.0 |
| 214 | Chosen Tech | 1 | 2.0 |
| 215 | SIN Implant | 1 | 2.0 |
| 216 | Bandit Laundry | 1 | 2.0 |

### 4.7 Regenerating this catalog


```sh
python3 Tools/SdbDump/sdb_dump.py monsters /path/to/clientdb.sd2 -o monsters.json
```

## 5. Regenerating the catalog

`Tools/SdbDump` is a dependency-free decoder for `.sd2` files (same format
logic as the `FauFau` library PIN uses; verified by its round-trip self-test).

```sh
# Full mobs report (monsters + names + factions + scaling + turrets)
python3 Tools/SdbDump/sdb_dump.py monsters /path/to/clientdb.sd2 -o monsters.json

# Everything spawnable (monsters, deployables, vehicles, carryables, turrets)
python3 Tools/SdbDump/sdb_dump.py spawnables /path/to/clientdb.sd2 -o spawnables.json
python3 Tools/SdbDump/sdb_dump.py spawnables /path/to/clientdb.sd2 vehicle

# How much of the file PIN actually loads
python3 Tools/SdbDump/sdb_dump.py coverage /path/to/clientdb.sd2

# Anything else, table by table
python3 Tools/SdbDump/sdb_dump.py dump /path/to/clientdb.sd2 dbcharacter::Faction
python3 Tools/SdbDump/sdb_dump.py info   /path/to/clientdb.sd2
```

The tool never needs a Firefall installation, and the `.sd2` itself is game
data that stays out of the repository.
