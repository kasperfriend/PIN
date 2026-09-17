# In-game chat commands

Every command this server understands when you type it in chat, in one place — the same list the
in-game `\help` command prints (plus the admin-channel commands it does not cover).

## How they work

* A chat message that starts with a backslash (`\`) is a command, on **any** chat channel:
  `\heal 50`, `\spawn monster 1000`, ... The rest of the line is split on spaces into parameters.
* Command names and aliases are case-insensitive.
* A command that is not found answers `Unknown command: <name>` in the debug chat window.
* Most commands answer in the **chat window** (the debug chat channel). A few print multi-line
  listings (command lists, static database rows, ability chains) to the **in-game console** instead —
  the chat window gets a short notice that the output is in the console.
* Two command sets exist:
  * **Chat commands** (`UdpHosts/GameServer/Systems/Chat/Commands`) — the ones in §1. Every player
    can use them; several are thin forwards onto the admin commands in §2.
  * **Admin (server) commands** (`UdpHosts/GameServer/Systems/Admin/Commands`) — the ones in §2.
    They are executed when the message is sent on the **Admin chat channel** (the command typed in
    the admin chat box). The in-game `\help` does *not* list these; `AdminService.GetCommandList`
    does.

## 1. Chat commands (what `\help` lists)

| Command | Aliases | Usage | What it does |
|---|---|---|---|
| `\abilityinfo` | `abi`, `ability`, `aptitudeinfo` | `abilityinfo [abilityId]` | Inspect how the current loadout slots map to ability modules and dump their aptitude chains (console) |
| `\ai` | `mobai` | `ai [on\|off\|status\|list\|routines]` | Inspect and toggle server-side NPC AI. Default: `status` |
| `\bags` | `fixbags`, `bagfix` | `bags [fix\|status]` | Fix a stuck "inventory full" red blink and show bag usage (forces a bag-model sync) |
| `\balance` | `showwallet`, `showcurrencies` | `balance` | Show your wallet: every stacked resource pool with SDB id and quantity |
| `\crystite` | `cy`, `money`, `cash` | `crystite [amount]` | Add crystite (vendor currency) to your wallet. No amount = 100k, e.g. `\crystite 500000` |
| `\dmg` | — | `dmg <multiplier>` | Set your outgoing damage multiplier: `1` normal, `10` x10, `-1` one-hit-kill, `0` no damage |
| `\down` | `bleed` | `down` | Put your own character into bleedout |
| `\fall` | `falldamage` | `fall <speed>` | Simulate a fall impact at the given downward speed (u/s): `12` safe, `20` big hit, `48+` lethal |
| `\frame` | `setframe` | `frame <frameName\|frameId>` | Switch your battleframe on the fly (e.g. `assault`, `dreadnaught`, `recon`, `firecat`) |
| `\heal` | — | `heal <amount>` | Heal your own character |
| `\health` | `vitals`, `hp` | `health` | Print your vitals: health, shields, character/lifecycle state |
| `\help` | `listcmd`, `listcmds`, `cmdlist`, `cmds` | `help` | Print a list of all chat commands (console) |
| `\hurt` | `damage` | `hurt <amount>` | Damage your own character |
| `\kill` | `suicide` | `kill` | Kill your own character |
| `\killaura` | `aura` | `killaura [<radius>\|on\|off]` | Kill every enemy around you once per second. No argument = toggle, `on`/`off` explicit, `<radius>` sets the radius |
| `\leaderboard` | `scoreboard` | `leaderboard <id>` | Open a leaderboard by id (authorizes a terminal of type 14) |
| `\level` | `setlevel` | `level <1-50>` | Set your battleframe progression level |
| `\npc` | `character`, `monster`, `spawn_npc`, `spawn_character`, `spawn_monster` | `npc <characterTypeId> [<x> <y> <z>]` | Spawn a mob (NPC/monster) by characterTypeId, optionally at a location (position required if you have no character in the world) |
| `\population` | `pop` | `population [on\|off\|status\|near [radius]]` | Inspect and toggle world population (the zone's monsters and NPCs from the database). `near` lists up to 15 NPCs within a radius (default 100) |
| `\resource` | `addresource`, `giveresource`, `addcurrency`, `givecurrency`, `currency` | `resource <sdbId> [amount]` | Add any stacked currency/resource to your wallet by SDB id, e.g. `\resource 10 500000` (10 = crystite) |
| `\respawn` | — | `respawn` | Force respawn your character |
| `\revive` | — | `revive` | Revive your own character from bleedout |
| `\sdb` | `sdblist`, `sdbsearch`, `sdbfind` | `sdb [<monster\|deployable\|vehicle\|carryable\|turret>] [<id\|name filter>] [limit]` | Browse/search the static database catalog of spawnable things (console) |
| `\sdbinfo` | `sdbshow`, `sdbrow` | `sdbinfo <type> <id\|name>` | Show the static database row behind a spawnable id or name (console) |
| `\sethp` | `hpme`, `fullhp` | `sethp [<amount>]` | No argument resets your health to the database value, `sethp <amount>` sets it |
| `\spawn` | `sdbspawn`, `spawn_sdb` | `spawn <monster\|deployable\|vehicle\|carryable\|turret> <id\|name> [<x> <y> <z>]` | Spawn anything from the static database by id or name, optionally at a location |
| `\wallet` | `fillwallet`, `givewallet`, `refill`, `currencies` | `wallet [all\|amount]` | Fill your wallet for vendors: crystite + vending tokens. `wallet` = 500k cy + 100 tokens, `wallet all` tops every fallback resource too |

### Notes

* **Forwards onto admin commands** — these chat commands simply forward to the identically-named
  (or related) admin command in §2, so both spellings behave the same: `ai`, `bags`, `balance`,
  `crystite`, `dmg`, `down` → `downme`, `frame` → `setframe`, `heal` → `healme`, `hurt` →
  `hurtme`, `kill` → `killme`, `killaura`, `level` → `setlevel`, `population`, `resource`,
  `respawn`, `revive`, `sethp` → `hp`, `wallet`. `spawn`, `sdb` and `sdbinfo` do not forward but
  call the same `SDBSpawner` implementation the admin spellings do, so both behave identically.
  The rest (`abilityinfo`, `fall`, `health`, `help`, `leaderboard`, `npc`) run server-side in the
  chat command itself.
* **Console output** — `abilityinfo`, `help`, `sdb`, `sdbinfo` print through
  `INetworkClient.SendDebugLog` (a `TempConsoleMessage`), i.e. the in-game console; long listings are
  split into ~700-character chunks. Everything else answers in the chat window.
* `\leaderboard` takes exactly one parameter — the `[id]` in the attribute is misleading, omitting it
  just prints the usage hint.
* `\ai` also accepts `enable`/`disable`/`1`/`0` for `on`/`off`; `\population` the same.
* Source: the `[ChatCommand]` attributes in
  `UdpHosts/GameServer/Systems/Chat/Commands/*.cs`, discovered by reflection at shard start.

## 2. Admin (server) commands — Admin chat channel

Typed in the **admin chat channel**; `AdminService` dispatches them. The full set, in the same
alphabetical style:

| Command | Aliases | Usage | What it does |
|---|---|---|---|
| `ai` | `mobai` | `ai [on\|off\|status\|list\|routines]` | Inspect and toggle server-side NPC AI |
| `applyeffect` | `apply_effect`, `apt_apply` | `applyeffect <effectId>` | Apply a status effect |
| `bags` | `fixbags`, `bagfix`, `inventorybags`, `bagstatus` | `bags [fix\|status]` | Fix a stuck "inventory full" red blink and show bag usage |
| `balance` | `showwallet`, `showcurrencies`, `currencies_list`, `listcurrencies` | `balance` | Show your wallet: every stacked resource pool with SDB id and quantity |
| `cancelfm` | — | `cancelfm <commandId>` | Cancel a ForcedMovement command |
| `carryable` | `spawn_carryable` | `carryable <carryableTypeId> [<x> <y> <z>]` | Spawn a carryable by id, optionally at a location |
| `clear` | `targetclear`, `cleartarget`, `untarget`, `removetarget`, `remtarget`, `deletetarget`, `deltarget` | `clear` | Clear the target used by target-aware server commands |
| `cflags` | `cflag` | `cflags [value]` | Set character CombatFlags |
| `createitem` | `create_item`, `giveitem`, `give_item` | `createitem <typeId>` | Add an item to your inventory |
| `crystite` | `cy`, `money`, `cash`, `addcrystite`, `givecrystite` | `crystite [amount]` | Add crystite to your wallet. No amount = 100k |
| `dbg_weapon` | — | `dbg_weapon` | Print server weapon template info (console) |
| `dbgattach` | — | `dbgattach <unk2> <unk3>` | Debug the AttachedTo state |
| `deployable` | `spawn_deployable` | `deployable <deployableTypeId> [<x> <y> <z>]` | Spawn a deployable by id, optionally at a location |
| `dmg` | `damagemult`, `cheatdamage` | `dmg <0.5\|1\|2\|-1>` | Set your outgoing damage multiplier; `-1` one-hit-kill, `1` reset |
| `downme` | `down`, `bleed`, `bleedout` | `downme` | Down your character instantly |
| `emote` | — | `emote <id>` | Perform an emote by id (only displays on remote views) |
| `healme` | — | `healme <amount>` | Heal your character |
| `help` | `listcmd`, `listcmds`, `cmdlist`, `cmds` | `help` | Print a list of all server commands (console) |
| `hp` | `sethealth`, `resethealth` | `hp [<amount>]` | Reset (no argument) or set the health of your character (or your target) |
| `hurtme` | — | `hurtme <amount>` | Take damage on your character |
| `killaura` | `aura` | `killaura [<radius>\|on\|off]` | Kill every enemy around you once per second |
| `killme` | `suicide`, `die` | `killme` | Kill your character instantly |
| `listeffects` | `list_effects`, `apt_status`, `apt_list` | `listeffects` | Log the current status effects |
| `npc` | `character`, `monster`, `spawn_npc`, `spawn_character`, `spawn_monster` | `npc <characterTypeId> [<x> <y> <z>]` | Spawn a character/mob by id, optionally at a location |
| `pflags` | `pflag`, `float` | `pflags [value]` | Set character PermissionFlags |
| `population` | `pop` | `population [on\|off\|status\|near [radius]]` | Inspect and toggle world population |
| `rarmy` | `reloadarmy` | `rarmy` | Reload the Army UI |
| `removeeffect` | `remove_effect`, `apt_remove`, `apt_clear`, `apt_cancel` | `removeeffect <effectId>` | Remove a status effect |
| `resource` | `addresource`, `giveresource`, `addcurrency`, `givecurrency`, `currency` | `resource <sdbId> [amount]` | Add any stacked currency/resource to your wallet by SDB id |
| `respawn` | `force_respawn` | `respawn` | Force respawn your character |
| `revive` | `reviveme` | `revive` | Revive your character from downed/bleedout |
| `rment` | `killall` | `rment` | Remove all entities except player characters |
| `say` | — | `say <message>` | Send a chat message to all clients as the server (admin channel) |
| `sdb` | `sdblist`, `sdbsearch`, `sdbfind` | `sdb [<type>] [<id\|name filter>] [limit]` | Browse/search the static database catalog (console) |
| `sdbinfo` | `sdbshow`, `sdbrow` | `sdbinfo <type> <id\|name>` | Show the static database row behind a spawnable id or name (console) |
| `setframe` | `frame`, `switchframe` | `setframe <frameName\|frameId>` | Switch your (or your target's) battleframe on the fly |
| `setlevel` | `level`, `playerlevel` | `setlevel <1-50>` | Set your (or your target's) battleframe progression level |
| `spawn` | `sdbspawn`, `spawn_sdb` | `spawn <type> <id\|name> [<x> <y> <z>]` | Spawn anything from the static database by id or name |
| `target` | — | `target [entityId/name]` | Set the target used by target-aware server commands (`hp`, `setframe`, `setlevel`) |
| `tmpeq` | `tmpeq_list`, `tmpeqlist` | `tmpeq` | List the temporary equipment overrides |
| `tmpeq_clear` | `tmpeqclear` | `tmpeq_clear` | Remove all temporary equipment overrides |
| `tmpeq_remove` | `tmpeqremove` | `tmpeq_remove <slot>` | Remove one temporary equipment override |
| `tmpeq_set` | `tmpeqset` | `tmpeq_set <slot> <itemId>` | Set a temporary equipment override |
| `tp` | `teleport` | `tp <x> <y> <z>` | Teleport your character |
| `vehicle` | `spawn_vehicle` | `vehicle <vehicleTypeId> [<x> <y> <z>]` | Spawn a vehicle by id, optionally at a location |
| `wallet` | `fillwallet`, `givewallet`, `refill`, `currencies` | `wallet [all\|amount]` | Fill your wallet for vendors (crystite + tokens) |

`<type>` in `sdb` / `sdbinfo` / `spawn` is one of `monster`, `deployable`, `vehicle`, `carryable`,
`turret`.

* `spectate` (`SpectateServerCommand`) exists in source but is **commented out** — it is not
  registered and cannot be run.
* Source: the `[ServerCommand]` attributes in
  `UdpHosts/GameServer/Systems/Admin/Commands/*.cs`.

## 3. Text restrictions: keep command output ASCII

The chat and console messages above travel in `[AeroString]` fields with the **null-terminated**
string mode. The Aero source generator sizes the pack buffer from the string's *character* count
(`1 + text.Length`) but writes its *UTF-8 byte* count — the two only agree for ASCII. A single
multi-byte character (an em dash `—` used to appear in the `\crystite` and `\resource` descriptions)
made `TempConsoleMessage.Pack` write past the end of the buffer and throw
`ArgumentOutOfRangeException` out of the client's network tick, logged as
`HandlePacket Caught Specified argument was out of the range of valid values`.

Rules to keep it that way:

* **Write command descriptions, usage strings and feedback messages in plain ASCII.**
* As a backstop, all server-generated text is run through
  `AeroTextExtensions.AsAeroSafeText()` before it is packed — in
  `INetworkClient.SendDebugLog` (`TempConsoleMessage`) and in `ChatService.PrepareSingleMessage`
  (`ChatMessageList`, which also relays raw player chat). It replaces every non-ASCII character with
  `?` rather than letting a bad byte sequence take the sender's network tick down. A `?` in output
  is a signal that something to fix, not a feature.
