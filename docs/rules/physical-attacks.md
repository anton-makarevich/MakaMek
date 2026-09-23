**Declaration and resolution for physical attacks follow the same two-step principle as weapon attacks, with one notable exception regarding specific movement-based attacks.**

---

### **1. Do Physical Attacks Work Like Weapon Attacks? (Declaration & Resolution)**

**Yes, with a key exception for Charges and DFAs:**

* **Phase Sequence:** Physical attacks take place in Phase 5 (**Physical Attack Phase**), which immediately follows the Weapon Attack Phase.
* **Declaration:** Players alternate declaring physical attacks unit by unit (following Initiative / Unequal Numbers rules). 
* **Simultaneous Resolution & Damage:** All declared physical attacks are resolved during the Physical Attack Phase, and all damage takes effect simultaneously at the end of the phase before the Heat Phase. Any required Piloting Skill Rolls (PSRs) resulting from physical attacks are rolled at the end of the phase.
* **The Exception (Charge & Death From Above):** Unlike punches, kicks, or clubs—which are declared during the Physical Attack Phase—**Charge** and **Death From Above (DFA)** attacks must be declared during the **Movement Phase**. This is because these attacks dictate how and where the unit moves across the map. However, their actual damage resolution still takes place in the Physical Attack Phase.

---

### **2. Core Physical Attack Rules & Modifiers**

* **Base To-Hit Number:** Equal to the pilot’s **Piloting Skill Rating** (unlike weapon attacks, which use Gunnery Skill).
* **Modifiers:** Standard movement and terrain modifiers apply. Relative Piloting Skill modifiers apply to Charges and DFAs.
* **Heat & Sensors:** **Heat build-up and sensor damage modifiers NEVER apply to physical attacks**.
* **Single Attack Limit:** A ’Mech may only perform **one type** of physical attack per turn (e.g., a ’Mech cannot kick and punch, or use a hatchet and kick in the same turn).

---

### **3. Summary of 'Mech Physical Attack Types**

| Attack Type | When Declared | Base Modifier | Damage Value | Hit Location & Notes |
| :--- | :--- | :--- | :--- | :--- |
| **Punch** | Physical Phase | `+0` | Tonnage / 10 *(round up)* | Uses **Punch Location Table** (head/torsos/arms). Can punch with 1 or 2 arms (separate roll each) if no weapons were fired from those arms. |
| **Kick** | Physical Phase | `-2` | Tonnage / 5 *(round up)* | Uses **Kick Location Table** (legs). The **kicking leg** must not have fired any weapons mounted in it during the Weapon Attack Phase (rule: *"No weapons mounted on a kicking leg can fire in the turn in which a 'Mech kicks"*). The restriction is **per-leg**: firing a right-leg weapon only disqualifies the right leg; the left leg may still kick. Both **hip actuators** must be undamaged, and the target must be in one of the three forward-arc hexes based on the kicking foot's orientation. On a hit, the target must pass a PSR or fall; on a miss, the attacker must pass a PSR or fall. |
| **Club** | Physical Phase | `-1` | Tonnage / 5 *(round up)* | Uses **Standard Hit Location Table**. Requires two undamaged hand/shoulder actuators and no arm weapons fired. Clubs can be severed limbs, trees, or girders. |
| **Push** | Physical Phase | `-1` | `0` *(No damage)* | Target must be a **standing ’Mech** at the **exact same elevation** in the hex directly in front of the attacker's **feet** (not torso twist); cannot be performing a Charge or DFA, and a unit may receive only one Charge/DFA/Push per turn. Requires **both arms** and **no arm-mounted weapons fired** that turn (head/torso/leg weapons allowed). Damaged shoulder actuators do **not** block the push but add **+2 to-hit per damaged shoulder**. On a hit, target is pushed 1 hex directly away, attacker advances into the vacated hex at **0 MP cost**, and the target must pass a PSR or fall. Blocked/prohibited destination prevents movement but not the PSR. See §4 for full requirements. |
| **Physical Weapon** | Physical Phase | Varies *(e.g., Hatchet -1, Sword -2)* | Varies *(e.g., Hatchet = Tonnage / 5, round up; Sword = Tonnage / 10, round up, + 1)* | Uses **Standard Hit Location Table** (or Punch/Kick table with a +4 modifier). Cannot fire weapons on the weapon arm. |
| **Charge** | **Movement Phase** | `+0` *(± Piloting diff.)* | **Target:** (Attacker Tonnage / 10) × Hexes Moved *(round up)*.<br>**Attacker:** Target Tonnage / 10 *(round up)*. | Damage in 5-pt clusters. **Hexes Moved** counts only hexes traversed *before* entering the target's hex. Displaces target. **On hit:** both units must pass a **PSR (+2 modifier)** or fall. **On miss:** the attacker lands in the hex to the left or right of the target's forward arc, no damage is taken or dealt, and **no charge PSRs are rolled**. No weapon attacks allowed. |
| **Death From Above (DFA)** | **Movement Phase** | `+0` *(± Piloting diff. + Jump mod)* | **Target:** (Attacker Tonnage / 10) × 3 *(round up)*.<br>**Attacker:** Attacker Tonnage / 5 *(round up)* to legs. | Damage in 5-pt clusters on **Punch Location Table** for target. **On hit:** attacker lands in target hex, target is displaced; both make PSRs (Target +2, Attacker +4). **On miss:** the attacker **automatically falls** (2-level fall, damage applied to the rear); the target **does not roll a PSR** and instead is displaced to an adjacent passable hex of its choice. No weapon attacks allowed. |

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
* **Prohibited terrain / blocked hexes:** if the destination hex is prohibited terrain (or blocked by higher elevation), **neither unit moves**, but all other effects occur — including the defender’s mandatory PSR. *(Exception: being pushed into a hex more than 2 levels lower is allowed and results in an automatic fall.)*
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