# Plan A: Freeform Cell-Based Footprints Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace chunk-level building placement with unified cell-based placement driven by footprint masks (any shape, free cell position, rotating footprints, per-cell preview validity), plus bulldozer tree removal.

**Architecture:** Per spec `docs/superpowers/specs/2026-06-12-freeform-buildings-and-path-textures-design.md` Part A. New `FootprintMask` value type in Core; `PlacementService` rewritten around per-cell occupancy with `PlacedObject.OccupiedCells`; visual mask editor as a PropertyDrawer; save format bumped to Version 2 (old saves rejected); BulldozeTool gains building-by-footprint and tree removal.

**Tech Stack:** Unity 6 / URP 17, VContainer, R3, NUnit EditMode (`WheatFarm.Tests.EditMode` exists, 23 tests green). Project root `D:\UnityProjects\wheat-farm`, branch from `feature/per-plant-meshes`. **Unity Editor is OPEN — verify via Unity MCP after every task:** refresh → `is_compiling == false` → `read_console(types=["error"])` empty (ignore the standing "An error occurred while resolving packages:" line) → `run_tests(mode="EditMode")` green (tests cannot run in Play Mode) → commit **explicit paths only** after checking `git diff --cached --name-status` (the working tree usually carries unrelated owner edits — never `git add -A`/`.`/directories).

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/Core/Data/FootprintMask.cs` | Create | Mask parse, 4 pre-rotations, dilation, rotated rasterization |
| `Assets/Scripts/Tests/EditMode/FootprintMaskTests.cs` | Create | Mask unit tests |
| `Assets/Scripts/Core/Data/PlaceableData.cs` | Modify | + FootprintRows, PaddingCells; − PlacementLevel |
| `Assets/Scripts/Editor/FootprintMaskDrawer.cs` | Create | Visual grid editor (PropertyDrawer over FootprintRows) |
| `Assets/Scripts/Features/Buildings/PlacementService.cs` | Rewrite | Cell-based placement, EvaluateFootprint, TryGetAt, OccupiedCells |
| `Assets/Scripts/Tests/EditMode/PlacementServiceTests.cs` | Create | Placement/validity/padding/parity tests |
| `Assets/Scripts/Player/Tools/PlacementTool.cs` | Modify | Unified snap, RotationSteps, per-cell footprint preview |
| `Assets/Scripts/Player/Preview/BrushPreviewService.cs` | Modify | RenderFootprintCells (per-cell green/red) |
| `Assets/Scripts/Player/Tools/BulldozeTool.cs` | Modify | TryGetAt buildings + tree removal + refund |
| `Assets/Scripts/Player/FarmInteractionController.cs` | Modify | Bulldoze hover hook for tree highlight |
| `Assets/Scripts/Features/Farming/TreePlacementService.cs` | Modify | Expose trunk-cell hit test (small helper) |
| `Assets/Scripts/Infrastructure/Save/FarmSaveData.cs` + `FarmSaveManager.cs` | Modify | Version 2, RotationSteps, slot matching by cell |
| `Assets/Settings/Placeables/Placeable_*.asset` (13 шт.) | Modify | GridSize chunks→cells, masks roughed from prefab bounds |
| `Assets/Scripts/Editor/PlaceableFootprintMigration.cs` | Create | One-shot menu item to rough masks from renderer bounds |

Key existing facts (verified): `ChunkSystem.WorldToCell/CellToWorld/CellWorldSize` (chunk 4m / 8 cells → cell 0.5m); `ITreePlacementService.Remove(PlacedTree)` and `PlacedTrees` are ALREADY public (`PlacedTree { PlantData Data; Vector3 WorldPosition; Vector2Int CenterChunk; ... }`, trunk cells via private `GetTrunkCells(Data, WorldPosition)`); `PlantData.SeedCost` exists; `FarmSaveData.Version = 1` exists but is never read on load; `PlacedObjectSaveData { PlaceableId, ChunkCoordX/Y, CellX/Y, RotationY, Level }`; `ProductionSlotSaveData` matches by PlaceableId+ChunkCoord (must gain CellX/CellY); spec decisions: cell snap, PaddingCells default 0, break saves, refund = `SeedCost / 2`.

---

### Task 1: FootprintMask (TDD)

**Files:** Create `Assets/Scripts/Core/Data/FootprintMask.cs`; Test `Assets/Scripts/Tests/EditMode/FootprintMaskTests.cs`.

- [ ] **1.1 Failing tests first.** Cover:
  - `Parse(["XX.","XXX"])` → Width 3, Height 2, correct `Cells(0)` set (origin = row 0 col 0 = (0,0); rows go +y).
  - Empty/null rows + `GridSize(2,3)` fallback → full 2×3 rectangle.
  - Ragged rows (`["XX","X"]`) → does not throw, logs error, falls back to GridSize.
  - `Cells(1)` (90° CW) of `["XX.","XXX"]` equals manually rotated set; `Cells(4)` == `Cells(0)`.
  - `Dilate(1)` of a single cell → 3×3 ring minus the cell itself (returns ONLY the ring cells).
  - `RasterizeRotated(0f)` == `Cells(0)`; `RasterizeRotated(90f)` == `Cells(1)`; `RasterizeRotated(45f)` of 1×3 mask: superset of the axis projection, each returned cell actually overlapped (sanity: count between 3 and 9).
- [ ] **1.2 Verify red** (compile fail), **1.3 implement:**

```csharp
namespace WheatFarm.Core.Data
{
    /// <summary>
    /// Cell footprint of a placeable. Parsed from FootprintRows ('X' occupied, '.' free);
    /// empty rows fall back to a solid GridSize rectangle. Offsets are cell offsets from
    /// the anchor (top-left of the mask, +x right, +y down == world +z direction of rows).
    /// Step90 rotations are precomputed; arbitrary angles use conservative rasterization.
    /// </summary>
    public sealed class FootprintMask
    {
        public int Width { get; }   // of rotation 0
        public int Height { get; }
        // Cells(rotSteps): IReadOnlyList<Vector2Int>, rotSteps 0..3, rotation about mask center
        // Dilate(padding): ring cells around the rot-0 mask (recompute per rotation by caller via Cells)
        // RasterizeRotated(angleDeg): conservative — candidate cells from rotated AABB; a candidate
        //   is occupied if any of 5 sample points (4 corners inset 10% + center) of the candidate
        //   cell square, inverse-rotated into mask space about the mask center, lands inside an
        //   occupied mask cell square. angle multiple of 90 → delegate to Cells().
        public static FootprintMask Create(string[] rows, Vector2Int gridSizeFallback) { /* ... */ }
    }
}
```

  Rotation pivot is the **mask center** (`(Width-1)/2f, (Height-1)/2f`) per spec — rotating must not swing the ghost away from the cursor. Returned offsets after rotation are re-anchored so the rotated bounding box's top-left is (0,0) AND the caller can keep the cursor on the same world cell: expose `Vector2Int AnchorShift(int rotSteps)` (how much the center moved, in cells) — `PlacementService` subtracts it. Write exact rotation math in code with comments; tests pin it.
- [ ] **1.4 All green (23 + new), 1.5 commit** `feat: footprint mask with rotations and conservative rasterization`.

### Task 2: PlaceableData fields + visual mask editor

**Files:** Modify `Assets/Scripts/Core/Data/PlaceableData.cs`; Create `Assets/Scripts/Editor/FootprintMaskDrawer.cs`.

- [ ] **2.1** `PlaceableData`: add under Placement header:
```csharp
        [Tooltip("Маска футпринта: 'X' занято, '.' свободно. Пусто = прямоугольник GridSize (в КЛЕТКАХ).")]
        public string[] FootprintRows;
        [Tooltip("Зазор валидности вокруг маски, в клетках. Не помечает клетки занятыми.")]
        public int PaddingCells = 0;
```
  **Do NOT delete `PlacementLevel`/`Level` yet** — that happens in Task 3 with the service rewrite (keeps the project compiling between tasks). Add `[System.Obsolete("Removed in Task 3 of plan A")]` on the `Level` field now.
- [ ] **2.2** `FootprintMaskDrawer`: custom editor UI for `FootprintRows` (a `PropertyDrawer(typeof(PlaceableData))`-scoped custom inspector is acceptable instead — implementer's choice; requirement is: grid of toggle buttons sized Width×Height, click toggles cell, +/− row and column buttons, "Fill from GridSize" button; writes back as strings). Place in `Assets/Scripts/Editor` (asmdef `WheatFarm.Editor` exists).
- [ ] **2.3** Manual verification via Unity MCP: open `Assets/Settings/Placeables/Placeable_Mill.asset` selection in inspector is not screenshot-able reliably — instead verify by `execute_code`: set FootprintRows on a test instance, read back. Compile clean, tests still green.
- [ ] **2.4 Commit** `feat: footprint rows + padding on PlaceableData, visual mask editor`.

### Task 3: PlacementService rewrite (TDD)

**Files:** Rewrite `Assets/Scripts/Features/Buildings/PlacementService.cs`; Modify `PlaceableData.cs` (delete `PlacementLevel` + `Level` field); Test `Assets/Scripts/Tests/EditMode/PlacementServiceTests.cs`.

- [ ] **3.1 Failing tests** (construct `ChunkSystem(4f, 8)` + stub `IWalletService` always-true; PlacementService ctor takes them):
  - L-mask placement on clean field: `CanPlace` true; after `Place` exactly the mask cells are `Occupied`; `PlacedObject.OccupiedCells` matches.
  - One mask cell pre-occupied → `CanPlace` false; `EvaluateFootprint` marks exactly that cell `ok=false`, others true.
  - Mask crossing a chunk border (anchor at cell 7,4 of chunk (0,0), 2-wide mask) → cells land in both chunks (both unlocked).
  - Neighbor chunk locked → `CanPlace` false.
  - `PaddingCells=1`: building adjacent to an existing building → false (padding cell occupied), one cell gap → true; padding cells NOT marked occupied after placement; padding into locked/missing chunk → treated as free.
  - Parity: `EvaluateFootprint` all-ok ⇔ `CanPlace` (including padding).
  - `Remove` frees exactly `OccupiedCells`.
  - `TryGetAt(worldPos)` finds the object by any of its mask cells; misses outside.
  - Step90: `Place` with rotSteps=1 occupies rotated set.
- [ ] **3.2 Red, 3.3 implement.** API:
```csharp
    public interface IPlacementService
    {
        ObservableList<PlacedObject> PlacedObjects { get; }
        PlacedObject Place(PlaceableData data, Vector3 worldPos, int rotationSteps, float rotationY);
        bool CanPlace(PlaceableData data, Vector3 worldPos, int rotationSteps, float rotationY);
        /// <summary>Per-cell validity for preview; cells = mask cells (always) + blocking padding cells (only when blocked). All-ok ⇔ CanPlace.</summary>
        void EvaluateFootprint(PlaceableData data, Vector3 worldPos, int rotationSteps, float rotationY, List<(Vector3 worldPos, bool ok)> result);
        bool TryGetAt(Vector3 worldPos, out PlacedObject obj);
        bool Remove(PlacedObject obj);
        PlacedObject RestorePlace(PlaceableData data, Vector2Int chunkCoord, int cellX, int cellY, int rotationSteps, float rotationY, int level);
    }
```
  - Cell resolution: anchor = `WorldToCell(worldPos)`; each mask offset resolved through world math (`CellToWorld(anchor) + offset * CellWorldSize → WorldToCell`) so chunk borders are transparent.
  - Footprint source: `FootprintMask.Create(data.FootprintRows, data.GridSize)`, cached per PlaceableData (Dictionary).
  - Rotation: `RotationMode.Step90` → `Cells(rotationSteps)`; `RotationMode.Free5` → `RasterizeRotated(rotationY)`; `Fixed` → `Cells(0)`.
  - Mask cell valid: chunk exists && Unlocked && !Occupied && !HasPlant. Padding cell valid: !(exists && Unlocked && Occupied) — i.e. only an occupied unlocked cell blocks.
  - `PlacedObject`: + `int RotationSteps; List<(Vector2Int chunkCoord, int cellX, int cellY)> OccupiedCells;` `Place` fills it and sets `Occupied=true` on exactly those; `Remove`/`RestorePlace` symmetric. Spawn pos = world center of the occupied-cells bounding box, y=0. Delete: `_occupiedChunks`, all *ChunkLevel methods, `MarkChunkSubCellsOccupied`, `ChunkFootprintCenter`. Delete `PlacementLevel` enum + `PlaceableData.Level` field (upgrade level `PlacedObject.Level` STAYS).
  - `TryGetAt`: linear scan of `PlacedObjects` → `OccupiedCells.Contains(WorldToCell(worldPos))`.
- [ ] **3.4 Fix all compile fallout** of deleting `PlacementLevel` (`PlacementTool.SnapPosition/FootprintWorldSize`, `BulldozeTool` threshold branch, `FarmSaveManager`) — minimal mechanical edits to compile; their real rework comes in Tasks 4–6 (leave `// TODO(plan-A task N)` markers).
- [ ] **3.5 Green (all suites), 3.6 commit** `feat: cell-based placement with footprint masks, per-cell validity, TryGetAt`.

### Task 4: PlacementTool + per-cell footprint preview

**Files:** Modify `PlacementTool.cs`, `BrushPreviewService.cs`, `FarmInteractionController.cs` (only if signatures shift).

- [ ] **4.1** `BrushPreviewService`: replace `RenderFootprint(center,size,valid)` with
```csharp
        void RenderFootprintCells(List<(Vector3 worldPos, bool ok)> cells);
```
  — two instanced draws per frame (ok→green 0.35α, !ok→red 0.35α), reusing `_matrices`/`_mpb`/cell quad; add a second reusable list for the red set.
- [ ] **4.2** `PlacementTool`: `_pendingRotationSteps` (Step90) alongside `_pendingRotation` (Free5 degrees); `AdjustRotation` updates the right one by `RotationMode`. `UpdatePreview` (building mode): snap = anchor cell center (no chunk branch — delete it); call `_placementService.EvaluateFootprint(...)` into a reused list; ghost pose = occupied-bbox center (service exposes it via the eval call or a small struct — implementer picks, keep zero alloc); `_ghost.SetValid(allOk)`; `_brushPreview.RenderFootprintCells(list)`. `UseAtPosition` → `Place(data, pos, _pendingRotationSteps, _pendingRotation)`.
- [ ] **4.3** Play-mode sanity via MCP: select Mill — ghost snaps per-cell (0.5m), scroll rotates ghost + footprint together, cells over an occupied spot show individually red. Screenshot.
- [ ] **4.4 Commit** `feat: per-cell footprint preview, rotating footprints in placement tool`.

### Task 5: Save v2 + asset migration

**Files:** Modify `FarmSaveData.cs`, `FarmSaveManager.cs`; Create `Assets/Scripts/Editor/PlaceableFootprintMigration.cs`; Modify 13 `Placeable_*.asset`.

- [ ] **5.1** `FarmSaveData.Version` default → 2. `PlacedObjectSaveData` + `int RotationSteps`. `ProductionSlotSaveData` + `int CellX, CellY`; matching in `FarmSaveManager.RestoreFromData` becomes PlaceableId+ChunkCoord+Cell. Load path: `if (data.Version < 2) { Debug.LogWarning("[Save] Incompatible save version, starting fresh"); return false/skip; }` (old files deserialize with Version=1 via field initializer — works as the discriminator).
- [ ] **5.2** `PlaceableFootprintMigration` (menu `WheatFarm/Migrate Placeable Footprints`): for each `Placeable_*.asset` with Category != Path — compute prefab renderer bounds, `GridSize = ceil(bounds.size.xz / CellWorldSize(0.5f))` clamped to ≥1, clear FootprintRows (solid rectangle fallback), save assets. Run it via MCP `execute_menu_item`; log the resulting sizes per asset for the owner to eyeball.
- [ ] **5.3** Play-mode: fresh game starts (old save rejected with the warning), economy buildings auto-place (`EconomyBuildingsBootstrap`) still works on the new API. **Check `EconomyBuildingsBootstrap` + `ShopService`/UI call sites of `Place/CanPlace`** — signatures changed in Task 3; finish their adaptation here if Task 3 left TODOs.
- [ ] **5.4 Commit** `feat!: save format v2 — cell-anchored placements, slot matching by cell`.

### Task 6: Bulldoze — buildings by footprint + trees

**Files:** Modify `BulldozeTool.cs`, `TreePlacementService.cs`, `FarmInteractionController.cs`.

- [ ] **6.1** `ITreePlacementService` + small helper `bool TryGetTreeAt(Vector3 worldPos, out PlacedTree tree)` — reuses private `GetTrunkCells` (make a private hit-test over trunk cells of each tree; linear scan fine).
- [ ] **6.2** `BulldozeTool.TryRemovePlacedObject` → `if (_placementService.TryGetAt(pos, out var obj)) { Remove + existing refund path; return true; } if (_treePlacement.TryGetTreeAt(pos, out var tree)) { _treePlacement.Remove(tree); _wallet.Add(tree.Data.SeedCost / 2); return true; }` (inject `IWalletService`). Distance-threshold constants deleted.
- [ ] **6.3** Hover highlight: `BulldozeTool` gets `UpdateHover(Vector3 cursorPos)` — if hovering a building (`TryGetAt`) or tree (`TryGetTreeAt`), tint its renderers red via a shared `MaterialPropertyBlock` (`_BaseColor`→red-ish multiply; store previous block target to clear on hover change/unequip). `FarmInteractionController.HandlePreview` calls it when bulldoze is the current tool (mirror the placement branch). Clear on `OnUnequip`.
- [ ] **6.4** Play-mode: bulldoze a building by clicking its edge cell (not center), bulldoze a tree → it disappears, coins +SeedCost/2; hover tints red and untints. Screenshot.
- [ ] **6.5 Commit** `feat: bulldoze by footprint cells + tree removal with refund and hover highlight`.

### Task 7: Integration + checkpoint

- [ ] **7.1** Full EditMode suite green; play-mode regression: plant/water/harvest unaffected; paths still paint/repaint/bulldoze; preview system (ghost/x-ray/outline/brush cells) intact — the preview layer code paths were not touched except footprint rendering.
- [ ] **7.2** Owner checkpoint: mask editor UX on a real asset (e.g. make Mill an L-shape), rotation feel, padding default.
- [ ] **7.3** Commit any tuning.

## Risks

1. **Wide compile fallout** from deleting `PlacementLevel` (Task 3) — mitigated by TODO-markers + Task 5.3 sweep; grep `PlacementLevel|\.Level` across `Assets/Scripts` before declaring Task 3 done (careful: `PlacedObject.Level` upgrade level stays).
2. **Rotation re-anchoring math** (AnchorShift) is the fiddliest bit — pinned by Task 1 tests before anything consumes it.
3. **EconomyBuildingsBootstrap/auto-placement** assumes old chunk semantics — explicitly checked in 5.3.
4. Save-breaking is sanctioned; the only guard is the version warning.
