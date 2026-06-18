Explore input selection patterns

## Analysis: User Input Patterns in the UI State Machine

The codebase has **5 distinct patterns** for collecting additional user input, with meaningful inconsistencies between them. Here's a structured breakdown:

---

### Patterns Found

#### Pattern A: Modal Overlay + ViewModel Flag + Command
Used by **Direction/Facing Selector** and **Aimed Shot Location Selector**

Flow:
1. State calls `_viewModel.ShowXxx(...)` → sets `IsXxxVisible = true` + data property
2. View renders overlay
3. User selects → View calls a Command on ViewModel → `IUiState.HandleXxxSelection(value)`
4. State processes result, calls `_viewModel.HideXxx()`

Properties on ViewModel:
```
// Direction selector
bool IsDirectionSelectorVisible
HexCoordinates? DirectionSelectorPosition
IEnumerable<HexDirection>? AvailableDirections

// Aimed shot selector
bool IsUnitPartSelectorVisible
AimedShotLocationSelectorViewModel? UnitPartSelector
```

Entry point (uniform): `IUiState.HandleFacingSelection(HexDirection)` via `DirectionSelectedCommand`

#### Pattern B: `AvailableActions` Collection with Embedded Callbacks
Used by **Movement Type Menu** and **Attack Phase Action Menu**

Flow:
1. State's `GetAvailableActions()` returns `IEnumerable<StateAction>` with action lambdas
2. View binds list and invokes `StateAction.OnExecute` directly — **no ViewModel intermediary**
3. Callback captured state directly in closure

#### Pattern C: Dedicated ViewModel Wrapper + Injected Delegate
Used by **Weapon Selection Panel**

Flow:
1. Enemy hex tap → `WeaponsAttackState` sets `SelectedTarget`, sets `IsWeaponSelectionVisible = true`, populates `WeaponSelectionItems`
2. View renders the weapon panel
3. Each `WeaponSelectionViewModel.IsSelected` toggle fires `_onSelectionChanged(weapon, value)` delegate → back into state
4. `TargetSelectionViewModel.SetAsPrimary` fires another delegate

---

### Gaps and Inconsistencies

| Issue | Location | Description |
|---|---|---|
| **Cancellation asymmetry** | Direction vs. Aimed Shot | Direction selector is cancelled implicitly (tapping off-range hex) or by reset. Aimed Shot has an explicit `HideBodyPartSelectorCommand`. No consistent cancel contract. |
| **Result delivery divergence** | Pattern A vs. B | Direction/AimedShot results go through ViewModel command → `IUiState` method. Action menu results bypass `IUiState` entirely via closure callbacks — `HandleFacingSelection`, `HandleHexSelection`, `HandleUnitSelectionFromList`, `ExecutePlayerAction` are the official `IUiState` hooks, but action callbacks call internal state methods directly. |
| **Aimed shot triggered by View, not State** | `WeaponSelectionViewModel` | The aimed shot selector is opened by the **View** calling `ShowAimedShotSelector()` on a wrapper ViewModel — state is never notified. This is inverted compared to every other pattern where the **State** initiates the overlay. |
| **Weapon panel visibility gating** | `BattleMapViewModel` L188 | `IsWeaponSelectionVisible` is a computed expression (step == TargetSelection AND target != null AND field), not a simple field set by the state — unlike the other two boolean flags which are set imperatively. |
| **`SelectingStandingUpDirectionStep` position** | `MovementState` | Shows direction selector at the unit's *current* position (not a target hex). Structurally it's Pattern A, but semantically distinct — the position meaning differs. This is consistent in implementation but not in meaning. |
| **No async/await** | All patterns | All patterns are synchronous callback-driven. This is internally consistent, but worth noting as a deliberate constraint. |

---

### Proposed Alignment Plan

**Goal**: Every "additional input needed" scenario should follow Pattern A — State initiates, ViewModel signals, View renders, result returns via a named `IUiState` method.

#### Step 1: Fix Aimed Shot initiation (high priority)
The selector is currently opened by the View/wrapper ViewModel. Move initiation into `WeaponsAttackState`:
- Add `HandleWeaponAimedShotRequest(Weapon)` to `IUiState` (or handle via an existing hook)
- State calls `_viewModel.ShowAimedShotLocationSelector(...)` instead of the View doing it
- Keep `AimedShotLocationSelectorViewModel.SelectPart` as the result path (already goes via callback → wrapper → state, which is acceptable since it's scoped)

#### Step 2: Standardize cancellation contract
Define a consistent cancel path for all overlays. Options:
- Add `CancelCurrentSelection()` to `IUiState` — the View calls this on any explicit dismiss
- Or add `HandleCancelInput()` that each state implements to reset its current sub-step

#### Step 3: Evaluate action menu bypass of IUiState
`StateAction` callbacks currently call state internals directly. Decide whether this is acceptable or if actions should route through `ExecutePlayerAction(actionId)`. If the action menu is considered a "meta" selection (not user map input), Pattern B may be intentionally different and worth keeping as-is with explicit documentation.

#### Step 4: Normalize `IsWeaponSelectionVisible`
Change from a computed expression to an imperative flag (set by state via `ShowWeaponSelectionPanel()` / `HideWeaponSelectionPanel()`), matching the direction and aimed shot flags pattern.

---

**Biggest gap**: Aimed shot selector initiation (Step 1) is the most structurally inconsistent — it's the only case where the View/wrapper opens an overlay without the state's knowledge. Everything else is debatable style; this is a real architectural inversion.
