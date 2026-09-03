# Characters & Battleframes — `characters.json` Guide

> This document is the single source of truth for the `characters.json` system introduced in the current build. It replaces the old hardcoded character blobs that caused the selection screen and in-game avatar to diverge.

---

## 1. Overview

`characters.json` is a **process-wide, file-backed store** of player characters:

* Both **WebHostManager** (REST `ClientApi` → character selection screen) and **GameServer** (via gRPC `GameServerAPI`) read from the **same file**, so the character you pick is the character you spawn as.
* On first run the store is **seeded** with 38 entries — one per zone — from `CharacterStore.SeedZones`.
* When you switch battleframes in-game (`SelectLoadout`), the GameServer calls `SaveCurrentBattleframeAsync` → gRPC `SaveCurrentBattleframe` → `CharacterStore.UpdateCurrentBattleframe`, which updates the JSON and the selection screen instantly.
* When you log out / teleport, `SaveGameSessionData` persists `LastZoneId`, `LastOutpostId`, `TimePlayed`, `LastSeenAt`.

Implementation:

* `Lib/Shared.Common/Characters/CharacterStore.cs`
* `Lib/Shared.Common/Characters/CharacterRecord.cs`
* `Lib/Shared.Common/DefaultCharacterTemplate.cs`
* `WebHosts/WebHost.ClientApi/Characters/CharactersRepository.cs`
* `WebHosts/WebHost.GameServerApi/Services/GameServerApiService.cs`
* `UdpHosts/GameServer/GRPC/GRPCService.cs`
* `UdpHosts/GameServer/Controllers/Character/BaseController.cs` → `SelectLoadout`

---

## 2. File Location

Default:

```
<WebHostManager binary folder>/characters.json
# e.g. WebHosts/WebHostManager/bin/Release/net10.0/characters.json
# In release zip: next to WebHostManager.exe
```

Custom path via config `WebHosts/WebHostManager/config/appsettings.json`:

```json
{
  "Firefall": {
    "GameServerApi": {
      "Port": 5201,
      "CharacterStorePath": "C:\\PIN\\data\\characters.json"
    }
  }
}
```

* Empty `CharacterStorePath` = default location.
* Path is created automatically if missing.
* The GameServer **must** point at the same file indirectly: it talks to `WebHostManager` over gRPC at `http://localhost:5201` (`GrpcChannelAddress` in `GameServerSettings.cs`). If `WebHostManager` is not running, GameServer logs a warning and falls back to `HardcodedCharacterData.FallbackData`.

Environment variable override also works (ASP.NET config):

```
Firefall__GameServerApi__CharacterStorePath=/opt/pin/characters.json
Firefall__GameServerApi__Port=5201
```

### How persistence works

* `CharacterStore.Init(path)` loads JSON → `ConcurrentDictionary<ulong, CharacterRecord>`.
* Corrupt file → ignored, reseeded.
* `Save()` writes to `characters.json.tmp` then atomically moves → no truncated file on crash.
* `Save()` is best-effort; exceptions never crash the server.

---

## 3. GUID Scheme

```csharp
public const ulong GuidPrefix = 0x99aabbccddee0000;
guid = GuidPrefix + zoneId   // zoneId fits in low 16 bits
```

* `CharacterGuid` encodes the zone: `ZoneId = CharacterGuid & 0xffff`.
* `CharacterStore.Get(guid)` first tries exact match, then falls back to `GuidPrefix + (guid & 0xffff)` because GameServer's session packet overwrites the low byte of the guid. Always resolve via zone when possible.

Seeded zones (`CharacterStore.SeedZones`):

| Name | ZoneId | GUID (hex) |
|------|--------|------------|
| M22 Homecoming | 1181 | 0x99aabbccddee049d |
| M20 Razor Edge | 833 | 0x99aabbccddee0341 |
| M19 Gatecrasher | 1171 | ... |
| M18 Vagrant Dawn | 1007 | |
| M17 SOS | 1151 | |
| M15 Agrievan | 803 | |
| M14 Icebreaker | 1008 | |
| M13 Accelerate | 1154 | |
| M12 Prison Break | 1155 | |
| M11 Consequence | 1114 | |
| M10 Off the Grid | 1106 | |
| M09 Taken | 1099 | |
| M08 Catch | 1134 | |
| M07 Trespass | 1101 | |
| M06 Safehouse | 1113 | |
| M05 No Exit | 1117 | |
| M04 Razorwind | 1102 | |
| M03 Crash Down | 1003 | |
| M02 Bathsheba | 1104 | |
| M01 Shadow | 1100 | |
| OP3 ARES Team | 1089 | |
| OP2 High Tide | 1093 | |
| OP1 Miru | 1069 | |
| TDM Refinery | 1147 | |
| Omnidyne-M Stadium | 844 | |
| Holdout Jericho | 1163 | |
| R1 Defense of Dredge | 1173 | |
| Epicenter Melding Tornado | 805 | |
| Abyss Melding Tornado | 865 | |
| Cinerarium | 868 | |
| Danger Room | 1162 | |
| Baneclaw Lair | 1051 | |
| Battlelab | 1125 | |
| Nothing | 12 | 0x99aabbccddee000c |
| Diamond Head | 162 | |
| Sertao | 1030 | |
| New Eden | 448 | |

You can add your own entries with any guid, but keeping the prefix+zone pattern makes `UpdateSessionData`/`UpdateCurrentBattleframe` work automatically.

---

## 4. JSON Structure

File is `List<CharacterRecord>` indented JSON. Minimal valid entry:

```json
[
  {
    "CharacterGuid": 11068046444225741260,
    "Name": "M01 Shadow",
    "SortOrder": 20,
    "Gender": 1,
    "Race": 0,
    "TitleId": 0,
    "CurrentBattleframeSDBId": 76334,
    "CurrentLevel": 10,
    "MaxFrameLevel": 10,
    "ArmyTag": "ARMY",
    "ArmyGuid": 1,
    "ArmyIsOfficer": true,
    "LastZoneId": 1100,
    "LastOutpostId": 0,
    "TimePlayed": 0,
    "CreatedAt": "2017-01-03T23:41:26Z",
    "LastSeenAt": "2025-09-03T00:00:00Z",
    "Visuals": {
      "Head": 10026,
      "Eyes": 10001,
      "VoiceSet": 1033,
      "Glider": 0,
      "Vehicle": 0,
      "Hair": 10113,
      "FacialHair": 0,
      "SkinColorId": 118969,
      "EyeColorId": 118980,
      "LipColorId": 1,
      "HairColorId": 77193,
      "FacialHairColorId": 77193,
      "SkinColor": 4294930822,
      "EyeColor": 1633685600,
      "LipColor": 4294903873,
      "HairColor": 1917780001,
      "FacialHairColor": 1917780001,
      "HeadAccessories": [10117],
      "HeadAccessoryColor": 1211031763,
      "Ornaments": [],
      "WarpaintId": 143225,
      "Warpaint": [4216738474,0,4216717312,418250752,1525350400,4162844703,4162844703]
    }
  }
]
```

### `CharacterRecord` fields

| Field | Type | Description |
|-------|------|-------------|
| `CharacterGuid` | ulong | Unique id, encodes ZoneId in low 16 bits. `0x99aabbccddee0000 + zoneId` |
| `Name` | string | Shown in selection screen |
| `SortOrder` | int | Lower = earlier in list |
| `ZoneId` | derived | `CharacterGuid & 0xffff`, not stored, read-only |
| `Gender` | uint | 0=Male, 1=Female |
| `Race` | uint | 0=Human (only used) |
| `TitleId` | ushort | Title SDB id, 0=none |
| `CurrentBattleframeSDBId` | uint | Chassis SDB id (see §5) |
| `CurrentLevel` | int | Level shown in selection |
| `MaxFrameLevel` | int | Max frame level shown |
| `ArmyTag` | string | e.g. `ARMY` |
| `ArmyGuid` | ulong | Army id |
| `ArmyIsOfficer` | bool | Officer flag |
| `LastZoneId` | uint | Updated on logout/teleport via gRPC |
| `LastOutpostId` | uint | Last outpost id |
| `TimePlayed` | uint | Seconds played |
| `CreatedAt` | DateTime | Creation date |
| `LastSeenAt` | DateTime | Last session end |
| `Visuals` | object | Appearance (§4.1) |

### 4.1 `Visuals` (`CharacterVisualsRecord`)

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `Head` | uint | 10026 | Head mesh SDB |
| `Eyes` | uint | 10001 | Eyes SDB |
| `VoiceSet` | uint | 1033 | Voice set |
| `Glider` | uint | 0 | Glider item, 0=none |
| `Vehicle` | uint | 0 | Vehicle item |
| `Hair` | uint | 10113 | Hair mesh |
| `FacialHair` | uint | 0 | Facial hair |
| `SkinColorId` | uint | 118969 | Color palette item |
| `EyeColorId` | uint | 118980 | |
| `LipColorId` | uint | 1 | |
| `HairColorId` | uint | 77193 | |
| `FacialHairColorId` | uint | 77193 | |
| `SkinColor` | uint | 4294930822 | ARGB uint |
| `EyeColor` | uint | 1633685600 | |
| `LipColor` | uint | 4294903873 | |
| `HairColor` | uint | 1917780001 | |
| `FacialHairColor` | uint | 1917780001 | |
| `HeadAccessories` | uint[] | [10117] | Helmets, etc |
| `HeadAccessoryColor` | uint | 1211031763 | Tint |
| `Ornaments` | uint[] | [] | Ornaments |
| `WarpaintId` | int | 143225 | Warpaint pattern |
| `Warpaint` | uint[] | 7 values | Warpaint colors |

Defaults come from `Lib/Shared.Common/DefaultCharacterTemplate.cs`.

---

## 5. Battleframes — How to Control

### 5.1 What is `CurrentBattleframeSDBId`?

It's the **chassis** SDB id the player is currently wearing. GameServer uses it to pick a loadout:

```csharp
// NetworkPlayer.cs
loadoutId = Inventory.GetLoadoutIdForChassis(remoteData.CharacterInfo.CurrentBattleframeSDBId);
```

If no loadout exists for that chassis, it falls back to `DefaultCharacterTemplate.FrameSdbId = 76334 (Raptor)`.

Switching in-game triggers:

```csharp
// BaseController.cs SelectLoadout
GRPCService.SaveCurrentBattleframeAsync(characterId, zoneId, chassisId)
→ Command.SaveCurrentBattleframe
→ CharacterStore.UpdateCurrentBattleframe
→ Save() → characters.json
```

So your choice **survives relogin** and appears in selection screen.

### 5.2 Known Battleframe SDB IDs

From `HardcodedCharacterData.TempCharCreateLoadouts` and SDB:

| Archetype | Frame Name | SDB Id | Notes |
|-----------|------------|--------|-------|
| Accord | Dreadnaught | 75772 | |
| Accord | Assault | 76164 | |
| Accord | Biotech | 75774 | |
| Accord | Engineer | 75775 | |
| Accord | Recon | 75773 | |
| Advanced | Firecat | 76133 | |
| Advanced | Tigerclaw | 76132 | |
| Advanced | Electron | 76337 | |
| Advanced | Bastion | 76338 | |
| Advanced | Mammoth | 76331 | Tanky, default inventory heavy |
| Advanced | Rhino | 76332 | |
| Advanced | Dragonfly | 76335 | |
| Advanced | Recluse | 76336 | |
| Advanced | Nighthawk | 76333 | |
| Advanced | **Raptor** | **76334** | **Default** |
| Advanced2 | Graviton | 82359 | |
| Advanced2 | Arsenal | 82360 | |
| Advanced2 | Archangel | 82394 | |
| Social | Beach Party | 124356 | |
| Social | BattleLab Trainee | 77733 | |

All ids exist in `dbitems::Battleframe` SDB table (`Battleframe.Id`). You can use any that exists in your `clientdb.sd2`.

### 5.3 Editing battleframe per character

1. Stop `WebHostManager` (to avoid overwrite race).
2. Open `characters.json`.
3. Find character by `Name` or `LastZoneId`.
4. Change `CurrentBattleframeSDBId` to desired id, e.g. `76331` for Mammoth.
5. Save file.
6. Restart `WebHostManager` and `GameServer`. Character will now spawn as that frame.

Example — make New Eden start as Mammoth:

```json
{
  "CharacterGuid": 11068046444225741608,
  "Name": "New Eden",
  "SortOrder": 37,
  "CurrentBattleframeSDBId": 76331,
  ...
}
```

### 5.4 Adding a new character

Append to array, ensure unique guid and sort order:

```json
{
  "CharacterGuid": 11068046444225741700,
  "Name": "My Custom - Mammoth Test",
  "SortOrder": 100,
  "Gender": 0,
  "Race": 0,
  "TitleId": 0,
  "CurrentBattleframeSDBId": 76331,
  "CurrentLevel": 45,
  "MaxFrameLevel": 45,
  "ArmyTag": "TEST",
  "ArmyGuid": 2,
  "ArmyIsOfficer": true,
  "LastZoneId": 448,
  "LastOutpostId": 0,
  "TimePlayed": 0,
  "CreatedAt": "2025-01-01T00:00:00Z",
  "LastSeenAt": "2025-01-01T00:00:00Z",
  "Visuals": {
    "Head": 10026,
    "Eyes": 10001,
    "VoiceSet": 1033,
    "Glider": 0,
    "Vehicle": 0,
    "Hair": 10113,
    "FacialHair": 0,
    "SkinColorId": 118969,
    "EyeColorId": 118980,
    "LipColorId": 1,
    "HairColorId": 77193,
    "FacialHairColorId": 77193,
    "SkinColor": 4294930822,
    "EyeColor": 1633685600,
    "LipColor": 4294903873,
    "HairColor": 1917780001,
    "FacialHairColor": 1917780001,
    "HeadAccessories": [10117],
    "HeadAccessoryColor": 1211031763,
    "Ornaments": [],
    "WarpaintId": 143225,
    "Warpaint": [4216738474,0,4216717312,418250752,1525350400,4162844703,4162844703]
  }
}
```

If `LastZoneId` is set, the character will load into that zone (unless you override guid encoding). For a clean custom guid not tied to zone, you can pick any > `0x99aabbccddee0000`, but then session save will create a new mapping via `Get()` fallback — recommended to keep zone encoding for predictability.

### 5.5 Changing global default

Edit `Lib/Shared.Common/DefaultCharacterTemplate.cs`:

```csharp
public const uint Gender = 1; // 0 male, 1 female
public const uint FrameSdbId = 76334; // Raptor -> change to 76331 for Mammoth default
public const int CurrentLevel = 10;
```

This affects:

* New seeded characters when `characters.json` is deleted.
* `HardcodedCharacterData.FallbackData` (used when gRPC fails).
* Visual defaults if a record omits visuals.

Rebuild after change.

---

## 6. End-to-End Flow

```
[Client] → Login → ClientApi /api/v2/characters → CharactersRepository
                                   ↓
                           CharacterStore.GetAll() → characters.json

[Client picks character Guid=0x99aabb...01c0 (448=New Eden)]
[Client] → Enter World → MatrixServer → GameServer
GameServer: GRPCService.GetCharacterAndBattleframeVisualsAsync(448)
         → WebHostManager:5201 gRPC → CharacterStore.Get(448) → returns record
         → CharacterEntity.LoadRemote()
         → Inventory.GetLoadoutIdForChassis(CurrentBattleframeSDBId)
         → Spawn

[Player switches loadout in Battleframe Station]
Client → SelectLoadout packet → BaseController.SelectLoadout
→ GRPCService.SaveCurrentBattleframeAsync → Stream Command
→ WebHostManager GameServerApiService.Stream:
   CharacterStore.UpdateCurrentBattleframe(zone, battleframeId)
   → Save() → characters.json updated
   → Next GetAll() in selection screen shows new frame

[Player logs out]
→ SaveGameSessionData → UpdateSessionData → LastZoneId/Outpost/TimePlayed saved
```

Proto definitions in `UdpHosts/GameServer/GRPC/GameServerAPI.proto`.

---

## 7. Configuration Reference

### WebHostManager

`config/appsettings.json`:

```json
{
  "Firefall": {
    "GameServerApi": {
      "Port": 5201,
      "CharacterStorePath": ""
    }
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `Port` | 5201 | h2c HTTP/2 gRPC port. Must match GameServer's `GrpcChannelAddress` |
| `CharacterStorePath` | "" (auto) | Path to `characters.json`. Empty = next to binary |

### GameServer

`GameServer.config.json` or `App.config`:

```xml
<appSettings>
  <add key="GrpcChannelAddress" value="http://localhost:5201" />
</appSettings>
```

Code default in `GameServerSettings.cs`:

```csharp
public string GrpcChannelAddress { get; set; } = "http://localhost:5201";
```

If `WebHostManager` not reachable:

* GameServer logs warning
* Spawns `FallbackData` (female Raptor) instead of selected character

---

## 8. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| All characters show female Raptor but you picked something else | `WebHostManager` not running, gRPC fails | Start `WebHostManager` first, check port 5201 not blocked |
| `characters.json` resets on every start | File corrupt or path not writable | Delete corrupted file, let it reseed; check folder permissions; ensure `CharacterStorePath` writable |
| Selection screen shows old battleframe after switching in-game | GameServer → WebHostManager stream down | Check GameServer logs for "Dropping GRPC command"; restart both; ensure `GrpcChannelAddress` matches |
| New entry doesn't appear | Invalid JSON | Validate JSON (array, no trailing commas); check `SortOrder` duplicates not filtered but sorting will still show |
| Character loads into wrong zone | `CharacterGuid` low 16 bits ≠ `LastZoneId` | Make guid = `0x99aabbccddee0000 + LastZoneId` OR set `LastZoneId` to desired zone; `ZoneId` property is derived from guid, but spawn uses `LastZoneId`/`LastOutpostId` from session data |
| Battleframe not applying (still Raptor) | No loadout for that chassis in inventory | Add chassis to inventory via `HardcodedCharacterData.FallbackInventoryItems` or ensure `SDBUtils.GetDefaultLoadoutSlots` returns data; Mammoth/Raptor have full loadouts, others may need custom loadout generation |

### Logs to watch

* WebHostManager: `Serving character {Name} ({Guid}) with battleframe {Battleframe}`
* WebHostManager: `Saved battleframe {Battleframe} for {CharacterId}`
* WebHostManager: `Saved session for {CharacterId}: zone {ZoneId} outpost {OutpostId}`
* GameServer: `No loadout for battleframe {Battleframe}, falling back to {Fallback}`

---

## 9. Quick Recipes

### Make every seeded character start as Mammoth

Stop servers, run:

```powershell
(Get-Content characters.json) -replace '"CurrentBattleframeSDBId": \d+', '"CurrentBattleframeSDBId": 76331' | Set-Content characters.json
```

Restart.

### Create a dev character that always spawns at Sertao as Assault

```json
{
  "CharacterGuid": 11068046444225742590,
  "Name": "DEV Sertao Assault",
  "SortOrder": 0,
  "Gender": 0,
  "Race": 0,
  "CurrentBattleframeSDBId": 76164,
  "CurrentLevel": 45,
  "MaxFrameLevel": 45,
  "LastZoneId": 1030,
  ...
}
```

`1030 = Sertao`, `76164 = Assault`. Guid `0x99aabbccddee0000 + 1030 = 0x99aabbccddee0406 = 110680464442257428...` calculate via python: `hex(0x99aabbccddee0000+1030)`.

### Wipe and reseed

Delete `characters.json`, restart `WebHostManager`. 38 entries regenerated from `SeedZones`.

---

## 10. Developer Notes

* `CharacterStore` is static, thread-safe via `ConcurrentDictionary` + `SaveLock`.
* `GetAll()` orders by `SortOrder` — selection screen order.
* `DefaultCharacterTemplate` is single source of truth for visuals, gender, default frame.
* `CharacterRecord.ZoneId` is computed, not serialized (getter only). To change zone a character loads into, set both `CharacterGuid` low bits AND `LastZoneId`.
* Future: could replace file store with DB, but current design intentionally has zero external dependencies, matching rest of PIN.

---

## 11. See Also

* `Docs/README.md` — architecture overview
* `Docs/SPAWNING_AND_COMBAT.md` — spawning & combat flow
* `UdpHosts/GameServer/GRPC/GameServerAPI.proto` — gRPC contract
* `README.md` → Character persistence section
* `CHANGELOG.md` → Unreleased: Persist characters to `characters.json`
