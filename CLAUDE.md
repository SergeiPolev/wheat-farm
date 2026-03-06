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
        ├── ToolService, PlanterTool, WateringCanTool, SickleTool, DyeTool, FertilizerTool, UprootTool
        ├── BuildingService, ProductionService, TreePlacementService
        └── HUDPresenter, ShopPresenter, InventoryPresenter, ContractBoardPresenter
```

### Assembly Structure (9 asmdefs)

```
WheatFarm.Core            -- interfaces, base types, MeshProperties, PlantData, PlantDatabase
WheatFarm.Infrastructure  -- VContainer scopes (RootScope, GameScope, FarmScope), save/load
WheatFarm.Farming         -- PlantSystem, ChunkSystem, ChunkCropRenderer, FarmRenderSystem, BrushService
WheatFarm.Buildings       -- BuildingService, ProductionService
WheatFarm.Economy         -- WalletService, ShopService, ContractService
WheatFarm.Inventory       -- InventoryService
WheatFarm.DayNight        -- DayNightService
WheatFarm.UI              -- MVP Views + Presenters (HUD, Shop, Inventory, Contracts)
WheatFarm.Player          -- PlayerController, FarmInteractionController, Tools, PlantAutoSelector
```

**Dependency rule:** WheatFarm.Player -> WheatFarm.Farming (OK). Farming CANNOT reference Player.

### Data Flow

```
Input -> FarmInteractionController -> ToolService -> ITool -> BrushService (area)
  -> PlantSystem/BuildingService (state change) -> ChunkData.Dirty=true
  -> FarmRenderSystem.Tick() -> ChunkCropRenderer.SyncIfDirty() -> ComputeBuffer upload -> Draw
```

### Core Systems

| System | Purpose | Status |
|--------|---------|--------|
| **ChunkSystem** | Grid of chunks, CellToWorld, GetCellsInRadius, territory expansion | Working |
| **PlantSystem** | Plant/Water/Harvest/Uproot/Dye, growth in Tick(), visual scale | Working |
| **BrushService** | Brush radius (Small=1, Medium=2, Large=3), area painting | Working |
| **FarmRenderSystem** | Manages per-chunk ChunkCropRenderers, sync + draw each frame | Working |
| **FarmBootstrap** | Unlocks 25 starter chunks (5x5 grid, radius 2) | Working |
| **ToolService** | 6 tools (Planter, WateringCan, Sickle, Dye, Fertilizer, Uproot) | Working |
| **BuildingService** | Placement, moving, upgrading | Stub |
| **ProductionService** | Processing queues, timers, recipes | Stub |
| **TreePlacementService** | Free placement of trees/bushes | Stub |
| **WalletService** | Coins add/spend with reactive events | Stub |
| **ShopService** | Catalog, purchase, unlock | Stub |
| **ContractService** | Optional contracts, progress tracking | Stub |
| **InventoryService** | Seeds, dyes, fertilizers, harvest | Stub |
| **DayNightService** | 14-min cycle, lighting | Stub |

## Crop Rendering Pipeline (CRITICAL)

### How It Works

1. Each unlocked chunk gets a `ChunkCropRenderer` with its own `ComputeBuffer` and **cloned Material**
2. `MeshProperties` struct (160 bytes): `m` (TRS matrix), `gr` (ground matrix), `color`, `uv`, `cropState`
3. `GetStructedBuffer.hlsl` provides `StructuredBuffer<MeshProperties>` to ShaderGraph
4. ShaderGraph `Grass Instanced` with `PROCEDURAL_INSTANCING_ON` keyword
5. `Graphics.DrawMeshInstancedIndirect(mesh, 0, material, bounds, argsBuffer)` per chunk

### Key Shader Rules

- **Positions are RELATIVE to chunk bounds center.** The shader does `objectToWorld = mul(objectToWorld, data.m)` — the matrix `m` is multiplied with the bounds-based objectToWorld. Using absolute world positions causes double-offset.
- **`cropState.x` must match material's `_Id` property** (currently `_Id = 1.0` on Grass.mat). Set `cropState.x = 1`.
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
- **FarmInteractionController:** Left-click → raycast to Y=0 plane → UseCurrentTool. Keys 1-6 switch tools. Q/E change brush size. Sets `Shader.SetGlobalVector("_Interaction_Position", transform.position)` each frame for grass trampling.
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
- **Tools:** Implement `ITool` interface, registered in FarmScope. `PlanterTool` registered as `.As<PlanterTool, ITool>()`.

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

### Scene
- `Assets/Project/Scenes/Main.unity` — Player, Cinemachine, Ground, RootScope>GameScope>FarmScope

## Current State

### What Works (Play Mode verified)
- VContainer DI: all 3 scopes configure correctly (Root → Game → Farm)
- 25 starter chunks (5x5), 256 cells each = 6,400 total plantable cells
- Click to plant crops (brush-based), visual growth from 30% to 100% scale
- WASD movement, isometric camera follow
- 6 tools switchable with keys 1-6 (Planter, WateringCan, Sickle, Dye, Fertilizer, Uproot)
- Q/E to change brush size
- Grass trampling effect follows player position (global shader property)
- GPU instanced indirect rendering (per-chunk ComputeBuffers)

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

### What's Next (priority order)
1. **Auto-water on plant** — crops don't grow until watered, no visual prompt yet. Quick fix: auto-set Watered=true on plant.
2. **HUD UI** — show current tool, selected plant, coins, brush size. MVP views exist but not wired to scene.
3. **Per-plant-type meshes** — use Corn1_P.fbx, Sunflower1_P.fbx etc. Requires multi-material rendering.
4. **Harvest + economy loop** — sickle harvests → coins added → shop to buy seeds.
5. **Save/load** — FarmSaveService exists as stub.
6. **Buildings, production chains, day/night** — stubs exist, need implementation.

### Known Issues
- Graphy FPS counter shows 1 FPS on first frame after entering Play Mode (screenshot artifact, normalizes after).
- No visual feedback for tool switching or brush size.
- Crops only grow when `Watered = true` — need to either auto-water or make watering more discoverable.
