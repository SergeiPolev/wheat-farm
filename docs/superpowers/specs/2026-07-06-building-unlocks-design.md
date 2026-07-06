# Building Unlock Gating (Stage E) — Design

**Date:** 2026-07-06
**Status:** Approved by user (session decision log below)
**Depends on:** Stage D (contracts & unlocks, merged in PR #2)

## Goal

Buildings become progression rewards instead of being available from the start. Locked buildings
are visible in the catalog (greyed, with their unlock price or source), can be unlocked by coins
or granted by contract rewards, and the daily contract rotation stops offering contracts whose
required items can only be produced by still-locked buildings. Closes the known issue: *"Bakery
UnlockedByDefault=false but no unlock system exists yet."*

## Decisions (user-confirmed)

1. **Unlock mechanism:** both paths — contract reward (`UnlockBuildingId`) and coin purchase from
   the catalog. A building with `UnlockCost = 0` is contract-only.
2. **Initially locked:** Bakery, Kitchen, Workshop, Sawmill. Mill stays unlocked (first production
   chain). Decor (Lamp, Fence) and paths stay unlocked.
3. **Catalog presentation:** locked buildings shown greyed with cost/source; clicking a purchasable
   one buys and selects it (same UX as plant unlock in CatalogPresenter / dye palette).

## Architecture

Mirror the proven `PlantUnlockService` / `DyeUnlockService` pattern — a third sibling service, not
a generalized registry (two working services stay untouched; YAGNI).

### 1. Data

- `PlaceableData` (Core): add `public int UnlockCost;` under the Economy header. `0` = not
  purchasable with coins (contract-only). `UnlockedByDefault` already exists.
- `ContractData` (Core): add `public string UnlockDyeId;`-style field `public string UnlockBuildingId;`.
- Assets: `Placeable_Bakery/Kitchen/Workshop/Sawmill` → `UnlockedByDefault: 0` with tentative
  `UnlockCost` 300/400/400/500 (balanced in the content task). Mill, Lamp, Fence, paths unchanged.

### 2. BuildingUnlockService (Economy asmdef, registered in GameScope)

```csharp
public interface IBuildingUnlockService
{
    IReadOnlyCollection<string> UnlockedIds { get; }
    event Action Changed;
    bool IsUnlocked(PlaceableData placeable);   // UnlockedByDefault || set.Contains(PlaceableId)
    bool TryUnlock(PlaceableData placeable);    // coin purchase: UnlockCost > 0 && wallet.TrySpend
    void Grant(string placeableId);             // free (contract reward), raises Changed
    List<string> ToSaveList();
    void LoadFrom(IEnumerable<string> ids);
}
```

Implementation folds the coin purchase in, exactly like `DyeUnlockService.TryUnlock`.
`TryUnlock` on an already-unlocked building returns true without spending. `TryUnlock` with
`UnlockCost <= 0` returns false (contract-only).

### 3. Integrations

- **ContractService.TryCompleteContract:** inject `IBuildingUnlockService`; after plant/dye
  rewards, `if (!string.IsNullOrEmpty(UnlockBuildingId)) _buildings.Grant(id)`.
- **CatalogPresenter:**
  - `PopulatePlaceables`: `locked = !_buildingUnlock.IsUnlocked(p)` (replaces `!p.UnlockedByDefault`).
  - `OnItemSelected` for a locked `PlaceableData` with `Category == Building`: call `TryUnlock`;
    on failure log and return (same flow as locked plants); on success fall through to select.
  - Subscribe `Changed` → repopulate current tab (mirror the existing plant `OnUnlocksChanged` wiring).
- **ContractBoardPresenter.AppendReward:** `+<DisplayName>` for `UnlockBuildingId` via
  `PlaceableDatabase.GetById` (third branch after plant and dye).
- **Save:** `FarmSaveData.UnlockedBuildings` (List<string>); `FarmSaveManager` collect/restore via
  `ToSaveList()/LoadFrom()`, wired next to `UnlockedDyes`.

### 4. Rotation eligibility fix (produced goods)

`ContractRotationService` currently treats all non-plant items as obtainable — with Bakery locked,
a "deliver 3 bread" contract is impossible but still offered. Fix:

- Inject `PlaceableDatabase` + `IBuildingUnlockService`.
- Build a map `itemId → producing placeables` from `PlaceableData.Recipes[].Output` across all
  placeables (computed lazily or at construction; the database is static at runtime).
- Requirement eligibility: an item produced **only by locked buildings** is not obtainable →
  contract excluded. Items that are plants keep the existing plant-unlock check. Items in neither
  set (e.g. `wood` from uprooting trees) remain obtainable.
- Reward eligibility: a contract whose `UnlockBuildingId` is already unlocked is excluded
  (symmetric with plant/dye reward exclusion).

### 5. Content

- 2–3 contracts gain building rewards (e.g. `flour_delivery` → Bakery; a mid-tier delivery →
  Sawmill; exact mapping balanced in the content task).
- Existing contracts requiring produced goods (`bread_order`, `cherry_jam`, `rose_bouquet`,
  `lumber_order`, `sauce_batch`) are automatically hidden by rotation until their producer is
  unlocked — no data changes needed for them.

## Data flow

```
Contract complete → ContractService → BuildingUnlockService.Grant → Changed
Catalog click (locked building) → CatalogPresenter → BuildingUnlockService.TryUnlock (wallet) → Changed
Changed → CatalogPresenter repopulate (greyed → active)
Dawn → ContractRotationService.Rotate → SelectEligible (plant + building obtainability + reward filters)
Save/Load → FarmSaveManager ↔ BuildingUnlockService.ToSaveList/LoadFrom
```

## Edge cases

- **Old saves / already-placed buildings:** locking affects only new placement from the catalog.
  A Bakery placed before the update keeps working after load (PlacementService.RestorePlace does
  not consult unlock state).
- **Locked contract-only building (`UnlockCost = 0`):** catalog click logs and does nothing;
  the entry stays greyed until a contract grants it.
- **Double grant / grant of already-purchased building:** `HashSet.Add` no-ops; no double `Changed`.

## Testing (EditMode, TDD)

- `BuildingUnlockServiceTests` (clone of DyeUnlockServiceTests): default-unlocked, coin purchase
  spends once, insufficient funds, contract-only (`UnlockCost=0`) not purchasable, `Grant` free,
  save/load round-trip.
- `ContractServiceTests`: completing a contract with `UnlockBuildingId` grants the building.
- `ContractRotationServiceTests`: requirement produced only by a locked building → excluded;
  unlocked producer → included; reward building already unlocked → excluded.

## Out of scope

- Building 3D models, smoke particles, new production chains (separate stages).
- Unlock gating for decor/paths (all stay unlocked).
- Migrating plant/dye unlock services into a shared registry.
