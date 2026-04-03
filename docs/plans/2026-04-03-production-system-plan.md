# Production System Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Activate the full production chain (5 buildings, auto-repeat recipes) and remove legacy BuildingService. Buildings placed via PlacementService interact through a new Building Panel (MVP) to start/stop production.

**Architecture:** ProductionService rewritten to use PlacedObject/PlaceableData. BuildingMarker stores PlacedObject instead of PlacedBuilding. New BuildingPanelView + BuildingPanelPresenter (MVP). Legacy BuildingService, BuildingData, PlacedBuilding removed.

**Tech Stack:** Unity 6, VContainer, R3, UniTask

**Design doc:** `docs/plans/2026-04-03-production-system-design.md`

---

## Phase A: Data Layer — New Assets & PlantData Wood Yield

### Task A1: Add UprootYield fields to PlantData

**Files:**
- Modify: `Assets/Scripts/Core/Data/PlantData.cs`

**Steps:**
1. Add two new fields to PlantData:
   ```csharp
   [Header("Uproot")]
   public string UprootYieldId;      // e.g. "wood". Empty = no yield on uproot.
   public int UprootYieldAmount;     // e.g. 3
   ```
   Place after the existing `TrunkSize` field.

2. Commit: `feat(A1): add UprootYield fields to PlantData`

### Task A2: Create new RecipeData assets

**Files:**
- Create: `Assets/Settings/Recipes/Recipe_TomatoToSauce.asset`
- Create: `Assets/Settings/Recipes/Recipe_CherryToJam.asset`
- Create: `Assets/Settings/Recipes/Recipe_RoseToBouquet.asset`
- Create: `Assets/Settings/Recipes/Recipe_WoodToPlanks.asset`

**Steps:**
1. In Unity Editor, create a `Recipes` folder under `Assets/Settings/` if it doesn't exist.
2. Move existing `Recipe_WheatToFlour.asset` and `Recipe_FlourToBread.asset` into this folder.
3. Create each RecipeData asset via Create > WheatFarm > RecipeData:

   | Asset | RecipeId | DisplayName | Inputs | Output | ProcessingTime |
   |-------|----------|-------------|--------|--------|----------------|
   | Recipe_TomatoToSauce | tomato_to_sauce | Tomato Sauce | 3x tomato | 1x sauce | 20 |
   | Recipe_CherryToJam | cherry_to_jam | Cherry Jam | 3x cherry | 1x jam | 25 |
   | Recipe_RoseToBouquet | rose_to_bouquet | Flower Bouquet | 5x rose | 1x bouquet | 30 |
   | Recipe_WoodToPlanks | wood_to_planks | Planks | 5x wood | 1x planks | 20 |

4. Commit: `feat(A2): create 4 new RecipeData assets`

### Task A3: Create new PlaceableData assets (Kitchen, Workshop, Sawmill)

**Files:**
- Create: `Assets/Settings/Placeables/Placeable_Kitchen.asset`
- Create: `Assets/Settings/Placeables/Placeable_Workshop.asset`
- Create: `Assets/Settings/Placeables/Placeable_Sawmill.asset`

**Steps:**
1. Create each PlaceableData asset via Create > WheatFarm > PlaceableData:

   | Asset | PlaceableId | DisplayName | Category | GridSize | Level | Cost | Unlocked | Interactable | MaxLevel | Recipes |
   |-------|------------|-------------|----------|----------|-------|------|----------|--------------|----------|---------|
   | Placeable_Kitchen | kitchen | Kitchen | Building | 2x2 | Chunk | 100 | true | true | 3 | [TomatoToSauce, CherryToJam] |
   | Placeable_Workshop | workshop | Workshop | Workshop | 2x2 | Chunk | 120 | true | true | 3 | [RoseToBouquet] |
   | Placeable_Sawmill | sawmill | Sawmill | Building | 2x2 | Chunk | 60 | true | true | 3 | [WoodToPlanks] |

   - Set `BlocksPlanting = true`, `Rotation = Step90`.
   - Use placeholder cube prefabs (duplicate Mill or Bakery prefab and rename).

2. Add all 3 new assets to `PlaceableDatabase.asset`.

3. Set Cherry PlantData: `UprootYieldId = "wood"`, `UprootYieldAmount = 3`.

4. Commit: `feat(A3): create Kitchen, Workshop, Sawmill PlaceableData assets + wood yield on Cherry`

### Task A4: Create placeholder prefabs for new buildings

**Files:**
- Create: `Assets/Project/Prefabs/Buildings/Kitchen.prefab`
- Create: `Assets/Project/Prefabs/Buildings/Workshop.prefab`
- Create: `Assets/Project/Prefabs/Buildings/Sawmill.prefab`

**Steps:**
1. Duplicate an existing building prefab (e.g., Mill).
2. Rename to Kitchen/Workshop/Sawmill.
3. Change the cube material color to distinguish:
   - Kitchen: orange-ish
   - Workshop: purple-ish
   - Sawmill: brown-ish
4. Add a BoxCollider to each (for click detection via Physics.Raycast).
5. Assign prefabs to their PlaceableData assets.

6. Commit: `feat(A4): placeholder prefabs for Kitchen, Workshop, Sawmill`

---

## Phase B: Remove Legacy BuildingService

### Task B1: Update BuildingMarker to use PlacedObject

**Files:**
- Modify: `Assets/Scripts/Features/Buildings/BuildingMarker.cs`

**Steps:**
1. Replace the `PlacedBuilding` reference with `PlacedObject`:
   ```csharp
   using UnityEngine;

   namespace WheatFarm.Buildings
   {
       /// <summary>
       /// Attached to building/decor GameObjects at instantiation time.
       /// Allows raycasting to identify which PlacedObject was clicked.
       /// </summary>
       public class BuildingMarker : MonoBehaviour
       {
           public PlacedObject PlacedObject { get; set; }

           // Legacy bridge — kept temporarily for save manager migration
           [System.Obsolete("Use PlacedObject instead")]
           public PlacedBuilding Building { get; set; }
       }
   }
   ```

2. Commit: `refactor(B1): BuildingMarker stores PlacedObject`

### Task B2: Update PlacementService to set PlacedObject on BuildingMarker

**Files:**
- Modify: `Assets/Scripts/Features/Buildings/PlacementService.cs`

**Steps:**
1. In `PlaceChunkLevel()` method (~line 137-147), where `BuildingMarker` is created, replace:
   ```csharp
   // OLD:
   var marker = instance.AddComponent<BuildingMarker>();
   var legacyBuilding = new PlacedBuilding
   {
       Data = FindBuildingDataFallback(data),
       ChunkCoord = chunkCoord,
       Level = 1,
       Instance = instance
   };
   marker.Building = legacyBuilding;
   ```
   with:
   ```csharp
   // NEW:
   var marker = instance.AddComponent<BuildingMarker>();
   marker.PlacedObject = obj;
   ```

2. Remove the `FindBuildingDataFallback()` method entirely (~lines 269-278).

3. In `RestorePlace()` method, do the same — set `marker.PlacedObject = obj` instead of `marker.Building = legacyBuilding`.

4. Commit: `refactor(B2): PlacementService sets PlacedObject on BuildingMarker`

### Task B3: Rewrite ProductionService for PlacedObject

**Files:**
- Modify: `Assets/Scripts/Features/Buildings/ProductionService.cs`

**Steps:**
1. Replace entire file with:
   ```csharp
   using System.Collections.Generic;
   using System.Linq;
   using R3;
   using UnityEngine;
   using VContainer.Unity;
   using WheatFarm.Core.Data;
   using WheatFarm.Inventory;

   namespace WheatFarm.Buildings
   {
       public class ProductionSlot
       {
           public RecipeData Recipe;
           public float TimeRemaining;
           public float TotalTime;
           public bool AutoRepeat;
           public float Progress => TotalTime > 0 ? 1f - (TimeRemaining / TotalTime) : 1f;
           public bool IsComplete => TimeRemaining <= 0f;
       }

       public interface IProductionService
       {
           Subject<RecipeData> OnProductionCompleted { get; }
           Subject<PlacedObject> OnSlotsChanged { get; }
           bool TryStartProduction(PlacedObject building, RecipeData recipe, bool autoRepeat = true);
           bool TryStopProduction(PlacedObject building, int slotIndex);
           List<ProductionSlot> GetSlots(PlacedObject building);
           int GetMaxSlots(PlacedObject building);
           bool IsProducing(PlacedObject building);
           void SetAutoRepeat(PlacedObject building, bool enabled);
       }

       public class ProductionService : IProductionService, ITickable
       {
           private readonly IInventoryService _inventory;
           private readonly Dictionary<PlacedObject, List<ProductionSlot>> _active = new();

           public Subject<RecipeData> OnProductionCompleted { get; } = new();
           public Subject<PlacedObject> OnSlotsChanged { get; } = new();

           public ProductionService(IInventoryService inventory)
           {
               _inventory = inventory;
           }

           public int GetMaxSlots(PlacedObject building)
           {
               if (building?.Data == null) return 0;
               return Mathf.Clamp(building.Level, 1, building.Data.MaxLevel);
           }

           public bool IsProducing(PlacedObject building)
           {
               return _active.TryGetValue(building, out var slots) && slots.Count > 0;
           }

           public bool TryStartProduction(PlacedObject building, RecipeData recipe, bool autoRepeat = true)
           {
               if (recipe == null || building?.Data == null) return false;

               // Check slot limit
               int maxSlots = GetMaxSlots(building);
               if (_active.TryGetValue(building, out var existingSlots) && existingSlots.Count >= maxSlots)
                   return false;

               // Check inputs in inventory
               foreach (var input in recipe.Inputs)
               {
                   if (!_inventory.HasItem(input.ItemId, input.Amount)) return false;
               }

               // Consume inputs
               foreach (var input in recipe.Inputs)
               {
                   _inventory.TryConsume(input.ItemId, input.Amount);
               }

               if (!_active.TryGetValue(building, out var slots))
               {
                   slots = new List<ProductionSlot>();
                   _active[building] = slots;
               }

               slots.Add(new ProductionSlot
               {
                   Recipe = recipe,
                   TimeRemaining = recipe.ProcessingTime,
                   TotalTime = recipe.ProcessingTime,
                   AutoRepeat = autoRepeat
               });

               OnSlotsChanged.OnNext(building);
               return true;
           }

           public bool TryStopProduction(PlacedObject building, int slotIndex)
           {
               if (!_active.TryGetValue(building, out var slots)) return false;
               if (slotIndex < 0 || slotIndex >= slots.Count) return false;

               var slot = slots[slotIndex];
               // Refund ingredients
               foreach (var input in slot.Recipe.Inputs)
               {
                   _inventory.TryAdd(new InventoryItem(input.ItemId, ItemType.Harvest, input.Amount));
               }

               slots.RemoveAt(slotIndex);
               if (slots.Count == 0) _active.Remove(building);
               OnSlotsChanged.OnNext(building);
               return true;
           }

           public List<ProductionSlot> GetSlots(PlacedObject building)
           {
               return _active.GetValueOrDefault(building);
           }

           public void SetAutoRepeat(PlacedObject building, bool enabled)
           {
               if (!_active.TryGetValue(building, out var slots)) return;
               foreach (var slot in slots) slot.AutoRepeat = enabled;
           }

           public void Tick()
           {
               float dt = Time.deltaTime;
               var completed = new List<(PlacedObject building, ProductionSlot slot)>();

               foreach (var (building, slots) in _active)
               {
                   for (int i = slots.Count - 1; i >= 0; i--)
                   {
                       var slot = slots[i];
                       slot.TimeRemaining -= dt;

                       if (slot.IsComplete)
                       {
                           var output = slot.Recipe.Output;
                           _inventory.TryAdd(new InventoryItem(output.ItemId, ItemType.Product, output.Amount));
                           OnProductionCompleted.OnNext(slot.Recipe);
                           completed.Add((building, slot));
                           slots.RemoveAt(i);
                       }
                   }
               }

               // Auto-repeat for completed slots
               foreach (var (building, slot) in completed)
               {
                   if (slot.AutoRepeat)
                   {
                       TryStartProduction(building, slot.Recipe, true);
                   }
                   OnSlotsChanged.OnNext(building);
               }

               // Clean up empty entries
               var emptyKeys = _active.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key).ToList();
               foreach (var key in emptyKeys) _active.Remove(key);
           }

           // --- Save/Load support ---

           public List<ProductionSlotSaveData> GetSaveData()
           {
               var result = new List<ProductionSlotSaveData>();
               foreach (var (building, slots) in _active)
               {
                   foreach (var slot in slots)
                   {
                       result.Add(new ProductionSlotSaveData
                       {
                           PlaceableId = building.Data.PlaceableId,
                           ChunkCoord = building.ChunkCoord,
                           RecipeId = slot.Recipe.RecipeId,
                           TimeRemaining = slot.TimeRemaining,
                           AutoRepeat = slot.AutoRepeat
                       });
                   }
               }
               return result;
           }

           public void RestoreSlot(PlacedObject building, RecipeData recipe, float timeRemaining, bool autoRepeat)
           {
               if (building == null || recipe == null) return;

               if (!_active.TryGetValue(building, out var slots))
               {
                   slots = new List<ProductionSlot>();
                   _active[building] = slots;
               }

               slots.Add(new ProductionSlot
               {
                   Recipe = recipe,
                   TimeRemaining = timeRemaining,
                   TotalTime = recipe.ProcessingTime,
                   AutoRepeat = autoRepeat
               });
           }
       }
   }
   ```

2. Commit: `refactor(B3): ProductionService uses PlacedObject with slot limits and auto-repeat`

### Task B4: Add ProductionSlotSaveData to FarmSaveData

**Files:**
- Modify: `Assets/Scripts/Infrastructure/Save/FarmSaveData.cs`

**Steps:**
1. Add new struct:
   ```csharp
   [System.Serializable]
   public struct ProductionSlotSaveData
   {
       public string PlaceableId;
       public Vector2Int ChunkCoord;
       public string RecipeId;
       public float TimeRemaining;
       public bool AutoRepeat;
   }
   ```

2. Add field to `FarmSaveData` class:
   ```csharp
   public List<ProductionSlotSaveData> ProductionSlots;
   ```

3. Commit: `feat(B4): add ProductionSlotSaveData to save data`

### Task B5: Update FarmSaveManager for production save/load

**Files:**
- Modify: `Assets/Scripts/Infrastructure/Save/FarmSaveManager.cs`

**Steps:**
1. Add `IProductionService` as constructor dependency (cast to `ProductionService` for save/restore methods, or add save/load methods to the interface).

2. In `BuildSaveData()`, after the existing PlacedObject save block, add:
   ```csharp
   // Save production slots
   data.ProductionSlots = ((ProductionService)_production).GetSaveData();
   ```

3. In `RestoreData()`, after restoring PlacedObjects, add:
   ```csharp
   // Restore production slots
   if (data.ProductionSlots != null)
   {
       foreach (var slotData in data.ProductionSlots)
       {
           // Find the matching PlacedObject
           var building = _placement.PlacedObjects
               .FirstOrDefault(po => po.Data.PlaceableId == slotData.PlaceableId
                   && po.ChunkCoord == slotData.ChunkCoord);
           if (building == null) continue;

           // Find the recipe
           var recipe = building.Data.Recipes?
               .FirstOrDefault(r => r.RecipeId == slotData.RecipeId);
           if (recipe == null) continue;

           ((ProductionService)_production).RestoreSlot(
               building, recipe, slotData.TimeRemaining, slotData.AutoRepeat);
       }
   }
   ```

4. Commit: `feat(B5): save/load active production slots`

### Task B6: Remove legacy BuildingService and BuildingData

**Files:**
- Delete: `Assets/Scripts/Features/Buildings/BuildingService.cs`
- Delete: `Assets/Scripts/Core/Data/BuildingData.cs`
- Delete: `Assets/Scripts/Core/Data/BuildingDatabase.cs` (if exists)
- Delete: `Assets/Settings/Buildings/` folder (BuildingData assets)
- Modify: `Assets/Scripts/Infrastructure/Scopes/FarmScope.cs` — remove BuildingService registration
- Modify: `Assets/Scripts/Infrastructure/Save/FarmSaveManager.cs` — remove legacy building save/load

**Steps:**
1. In `FarmScope.cs`:
   - Remove: `builder.Register<BuildingService>(Lifetime.Singleton).As<IBuildingService>();`
   - Remove constructor parameter for `IBuildingService` if referenced by FarmSaveManager

2. In `FarmSaveManager.cs`:
   - Remove the `IBuildingService` dependency
   - Remove the legacy `PlacedBuildingSaveData` save block
   - Remove the legacy building restore block
   - Keep only the `PlacementService`-based save/load for buildings

3. Remove `PlacedBuildingSaveData` from `FarmSaveData.cs` (keep for backward compat if needed, or remove if clean break is ok).

4. Delete the files listed above.

5. Fix any remaining compilation errors — search for `PlacedBuilding`, `IBuildingService`, `BuildingData` references.

6. Commit: `refactor(B6): remove legacy BuildingService, BuildingData, PlacedBuilding`

---

## Phase C: Building Panel UI (MVP)

### Task C1: Create BuildingPanelView

**Files:**
- Create: `Assets/Scripts/UI/Building/BuildingPanelView.cs`

**Steps:**
1. Create a MonoBehaviour that builds its UI programmatically (same pattern as existing ShopView, InventoryView):
   ```csharp
   using System;
   using System.Collections.Generic;
   using TMPro;
   using UnityEngine;
   using UnityEngine.UI;
   using WheatFarm.Buildings;
   using WheatFarm.Core.Data;

   namespace WheatFarm.UI
   {
       public class BuildingPanelView : MonoBehaviour
       {
           public event Action<RecipeData> OnStartRecipeClicked;
           public event Action<int> OnStopSlotClicked;
           public event Action OnUpgradeClicked;
           public event Action OnCloseClicked;
           public event Action<bool> OnAutoRepeatToggled;

           private TextMeshProUGUI _titleText;
           private TextMeshProUGUI _levelText;
           private Transform _recipeListRoot;
           private Transform _activeSlotRoot;
           private Toggle _autoRepeatToggle;
           private Button _upgradeButton;
           private TextMeshProUGUI _upgradeCostText;
           private Button _closeButton;
           private CanvasGroup _canvasGroup;

           private readonly List<GameObject> _recipeRows = new();
           private readonly List<GameObject> _slotRows = new();

           public bool IsOpen => _canvasGroup != null && _canvasGroup.alpha > 0;

           // Build UI, Show/Hide, UpdateRecipes, UpdateSlots, UpdateUpgrade
           // (Programmatic Canvas construction — same approach as existing panels)

           public void Show() { _canvasGroup.alpha = 1; _canvasGroup.blocksRaycasts = true; }
           public void Hide() { _canvasGroup.alpha = 0; _canvasGroup.blocksRaycasts = false; }

           public void SetTitle(string name, int level)
           {
               _titleText.text = $"{name} (Lv.{level})";
           }

           public void SetRecipes(RecipeData[] recipes, Func<RecipeData, bool> canStart)
           {
               // Clear old rows, create new rows with Start buttons
               // Gray out button if canStart returns false
           }

           public void SetActiveSlots(List<ProductionSlot> slots)
           {
               // Show progress bars for each active slot
               // Add Stop button per slot
           }

           public void SetUpgradeInfo(int coinCost, int plankCost, bool canAfford, bool isMaxLevel)
           {
               // Show/hide upgrade button, set cost text
           }

           public void SetAutoRepeat(bool enabled)
           {
               _autoRepeatToggle.isOn = enabled;
           }

           public static BuildingPanelView Create(Transform canvasRoot)
           {
               // Programmatic construction (same pattern as BuildingInteractionPanel.Create)
               // Panel on right side of screen
               // Returns configured BuildingPanelView
           }
       }
   }
   ```

   Implementation note: Follow the exact same programmatic Canvas creation pattern used in `BuildingInteractionPanel.Create()` (currently in BuildingInteractionPanel.cs lines 174-248). Adapt the layout to include recipe list, slot progress, upgrade, and auto-repeat toggle.

2. Commit: `feat(C1): create BuildingPanelView with programmatic UI`

### Task C2: Create BuildingPanelPresenter

**Files:**
- Create: `Assets/Scripts/UI/Building/BuildingPanelPresenter.cs`

**Steps:**
1. Create presenter following MVP pattern:
   ```csharp
   using System;
   using R3;
   using VContainer.Unity;
   using WheatFarm.Buildings;
   using WheatFarm.Core.Data;
   using WheatFarm.Economy;
   using WheatFarm.Inventory;
   using WheatFarm.Player;

   namespace WheatFarm.UI
   {
       public class BuildingPanelPresenter : IInitializable, IDisposable, ITickable
       {
           private readonly BuildingPanelView _view;
           private readonly IProductionService _production;
           private readonly IInventoryService _inventory;
           private readonly IWalletService _wallet;
           private readonly FarmInteractionController _interaction;
           private readonly CompositeDisposable _disposables = new();

           private PlacedObject _currentBuilding;

           public BuildingPanelPresenter(
               BuildingPanelView view,
               IProductionService production,
               IInventoryService inventory,
               IWalletService wallet,
               FarmInteractionController interaction)
           {
               _view = view;
               _production = production;
               _inventory = inventory;
               _wallet = wallet;
               _interaction = interaction;
           }

           public void Initialize()
           {
               _interaction.OnBuildingClicked += OnBuildingClicked;
               _view.OnStartRecipeClicked += OnStartRecipe;
               _view.OnStopSlotClicked += OnStopSlot;
               _view.OnUpgradeClicked += OnUpgrade;
               _view.OnCloseClicked += OnClose;
               _view.OnAutoRepeatToggled += OnAutoRepeatToggled;

               _production.OnSlotsChanged
                   .Subscribe(building =>
                   {
                       if (building == _currentBuilding) RefreshView();
                   })
                   .AddTo(_disposables);
           }

           private void OnBuildingClicked(GameObject go)
           {
               var marker = go.GetComponent<BuildingMarker>();
               if (marker?.PlacedObject == null) return;
               if (!marker.PlacedObject.Data.Interactable) return;

               if (_view.IsOpen && _currentBuilding == marker.PlacedObject)
               {
                   _view.Hide();
                   _currentBuilding = null;
               }
               else
               {
                   _currentBuilding = marker.PlacedObject;
                   RefreshView();
                   _view.Show();
               }
           }

           private void RefreshView()
           {
               if (_currentBuilding == null) return;

               var data = _currentBuilding.Data;
               _view.SetTitle(data.DisplayName, _currentBuilding.Level);
               _view.SetRecipes(data.Recipes, CanStartRecipe);
               _view.SetActiveSlots(_production.GetSlots(_currentBuilding));

               int upgradeCoinCost = data.Cost * (_currentBuilding.Level + 1);
               int upgradePlankCost = _currentBuilding.Level == 1 ? 5 : 10;
               bool isMax = _currentBuilding.Level >= data.MaxLevel;
               bool canAfford = !isMax
                   && _wallet.CanAfford(upgradeCoinCost)
                   && _inventory.HasItem("planks", upgradePlankCost);
               _view.SetUpgradeInfo(upgradeCoinCost, upgradePlankCost, canAfford, isMax);
           }

           private bool CanStartRecipe(RecipeData recipe)
           {
               int maxSlots = _production.GetMaxSlots(_currentBuilding);
               var slots = _production.GetSlots(_currentBuilding);
               int used = slots?.Count ?? 0;
               if (used >= maxSlots) return false;

               foreach (var input in recipe.Inputs)
               {
                   if (!_inventory.HasItem(input.ItemId, input.Amount)) return false;
               }
               return true;
           }

           private void OnStartRecipe(RecipeData recipe)
           {
               _production.TryStartProduction(_currentBuilding, recipe, true);
               // RefreshView called via OnSlotsChanged subscription
           }

           private void OnStopSlot(int slotIndex)
           {
               _production.TryStopProduction(_currentBuilding, slotIndex);
           }

           private void OnUpgrade()
           {
               if (_currentBuilding == null) return;
               var data = _currentBuilding.Data;
               int coinCost = data.Cost * (_currentBuilding.Level + 1);
               int plankCost = _currentBuilding.Level == 1 ? 5 : 10;

               if (_wallet.TrySpend(coinCost) && _inventory.TryConsume("planks", plankCost))
               {
                   _currentBuilding.Level++;
                   RefreshView();
               }
           }

           private void OnClose()
           {
               _view.Hide();
               _currentBuilding = null;
           }

           private void OnAutoRepeatToggled(bool enabled)
           {
               if (_currentBuilding == null) return;
               _production.SetAutoRepeat(_currentBuilding, enabled);
           }

           // Tick for periodic UI refresh (progress bars)
           public void Tick()
           {
               if (_currentBuilding != null && _view.IsOpen)
               {
                   var slots = _production.GetSlots(_currentBuilding);
                   _view.SetActiveSlots(slots);
               }
           }

           public void Dispose()
           {
               _disposables.Dispose();
               if (_interaction != null) _interaction.OnBuildingClicked -= OnBuildingClicked;
           }
       }
   }
   ```

2. Commit: `feat(C2): create BuildingPanelPresenter with MVP pattern`

### Task C3: Register Building Panel in FarmScope

**Files:**
- Modify: `Assets/Scripts/Infrastructure/Scopes/FarmScope.cs`

**Steps:**
1. Remove old BuildingInteractionPanel + BuildingClickHandler registrations:
   ```csharp
   // REMOVE these lines:
   var buildPanel = BuildingInteractionPanel.Create(canvasRoot);
   builder.RegisterInstance(buildPanel);
   builder.Register<BuildingClickHandler>(Lifetime.Singleton)
       .As<IInitializable, System.IDisposable>();
   ```

2. Add new Building Panel registrations:
   ```csharp
   // Building Panel (MVP)
   var buildingPanel = BuildingPanelView.Create(canvasRoot);
   builder.RegisterInstance(buildingPanel);
   builder.Register<BuildingPanelPresenter>(Lifetime.Singleton)
       .As<IInitializable, ITickable, System.IDisposable>();
   ```

3. Delete the old `BuildingInteractionPanel.cs` file (or its entire content including `BuildingClickHandler`).

4. Commit: `refactor(C3): register BuildingPanelPresenter in FarmScope, remove legacy panel`

---

## Phase D: Wood from Uproot

### Task D1: PlantSystem yields wood on tree uproot

**Files:**
- Modify: `Assets/Scripts/Features/Farming/PlantSystem.cs`

**Steps:**
1. Add `IInventoryService` as a constructor dependency to PlantSystem.

2. In the `Uproot()` method, before `ClearCell()`, add:
   ```csharp
   // Yield uproot resource (e.g., wood from trees)
   if (!string.IsNullOrEmpty(plantData.UprootYieldId) && plantData.UprootYieldAmount > 0)
   {
       _inventory.TryAdd(new InventoryItem(
           plantData.UprootYieldId,
           ItemType.Harvest,
           plantData.UprootYieldAmount));
   }
   ```

   Note: `plantData` needs to be looked up from the cell's `PlantId` before clearing. Currently, `ClearCell` zeros the cell, so the lookup must happen first.

3. This means the Uproot method needs to look up the PlantData from PlantDatabase using `cell.PlantId` before clearing:
   ```csharp
   public void Uproot(Vector2Int chunkCoord, int cellX, int cellY)
   {
       var chunk = _chunkSystem.GetChunk(chunkCoord);
       if (chunk == null) return;

       int idx = chunk.CellIndex(cellX, cellY);
       ref var cell = ref chunk.Cells[idx];
       if (!cell.HasPlant) return;

       // Yield uproot resource before clearing
       var plantData = _plantDatabase.GetByMeshId(cell.PlantId);
       if (plantData != null && !string.IsNullOrEmpty(plantData.UprootYieldId) && plantData.UprootYieldAmount > 0)
       {
           _inventory.TryAdd(new InventoryItem(
               plantData.UprootYieldId,
               ItemType.Harvest,
               plantData.UprootYieldAmount));
       }

       ClearCell(ref cell, ref chunk.MeshProps[idx]);
       chunk.Dirty = true;
       _chunkSystem.UpdateGroundNeighborFlags(chunkCoord, cellX, cellY);
   }
   ```

4. Commit: `feat(D1): uproot trees yields wood to inventory`

---

## Phase E: Visual Feedback (Smoke Particles)

### Task E1: Add particle system to building prefabs

**Files:**
- Modify: Each building prefab (Mill, Bakery, Kitchen, Workshop, Sawmill)

**Steps:**
1. Add a child GameObject named "SmokeEffect" to each building prefab.
2. Add a ParticleSystem component:
   - Shape: Cone (small radius)
   - Emission: 5 particles/sec
   - Lifetime: 2s
   - Start color: light gray (0.7, 0.7, 0.7, 0.5)
   - Start size: 0.3
   - Simulation Space: World
   - Play On Awake: false (disabled by default)
3. Add a tag or naming convention: the smoke child must be named "SmokeEffect" for code to find it.

4. Commit: `feat(E1): add smoke particle system to building prefabs`

### Task E2: ProductionService toggles smoke particles

**Files:**
- Modify: `Assets/Scripts/Features/Buildings/ProductionService.cs`

**Steps:**
1. After starting production (in `TryStartProduction`), enable smoke:
   ```csharp
   EnableSmoke(building, true);
   ```

2. When all slots complete and no auto-repeat starts, disable smoke:
   ```csharp
   if (!_active.ContainsKey(building) || _active[building].Count == 0)
       EnableSmoke(building, false);
   ```

3. Add helper method:
   ```csharp
   private void EnableSmoke(PlacedObject building, bool enabled)
   {
       if (building?.Instance == null) return;
       var smoke = building.Instance.transform.Find("SmokeEffect");
       if (smoke == null) return;

       var ps = smoke.GetComponent<ParticleSystem>();
       if (ps == null) return;

       if (enabled && !ps.isPlaying) ps.Play();
       else if (!enabled && ps.isPlaying) ps.Stop();
   }
   ```

4. Commit: `feat(E2): toggle smoke particles during production`

---

## Phase F: Integration & Cleanup

### Task F1: Update CLAUDE.md

**Files:**
- Modify: `CLAUDE.md`

**Steps:**
1. Update the "Current State" section:
   - Move ProductionService from "Stub" to "Working"
   - Add Kitchen, Workshop, Sawmill to the placeable list
   - Note legacy BuildingService removed
   - Update architecture diagram (remove BuildingService from FarmScope)

2. Update Known Issues:
   - Remove "ProductionService is stub"
   - Add any new issues discovered during implementation

3. Commit: `docs(F1): update CLAUDE.md with production system status`

### Task F2: Play test the full chain

**Steps:**
1. Enter Play Mode.
2. Verify:
   - [ ] Plant wheat, wait for growth, harvest → wheat appears in inventory
   - [ ] Place Mill via Catalog > Buildings, click it → Building Panel opens
   - [ ] Start "Wheat to Flour" recipe → flour appears in inventory after 15s
   - [ ] Auto-repeat: recipe restarts if wheat available
   - [ ] Place Bakery, start "Flour to Bread" → bread in inventory
   - [ ] Place Kitchen, start "Tomato Sauce" and "Cherry Jam"
   - [ ] Place Workshop, start "Flower Bouquet"
   - [ ] Plant Cherry tree, wait for growth, uproot → wood in inventory
   - [ ] Place Sawmill, start "Wood to Planks"
   - [ ] Upgrade Mill (need planks) → level 2, now 2 slots
   - [ ] Save (F5), reload (F9) → production slots resume
   - [ ] Bulldoze a building → smoke stops, production stops

3. Fix any issues found.

4. Commit: `feat(F2): production system integration verified`

---

## Execution Order Summary

```
Phase A:  Data assets (PlantData, Recipes, Placeables, Prefabs)  (~1 session)
Phase B:  Remove legacy + rewrite ProductionService               (~1-2 sessions)
Phase C:  Building Panel UI (MVP)                                  (~1-2 sessions)
Phase D:  Wood from uproot                                         (~30 min)
Phase E:  Smoke particles                                          (~30 min)
Phase F:  Integration, CLAUDE.md, play test                        (~1 session)
```

Each phase delivers a working increment. After Phase B, the core production code is functional. Phase C adds the UI to interact with it. Phases D-E are small additions. Phase F validates everything.
