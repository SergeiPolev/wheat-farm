# Wheat Farm Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Refactor wheat-farm from legacy AllServices/GameStateMachine to VContainer+R3+MVP stack, then build all game systems from the design doc.

**Architecture:** Hierarchical VContainer scopes (RootScope -> GameScope -> FarmScope). Feature installers per system. R3 reactive for communication. MVP for UI. GPU Instanced Indirect for crop rendering (existing, adapted to chunk-based).

**Tech Stack:** Unity 6 (6000.0.48f1), URP 17.0.4, VContainer, R3, UniTask, DOTween, Cinemachine 3.1.3

**Design doc:** `docs/plans/2026-03-04-wheat-farm-design.md`

---

## Phase 0: Package Installation & Project Setup

### Task 0.1: Install VContainer

**Files:**
- Modify: `Packages/manifest.json`

**Steps:**
1. Add VContainer to manifest.json via OpenUPM or git URL:
   ```json
   "jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.16.2"
   ```
2. Open Unity, wait for import. Verify no errors in Console.
3. Commit: `chore: add VContainer package`

### Task 0.2: Install R3

**Files:**
- Modify: `Packages/manifest.json`

**Steps:**
1. Add R3 + ObservableCollections via NuGetForUnity or git:
   ```json
   "com.cysharp.r3": "https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity"
   ```
2. Add `R3` and `ObservableCollections` NuGet dlls if needed (check R3 docs for Unity install).
3. Verify import. Add `CYSHARP_R3` to scripting define symbols if needed.
4. Commit: `chore: add R3 reactive library`

### Task 0.3: Install UniTask

**Files:**
- Modify: `Packages/manifest.json`

**Steps:**
1. Add UniTask:
   ```json
   "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"
   ```
2. Verify import.
3. Commit: `chore: add UniTask async library`

### Task 0.4: Create Assembly Definitions

**Files:**
- Create: `Assets/Scripts/Core/WheatFarm.Core.asmdef`
- Create: `Assets/Scripts/Infrastructure/WheatFarm.Infrastructure.asmdef`
- Create: `Assets/Scripts/Features/Farming/WheatFarm.Farming.asmdef`
- Create: `Assets/Scripts/Features/Buildings/WheatFarm.Buildings.asmdef`
- Create: `Assets/Scripts/Features/Economy/WheatFarm.Economy.asmdef`
- Create: `Assets/Scripts/Features/Inventory/WheatFarm.Inventory.asmdef`
- Create: `Assets/Scripts/Features/DayNight/WheatFarm.DayNight.asmdef`
- Create: `Assets/Scripts/UI/WheatFarm.UI.asmdef`
- Create: `Assets/Scripts/Player/WheatFarm.Player.asmdef`

**Steps:**
1. Create directory structure:
   ```
   Assets/Scripts/
     Core/           -- interfaces, base types, extensions, data structs
     Infrastructure/ -- VContainer scopes, bootstrap, save/load
     Features/
       Farming/      -- PlantSystem, ChunkSystem, CropRenderer, BrushService
       Buildings/    -- BuildingService, ProductionService
       Economy/      -- WalletService, ShopService, ContractService
       Inventory/    -- InventoryService
       DayNight/     -- DayNightService, LightingController
     UI/             -- Views, Presenters (MVP)
     Player/         -- movement, tools, ToolService
   ```
2. Create each `.asmdef` with appropriate references:
   - `WheatFarm.Core`: references VContainer, R3, UniTask, UnityEngine
   - `WheatFarm.Infrastructure`: references Core, VContainer, R3, UniTask
   - `WheatFarm.Farming`: references Core, Infrastructure, R3
   - `WheatFarm.Economy`: references Core, R3
   - `WheatFarm.UI`: references Core, Economy, Farming, R3, VContainer, DOTween, TextMeshPro
   - etc.
3. **Do NOT move existing files yet.** Just create the empty structure.
4. Commit: `chore: create assembly definitions and directory structure`

---

## Phase 1: VContainer Bootstrap (replace AllServices)

### Task 1.1: Create Core Interfaces

**Files:**
- Create: `Assets/Scripts/Core/IInitializable.cs`
- Create: `Assets/Scripts/Core/ITickable.cs`
- Create: `Assets/Scripts/Core/IDisposableService.cs`

**Steps:**
1. Create base interfaces:
   ```csharp
   // ITickable.cs
   namespace WheatFarm.Core
   {
       public interface ITickable
       {
           void Tick(float deltaTime);
       }

       public interface IFixedTickable
       {
           void FixedTick(float fixedDeltaTime);
       }
   }
   ```
2. Commit: `feat: add core interfaces`

### Task 1.2: Create RootScope

**Files:**
- Create: `Assets/Scripts/Infrastructure/Scopes/RootScope.cs`

**Steps:**
1. Create RootScope LifetimeScope:
   ```csharp
   using VContainer;
   using VContainer.Unity;
   using UnityEngine;

   namespace WheatFarm.Infrastructure
   {
       public class RootScope : LifetimeScope
       {
           protected override void Configure(IContainerBuilder builder)
           {
               DontDestroyOnLoad(gameObject);

               // Core services that live for entire app lifetime
               builder.Register<SaveService>(Lifetime.Singleton).AsImplementedInterfaces();
               builder.Register<TimeService>(Lifetime.Singleton).AsImplementedInterfaces();
           }
       }
   }
   ```
2. Create a GameObject in scene with RootScope component.
3. Commit: `feat: add RootScope VContainer bootstrap`

### Task 1.3: Create GameScope

**Files:**
- Create: `Assets/Scripts/Infrastructure/Scopes/GameScope.cs`

**Steps:**
1. Create GameScope as child of RootScope:
   ```csharp
   using VContainer;
   using VContainer.Unity;

   namespace WheatFarm.Infrastructure
   {
       public class GameScope : LifetimeScope
       {
           protected override void Configure(IContainerBuilder builder)
           {
               // Economy services
               builder.Register<WalletService>(Lifetime.Singleton).AsImplementedInterfaces();
               builder.Register<InventoryService>(Lifetime.Singleton).AsImplementedInterfaces();
               builder.Register<ShopService>(Lifetime.Singleton).AsImplementedInterfaces();
               builder.Register<ContractService>(Lifetime.Singleton).AsImplementedInterfaces();

               // Game services
               builder.Register<DayNightService>(Lifetime.Singleton).AsImplementedInterfaces();
               builder.Register<InputService>(Lifetime.Singleton).AsImplementedInterfaces();
               builder.Register<CameraService>(Lifetime.Singleton).AsImplementedInterfaces();
           }
       }
   }
   ```
2. Commit: `feat: add GameScope VContainer`

### Task 1.4: Create FarmScope

**Files:**
- Create: `Assets/Scripts/Infrastructure/Scopes/FarmScope.cs`

**Steps:**
1. Create FarmScope as child of GameScope:
   ```csharp
   using VContainer;
   using VContainer.Unity;

   namespace WheatFarm.Infrastructure
   {
       public class FarmScope : LifetimeScope
       {
           protected override void Configure(IContainerBuilder builder)
           {
               // Farming systems
               builder.Register<ChunkSystem>(Lifetime.Singleton).AsImplementedInterfaces();
               builder.Register<PlantSystem>(Lifetime.Singleton).AsImplementedInterfaces();
               builder.Register<BrushService>(Lifetime.Singleton).AsImplementedInterfaces();
               builder.Register<BuildingService>(Lifetime.Singleton).AsImplementedInterfaces();
               builder.Register<ProductionService>(Lifetime.Singleton).AsImplementedInterfaces();
               builder.Register<TreePlacementService>(Lifetime.Singleton).AsImplementedInterfaces();
               builder.Register<ToolService>(Lifetime.Singleton).AsImplementedInterfaces();
           }
       }
   }
   ```
2. Commit: `feat: add FarmScope VContainer`

### Task 1.5: Create TickRunner (replaces GameStateMachine tick routing)

**Files:**
- Create: `Assets/Scripts/Infrastructure/TickRunner.cs`

**Steps:**
1. Create TickRunner that collects all ITickable from VContainer and ticks them:
   ```csharp
   using System.Collections.Generic;
   using VContainer.Unity;
   using WheatFarm.Core;

   namespace WheatFarm.Infrastructure
   {
       public class TickRunner : ITickable, IFixedTickable
       {
           private readonly IEnumerable<Core.ITickable> _tickables;
           private readonly IEnumerable<Core.IFixedTickable> _fixedTickables;

           public TickRunner(
               IEnumerable<Core.ITickable> tickables,
               IEnumerable<Core.IFixedTickable> fixedTickables)
           {
               _tickables = tickables;
               _fixedTickables = fixedTickables;
           }

           void ITickable.Tick()
           {
               var dt = UnityEngine.Time.deltaTime;
               foreach (var t in _tickables) t.Tick(dt);
           }

           void IFixedTickable.FixedTick()
           {
               var dt = UnityEngine.Time.fixedDeltaTime;
               foreach (var t in _fixedTickables) t.FixedTick(dt);
           }
       }
   }
   ```
2. Register in FarmScope: `builder.RegisterEntryPoint<TickRunner>();`
3. Commit: `feat: add TickRunner replacing GameStateMachine tick routing`

### Task 1.6: Verify Bootstrap Works

**Steps:**
1. Remove or disable `Game.cs` entry point (comment out, don't delete yet).
2. Set up scene: RootScope GameObject -> GameScope child -> FarmScope child.
3. Enter Play Mode. Verify no errors. VContainer scopes create and resolve.
4. Commit: `feat: VContainer bootstrap verified, legacy Game.cs disabled`

---

## Phase 2: Data Layer (PlantData, BuildingData)

### Task 2.1: Create PlantData ScriptableObject

**Files:**
- Create: `Assets/Scripts/Core/Data/PlantData.cs`
- Create: `Assets/Scripts/Core/Data/PlantCategory.cs`

**Steps:**
1. Define data-driven plant types:
   ```csharp
   using UnityEngine;

   namespace WheatFarm.Core.Data
   {
       public enum PlantCategory { Crop, Bush, Tree }

       [CreateAssetMenu(fileName = "Plant_", menuName = "WheatFarm/PlantData")]
       public class PlantData : ScriptableObject
       {
           [Header("Identity")]
           public string PlantId;
           public string DisplayName;
           public PlantCategory Category;

           [Header("Visuals")]
           public Mesh Mesh;
           public Material Material;
           public Vector2 ScaleRange = new(0.8f, 1.2f);

           [Header("Gameplay")]
           public float GrowthDuration = 60f; // seconds at normal speed
           public int SellPrice = 10;
           public int SeedCost = 5;
           public bool RenewableHarvest = false; // bushes and trees
           public Vector2Int TrunkSize = Vector2Int.one; // 1x1 for crops, 3x3 for trees

           [Header("Unlock")]
           public bool UnlockedByDefault = false;
       }
   }
   ```
2. Create: `Assets/Scripts/Core/Data/PlantDatabase.cs`
   ```csharp
   using UnityEngine;

   namespace WheatFarm.Core.Data
   {
       [CreateAssetMenu(menuName = "WheatFarm/PlantDatabase")]
       public class PlantDatabase : ScriptableObject
       {
           public PlantData[] Plants;

           public PlantData GetById(string id)
           {
               foreach (var p in Plants)
                   if (p.PlantId == id) return p;
               return null;
           }
       }
   }
   ```
3. In Unity Editor: create PlantData assets for starter plants (Wheat, Grass, Carrot, Rose, Apple).
4. Create PlantDatabase asset referencing all PlantData.
5. Commit: `feat: add PlantData and PlantDatabase ScriptableObjects`

### Task 2.2: Create BuildingData ScriptableObject

**Files:**
- Create: `Assets/Scripts/Core/Data/BuildingData.cs`
- Create: `Assets/Scripts/Core/Data/BuildingDatabase.cs`
- Create: `Assets/Scripts/Core/Data/RecipeData.cs`

**Steps:**
1. Define building data:
   ```csharp
   using UnityEngine;

   namespace WheatFarm.Core.Data
   {
       public enum BuildingType { Functional, Decorative }

       [CreateAssetMenu(menuName = "WheatFarm/BuildingData")]
       public class BuildingData : ScriptableObject
       {
           public string BuildingId;
           public string DisplayName;
           public BuildingType Type;
           public Vector2Int GridSize = new(2, 2); // in chunks
           public GameObject Prefab;
           public int Cost;
           public int PlanksRequired;
           public int MaxLevel = 3;
           public RecipeData[] Recipes; // null for decorative
           public bool StarterBuilding;
       }
   }
   ```
2. Define recipes:
   ```csharp
   using UnityEngine;

   namespace WheatFarm.Core.Data
   {
       [CreateAssetMenu(menuName = "WheatFarm/RecipeData")]
       public class RecipeData : ScriptableObject
       {
           public string RecipeId;
           public string DisplayName;
           public ItemStack[] Inputs;
           public ItemStack Output;
           public float ProcessingTime = 60f; // seconds
       }

       [System.Serializable]
       public struct ItemStack
       {
           public string ItemId;
           public int Amount;
       }
   }
   ```
3. Commit: `feat: add BuildingData, RecipeData ScriptableObjects`

### Task 2.3: Create DyeData

**Files:**
- Create: `Assets/Scripts/Core/Data/DyeData.cs`

**Steps:**
1. Define dye data:
   ```csharp
   using UnityEngine;

   namespace WheatFarm.Core.Data
   {
       [CreateAssetMenu(menuName = "WheatFarm/DyeData")]
       public class DyeData : ScriptableObject
       {
           public string DyeId;
           public string DisplayName;
           public Color Color;
           public int Cost;
           public bool RequiresCrafting;
           public string[] CraftIngredientIds; // e.g., ["dye_red", "dye_blue"] -> purple
       }
   }
   ```
2. Commit: `feat: add DyeData ScriptableObject`

---

## Phase 3: Chunk-Based Farming Core

### Task 3.1: Create ChunkData and ChunkSystem

**Files:**
- Create: `Assets/Scripts/Features/Farming/ChunkData.cs`
- Create: `Assets/Scripts/Features/Farming/ChunkSystem.cs`

**Steps:**
1. Define chunk data:
   ```csharp
   using UnityEngine;
   using WheatFarm.Core.Data;

   namespace WheatFarm.Farming
   {
       public class ChunkData
       {
           public Vector2Int ChunkCoord;
           public bool Unlocked;
           public MeshProperties[] SubCells;
           public int SubCellResolution; // e.g., 4 = 4x4 sub-cells per chunk

           public int SubCellCount => SubCellResolution * SubCellResolution;
       }
   }
   ```
2. Create ChunkSystem managing a dictionary of chunks:
   ```csharp
   using System.Collections.Generic;
   using R3;
   using UnityEngine;

   namespace WheatFarm.Farming
   {
       public interface IChunkSystem
       {
           ReactiveProperty<int> UnlockedChunkCount { get; }
           ChunkData GetChunk(Vector2Int coord);
           List<ChunkData> GetChunksInRadius(Vector3 worldPos, float radius);
           bool TryUnlockChunk(Vector2Int coord);
           Vector2Int WorldToChunkCoord(Vector3 worldPos);
           (Vector2Int chunkCoord, Vector2Int subCell) WorldToSubCell(Vector3 worldPos);
       }

       public class ChunkSystem : IChunkSystem
       {
           private readonly Dictionary<Vector2Int, ChunkData> _chunks = new();
           private readonly float _chunkWorldSize;
           private readonly int _subCellResolution;

           public ReactiveProperty<int> UnlockedChunkCount { get; } = new(0);

           public ChunkSystem(float chunkWorldSize = 4f, int subCellResolution = 4)
           {
               _chunkWorldSize = chunkWorldSize;
               _subCellResolution = subCellResolution;
           }

           // Implementation: create starter chunks, coordinate math, etc.
       }
   }
   ```
3. Commit: `feat: add ChunkSystem with chunk-based grid`

### Task 3.2: Refactor CropRenderer to Per-Chunk

**Files:**
- Modify: `Assets/Project/Scripts/Crops/CropRenderer.cs` -> move to `Assets/Scripts/Features/Farming/ChunkCropRenderer.cs`
- Modify: `Assets/Project/Scripts/Crops/MeshProperties.cs` -> move to `Assets/Scripts/Core/Data/MeshProperties.cs`

**Steps:**
1. Rename CropRenderer -> ChunkCropRenderer. Each chunk gets its own instance.
2. ChunkCropRenderer takes a ChunkData reference instead of CropFieldData.
3. Owns its own ComputeBuffer sized to ChunkData.SubCellCount.
4. OnChangedData() re-uploads only this chunk's buffer.
5. Keep existing shader pipeline (GetStructedBuffer.hlsl) unchanged.
6. Commit: `refactor: chunk-based CropRenderer with per-chunk ComputeBuffers`

### Task 3.3: Create PlantSystem

**Files:**
- Create: `Assets/Scripts/Features/Farming/PlantSystem.cs`

**Steps:**
1. Create PlantSystem managing growth:
   ```csharp
   using R3;
   using WheatFarm.Core;
   using WheatFarm.Core.Data;

   namespace WheatFarm.Farming
   {
       public interface IPlantSystem
       {
           ReactiveEvent<HarvestData> OnHarvested { get; }
           void Plant(Vector2Int chunkCoord, Vector2Int subCell, PlantData data);
           void Water(Vector2Int chunkCoord, Vector2Int subCell);
           void Fertilize(Vector2Int chunkCoord, Vector2Int subCell, float multiplier);
           HarvestData Harvest(Vector2Int chunkCoord, Vector2Int subCell);
           void Uproot(Vector2Int chunkCoord, Vector2Int subCell);
       }

       public struct HarvestData
       {
           public PlantData PlantData;
           public int Yield;
           public Vector3 WorldPosition;
       }

       public class PlantSystem : IPlantSystem, ITickable
       {
           private readonly IChunkSystem _chunkSystem;

           public ReactiveEvent<HarvestData> OnHarvested { get; } = new();

           public PlantSystem(IChunkSystem chunkSystem)
           {
               _chunkSystem = chunkSystem;
           }

           public void Tick(float deltaTime)
           {
               // Iterate chunks, grow watered plants by deltaTime / growthDuration
               // Update cropState.y in MeshProperties
           }

           // Plant, Water, Fertilize, Harvest, Uproot implementations
           // All modify MeshProperties in the chunk's SubCells array
           // Then notify chunk's renderer to re-upload ComputeBuffer
       }
   }
   ```
2. Commit: `feat: add PlantSystem with growth, watering, harvesting`

### Task 3.4: Create BrushService

**Files:**
- Create: `Assets/Scripts/Features/Farming/BrushService.cs`

**Steps:**
1. Create BrushService handling radius-based tool application:
   ```csharp
   using R3;
   using UnityEngine;
   using WheatFarm.Core;

   namespace WheatFarm.Farming
   {
       public enum BrushSize { Small = 1, Medium = 2, Large = 3 }

       public interface IBrushService
       {
           ReactiveProperty<BrushSize> CurrentSize { get; }
           void ApplyAtWorldPos(Vector3 worldPos, IBrushAction action);
       }

       public interface IBrushAction
       {
           void Apply(Vector2Int chunkCoord, Vector2Int subCell);
       }

       public class BrushService : IBrushService
       {
           private readonly IChunkSystem _chunkSystem;
           public ReactiveProperty<BrushSize> CurrentSize { get; } = new(BrushSize.Medium);

           public BrushService(IChunkSystem chunkSystem)
           {
               _chunkSystem = chunkSystem;
           }

           public void ApplyAtWorldPos(Vector3 worldPos, IBrushAction action)
           {
               float radius = (int)CurrentSize.Value;
               // Convert world position to sub-cell coordinates
               // Iterate sub-cells in radius
               // Call action.Apply() for each valid sub-cell
               // Similar to current FieldToolsService logic
           }
       }
   }
   ```
2. Commit: `feat: add BrushService for radius-based tool application`

---

## Phase 4: Tools & Player

### Task 4.1: Create ITool Interface and ToolService

**Files:**
- Create: `Assets/Scripts/Player/Tools/ITool.cs`
- Create: `Assets/Scripts/Player/Tools/ToolService.cs`

**Steps:**
1. Define clean tool interface:
   ```csharp
   using R3;
   using UnityEngine;

   namespace WheatFarm.Player.Tools
   {
       public enum ToolId { Planter, WateringCan, Fertilizer, Dye, Sickle, Uproot, Build, PlaceTree }

       public interface ITool
       {
           ToolId Id { get; }
           bool RequiresResource { get; }
           void OnEquip();
           void OnUnequip();
           void UseAtPosition(Vector3 worldPos);
       }

       public interface IToolService
       {
           ReactiveProperty<ITool> CurrentTool { get; }
           void EquipTool(ToolId id);
       }

       public class ToolService : IToolService
       {
           private readonly Dictionary<ToolId, ITool> _tools;
           public ReactiveProperty<ITool> CurrentTool { get; } = new();

           public ToolService(/* inject all ITool implementations */) { }
           public void EquipTool(ToolId id) { /* switch tool */ }
       }
   }
   ```
2. Commit: `feat: add ITool interface and ToolService`

### Task 4.2: Implement Individual Tools

**Files:**
- Create: `Assets/Scripts/Player/Tools/PlanterTool.cs`
- Create: `Assets/Scripts/Player/Tools/WateringCanTool.cs`
- Create: `Assets/Scripts/Player/Tools/SickleTool.cs`
- Create: `Assets/Scripts/Player/Tools/DyeTool.cs`
- Create: `Assets/Scripts/Player/Tools/FertilizerTool.cs`
- Create: `Assets/Scripts/Player/Tools/UprootTool.cs`

**Steps:**
1. Each tool implements ITool and IBrushAction:
   ```csharp
   namespace WheatFarm.Player.Tools
   {
       public class WateringCanTool : ITool, IBrushAction
       {
           private readonly IPlantSystem _plantSystem;
           private readonly IBrushService _brush;

           public ToolId Id => ToolId.WateringCan;
           public bool RequiresResource => true; // water from well

           public WateringCanTool(IPlantSystem plantSystem, IBrushService brush)
           {
               _plantSystem = plantSystem;
               _brush = brush;
           }

           public void UseAtPosition(Vector3 worldPos)
           {
               _brush.ApplyAtWorldPos(worldPos, this);
           }

           public void Apply(Vector2Int chunkCoord, Vector2Int subCell)
           {
               _plantSystem.Water(chunkCoord, subCell);
           }
       }
   }
   ```
2. Each tool follows same pattern: inject dependencies, delegate to BrushService.
3. Commit: `feat: implement all farming tools`

### Task 4.3: Refactor Player Movement

**Files:**
- Modify: `Assets/Project/Scripts/Player.cs` -> move to `Assets/Scripts/Player/PlayerController.cs`

**Steps:**
1. Keep existing Rigidbody movement logic.
2. Inject InputService via VContainer instead of static access.
3. Add cursor radius limit: `MaxToolRange = 5f`, tools only work within this distance from player.
4. Keep `_Interaction_Position` shader global update.
5. Commit: `refactor: PlayerController with VContainer injection and cursor radius`

---

## Phase 5: Economy

### Task 5.1: Create WalletService

**Files:**
- Create: `Assets/Scripts/Features/Economy/WalletService.cs`

**Steps:**
1. Single currency wallet with R3:
   ```csharp
   using R3;

   namespace WheatFarm.Economy
   {
       public interface IWalletService
       {
           ReadOnlyReactiveProperty<int> Coins { get; }
           bool CanAfford(int amount);
           void Add(int amount);
           bool TrySpend(int amount);
       }

       public class WalletService : IWalletService
       {
           private readonly ReactiveProperty<int> _coins = new(100); // starter money
           public ReadOnlyReactiveProperty<int> Coins => _coins;

           public bool CanAfford(int amount) => _coins.Value >= amount;
           public void Add(int amount) => _coins.Value += amount;
           public bool TrySpend(int amount)
           {
               if (!CanAfford(amount)) return false;
               _coins.Value -= amount;
               return true;
           }
       }
   }
   ```
2. Commit: `feat: add WalletService with reactive coins`

### Task 5.2: Create InventoryService

**Files:**
- Create: `Assets/Scripts/Features/Inventory/InventoryService.cs`
- Create: `Assets/Scripts/Core/Data/InventoryItem.cs`

**Steps:**
1. Define inventory item:
   ```csharp
   namespace WheatFarm.Core.Data
   {
       public enum ItemType { Seed, Dye, Fertilizer, Harvest, Product, Plank, Sapling }

       [System.Serializable]
       public struct InventoryItem
       {
           public string ItemId;
           public ItemType Type;
           public int Amount;
       }
   }
   ```
2. Create InventoryService with ObservableList:
   ```csharp
   using ObservableCollections;
   using R3;

   namespace WheatFarm.Inventory
   {
       public interface IInventoryService
       {
           ObservableList<InventoryItem> Items { get; }
           ReactiveProperty<int> Capacity { get; } // barn limit
           bool HasItem(string itemId, int amount = 1);
           bool TryConsume(string itemId, int amount = 1);
           bool TryAdd(InventoryItem item);
       }

       public class InventoryService : IInventoryService
       {
           public ObservableList<InventoryItem> Items { get; } = new();
           public ReactiveProperty<int> Capacity { get; } = new(20);
           // Implementation: stack items, check capacity, etc.
       }
   }
   ```
3. Commit: `feat: add InventoryService with observable items`

### Task 5.3: Create ShopService

**Files:**
- Create: `Assets/Scripts/Features/Economy/ShopService.cs`

**Steps:**
1. Create ShopService reading from PlantDatabase and DyeData:
   ```csharp
   namespace WheatFarm.Economy
   {
       public interface IShopService
       {
           bool TryBuy(string itemId, int amount = 1);
           bool IsUnlocked(string itemId);
       }

       public class ShopService : IShopService
       {
           private readonly IWalletService _wallet;
           private readonly IInventoryService _inventory;
           // Inject databases for price lookups

           public bool TryBuy(string itemId, int amount)
           {
               // Look up price, check wallet, add to inventory
           }
       }
   }
   ```
2. Commit: `feat: add ShopService`

### Task 5.4: Create ContractService

**Files:**
- Create: `Assets/Scripts/Features/Economy/ContractService.cs`
- Create: `Assets/Scripts/Core/Data/ContractData.cs`

**Steps:**
1. Define contract:
   ```csharp
   namespace WheatFarm.Core.Data
   {
       [System.Serializable]
       public class ContractData
       {
           public string ContractId;
           public string Description;
           public ItemStack[] Required;
           public int CoinReward;
           public string UnlockPlantId; // nullable, reward new plant
       }
   }
   ```
2. Create ContractService:
   ```csharp
   using ObservableCollections;
   using R3;

   namespace WheatFarm.Economy
   {
       public interface IContractService
       {
           ObservableList<ActiveContract> ActiveContracts { get; }
           ObservableList<ContractData> AvailableContracts { get; }
           void AcceptContract(string contractId);
           void TryCompleteContract(string contractId);
       }

       public struct ActiveContract
       {
           public ContractData Data;
           public int[] Progress; // per required item
       }
   }
   ```
3. Commit: `feat: add ContractService with optional contracts`

---

## Phase 6: Buildings & Production

### Task 6.1: Create BuildingService

**Files:**
- Create: `Assets/Scripts/Features/Buildings/BuildingService.cs`

**Steps:**
1. Create BuildingService for grid-snap placement:
   ```csharp
   using System.Collections.Generic;
   using R3;
   using UnityEngine;

   namespace WheatFarm.Buildings
   {
       public interface IBuildingService
       {
           ObservableCollections.ObservableList<PlacedBuilding> Buildings { get; }
           bool CanPlace(BuildingData data, Vector2Int chunkCoord);
           PlacedBuilding Place(BuildingData data, Vector2Int chunkCoord);
           void Move(PlacedBuilding building, Vector2Int newCoord);
           void Remove(PlacedBuilding building);
           void Upgrade(PlacedBuilding building);
       }

       public class PlacedBuilding
       {
           public BuildingData Data;
           public Vector2Int ChunkCoord;
           public int Level = 1;
           public GameObject Instance;
       }
   }
   ```
2. Placement checks: chunk unlocked, no overlap, enough money/planks.
3. Commit: `feat: add BuildingService with grid-snap placement`

### Task 6.2: Create ProductionService

**Files:**
- Create: `Assets/Scripts/Features/Buildings/ProductionService.cs`

**Steps:**
1. Create ProductionService managing processing queues:
   ```csharp
   using System.Collections.Generic;
   using R3;
   using WheatFarm.Core;

   namespace WheatFarm.Buildings
   {
       public interface IProductionService
       {
           bool TryStartProduction(PlacedBuilding building, RecipeData recipe);
           List<ProductionSlot> GetSlots(PlacedBuilding building);
       }

       public class ProductionSlot
       {
           public RecipeData Recipe;
           public float TimeRemaining;
           public ReactiveProperty<float> Progress { get; } = new(0f); // 0-1
           public bool IsComplete => TimeRemaining <= 0;
       }

       public class ProductionService : IProductionService, ITickable
       {
           private readonly Dictionary<PlacedBuilding, List<ProductionSlot>> _activeProduction = new();

           public void Tick(float deltaTime)
           {
               // Advance all active production timers
               // When complete, add product to inventory
           }
       }
   }
   ```
2. Commit: `feat: add ProductionService with processing queues`

---

## Phase 7: Tree & Bush Placement

### Task 7.1: Create TreePlacementService

**Files:**
- Create: `Assets/Scripts/Features/Farming/TreePlacementService.cs`

**Steps:**
1. Handle free placement + trunk displacement:
   ```csharp
   using UnityEngine;
   using WheatFarm.Core.Data;

   namespace WheatFarm.Farming
   {
       public interface ITreePlacementService
       {
           bool CanPlace(PlantData treeData, Vector3 worldPos);
           void Place(PlantData treeData, Vector3 worldPos);
           void Remove(Vector3 worldPos);
       }

       public class TreePlacementService : ITreePlacementService
       {
           private readonly IChunkSystem _chunkSystem;
           private readonly IPlantSystem _plantSystem;

           public void Place(PlantData treeData, Vector3 worldPos)
           {
               // 1. Calculate trunk sub-cells (TrunkSize, centered on worldPos)
               // 2. Clear sub-cells under trunk (set cropState.x = 0)
               // 3. Mark those sub-cells as occupied (prevent planting)
               // 4. Instantiate tree prefab at worldPos (trunk + canopy)
               // 5. Canopy renders on separate layer above crop instances
           }
       }
   }
   ```
2. Commit: `feat: add TreePlacementService with trunk displacement`

---

## Phase 8: Day/Night Cycle

### Task 8.1: Create DayNightService

**Files:**
- Create: `Assets/Scripts/Features/DayNight/DayNightService.cs`
- Create: `Assets/Scripts/Features/DayNight/LightingController.cs`

**Steps:**
1. Create DayNightService:
   ```csharp
   using R3;
   using WheatFarm.Core;

   namespace WheatFarm.DayNight
   {
       public enum TimeOfDay { Dawn, Day, Dusk, Night }

       public interface IDayNightService
       {
           ReadOnlyReactiveProperty<float> TimeNormalized { get; } // 0-1 over full cycle
           ReadOnlyReactiveProperty<TimeOfDay> CurrentPhase { get; }
           ReactiveProperty<float> TimeScale { get; } // 1, 2, 4, or 0 (pause)
       }

       public class DayNightService : IDayNightService, ITickable
       {
           private const float CycleDuration = 14f * 60f; // 14 minutes
           private readonly ReactiveProperty<float> _time = new(0.25f); // start at day
           private readonly ReactiveProperty<TimeOfDay> _phase = new(TimeOfDay.Day);

           public ReadOnlyReactiveProperty<float> TimeNormalized => _time;
           public ReadOnlyReactiveProperty<TimeOfDay> CurrentPhase => _phase;
           public ReactiveProperty<float> TimeScale { get; } = new(1f);

           public void Tick(float deltaTime)
           {
               _time.Value = (_time.Value + deltaTime * TimeScale.Value / CycleDuration) % 1f;
               _phase.Value = _time.Value switch
               {
                   < 0.14f => TimeOfDay.Dawn,   // ~2 min
                   < 0.57f => TimeOfDay.Day,     // ~6 min
                   < 0.71f => TimeOfDay.Dusk,    // ~2 min
                   _ => TimeOfDay.Night           // ~4 min
               };
           }
       }
   }
   ```
2. Create LightingController subscribing to DayNightService:
   ```csharp
   namespace WheatFarm.DayNight
   {
       public class LightingController : IInitializable, IDisposable
       {
           // Subscribe to TimeNormalized
           // Rotate directional light
           // Lerp color temperature (warm dawn -> neutral day -> orange dusk -> blue night)
           // Toggle lamp objects at Night phase
       }
   }
   ```
3. Commit: `feat: add DayNightService and LightingController`

---

## Phase 9: UI (MVP)

### Task 9.1: Create HUD

**Files:**
- Create: `Assets/Scripts/UI/HUD/HUDView.cs`
- Create: `Assets/Scripts/UI/HUD/HUDPresenter.cs`

**Steps:**
1. HUDView (MonoBehaviour, only visual):
   ```csharp
   using TMPro;
   using UnityEngine;
   using UnityEngine.UI;

   namespace WheatFarm.UI
   {
       public class HUDView : MonoBehaviour
       {
           [SerializeField] private TextMeshProUGUI _coinsText;
           [SerializeField] private Image[] _toolIcons;
           [SerializeField] private TextMeshProUGUI _timeText;

           public void UpdateCoins(int amount) => _coinsText.text = amount.ToString();
           public void HighlightTool(int index) { /* highlight active tool icon */ }
           public void UpdateTime(string text) => _timeText.text = text;
       }
   }
   ```
2. HUDPresenter (pure C#, reactive subscriptions):
   ```csharp
   using System;
   using R3;
   using VContainer.Unity;

   namespace WheatFarm.UI
   {
       public class HUDPresenter : IInitializable, IDisposable
       {
           private readonly HUDView _view;
           private readonly IWalletService _wallet;
           private readonly IToolService _toolService;
           private readonly IDayNightService _dayNight;
           private readonly CompositeDisposable _disposables = new();

           public HUDPresenter(HUDView view, IWalletService wallet,
               IToolService toolService, IDayNightService dayNight)
           {
               _view = view;
               _wallet = wallet;
               _toolService = toolService;
               _dayNight = dayNight;
           }

           public void Initialize()
           {
               _wallet.Coins.Subscribe(c => _view.UpdateCoins(c)).AddTo(_disposables);
               _toolService.CurrentTool.Subscribe(t => _view.HighlightTool((int)t.Id)).AddTo(_disposables);
           }

           public void Dispose() => _disposables.Dispose();
       }
   }
   ```
3. Register in GameScope: `builder.RegisterComponentInHierarchy<HUDView>(); builder.RegisterEntryPoint<HUDPresenter>();`
4. Commit: `feat: add HUD with MVP pattern`

### Task 9.2: Create Shop Window

**Files:**
- Create: `Assets/Scripts/UI/Shop/ShopView.cs`
- Create: `Assets/Scripts/UI/Shop/ShopPresenter.cs`

**Steps:**
1. ShopView: displays catalog, buy buttons. ShopPresenter: reads from ShopService, handles buy clicks.
2. Same MVP pattern as HUD.
3. Commit: `feat: add Shop UI with MVP`

### Task 9.3: Create Inventory Window

**Files:**
- Create: `Assets/Scripts/UI/Inventory/InventoryView.cs`
- Create: `Assets/Scripts/UI/Inventory/InventoryPresenter.cs`

**Steps:**
1. InventoryView: grid of item slots. InventoryPresenter: subscribes to ObservableList<InventoryItem>.
2. Commit: `feat: add Inventory UI with MVP`

### Task 9.4: Create Contract Board Window

**Files:**
- Create: `Assets/Scripts/UI/Contracts/ContractBoardView.cs`
- Create: `Assets/Scripts/UI/Contracts/ContractBoardPresenter.cs`

**Steps:**
1. Shows available/active contracts. Accept/complete buttons.
2. Commit: `feat: add Contract Board UI with MVP`

---

## Phase 10: Save/Load

### Task 10.1: Create FarmSaveData

**Files:**
- Create: `Assets/Scripts/Infrastructure/Save/FarmSaveData.cs`

**Steps:**
1. Define serializable farm state:
   ```csharp
   using System.Collections.Generic;
   using UnityEngine;

   namespace WheatFarm.Infrastructure.Save
   {
       [System.Serializable]
       public class FarmSaveData
       {
           public int Coins;
           public List<ChunkSaveData> Chunks;
           public List<PlacedBuildingSaveData> Buildings;
           public List<TreeSaveData> Trees;
           public List<InventoryItemSaveData> Inventory;
           public List<string> UnlockedPlants;
           public float DayNightTime;
       }

       [System.Serializable]
       public struct ChunkSaveData
       {
           public Vector2Int Coord;
           public bool Unlocked;
           public SubCellSaveData[] SubCells;
       }

       [System.Serializable]
       public struct SubCellSaveData
       {
           public string PlantId; // empty = no plant
           public float Growth;   // 0-1
           public Color32 Color;
           public bool Watered;
       }

       [System.Serializable]
       public struct PlacedBuildingSaveData
       {
           public string BuildingId;
           public Vector2Int ChunkCoord;
           public int Level;
       }

       [System.Serializable]
       public struct TreeSaveData
       {
           public string PlantId;
           public Vector3 Position;
           public float Growth;
           public Color32 Color;
       }

       [System.Serializable]
       public struct InventoryItemSaveData
       {
           public string ItemId;
           public int Amount;
       }
   }
   ```
2. Commit: `feat: add FarmSaveData serializable structures`

### Task 10.2: Create SaveService

**Files:**
- Create: `Assets/Scripts/Infrastructure/Save/FarmSaveService.cs`

**Steps:**
1. JSON serialization to file (Steam PC, not PlayerPrefs):
   ```csharp
   using UnityEngine;
   using Cysharp.Threading.Tasks;

   namespace WheatFarm.Infrastructure.Save
   {
       public interface IFarmSaveService
       {
           UniTask Save(FarmSaveData data);
           UniTask<FarmSaveData> Load();
           bool HasSave();
       }

       public class FarmSaveService : IFarmSaveService
       {
           private string SavePath => System.IO.Path.Combine(
               Application.persistentDataPath, "farm_save.json");

           public async UniTask Save(FarmSaveData data)
           {
               var json = JsonUtility.ToJson(data, true);
               await System.IO.File.WriteAllTextAsync(SavePath, json);
           }

           public async UniTask<FarmSaveData> Load()
           {
               var json = await System.IO.File.ReadAllTextAsync(SavePath);
               return JsonUtility.FromJson<FarmSaveData>(json);
           }

           public bool HasSave() => System.IO.File.Exists(SavePath);
       }
   }
   ```
2. Commit: `feat: add FarmSaveService with JSON file persistence`

---

## Phase 11: Integration & Cleanup

### Task 11.1: Wire Everything Together

**Steps:**
1. Verify all scopes register correct services.
2. FarmScope creates starter chunks, places starter buildings.
3. PlantSystem ticks growth. BrushService applies tools. CropRenderers update.
4. Economy flows: harvest -> sell -> buy -> plant.
5. Play test full loop.
6. Commit: `feat: integrate all systems, full gameplay loop working`

### Task 11.2: Remove Legacy Code

**Steps:**
1. Delete or archive:
   - `AllServices.cs`
   - `GameStateMachine.cs` and all states
   - `Game.cs`
   - `FieldToolsService.cs`
   - `ChangeToolsService.cs`
   - `GetCropPointsService.cs`
   - `CropPainter.cs`
   - Old `Tool.cs`, `ToolHandler.cs`
   - Old `WalletService.cs`, `GameWalletService.cs`
   - Old `WindowBase.cs` UI system
   - Google Sheet services (not needed for now)
   - Combat text, rarity system, layer manager (template leftovers)
2. Commit: `chore: remove legacy AllServices/GameStateMachine architecture`

### Task 11.3: Update Shaders for Growth Stages

**Files:**
- Modify: `Assets/Project/Shaders/Grass Instanced.shadergraph`

**Steps:**
1. In ShaderGraph, use `cropState.y` (0-1) to:
   - Scale instance: lerp from 0.2 to 1.0 based on growth
   - Color intensity: lerp from pale/desaturated to full color
   - Combine with `color` property for dye tint
2. Commit: `feat: shader growth stages based on cropState.y`

---

## Execution Order Summary

```
Phase 0:  Packages + asmdef structure           (~1 session)
Phase 1:  VContainer bootstrap                  (~1 session)
Phase 2:  Data layer (ScriptableObjects)         (~1 session)
Phase 3:  Chunk-based farming core               (~2-3 sessions)
Phase 4:  Tools & player                         (~1-2 sessions)
Phase 5:  Economy                                (~1-2 sessions)
Phase 6:  Buildings & production                 (~1-2 sessions)
Phase 7:  Trees & bushes                         (~1 session)
Phase 8:  Day/night                              (~1 session)
Phase 9:  UI (MVP)                               (~2-3 sessions)
Phase 10: Save/load                              (~1 session)
Phase 11: Integration & cleanup                  (~1-2 sessions)
```

**Each phase delivers a working increment.** After Phase 5, the core loop (plant -> grow -> harvest -> sell -> buy) works. Remaining phases add depth.
