# Dye Color Picker (Stage C) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A data-driven HUD palette that lets the player buy (one-time, with coins) and select dye colors for the Dye tool, shown only while the Dye tool is active.

**Architecture:** `DyeData` (exists) + new `DyeDatabase` SO feed a palette UI. `DyeUnlockService` owns which dyes are unlocked (coin purchase via `IWalletService`, persisted in the save like `UnlockedPlants`). MVP UI: `DyeColorPaletteView` (programmatic, `PanelBuilder`) + `DyeColorPalettePresenter` (R3, shows palette when `ToolId.Dye` active, click → buy/select via `DyeTool.SelectColor`). The brush preview already reflects `DyeTool.PreviewCellColor`.

**Tech Stack:** Unity 6, VContainer, R3, UGUI/TMP, NUnit EditMode. Verify C# via csharp-analyzer; shaders/UI visually via Unity MCP.

**Design Doc:** `docs/superpowers/specs/2026-06-21-dye-color-picker-design.md`

**Critical env notes (same as stage B):** real project on `D:\UnityProjects\wheat-farm`; native Read/Edit/Write work here. After editing C# call `refresh_unity` then `read_console`; run EditMode via `run_tests`/`get_test_job`. Commit explicit paths (`git add <path>` + `git diff --cached`) — the tree can hold others' edits (e.g. `UserSettings/Layouts/*.dwlt` — never commit it). Branch: `feature/dye-color-picker` (already created from `main`).

---

## File Structure

| File | Role | Action |
|---|---|---|
| `Assets/Scripts/Core/Data/DyeDatabase.cs` | SO catalog of dyes (mirrors `PlantDatabase`) | Create |
| `Assets/Settings/Dyes/Dye_*.asset` (×8) | Individual `DyeData` assets | Create |
| `Assets/Settings/DyeDatabase.asset` | Database listing the 8 dyes | Create |
| `Assets/Scripts/Features/Economy/DyeUnlockService.cs` | Owns unlocked set + coin purchase + persistence | Create |
| `Assets/Scripts/Infrastructure/Save/FarmSaveData.cs` | + `UnlockedDyes` field | Modify |
| `Assets/Scripts/Infrastructure/Save/FarmSaveManager.cs` | collect/restore `UnlockedDyes` | Modify |
| `Assets/Scripts/Infrastructure/Scopes/GameScope.cs` | register `DyeDatabase` + `DyeUnlockService` | Modify |
| `Assets/Scripts/UI/Dye/DyeColorPaletteView.cs` | Swatch panel (visuals + events) | Create |
| `Assets/Scripts/UI/Dye/DyeColorPalettePresenter.cs` | tool-active→visibility, click→buy/select | Create |
| `Assets/Scripts/UI/PanelBuilder.cs` | + `BuildDyePalettePanel(canvasRoot)` | Modify |
| `Assets/Scripts/Infrastructure/Scopes/FarmScope.cs` | register View + Presenter | Modify |
| `Assets/Scripts/Tests/EditMode/DyeDatabaseTests.cs` | DyeDatabase lookups | Create |
| `Assets/Scripts/Tests/EditMode/DyeUnlockServiceTests.cs` | unlock/buy/persist | Create |
| `Assets/Scripts/Tests/EditMode/DyeColorPalettePresenterTests.cs` | presenter logic | Create |

**Boundary note:** `DyeUnlockService` folds the coin purchase (`IWalletService.TrySpend`) into `TryUnlock(dye)` — unlike plants (where `ShopService.TryUnlockPlant` does the spend). This keeps the dye economy self-contained and avoids growing `ShopService`.

---

## Task 1: DyeDatabase SO

**Files:**
- Create: `Assets/Scripts/Core/Data/DyeDatabase.cs`
- Test: `Assets/Scripts/Tests/EditMode/DyeDatabaseTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// DyeDatabaseTests.cs
using NUnit.Framework;
using UnityEngine;
using WheatFarm.Core.Data;

namespace WheatFarm.Tests
{
    public class DyeDatabaseTests
    {
        private static DyeData Dye(string id)
        {
            var d = ScriptableObject.CreateInstance<DyeData>();
            d.DyeId = id; d.Color = Color.red; d.Cost = 10;
            return d;
        }

        [Test]
        public void GetById_ReturnsMatch_AndNullForUnknown()
        {
            var db = ScriptableObject.CreateInstance<DyeDatabase>();
            db.Items = new[] { Dye("red"), Dye("blue") };

            Assert.AreEqual("red", db.GetById("red").DyeId);
            Assert.IsNull(db.GetById("nope"));
        }
    }
}
```

- [ ] **Step 2: Run it, expect FAIL** (`DyeDatabase` undefined). Unity MCP `run_tests` (EditMode) → compile error / fail.

- [ ] **Step 3: Implement `DyeDatabase`** (mirror `PlantDatabase`):

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace WheatFarm.Core.Data
{
    [CreateAssetMenu(menuName = "WheatFarm/DyeDatabase")]
    public class DyeDatabase : ScriptableObject
    {
        public DyeData[] Items;

        private Dictionary<string, DyeData> _cache;

        public IReadOnlyList<DyeData> All => Items;

        public DyeData GetById(string id)
        {
            if (_cache == null)
            {
                _cache = new Dictionary<string, DyeData>();
                foreach (var d in Items)
                    if (d != null) _cache[d.DyeId] = d;
            }
            return _cache.GetValueOrDefault(id);
        }
    }
}
```

- [ ] **Step 4: Run test, expect PASS** + whole EditMode suite still green.
- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/Data/DyeDatabase.cs Assets/Scripts/Core/Data/DyeDatabase.cs.meta \
        Assets/Scripts/Tests/EditMode/DyeDatabaseTests.cs Assets/Scripts/Tests/EditMode/DyeDatabaseTests.cs.meta
git commit -m "feat(dye): DyeDatabase SO + lookup test"
```

---

## Task 2: DyeData assets + database asset + GameScope registration

**Files:**
- Create: `Assets/Settings/Dyes/Dye_White.asset` … `Dye_Pink.asset` (8), `Assets/Settings/DyeDatabase.asset`
- Modify: `Assets/Scripts/Infrastructure/Scopes/GameScope.cs`

- [ ] **Step 1:** Create 8 `DyeData` assets (via `manage_scriptable_object` create, type `WheatFarm.Core.Data.DyeData`) in `Assets/Settings/Dyes/`. `RequiresCrafting=false`, `CraftIngredientIds` empty for all:

| Id | DisplayName | Color (RGB) | Cost |
|---|---|---|---|
| white | White | 1,1,1 | 0 |
| red | Red | 0.85,0.2,0.2 | 20 |
| orange | Orange | 0.95,0.55,0.15 | 25 |
| yellow | Yellow | 0.95,0.85,0.2 | 20 |
| green | Green | 0.3,0.7,0.3 | 25 |
| blue | Blue | 0.25,0.5,0.85 | 30 |
| purple | Purple | 0.55,0.3,0.7 | 40 |
| pink | Pink | 0.9,0.5,0.7 | 35 |

- [ ] **Step 2:** Create `DyeDatabase.asset` (`manage_scriptable_object` create, type `WheatFarm.Core.Data.DyeDatabase`), set `Items` to the 8 assets (patch by guid/path).
- [ ] **Step 3:** In `GameScope.cs` add `[SerializeField] private DyeDatabase _dyeDatabase;` and `builder.RegisterInstance(_dyeDatabase);` next to where `PlantDatabase` is registered. Assign `_dyeDatabase` in the scene GameScope Inspector (via `manage_gameobject`/component set, or note for manual).
- [ ] **Step 4:** Verify: `refresh_unity` + `read_console` clean; Play Mode no DI errors.
- [ ] **Step 5: Commit** (assets + GameScope.cs + scene if changed).

```bash
git add Assets/Settings/Dyes Assets/Settings/DyeDatabase.asset Assets/Settings/DyeDatabase.asset.meta \
        Assets/Scripts/Infrastructure/Scopes/GameScope.cs
git commit -m "feat(dye): 8 DyeData assets + DyeDatabase, registered in GameScope"
```

---

## Task 3: DyeUnlockService (coin unlock + persistence)

**Files:**
- Create: `Assets/Scripts/Features/Economy/DyeUnlockService.cs`
- Test: `Assets/Scripts/Tests/EditMode/DyeUnlockServiceTests.cs`

Interface + class (mirrors `PlantUnlockService`, folds in purchase):

```csharp
public interface IDyeUnlockService
{
    System.Collections.Generic.IReadOnlyCollection<string> UnlockedIds { get; }
    event System.Action Changed;
    bool IsUnlocked(DyeData dye);          // Cost == 0 OR in the set
    bool TryUnlock(DyeData dye);           // already unlocked -> true; else TrySpend(Cost) then add
    System.Collections.Generic.List<string> ToSaveList();
    void LoadFrom(System.Collections.Generic.IEnumerable<string> ids);
}
```

- [ ] **Step 1: Write failing tests** (use a fake wallet; reuse the `FakeWalletService` pattern from `PlacementServiceTests`, or a local minimal one):

```csharp
// DyeUnlockServiceTests.cs — key cases
// - Cost0_AlwaysUnlocked: IsUnlocked(whiteCost0) == true without buying
// - TryUnlock_SpendsAndUnlocks: enough coins -> true, wallet spent == Cost, IsUnlocked true, Changed fired
// - TryUnlock_Insufficient_NoUnlock: wallet returns false -> TryUnlock false, IsUnlocked false, nothing spent
// - TryUnlock_AlreadyUnlocked_NoDoubleSpend: second call true, spend count unchanged
// - SaveLoad_RoundTrip: unlock red -> ToSaveList contains "red"; new service LoadFrom -> IsUnlocked(red) true; Cost0 not stored
```

(Write each as its own `[Test]`; assert wallet spend via a fake that records `TrySpend` amounts and can be set to allow/deny.)

- [ ] **Step 2: Run, expect FAIL** (`DyeUnlockService`/`IDyeUnlockService` undefined).
- [ ] **Step 3: Implement** `DyeUnlockService` (ctor `IWalletService wallet`; `HashSet<string> _unlocked`; `IsUnlocked` = `dye.Cost == 0 || _unlocked.Contains(dye.DyeId)`; `TryUnlock` short-circuits if unlocked, else `wallet.TrySpend(dye.Cost)` then add + raise `Changed`; `ToSaveList` = `_unlocked.ToList()`; `LoadFrom` clears + adds). Namespace `WheatFarm.Economy`.
- [ ] **Step 4: Run, expect PASS** + full suite green.
- [ ] **Step 5:** Register in `GameScope.cs`: `builder.Register<DyeUnlockService>(Lifetime.Singleton).As<IDyeUnlockService>();` (near `PlantUnlockService`). `read_console` clean.
- [ ] **Step 6: Commit.**

```bash
git add Assets/Scripts/Features/Economy/DyeUnlockService.cs Assets/Scripts/Features/Economy/DyeUnlockService.cs.meta \
        Assets/Scripts/Tests/EditMode/DyeUnlockServiceTests.cs Assets/Scripts/Tests/EditMode/DyeUnlockServiceTests.cs.meta \
        Assets/Scripts/Infrastructure/Scopes/GameScope.cs
git commit -m "feat(dye): DyeUnlockService — coin unlock + persistence"
```

---

## Task 4: Persist unlocked dyes in save/load

**Files:**
- Modify: `Assets/Scripts/Infrastructure/Save/FarmSaveData.cs` (+ `public List<string> UnlockedDyes;`)
- Modify: `Assets/Scripts/Infrastructure/Save/FarmSaveManager.cs` (inject `IDyeUnlockService`; collect/restore)

Mirror `UnlockedPlants` exactly (`FarmSaveManager.cs:180` collect, `:200` restore).

- [ ] **Step 1:** Add `public List<string> UnlockedDyes;` to `FarmSaveData`.
- [ ] **Step 2:** In `FarmSaveManager`: add `IDyeUnlockService _dyeUnlock` to ctor/fields; in `CollectSaveData`: `data.UnlockedDyes = _dyeUnlock?.ToSaveList() ?? new List<string>();`; in `RestoreFromData`: `if (data.UnlockedDyes != null) _dyeUnlock.LoadFrom(data.UnlockedDyes);`.
- [ ] **Step 3:** Verify compile (`read_console`); Play Mode F5/F9 round-trip keeps a bought dye unlocked. (Save round-trip is also indirectly covered by Task 3 SaveLoad test on the service.)
- [ ] **Step 4: Commit.**

```bash
git add Assets/Scripts/Infrastructure/Save/FarmSaveData.cs Assets/Scripts/Infrastructure/Save/FarmSaveManager.cs
git commit -m "feat(dye): persist unlocked dyes in save/load"
```

---

## Task 5: DyeColorPaletteView + PanelBuilder

**Files:**
- Create: `Assets/Scripts/UI/Dye/DyeColorPaletteView.cs`
- Modify: `Assets/Scripts/UI/PanelBuilder.cs` (+ `BuildDyePalettePanel`)

Shader/UI is verified visually (no unit test), per the project's UI convention.

- [ ] **Step 1: Create `DyeColorPaletteView`** (MonoBehaviour). Responsibilities only:
  - `void SetVisible(bool)` — toggles the panel root.
  - `void Build(IReadOnlyList<DyeData> dyes)` — create one swatch button per dye (cache button + its background image + price label).
  - `void SetSwatchState(int index, bool unlocked, bool selected, int cost)` — color the swatch, show/hide price label, draw a selected highlight (e.g. outline), dim if locked.
  - `event Action<int> SwatchClicked` — fired with the dye index.
  Keep it a thin view (no economy logic).

- [ ] **Step 2: Add `BuildDyePalettePanel(Transform canvasRoot)`** to `PanelBuilder` (follow `BuildShopPanel`/`CreatePanel`/`CreateButton` patterns; use `SetField` to wire the View's private refs). Small horizontal panel anchored bottom-center (above the catalog) with a row of square swatches.

- [ ] **Step 3:** Verify compile (`read_console` clean). Visual check deferred to Task 6.
- [ ] **Step 4: Commit.**

```bash
git add Assets/Scripts/UI/Dye/DyeColorPaletteView.cs Assets/Scripts/UI/Dye/DyeColorPaletteView.cs.meta \
        Assets/Scripts/UI/PanelBuilder.cs
git commit -m "feat(dye): DyeColorPaletteView + PanelBuilder.BuildDyePalettePanel"
```

---

## Task 6: DyeColorPalettePresenter + FarmScope wiring

**Files:**
- Create: `Assets/Scripts/UI/Dye/DyeColorPalettePresenter.cs`
- Test: `Assets/Scripts/Tests/EditMode/DyeColorPalettePresenterTests.cs`
- Modify: `Assets/Scripts/Infrastructure/Scopes/FarmScope.cs`

Presenter (`IInitializable, IDisposable`), ctor injects: `DyeColorPaletteView view, DyeDatabase db, IDyeUnlockService unlock, IToolService tools, DyeTool dyeTool`. (If `DyeTool` isn't directly resolvable, resolve via `IToolService`/the tool registration used by other tools — check FarmScope; otherwise register `DyeTool` `.As<DyeTool, ITool>()` like `PlacementTool`.)

Logic to test (with fakes — fake `IToolService` exposing a writable `ReactiveProperty<ToolId>`, real `DyeUnlockService` + fake wallet, a test double `DyeColorPaletteView` recording calls, a real `DyeDatabase`):

- [ ] **Step 1: Write failing tests:**
  - `PaletteVisibleOnlyWhenDyeToolActive`: CurrentToolId = Dye → view.SetVisible(true); switch to another tool → SetVisible(false).
  - `ClickUnlocked_SelectsColor`: click an unlocked swatch → `dyeTool.SelectedColor` equals that dye's color (assert via `DyeTool.SelectedColor`).
  - `ClickLocked_Affordable_Unlocks_ThenSelects`: locked dye, enough coins → becomes unlocked (`unlock.IsUnlocked` true) and selected.
  - `ClickLocked_Unaffordable_NoSelect`: not enough coins → stays locked, color not selected.

  (To make `DyeColorPaletteView` test-friendly without a real Canvas, extract an interface `IDyeColorPaletteView` with `SetVisible/Build/SetSwatchState` + `SwatchClicked`, implemented by the MonoBehaviour; the presenter depends on the interface. The test uses a fake implementation.)

- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement presenter:** on `Initialize`, `view.Build(db.All)`, subscribe to `tools.CurrentToolId` (R3) → SetVisible when `== ToolId.Dye`; subscribe to `unlock.Changed` and `wallet.Coins` → re-render swatch states; subscribe to `view.SwatchClicked` → if `unlock.IsUnlocked(dye)` then `dyeTool.SelectColor(dye.Color)` + mark selected, else `unlock.TryUnlock(dye)` and on success select. Track selected index; dispose subscriptions.
- [ ] **Step 4: Run, expect PASS** + full suite green.
- [ ] **Step 5:** Register in `FarmScope.cs`: build the view via `PanelBuilder.BuildDyePalettePanel(hudCanvas)` and `RegisterComponent`, register presenter `.As<IInitializable>()` (mirror HUD/Shop presenter registration). Ensure `DyeTool` is resolvable.
- [ ] **Step 6: Verify (Play Mode, visual):** equip Dye tool → palette appears; locked swatches show price; buy one → coins drop, swatch unlocks & highlights; select → paint a crop, color applies; switch tool → palette hides; F5/F9 → unlock persists. `read_console` clean.
- [ ] **Step 7: Commit.**

```bash
git add Assets/Scripts/UI/Dye/DyeColorPalettePresenter.cs Assets/Scripts/UI/Dye/DyeColorPalettePresenter.cs.meta \
        Assets/Scripts/Tests/EditMode/DyeColorPalettePresenterTests.cs Assets/Scripts/Tests/EditMode/DyeColorPalettePresenterTests.cs.meta \
        Assets/Scripts/Infrastructure/Scopes/FarmScope.cs
git commit -m "feat(dye): DyeColorPalettePresenter — show on Dye tool, buy/select swatches"
```

---

## Completion checklist
- [ ] Palette visible only when Dye tool active.
- [ ] Locked dyes show price; buying spends coins and unlocks; unlock persists across F5/F9.
- [ ] Selecting a dye drives `DyeTool.SelectColor`; brush preview + applied color match.
- [ ] EditMode: `DyeDatabaseTests`, `DyeUnlockServiceTests`, `DyeColorPalettePresenterTests` green; existing suite unbroken.
- [ ] No console errors.

## Out of scope (per spec)
- Crafting dyes from ingredients (`RequiresCrafting`/`CraftIngredientIds` stay false/empty).
- Per-application cost, free-form color wheel.

## Execution order
```
T1 (DyeDatabase) → T2 (assets+GameScope) → T3 (DyeUnlockService) → T4 (save) → T5 (View) → T6 (Presenter+wiring)
```
Each task is a compilable, committable increment.
