### Unconscious MechWarrior Effects Checklist
Version: **0.41.x**

#### 1. Mech Status: Immobile and Inactive

* [x] Set the unit movement state to **immobile**.
* [x] Prevent all **movement actions** (walk, run, jump, etc.).
* [x] Prevent all **firing/actions** (no weapons fire, no physical attacks, etc.).
* [x] Allow player to **declare "No Movement"** in the movement phase (for initiative mechanics or targeting).
* [x] Allow player to **Skip Attack** in the weapon attack declaration phase.
* [x] Display a status icon or flag for "Pilot Unconscious".

#### 2. Skill Roll Handling

* [ ] Automatically **fail all Piloting Skill Rolls**.

#### 3. Falling Damage

* [ ] If the 'Mech **falls while the pilot is unconscious**, apply normal falling damage.
* [ ] Automatically apply **pilot damage from fall** (no roll to resist).

#### 4. Targeting Modifiers
* [ ] Allow **aimed shots** (e.g. called shots to location) against the unit.
* [ ] Adjust hit location modifiers accordingly (e.g. ignore movement modifiers, apply +4 for immobile).