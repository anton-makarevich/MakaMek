# MakaMek MVP Product Requirements Document

## Executive Summary

MakaMek is a cross-platform implementation of the classic BattleTech tabletop game. The MVP is targeting simplified 3025-era combat mechanics and focuses on core turn-based tactical combat featuring BattleMechs on hex-based maps with essential game mechanics including movement, combat, heat management, and critical damage systems.

**Primary Objectives:**
- Deliver a playable BattleTech experience with simplified rules
- Support cross-platform deployment (Desktop, Web, Mobile)
- Provide multiplayer capabilities (local and LAN)
- Maintain compatibility with standard BattleTech community data formats (MTF)

**Scope:** Single-player and multiplayer tactical combat with up to 4 players using bipedal BattleMechs on hex maps with basic terrain types.

## Functional Requirements

### 1. Core Game Systems

#### 1.1 Game Structure
- **Turn-based gameplay** 
- **Phase-based turn structure:**
  1. Initiative Phase
  2. Movement Phase
  3. Weapon Attack Phase
  4. Heat Phase
  5. End Phase
- **Victory conditions:** Eliminate all opposing BattleMechs

#### 1.2 BattleMech Classifications
- **Light 'Mechs:** 20-35 tons (reconnaissance, speed)
- **Medium 'Mechs:** 40-55 tons (workhorses, core units)
- **Heavy 'Mechs:** 60-75 tons (commanders, damage dealers)
- **Assault 'Mechs:** 80-100 tons (battlefield dominance)
- **Chassis type:** Bipedal only (no quad 'Mechs in MVP)

#### 1.3 MechWarrior Skills
- **Piloting Skill:** Controls movement and fall avoidance (lower = better)
- **Gunnery Skill:** Controls weapon accuracy (lower = better)
- **Health:** 5 damage points maximum (6th point is fatal)

### 2. Map and Terrain System

#### 2.1 Hex-based Maps
- **Hex grid:** 6-sided hexagons representing 30m areas
- **Coordinate system:** Standard hex addressing

#### 2.2 Terrain Types
- **Clear Terrain:** No movement penalty, no cover
- **Light Woods:** +1 MP cost, +1 to-hit modifier, line of sight allowed
- **Heavy Woods:** +2 MP cost, +2 to-hit modifier, provides cover

### 3. Movement System

#### 3.1 Movement Modes
- **Walking:** Standard movement, +1 to-hit modifier when attacking
- **Running:** Increased speed, +2 to-hit modifier when attacking
- **Jumping:** Ignore terrain penalties, +3 to-hit modifier when attacking
- **Standing Still:** No movement, +0 to-hit modifier

#### 3.2 Movement Mechanics
- **Movement Points (MP):** 1 MP per hex entered
- **Terrain costs:** Clear (+0), Light Woods (+1), Heavy Woods (+2)
- **Facing changes:** 1 MP per 60-degree turn
- **Target Movement Modifier (TMM):** Based on hexes moved (0-2: +0, 3-4: +1, 5-6: +2, etc.)

#### 3.3 Piloting Skill Rolls
- **Triggers:** Heavy damage (20+ points), gyro hits, leg actuator damage
- **Modifiers:** Gyro hit (+3), gyro destroyed (+6), leg actuator hit (+1), heavy damage (+1)
- **Failure consequences:** 'Mech falls, takes damage, MechWarrior may be injured

### 4. Combat System

#### 4.1 Weapon Attack Resolution
- **Declaration phase:** All attacks declared before resolution
- **Line of Sight (LOS):** Required between attacker and target
- **Firing arcs:** Forward, Left Side, Right Side, Rear
- **Torso twist:** Allows shifting upper body firing arc

#### 4.2 To-Hit Calculation (GATOR System)
- **Base number:** Attacker's Gunnery Skill
- **Cumulative modifiers:**
  - Attacker movement: Walk (+1), Run (+2), Jump (+3)
  - Target movement: Based on hexes moved
  - Range: Short (+0), Medium (+2), Long (+4)
  - Minimum range: Penalty if within weapon minimum
  - Terrain: Light woods (+1), Heavy woods (+2)
  - Heat effects: Various penalties at different heat levels

#### 4.3 Hit Location and Damage
- **Hit location tables:** 2D6 roll determines hit location by attack direction
- **Damage resolution:** Armor first, then internal structure
- **Damage transfer:** From destroyed locations to adjacent areas
- **Critical hits:** When internal structure damaged, roll for component damage

#### 4.4 Critical Hit Effects
- **Cockpit:** Destroys 'Mech, kills MechWarrior
- **Engine:** 3 hits destroy 'Mech, each hit increases heat
- **Gyro:** Adds PSR modifiers, destroyed = automatic fall + immobile
- **Heat sinks:** Each hit reduces heat dissipation
- **Sensors:** Adds to-hit penalties, 2 hits prevent weapon fire

### 5. Heat Management System

#### 5.1 Heat Generation
- **Movement heat:** Walk (+1), Run (+2), Jump (+3 minimum or MP spent)
- **Weapon heat:** Variable per weapon type
- **Heat tracking:** On 'Mech record sheet heat scale

#### 5.2 Heat Effects
- **Movement penalties:** Reduced Walking MP at heat levels 5, 10, 15, 20, 25
- **Weapon penalties:** To-hit modifiers at heat 4, 8, 13, 17, 21, 25, 29+
- **Shutdown risks:** Automatic shutdown attempts at heat 14, 18, 22, 26, 30
- **Ammunition explosions:** Risk at heat 19, 23, 28
- **MechWarrior damage:** If life support hit, damage at heat 15+

### 6. Unit Destruction Conditions
- **MechWarrior death:** Any cause
- **Engine destruction:** 3 critical hits
- **Location/component destruction:** Head/cockpit, or center torso destroyed

## Technical Requirements

### Architecture
- **.NET 9** 
- **Client-Server architecture** with reactive communication
- **Cross-platform UI** using AvaloniaUI
- **Modular design** separating Core, Presentation, and UI layers

### Platform Support
- **Desktop:** Windows, Linux, macOS
- **Web:** WebAssembly (WASM)
- **Mobile:** Android, iOS

### Multiplayer Support
- **Local play:** Single device, up to 4 players
- **LAN multiplayer:** SignalR communication

### Data Formats
- **'Mech definitions:** MTF (MegaMek) format compatibility
- **Game state:** JSON serialization
- **Map data:** Custom format with MegaMek asset compatibility

## Acceptance Criteria

### Phase Implementation
- [x] **Initiative Phase:** 2D6 roll determines turn order
- [x] **Movement Phase:** Alternating movement with proper MP costs
- [x] **Attack Phase:** Weapon targeting and to-hit calculation
- [x] **Heat Phase:** Heat accumulation and effect application
- [x] **End Phase:** Turn cleanup and victory condition checks

### Combat Mechanics
- [x] **GATOR system:** Accurate to-hit calculation with all modifiers
- [x] **Hit location:** Proper 2D6 tables for front/side/rear attacks
- [x] **Critical hits:** Component damage with correct effects
- [x] **Damage transfer:** Proper armor/structure/location destruction

### Movement Validation
- [x] **Terrain costs:** Correct MP expenditure for terrain types
- [x] **Piloting rolls:** Triggered by appropriate conditions
- [x] **Fall mechanics:** Damage calculation and facing changes
- [x] **Jump mechanics:** Proper heat and to-hit effects

### Heat System
- [x] **Heat tracking:** Accurate accumulation from movement/weapons
- [ ] **Performance effects:** Movement/accuracy penalties at thresholds
- [ ] **Shutdown mechanics:** Automatic attempts at heat levels
- [ ] **Critical effects:** Ammunition/life support interactions

## Priority Matrix

### Must-Have (P0)
- Core game phases and turn structure
- Basic movement (walk/run) with terrain costs
- Weapon attack resolution with GATOR system
- Heat generation and basic effects
- Hit location and damage resolution
- Critical hit system for major components
- Victory/defeat conditions

### Should-Have (P1)
- Jump movement mechanics
- Piloting skill rolls and fall system
- Advanced heat effects (shutdown, ammo explosion)
- Torso twist mechanics
- Multiple terrain types
- LAN multiplayer support

### Could-Have (P2)
- Advanced critical hit effects
- Detailed component damage modeling
- Enhanced UI/UX features
- Additional 'Mech variants

## Dependencies and Assumptions

### Technical Dependencies
- AvaloniaUI framework for cross-platform UI
- SignalR for multiplayer communication
- .NET 9 runtime availability on target platforms
- MTF format specification for 'Mech data

### Design Assumptions
- Players familiar with basic BattleTech concepts
- Simplified rules sufficient for engaging gameplay
- Cross-platform deployment feasible with chosen tech stack
- Performance acceptable on mobile devices

### External Dependencies
- BattleTech IP usage within fair use/fan project guidelines
- Community 'Mech data availability in MTF format
- Asset compatibility with MegaMek resources

## Success Metrics

### Functional Metrics
- **Game completion rate:** >90% of started games reach victory condition
- **Rule accuracy:** compliance with specified BattleTech mechanics
- **Cross-platform compatibility:** Successful deployment on all target platforms
- **Multiplayer stability:** <1% disconnection rate in LAN games

---

*This PRD serves as the foundation for implementing the MakaMek MVP, providing clear requirements for an LLM-based AI agent to understand scope, prioritize features, and generate specific implementation plans.*
