# Map Analysis for Real Server Implementation - Actionable Guide

This is the **actionable companion** to `MAP_FILES_FINDINGS.md`. It translates the raw map file breakdown into concrete tasks for the real server's world population and NPC routine systems.

**Source:** `/tmp/maps.zip` (3.2GB, 38 zones, 281 chunks, 533 worldMaps) uploaded via chunked live transfer (8MB chunks, 387 requests). Parsed with Python tools in `Tools/MapAnalysis/`.

## TL;DR for Implementers

- **Zone files** (`*.zone`) are NOT a spawn table, but they contain ground truth for *where* things can be: chunk grid, bounds, Melding wall, subzone regions, and **vehicle flight/drive paths** (0x20800) that are NOT NPC patrols.
- **Chunk files** (`*.gtchunk`) contain compressed Enwf collision (walkable surfaces) and **SubZoneGridLayer** (0x40102) which maps 64x64 cells per chunk to subzone IDs - this is the closest thing to spawn volumes.
- **No per-zone monster list** exists in maps. That lived in live server spawn groups and never shipped. Current system (habitat via outposts/deployables/Melding + level via nearest banded anchor) is the correct reconstruction.
- **Path layers are vehicle routes**, not NPC routes. Actions are `fly`, `land`, `liftoff`, `drive`, `signal(...)`, not `WanderPoint`. So PR #93's MissingRoute remains missing - don't invent patrols from path layers.
- **Most actionable improvements:** SubZoneGrid for habitat, ZoneBounds for early-out, Melding interpolation already done in PR #94, PropDoodad reverse engineering for work stations.

## 1. What We Actually Found in Maps

### 1.1 Zone File Inventory (38 zones)

| ZoneId | Name | ChunkRefs | Paths | Melding | EncounterNames |
|---|---|---|---|---|---|
| 448 | New Eden | 93 | 129 | 36 | many |
| 1030 | Sertao | 93 | 129 | 36 | many |
| 162 | Diamond Head | 35 | 105 | 7 | some |
| ... | 35 other mission/instance zones | 1-25 | 0-42 | 0-8 | VFX/lights |

- **ChunkRefs:** 93 for open-world zones (New Eden, Sertao) - matches Coral Forest's 93 chunks mentioned in WORLD_POPULATION.md (64 of which are client-only). Each ref is X,Y,ChunkRecordId mapping to `dbzonemetadata::ZoneChunkLinker` and `ChunkRecord`.
- **Paths:** 129 in New Eden/Sertao - but analysis of action bytes shows they are **vehicle/dropship routes**, not NPC wander routes. Top actions: `fly` (633), `land` (569), `signal("request landing")` (304), `liftoff` (248), `drive` (129). No `WanderPoint`, `StockShootAndFollowRoute`, `Arch_Follower` etc. found. So path layers cannot solve MissingRoute.
- **Melding:** 36 perimeters in open-world zones - each with ControlPoints 4-23, name like `crash_down_melding_set`, perimeters list `default`, `collapsed`, `perimeter 3`. Bitfield may encode visibility.
- **EncounterNames:** 313 unique in zones, 97 in chunks (20-file sample). Mostly VFX/lights (`Blackwater_SinkholeBoostSmoke`, `VFX_Pillar_amb_07`, `Chain2bInterior`), not spawn groups. So not useful for per-zone monster list.

### 1.2 Chunk File Inventory (281 files, 50 sampled)

- **LOD structure:** Each chunk has 1-5 LODs, each LOD has shared block + 1-16 subchunks. Shared and subchunk blocks compressed with `DATA` (zlib) or `DAT2` (LZMA, 5-byte props).
- **Decompressed layers per chunk:** Typically 1-3 layers: `0x40101` StaticGeometry (Enwf), `0x40102` SubZoneGrid, `0x40104` EncounterRegistry.
- **SubZoneGrid (0x40102):** Found in 30/30 sampled chunks. GridSize 64, IdsCount 1-4, GridCount 1-2, GridDataBytes 4096-8192. Example: `0_0299_1292.gtchunk: GridSize 64, Ids [10285,10618,10617], GridCount 1, 4096 bytes`. Each byte in GridData is index into SubZoneIds list, mapping 64x64 cells to subzone region ID. Subzone IDs like 10285, 10618 correspond to `ZoneSubZoneRegionLayer` regionIds (0x21700) which have bitmaps.
- **Enwf:** Static geometry with VertBlocks, IndiceBlocks, MatItems, MoppBlocks, HavokBinaryTagfile. Used by `NavigationGeometryExtractor` to build triangles, then `NavigationMesh`, then `WorldPopulationPlanner` accumulates face centroids into 32m cells.

### 1.3 WorldDir and WorldMap

- **.worldDir:** 581B-14KB, contains `WMAP` and paths like `\ToolsData\Environments\prod\BakedData\all_users\world\` and `*.worldMap` refs - likely index of worldMap chunks.
- **.worldMap (GTNO):** Magic `GTNO`, 533 files, 197MB, named `0_07_0000001074_opt.worldMap`. Likely terrain material mapping, not needed for population.

## 2. How Current Population Uses Maps (Correct)

From `PhysicsWorldPopulationTerrain.cs`, `ZoneLoader.cs`, `SdbWorldPopulationDataSource.cs`:

1. **ZoneLoader** loads `{zoneId}.zone` from `MapsPath`, extracts chunk refs via `ChunkOriginCalculator`:
   - Uses `ZoneChunkRangeLayer` (CubeFaceId, MinX, MaxX, MinY, MaxY) and `ChunkRefLayer` (X,Y,ChunkRecordId)
   - Origin: special-cased 448 (4,3.5) and 1030 (9.5,3), else (max-min)/2. Origin = (centerIndex - (max - x)) * 512
   - Chunk name `{CubeFaceId}_{X:D4}_{Y:D4}` -> `maps/chunks/{name}.gtchunk`
   - `ChunkProcessor.ProcessChunk` decompresses Enwf into BepuPhysics and collects `NavigationTriangles`

2. **WorldPopulationPlanner**:
   - `SurfaceCount` + `TryGetSurface(i)` = walkable face centroid
   - Accumulate into 32m cells: `CellIndexOf`, `MakeKey`, `CellDraft` Sum/Count -> Center
   - `GetChunkRecordId(pos)` via `ChunkByIndex` (chunk grid origin)
   - `IsChunkSpawnable` via `ZoneChunkLinker.clientonly` + `ChunkRecord.remove_in_production` - RefusedChunkCells reported

3. **Habitat**:
   - Outposts: pos+radius (150-550m) + LevelBandId -> Settlement
   - Deployables: 469 in Coral Forest -> Settlement, radius 0 uses `DeployableInfluenceRadius` 25m config
   - Melding ControlPoints: 16*4-23 -> Melding, radius 0 uses `MeldingInfluenceRadius` 120m + 60m interpolated edge anchors (PR #94)
   - Settlement beats Melding, level via nearest banded anchor

4. **Placement**:
   - `TryGetGroundSurface`: probe 1.5m up/3m down, |normal.Z|>=0.35, horizontal probes ankle/waist/shoulder, headroom, broad-phase
   - `SpawnOccupancyGrid`: hash planned bodies + 25m player clearance
   - Ground refusal -> after 8 rounds parked, room refusal -> retry 1s

This is **correct and data-backed**. No spawn table in maps or SDB, so habitat inference is the right approach.

## 3. What Maps Could Improve (Ranked)

### 3.1 High Value, Low Effort

#### ZoneBounds (0x21000) for Early-Out
- **What:** Min/Max Vec3 per zone - AABB.
- **Current:** Planner scans all navigation faces (up to few hundred thousand) and builds cells for whole zone, even if player is in corner.
- **Improvement:** If player outside bounds, don't activate cells. Also use bounds to limit `WorldPopulationCell` key scan to bounding box, not entire grid.
- **Code:** In `WorldPopulationService`, check `ZoneLoader.Bounds` (need to expose) before `Work(budget)`.
- **Effort:** 1-2 hours.

#### Melding Interpolation Already Done (PR #94) - Verify with Real Data
- **What:** PR #94 interpolates edge anchors every 60m along spline using `AiVectors.HorizontalDistance`.
- **Validation with maps:** We confirmed control points 4-23 per Melding, 16 Meldings in New Eden. 60m steps follow perimeter line instead of dotted circles.
- **Next:** Parse bitfield in MeldingPerimeterLayer to see if it encodes spline type (linear vs bezier). If bezier, interpolation should use curve, not straight line. But current 60m linear is already faithful to shipped knots + configured radius, no invented radius.
- **Effort:** 2-4 hours to parse bitfield and test.

### 3.2 High Value, Medium Effort

#### SubZoneGridLayer (0x40102) for Habitat/Level
- **What:** 64x64 grid per chunk mapping to subzone IDs. Subzone IDs map to `ZoneSubZoneRegionLayer` (0x21700) which has bitmap.
- **Current:** Habitat via nearest anchor distance, level via nearest banded anchor.
- **Improvement:** Use subzone ID at cell center to determine habitat or level band more accurately. For example, if cell is inside subzone that is known settlement (via region name or via overlap with outpost), classify as Settlement even if far from anchor. Or use subzone grid to define spawn volumes (e.g., only spawn inside certain subzones).
- **Implementation:**
  ```csharp
  // New interface
  interface ISubZoneGridDataSource {
      uint GetSubZoneId(Vector3 pos);
      IReadOnlyList<uint> GetSubZoneIdsForChunk(uint chunkRecordId);
  }
  // Implementation reads chunk's 0x40102 layers, builds dict chunkIndex -> grid
  // Grid lookup: world pos -> chunk index -> grid cell -> subzoneId
  ```
- **Effort:** 1-2 days, need to parse all chunks at zone load (similar to navigation triangles), build grid lookup.

#### PropDoodad (0x21300/0x21400) and PropEnv (0x50001) for Work Stations
- **What:** Currently UnknownWorldLayer with RawData. Likely contains deployable placements (chairs, bars, repair consoles) that are missing from `CustomData/deployable.json` (109 nonempty behaviours, 0 placements).
- **Current:** Work/rest activities tested but world has no chairs/bars. Operator can test via `\spawn deployable 116` + `\spawn monster 2939` but not original placements.
- **Improvement:** Reverse engineer doodad format. Likely structure: deployableId uint32 + position Vec3 + orientation Vec4 + maybe scale. Try to parse RawData as series of such records, compare with known deployable positions (469 in Coral Forest) - are those from SDB or from zone? `GetZoneDeployables` loads from JSON, not zone, so zone may have more.
- **Implementation:** Write `PropDoodadDump` tool that tries to parse RawData as: count + entries (id, pos, orient). Then populate JSON.
- **Effort:** 2-3 days reverse engineering, 1 day to populate JSON.

### 3.3 Medium Value, High Effort

#### EncounterNameRegistry for Spawn Groups (Probably Not Useful)
- **What:** 313 unique zone names, 97 chunk names, mostly VFX/lights, not spawn groups.
- **Finding:** Encounter names like `Blackwater_SinkholeBoostSmoke`, `VFX_Pillar_amb_07` are not monster spawn groups. So unlikely to reconstruct per-zone monster list.
- **Action:** Still worth dumping all encounter names and searching SDB 575 tables for foreign keys, but expectation low. Current global roster (2853 rows) is correct.
- **Effort:** 1 day to dump + search SDB.

#### ZonePathLayer for Authored Routes (Not NPC Routes)
- **What:** 337 unique CceIds, 326 unique actions, actions are `fly`, `land`, `drive`, `signal(...)`, not `WanderPoint`.
- **Finding:** Path layers are vehicle/dropship routes, not NPC patrols. So they cannot solve MissingRoute for `StockShootAndFollowRoute`, `WanderPoint`, etc.
- **Action:** Don't use path layers for NPC routines. Instead, document that path layers are vehicle routes and keep MissingRoute as is (don't invent patrols).
- **Effort:** 0 - just document.

### 3.4 Low Value / Requires Original Server Data

- **CAIS tree/instance defaults:** Need original tree definitions (757/7/4 non-zero refs). Not in maps, maybe in assetdb or Lua.
- **Non-ground locomotion:** `groundOffset=1.6,inSpawnVolume=true,climber=true` 1249 rows need spawn volumes + climbing/flying navmesh. Spawn volumes may be in SubZoneGrid or separate encounter volume data - not found.
- **Body radius/height -1 sentinel:** 3103 rows have -1, using 0.7/1.8 defaults (AI agent numbers). Could be derived from chassis/posetype if parse assetdb.
- **Respawn_flags, swarm formation, Deployable/MeldingInfluenceRadius:** Guessed 25m/120m, documented as compatibility policy.

## 4. Concrete Implementation Tasks

### Task 1: Add ZoneBounds to ZoneLoader and Use for Early-Out
```csharp
// In ZoneLoader.cs
public Vector3? ZoneMin { get; private set; }
public Vector3? ZoneMax { get; private set; }
// In LoadZone, after reading ZoneFile, find ZoneBoundsLayer child of root
// Store Min/Max
// In WorldPopulationService, before activating cells, check if player pos inside bounds
```

### Task 2: SubZoneGrid Data Source
```csharp
// New file: SubZoneGridDataSource.cs
public class SubZoneGridDataSource {
    // Load all chunk SubZoneGridLayers at zone load
    // Build dict (chunkX,chunkY) -> (GridSize, SubZoneIds[], GridData)
    // Method GetSubZoneId(Vector3 pos):
    //   chunkIndex = ChunkIndex(pos) (same as PhysicsWorldPopulationTerrain)
    //   grid = grids[chunkIndex]
    //   localX = (pos.X - chunkOrigin.X) / ChunkSize * GridSize
    //   localY = (pos.Y - chunkOrigin.Y) / ChunkSize * GridSize
    //   cellIdx = localY*GridSize + localX
    //   idIdx = GridData[cellIdx] (byte)
    //   return SubZoneIds[idIdx]
}
// Then in WorldPopulationPlanner.Classify, try subzone first, else anchor distance
```

### Task 3: PropDoodad Reverse Engineering Tool
```python
# Tools/MapAnalysis/prop_doodad_dump.py
# For each zone file, for each 0x21300/0x21400 layer, try to parse RawData:
# - First uint32 = count?
# - Then each entry: deployableId uint32 + Vec3 pos + Vec4 orient?
# - Dump to JSON and compare with existing deployable.json positions
```

### Task 4: Document Path Layers as Vehicle Routes
- Update `Docs/NPC_ROUTINES.md` missing-content list to clarify that ZonePathLayer (0x20800) is vehicle/dropship routes, not NPC routes, so MissingRoute remains.
- Update `Docs/MAP_FILES_FINDINGS.md` with action analysis (fly, land, etc.)

## 5. Validation Plan

- **ZoneBounds:** Test with `\population status` - should show same cell counts but less work when player outside bounds.
- **SubZoneGrid:** Compare habitat classification before/after - should be more accurate near subzone boundaries. Log `subzoneId` in `WorldPopulationCell`.
- **PropDoodad:** After populating deployable.json, test work/rest via `\spawn monster 2939` near placed deployable 116 - should walk to it and play emote without manual `\spawn deployable`.
- **Melding:** Visual check - Melding wall should be continuous line, not dotted circles (already fixed in PR #94).

## 6. What NOT to Do

- **Don't invent patrols** from path layers - they are vehicle routes, not NPC routes. Keep MissingRoute as MissingRoute.
- **Don't invent spawn table** - no per-zone monster list in maps or SDB. Keep global roster + habitat classification.
- **Don't guess radii** beyond config - Deployable 25m, Melding 120m are already documented as compatibility policy. Don't change without data.
- **Don't parse WorldMap (GTNO)** unless needed - 197MB of terrain material, not needed for population.

## 7. Files to Create/Modify

- `Tools/MapAnalysis/analyze_maps.py` (already created) - parses zone/chunk, outputs `Docs/MAP_FILES_FINDINGS.md` and `MAP_FILES_SUMMARY.json`
- `Tools/MapAnalysis/zone_path_dump.py` (new) - dumps path layers to JSON for inspection
- `Tools/MapAnalysis/subzone_grid_dump.py` (new) - dumps subzone grids
- `Tools/MapAnalysis/encounter_name_dump.py` (new) - dumps encounter names
- `Tools/MapAnalysis/prop_doodad_dump.py` (new) - attempts to parse prop doodad
- `Docs/MAP_FILES_FINDINGS.md` (created, 89KB, 1463 lines) - raw findings
- `Docs/MAP_ANALYSIS_FOR_SERVER.md` (this file) - actionable guide
- `Lib/Shared.Collision/ZoneLoading/ZoneLoader.cs` - add Bounds
- `UdpHosts/GameServer/Systems/Spawning/Population/` - add SubZoneGridDataSource, use for classification
- `Docs/WORLD_POPULATION.md` - update with subzone grid and path layer clarification
- `Docs/NPC_ROUTINES.md` - clarify path layers are vehicle routes

## 8. Conclusion

Maps provide ground truth for *where* NPCs can stand (collision, chunk grid, bounds, subzone grids, Melding wall) but not *which* NPCs belong where (no spawn table). Current world population system is faithful to data that exists. Most actionable improvements from maps are:

1. **ZoneBounds early-out** (1-2h)
2. **SubZoneGrid for habitat** (1-2d)
3. **PropDoodad for work stations** (2-3d reverse engineering)
4. **Document path layers as vehicle routes** (0 effort, just doc)

Path layers are NOT NPC patrols - they are `fly`, `land`, `drive` vehicle routes. So MissingRoute remains missing, which is correct - don't invent data.

The 3.2GB maps.zip contains 38 zones, 281 chunks, 533 worldMaps, and is now parsed. The findings are documented in two docs: `MAP_FILES_FINDINGS.md` (raw) and `MAP_ANALYSIS_FOR_SERVER.md` (actionable, this file).

---
*Generated after successful chunked upload of maps.zip via live transfer server (8MB chunks, 387 requests, bypassing Google Drive SSL block).*
