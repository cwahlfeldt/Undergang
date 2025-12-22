# Undergang - Game Design Document

## Overview

**Genre:** Turn-based Tactical Roguelike
**Core Inspiration:** Hoplite
**Platform:** Desktop (Godot 4.5 / C#)
**Target Audience:** Players who enjoy strategic puzzle-like combat and tactical positioning

### High Concept

Undergang is a turn-based tactical game where every move is a life-or-death decision. Navigate a hex-based battlefield where enemies attack reactively when you enter their range. Success requires careful planning, spatial awareness, and clever use of limited abilities to survive increasingly challenging encounters.

---

## Core Pillars

1. **Tactical Depth** - Every movement choice matters; positioning is survival
2. **Reactive Combat** - Enemies respond to your actions, creating dynamic puzzle-like scenarios
3. **Risk vs Reward** - Aggressive play versus cautious positioning
4. **Clarity** - Clear visual feedback for threat zones and movement options

---

## Game Theme & Setting

> **[TO BE FLESHED OUT]**
>
> Current working concept: The name "Undergang" (Norwegian/Danish for "downfall" or "demise") suggests themes of descent, inevitable conflict, or fighting against overwhelming odds.
>
> **Potential Directions:**
> - Underground labyrinth exploration (literal "undergang")
> - Last stand against an overwhelming force
> - Gladiatorial arena combat
> - Abstract tactical puzzle space (minimal narrative)
> - Norse/Scandinavian mythology themes
>
> **Questions to Answer:**
> - Who is the player character?
> - Why are they fighting?
> - What is the world/setting?
> - What gives each enemy type its unique attack pattern?

---

## Core Gameplay Loop

### Turn Structure

1. **Player Turn**
   - Survey the battlefield and enemy threat zones
   - Choose movement destination or activate ability
   - Movement triggers reactive combat
   - Combat resolves (enemies attack, player counter-attacks)

2. **Enemy Turn** (for each enemy in turn order)
   - If player is in attack range: Enemy **passes turn** (waits menacingly)
   - If player is out of range: Enemy **moves closer** to player
   - Enemies never attack on their own turn

### Movement & Combat Flow

**The Hoplite-Style Combat System:**

- **Entering Enemy Range** (NEW threat)
  - ALL enemies whose range you enter attack you
  - Multiple overlapping ranges = multiple attacks
  - Enemies do not move when attacking reactively

- **Moving Within Range** (EXISTING threat)
  - If you're already adjacent to an enemy and move to another tile still adjacent
  - You counter-attack that specific enemy
  - Does NOT trigger when first entering range

- **Enemy Decision Making**
  - On enemy turn, if player is in attack range: Pass turn
  - On enemy turn, if player is out of range: Move toward player
  - Enemies NEVER attack on their own turn

This creates a tactical puzzle where:
- Entering multiple threat zones is extremely dangerous
- Staying adjacent to an enemy lets you attack them by "dancing" around them
- Enemies act as stationary threats when you're in range
- You must carefully plan your path through overlapping threat zones

---

## Player Abilities

### Movement
- **Range:** Adjacent hex tiles (6 neighbors)
- **Traversal:** Can only move to non-blocked, walkable tiles
- **Triggers Combat:** Movement is the primary combat trigger

### Dash Ability
- **Effect:** Teleport to any tile within 2-tile radius
- **Cooldown:** 4 turns
- **Tactical Use:**
  - Escape from surrounded positions
  - Bypass enemy threat zones
  - Quickly close distance to objectives
  - Does NOT trigger reactive enemy attacks (teleport, not movement)

### Block Ability
- **Effect:** Negate the next incoming attack
- **Cooldown:** 3 turns (starts after block is consumed)
- **Duration:** Persists until consumed by an attack
- **Tactical Use:**
  - Safe passage through a single enemy threat zone
  - Survive when low on health
  - Enables aggressive positioning
  - Strategic timing: activate before risky moves

---

## Enemy Types

### Current Enemy Types

#### Grunt (Melee)
- **Attack Range:** Adjacent tiles only (RangeCircle)
- **Behavior:** Basic melee threat, must be directly next to player
- **Tactical Note:** Easiest to avoid, dangerous in groups

#### Wizard
> **[TO BE IMPLEMENTED]**
> - Potential for area-of-effect attacks
> - Explosion radius pattern available in code

#### Sniper Variants (Ranged)

**SniperAxisQ** - Q-Axis Sniper
- **Attack Range:** Linear along Q-axis (2-5 tiles)
- **Coverage:** Creates threat "lanes" in one hex direction

**SniperAxisR** - R-Axis Sniper
- **Attack Range:** Linear along R-axis (2-5 tiles)
- **Coverage:** Creates threat "lanes" in another hex direction

**SniperAxisS** - S-Axis Sniper
- **Attack Range:** Linear along S-axis (2-5 tiles)
- **Coverage:** Creates threat "lanes" in the third hex direction

**Combined Sniper Threat:** Three sniper variants can cover all six hex directions, creating complex threat patterns that require careful navigation.

### Available Range Patterns (for future enemy types)

The engine supports these additional range patterns:
- **RangeDiagonal** - Diagonal lines (2-6 tiles), alternating directions
- **RangeHex** - Hex ring at specific distance (tiles exactly N steps away)
- **RangeExplosion** - Area of effect (all tiles within radius)
- **RangeNGon** - Polygon pattern (triangular shapes in alternating directions)

---

## Map Generation

### Current Implementation
- **Size:** 5-tile radius hex grid (configurable)
- **Blocked Tiles:** 24 randomly placed obstacles
- **Tile Variation:** Visual variety through tile index (20-90 range)
- **Player Spawn:** Fixed position at (0, 4, -4)
- **Enemy Spawn:** Random placement with 3-tile exclusion radius around player

### Map Elements
- **Walkable Tiles** - Standard hex tiles
- **Blocked Tiles** - Impassable obstacles that block movement and line of sight
- **Traversable Indicator** - Clear visual distinction between walkable/blocked

---

## Visual & Animation System

### Animation States
- **Idle** - Default resting state
- **Move** - Walking/running during movement
- **Attack** - Combat action animation
- **Hurt** - Taking damage reaction
- **Die** - Death animation
- **Spawn, Victory, Defeat** - Optional states for future polish

### Visual Feedback
- **Movement Range** - Highlighted walkable tiles
- **Threat Zones** - Enemy attack ranges clearly marked
- **Dash Range** - Special highlight for dash ability targets
- **Block Indicator** - Visual cue when block is active
- **Health Display** - Current HP visible on units

### Lighting & Atmosphere
- **SSAO** - Screen-space ambient occlusion for depth
- **SSIL** - Screen-space indirect lighting for atmosphere
- **Ambient Lighting** - Environmental mood setting

> **[THEME DEPENDENT]**
> Visual style should reinforce the game's theme once established.
> Current implementation supports realistic character models (Mixamo integration).

---

## Progression & Difficulty

> **[TO BE FLESHED OUT]**
>
> **Potential Systems:**
> - **Wave-based survival** - Increasingly difficult enemy configurations
> - **Roguelike runs** - Permadeath with meta-progression
> - **Puzzle levels** - Hand-crafted tactical scenarios
> - **Arena challenges** - Score-based survival modes
> - **Ability unlocks** - New abilities earned through gameplay
> - **Enemy escalation** - New enemy types introduced progressively
>
> **Current State:**
> Game has combat and systems in place but no win/loss conditions or progression structure yet.

---

## Win/Loss Conditions

> **[TO BE DEFINED]**
>
> **Options:**
> - Survive N turns/waves
> - Defeat all enemies
> - Reach extraction point
> - Score threshold
> - Last as long as possible (endless survival)
>
> **Loss Condition:**
> Player health reaches zero (currently implemented in combat system)

---

## Technical Architecture

### Entity-Component-System (ECS)
- **Entities:** Unique ID containers for components
- **Components:** Data-only structs organized by domain (Combat, Movement, State, etc.)
- **Systems:** Logic processors that operate on entities with specific components

### Core Systems
- **TurnSystem** - Turn order and progression
- **PlayerSystem** - Player input handling
- **EnemySystem** - AI decision making
- **MovementSystem** - Movement execution and combat triggers
- **CombatSystem** - Damage calculation and resolution
- **RangeSystem** - Attack range calculation and threat marking
- **AnimationSystem** - Character animation states
- **DashSystem** - Dash ability logic
- **BlockSystem** - Block ability logic

### Hex Grid Mathematics
- **Cube Coordinates** - Vector3I for hex positions
- **Distance Calculation** - Accurate hex range measurement
- **Pathfinding** - A* algorithm on hex grid
- **Range Patterns** - Multiple geometric patterns for attack ranges

---

## Future Features & Expansion Areas

> **[TO BE FLESHED OUT]**

### Potential Feature Additions

#### Gameplay Enhancements
- [ ] More player abilities (cooldown-based special moves)
- [ ] Power-ups or temporary buffs collected from tiles
- [ ] Environmental hazards (lava, spikes, etc.)
- [ ] Interactive map elements (doors, switches, teleporters)
- [ ] Line-of-sight mechanics for stealth/visibility
- [ ] Multiple playable characters with different abilities
- [ ] Combo system for chaining actions

#### Enemy Variety
- [ ] Wizard enemy with AoE attacks (Explosion range)
- [ ] Enemies with special behaviors (flee when low HP, summon allies)
- [ ] Boss encounters with unique mechanics
- [ ] Enemy combinations that synergize

#### Progression Systems
- [ ] Unlock new abilities between runs
- [ ] Upgrade existing abilities (reduced cooldown, increased range)
- [ ] Character customization / build variety
- [ ] Persistent meta-progression currency
- [ ] Daily challenges / seeded runs

#### Content
- [ ] Multiple biomes/environments
- [ ] Hand-crafted puzzle levels
- [ ] Procedurally generated campaigns
- [ ] Story mode with narrative beats

#### Polish & Juice
- [ ] Particle effects for abilities
- [ ] Screen shake on impacts
- [ ] Dynamic camera movements
- [ ] Sound effects and music
- [ ] Hit stop / freeze frames
- [ ] Combo counters and score feedback

#### UI/UX
- [ ] Tutorial system
- [ ] Ability cooldown indicators
- [ ] Turn counter / wave display
- [ ] Pause menu
- [ ] Settings / options
- [ ] Undo move feature (limited uses?)

#### Multiplayer (Ambitious)
- [ ] Local hot-seat multiplayer
- [ ] Asynchronous turn-based multiplayer
- [ ] Competitive puzzle challenges

---

## Design Questions to Resolve

### Immediate Priorities
1. **Theme & Setting** - What is the world? Who are these combatants?
2. **Win/Loss** - What are players trying to achieve?
3. **Progression** - How does the game escalate in difficulty?
4. **Content Scope** - How much content for initial release?

### Balance Considerations
1. Are current ability cooldowns balanced? (Dash: 4 turns, Block: 3 turns)
2. Should there be health regeneration or is it fixed per run?
3. How many enemies should spawn per wave/level?
4. What should the difficulty curve look like?
5. Should blocked tiles be destructible?

### Player Experience
1. What is the target session length? (Quick 5-min runs vs longer campaigns)
2. Should there be difficulty modes?
3. How much randomness vs hand-crafted design?
4. What metrics define "success" for a run?

---

## Development Roadmap

> **[TO BE DEFINED]**
>
> **Current State:** Core combat and ability systems functional
>
> **Suggested Next Steps:**
> 1. Define theme and visual direction
> 2. Implement win/loss conditions
> 3. Create initial progression system (waves or levels)
> 4. Add Wizard enemy type
> 5. Create tutorial level
> 6. Balance pass on abilities and enemy difficulty
> 7. Add sound effects and music
> 8. Polish pass on animations and VFX
> 9. Playtest and iterate

---

## Conclusion

Undergang has a solid tactical combat foundation with unique reactive combat mechanics that create engaging puzzle-like scenarios. The hex-based movement, multiple attack range patterns, and ability system provide deep strategic options.

The next phase of development should focus on:
1. **Establishing identity** - Theme, setting, and narrative context
2. **Defining goals** - Win conditions and progression structure
3. **Content creation** - More enemy types, maps, and scenarios
4. **Polish** - Animations, effects, sound, and juice

The modular ECS architecture and flexible range system make it easy to add new enemy types and mechanics, positioning the game well for iterative development and experimentation.

---

**Document Version:** 1.0
**Last Updated:** 2025-12-22
**Status:** Living document - sections marked [TO BE FLESHED OUT] require additional design work
