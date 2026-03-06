# CLAUDE.md

## Project Overview

Farm creative sandbox for Steam (PC). Isometric top-down perspective. Player builds a farm where the field is a creative canvas -- plant crops, trees, bushes, change their colors, place buildings. Beauty requires effort: seeds, dyes, buildings cost money earned through farming.

**Vibe:** Spilled, Stardew Valley. Meditative with optional goals.

**Design doc:** `docs/plans/2026-03-04-wheat-farm-design.md`

## Tech Stack (Target)

- **Unity 6000.0.48f1**, URP 17.0.4, Cinemachine 3.1.3
- **DI:** VContainer (hierarchical scopes: RootScope -> GameScope -> FarmScope)
- **Reactive:** R3 (ReactiveProperty, ReactiveEvent, ObservableList)
- **Async:** UniTask (replacing coroutines)
- **UI Pattern:** MVP (View / Presenter / Service)
- **Rendering:** GPU Instanced Indirect (DrawMeshInstancedIndirect + ComputeBuffers)
- **Other:** DOTween, LeanPool, NaughtyAttributes, Graphy

## Tech Stack (Current / Legacy -- being refactored)

- **DI:** `AllServices` singleton ServiceLocator
- **State:** `GameStateMachine` with `IState`/`ITick`/`IFixedTick`
- **UI:** `WindowBase` MonoBehaviour with logic inside
- **Async:** Coroutines

## Architecture (Target)

### Scope Hierarchy (VContainer)

```
RootScope (DontDestroyOnLoad)
├── SaveService, TimeService, ConfigDatabase
│
└── GameScope (game session)
    ├── WalletService, InventoryService, ShopService, ContractService
    ├── DayNightService, InputService, CameraService
    │
    └── FarmScope (farm lifetime)
        ├── ChunkSystem, PlantSystem, BrushService
        ├── BuildingService, ProductionService
        ├── TreePlacementService
        └── CropRenderer (per-chunk)
```

### Feature Installers

Each game system = separate `IFeatureInstaller` + assembly definition:

```
Features/
  Farming/FarmingInstaller.cs         -- PlantSystem, BrushService, ChunkSystem
  Buildings/BuildingsInstaller.cs     -- BuildingService, ProductionService
  Economy/EconomyInstaller.cs         -- WalletService, ShopService, ContractService
  DayNight/DayNightInstaller.cs       -- DayNightService, LightingController
  Inventory/InventoryInstaller.cs     -- InventoryService
```

### Assembly Structure

```
WheatFarm.Core            -- interfaces, base types, extensions
WheatFarm.Infrastructure  -- VContainer scopes, bootstrap, save/load
WheatFarm.Farming         -- PlantSystem, ChunkSystem, CropRenderer, BrushService
WheatFarm.Buildings       -- BuildingService, ProductionService
WheatFarm.Economy         -- Wallet, Shop, Contracts, Inventory
WheatFarm.UI              -- Views, Presenters (MVP)
WheatFarm.DayNight        -- time of day, lighting
WheatFarm.Player          -- movement, tools, ToolService
```

### Data Flow

```
Input -> BrushService (area) -> ITool (action) -> ChunkSystem (chunks)
  -> PlantSystem/BuildingService (state change) -> CropRenderer (ComputeBuffer update)
```

R3 reactive for service communication:
```csharp
// Service exposes reactive state
public ReactiveProperty<int> Coins { get; }
public ReactiveEvent<HarvestData> OnHarvested { get; }

// Presenter subscribes
wallet.Coins.Subscribe(view.UpdateCoinsText).AddTo(disposables);
```

### Core Systems

| System | Purpose |
|--------|---------|
| **ChunkSystem** | Grid of chunks, territory expansion, building snap |
| **PlantSystem** | Growth over time, stages, watering/fertilizer modifiers |
| **BrushService** | Brush radius, tool application to sub-cells |
| **BuildingService** | Placement (grid-snap), moving, upgrading |
| **ProductionService** | Processing queues, timers, recipes |
| **TreePlacementService** | Free placement of trees/bushes, trunk displaces sub-cells, canopy renders above |
| **InventoryService** | Seeds, dyes, fertilizers, harvest. Slots, stacks, barn limit |
| **WalletService** | Single currency (coins). Add/Spend with reactive events |
| **ShopService** | Catalog, purchase, unlock by progression |
| **ContractService** | Optional contracts, progress tracking, rewards (new plant types) |
| **DayNightService** | 14-min cycle (dawn/day/dusk/night), lighting, speed/pause controls |

### Crop Rendering Pipeline

Crops use `Graphics.DrawMeshInstancedIndirect`:
- Two-level grid: chunks (gameplay) -> sub-cells (GPU instances)
- `MeshProperties` struct: position matrix, ground matrix, color, UV, cropState (160 bytes)
- `GetStructedBuffer.hlsl` provides `StructuredBuffer<MeshProperties>` for ShaderGraph
- `cropState.x` = plant type, `cropState.y` = growth progress (0-1)
- Per-chunk ComputeBuffers, re-uploaded on changes

### Plant Placement Model

- **Crops** -- brush-based, freeform. Fill sub-cells in radius.
- **Bushes** -- free placement (not grid-snapped), occupy space around them.
- **Trees** -- free placement, trunk displaces sub-cells, canopy renders above crops.
- **Buildings** -- grid-snap to chunks. Strict placement.

### Plant Types

3 categories:
- **Crops** (1 cell): Wheat, Corn, Tomato, Carrot, Sunflower... Fast growth, single harvest.
- **Bushes** (1 cell, visually larger): Rose, Berry, Lavender, Lilac... Medium growth, renewable harvest.
- **Trees** (3x3 trunk): Cherry, Apple, Oak, Spruce, Birch... Slow growth, renewable, long-term investment.

### Economy

Single currency: coins. Sources: sell harvest, contracts (x1.5-3), rare crops. Sinks: seeds, dyes, fertilizer, buildings, territory expansion.

Production chains (1-2 steps): Wheat->Flour, Tomato->Sauce, Flowers->Bouquet, Berries->Jam, Wood->Planks.

## Conventions (Target)

- **DI:** VContainer constructor injection. No static ServiceLocator.
- **UI:** MVP pattern. View (MonoBehaviour, visuals only) + Presenter (pure C#, IInitializable) + Service (business logic).
- **Reactive:** R3 ReactiveProperty for state, ReactiveEvent for events. No Update() polling for UI.
- **Async:** UniTask for all async operations. No coroutines.
- **Features:** Each system = IFeatureInstaller + separate asmdef.
- **Data:** PlantData ScriptableObject per plant type (data-driven, not enums).
- **Naming:** `Service` suffix for services, `View`/`Presenter` suffix for UI, `Installer` suffix for DI registration.

## Build Target

- **Steam PC** only. `PC_RPAsset.asset`, `PC_Renderer.asset`.

## VContainer Critical Notes

- **Parent scope references are NOT automatic.** VContainer does NOT detect parent-child from Unity's GameObject hierarchy. Each LifetimeScope needs `parentReference.TypeName` set in Inspector to the parent scope's fully-qualified type name.
  - RootScope: empty (it's the root, found via VContainerSettings)
  - GameScope: `WheatFarm.Infrastructure.RootScope`
  - FarmScope: `WheatFarm.Infrastructure.GameScope`
- **ITickable registration:** Use `.As<IMyService, ITickable>()` — VContainer's EntryPointDispatcher picks up all `ITickable` registrations automatically. No need for `RegisterEntryPoint` (which is just `.AsImplementedInterfaces()` + `EnsureDispatcherRegistered`, the latter already called by every LifetimeScope).
- **`RegisterInstance` registers as concrete type** — child scopes can resolve it via parent chain IF parentReference is correctly configured.

## Current State

Early development (v0.1.0). Legacy architecture (AllServices, GameStateMachine, WindowBase) being refactored to VContainer + R3 + MVP stack. GPU instanced crop rendering works. Phase 3 (chunk-based farming core) complete — all systems compile and run in Play Mode.

### Phase 3 Status: COMPLETE
- ChunkSystem, PlantSystem, BrushService, FarmRenderSystem, FarmBootstrap all registered in FarmScope
- PlantDatabase registered in GameScope (accessible to FarmScope via parent chain)
- FarmRenderConfig ScriptableObject created (needs CropMesh/CropMaterial assigned when assets are ready)
- 25 starter chunks (5x5 around origin) unlocked on start
- MeshProperties moved from legacy `Assembly-CSharp` to `WheatFarm.Core.Data` namespace
- Per-chunk GPU instanced indirect rendering ready (ChunkCropRenderer + FarmRenderSystem)
- Play Mode verified: zero errors, all scopes configure correctly
