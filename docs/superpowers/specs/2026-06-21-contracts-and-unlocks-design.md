# Contracts & Unlocks (Stage D) — Design

## Problem

The contract scaffolding exists but the loop is broken as a progression engine:

- `ContractService.TryCompleteContract` pays `CoinReward` but **never applies `UnlockPlantId`** — the
  headline reward (new seeds) is dead. No dye-reward path either.
- Fulfillment is harvest-only and non-consuming: `HarvestRewardHandler` calls
  `ContributeItem(plantId, 1)` on harvest, which fills progress but doesn't remove items, and
  production goods (e.g. flour) never progress a contract.
- The board is static with 3 inline contracts; `ContractStarter` is an empty hook (no rotation).

Goal: make contracts the optional-goal progression spine — deliver items, earn coins + unlock new
plants/dyes, on a board that refreshes daily.

## Decisions (owner, 2026-06-21)

- **Fulfillment:** deliver from inventory on **Complete** — Complete checks the inventory holds all
  `Required` items, consumes them, then grants the reward. Works for crops and produced goods alike.
  The harvest auto-contribute is removed; board progress is derived from current inventory coverage.
- **Rewards:** coins + **plant unlock** (`UnlockPlantId` → `PlantUnlockService`) + **dye unlock**
  (`UnlockDyeId` → `DyeUnlockService`, the stage-C service). Contracts are the "earn by effort"
  path to new seeds and colors.
- **Board:** **daily rotation** — the available set refreshes each day (Dawn). Accepted contracts
  persist until completed or abandoned.

## Architecture

### Data

- **`ContractData`** (plain serializable class inside `ContractDatabase.asset`): existing fields
  `ContractId, Description, Required (ItemStack[]), CoinReward, UnlockPlantId, RewardMultiplier`
  plus new **`UnlockDyeId`** (string; empty = none).
- **`ContractDatabase.asset`**: expand the inline `Contracts[]` pool to ~10–15 contracts spanning
  crops and produced goods, each rewarding coins and (often) a plant or dye unlock.

### Service (`ContractService`)

- Inject `IPlantUnlockService` + `IDyeUnlockService` (in addition to `IWalletService`,
  `IInventoryService`).
- `TryCompleteContract(index)`:
  1. Verify the inventory holds every `Required` ItemStack (amount-wise).
  2. Consume them from inventory.
  3. `_wallet.Add(CoinReward)`.
  4. If `UnlockPlantId` set → `PlantUnlockService.Unlock(id)`; if `UnlockDyeId` set →
     `DyeUnlockService.TryUnlock`/`Unlock` (free grant — no coin cost on contract reward).
  5. Fire `OnContractCompleted`; remove from active.
  - Returns success/failure (false if inventory insufficient) so the UI can gate the button.
- Add `AbandonContract(index)` — removes an active contract without reward (per "держится, пока сам
  не откажешься").
- Remove `ContributeItem` and the `HarvestRewardHandler` hook (progress is now inventory-derived).
- `bool CanComplete(ActiveContract)` / coverage helper for the UI.

### Rotation (`ContractRotationService`, new)

- `IStartable` + subscribes to `IDayNightService.CurrentPhase`; on transition to `Dawn` (new day),
  rebuild the **available** set.
- Selection: pick N (≈3–4) random **eligible** contracts from the database. Eligible =
  reward not already owned (plant/dye not yet unlocked) AND requirements are obtainable (required
  item ids come from already-unlocked plants/known produced goods). Exclude contracts already active.
- Exposes the available set (e.g. `ObservableList<ContractData> Available`) consumed by the board.
- **Persistence:** the available contract ids + a day index persist (so a reload doesn't reshuffle).
  Active contracts already persist (`FarmSaveData.ActiveContracts`).

### UI (`ContractBoardPresenter` — mostly exists)

- Available list reads from `ContractRotationService.Available` (not the whole database).
- Active list: progress per required item = `min(inventoryAmount, requiredAmount)`; Complete enabled
  only when all requirements are covered.
- Show the reward: coins + plant/dye name (icon/color swatch where available).
- Add an Abandon button on active entries.

### Persistence

- `FarmSaveData`: add `AvailableContractIds (List<string>)` and `ContractDayIndex (int)`.
- `FarmSaveManager`: collect/restore them via `ContractRotationService` (mirrors UnlockedDyes wiring).

## Data flow

```
Dawn (DayNightService.CurrentPhase) → ContractRotationService rebuilds Available (eligible, N)
Board → AcceptContract → ActiveContracts (persists)
Grow/produce → items land in InventoryService
Board Complete → ContractService verifies+consumes inventory → coins + PlantUnlock/DyeUnlock
              → OnContractCompleted → remove active
Board Abandon → remove active (no reward)
```

## Testing (EditMode)

- `ContractService`: Complete with sufficient inventory consumes items + pays coins + unlocks the
  plant and dye; Complete with insufficient inventory does nothing and returns false; Abandon removes
  without reward.
- `ContractRotationService`: selection excludes already-active and already-unlocked-reward contracts;
  excludes contracts requiring items from locked plants; respects N; save/load round-trips the
  available set + day index.
- UI presenter (fakes): Complete disabled until covered; reward text reflects plant/dye/coins.

## Components & boundaries

| Unit | Responsibility | Depends on |
|------|----------------|------------|
| `ContractData` / `ContractDatabase` | Contract catalog (+UnlockDyeId) | — |
| `ContractService` | Accept / complete (consume+reward) / abandon | `IWalletService`, `IInventoryService`, `IPlantUnlockService`, `IDyeUnlockService` |
| `ContractRotationService` | Daily eligible available set + persistence | `ContractDatabase`, `IDayNightService`, unlock services |
| `ContractBoardPresenter`/`View` | Board UI: available/active, progress, complete/abandon | `IContractService`, `ContractRotationService` |
| Save extension | Persist available set + day index | `ContractRotationService` |

## Out of scope

- Building blueprints as rewards (no building-unlock system exists — separate feature).
- Procedural contract generation (we select from an authored pool).
- Contract expiry/timers beyond the daily available-set refresh.
