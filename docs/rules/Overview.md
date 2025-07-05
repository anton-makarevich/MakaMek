# Total Warfare. Game Overview
*Total Warfare* is a comprehensive rulebook for the *BattleTech* game system, unifying rules for various units that directly impact 31st-century battlefields. It is primarily a **reference work for experienced players**, not an introductory guide. New players are advised to start with the *A Game of Armored Combat* box set. Construction rules for units found in *Total Warfare* are located in *TechManual*. The game is set in the 31st century, a time of "endless wars" between star empires, fought by BattleMechs and their support units.

## 1. Core Game Components & Terminology
To play *BattleTech*, players require:
*   **Dice**: Two six-sided dice (2D6), preferably of different colours, are standard. 1D6 is also used for specific rolls.
*   **Mapsheets**: These are 18x22 inch hex-grid playing maps, where each hex represents 30 metres of ground. Mapsheets can represent various terrain types (e.g., clear, rough, woods, water, buildings). Aerospace units can operate on space maps or atmospheric maps, with different scales for hexes and turns.
*   **Counters or Miniatures**: Used to represent units on the mapsheet, indicating their position and facing.
*   **Record Sheets**: Unique sheets for each unit type (e.g., `Mech`, ProtoMech, Combat Vehicle, Infantry, Aerospace Unit) used to track damage, heat, ammunition, and other statistics. Blank sheets are in *TechManual*, pre-filled ones are online or in other sourcebooks.

### Key Terminology for Units
*   **Unit**: Any mobile element fielded in a *BattleTech* game (BattleMech, IndustrialMech, ProtoMech, Combat Vehicle, conventional infantry platoon/Point, battle armour squad/Point, small craft, conventional fighter, aerospace fighter, DropShip, Support Vehicle). Each unit moves and attacks individually.
*   **`Mech`**: Refers to BattleMechs/OmniMechs and IndustrialMechs (bipedal and four-legged). Does **not** refer to ProtoMechs.
*   **Omni**: Refers to OmniMechs and OmniVehicles.
*   **Fighter**: Refers to conventional and aerospace fighters.
*   **Vehicle**: Refers to all Combat and Support vehicles.
*   **Infantry**: Refers to conventional and battle armour.
*   **Location**: A physical section of a unit (e.g., head, arm, torso, leg).
*   **Critical Slot**: A line in a location on the Critical Hit Table representing a weapon or equipment susceptible to destruction.
*   **Level/Elevation/Altitude**: Represents vertical height; `Level` for ground terrain/buildings, `Elevation` for low-altitude aerospace units, `Altitude` for high-altitude aerospace units.

### Unit Types Overview
*   **BattleMechs**: 30-foot-tall humanoid metal titans, weighing 20-100 tons. Classified by weight (Light, Medium, Heavy, Assault) and chassis (bipedal, four-legged). IndustrialMechs are a variant.
*   **ProtoMechs**: Smaller humanoid `Mechs` (2-9 tons), controlled via neural interface, often with hand-held main guns. Deployed in Points of five units.
*   **Combat Vehicles**: Auxiliary units to BattleMechs, classified by weight and locomotion type (wheeled, tracked, hover, VTOL, WiGE, naval). Can be OmniVehicles.
*   **Support Vehicles**: Backbone of military machines (e.g., cargo trucks, airships, police cruisers). Different chassis types and size classes (small, medium, large).
*   **Infantry**:
    *   **Conventional Infantry**: Platoons/Points. Damage absorption differs from other units. Do not track ammunition for most weapons.
    *   **Battle Armour**: Squads/Points of troopers. Can be mechanized (riding on `Mechs`/vehicles).
*   **Aerospace Units**:
    *   **Aerospace Fighters**: Designed for atmospheric and space flight.
    *   **Small Craft**: Larger than fighters, smaller than DropShips.
    *   **DropShips**: Large transport vessels (200-2,499 tons), classified by role (troop, `Mech`, fighter, assault, cargo, passenger) and shape (aerodyne, spheroid).
    *   **WarShips**: Generally outside the scope of *Total Warfare* core rules, detailed in *Strategic Operations*.

## 2. Game Setup
1.  **Select Scenario Type**: Players choose or randomly generate a scenario. Scenarios define objectives and conditions (e.g., Standup Fight, Hide and Seek, Hold the Line, Extraction, Breakthrough, Chase).
2.  **Select Mapsheets**: Determine the number of mapsheets (e.g., one per four units) and their terrain type. Mapsheets are laid out in a continuous rectangular or square area.
3.  **Determine Force Composition**: Players generate forces using Battle Value (BV) points for balance, or randomly assign unit types and weight classes.
4.  **Assign Units and Skills**: Specific unit designs are chosen/rolled from Random Assignment Tables. Warriors (MechWarriors, pilots, crew) are assigned experience ratings (Green, Regular, Veteran, Elite) and skills (Gunnery, Piloting/Driving, Anti-`Mech` Skill). Skills can be improved over scenarios.
5.  **Initial Deployment**: Players roll 2D6 for initiative; the winner chooses their home map edge. All units typically start off-map and enter through their home edge. Aerospace units announce starting velocity.

## 3. Sequence of Play (Game Turn)
A *BattleTech* game consists of turns, each representing ten seconds of real time (ground scale). Each turn has seven phases, executed in order:

1.  **Initiative Phase**: Each side rolls 2D6; the higher roll wins initiative for the turn. Ties are re-rolled.
2.  **Movement Phase (Ground)**: The side that lost initiative moves one ground unit, then the winner moves one, and so on, until all ground units have moved or declared they are staying still.
3.  **Movement Phase (Aerospace)**: Similar to Ground Movement, but for aerospace units.
4.  **Weapon Attack Phase**: All players declare weapon attacks before any are resolved. Attacks are then resolved in an order chosen by the player (typically top-to-bottom on record sheets). Damage takes effect at the end of the phase.
5.  **Physical Attack Phase**: Players declare and resolve physical attacks (e.g., punches, kicks). Damage takes effect before the Heat Phase.
6.  **Heat Phase**: Players adjust units' heat scales, resolve heat effects (movement penalties, weapon attack modifiers, shutdown, ammunition explosions, warrior damage). ProtoMechs, Combat/Support Vehicles do not track heat.
7.  **End Phase**: Unconscious warriors attempt to regain consciousness. Miscellaneous actions (e.g., switching heat sinks on/off, dumping ammunition, turning stealth armor on/off) are performed.

## 4. Core Mechanics

### 4.1. Movement
*   **Movement Modes**:
    *   **Walking/Cruising**: Standard movement, up to Walking/Cruising MP rating. Attacker incurs a +1 to-hit modifier.
    *   **Running/Flanking**: Up to 1.5 times Walking/Cruising MP (rounded up). `Mechs` generate 2 heat points. Critical damage to hip actuators or gyros requires Piloting Skill Roll to avoid falling. VTOL/WiGE/Hover units risk sideslipping.
    *   **Jumping**: `Mechs` can use jump jets to move without terrain penalties and gain a Target Movement Modifier (TMM). Generates heat.
*   **Movement Costs**: Entering any hex costs 1 MP, plus terrain-specific costs. Changing facing costs 1 MP per hexside. Level changes also incur MP costs.
*   **Skidding/Sideslipping**: Occurs when units on pavement fail Piloting/Driving Skill Rolls (PSR) during certain movement types (e.g., running after facing change). Results in uncontrolled movement and damage. Hover vehicles sideslip instead of skid.
*   **Falling**: `Mechs` can fall due to failed PSRs, physical attacks, or critical damage. Falling inflicts damage to the `Mech` and potentially the MechWarrior. A fallen `Mech` is prone and face down, and can attempt to stand up by expending MP and making a successful PSR.
*   **Stacking**: Generally, only one `Mech` can occupy a hex. Limits apply to other unit types as well. Infantry riding carriers do not count against stacking limits.

### 4.2. Combat (Weapon Attacks)
*   **Line of Sight (LOS)**: Must exist between attacker and target. Determined by drawing a line between hex centres and considering intervening terrain (levels, woods, buildings, partial cover).
*   **Firing Arcs**: Weapons have defined firing arcs (Forward, Left Side, Right Side, Rear). `Mechs` can perform a **torso twist** to change their forward arc (except quads). Turreted vehicles can rotate turrets. Arms can be reversed to fire rearwards.
*   **To-Hit Roll Calculation (GATOR)**: A **2D6 roll** must equal or exceed a **Modified To-Hit Number** for an attack to succeed.
    *   **Base To-Hit Number**: Equal to the attacking unit's Gunnery Skill rating.
    *   **Modifiers (Cumulative)**:
        *   **Attacker Movement Modifier**: Based on attacker's movement mode (walking/cruising, running/flanking, jumping).
        *   **Target Movement Modifier**: Based on the number of hexes the target moved.
        *   **Range Modifier**: Based on distance to target (Short, Medium, Long, Extreme).
        *   **Minimum Range Modifier**: Penalty if target is within a weapon's minimum range.
        *   **Terrain Modifiers**: For intervening terrain, partial cover.
        *   **Heat/Damage Modifiers**: From attacker's heat buildup or critical damage.
        *   **Multiple Target Modifier**: If attacker fires at multiple targets.
        *   **Special Weapon/Equipment Modifiers**: (e.g., Pulse weapons get -2 to-hit, Targeting Computers give -1).
        *   **Aimed Shots**: Special rules for targeting specific locations on immobile or active units.
*   **Ammunition**: Limited for missile and ballistic weapons. Tracked on Critical Hit Table slots. Can be dumped. Infantry generally don't track ammo.
*   **Hit Location**: On a successful hit, roll 2D6 on the appropriate Hit Location Table (unit-specific) to determine where the damage lands. Attack direction (front, side, rear) can influence the table used.
*   **Damage Resolution**: Applied first to **Armor** (circles on record sheet). Remaining damage transfers to **Internal Structure** (circles). If internal structure is destroyed, damage can transfer to the next inward location based on the Damage Transfer Diagram.
*   **Critical Damage**: Whenever a unit's internal structure takes damage, a **2D6 roll** on the **Determining Critical Hits Table** (8+ for critical hit) determines if an internal component is damaged. The specific component hit is then rolled for on the unit's Critical Hit Table. Critical hits can disable weapons, actuators, engines, sensors, etc.. Critical hits to explosive slots (e.g., ammo) can cause an explosion.
*   **Unit Destruction**: A unit is destroyed under specific conditions (e.g., MechWarrior killed, three engine hits for `Mechs`, all internal structure in a location destroyed for vehicles). Destroyed units are removed from the map.

### 4.3. Heat
*   **Heat Points**: Tracked on a Heat Scale (0-30+, with overflow).
*   **Heat Generation**: From movement (running, jumping), weapon fire, and outside sources (e.g., flamers, plasma weapons).
*   **Heat Dissipation**: Through heat sinks (Standard: 1 pt/turn; Double: 2 pts/turn). Increased dissipation in water for submerged heat sinks.
*   **Effects of Heat (at specific thresholds)**:
    *   **Movement Penalty**: Reduced Walking/Running MP.
    *   **Weapon Attack Modifier**: + to-hit number.
    *   **Shutdown**: `Mech` power plant shuts down (can be avoided by Piloting Skill Roll for conscious MechWarriors, or automatically below 14 heat). Shutdown `Mechs` are immobile and vulnerable.
    *   **Ammunition Explosion**: Risk of ammo explosion at high heat (19+, 23+, 28+ heat points), can be avoided by 2D6 roll. Explodes most destructive ammo first, applies damage to internal structure.
    *   **Damage to MechWarrior**: Suffers damage at very high heat levels (21+, 27+ heat points), requiring Consciousness Rolls.
    *   **Aerospace Units**: Have similar heat effects but also risk random movement and pilot damage from ammo explosions. Large Craft use an abstract heat system, generating heat per firing arc.

## 4.4. Physical Attacks
*   `Mechs` can make one type of physical attack per turn. Vehicles can charge/ram or use physical weapons. ProtoMechs can frenzy.
*   **Types**:
    *   **Punch**: `Mechs` use arms (one or both). Damage based on tonnage. Modifiers for arm damage.
    *   **Club**: `Mechs` use fallen arms/legs as clubs.
    *   **Kick**: `Mechs` use legs. Damage reduced by leg actuator damage. Requires PSR for `Mech` kicked or if attack misses.
    *   **Push**: `Mechs` use both arms to push another standing `Mech`. Requires PSRs for both `Mechs`.
    *   **Charge**: Unit moves into target's hex. Attacker/target take damage. Requires PSR/DSR for both units.
    *   **Death From Above (DFA)**: Jumping `Mech` lands on target's hex. Both units take damage. Requires PSRs.
    *   **Physical Weapon Attacks**: Using specific equipment (e.g., Hatchet, Sword, Wrecking Ball).
*   **Damage**: Physical attacks inflict damage based on attacker's weight or specific weapon damage. Halved in water.
*   **Displacement/Domino Effect**: If a fall or other action forces a unit into an occupied hex beyond stacking limits, units can be displaced.

## 5. Unit-Specific Rules & Equipment (Examples)

*   **Buildings**: Have Construction Factor (CF) determining damage resistance. Damage reduces CF; if reduced to 0, building becomes rubble. Units moving through buildings or taking damage inside them follow specific rules. Building collapse can damage units inside or underneath. Most building hexes have basements, rolled upon first entry.
*   **C3 Computer System**: Links up to twelve `Mechs`/vehicles. Attacks use range to target from the nearest networked unit, but firing unit's other modifiers. Improved C3 systems exist.
*   **ECM Suite**: Can jam C3 systems and other electronics within a radius. Stealth armour systems require an ECM suite to function.
*   **TAG (Target Acquisition Gear)**: Designates a target for TAG-guided ammunition and aids indirect LRM fire with no to-hit modifier incurred by the spotter.
*   **Targeting Computer**: Can provide a -1 to-hit modifier for compatible weapons. Can be used for aimed shots on active targets with a +3 modifier (instead of -1).
*   **TSM (Triple-Strength Myomer)**: `Mech` equipment that activates at 9+ heat, increasing Walking/Running MP and doubling physical attack damage.

## 6. Scenarios & Victory Conditions
*   **Ending the Game**: Typically, when one player's units are all destroyed or retreated. Scenarios may have specific additional or alternative victory conditions.
*   **Battle Value (BV) System**: A numerical rating for unit capabilities and survival potential, used for balanced force generation and determining victory level.
*   **Forced Withdrawal**: Units suffering "crippling damage" (e.g., side torso destroyed, specific critical hits, severe troop loss) must withdraw. Retreated units do not count as destroyed for victory.
*   **Hidden Units**: Units can be hidden on the map at the start of a scenario, revealed when they move or attack.
*   **Cargo Carriers**: Units can carry cargo, affecting movement or requiring specific equipment (e.g., lift hoists) for loading/unloading. Infantry can ride inside non-`Mech` units.
*   **Mechanized Battle Armour**: Battle armour units can attach to OmniMechs/OmniVehicles for transport. Magnetic clamps allow mounting on standard `Mechs`/vehicles.

## 7. Ancillary Systems
*   **Clan Honor (Zellbrigen)**: Optional rules to enhance roleplaying for Clan forces. Divides honour into levels (1-4) for batchall (bidding), zellbrigen (ritual duelling), physical attacks, and retreat. Strict zellbrigen means duel participants cannot be attacked by others, and breaking honour can lead to free-for-alls. Using these rules makes game balance more difficult.