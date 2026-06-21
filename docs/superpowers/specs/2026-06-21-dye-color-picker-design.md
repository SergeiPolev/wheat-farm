# Dye Color Picker (Stage C) — Design

## Problem

The Dye tool can recolor crops (`DyeTool.SelectColor(Color)` + `PreviewCellColor` are
implemented and the brush preview already reflects the selected color), but there is no UI to
choose a color. The player cannot pick what color to paint with.

This expands the thin "Часть C" of `2026-06-12-freeform-buildings-and-path-textures-design.md`
(a flat preset palette) into a data-driven palette with a coin economy, per owner decision
(2026-06-21).

## Scope

In scope:
- Data-driven palette of ~8 dyes (`DyeData` ScriptableObjects + a `DyeDatabase`).
- **One-time unlock** economy: a dye is bought once with coins, then used freely forever.
- HUD palette shown **only while the Dye tool is active**; click to buy (if locked) or select.
- Persistence of unlocked dyes across save/load.

Out of scope (deferred):
- **Crafting** dyes from ingredients (`DyeData.RequiresCrafting` / `CraftIngredientIds` stay in
  the model, all `false`/empty for now) — a separate later sub-stage.
- Per-application cost, a free-form color wheel.

## Decisions (owner, 2026-06-21)

- Color source: **data-driven** via `DyeData` SO + `DyeDatabase` (matches `PlantDatabase` /
  `PlaceableDatabase` conventions), not hardcoded.
- Cost model: **one-time unlock** (not per-use, not consumable). A creative sandbox shouldn't
  punish painting large areas.
- Crafting: **deferred**; Stage C unlocks dyes with coins only.

## Architecture

### Data

- **`DyeData`** (exists, `WheatFarm.Core.Data`): uses `DyeId`, `DisplayName`, `Color`, `Cost`.
  `Cost == 0` ⇒ the dye is always unlocked (e.g. White = reset/free). `RequiresCrafting` stays
  `false` for all Stage C assets.
- **`DyeDatabase`** (new SO, like `PlantDatabase`): `DyeData[] Items`, `GetById(string)`,
  `IReadOnlyList<DyeData> All`. Registered in `GameScope` (SerializeField + RegisterInstance).
- **~8 `DyeData` assets** in `Assets/Settings/Dyes/`: White (Cost 0), then Red/Orange/Yellow/
  Green/Blue/Purple/Pink with escalating costs (≈20–40, tunable). A `DyeDatabase.asset` lists them.

### Service

- **`DyeUnlockService`** (`WheatFarm.Economy` or a new feature folder), registered in `GameScope`:
  - Reactive set of unlocked `DyeId`s (`ObservableHashSet<string>` or `ReactiveProperty` wrapper).
  - `bool IsUnlocked(DyeData)` — true if `Cost == 0` or in the unlocked set.
  - `bool TryUnlock(DyeData)` — if already unlocked: true; else `WalletService.TrySpend(Cost)` and,
    on success, add to the set and emit a change. Returns false if unaffordable.
  - Exposes an observable so the UI updates when a dye is unlocked or coins change.
  - **Save/load**: persists the unlocked id list (Cost 0 dyes are implicit, not stored).

### UI (MVP)

- **`DyeColorPaletteView`** (MonoBehaviour, built programmatically via `PanelBuilder`): a small HUD
  panel — a row of swatch buttons, one per `DyeData`. Each swatch shows its color; locked dyes are
  dimmed with a price label; the active dye is highlighted. Exposes a swatch-clicked event and
  methods to set per-swatch state (locked/price/selected) and panel visibility.
- **`DyeColorPalettePresenter`** (pure C#, `IInitializable`): injects `DyeDatabase`,
  `DyeUnlockService`, `IWalletService`, `IToolService`, and the `DyeTool`.
  - Subscribes to `IToolService.CurrentToolId` (R3): show the panel only when the Dye tool is
    active, hide otherwise.
  - Builds swatches from `DyeDatabase`.
  - On swatch click: if unlocked → `DyeTool.SelectColor(color)` and highlight; if locked →
    `DyeUnlockService.TryUnlock` (buy), and on success select it; if unaffordable → light feedback.
  - Re-renders swatch states when the unlocked set or wallet changes.
- Registered in `FarmScope`, attached to the HUD canvas like other presenters.

### Data flow

```
CatalogTabBar → equip Dye tool → ToolService.CurrentToolId = Dye
  → DyeColorPalettePresenter shows palette (from DyeDatabase, states from DyeUnlockService+Wallet)
Swatch click → Presenter → (locked) DyeUnlockService.TryUnlock → WalletService.TrySpend
                          → (unlocked) DyeTool.SelectColor(color)
  → FarmInteractionController brush preview reads DyeTool.PreviewCellColor (already wired)
```

## Testing

- **EditMode (logic)**:
  - `DyeUnlockService`: Cost 0 is always unlocked; `TryUnlock` spends coins and adds the id;
    insufficient coins → not unlocked, no spend; already unlocked → no double-spend; save→load
    round-trips the unlocked set.
  - `DyeDatabase`: `GetById` returns the right asset / null for unknown.
  - `DyeColorPalettePresenter`: with fake `IToolService`/`DyeUnlockService`/wallet — palette
    visible only when Dye tool active; clicking unlocked → `SelectColor` called; clicking locked →
    `TryUnlock` called; unaffordable → no select.
- **Play Mode (visual)**: equip Dye tool → palette appears; buy a color → coins deducted, swatch
  unlocks; select → brush preview shows the color; paint a crop; reload → unlock persists.

## Components & boundaries

| Unit | Responsibility | Depends on |
|------|----------------|------------|
| `DyeData` / `DyeDatabase` | Static dye catalog | — |
| `DyeUnlockService` | Which dyes are owned; buying; persistence | `IWalletService` |
| `DyeColorPaletteView` | Swatch rendering + input events | `PanelBuilder` |
| `DyeColorPalettePresenter` | Wire tool-active → visibility, clicks → unlock/select | `DyeDatabase`, `DyeUnlockService`, `IWalletService`, `IToolService`, `DyeTool` |
| Save extension | Persist unlocked dye ids | `DyeUnlockService` |
