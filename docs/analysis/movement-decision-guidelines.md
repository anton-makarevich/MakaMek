# BattleTech Movement Phase AI Design Guide

## Core Strategic Principles

### Movement Modifier Management (Highest Priority)
- Maximize own movement modifiers to increase enemy's to-hit difficulty
- Move as far as possible each turn (3/5/7 hexes with jumping into heavy forests)
- Higher movement = harder target = survival
- "The game is won by the movement phase"

### Terrain Utilization
- Prioritize partial cover over no cover
- Use heavy forests for maximum defensive bonus
- Create cover using smoke/fire when terrain lacks it
- Combine movement modifiers with terrain modifiers for cumulative defense

### Positional Warfare
- Get behind enemy units (rear arc attacks)
- Seek flanking positions to target weapon-loaded sides (e.g., Hunchback's AC20 side)
- Two optimal positioning zones: 2 hexes directly behind enemy, or just to the side where one arm can't fire
- Reduce enemy firing arcs while maximizing own weapon coverage

## Tactical Decision Framework

### Initiative-Based Strategy
**If moving first (lost initiative):**
- Adopt defensive posture
- Maximize movement modifiers
- Position for overwatch

**If moving last (won initiative):**
- Take aggressive positioning
- Strike at exposed enemies
- Capitalize on enemy positioning mistakes

### Target Prioritization
- Focus fire on isolated or separated units
- Concentrate force for local superiority
- Target fallen/crippled units last 
- Separate enemy small groups from main force

### Force Distribution
- Maintain mutual overwatch between friendly units
- Don't spread forces too thin
- Achieve local numerical superiority before enemy reinforcements arrive
- Use tank units as damage sponges for valuable mechs

## Advanced Tactics

### Movement Order Optimization
1. Move predictable/committed units first (LRM boats in sniper nests)
2. Fast/mobile units move next to last
3. Fallen units move last
4. Keeps opponent guessing on key unit positioning

### Baiting and Exposure Management
- Force enemies to leave cover to get shots
- Ensure baiting units stay out of enemy reach
- Keep baited position with high movement modifiers
- Use intervening enemy units as cover from more dangerous opponents

### Range and Positioning Calculus
**Pre-movement checklist:**
1. Does any unit have 0 movement modifier currently?
2. What fire range achievable with max movement modifier?
3. What terrain features are available?
4. Can I break line of sight from dangerous enemies while maintaining firing position on target?

### Weapon Arc Management
- Minimize exposure of damaged/vulnerable arcs
- Square up to enemy force to distribute incoming damage broadly
- Flank enemies to exploit weak arcs or concentrate fire on critical components
- Position to bring maximum weapons to bear on priority targets

## Situational Factors to Consider

### Mission-Dependent Strategy
- Campaign: Consider long-term unit preservation and strategic objectives
- Pickup game: Focus purely on battlefield victory conditions
- Objective-based: Count turns needed to reach objective, balance offense/defense accordingly

## Key Philosophy

**Damage Economics:** Maximize damage output while minimizing damage input

**The Great Equalizer:** "Even a lowly small laser can kill an Atlas" - never underestimate any unit

**Risk Management:** Stay alive first, deal damage second

**Luck Factor:** Movement phase reduces luck dependency by creating favorable probability

## Implementation Note
The final piece of advice is perhaps most important: "Don't worry about making the game's AI too smart." Focus on solid fundamentals rather than perfect play - human-like decision making with clear logic will be more engaging than an unbeatable optimal calculator.
