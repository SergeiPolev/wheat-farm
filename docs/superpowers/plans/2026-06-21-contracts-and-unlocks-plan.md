# Contracts & Unlocks (Stage D) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the contract scaffolding into a working progression loop: deliver items from inventory to complete contracts, earning coins + plant/dye unlocks, on a board that refreshes daily.

**Architecture:** `ContractService` gains inventory-consume completion + plant/dye unlock rewards + abandon. A new `ContractRotationService` rebuilds the eligible *available* set each Dawn (persisted). The existing board UI is rewired to inventory-derived progress + reward display. Harvest auto-contribute is removed.

**Tech Stack:** Unity 6, VContainer, R3, NUnit EditMode. Verify C# via csharp-analyzer; UI/Play Mode via Unity MCP.

**Design Doc:** `docs/superpowers/specs/2026-06-21-contracts-and-unlocks-design.md`

**Env (same as B/C):** real project on `D:\UnityProjects\wheat-farm`; native Read/Edit/Write work. After C# edits: `refresh_unity` → `read_console`; run EditMode via `run_tests`/`get_test_job`. Commit explicit paths; never commit `UserSettings/Layouts/*.dwlt`. Branch: `feature/contracts-and-unlocks`.

---

## Key existing APIs (verified)

- `IInventoryService`: `bool HasItem(string,int)`, `int GetAmount(string)`, `bool TryConsume(string,int)`, `bool TryAdd(InventoryItem)`.
- `IContractService`: `ObservableList<ActiveContract> ActiveContracts`, `Subject<ActiveContract> OnContractCompleted`, `AcceptContract(ContractData)`, `TryCompleteContract(int)`, `ContributeItem(string,int)` (to be removed).
- `ActiveContract` (struct): `ContractData Data`, `int[] Progress` (kept for save compat; no longer drives completion), `bool IsComplete`.
- `ContractData`: `ContractId, Description, Required (ItemStack[]), CoinReward, UnlockPlantId, RewardMultiplier` (+`UnlockDyeId` to add).
- `IPlantUnlockService.Unlock(string)`, `IDyeUnlockService.TryUnlock(DyeData)` / needs a by-id grant → use `DyeDatabase.GetById` then `TryUnlock`, or add `Unlock(string)`.
- `IDayNightService.CurrentPhase` (ReadOnlyReactiveProperty<TimeOfDay>); `TimeOfDay.Dawn` = new day.
- `ContractBoardView`: `SetAvailableContracts(string[],bool[])`, `SetContracts(string[],float[],bool[])`, events `OnAcceptClicked/OnCompleteClicked` (+`OnAbandonClicked` to add).
- `HarvestRewardHandler.OnHarvested` calls `_contracts.ContributeItem(plantId,1)` — to remove.

---

## File Structure

| File | Role | Action |
|---|---|---|
| `Assets/Scripts/Core/Data/ContractData.cs` | + `UnlockDyeId` | Modify |
| `Assets/Scripts/Features/Economy/ContractService.cs` | consume+reward completion, abandon, CanComplete; drop ContributeItem | Modify |
| `Assets/Scripts/Core/Data/DyeUnlockService.cs` (Economy) | + `Unlock(string)` by-id grant | Modify |
| `Assets/Scripts/Infrastructure/HarvestRewardHandler.cs` | drop contract dependency | Modify |
| `Assets/Scripts/Features/Economy/ContractRotationService.cs` | daily eligible available set + persistence | Create |
| `Assets/Scripts/Infrastructure/Scopes/GameScope.cs` | register rotation; ContractService deps already via DI | Modify |
| `Assets/Scripts/Infrastructure/Save/FarmSaveData.cs` | + `AvailableContractIds`, `ContractDayIndex` | Modify |
| `Assets/Scripts/Infrastructure/Save/FarmSaveManager.cs` | collect/restore rotation state | Modify |
| `Assets/Scripts/UI/Contracts/ContractBoardView.cs` | + `OnAbandonClicked` + abandon button | Modify |
| `Assets/Scripts/UI/PanelBuilder.cs` | abandon button in active-entry prefab | Modify |
| `Assets/Scripts/UI/Contracts/ContractBoardPresenter.cs` | available from rotation; progress from inventory; reward text; abandon | Modify |
| `Assets/Settings/ContractDatabase.asset` | expand to ~10–15 contracts (+dye rewards) | Modify |
| Tests (EditMode) | ContractService, ContractRotationService | Create |

---

## Phase 1 — Completion loop + rewards

### Task 1: `ContractData.UnlockDyeId` + `DyeUnlockService.Unlock(string)`

**Files:** Modify `ContractData.cs`, `DyeUnlockService.cs`.

- [ ] **Step 1:** Add `public string UnlockDyeId;` to `ContractData` (after `UnlockPlantId`).
- [ ] **Step 2:** Add a by-id free grant to `DyeUnlockService` (contract rewards bypass the coin cost):
```csharp
// in IDyeUnlockService + DyeUnlockService
void Grant(string dyeId); // adds id to the unlocked set + raises Changed (no wallet spend)
```
Implement: `if (!string.IsNullOrEmpty(dyeId) && _unlocked.Add(dyeId)) Changed?.Invoke();`
- [ ] **Step 3:** `refresh_unity` + `read_console` clean; full EditMode suite still green.
- [ ] **Step 4: Commit** `feat(contracts): ContractData.UnlockDyeId + DyeUnlockService.Grant`.

---

### Task 2: ContractService — consume-on-complete + unlock rewards + abandon

**Files:** Modify `ContractService.cs`. Test: `Assets/Scripts/Tests/EditMode/ContractServiceTests.cs`.

Inject `IPlantUnlockService plant`, `IDyeUnlockService dye` (in addition to wallet+inventory).

- [ ] **Step 1: Write failing tests** (real `WalletService`, real `InventoryService`, real
  `PlantUnlockService`/`DyeUnlockService`, an in-code `ContractData`):
  - `Complete_WithItems_ConsumesPaysAndUnlocks`: inventory has 5 wheat; contract Required=5 wheat,
    CoinReward=50, UnlockPlantId="rose", UnlockDyeId="red". After `TryCompleteContract(0)` → returns
    true; inventory wheat == 0; coins += 50; `plant.IsUnlocked("rose")`; `dye` unlocked "red";
    contract removed; `OnContractCompleted` fired.
  - `Complete_WithoutItems_DoesNothing`: inventory has 2 wheat, need 5 → returns false; wheat still 2;
    coins unchanged; contract still active.
  - `Complete_MultiRequirement_AllOrNothing`: needs 3 wheat + 2 flour, has wheat but no flour →
    false; nothing consumed.
  - `Abandon_RemovesWithoutReward`: accept then `AbandonContract(0)` → active empty; coins unchanged.
  - `CanComplete_ReflectsInventoryCoverage`: true only when all requirements covered.

- [ ] **Step 2: Run → FAIL** (new ctor params / methods missing).
- [ ] **Step 3: Implement:**
  - ctor stores `_plant`, `_dye`.
  - `bool CanComplete(ActiveContract c)` = every `c.Data.Required[i]` has
    `_inventory.HasItem(id, amount)`.
  - `bool TryCompleteContract(int index)` → if `!CanComplete` return false; `TryConsume` each
    required; `_wallet.Add(CoinReward)`; if `UnlockPlantId` non-empty `_plant.Unlock(id)`; if
    `UnlockDyeId` non-empty `_dye.Grant(id)`; `OnContractCompleted.OnNext`; remove; return true.
    (Change signature `void`→`bool`; update `IContractService`.)
  - `void AbandonContract(int index)` → bounds-check, remove.
  - Remove `ContributeItem` from class + interface.
- [ ] **Step 4: Run → PASS** + full suite green.
- [ ] **Step 5: Commit** `feat(contracts): consume-on-complete with plant/dye unlock + abandon`.

---

### Task 3: Remove harvest auto-contribute

**Files:** Modify `HarvestRewardHandler.cs`.

- [ ] **Step 1:** Drop the `IContractService` ctor param + field + the `_contracts.ContributeItem(...)`
  line in `OnHarvested` (keep the plant→inventory add). Verify no other `ContributeItem` callers
  remain (`find_usages`).
- [ ] **Step 2:** `read_console` clean; full suite green.
- [ ] **Step 3: Commit** `refactor(contracts): drop harvest auto-contribute (progress is inventory-derived)`.

---

### Task 4: Board UI — inventory progress, reward, complete/abandon

**Files:** Modify `ContractBoardView.cs`, `PanelBuilder.cs` (active-entry prefab), `ContractBoardPresenter.cs`.

- [ ] **Step 1: View** — add `public event Action<int> OnAbandonClicked;` and an Abandon button in the
  active-entry prefab (`PanelBuilder.CreateContractEntryPrefab`), wired like the complete button.
  `SetContracts` stays; presenter decides button enable state.
- [ ] **Step 2: Presenter** — inject `IInventoryService`; in `RefreshActive`, compute progress per
  contract as `sum(min(GetAmount(req.id), req.amount)) / sum(req.amount)` and `canComplete =
  _contracts.CanComplete(active)`. `FormatActive`/`FormatAvailable` append the reward (coins +
  plant/dye display name; resolve plant via `PlantDatabase`, dye via `DyeDatabase`). Wire
  `OnAbandonClicked → _contracts.AbandonContract`.
- [ ] **Step 3:** `read_console` clean; full suite green. (Visual check happens in Task 7's Play Mode.)
- [ ] **Step 4: Commit** `feat(contracts): board shows inventory progress, reward, complete/abandon`.

---

## Phase 2 — Daily rotation + persistence

### Task 5: ContractRotationService (Dawn → eligible available set)

**Files:** Create `ContractRotationService.cs`; Modify `GameScope.cs`, `ContractBoardPresenter.cs`.
Test: `Assets/Scripts/Tests/EditMode/ContractRotationServiceTests.cs`.

Service (`IStartable, IDisposable`): ctor `ContractDatabase db, IDayNightService dayNight,
IPlantUnlockService plant, IDyeUnlockService dye, IContractService contracts`. Exposes
`ObservableList<ContractData> Available` + `int DayIndex`.

- [ ] **Step 1: Write failing tests** (no DayNight needed — test the pure selection method directly):
  - `Eligible_ExcludesAlreadyUnlockedReward`: a contract rewarding an already-unlocked plant is not
    selected.
  - `Eligible_ExcludesRequirementsFromLockedPlants`: a contract requiring a crop whose plant is
    locked is not selected. (Produced goods / always-available items count as obtainable.)
  - `Eligible_ExcludesActive`: a contract already in `ActiveContracts` is not offered.
  - `Rotate_RespectsCount`: picks at most N.
  - `SaveLoad_RoundTrips`: `ToSave()/LoadFrom(ids, dayIndex)` restores the same available set.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement:** pure `IReadOnlyList<ContractData> SelectEligible(int n, int seed)`;
  `Rotate()` fills `Available` from it and bumps `DayIndex`; `Start()` subscribes
  `dayNight.CurrentPhase.Where(p => p == TimeOfDay.Dawn)` → `Rotate()` (skip if loaded from save this
  session — guard so load doesn't immediately reshuffle). `ToSave()/LoadFrom(...)`.
- [ ] **Step 4: Run → PASS** + suite green.
- [ ] **Step 5:** Register in `GameScope`: `builder.Register<ContractRotationService>(Lifetime.Singleton).As<ContractRotationService, IStartable>();`. Point `ContractBoardPresenter.RefreshAvailable` at `rotation.Available` instead of the whole `ContractDatabase` (inject `ContractRotationService`).
- [ ] **Step 6: Commit** `feat(contracts): daily-rotating eligible available board`.

---

### Task 6: Persist rotation state

**Files:** Modify `FarmSaveData.cs`, `FarmSaveManager.cs`.

- [ ] **Step 1:** Add `public List<string> AvailableContractIds = new();` and `public int ContractDayIndex;` to `FarmSaveData`.
- [ ] **Step 2:** `FarmSaveManager`: inject `ContractRotationService`; in collect set both from `rotation.ToSave()`; in restore call `rotation.LoadFrom(data.AvailableContractIds, data.ContractDayIndex)` (mirror UnlockedDyes wiring at `:180/:200`).
- [ ] **Step 3:** `read_console` clean; suite green.
- [ ] **Step 4: Commit** `feat(contracts): persist available set + day index`.

---

## Phase 3 — Content

### Task 7: Expand contract catalog + Play Mode verification

**Files:** Modify `Assets/Settings/ContractDatabase.asset` (via `manage_scriptable_object` modify, or edit YAML through Bash for the inline array).

- [ ] **Step 1:** Author ~10–15 contracts in `ContractDatabase.Contracts[]`: a mix of crop deliveries
  and produced-goods deliveries, escalating `Required`/`CoinReward`, with `UnlockPlantId`/`UnlockDyeId`
  rewards spanning the locked plants (rose, cherry, …) and a few dyes (use ids from
  `Assets/Settings/Dyes/`). Keep `RequiresCrafting`-style gating implicit via eligibility.
- [ ] **Step 2: Play Mode (visual):** open the board (press C), accept a contract, grow/deliver the
  required items, Complete → coins added + reward unlocked (new plant appears in catalog / dye in
  palette); Abandon works; advance a day (DayNight) → available set rotates; F5/F9 → board + active +
  unlocks persist. `read_console` clean.
- [ ] **Step 3: Commit** `feat(contracts): authored contract catalog with plant/dye rewards`.

---

## Completion checklist
- [ ] Complete consumes inventory and is blocked without the items; rewards add coins + unlock plant/dye.
- [ ] Abandon removes a contract without reward.
- [ ] Board progress reflects inventory; available set rotates each day and excludes ineligible/active.
- [ ] EditMode: `ContractServiceTests`, `ContractRotationServiceTests` green; existing suite unbroken.
- [ ] Save/load round-trips available set + day index; active contracts + unlocks persist.

## Out of scope
- Building blueprints as rewards; procedural contract generation; contract timers/expiry.

## Execution order
```
T1 → T2 → T3 → T4  (loop + rewards + UI)  →  T5 → T6  (rotation + save)  →  T7 (content + verify)
```
