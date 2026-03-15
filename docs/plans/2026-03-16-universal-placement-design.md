# Universal Placement System — Design Document

**Date:** 2026-03-16
**Status:** Draft

## Problem

Current codebase has 3 separate placement systems (BuildingService, TreePlacementService, PlanterTool/PlantSystem) with duplicated logic for grid snapping, cell occupation, prefab instantiation, and save/load. Adding new object types (decor, paths, fences) requires creating yet another service each time. This doesn't scale.

## Solution

Unified `PlacementService` that handles all placeable objects through a single data-driven pipeline. One `PlaceableData` ScriptableObject defines everything about how an object is placed, rendered, and interacted with.

## Categories

| Category | Examples | Grid Snap | Blocks Planting | Visual | Interactable |
|----------|----------|-----------|-----------------|--------|-------------|
| **Crop** | Wheat, Corn, Tomato | Cell grid | No (IS the planting) | GPU instanced (existing) | No (harvest via tool) |
| **Tree** | Cherry, Oak, Pine | Cell grid | Yes (trunk cells) | Prefab instantiation | No |
| **Bush** | Rose, Berry | Cell grid | No | GPU instanced (existing) | No (harvest via tool) |
| **Building** | Mill, Bakery, Silo | Chunk grid | Yes (whole chunk) | Prefab | Yes (click → recipe UI) |
| **Decor** | Lamp, Bench, Statue, Fence | Cell grid | Yes | Prefab | No |
| **Path** | Stone, Wood, Brick | Cell grid (brush) | Yes | Ground tile shader | No |

## Data Model

### PlaceableData (ScriptableObject)

Replaces `BuildingData` for buildings/decor. Crops/bushes/trees continue using `PlantData` (too intertwined with growth/harvest to merge).

```csharp
[CreateAssetMenu(menuName = "WheatFarm/PlaceableData")]
public class PlaceableData : ScriptableObject
{
    [Header("Identity")]
    public string PlaceableId;
    public string DisplayName;
    public PlaceableCategory Category; // Building, Decor, Path
    public Sprite Icon; // for catalog UI

    [Header("Placement")]
    public Vector2Int GridSize = Vector2Int.one; // cells occupied
    public bool BlocksPlanting = true;
    public PlacementLevel Level; // Cell, Chunk (buildings use chunk-level)

    [Header("Visual")]
    public GameObject Prefab; // null for paths (shader-rendered)

    [Header("Path Properties")]
    public int PathSubtype; // 0=stone, 1=wood, 2=brick (only for Category.Path)
    public Color PathColor = Color.white;

    [Header("Economy")]
    public int Cost;
    public bool UnlockedByDefault;

    [Header("Interaction")]
    public bool Interactable; // click opens UI
    public RecipeData[] Recipes; // production recipes (buildings only)
    public int MaxLevel = 1;
}

public enum PlaceableCategory { Building, Decor, Path }
public enum PlacementLevel { Cell, Chunk }
```

### PlantData stays separate

`PlantData` (crops, bushes, trees) remains unchanged — it has growth, watering, harvesting, renewable flag, scale ranges. These are fundamentally different from static placeables. `PlanterTool` continues to handle PlantData.

### Catalog: PlaceableDatabase

```csharp
[CreateAssetMenu(menuName = "WheatFarm/PlaceableDatabase")]
public class PlaceableDatabase : ScriptableObject
{
    public PlaceableData[] Items;
    
    public PlaceableData GetById(string id);
    public PlaceableData[] GetByCategory(PlaceableCategory cat);
    public PlaceableData[] GetUnlocked(); // for catalog UI
}
```

## Placement Pipeline

### PlacementService (replaces BuildingService for all static objects)

```
SelectFromCatalog(PlaceableData) → Preview ghost → Click → Validate → Place → Mark cells → Instantiate
```

**Validation:**
- All target cells exist and are in unlocked chunks
- No cells occupied by other placeables or crops
- Player can afford the cost

**Cell marking:**
- For prefab objects (Building, Decor): mark cells as `Occupied = true`
- For paths: set `GroundState = Path`, `PathSubtype` in cell data

**Instantiation:**
- Prefab objects: `Object.Instantiate(prefab, snappedPosition, rotation)`
- Paths: no instantiation, ground tile shader handles rendering

### PlacedObject (runtime data)

```csharp
public class PlacedObject
{
    public PlaceableData Data;
    public Vector2Int AnchorCell; // bottom-left cell (chunk coord + cell coord)
    public Vector2Int ChunkCoord;
    public int CellX, CellY;
    public int Level = 1;
    public GameObject Instance; // null for paths
}
```

### Path rendering

Paths use the existing ground tile system:
- `GroundState` enum gets new values: `PathStone = 4, PathWood = 5, PathBrick = 6`
- `GroundInstanced.shader` gets new tint colors for each path type
- `cropState.z` encodes the path state, shader renders appropriate color/pattern
- Path cells set `Occupied = true` to block crop planting
- Brush-based painting (Q/E size) like crops

## UI: Category Tab Bar

Top of screen, horizontal tabs. Replaces current tool switching (1-7 keys).

```
[Crops] [Trees] [Buildings] [Decor] [Paths] [Tools]
```

### Behavior
- Click tab → bottom panel shows items in that category
- Click item → enters placement mode (cursor shows ghost preview)
- LMB click → place object
- RMB or Escape → cancel placement
- **Tools tab** contains non-placement tools: WateringCan, Sickle, Dye, Fertilizer, Uproot

### Item entries
Each entry shows: icon, name, cost. Locked items shown grayed out.

### Implementation
- `CatalogTabBar` (MonoBehaviour on Canvas) — tab buttons + item scroll area
- `CatalogPresenter` (pure C#, IInitializable) — populates from PlantDatabase + PlaceableDatabase
- Items sourced from two databases: PlantData (crops/trees/bushes) + PlaceableData (buildings/decor/paths)

## Placement Tool (unified)

Replaces `PlanterTool` + `BuildTool`. Single tool that handles all placement.

```csharp
public class PlacementTool : ITool
{
    // Can hold either PlantData or PlaceableData
    public void SelectPlant(PlantData plant);
    public void SelectPlaceable(PlaceableData placeable);
    
    public void UseAtPosition(Vector3 worldPos)
    {
        if (_selectedPlant != null)
        {
            // Crops/bushes: brush-based (existing PlantSystem logic)
            // Trees: single placement (existing TreePlacementService logic)
        }
        else if (_selectedPlaceable != null)
        {
            if (_selectedPlaceable.Category == PlaceableCategory.Path)
                // Brush-based ground state change
            else
                // Single placement via PlacementService
        }
    }
}
```

## Interaction (Buildings)

Unchanged from current: `BuildingClickHandler` detects Physics.Raycast hit on `BuildingMarker` component, opens `BuildingInteractionPanel` with recipes.

Decor objects have no interaction — just visual.

## Save/Load

### PlacedObjectSaveData
```csharp
public struct PlacedObjectSaveData
{
    public string PlaceableId;
    public int ChunkCoordX, ChunkCoordY;
    public int CellX, CellY;
    public int Level;
}
```

Replaces `PlacedBuildingSaveData`. Paths are saved as part of cell state (GroundState already saved in SubCellSaveData).

## Migration from Current Systems

### Keep (no changes):
- `PlantSystem` — crop growth, watering, harvesting, GPU instanced rendering
- `ChunkSystem` — grid, cells, chunks
- `FarmRenderSystem` / `ChunkCropRenderer` — GPU instanced rendering pipeline
- `GroundInstanced.shader` — extend with path states
- `BrushService` — brush radius painting

### Refactor:
- `BuildingService` → merge into `PlacementService` (keep production logic in `ProductionService`)
- `TreePlacementService` → merge tree placement into `PlacementTool` (trees still use PlantData)
- `BuildTool` → merge into `PlacementTool`
- `PlanterTool` → merge into `PlacementTool`
- `FarmInteractionController` tool switching → replaced by `CatalogTabBar` UI

### New:
- `PlaceableData` ScriptableObject
- `PlaceableDatabase` ScriptableObject
- `PlacementService` — unified placement/removal for buildings, decor, paths
- `PlacementTool` — unified tool
- `CatalogTabBar` — UI for category selection
- `CatalogPresenter` — populates catalog from databases
- Extend `GroundState` with path types
- Extend `GroundInstanced.shader` with path tint colors
- Extend `SubCellSaveData` with path subtype
- `PlacedObjectSaveData` replacing `PlacedBuildingSaveData`

## Implementation Phases

### Phase A: Data + PlacementService (foundation)
1. Create `PlaceableData`, `PlaceableDatabase` ScriptableObjects
2. Create `PlacementService` with place/remove/validate for prefab objects
3. Create initial data assets: Mill, Bakery as PlaceableData (migrate from BuildingData)
4. Create decor assets: Lamp, Fence (simple cube placeholders)

### Phase B: PlacementTool (unified tool)
1. Create `PlacementTool` that handles PlantData + PlaceableData
2. Migrate PlanterTool crop/tree logic into PlacementTool
3. Migrate BuildTool building logic into PlacementTool
4. Remove old PlanterTool, BuildTool

### Phase C: Path system
1. Add `PathStone/PathWood/PathBrick` to GroundState enum
2. Add path tint colors to GroundInstanced.shader
3. PlacementTool brush-paints paths via PlantSystem-like ground state changes
4. Path cells block crop planting

### Phase D: Catalog UI
1. Create `CatalogTabBar` with category tabs (top of screen)
2. Create item list panel (bottom, scrollable)
3. `CatalogPresenter` populates from PlantDatabase + PlaceableDatabase
4. Selection → PlacementTool.SelectPlant/SelectPlaceable
5. Replace key-based tool switching with tab-based UI

### Phase E: Save/Load + Cleanup
1. Extend save data with PlacedObjectSaveData
2. Remove old BuildingService, TreePlacementService, BuildTool, PlanterTool
3. Update FarmScope registrations
4. Update CLAUDE.md

## Open Questions

- Ghost preview during placement? (transparent copy of prefab following cursor)
- Rotation support for placed objects? (R to rotate 90°)
- Undo/bulldoze tool for removing placed objects?
