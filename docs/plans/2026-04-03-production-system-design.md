# Production System Design

**Date:** 2026-04-03
**Status:** Approved
**Depends on:** PlacementService (PlacedObject), InventoryService, PlantSystem

---

## Goal

Activate the production chain: raw crops enter buildings and become processed goods worth more money. Buildings auto-repeat recipes while ingredients are available. Legacy BuildingService is removed; ProductionService operates entirely on PlacedObject/PlaceableData.

## Architecture Change: Remove Legacy BuildingService

**Current state:** Two parallel building systems coexist.
- `BuildingService` (legacy) — uses `BuildingData`, `PlacedBuilding`
- `PlacementService` (new) — uses `PlaceableData`, `PlacedObject`

**Problem:** `ProductionService` depends on `PlacedBuilding` whose `.Data` is always null when buildings are placed via PlacementService. The bridge (`FindBuildingDataFallback`) returns null.

**Solution:** Remove `BuildingService` entirely. Rewrite `ProductionService` to accept `PlacedObject`. Recipes come from `PlaceableData.Recipes[]`. The `BuildingClickHandler` and `BuildingInteractionPanel` are rewritten as `BuildingPanelPresenter` + `BuildingPanelView` (MVP).

## 5 Production Buildings

| Building | Cost | Unlocked | Recipes |
|----------|------|----------|---------|
| Mill | 50 | Yes | 3 Wheat -> 1 Flour (15s) |
| Bakery | 80 | No | 2 Flour -> 1 Bread (20s) |
| Kitchen | 100 | No | 3 Tomato -> 1 Sauce (20s), 3 Cherry -> 1 Jam (25s) |
| Workshop | 120 | No | 5 Rose -> 1 Bouquet (30s) |
| Sawmill | 60 | Yes | 5 Wood -> 1 Planks (20s) |

### Sell Prices (processed goods)

| Product | Sell Price | Profit vs raw |
|---------|-----------|---------------|
| Flour | 12 | 3x Wheat(8ea) = 24 -> 12 flour (loss alone, used in chain) |
| Bread | 35 | 2 Flour -> 1 Bread (chain profit) |
| Sauce | 20 | 3x Tomato(15ea) = 45 -> 20 (volume discount, but contracts pay more) |
| Jam | 30 | 3x Cherry(40ea) = 120 -> 30 (prestige item, contract-driven) |
| Bouquet | 60 | 5x Rose(20ea) = 100 -> 60 (contract-driven) |
| Planks | 15 | Construction material, not for direct sale |

**Note:** Some processed goods are not profitable to sell directly. Their value comes from contracts that pay premium prices for processed goods, creating the motivation to build the chain.

## Production Mechanics

### Slot System
- Level 1: 1 production slot
- Level 2: 2 slots (upgrade cost: base_cost * 2)
- Level 3: 3 slots (upgrade cost: base_cost * 3)
- Upgrade requires Planks (from Sawmill): Level 2 = 5 Planks, Level 3 = 10 Planks

### Auto-Repeat
When a recipe completes:
1. Output added to inventory
2. If `autoRepeat` is enabled (default: on) AND ingredients available in inventory -> start new cycle automatically
3. If ingredients unavailable -> slot becomes idle, building stops producing
4. Player can toggle auto-repeat per building via the panel
5. Player can stop active production (ingredients returned to inventory)

### Production Flow
```
Player clicks building
  -> BuildingPanelPresenter.Show(PlacedObject)
  -> Display: building name, level, available recipes, active slots
  -> Player clicks recipe "Start"
  -> ProductionService.TryStartProduction(PlacedObject, RecipeData)
    -> Check slot availability (slots < maxSlots for level)
    -> Check inventory for ingredients
    -> Consume ingredients from InventoryService
    -> Create ProductionSlot { Recipe, TimeRemaining, AutoRepeat }
    -> Mark building as producing (enable particles)
  -> Tick() each frame:
    -> Decrement TimeRemaining
    -> Update progress (1 - TimeRemaining/TotalTime)
    -> On complete:
      -> Add output to inventory
      -> Fire OnProductionCompleted event
      -> If autoRepeat: try start again
      -> If no more active slots: stop particles
```

## Wood Resource

- **Source:** Uproot tool on fully grown trees (Cherry, and any future tree types)
- **Yield:** Uprooting a grown tree gives Wood (amount based on tree type: Cherry = 3 Wood)
- **Tree is destroyed** on uproot (non-renewable harvesting)
- **This creates a trade-off:** trees are decorative (beauty) vs. functional (wood for planks for upgrades)
- PlantData gets a new field: `UprootYieldId` (string) and `UprootYieldAmount` (int)

## UI: Building Panel (MVP)

### BuildingPanelView (MonoBehaviour)
- Title text (building name + level)
- Recipe list (scrollable if > 3 recipes)
  - Each recipe row: icon, "3x Wheat -> 1 Flour", Start button
  - If slot active: progress bar replacing Start button, Stop button
- Auto-repeat toggle (per building)
- Upgrade button (shows cost: coins + planks)
- Close button (X)

### BuildingPanelPresenter (IInitializable, IDisposable)
- Subscribes to `ProductionService` slot progress for live updates
- Handles Start/Stop/Upgrade button clicks
- Queries `InventoryService` for ingredient availability (grays out Start if insufficient)

## Visual Feedback

- **Smoke particles:** Simple ParticleSystem on building prefab, disabled by default
  - Enabled when any production slot is active
  - Disabled when all slots idle
- **Progress indicator:** World-space progress bar above building (optional, V2)

## Save/Load

### ProductionSaveData (new)
```csharp
[System.Serializable]
public struct ProductionSlotSaveData
{
    public string PlaceableId;     // which building (matches PlacedObject)
    public Vector2Int ChunkCoord;  // building location (unique identifier)
    public string RecipeId;
    public float TimeRemaining;
    public bool AutoRepeat;
}
```

Added to `FarmSaveData.ProductionSlots` list. On load, ProductionService recreates active slots for matching PlacedObjects.

## New ScriptableObject Assets Needed

### RecipeData (4 new)
- Recipe_TomatoToSauce: 3 Tomato -> 1 Sauce, 20s
- Recipe_CherryToJam: 3 Cherry -> 1 Jam, 25s
- Recipe_RoseToBouquet: 5 Rose -> 1 Bouquet, 30s
- Recipe_WoodToPlanks: 5 Wood -> 1 Planks, 20s

### PlaceableData (3 new)
- Placeable_Kitchen: cost 100, recipes [TomatoToSauce, CherryToJam]
- Placeable_Workshop: cost 120, recipes [RoseToBouquet]
- Placeable_Sawmill: cost 60, recipes [WoodToPlanks]

### PlantData modifications
- All plants: add `UprootYieldId` and `UprootYieldAmount` fields
- Cherry: `UprootYieldId = "wood"`, `UprootYieldAmount = 3`
- Future trees can also yield wood

## Out of Scope (V2)
- Building-specific 3D models (keep placeholder cubes for now)
- Recipe unlocking per building level
- World-space progress bars
- Sound effects
- Kitchen/Workshop/Sawmill unlock conditions (use UnlockedByDefault for now, contract unlocks later)
