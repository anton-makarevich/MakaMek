## Mech Critical Hit Effects Implementation Tracking
**Version 0.40.x**

### General
- [x] Ammunition: If a critical hit destroys a slot with explosive ammunition, the ammo explodes. The 'Mech takes internal structure damage based on the total Damage Value of the ammo in the slot, multiplied by shots remaining.
  - [ ] If the location has CASE, excess damage dissipates.
  - [ ] The MechWarrior automatically takes 2 points of damage and requires a Consciousness Roll.

- [x] Weapons: The first critical hit to a weapon (even multi-slot ones) knocks out the weapon. Additional hits to multi-slot weapons have no further effect.
  - [ ] Explosive components (e.g., Gauss rifles) explode similarly to ammunition explosions

- [x] Jump Jet: That jump jet can no longer deliver thrust. Reduces the 'Mech's Jumping MP by 1 for each hit.

- [x] Heat Sinks: Destroys the heat sink, reducing heat dissipation by 1 point (or 2 for a double heat sink). Destroyed heat sinks do not dissipate heat in the current Heat Phase.

### Torso
- [x] Engine (Torso): Fusion engines have 3 shielding points.
  - [x] First hit: Increases heat build-up by 5 points per turn.
  - [x] Second hit: Increases heat build-up to 10 total points per turn.
  - [x] Third hit: Shuts down the engine and puts the BattleMech out of commission.

- [x] Gyro (Torso): Can survive only one critical hit; the second destroys it.
  - [x] First hit: Requires a PSR with a +3 modifier when applied and every time the 'Mech runs or jumps.
  - [x] Second hit: Gyro is destroyed, the 'Mech automatically falls (if standing)
  - [ ] and becomes immobile (cannot stand, cannot move, only hexside changes possible).

### Head
- [x] Cockpit: Destroys the slot, kills the MechWarrior, and puts the 'Mech out of commission. Small cockpits have the same effect.

- [x] Sensors:
  - [x] First hit: Adds +2 modifier to 'Mech's weapon to-hit numbers.
  - [x] Second hit: Makes it impossible to fire weapons.

- [ ] Head Blown Off: Occurs on a roll of 12 on the Determining Critical Hits Table for a head hit. 
  - [ ] Destroys the 'Mech's head, kills the MechWarrior, and puts the 'Mech out of commission.

- [ ] Life Support: System is permanently knocked out.
  - [ ] Pilot takes 1 point of damage at the end of every Heat Phase if heat is 15-25, or 2 points of damage if heat is 26+.

### Legs
- [x] Lower Leg Actuator: Destroys the actuator. Requires a PSR with a +1 modifier at the end of the phase and for subsequent PSRs.
  - [ ] Reduces Walking MP by 1 (recalculate Running MP).

- [x] Upper Leg Actuator: Destroys the actuator. Requires a PSR with a +1 modifier at the end of the phase and for subsequent PSRs.
  - [ ] Reduces Walking MP by 1 (recalculate Running MP).

- [ ] Hip: Freezes the affected leg. Requires a PSR with a +2 modifier at the end of the phase and for subsequent PSRs. Walking MP is halved (recalculate Running MP). 'Mech cannot make kick attacks. Ignores previous critical hit modifiers to that leg.
  - [ ] Second hip hit: Reduces 'Mech's MP to 0 and adds another +2 modifier to PSR (not immobile).

- [x] Foot Actuator: Destroys the foot muscle. Requires a Piloting Skill Roll (PSR) with a +1 modifier at the end of the phase and for subsequent PSRs.
  - [ ] Reduces Walking MP by 1 (recalculate Running MP).

- [x] Leg Blown Off: Occurs on a roll of 12 on the Determining Critical Hits Table for a leg hit. The leg is blown off, and the 'Mech automatically falls (takes normal falling damage). Explosive components do not explode.
  - [ ] Single Attempt to stand up that counts as running.
  - [ ] Only one MP if mech is standing up, no run

### Arms
- [ ] Lower Arm Actuator: Destroys the actuator. Adds +1 modifier to weapons firing from that arm;

- [x] Arm Blown Off: Occurs on a roll of 12 on the Determining Critical Hits Table for an arm hit. The arm is blown off, and its weapons/equipment are lost. Explosive components in the arm do not explode.

- [ ] Shoulder: Freezes the shoulder joint. Adds +4 modifier to weapon attacks made with weapons mounted on that arm, ignoring other arm critical hit weapon modifiers.

- [ ] Upper Arm Actuator: Destroys the actuator. Adds +1 modifier to weapons firing from that arm
