# Universal Placement System — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Unify all placement logic (buildings, decor, paths, trees) into one data-driven system with category tab UI.

**Architecture:** Single `PlaceableData` SO + `PlacementService` handles buildings/decor/paths. `PlantData` stays for crops/trees/bushes. `PlacementTool` replaces PlanterTool + BuildTool. `CatalogTabBar` replaces key-based tool switching.

**Tech Stack:** Unity 6, VContainer, R3, GPU Instanced Indirect rendering, UGUI (TMP)

**Design Doc:** `docs/plans/2026-03-16-universal-placement-design.md`

---

## Phase A: Data Foundation + PlacementService

### Task A1: Create PlaceableData and PlaceableDatabase ScriptableObjects

**Files:**
- Create: `Assets/Scripts/Core/Data/PlaceableData.cs`
- Create: `Assets/Scripts/Core/Data/PlaceableDatabase.cs`

**Steps:**

1. Create `PlaceableData.cs`:
```csharp
using UnityEngine;

namespace WheatFarm.Core.Data
{
    public enum PlaceableCategory { Building, Decor, Path }
    public enum PlacementLevel { Cell, Chunk }
    public enum RotationMode { Fixed, Step90, Free5 }

    [CreateAssetMenu(menuName = "WheatFarm/PlaceableData")]
    public class PlaceableData : ScriptableObject
    {
        [Header("Identity")]
        public string PlaceableId;
        public string DisplayName;
        public PlaceableCategory Category;
        public Sprite Icon;

        [Header("Placement")]
        public Vector2Int GridSize = Vector2Int.one;
        public bool BlocksPlanting = true;
        public PlacementLevel Level;
        public RotationMode Rotation = RotationMode.Fixed;

        [Header("Visual")]
        public GameObject Prefab;

        [Header("Path Properties")]
        public int PathSubtype;
        public Color PathColor = Color.white;

        [Header("Economy")]
        public int Cost;
        public bool UnlockedByDefault = true;

        [Header("Interaction")]
        public bool Interactable;
        public RecipeData[] Recipes;
        public int MaxLevel = 1;
    }
}
```

2. Create `PlaceableDatabase.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace WheatFarm.Core.Data
{
    [CreateAssetMenu(menuName = "WheatFarm/PlaceableDatabase")]
    public class PlaceableDatabase : ScriptableObject
    {
        public PlaceableData[] Items;

        private Dictionary<string, PlaceableData> _cache;

        public PlaceableData GetById(string id)
        {
            if (_cache == null)
            {
                _cache = new Dictionary<string, PlaceableData>();
                foreach (var item in Items)
                    if (item != null) _cache[item.PlaceableId] = item;
            }
            return _cache.GetValueOrDefault(id);
        }

        public List<PlaceableData> GetByCategory(PlaceableCategory category)
        {
            var result = new List<PlaceableData>();
            foreach (var item in Items)
                if (item != null && item.Category == category)
                    result.Add(item);
            return result;
        }
    }
}
```

3. Verify: Unity compiles with 0 errors.
4. Commit: `feat(A1): PlaceableData + PlaceableDatabase ScriptableObjects`

---

### Task A2: Create PlacementService

**Files:**
- Create: `Assets/Scripts/Features/Buildings/PlacementService.cs`

**Steps:**

1. Create `PlacementService` with place/remove/validate for prefab-based placeables. It should:
   - Resolve cell grid position from world position via IChunkSystem
   - Validate: cells exist, unlocked, not occupied
   - Mark cells as Occupied
   - Instantiate prefab
   - Add BuildingMarker for interactable objects
   - Track all placed objects in ObservableList<PlacedObject>
   - Handle path placement by changing GroundState (no prefab)
   - Handle removal (Bulldoze): destroy instance, unmark cells, partial refund

2. Interface:
```csharp
public interface IPlacementService
{
    ObservableCollections.ObservableList<PlacedObject> PlacedObjects { get; }
    PlacedObject Place(PlaceableData data, Vector3 worldPos, float rotationY = 0f);
    bool CanPlace(PlaceableData data, Vector3 worldPos);
    bool Remove(PlacedObject obj);
}
```

3. `PlacedObject` runtime class:
```csharp
public class PlacedObject
{
    public PlaceableData Data;
    public Vector2Int ChunkCoord;
    public int CellX, CellY;
    public float RotationY;
    public int Level = 1;
    public GameObject Instance;
}
```

4. Register in FarmScope as `IPlacementService`.
5. Verify: Unity compiles.
6. Commit: `feat(A2): PlacementService — unified placement/removal for buildings, decor, paths`

---

### Task A3: Create PlaceableData assets (migrate Mill/Bakery + new decor)

**Files:**
- Create: `Assets/Settings/Placeables/Placeable_Mill.asset`
- Create: `Assets/Settings/Placeables/Placeable_Bakery.asset`
- Create: `Assets/Settings/Placeables/Placeable_Lamp.asset`
- Create: `Assets/Settings/Placeables/Placeable_Fence.asset`
- Create: `Assets/Settings/PlaceableDatabase.asset`
- Create: placeholder prefabs for Lamp, Fence

**Steps:**

1. Create `Assets/Settings/Placeables/` folder.
2. Create Mill PlaceableData: PlaceableId="mill", Category=Building, GridSize=2x2, Level=Chunk, Rotation=Step90, Prefab=Mill.prefab, Cost=50, Interactable=true, Recipes=[WheatToFlour].
3. Create Bakery PlaceableData similarly.
4. Create Lamp placeholder: Category=Decor, GridSize=1x1, Level=Cell, Rotation=Free5, Cost=15.
5. Create Fence placeholder: Category=Decor, GridSize=1x1, Level=Cell, Rotation=Step90, Cost=5.
6. Create PlaceableDatabase referencing all 4.
7. Register PlaceableDatabase in GameScope (SerializeField + RegisterInstance).
8. Assign in scene Inspector.
9. Verify: Play Mode, no errors.
10. Commit: `feat(A3): PlaceableData assets — Mill, Bakery, Lamp, Fence + database`

---

## Phase B: PlacementTool (unified)

### Task B1: Create PlacementTool

**Files:**
- Create: `Assets/Scripts/Player/Tools/PlacementTool.cs`

**Steps:**

1. Create `PlacementTool : ITool` that:
   - Holds either `PlantData` (for crops/trees/bushes) or `PlaceableData` (for buildings/decor/paths)
   - `SelectPlant(PlantData)` / `SelectPlaceable(PlaceableData)` / `ClearSelection()`
   - `UseAtPosition(Vector3)`:
     - PlantData + Crop/Bush → brush-based via BrushService + PlantSystem
     - PlantData + Tree → single placement via TreePlacementService
     - PlaceableData + Path → brush-based ground state change
     - PlaceableData + Building/Decor → single placement via PlacementService
   - Rotation: track `float _pendingRotation`, scroll wheel adjusts based on RotationMode
   - Ghost preview: `UpdatePreview(Vector3 cursorPos)` moves transparent copy

2. Add `ToolId.Placement` to enum (replacing Build).
3. Register in FarmScope.
4. Verify compilation.
5. Commit: `feat(B1): PlacementTool — unified placement for plants + placeables`

---

### Task B2: Ghost preview system

**Files:**
- Modify: `Assets/Scripts/Player/Tools/PlacementTool.cs`
- Modify: `Assets/Scripts/Player/FarmInteractionController.cs`

**Steps:**

1. In PlacementTool:
   - On selection, instantiate ghost prefab with transparent material
   - On `UpdatePreview(cursorPos)`: move ghost to snapped position, tint green/red (valid/invalid)
   - On place/deselect: destroy ghost
   - Scroll wheel: adjust `_pendingRotation` per RotationMode (5° for Free5, 90° for Step90)

2. In FarmInteractionController:
   - Each Update, if PlacementTool active, call `UpdatePreview(groundHitPoint)`
   - Pass scroll delta to tool for rotation

3. Create a shared transparent material for ghost preview.
4. Verify: Play Mode — ghost follows cursor.
5. Commit: `feat(B2): ghost preview with rotation during placement`

---

### Task B3: Remove old tools, migrate references

**Files:**
- Delete: `Assets/Scripts/Player/Tools/PlanterTool.cs`
- Delete: `Assets/Scripts/Player/Tools/BuildTool.cs`
- Modify: `Assets/Scripts/Infrastructure/Scopes/FarmScope.cs`
- Modify: `Assets/Scripts/Player/FarmInteractionController.cs`

**Steps:**

1. Remove `PlanterTool` and `BuildTool` registrations from FarmScope.
2. Register `PlacementTool` as `.As<PlacementTool, ITool>()`.
3. Remove old ToolId values (Planter, Build) or alias them.
4. Update FarmInteractionController: remove `_buildTool` injection, keybind for B cycling.
5. Remove `PlantAutoSelector.cs` if it exists (was selecting first plant on start).
6. Verify: compiles, Play Mode works.
7. Commit: `refactor(B3): remove PlanterTool + BuildTool, PlacementTool is the unified tool`

---

## Phase C: Path System

### Task C1: Extend GroundState with path types

**Files:**
- Modify: `Assets/Scripts/Features/Farming/SubCellState.cs`
- Modify: `Assets/Project/Shaders/GroundInstanced.shader`
- Modify: `Assets/Scripts/Features/Farming/PlantSystem.cs` (block planting on paths)

**Steps:**

1. Add to GroundState enum: `PathStone = 4, PathWood = 5, PathBrick = 6`
2. In GroundInstanced.shader frag():
   - State 4-6: use path-specific tint colors (new properties `_TintPathStone`, `_TintPathWood`, `_TintPathBrick`)
   - Render as full solid tile (same as farmed states — no edge softening, proximity handles edges)
3. In PlantSystem.Plant(): reject if `cell.GroundState >= GroundState.PathStone`
4. Mark path cells as Occupied = true
5. Verify: can set a cell to PathStone in code and see different ground color.
6. Commit: `feat(C1): path ground states — stone/wood/brick with shader tints`

---

### Task C2: Brush-based path painting in PlacementTool

**Files:**
- Modify: `Assets/Scripts/Player/Tools/PlacementTool.cs`
- Modify: `Assets/Scripts/Features/Farming/ChunkSystem.cs` (UpdateGroundNeighborFlags for paths)

**Steps:**

1. When PlacementTool has PlaceableData with Category.Path:
   - Use BrushService for area application (like crops)
   - For each cell: set GroundState to PathStone/Wood/Brick, set Occupied=true
   - Update cropState.z on GPU props
   - Call UpdateGroundNeighborFlags (paths count as "farmed" for neighbor awareness)
2. Verify: brush-paint paths in Play Mode, they block crop planting.
3. Commit: `feat(C2): brush-based path painting via PlacementTool`

---

## Phase D: Catalog Tab Bar UI

### Task D1: Create CatalogTabBar

**Files:**
- Create: `Assets/Scripts/UI/Catalog/CatalogTabBar.cs`
- Create: `Assets/Scripts/UI/Catalog/CatalogPresenter.cs`

**Steps:**

1. `CatalogTabBar` (MonoBehaviour) creates programmatic UI:
   - Top bar with tab buttons: Crops, Trees, Buildings, Decor, Paths, Tools
   - Bottom item panel: scrollable grid of items for selected category
   - Each item: icon placeholder + name + cost
   - Close/collapse button

2. `CatalogPresenter` (IInitializable):
   - Injects PlantDatabase + PlaceableDatabase + WalletService + IToolService
   - On tab select: populates items from appropriate database
   - On item click: calls PlacementTool.SelectPlant/SelectPlaceable
   - Tools tab: shows WateringCan, Sickle, Dye, Fertilizer, Uproot, Bulldoze
   - On tool click: equips that tool

3. Register in FarmScope, auto-build on HUD canvas.
4. Verify: tabs visible, clicking items selects them.
5. Commit: `feat(D1): CatalogTabBar — category tabs + item selection UI`

---

### Task D2: Bulldoze tool

**Files:**
- Create: `Assets/Scripts/Player/Tools/BulldozeTool.cs`

**Steps:**

1. `BulldozeTool : ITool`:
   - On click: Physics.Raycast for buildings/decor → PlacementService.Remove()
   - For paths: resolve cell → reset GroundState to Grass, clear Occupied
   - For crops: call PlantSystem.Uproot (existing)
2. Add `ToolId.Bulldoze`.
3. Register in FarmScope.
4. Verify: can remove placed objects and paths.
5. Commit: `feat(D2): BulldozeTool — remove placed objects, paths, crops`

---

### Task D3: Replace key-based tool switching

**Files:**
- Modify: `Assets/Scripts/Player/FarmInteractionController.cs`

**Steps:**

1. Remove all Alpha1-7 keybinds from HandleToolSwitching.
2. CatalogTabBar becomes the sole way to select tools/items.
3. Keep Q/E for brush size.
4. Keep Escape for cancel placement.
5. Verify: Play Mode — tools only accessible via catalog tabs.
6. Commit: `refactor(D3): remove keyboard tool switching, catalog UI is primary input`

---

## Phase E: Save/Load + Migration + Cleanup

### Task E1: Extend save/load for PlacedObjects

**Files:**
- Modify: `Assets/Scripts/Infrastructure/Save/FarmSaveData.cs`
- Modify: `Assets/Scripts/Infrastructure/Save/FarmSaveManager.cs`

**Steps:**

1. Add `PlacedObjectSaveData` struct: PlaceableId, ChunkCoordX/Y, CellX/Y, RotationY, Level.
2. Replace `PlacedBuildingSaveData` usage with `PlacedObjectSaveData`.
3. In CollectSaveData: iterate PlacementService.PlacedObjects.
4. In RestoreFromData: look up PlaceableDatabase, call PlacementService.Place().
5. Path cells already saved via SubCellSaveData.GroundState.
6. Verify: F5 save → F9 load → buildings + decor restored.
7. Commit: `feat(E1): save/load for unified PlacedObjects`

---

### Task E2: Remove old systems + cleanup

**Files:**
- Delete or gut: `Assets/Scripts/Features/Buildings/BuildingService.cs` (keep ProductionService)
- Delete: `Assets/Scripts/Features/Farming/TreePlacementService.cs` (merged into PlacementTool)
- Modify: `Assets/Scripts/Infrastructure/Scopes/FarmScope.cs`
- Modify: `Assets/Scripts/Infrastructure/Scopes/GameScope.cs`
- Update: `CLAUDE.md`

**Steps:**

1. Remove BuildingService (PlacementService handles placement, ProductionService handles recipes).
2. Remove TreePlacementService (PlacementTool handles tree placement directly).
3. Remove old BuildingData/BuildingDatabase (replaced by PlaceableData/PlaceableDatabase).
4. Update all FarmScope/GameScope registrations.
5. Update CLAUDE.md with new architecture.
6. Verify: full Play Mode test — plant crops, place buildings, paint paths, bulldoze, save/load.
7. Commit: `refactor(E2): remove legacy BuildingService + TreePlacementService, cleanup`

---

## Execution Order

```
A1 → A2 → A3 → B1 → B2 → B3 → C1 → C2 → D1 → D2 → D3 → E1 → E2
```

Each task delivers a compilable increment. Can pause after any task.

**Estimated effort:** ~13 tasks, each 10-30 minutes = roughly 4-6 hours total.
