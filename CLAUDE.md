# CLAUDE.md

## Project Overview

Farm creative sandbox for Steam (PC). Isometric top-down perspective. Player builds a farm where the field is a creative canvas -- plant crops, trees, bushes, change their colors, place buildings. Beauty requires effort: seeds, dyes, buildings cost money earned through farming.

**Vibe:** Spilled, Stardew Valley. Meditative with optional goals.

**Design doc:** `docs/plans/2026-03-04-wheat-farm-design.md`
**Implementation plan:** `docs/plans/2026-03-04-wheat-farm-implementation.md` (11 phases, ~35 tasks)

## Tech Stack

- **Unity 6000.0.48f1**, URP 17.0.4, Cinemachine 3.1.3
- **DI:** VContainer (hierarchical scopes: RootScope -> GameScope -> FarmScope)
- **Reactive:** R3 (ReactiveProperty, Subject). R3.Unity via git URL + R3.dll via NuGetForUnity
- **Async:** UniTask (replacing coroutines)
- **UI Pattern:** MVP (View / Presenter / Service)
- **Rendering:** GPU Instanced Indirect (DrawMeshInstancedIndirect + ComputeBuffers)
- **Other:** DOTween, LeanPool, NaughtyAttributes, Graphy

## Architecture

### Scope Hierarchy (VContainer)

```
RootScope (DontDestroyOnLoad)
├── SaveService, TimeService, ConfigDatabase
│
└── GameScope (game session)
    ├── WalletService, InventoryService, ShopService, ContractService
    ├── DayNightService, PlantDatabase
    │
    └── FarmScope (farm lifetime)
        ├── ChunkSystem, PlantSystem, BrushService, FarmRenderSystem, FarmBootstrap
        ├── ToolService, PlacementTool, WateringCanTool, SickleTool, DyeTool, FertilizerTool, UprootTool, BulldozeTool
        ├── PlacementService, ProductionService, TreePlacementService
        └── HUDPresenter, ShopPresenter, InventoryPresenter, ContractBoardPresenter, CatalogPresenter, BuildingPanelPresenter
```

### Assembly Structure (9 asmdefs)

```
WheatFarm.Core            -- interfaces, base types, MeshProperties, PlantData, PlantDatabase, PlaceableData, PlaceableDatabase
WheatFarm.Infrastructure  -- VContainer scopes (RootScope, GameScope, FarmScope), save/load
WheatFarm.Farming         -- PlantSystem, ChunkSystem, ChunkCropRenderer, FarmRenderSystem, BrushService
WheatFarm.Buildings       -- PlacementService, ProductionService, BuildingMarker
WheatFarm.Economy         -- WalletService, ShopService, ContractService
WheatFarm.Inventory       -- InventoryService
WheatFarm.DayNight        -- DayNightService
WheatFarm.UI              -- MVP Views + Presenters (HUD, Shop, Inventory, Contracts)
WheatFarm.Player          -- PlayerController, FarmInteractionController, Tools (PlacementTool, BulldozeTool, etc.), PlantAutoSelector
```

**Dependency rule:** WheatFarm.Player -> WheatFarm.Farming (OK). Farming CANNOT reference Player.

### Data Flow

```
Input -> FarmInteractionController -> ToolService -> ITool -> BrushService (area)
  -> PlantSystem/PlacementService (state change) -> ChunkData.Dirty=true
  -> FarmRenderSystem.Tick() -> ChunkCropRenderer.SyncIfDirty() -> ComputeBuffer upload -> Draw

CatalogTabBar (UI) -> CatalogPresenter -> PlacementTool.SelectPlant/SelectPlaceable
  -> PlacementTool.UseAtPosition -> PlantSystem (crops/bushes) / TreePlacementService (trees)
     / PlacementService (buildings/decor) / BrushService+GroundState (paths)
```

### Core Systems

| System | Purpose | Status |
|--------|---------|--------|
| **ChunkSystem** | Grid of chunks, CellToWorld, GetCellsInRadius, territory expansion | Working |
| **PlantSystem** | Plant/Water/Harvest/Uproot/Dye, growth in Tick(), visual scale | Working |
| **BrushService** | Brush radius (Small=1, Medium=2, Large=3), area painting | Working |
| **FarmRenderSystem** | Manages per-chunk ChunkCropRenderers, sync + draw each frame | Working |
| **FarmBootstrap** | Unlocks 25 starter chunks (5x5 grid, radius 2) | Working |
| **ToolService** | 8 tools (Placement, WateringCan, Sickle, Dye, Fertilizer, Uproot, Bulldoze) | Working |
| **PlacementService** | Unified placement/removal for buildings, decor (prefab-based) | Working |
| **PlacementTool** | Unified tool: plants + placeables, ghost preview, rotation | Working |
| **BulldozeTool** | Remove buildings/decor/paths/crops with partial refund | Working |
| **CatalogTabBar** | Category tab UI: Crops/Trees/Buildings/Decor/Paths/Tools | Working |
| **ProductionService** | Processing queues, timers, recipes, auto-repeat, slot limits | Working |
| **BuildingPanelPresenter** | MVP UI for building interaction, recipes, upgrades | Working |
| **TreePlacementService** | Multi-cell tree placement | Working |
| **WalletService** | Coins add/spend with reactive events | Stub |
| **ShopService** | Catalog, purchase, unlock | Stub |
| **ContractService** | Optional contracts, progress tracking | Stub |
| **InventoryService** | Seeds, dyes, fertilizers, harvest | Stub |
| **DayNightService** | 14-min cycle, lighting | Stub |

## Crop Rendering Pipeline (CRITICAL)

### How It Works

1. Each unlocked chunk gets a `ChunkCropRenderer` with its own `ComputeBuffer` and **cloned Materials**
2. `MeshProperties` struct (160 bytes): `m` (TRS matrix), `gr` (ground matrix), `color`, `uv`, `cropState`
3. `GetStructedBuffer.hlsl` provides `StructuredBuffer<MeshProperties>` to ShaderGraph
4. ShaderGraph `Grass Instanced` with `PROCEDURAL_INSTANCING_ON` keyword
5. **Multi-pass rendering:** one `DrawMeshInstancedIndirect` per (mesh, material) pair per chunk. Each material's `_Id` filters which instances are visible via ShaderGraph (`cropState.x == _Id`).

### Key Shader Rules

- **Positions are RELATIVE to chunk bounds center.** The shader does `objectToWorld = mul(objectToWorld, data.m)` — the matrix `m` is multiplied with the bounds-based objectToWorld. Using absolute world positions causes double-offset.
- **`cropState.x` must match material's `_Id` property** (currently `_Id = 1.0` on Grass.mat). Set `cropState.x = 1`.
- **`cropState.y` controls visibility.** When `cropState.y = 0`, shader moves instance to `_HiddenHeight = -2` (invisible). When `cropState.y > 0`, it appears at `_ShowHeight = 0`. **Newly planted crops MUST have `cropState.y > 0`.**
- **`cropState.y` controls visibility.** When `cropState.y = 0`, shader moves instance to `_HiddenHeight = -2` (invisible). When `cropState.y > 0`, it appears at `_ShowHeight = 0`. **Newly planted crops MUST have `cropState.y > 0`.**
- **`_Interaction_Position` is a GLOBAL shader property.** Set via `Shader.SetGlobalVector()`, NOT `material.SetVector()`. This is because each chunk clones the material (`new Material(sharedMaterial)` in ChunkCropRenderer line 32), so per-material updates to the original have no effect.
- **`_Interaction_Power = 7.24`** and **`_Interaction_Radius = 1.2`** control the grass-trampling/bending effect around `_Interaction_Position`.

### Current Render Config (FarmRenderConfig.asset)

- **CropMesh:** `Plane` from `pyramid.fbx` (very small native mesh)
- **CropMaterial:** `Grass.mat` (Grass Instanced ShaderGraph)
- **ChunkWorldSize:** 4
- **SubCellResolution:** 16 (16x16 = 256 cells per chunk)
- **StarterChunkRadius:** 2 (5x5 = 25 chunks)
- **ScaleMultiplier:** 72 (in PlantSystem.cs — compensates for small mesh and 0.25 CellWorldSize)

### Visual Growth

Plants scale from 30% to 100% as they grow (0 -> 1). `RebuildMatrix()` in `PlantSystem.Tick()` reconstructs TRS matrix each frame for growing plants. Each plant gets random `BaseScale` (from PlantData.ScaleRange) and `RotationY` at planting time.

## Player & Interaction

- **PlayerController:** WASD camera-relative movement. Player at Y=1.51 (capsule center), Rigidbody + CapsuleCollider.
- **FarmInteractionController:** Left-click → raycast to Y=0 plane → UseCurrentTool. Q/E change brush size. Scroll wheel rotates placement. Escape cancels. No keyboard tool switching (CatalogTabBar UI only). Sets `Shader.SetGlobalVector("_Interaction_Position", transform.position)` each frame for grass trampling.
- **PlantAutoSelector:** Auto-selects first unlocked plant (wheat) on start.
- **Camera:** Isometric angle (35.26°, 45°), CinemachineCamera + CinemachineFollow offset (-5, 5, -5).
- **Raycast** uses mathematical `Plane(Vector3.up, Vector3.zero)`, NOT Physics.Raycast.

## Plant Data (ScriptableObjects)

| Plant | Category | Growth | Cost | Sell | Unlocked | Renewable |
|-------|----------|--------|------|------|----------|-----------|
| Wheat | Crop | 5s* | 3 | 8 | Yes | No |
| Corn | Crop | 45s | 5 | 12 | Yes | No |
| Tomato | Crop | 50s | 6 | 15 | Yes | No |
| Sunflower | Crop | 40s | 4 | 10 | Yes | No |
| Rose | Bush | 90s | 10 | 20 | No | Yes |
| Cherry | Tree | 180s | 25 | 40 | No | Yes |

*Wheat growth = 5s for testing

## Conventions

- **DI:** VContainer constructor injection. No static ServiceLocator.
- **UI:** MVP pattern. View (MonoBehaviour, visuals only) + Presenter (pure C#, IInitializable) + Service.
- **Reactive:** R3 ReactiveProperty for state, Subject for events. No Update() polling for UI.
- **Features:** Each system = IFeatureInstaller + separate asmdef.
- **Data:** PlantData ScriptableObject per plant type (data-driven, not enums).
- **Naming:** `Service` suffix for services, `View`/`Presenter` suffix for UI, `Installer` suffix for DI.
- **Tools:** Implement `ITool` interface, registered in FarmScope. `PlacementTool` registered as `.As<PlacementTool, ITool>()`.

## VContainer Critical Notes

- **Parent scope references are NOT automatic.** Each LifetimeScope needs `parentReference.TypeName` set in Inspector:
  - RootScope: empty (root, found via VContainerSettings)
  - GameScope: `WheatFarm.Infrastructure.RootScope`
  - FarmScope: `WheatFarm.Infrastructure.GameScope`
- **ITickable.Tick()** takes no parameters. Use `Time.deltaTime` inside.
- **ITickable registration:** Use `.As<IMyService, ITickable>()`.
- **`RegisterInstance` registers as concrete type** — child scopes resolve via parent chain IF parentReference is set.

## Key Files

### Infrastructure
- `Assets/Scripts/Infrastructure/Scopes/RootScope.cs`
- `Assets/Scripts/Infrastructure/Scopes/GameScope.cs`
- `Assets/Scripts/Infrastructure/Scopes/FarmScope.cs` — registers ALL farming, tool, building, UI systems

### Farming (core gameplay)
- `Assets/Scripts/Features/Farming/PlantSystem.cs` — Plant/Water/Harvest/Tick, ScaleMultiplier=72, InitialGrowth=0.1
- `Assets/Scripts/Features/Farming/ChunkSystem.cs` — CellToWorld, ChunkBoundsCenter, GetCellsInRadius
- `Assets/Scripts/Features/Farming/ChunkCropRenderer.cs` — per-chunk ComputeBuffer + DrawMeshInstancedIndirect, **clones material**
- `Assets/Scripts/Features/Farming/FarmRenderSystem.cs` — manages all ChunkCropRenderers
- `Assets/Scripts/Features/Farming/FarmRenderConfig.cs` — ScriptableObject config
- `Assets/Scripts/Features/Farming/SubCellState.cs` — cell state (PlantId, Growth, Watered, BaseScale, RotationY, Color)
- `Assets/Scripts/Features/Farming/BrushService.cs` — area painting

### Player
- `Assets/Scripts/Player/PlayerController.cs` — WASD movement
- `Assets/Scripts/Player/FarmInteractionController.cs` — click→tool, brush size, grass interaction position
- `Assets/Scripts/Player/Tools/ITool.cs` — tool interface
- `Assets/Scripts/Player/Tools/ToolService.cs` — tool management

### Shader / Material
- `Assets/Project/Shaders/GetStructedBuffer.hlsl` — GPU instanced indirect HLSL (**DO NOT MODIFY**)
- `Assets/Project/Shaders/Grass Instanced.shadergraph` — ShaderGraph with PROCEDURAL_INSTANCING_ON
- `Assets/Project/Materials/Crops/Grass.mat` — main crop material (_Id=1, _Interaction_Power=7.24)

### Config Assets
- `Assets/Settings/FarmRenderConfig.asset` — SubCellResolution=16, ChunkWorldSize=4
- `Assets/Settings/PlantDatabase.asset` — references all 6 PlantData assets
- `Assets/Settings/Plants/Plant_*.asset` — individual plant data
- `Assets/Settings/PlaceableDatabase.asset` — references all 7 PlaceableData assets
- `Assets/Settings/Placeables/Placeable_*.asset` — individual placeable data (Mill, Bakery, Lamp, Fence, 3 paths)

### Scene
- `Assets/Project/Scenes/Main.unity` — Player, Cinemachine, Ground, RootScope>GameScope>FarmScope

## Current State

### What Works (Play Mode verified)
- VContainer DI: all 3 scopes configure correctly (Root → Game → Farm)
- 25 starter chunks (5x5), 256 cells each = 6,400 total plantable cells
- Click to plant crops (brush-based), visual growth from 30% to 100% scale
- WASD movement, isometric camera follow
- 8 tools switchable via CatalogTabBar UI (Placement, WateringCan, Sickle, Dye, Fertilizer, Uproot, Bulldoze)
- Q/E to change brush size
- Grass trampling effect follows player position (global shader property)
- GPU instanced indirect rendering (per-chunk ComputeBuffers)
- Auto-water on plant (crops grow immediately after planting)
- Per-cell ground state tracking (Grass/Tilled/Watered/Fertilized in cropState.z)
- Dual DrawMeshInstancedIndirect per chunk (ground tiles + crops, shared ComputeBuffer)
- Ground tiles with neighbor-aware edge softening + proximity fade on grass (2-cell radius)
- Cross-chunk neighbor flags computed in C# for seamless chunk boundaries
- **Economy loop**: harvest → inventory → sell → coins → buy seeds
- HarvestRewardHandler bridges PlantSystem.OnHarvested → InventoryService
- ShopService.TrySell: sell from inventory for coins via PlantDatabase prices
- **HUD**: programmatic Canvas with coins, 6 tool icons, day/night time display
- **Shop UI**: Tab to toggle, catalog from PlantDatabase, buy with coins
- **Inventory UI**: I to toggle, live-updating item list from ObservableList
- **Buildings**: 5 production buildings (Mill, Bakery, Kitchen, Workshop, Sawmill) with placeholder cube prefabs
- **Tree placement**: PlanterTool routes Category.Tree through TreePlacementService (multi-cell trunk)
- **Day/night lighting**: LightingController drives Directional Light sun arc, color, intensity, ambient
- **Save/load**: F5=Save, F9=Load, auto-load on start. Saves chunks, cells, inventory, buildings, trees, time
- Crop rotation restricted to camera-facing range (165° ± 25°)

### Universal Placement System (NEW)
- **PlaceableData**: ScriptableObject for buildings/decor/paths (10 assets: Mill, Bakery, Kitchen, Workshop, Sawmill, Lamp, Fence, 3 paths)
- **PlacementService**: Unified placement/removal for buildings (chunk-level) + decor (cell-level) with ghost preview
- **PlacementTool**: Single tool handles crops (brush), trees (single), buildings/decor (single+ghost), paths (brush)
- **BulldozeTool**: Brush-based removal of paths/crops, proximity removal of buildings/decor, 50% refund
- **CatalogTabBar**: 6 category tabs (Crops/Trees/Buildings/Decor/Paths/Tools) with item panel
- **Ghost preview**: Transparent prefab follows cursor, green=valid/red=invalid, scroll rotates (Step90/Free5)
- **Path system**: 3 path types (Stone/Wood/Brick) as GroundState 4-6, brush-painted, shader-rendered with tint colors
- Save/load extended with PlacedObjectSaveData (buildings+decor via PlacementService)

### Production System
- **ProductionService**: Processing queues with slot limits (1/2/3 per building level), auto-repeat recipes
- **5 buildings**: Mill (wheat→flour), Bakery (flour→bread), Kitchen (tomato→sauce, cherry→jam), Workshop (rose→bouquet), Sawmill (wood→planks)
- **BuildingPanelPresenter**: MVP UI — click building to open panel, start/stop recipes, upgrade, auto-repeat toggle
- **Wood from trees**: Uprooting fully grown trees yields wood (Cherry = 3 wood)
- **Upgrades**: Coins + Planks to upgrade buildings (more production slots)
- **Save/load**: Active production slots persist across save/load
- Legacy `BuildingService` and `BuildingData` removed — `PlacementService` + `PlacedObject` is the sole building system

### Git History
```
bf28b5f fix: use Shader.SetGlobalVector for _Interaction_Position (global shader property)
8edb3b9 feat: pass player position to crop shader for grass trampling effect
5d9dfd7 feat: increase crop density 4x (SubCellResolution 8->16), disable trampling effect
cd9e128 fix: crop positions relative to chunk bounds center, increase scale 20%
f19d775 feat: visual crop growth — plants scale up from 30% to 100% as they grow
ff6150d fix: GPU instanced crop rendering — proper scale, cropState, TRS matrix on plant placement
4c0105e feat: player controls, 6 plant data assets, PlantAutoSelector, clean up missing scripts
433fa6d feat: VContainer+R3+MVP architecture (phases 0-11), remove legacy AllServices/GameStateMachine
```

### What's Next (polish & content)
1. **Contract system UI** — ContractBoardView/Presenter exist, need scene setup + data.
2. **Ground atlas texture** — actual soil textures instead of flat tint colors.
3. **Building 3D models** — replace placeholder cubes with actual meshes.
4. **Smoke particles on buildings** — add ParticleSystem named "SmokeEffect" to building prefabs (code already handles it).
5. **More production chains** — additional recipes, new building types.

### Known Issues
- Graphy FPS counter shows 1 FPS on first frame after entering Play Mode (screenshot artifact, normalizes after).
- No visual feedback for brush size changes.
- Tree growth not saved (hardcoded 0 in FarmSaveManager).
- Unlocked plants and contracts not persisted in save data.
- Bakery UnlockedByDefault=false but no unlock system exists yet (set to true for testing).

## Obsidian Knowledge Base

Two Obsidian vaults connected via MCP (`obsidian` server in `.opencode/opencode.json`):

### dev-knowledge (cross-project)
Path: `D:\ObsidianVaults\Vaults\dev-knowledge`
Contains: Bug postmortems, Unity guidelines, code patterns, architecture decisions, performance notes.

**When to update dev-knowledge:**
- New non-obvious bug discovered (with root cause and fix)
- Architecture decision made (why X not Y, with trade-offs)
- Reusable pattern identified (used in 2+ places)
- Code review feedback received (from lead or peers)
- Performance optimization with measurable results

**Do NOT update for:** routine code changes, adding methods, refactoring, balance tweaks.

### wheat-farm (project-specific)
Path: `D:\ObsidianVaults\Vaults\wheat-farm`
Contains: System descriptions, DI scope maps, render pipeline docs, architecture tree.

**When to update wheat-farm vault:**
- New DI scope or major system added
- Architecture changed (system replaced, new assembly)
- Render pipeline modified

**Do NOT update for:** new PlantData, new tool (follows ITool pattern), UI changes, bug fixes.
