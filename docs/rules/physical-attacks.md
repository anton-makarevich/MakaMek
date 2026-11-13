**Declaration and resolution for physical attacks follow the same two-step principle as weapon attacks, with one notable exception regarding specific movement-based attacks.**

---

### **1. Do Physical Attacks Work Like Weapon Attacks? (Declaration & Resolution)**

**Yes, with a key exception for Charges and DFAs:**

* **Phase Sequence:** Physical attacks take place in Phase 5 (**Physical Attack Phase**), which immediately follows the Weapon Attack Phase.
* **Declaration:** Players alternate declaring physical attacks unit by unit (following Initiative / Unequal Numbers rules). 
* **Resolution Order & Damage:** All declared physical attacks are resolved during the Physical Attack Phase. Attacks resolve from the **lower Initiative to the higher** (Total Warfare p. 144), so a charge declared during the Movement Phase keeps its attacker's slot rather than always resolving last. Each attack's damage and **displacement** take effect immediately, before the next attack resolves (Total Warfare p. 151); only a mutual push pair waits for both rolls, because if both hit neither unit moves (p. 148). Any required Piloting Skill Rolls (PSRs) resulting from physical attacks are rolled at the end of the phase.
* **The Exception (Charge & Death From Above):** Unlike punches, kicks, or clubs—which are declared during the Physical Attack Phase—**Charge** and **Death From Above (DFA)** attacks must be declared during the **Movement Phase**. This is because these attacks dictate how and where the unit moves across the map. However, their actual damage resolution still takes place in the Physical Attack Phase.

---

### **2. Core Physical Attack Rules & Modifiers**

* **Base To-Hit Number:** Equal to the pilot’s **Piloting Skill Rating** (unlike weapon attacks, which use Gunnery Skill).
* **Modifiers:** The modified to-hit number is the base to-hit number plus the attack-type modifier, plus **all standard weapon-attack modifiers** — attacker movement, target movement, target state, and terrain — unless a rule for the specific attack type says otherwise. The only exceptions are heat and sensor-damage modifiers, which never apply. Relative Piloting Skill modifiers apply to Charges and DFAs.
  * **DFA terrain exception:** a DFA applies all the normal attack modifiers, including the attacker's jumping movement, but **the roll is not modified for terrain** (Total Warfare p. 144, Attack Modifiers Table footnote). Woods, partial cover and any other terrain-derived modifier are therefore excluded from the DFA to-hit number; target movement (including the +1 for jumping/airborne targets) and target-status modifiers still apply.
* **Target-Side Modifiers (apply to all attacks, weapon and physical; partial cover does not apply to a DFA):**
  * **Partial cover +1** — the target is partially concealed (see Line of Sight, p. 99). Partial cover only applies to standing 'Mechs. 'Mechs in **Depth 1 water** receive partial cover. An attacker firing downhill negates the target's partial cover.
  * **Skidding target +2** — a skidding ground unit is easier to hit for the remainder of the turn it skids.
  * **Prone target** — –2 when attacking from an adjacent hex, +1 from all other hexes.
* **Attacker-Side Modifiers (apply to all attacks, weapon and physical):** the attacker movement rows of the Attack Modifiers Table (stationary / walked / ran / jumped / prone / skidding) are **cumulative**, not exclusive. A **skidding** attacker adds **+1** on top of its movement-type modifier (Total Warfare p. 62: the skidding modifiers are "in addition to all the standard attacker and target movement modifiers"). Because a skid begins with a fall, a skidding 'Mech is also **prone**, so a running 'Mech that skids attacks with **+2 (ran) +1 (skidding) +2 (prone) = +5**. The same rule applies to weapon and physical attacks. Four-legged 'Mechs (prone exemption) and unintentional charges (skidding exemption) are not modelled.
* **Heat & Sensors:** **Heat build-up and sensor damage modifiers NEVER apply to physical attacks**.
* **Single Attack Limit:** A ’Mech may only perform **one type** of physical attack per turn (e.g., a ’Mech cannot kick and punch, or use a hatchet and kick in the same turn).

---

### **2.1. Actuator Damage — To-Hit Penalties & Restrictions**

Attacker 'Mech damage that specifically affects physical attacks (Attack Modifiers Table, p. 119):

| Damage | Effect on physical attacks |
| :--- | :--- |
| **Shoulder actuator hit** | No punching or physical weapon attack with that arm; no clubbing attacks; **+2 to a pushing attack (each)** |
| **Upper or lower arm actuator hit (each)** | **+2 to punching and physical weapon attacks with that arm**; half damage for a punching attack with that arm (round down, to a minimum of 1); +2 to clubbing attacks. An arm actuator the design does not mount at all counts as damaged for punch damage. |
| **Hand actuator hit** | **+1 to a punching attack with that arm**; no clubbing attacks; no physical weapon attack with that arm |
| **Hip actuator hit** | No kicking attacks |
| **Upper or lower leg actuator hit (each)** | **+2 to all kick attacks**; half damage to kick attacks made with that leg (round down, to a minimum of 1) |
| **Foot actuator hit** | **+1 to all kick attacks** |

A 'Mech punching with one or two arms resolves each arm separately, so the arm-actuator penalties apply per punching arm. A 'Mech making a punch or push attack with an arm that mounts a **salvage arm** treats that arm as though it has no hand actuator. A club requires hands (or hand actuators) on both arms, and 'Mechs with damaged upper or lower arm actuators add +2 to the to-hit number for each damaged actuator.

---

### **3. Summary of 'Mech Physical Attack Types**

| Attack Type | When Declared | Base Modifier | Damage Value | Hit Location & Notes |
| :--- | :--- | :--- | :--- | :--- |
| **Punch** | Physical Phase | `+0` | Tonnage / 10 *(round up)* | Uses **Punch Location Table** (head/torsos/arms). Can punch with 1 or 2 arms (separate roll each) if no weapons were fired from those arms. A damaged **shoulder** actuator blocks punching with that arm; damaged upper/lower **arm** actuators add **+2 to-hit each** (and halve the punch damage, rounding down to a minimum of 1) and a damaged **hand** actuator adds **+1 to-hit** (see §2.1). |
| **Kick** | Physical Phase | `-2` | Tonnage / 5 *(round up)* | Uses **Kick Location Table** (legs). The **kicking leg** must not have fired any weapons mounted in it during the Weapon Attack Phase (rule: *"No weapons mounted on a kicking leg can fire in the turn in which a 'Mech kicks"*). The restriction is **per-leg**: firing a right-leg weapon only disqualifies the right leg; the left leg may still kick. Both **hip actuators** must be undamaged, and the target must be in one of the three forward-arc hexes based on the kicking foot's orientation. Damaged upper/lower **leg** actuators add **+2 to-hit each** (and halve the kick damage when kicking with that leg, rounding down to a minimum of 1) and a damaged **foot** actuator adds **+1 to-hit** (see §2.1). On a hit, the target must pass a PSR or fall; on a miss, the attacker must pass a PSR or fall. |
| **Club** | Physical Phase | `-1` | Tonnage / 5 *(round up)* | Uses **Standard Hit Location Table**. Requires two undamaged hand/shoulder actuators and no arm weapons fired. Clubs can be severed limbs, trees, or girders. |
| **Push** | Physical Phase | `-1` | `0` *(No damage)* | Target must be a **standing ’Mech** at the **exact same elevation** in the hex directly in front of the attacker's **feet** (not torso twist); cannot be performing a Charge or DFA, and a unit may receive only one Charge/DFA/Push per turn. Requires **both arms** and **no arm-mounted weapons fired** that turn (head/torso/leg weapons allowed). Damaged shoulder actuators do **not** block the push but add **+2 to-hit per damaged shoulder**. On a hit, target is pushed 1 hex directly away, attacker advances into the vacated hex at **0 MP cost**, and the target must pass a PSR or fall. An occupied destination triggers a domino effect (§4). Blocked/prohibited destination prevents movement but not the PSR. See §4 for full requirements. |
| **Physical Weapon** | Physical Phase | Varies *(e.g., Hatchet -1, Sword -2)* | Varies *(e.g., Hatchet = Tonnage / 5, round up; Sword = Tonnage / 10, round up, + 1)* | Uses **Standard Hit Location Table** (or Punch/Kick table with a +4 modifier). Cannot fire weapons on the weapon arm. |
| **Charge** | **Movement Phase** | `+0` *(± Piloting diff.)* | **Target:** (Attacker Tonnage / 10) × Hexes Moved, then *round up*.<br>**Attacker:** Target Tonnage / 10 *(round up)*. | Damage in 5-pt clusters. **Hexes Moved** counts only hexes traversed *before* entering the target's hex. The target must be **directly in front of the charging unit's feet** at the start of the Physical Attack Phase (torso twists disregarded), so a backward entry into its hex is not a charge; a shut-down attacker cannot declare one. Displaces the target one hex in the charge direction (an occupied destination triggers the domino effect). If that hex is prohibited terrain, off the map, or an occupant cannot be dislodged, **neither unit moves** while damage and PSRs still apply. **On hit:** both units must pass a **PSR (+2 modifier)** or fall. **On miss:** the attacker lands in the hex to the left or right of the attacker's forward arc (never a rear or rear-side hex), no damage is taken or dealt, and **no charge PSRs are rolled**. If the declared target is gone at resolution (prone, destroyed or displaced) and no replacement enemy occupies the hex, the charge **automatically fails**: no to-hit roll and no damage, and the attacker lands in a side hex of its forward arc exactly as on a miss — a failed charge never ends in the target hex. A pending-DFA 'Mech is never a valid target (§3.1.1) and, being airborne, is not a ground occupant, so it does not count for stacking. No weapon attacks allowed. |
| **Death From Above (DFA)** | **Movement Phase** | `+0` *(± Piloting diff. + Jump mod; **no terrain modifier**)* | **Target:** (Attacker Tonnage × 3) / 10 *(round the final result up)*.<br>**Attacker:** Attacker Tonnage / 5 *(round up)* to legs **(on hit only)**. | Damage in 5-pt clusters on **Punch Location Table** for target. **On hit:** attacker lands in target hex, target is displaced; both make PSRs (Target +2, Attacker +4). **On miss:** the attacker **automatically falls** (2-level fall, damage applied to the rear); the target **does not roll a PSR** and instead is displaced to an adjacent passable hex of its choice. No weapon attacks allowed. The to-hit roll applies all normal attack modifiers (attacker jumping movement, target movement, target status) **except terrain** (p. 144 footnote). |

---

### **3.1. Death From Above — Line of Sight During the Weapon Attack Phase**

A DFA attacker does not enter the target hex while the weapon attacks are declared. It waits in
the approach hex (the last hex of the jump path, adjacent to the target) and is considered
**airborne** for line of sight until the attack resolves (Total Warfare p. 150; BattleMech Manual
6th printing p. 37; official rules answers, forum topics 82519 and 81876).

**Formula:**

```text
airborne base (feet) = max(reference level of the declared target hex,
                           reference level of the approach hex) + 2
effective LOS level  = airborne base + unit.Height
                     = max(...) + 2 + unit.Height
```

* The `+2` is the attacker's **mid-air elevation**: the rule says the attacker is "in the air above
  the hex, standing two levels higher than either the target hex or the level of the hex the
  attacker occupies". Its feet are planted two levels above the higher hex. It is **not** the
  standing height — the normal LOS calculation adds `unit.Height` on top. A standing 'Mech
  therefore has an LOS level of `max(...) + 4`.
* Official errata clarification (forum 82519): "The attacking mech would be at Level 3 (the
  target's level of 1, +2 because the attacker is in mid-air)… For those firing at the attacking
  mech, they would be dealing with an LOS level of 5… because the DFA'ing mech's feet are
  'planted' in mid-air at Level 3, but it's also two levels tall like any other standing mech."
  The BattleMech Manual example says the same: base level 3, LOS level 5.
* The earlier July-2023 answer in forum 81876 ("LOS level of 5, its own two plus 3") conflates the
  attacker's level with its LOS level; it contradicts the BMM example and the later clarification.
  The corrected errata reading above governs. The AGoAC 1st-printing wording ("standing two levels
  higher") is consistent with the corrected base reading.
* The declared target hex is `MovementTaken.Destination` (coordinates and surface), not the target
  unit's current position: the target can be displaced or destroyed before the DFA resolves.
* The airborne base is stored on `Unit.Position.AirborneLevel`; every LOS consumer adds the unit or
  mount height, exactly as for a grounded unit's standing level. This makes the level independent
  of which unit is shooting and works in both directions of `GetLineOfSight`.
* Example: target hex at Level 3, approach hex at Level 0, standing 'Mech → base
  `max(3, 0) + 2 = 5`, LOS level `5 + 2 = 7`.

**Reference level of a hex:** the bridge deck when the hex has a bridge, otherwise the raw
`Hex.Level`. Water therefore contributes its **surface**, never the riverbed (`GetStandingLevel`
would return the bottom and is deliberately not used). Building levels are not supported: the map
has no building terrain, so a building reference level is deferred to the change that adds
buildings.

**Authoritative recomputation:** `BaseGame.OnMoveUnit` computes the approach position's
`AirborneLevel` from the map for every completed DFA move, on both the server and the clients.
`MovementTaken.Destination` supplies the declared target hex. The map's jump profile stores the
same `max(...) + 2` base on the approach waypoint (the rules provider supplies the offset to the
map jump-profile calculation through `IRulesProvider.GetDfaAirborneBaseOffset()`), so the
path and the derived level agree. The submitted waypoint level is not trusted: legacy payloads
have none and a client can forge one. The submitted path segments, `MovementTaken`, MP and
`HexesTraveled` are left unchanged, so TMM and attack modifiers are unchanged. No new wire field
is added.

**Decisions and edge cases:**

* **Prone attacker:** a failed jump-landing PSR (or another weapon-phase fall) sets
  `UnitStatus.Prone` but does not cancel the pending DFA; it resolves as a miss in the Physical
  Attack Phase and the attacker then falls. `Mech.Height` therefore keeps the **standing** height
  while `Position.AirborneLevel` is non-null, so the LOS level does not silently drop by one level.
* **Damaged / immobile status:** does not change the formula. The DFA attacker is already limited
  by the declaration validator (jump jets, MP, submerged attacker, target reservation).
* **Cover:** an airborne unit can never claim partial cover (`Mech.CanHavePartialCover`); a target
  that is airborne is not partially concealed by the terrain it hovers over.
* **Physical reach:** uses the airborne base level. The endpoint resolves `Position.AirborneLevel` when it is set, so the DFA attacker's feet are `max(approach, target) + 2` and the Different-Levels table sees that difference. The airborne level is the unit's base (feet) level; LOS adds the unit height on top, reach does not. An airborne endpoint is never submerged or in water.
* **Water eligibility:** unchanged. Whether a DFA may be declared at all (including the
  submerged-attacker rejection) is decided before the move, when the attacker is grounded.
* **Lifecycle:** the airborne value ends deterministically when the pending movement attack ends —
  on attacker destruction and on the turn reset (`ResetTurnState`). The DFA resolution slice grounds
  the attacker through the displacement pipeline: a hit lands it in the target's original hex, a
  miss or a destroyed target lands it there before the automatic two-level rear fall, and an
  auto-fail lands it in the declared target hex (the jump destination) without damage or a fall.
  The declaration itself remains turn-scoped and is cleared by `ResetTurnState` at the end of the
  turn.
* **Serialization / reconstruction:** `Unit.ToData` exports the airborne position through the
  optional `HexPositionData.AirborneLevel`; the declaration itself is not exported. `MechFactory`
  and `CloneUnit` do not restore the position, so a reconstructed unit is undeployed and never
  airborne.

**Known gaps (tracked as follow-ups):**

* **Approach-hex choice** on a divided jump path: the movement model stores a single stop hex (the
  `From` of the last translation). The rule lets the attacker choose between two eligible approach
  hexes.
* **Facing** toward the target: the stop position keeps the facing of the last translation rather
  than being turned toward the declared target hex.
* `MoveUnitCommand.GetPayloadHash` omits the surface and `AirborneLevel`; the value is derived, so
  hashing it is not required for correctness today.

---

 ### **3.1.1. DFA Target Immunity (Total Warfare p. 149)**

A 'Mech with a pending Death From Above **cannot be the target of any physical attack** while its
movement is unfinished: Punch, Kick, Push, Charge and another DFA are all illegal against it. The
rule is enforced once in the shared `PhysicalAttackEligibility` policy, so server declaration
validation, phase enrollment, the resolution-time `CanResolve` gate and the UI all apply it. The
physical-attack rows report `TargetPerformingDeathFromAbove` ("The target is performing Death From
Above"). A declaration made before the target becomes a DFA performer is skipped at resolution
instead of being resolved.

* The 'Mech **remains a legal weapon target** during the Weapon Attack Phase, at its airborne LOS
  level (see §3.1 above).
* Physical reach **uses the airborne base level**: the airborne level is resolved as the unit's
  elevation, so a DFA performer is normally two levels above the higher adjacent hex and out of
  reach for Punch and Kick, and at a different elevation for Push. The p. 149 immunity remains the
  authoritative rule and still blocks the attack when terrain puts the airborne level within reach.
* A 'Mech performing a **Charge** is not immune to physical attacks. Punch and Kick remain legal
  against it when the reach rules permit them; Push keeps the
  `TargetPerformingDisplacementAttack` restriction, and another Charge or DFA is rejected by
  `MovementAttackDeclarationValidator`.
* **Charge resolution (issue [#1706](https://github.com/anton-makarevich/MakaMek/issues/1706)):**
  `ChargeTargetSelector` applies the immunity at resolution time too. A pending-DFA 'Mech is never
  the declared target and never a replacement target, so when it is the only occupant of the
  committed destination hex the charge automatically fails: no to-hit roll, no damage and no charge
  PSRs. A failed charge never ends in the target hex (Total Warfare p. 148): the attacker is placed
  in the hex to the right or left of its forward arc, exactly like a rolled miss, whether the hex is
  empty or still holds a grounded unit. The DFA performer is **airborne**, so it is not a ground
  occupant: it does not count for stacking, and cannot be displaced while it is in the air.
* **Airborne stacking exemption in every displacement path:** `IsAirborneDfaAttacker`
  (`HasPendingMovementAttack` + `IsDfa` + an `AirborneLevel` on the position) is excluded from the
  occupant checks of `ChargeDisplacementResolver`, `PushDisplacementResolver` and the shared
  `DominoEffectResolver`. A unit displaced into a hex that only an airborne DFA 'Mech holds
  therefore enters it without a domino, and a domino chain stops there instead of pushing the
  airborne 'Mech. Standard stacking applies again only once the DFA resolves and the attacker
  lands; the domino is then evaluated from the final landing positions.

---

### **3.2. Movement-Phase Declaration UI (issue #1703)**

Charge and DFA are declared while the unit moves, so the Movement Phase owns the declaration and
`MovementState` drives it. It uses the same `MovementAttackDeclarationValidator` instance as the
server (`IGame.MovementAttackDeclarationValidator`), so a highlighted target is exactly one the
server accepts.

1. The player selects a unit and a movement type (Walk/Run for Charge, Jump for DFA).
2. Reachable hexes are highlighted as usual. In addition, every enemy 'Mech that has completed its
   movement and can legally be the target is highlighted with `AttackReachableHighlight` and the
   attack-type label. A target is only highlighted when at least one path into its hex passes the
   shared validator.
3. Clicking a highlighted enemy opens the existing physical attack declaration panel, built from
   `IPhysicalAttackDeclarationState`. The UI computes all paths into the target hex, keeps the
   cheapest legal one, and shows it as the movement preview. Clicking an ineligible enemy is ignored
   so the current movement selection survives.
4. No direction selector is shown: the chosen path already fixes the approach hex and facing. A
   Charge always faces the target through its entering segment; a DFA does not depend on facing.
5. The preview lists the to-hit number, hit probability, modifier breakdown, the hexes moved before
   entering the target hex, target and attacker damage, PSR modifiers and the miss outcome, plus the
   reminder that the unit cannot make weapon or physical-phase attacks this turn. The DFA preview
   uses the airborne approach level, exactly as `BaseGame.OnMoveUnit` re-derives it after the move.
6. The generic player action button confirms (`Declare Charge` / `Declare Death From Above`); the
   panel's close button cancels without moving the unit. Cancelling returns to the step the
   declaration was opened from: target selection while no leg of the path has been confirmed yet,
   otherwise the movement confirmation step, with the partial path still visible and confirmable.
   Confirmation revalidates the declaration before publishing one `MoveUnitCommand` with the
   declaration.

The panel is the existing `PhysicalAttackDeclarationPanel.axaml`; a movement attack row has no limb
options, so the panel hides the limb area and shows the attack row, hit probability and info flyout.
The panel's properties on `BattleMapViewModel` are sourced from `IPhysicalAttackDeclarationState`,
which both `PhysicalAttackState` and `MovementState` implement.

**Target-jump modifier:** a target that jumped this turn, including a pending DFA attacker waiting
airborne in its approach hex, now adds the cumulative +1 to physical attacks
(`JumpedTargetModifier`). This closes the gap recorded in the previous section.

---

### **4. Push Attack — Full Requirements & Resolution**

**Target & elevation prerequisites:**

* **Valid targets only:** a push can only be made against another **standing ’Mech**. It cannot be performed against combat vehicles, ProtoMechs, conventional infantry, battle armor, buildings, or **prone** ’Mechs.
* **Target activity restriction:** the target ’Mech cannot be performing a **Charge** or **Death From Above (DFA)** attack during that turn.
* **Facing & orientation:** the target must be in the single hex **directly in front** of the attacker, determined strictly by the orientation of the attacker’s **feet** (not by a torso twist).
* **Elevation / level:** attacker and target must be at the **exact same elevation level**. Pushes are prohibited if the target is 1 level higher or lower.
* **Single displacement limit:** a unit may only be the target of **one** Charge, DFA, or Push attack in a single turn. Likewise, only one push attack can be declared against a single target per turn.

**Attacker requirements & weapon restrictions:**

* **Requires both arms:** pushing requires the use of **both arms**.
* **No arm weapons fired:** the attacker cannot have fired any **arm-mounted weapons** during the Weapon Attack Phase of that turn. Weapons mounted in the head, torso, or legs may be fired normally without restricting the push.
* **Actuator damage:** unlike punches or clubs (which are blocked by a damaged shoulder actuator), a ’Mech with a damaged shoulder actuator **can still attempt a push**, but incurs **+2 to-hit per damaged shoulder**.
* **Retractable blade:** if a retractable blade is mounted in an arm, that ’Mech cannot push while the blade is extended.
* **Salvage arm:** an arm mounting a salvage arm treats push attacks as though the arm lacks a hand actuator.

**Attack resolution & outcome:**

* **Timing:** declared and resolved during the Physical Attack Phase.
* **Base to-hit & modifiers:** uses the attacker’s **Piloting Skill Rating** with a base modifier of **–1**. Standard attacker/target movement and terrain modifiers apply. Heat build-up and sensor damage modifiers **never** apply (see §2).
* **Damage:** inflicts **0 points of damage**.
* **Displacement & advance:** on a hit, the target is pushed 1 hex directly away into the adjacent hex; the attacker automatically advances into the vacated hex at **0 additional MP cost**.
* **Mandatory PSR:** the pushed target must immediately pass a **Piloting Skill Roll or fall prone**.
* **Water:** entering water does not add a second roll. The standard water-depth modifier (−1 for Depth 1, 0 for Depth 2, +1 for Depth 3+) applies to the push PSR itself.
* **Ascent:** an upward displacement is allowed up to the displaced unit’s single-hex level-change limit (`MaxLevelChangeForward` — 2 levels for a ’Mech). An ascent of 3 or more levels is prohibited terrain: **neither unit moves**, but all other effects occur — including the defender’s mandatory PSR.
* **Prohibited terrain / blocked hexes:** if the destination hex is prohibited terrain (including an ascent above the unit’s level-change limit), **neither unit moves**, but all other effects occur — including the defender’s mandatory PSR. Downward displacement is unrestricted: being pushed into an empty hex more than 2 levels lower is allowed and results in an automatic fall.
* **Attacker advance:** the attacker must be able to legally enter the vacated hex and must not drop more than the automatic-fall threshold. If either condition fails, the whole push is blocked — neither unit moves and the push PSR still occurs. This extends the “neither unit moves” rule by analogy with Total Warfare p. 151.
* **Occupied destination (domino effect):** when the destination hex already holds a deployed unit, the push triggers a **domino effect** (Total Warfare p. 151–153). A 'Mech that is airborne for a pending Death From Above does **not** count as an occupant (§3.1.1): the destination is treated as empty and the airborne 'Mech cannot be displaced while it is in the air. The occupant is forced out one hex directly away from the attacker, and the chain continues while the next hex is occupied. The pushed target then moves into the vacated hex and the attacker advances. The **first** link decides whether anything moves: if the entering unit cannot reach the hex, or the occupant cannot be forced out of it (off the map, unstandable terrain, an ascent above the unit’s single-hex climb limit — a ’Mech can be forced up 2 levels), **no unit moves** — the push is blocked as above and the target still rolls its push PSR. A link that fails **later** in the chain does **not** rewind it: the displacements already resolved stay applied, the blocked unit keeps its hex and now shares it with the unit displaced into it, so both roll a PSR. Direct pushes and domino links use the same forced-entry check; the domino fall threshold stays at 2 or more levels. Chain links use `DominoEffect`; the target and the attacker keep `Push`/`PushAdvance`.
* **Domino falls:** a unit forced down two or more levels falls. Into an **empty** hex it falls normally after the displacement (falling damage = tonnage/10 rounded up × (levels fallen + 1)). Into an **occupied** hex the deeper drop is an **accidental fall from above** rather than a domino; the full rule (to-hit roll and damage to both units) is deferred, so it currently blocks the chain instead.
* **Domino PSRs:** every unit involved in the domino — the pushed target included — rolls a mandatory PSR immediately once the displacement is complete (Total Warfare p. 152). The target’s `PushHit` roll is a separate requirement and does not replace its domino roll; a target that fell from its domino roll is prone and simply no longer owes the push roll. A domino into water adds the standard water-depth modifier to this roll instead of a separate water-entry roll (−1 for Depth 1, 0 for Depth 2, +1 for Depth 3+).
* **Off-board displacement (deferred):** per Total Warfare p. 151, being displaced off the map is legal and destroys the unit; the current implementation still treats an off-map link as prohibited terrain and blocks the chain. Known deviation, tracked in [#1670](https://github.com/anton-makarevich/MakaMek/issues/1670).
* **Side-hex dodge (deferred):** the rule that lets a mobile unit with Ground MP remaining dodge into its forward/rear hex when a domino enters through a side hex is not implemented yet. The occupant selection point in `DominoEffectResolver` is where it will be evaluated.
* **Mutual pushes:** if two ’Mechs both declare a push against each other:
    * **Both succeed:** neither ’Mech moves, and **both** must make PSRs to avoid falling.
    * **Only one succeeds:** resolved as a standard push.
    * **Both fail:** nothing happens.

---

### **5. Other Units & Special Conditions**

* **Prone ’Mechs:** Cannot perform standard physical attacks; they may only punch ground vehicles in their hex or perform a "thrashing attack" against infantry in the same hex.
* **Vehicles:** Cannot perform physical attacks, except for **Charge (ramming)** attacks or using specialized mounted equipment (like a bulldozer or saw).
* **ProtoMechs & Infantry:** ProtoMechs cannot make standard physical attacks (they have a special "frenzy" attack). Infantry use specialized Anti-’Mech leg or swarm attack rules instead of standard physical attacks.
* **Aerospace Units:** Cannot make physical attacks against ground units.

---

### **6. Water — Physical Attack Eligibility (Total Warfare p. 109, p. 151; issue #1600)**

The water rules decide whether a physical attack may be **made at all** — they are *eligibility* rules, not to-hit modifiers. The only to-hit effect of water on a physical attack is the **partial cover +1** a target in Depth 1 water receives (see §2), and the **damage halving** for attacks applied to an underwater location is a resolution rule (tracked in #1503), not an eligibility rule. The full decision is recorded in [`docs/architecture/Physical-Attack-Level-And-Water-Rules.md`](../architecture/Physical-Attack-Level-And-Water-Rules.md).

**Effective standing levels (p. 109):** water counts as negative levels. A unit in a water hex stands at the bottom of that hex, so its effective standing level is `hex level − water depth`. A unit on a bridge over water stands at the bridge level and is never considered to be in water.

**Punch, Kick and Push — target-side rule (p. 151):** an attack cannot be made against a unit that is **fully submerged** (water depth ≥ the unit's height — for a 'Mech: standing in Depth 2+ water, or prone in Depth 1 water) unless the attack **begins underwater as well**:

| Attacker's position | Attacks allowed against a submerged target |
| :--- | :--- |
| **Not in water** (on land or on a bridge) | **None** — no part of the attack is underwater |
| **Standing in Depth 1 water** (half-submerged: torso and arms above the surface) | **Kick only** — the only melee attack that occurs entirely underwater. A punch or push would partly take place above the surface |
| **Fully submerged** (Depth 2+ water, or prone in Depth 1 water) | **Any** supported melee attack — the whole attack takes place underwater |

**No attacker-side restriction toward surface targets (Punch, Kick, Push):** the Depth 2+ paragraph grants a fully submerged 'Mech permission to make any of the listed physical attacks without restricting the target's state. An underwater attacker may therefore punch or push a surface target subject to the normal requirements — adjacency, arcs, and the **Different Levels Table** acting on the effective standing levels of both units. A **Depth 1 attacker** is also explicitly unrestricted against a surface target: it "may make any physical attack, provided the unit meets the normal requirements for each type of attack".

**Charge and DFA — movement-phase attacks.** These attacks are declared while the attacker moves, so the waterline rule is evaluated at declaration time:

* **Charge:** when either the attacker at the approach position or the target is fully submerged, the attack must be entirely underwater. A fully submerged 'Mech may charge another fully submerged unit. A submerged unit **cannot** charge a surface or Depth 1 unit, and a surface/Depth 1 unit **cannot** charge a submerged unit. A standing 'Mech in Depth 1 water may charge another standing unit because neither is fully submerged. In implementation terms, the attacker at the **approach position** and the target must be on the same side of the waterline.

  | Approach position | Target | Charge |
  | :--- | :--- | :--- |
  | Surface or Depth 1 (not fully submerged) | Surface or Depth 1 (not fully submerged) | Legal |
  | Surface or Depth 1 (not fully submerged) | Fully submerged | Illegal |
  | Fully submerged | Surface or Depth 1 (not fully submerged) | Illegal |
  | Fully submerged | Fully submerged | Legal |

* **DFA:** the attack is **never** entirely underwater. A fully submerged target is rejected by an explicit check because DFA has no level comparison to fall back on. A fully submerged attacker is also rejected, but under the **jump-jet rule** ("jump jets cannot be fired while submerged"), not under the physical-attack water rule. A 'Mech in Depth 1 water can still jump with its torso-mounted jets.
* **DFA against a target standing in Depth 1 water:** legal. A standing 'Mech is waist-deep, so its torso and head remain above the surface and it is **not** submerged; the target-side rule only protects a fully submerged unit. The attacker needs enough Jumping MP to clear the target hex's standing level plus the target's height (for a Depth 1 hex at level 0 the waterbed is −1 and a standing 'Mech reaches +1). If the DFA lands in the water hex, the attacker makes a landing PSR; on success it stands on the waterbed, on failure it falls onto the waterbed.

**Implementation scope:** `PhysicalAttackEligibility` applies the Punch/Kick/Push rules (and the effective-standing-level comparison) to physical-phase declarations; the same rules are re-checked at resolution time via `CanResolve` and `PhysicalAttackTargetValidityGate`, so a declaration that has become illegal by resolution is skipped automatically. `MovementAttackDeclarationValidator` applies the Charge/DFA declaration rules. Charge and DFA resolutions live in `PhysicalAttackResolutionPhase`: a charge applies the charge water rules, and a DFA grounds the attacker through the displacement pipeline, with a two-level rear fall on a miss. The DFA landing PSR carries the water-entry modifier when the attacker lands in water (Depth 1 −1, Depth 2 0, Depth 3+ +1).

**DFA miss displacement — first implementation:** the rules give the target's controller the choice of the adjacent hex. The first version resolves it on the server instead, so a miss needs no client interaction: `DfaDisplacementResolver.ResolveMiss` walks the adjacent hexes ordered by alignment away from the attacker's jump origin (`MovementTaken.Start`) and takes the first passable, unoccupied one, which is the hex a hit would push the target into. No adjacent hex is available → the attacker is destroyed. Restoring the controller's choice is tracked as a follow-up.

---

### **7. Eligibility Policy and Structural Metadata (issue #1612)**

A decision is recorded here about where physical-attack knowledge lives:

* **Keep eligibility as a policy service.** `IPhysicalAttackEligibility` is the construction-time replaceable policy, registered in `CoreServices` and `BotContainerServices`. The declaration phase, the resolution phase and the resolution gates consume it; `CanResolve` skips only the one-attack-per-turn check.
* **Do not extract the level-difference table now.** It is a small, well-tested table inside the default policy and no house rule needs to vary it yet.
* **Do not introduce `IPhysicalAttackEligibilityRules`.** The existing `IPhysicalAttackEligibility` registration is already replaceable, so a second abstraction would add indirection without a consumer.
* **`PhysicalAttackDefinitions` holds structural metadata only** — which attack types exist, their candidate limbs and the maximum limbs per declaration. Dynamic policy (arcs, actuator prerequisites, fired-weapon restrictions, level differences, target checks) stays in the eligibility service.
* **`GetRestriction` explains every supported type.** Punch, Kick and Push derive the restriction and the reaching limbs from the same private evaluation, so a null restriction always means the attack has at least one reaching limb apart from the one-attack-per-turn rule. The first failure wins; the melee order is generic validity, the DFA target immunity, water, level difference, arc, then limbs.
* **`GetDeclarationRestriction` adds the declaration-only budget check.** It returns the intrinsic `GetRestriction` first and then reports `TargetAlreadyDisplaced` only for a displacement attack whose target is already reserved this turn. `CanResolve` and `HasAnyOption` are unchanged and never apply the budget, so a declaration that reserved the target can still resolve.
* **Qualified by the physical-attack level and water decision ([#1685](https://github.com/anton-makarevich/MakaMek/issues/1685)).** A shared Core endpoint record (effective standing level, submersion, in-water) and a small attack-type water predicate are planned, but the Different-Levels table stays local to this policy and no new eligibility interface is introduced. Charge/DFA remain in `MovementAttackDeclarationValidator`. See [`Physical-Attack-Level-And-Water-Rules.md`](../architecture/Physical-Attack-Level-And-Water-Rules.md).