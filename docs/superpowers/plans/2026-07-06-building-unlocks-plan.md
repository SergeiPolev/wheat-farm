# Building Unlock Gating (Stage E) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Buildings become progression rewards: locked in the catalog (greyed, price/source shown), unlockable by coins or contract rewards, with contract rotation hiding contracts whose required items only locked buildings can produce.

**Architecture:** A third unlock service (`BuildingUnlockService`) mirroring the proven PlantUnlockService/DyeUnlockService pattern. `ContractData.UnlockBuildingId` reward, catalog click-to-buy, rotation obtainability via a recipe-output→producer map, persistence next to UnlockedDyes.

**Tech Stack:** Unity 6, VContainer, R3, NUnit EditMode. Verify via Unity MCP (`refresh_unity` → `read_console`, `run_tests`/`get_test_job`); Play Mode e2e via `execute_code` resolving from FarmScope.Container.

**Design Doc:** `docs/superpowers/specs/2026-07-06-building-unlocks-design.md`

**Env (same as D):** real project on `D:\UnityProjects\wheat-farm`; native Read/Edit/Write work. Never commit `UserSettings/Layouts/*.dwlt`. Branch: continue on `feature/contracts-and-unlocks` or branch `feature/building-unlocks` from current main (user's call at execution start; main already contains stage D).

---

## Key existing APIs (verified 2026-07-06)

- `PlaceableData` (Core): `PlaceableId, DisplayName, Category (Building/Decor/Path), Cost, UnlockedByDefault (bool, exists), Recipes (RecipeData[]), Role`.
- `RecipeData` (Core): `RecipeId, Inputs (ItemStack[]), Output (ItemStack), ProcessingTime`.
- `PlaceableDatabase.GetById(string)`, `.GetByCategory(PlaceableCategory)`.
- `IWalletService`: `Coins (ReadOnlyReactiveProperty<int>), CanAfford, Add, TrySpend, SetCoins`.
- `DyeUnlockService` (Economy) — the template to clone: ctor `(IWalletService)`, `IsUnlocked`, `TryUnlock` (spend-once), `ToSaveList/LoadFrom`, `event Action Changed`, `Grant(string)`.
- `ContractService` ctor: `(IWalletService, IInventoryService, IPlantUnlockService, IDyeUnlockService)`; `TryCompleteContract` grants `UnlockPlantId`/`UnlockDyeId`.
- `ContractRotationService` ctor: `(ContractDatabase, IDayNightService, IPlantUnlockService, IDyeUnlockService, IContractService, PlantDatabase)`; eligibility in `IsEligible(ContractData)`; tests construct via `Service(db)` helper with `null` dayNight.
- `CatalogPresenter` ctor: `(CatalogTabBar, PlantDatabase, PlaceableDatabase, IToolService, PlacementTool, IPlantUnlockService, IShopService)`; `PopulatePlaceables` sets `locked = !p.UnlockedByDefault`; `OnItemSelected` selects placeables unconditionally (the hole); `_unlock.Changed += OnUnlocksChanged` is the repopulate pattern.
- `ContractBoardPresenter.AppendReward(StringBuilder, ContractData)` — plant + dye branches; ctor has `PlantDatabase`, `DyeDatabase` (no PlaceableDatabase yet).
- `FarmSaveData`: `UnlockedDyes (List<string>)`; `FarmSaveManager` collects at `data.UnlockedDyes = _dyeUnlock?.ToSaveList()...`, restores via `_dyeUnlock.LoadFrom(...)`; optional ctor params pattern (`IDyeUnlockService dyeUnlock = null`).
- `GameScope.Configure`: `builder.Register<DyeUnlockService>(Lifetime.Singleton).As<IDyeUnlockService>();` — registration pattern.
- Tests: `Assets/Scripts/Tests/EditMode/`, asmdef `WheatFarm.Tests.EditMode` (overrideReferences=true; ObservableCollections.dll + R3.dll already in precompiledReferences).

## Producer map facts (for rotation)

Recipe outputs → producing building: flour→Mill, bread→Bakery, sauce→Kitchen, jam→Kitchen, bouquet→Workshop, planks→Sawmill. `wood` is NOT a recipe output (comes from uprooting trees) → stays obtainable. Mill stays `UnlockedByDefault=true`, so flour remains obtainable.

---

## File Structure

| File | Role | Action |
|---|---|---|
| `Assets/Scripts/Core/Data/PlaceableData.cs` | + `UnlockCost` | Modify |
| `Assets/Scripts/Core/Data/ContractData.cs` | + `UnlockBuildingId` | Modify |
| `Assets/Scripts/Features/Economy/BuildingUnlockService.cs` | new unlock service | Create |
| `Assets/Scripts/Features/Economy/ContractService.cs` | building grant on complete | Modify |
| `Assets/Scripts/Features/Economy/ContractRotationService.cs` | producer-map obtainability + reward filter | Modify |
| `Assets/Scripts/Infrastructure/Scopes/GameScope.cs` | register BuildingUnlockService | Modify |
| `Assets/Scripts/UI/Catalog/CatalogPresenter.cs` | locked via service, click=TryUnlock, Changed→repopulate | Modify |
| `Assets/Scripts/UI/Contracts/ContractBoardPresenter.cs` | reward text third branch | Modify |
| `Assets/Scripts/Infrastructure/Save/FarmSaveData.cs` | + `UnlockedBuildings` | Modify |
| `Assets/Scripts/Infrastructure/Save/FarmSaveManager.cs` | collect/restore | Modify |
| `Assets/Settings/Placeables/Placeable_{Bakery,Kitchen,Workshop,Sawmill}.asset` | lock + UnlockCost | Modify |
| `Assets/Settings/ContractDatabase.asset` | building rewards: 1 existing contract (kitchen) + 1-2 new authored (sawmill, bakery fallback) | Modify |
| `Assets/Scripts/Tests/EditMode/BuildingUnlockServiceTests.cs` | TDD | Create |
| `Assets/Scripts/Tests/EditMode/ContractServiceTests.cs` | + building grant test | Modify |
| `Assets/Scripts/Tests/EditMode/ContractRotationServiceTests.cs` | + producer/reward eligibility tests | Modify |

---

## Task 1: Data fields — `PlaceableData.UnlockCost` + `ContractData.UnlockBuildingId`

**Files:** Modify `PlaceableData.cs`, `ContractData.cs`.

- [ ] **Step 1:** In `PlaceableData`, under `public bool UnlockedByDefault = true;` add:
```csharp
        [Tooltip("Coin price to unlock a locked building from the catalog. 0 = contract-only.")]
        public int UnlockCost;
```
- [ ] **Step 2:** In `ContractData`, after `UnlockDyeId` add:
```csharp
        /// <summary>Nullable — reward: unlock a building (free grant).</summary>
        public string UnlockBuildingId;
```
- [ ] **Step 3:** `refresh_unity` (compile request) → `read_console` types=error: no CS errors. Run EditMode suite → 91/91 PASS.
- [ ] **Step 4: Commit** `feat(buildings): PlaceableData.UnlockCost + ContractData.UnlockBuildingId`.

---

## Task 2: BuildingUnlockService (TDD)

**Files:** Create `Assets/Scripts/Features/Economy/BuildingUnlockService.cs`; Create `Assets/Scripts/Tests/EditMode/BuildingUnlockServiceTests.cs`; Modify `GameScope.cs`.

- [ ] **Step 1: Write failing tests** (clone the DyeUnlockServiceTests shape; `Placeable(...)` helper builds a `ScriptableObject.CreateInstance<PlaceableData>()` with `PlaceableId`, `UnlockedByDefault`, `UnlockCost`):

```csharp
using NUnit.Framework;
using UnityEngine;
using WheatFarm.Core.Data;
using WheatFarm.Economy;

namespace WheatFarm.Tests
{
    public class BuildingUnlockServiceTests
    {
        private WalletService _wallet;

        [SetUp]
        public void SetUp() => _wallet = new WalletService();

        [TearDown]
        public void TearDown() => _wallet.Dispose();

        private static PlaceableData Placeable(string id, bool byDefault, int unlockCost)
        {
            var p = ScriptableObject.CreateInstance<PlaceableData>();
            p.PlaceableId = id;
            p.UnlockedByDefault = byDefault;
            p.UnlockCost = unlockCost;
            return p;
        }

        [Test]
        public void DefaultUnlocked_IsUnlocked_WithoutBuying()
        {
            var svc = new BuildingUnlockService(_wallet);
            Assert.IsTrue(svc.IsUnlocked(Placeable("mill", true, 0)));
        }

        [Test]
        public void TryUnlock_Affordable_SpendsOnceAndUnlocks_AndFiresChanged()
        {
            _wallet.SetCoins(500);
            var svc = new BuildingUnlockService(_wallet);
            var bakery = Placeable("bakery", false, 300);
            bool changed = false;
            svc.Changed += () => changed = true;

            Assert.IsTrue(svc.TryUnlock(bakery));
            Assert.IsTrue(svc.IsUnlocked(bakery));
            Assert.AreEqual(200, _wallet.Coins.CurrentValue);
            Assert.IsTrue(changed);

            Assert.IsTrue(svc.TryUnlock(bakery)); // no double spend
            Assert.AreEqual(200, _wallet.Coins.CurrentValue);
        }

        [Test]
        public void TryUnlock_Insufficient_DoesNotUnlockNorSpend()
        {
            _wallet.SetCoins(100);
            var svc = new BuildingUnlockService(_wallet);
            var bakery = Placeable("bakery", false, 300);

            Assert.IsFalse(svc.TryUnlock(bakery));
            Assert.IsFalse(svc.IsUnlocked(bakery));
            Assert.AreEqual(100, _wallet.Coins.CurrentValue);
        }

        [Test]
        public void TryUnlock_ContractOnly_NotPurchasable()
        {
            _wallet.SetCoins(1000);
            var svc = new BuildingUnlockService(_wallet);
            var special = Placeable("special", false, 0); // UnlockCost 0 = contract-only

            Assert.IsFalse(svc.TryUnlock(special));
            Assert.AreEqual(1000, _wallet.Coins.CurrentValue);
        }

        [Test]
        public void Grant_UnlocksFree_ThenTryUnlockTrueWithoutSpend()
        {
            _wallet.SetCoins(50);
            var svc = new BuildingUnlockService(_wallet);
            var special = Placeable("special", false, 0);

            svc.Grant("special");
            Assert.IsTrue(svc.IsUnlocked(special));
            // check order: already-unlocked wins over contract-only rule
            Assert.IsTrue(svc.TryUnlock(special));
            Assert.AreEqual(50, _wallet.Coins.CurrentValue);
        }

        [Test]
        public void SaveLoad_RoundTrips_GrantedOnly()
        {
            _wallet.SetCoins(500);
            var svc = new BuildingUnlockService(_wallet);
            svc.TryUnlock(Placeable("bakery", false, 300));

            var saved = svc.ToSaveList();
            CollectionAssert.Contains(saved, "bakery");
            CollectionAssert.DoesNotContain(saved, "mill"); // default-unlocked never stored

            var restored = new BuildingUnlockService(_wallet);
            restored.LoadFrom(saved);
            Assert.IsTrue(restored.IsUnlocked(Placeable("bakery", false, 300)));
        }
    }
}
```

- [ ] **Step 2: Run → FAIL** (`BuildingUnlockService` not found; force-refresh may be needed before the error shows: `refresh_unity mode=force`).
- [ ] **Step 3: Implement** `BuildingUnlockService.cs` (mirror DyeUnlockService; already-unlocked check BEFORE the contract-only rule):

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using WheatFarm.Core.Data;

namespace WheatFarm.Economy
{
    /// <summary>
    /// Tracks which buildings the player has unlocked. UnlockedByDefault buildings are always
    /// unlocked. A locked building is unlocked either by coin purchase (UnlockCost > 0) or by a
    /// contract reward (Grant). Mirrors DyeUnlockService.
    /// </summary>
    public interface IBuildingUnlockService
    {
        IReadOnlyCollection<string> UnlockedIds { get; }
        event Action Changed;
        bool IsUnlocked(PlaceableData placeable);
        bool TryUnlock(PlaceableData placeable);
        /// <summary>Free by-id grant (contract rewards bypass the coin cost).</summary>
        void Grant(string placeableId);
        List<string> ToSaveList();
        void LoadFrom(IEnumerable<string> ids);
    }

    public class BuildingUnlockService : IBuildingUnlockService
    {
        private readonly IWalletService _wallet;
        private readonly HashSet<string> _unlocked = new();

        public BuildingUnlockService(IWalletService wallet)
        {
            _wallet = wallet;
        }

        public IReadOnlyCollection<string> UnlockedIds => _unlocked;

        public event Action Changed;

        public bool IsUnlocked(PlaceableData placeable)
        {
            if (placeable == null) return false;
            return placeable.UnlockedByDefault || _unlocked.Contains(placeable.PlaceableId);
        }

        public bool TryUnlock(PlaceableData placeable)
        {
            if (placeable == null) return false;
            if (IsUnlocked(placeable)) return true;   // already-unlocked wins
            if (placeable.UnlockCost <= 0) return false; // contract-only

            if (!_wallet.TrySpend(placeable.UnlockCost)) return false;

            _unlocked.Add(placeable.PlaceableId);
            Changed?.Invoke();
            return true;
        }

        public void Grant(string placeableId)
        {
            if (!string.IsNullOrEmpty(placeableId) && _unlocked.Add(placeableId))
                Changed?.Invoke();
        }

        public List<string> ToSaveList() => _unlocked.ToList();

        public void LoadFrom(IEnumerable<string> ids)
        {
            _unlocked.Clear();
            if (ids != null)
                foreach (var id in ids)
                    if (!string.IsNullOrEmpty(id)) _unlocked.Add(id);
            Changed?.Invoke();
        }
    }
}
```

- [ ] **Step 4:** Register in `GameScope.Configure`, next to DyeUnlockService:
```csharp
            builder.Register<BuildingUnlockService>(Lifetime.Singleton)
                .As<IBuildingUnlockService>();
```
- [ ] **Step 5: Run → PASS** (91 + 6 = 97). Full suite green.
- [ ] **Step 6: Commit** `feat(buildings): BuildingUnlockService — coin unlock + free grant`.

---

## Task 3: ContractService — building grant on complete (TDD)

**Files:** Modify `ContractService.cs`, `ContractServiceTests.cs`.

- [ ] **Step 1: Add failing test** to `ContractServiceTests` (SetUp gains `_buildings = new BuildingUnlockService(_wallet);` and passes it as 5th ctor arg — update the existing `new ContractService(...)` call; `Contract(...)` helper gains optional `string buildingId = null` mapped to `UnlockBuildingId`):

```csharp
        [Test]
        public void Complete_WithBuildingReward_GrantsBuilding()
        {
            _inventory.TryAdd(new InventoryItem("flour", ItemType.Product, 3));
            var c = Contract(150, null, null, new ItemStack("flour", 3));
            c.UnlockBuildingId = "bakery";
            _svc.AcceptContract(c);

            Assert.IsTrue(_svc.TryCompleteContract(0));
            CollectionAssert.Contains(_buildings.UnlockedIds, "bakery");
        }
```

- [ ] **Step 2: Run → FAIL** (ctor arity).
- [ ] **Step 3: Implement:** `ContractService` ctor gains `IBuildingUnlockService buildings`, stores `_buildings`; in `TryCompleteContract` after the dye grant:
```csharp
            if (!string.IsNullOrEmpty(contract.Data.UnlockBuildingId))
                _buildings.Grant(contract.Data.UnlockBuildingId);
```
(VContainer resolves the new param automatically — registration done in Task 2.)
- [ ] **Step 4: Run → PASS.** Full suite green (98).
- [ ] **Step 5: Commit** `feat(contracts): building unlock as contract reward`.

---

## Task 4: Rotation eligibility — producer map + reward filter (TDD)

**Files:** Modify `ContractRotationService.cs`, `ContractRotationServiceTests.cs`.

- [ ] **Step 1: Add failing tests.** Test scaffolding changes:
  - SetUp gains `_buildings = new BuildingUnlockService(_wallet);` and `_placeableDb` built by a helper:
```csharp
        private static PlaceableData Producer(string buildingId, bool byDefault, params string[] outputs)
        {
            var p = ScriptableObject.CreateInstance<PlaceableData>();
            p.PlaceableId = buildingId;
            p.UnlockedByDefault = byDefault;
            p.Category = PlaceableCategory.Building;
            p.Recipes = System.Array.ConvertAll(outputs, o =>
            {
                var r = ScriptableObject.CreateInstance<RecipeData>();
                r.RecipeId = buildingId + "_" + o;
                r.Inputs = new[] { new ItemStack("wheat", 1) };
                r.Output = new ItemStack(o, 1);
                return r;
            });
            return p;
        }

        private static PlaceableDatabase PlaceableDb(params PlaceableData[] placeables)
        {
            var db = ScriptableObject.CreateInstance<PlaceableDatabase>();
            db.Items = placeables; // field is `Items` (PlaceableDatabase.cs:9), NOT `Placeables`
            return db;
        }
```
  - `Service(db)` helper becomes `new(db, null, _plants, _dyes, _contracts, _plantDb, _placeableDb, _buildings)`.
  - Default `_placeableDb` in SetUp: `PlaceableDb(Producer("mill", true, "flour"), Producer("bakery", false, "bread"))`.
  - New tests:

```csharp
        [Test]
        public void Eligible_ExcludesItemsProducedOnlyByLockedBuildings()
        {
            var db = Db(
                Contract("bread-req", required: new ItemStack("bread", 3)),   // bakery locked
                Contract("flour-req", required: new ItemStack("flour", 2)),   // mill unlocked
                Contract("wood-req", required: new ItemStack("wood", 2)));    // not a recipe output
            var svc = Service(db);

            var ids = svc.SelectEligible(10, 0).Select(c => c.ContractId).ToArray();
            CollectionAssert.DoesNotContain(ids, "bread-req");
            CollectionAssert.Contains(ids, "flour-req");
            CollectionAssert.Contains(ids, "wood-req");
        }

        [Test]
        public void Eligible_IncludesProducedItem_AfterBuildingUnlocked()
        {
            var db = Db(Contract("bread-req", required: new ItemStack("bread", 3)));
            var svc = Service(db);

            Assert.AreEqual(0, svc.SelectEligible(10, 0).Count);
            _buildings.Grant("bakery");
            Assert.AreEqual(1, svc.SelectEligible(10, 0).Count);
        }

        [Test]
        public void Eligible_ExcludesAlreadyUnlockedBuildingReward()
        {
            var c = Contract("c1", required: new ItemStack("wheat", 5));
            c.UnlockBuildingId = "bakery";
            var db = Db(c);
            var svc = Service(db);

            Assert.AreEqual(1, svc.SelectEligible(10, 0).Count);
            _buildings.Grant("bakery");
            Assert.AreEqual(0, svc.SelectEligible(10, 0).Count);
        }

        [Test]
        public void Eligible_ExcludesDefaultUnlockedBuildingReward()
        {
            var c = Contract("c1", required: new ItemStack("wheat", 5));
            c.UnlockBuildingId = "mill"; // UnlockedByDefault — never in the granted set
            var db = Db(c);
            var svc = Service(db);

            Assert.AreEqual(0, svc.SelectEligible(10, 0).Count);
        }
```

- [ ] **Step 2: Run → FAIL** (ctor arity).
- [ ] **Step 3: Implement** in `ContractRotationService`:
  - ctor gains `PlaceableDatabase placeableDb, IBuildingUnlockService buildings` (after `PlantDatabase`); store both.
  - Lazy producer map:
```csharp
        private Dictionary<string, List<PlaceableData>> _producers;

        private Dictionary<string, List<PlaceableData>> Producers
        {
            get
            {
                if (_producers != null) return _producers;
                _producers = new Dictionary<string, List<PlaceableData>>();
                if (_placeableDb?.Items != null)
                    foreach (var p in _placeableDb.Items)
                        if (p?.Recipes != null)
                            foreach (var r in p.Recipes)
                                if (r != null && !string.IsNullOrEmpty(r.Output.ItemId))
                                {
                                    if (!_producers.TryGetValue(r.Output.ItemId, out var list))
                                        _producers[r.Output.ItemId] = list = new List<PlaceableData>();
                                    list.Add(p);
                                }
                return _producers;
            }
        }
```
  - In `IsEligible`, reward filter after the dye check (use `IsUnlocked`, NOT `UnlockedIds.Contains` — default-unlocked ids are never in the granted set):
```csharp
            if (!string.IsNullOrEmpty(c.UnlockBuildingId)
                && _buildings.IsUnlocked(_placeableDb?.GetById(c.UnlockBuildingId)))
                return false;
```
  - In the requirements loop, extend obtainability: after the locked-plant check add
```csharp
                    if (Producers.TryGetValue(req.ItemId, out var producers)
                        && !producers.Exists(p => _buildings.IsUnlocked(p)))
                        return false;
```
- [ ] **Step 4: Run → PASS.** Full suite green (102).
- [ ] **Step 5: Commit** `feat(contracts): rotation excludes contracts gated by locked buildings`.

---

## Task 5: UI — catalog click-to-unlock + board reward text

**Files:** Modify `CatalogPresenter.cs`, `ContractBoardPresenter.cs`.

- [ ] **Step 1: CatalogPresenter:**
  - ctor gains `IBuildingUnlockService buildingUnlock`; store `_buildingUnlock`.
  - `Initialize`: `_buildingUnlock.Changed += OnUnlocksChanged;` `Dispose`: unsubscribe.
  - `PopulatePlaceables`: `display.Add((p.DisplayName, p.Cost, !_buildingUnlock.IsUnlocked(p)));`
    Note: `cost` shown stays `p.Cost` (placement price). Locked entries show the lock badge; unlock price communicated on click failure log for now (catalog tile has no second price slot — YAGNI).
  - `OnItemSelected`, placeable branch — gate before select (direct service call, NO ShopService indirection):
```csharp
            else if (item is PlaceableData placeable)
            {
                // Gate ONLY buildings (spec §3). Placeable_PathBrick has UnlockedByDefault=0 with
                // no unlock path — gating all categories would make it permanently unselectable.
                if (placeable.Category == PlaceableCategory.Building
                    && !_buildingUnlock.IsUnlocked(placeable))
                {
                    if (!_buildingUnlock.TryUnlock(placeable))
                    {
                        Debug.Log($"[Catalog] {placeable.DisplayName} is locked — " +
                            (placeable.UnlockCost > 0
                                ? $"need {placeable.UnlockCost} coins."
                                : "unlocked by a contract reward."));
                        return;
                    }
                    Debug.Log($"[Catalog] Unlocked {placeable.DisplayName}.");
                }

                _toolService.EquipTool(ToolId.Placement);
                _placementTool.SelectPlaceable(placeable);
                Debug.Log($"[Catalog] Selected placeable: {placeable.DisplayName}");
            }
```
- [ ] **Step 2: ContractBoardPresenter:** ctor gains `PlaceableDatabase placeableDb = null` (optional default, like FarmSaveManager's `dyeUnlock = null` — GameScope registers the database instance conditionally, resolution must not fail on an unwired scene); in `AppendReward` add third branch:
```csharp
            if (!string.IsNullOrEmpty(contract.UnlockBuildingId))
            {
                var b = _placeableDb != null ? _placeableDb.GetById(contract.UnlockBuildingId) : null;
                sb.Append($" +{b?.DisplayName ?? contract.UnlockBuildingId}");
            }
```
- [ ] **Step 3:** `refresh_unity` → no CS errors; full suite green (UI has no EditMode tests — compile is the check).
- [ ] **Step 4: Commit** `feat(buildings): catalog click-to-unlock + contract board building reward text`.

---

## Task 6: Persistence

**Files:** Modify `FarmSaveData.cs`, `FarmSaveManager.cs`.

- [ ] **Step 1:** `FarmSaveData`: after `UnlockedDyes` add `public List<string> UnlockedBuildings = new();`
- [ ] **Step 2:** `FarmSaveManager`: field `IBuildingUnlockService _buildingUnlock;` ctor param `IBuildingUnlockService buildingUnlock = null` (after `dyeUnlock`); collect `data.UnlockedBuildings = _buildingUnlock?.ToSaveList() ?? new List<string>();`; restore (next to dyes) `if (_buildingUnlock != null && data.UnlockedBuildings != null) _buildingUnlock.LoadFrom(data.UnlockedBuildings);`
- [ ] **Step 3:** `refresh_unity` → clean; suite green.
- [ ] **Step 4: Commit** `feat(buildings): persist unlocked buildings`.

---

## Task 7: Content + Play Mode verification

**Files:** Modify `Assets/Settings/Placeables/Placeable_Bakery.asset`, `Placeable_Kitchen.asset`, `Placeable_Workshop.asset`, `Placeable_Sawmill.asset`, `Assets/Settings/ContractDatabase.asset`.

- [ ] **Step 1: Placeable assets** (YAML edit: `UnlockedByDefault: 0`, add `UnlockCost:` line next to it; field appears in YAML after a `refresh_unity` reserialize — safe to add manually):
  - Bakery: `UnlockCost: 300` (already `UnlockedByDefault: 0` in the asset — the CLAUDE.md "set to true for testing" note is stale; expect no diff on that line)
  - Kitchen: `UnlockCost: 400`
  - Workshop: `UnlockCost: 400`
  - Sawmill: `UnlockCost: 500`
  - Untouched: Mill, Lamp, Fence, paths, and the non-production buildings `Market`, `Warehouse`, `Contracts` (all `UnlockedByDefault: 1`).
- [ ] **Step 2: ContractDatabase.asset** — add `UnlockBuildingId:` to all 13 entries (empty), set:
  - Bakery reward: **NOT `flour_delivery`** — it already carries `UnlockDyeId: red`, and rotation
    hides a contract once ANY of its unlock rewards is owned (buy red dye → contract gone → Bakery
    stranded to coin-only). Building-reward contracts must carry **no other unlock reward**. Pick a
    reward-free crop/flour contract from the catalog; if none exists, add one:
    `golden_harvest: 10 wheat + 5 corn → 120 coins + UnlockBuildingId: bakery`.
  - `tomato_crate` → `UnlockBuildingId: kitchen` (requires tomato, a crop — verified reward-free)
  - Sawmill reward: **NOT `mixed_harvest`** — it carries `UnlockDyeId: green` (same conflict as
    flour_delivery). No other reward-free mid-tier contract exists (`wheat_harvest`/`corn_delivery`
    are early-cheap) → **add a new contract**:
    `timber_prep: 15 corn → 150 coins + UnlockBuildingId: sawmill` (corn is a default-unlocked
    crop; does not require Sawmill's own output — constraint holds). Entry count becomes 14
    (15 with `golden_harvest` if the bakery fallback is needed); the "add empty `UnlockBuildingId:`
    to all entries" instruction covers the new ones too.
  - Workshop stays coin-only (400c).
  - Constraint check: no unlock contract requires its own building's output. `bread_order`/`sauce_batch`/`cherry_jam`/`rose_bouquet`/`lumber_order` auto-hidden by rotation until producers unlock.
- [ ] **Step 3:** `refresh_unity mode=force` → `read_console` clean; verify via `execute_code`: load ContractDatabase + 4 placeables, assert `UnlockCost`/`UnlockBuildingId` parsed.
- [ ] **Step 4: Play Mode e2e** (`manage_editor play`, then `execute_code` resolving from FarmScope.Container):
  - Fresh state: `IBuildingUnlockService.IsUnlocked(bakery)` false; rotation `SelectEligible(20, 0)` contains no `bread_order`/`sauce_batch`/`cherry_jam`/`rose_bouquet`/`lumber_order`.
  - Coin path: `wallet.SetCoins(600)`; `TryUnlock(bakery)` → true, coins 300; `bread_order` now eligible.
  - Contract path: accept `tomato_crate` via db, add 8 tomato, `TryCompleteContract` → kitchen unlocked, board text shows `+Kitchen`.
  - Save/load: `SaveGame` (UniTaskExtensions.Forget), `Grant("sawmill")`, `LoadGame` → sawmill lock restored to saved state, bakery/kitchen stay unlocked.
  - `read_console` errors clean. `manage_editor stop`.
- [ ] **Step 5: Commit** `feat(buildings): lock Bakery/Kitchen/Workshop/Sawmill + contract rewards`.

---

## Completion checklist
- [ ] Locked buildings greyed in catalog; click buys (coins) or explains (contract-only); unlock persists.
- [ ] Contract reward `UnlockBuildingId` grants building; board shows `+<DisplayName>`.
- [ ] Rotation never offers contracts requiring items only locked buildings produce, nor rewards for unlocked buildings.
- [ ] EditMode: BuildingUnlockServiceTests + extended contract/rotation tests green; suite ~102, no regressions.
- [ ] Old saves: previously placed locked buildings keep working (RestorePlace ignores unlock state).

## Out of scope
- Building 3D models, smoke particles, new chains; decor/path gating; unlock-registry refactor.

## Execution order
```
T1 → T2 → T3 → T4 (services, TDD) → T5 (UI) → T6 (save) → T7 (content + Play Mode)
```
