# Undergang - Game Design Document

## Overview

**Genre:** Turn-based Tactical Roguelike
**Core Inspiration:** Hoplite
**Platform:** Desktop (Godot 4.5 / C#)
**Target Audience:** Players who enjoy strategic puzzle-like combat and tactical positioning

### High Concept

Undergang is a dark medieval fantasy turn-based tactics game where you must survive the cursed crypts filled with undead warriors. Every move is a life-or-death decision as skeletal enemies attack reactively when you enter their range. Navigate deeper into ancient tombs, using tactical positioning, special abilities, and careful planning to overcome increasingly deadly undead guardians and escape with your life.

---

## Core Pillars

1. **Tactical Depth** - Every movement choice matters; positioning is survival
2. **Reactive Combat** - Enemies respond to your actions, creating dynamic puzzle-like scenarios
3. **Risk vs Reward** - Aggressive play versus cautious positioning
4. **Clarity** - Clear visual feedback for threat zones and movement options

---

## Game Theme & Setting

### Medieval Fantasy - The Crypt of the Fallen

**Setting:** Ancient crypts and burial grounds where the dead refuse to rest. The name "Undergang" (downfall/demise) refers to the cursed underground tombs where skeletal warriors endlessly rise to defend their domain.

**Player Character:** A lone warrior (knight, adventurer, or tomb raider) delving into forbidden crypts seeking treasure, glory, or perhaps to end an undead curse.

**The World:**
- **Dark Medieval Fantasy** - Gothic crypts, ancient stonework, flickering torchlight
- **The Undead Rise** - Skeleton warriors awaken from centuries of slumber
- **Cursed Depths** - Each level represents a deeper chamber of the crypt
- **Tactical Horror** - Not about jump scares, but the slow dread of being surrounded

**Enemy Lore:**
- **Grunts (Skeletal Warriors)** - Former soldiers, still trained in close combat
- **Wizards (Skeletal Mages)** - Undead spellcasters who can project dark magic in all directions
- **Snipers (Skeletal Archers)** - Precision marksmen who fire along precise firing lanes
- Each deeper level contains older, stronger, and more dangerous undead

**Atmosphere:**
- Hex tiles represent ancient flagstones in hexagonal crypt chambers
- Blocked tiles are pillars, rubble, sarcophagi, and collapsed sections
- The tactical combat represents methodical, deadly duels in confined spaces
- Every movement echoes off stone walls as skeletal eyes track your position

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

### Movement (Basic Attack)
- **Range:** Adjacent hex tiles (6 neighbors)
- **Traversal:** Navigate between flagstones, avoiding rubble and pillars
- **Triggers Combat:** Movement is the primary way to engage enemies
- **Flavor:** Careful footwork and blade positioning in close quarters

### Dash (Combat Roll / Shadow Step)
- **Effect:** Swift movement to any tile within 2-tile radius
- **Cooldown:** 4 turns
- **Flavor:** A desperate roll through skeletal warriors or a mystical shadow step
- **Tactical Use:**
  - Escape from surrounded positions
  - Bypass enemy threat zones
  - Quickly close distance to objectives or retreat
  - Does NOT trigger reactive enemy attacks (too fast/magical to intercept)

### Block (Shield Parry / Defensive Stance)
- **Effect:** Negate the next incoming attack
- **Cooldown:** 3 turns (starts after block is consumed)
- **Duration:** Persists until consumed by an attack
- **Flavor:** Raise your shield or take a defensive stance
- **Tactical Use:**
  - Safe passage through a single enemy threat zone
  - Survive when low on health
  - Enables aggressive positioning through danger zones
  - Strategic timing: activate before risky moves

---

## Enemy Types

### Current Enemy Types

All enemies are animated skeleton warriors with distinct combat roles:

#### Skeletal Warrior (Grunt)
- **Type:** Melee undead fighter
- **Attack Range:** Adjacent tiles only (RangeCircle)
- **Behavior:** Rushes into close combat, must be directly next to player
- **Lore:** Former soldiers who retained their muscle memory for blade work
- **Tactical Note:** Easiest to avoid individually, deadly in groups

#### Skeletal Mage (Wizard)
- **Type:** Undead spellcaster
- **Attack Range:** Area-of-effect / Explosion radius pattern
- **Behavior:** Projects dark magic in all directions
- **Lore:** Ancient mages whose spirits cling to forbidden knowledge
- **Tactical Note:** Forces area denial, avoid clustering near them
> **[PARTIAL IMPLEMENTATION]** - Range pattern available, needs full integration

#### Skeletal Archer Variants (Snipers)

The crypt's former marksmen, each guarding a different firing lane:

**Skeletal Archer Q** (SniperAxisQ)
- **Attack Range:** Linear along Q-axis (2-5 tiles)
- **Coverage:** Creates deadly threat "lanes" in one hex direction
- **Lore:** Archers trained to guard the northern approaches

**Skeletal Archer R** (SniperAxisR)
- **Attack Range:** Linear along R-axis (2-5 tiles)
- **Coverage:** Creates threat "lanes" in another hex direction
- **Lore:** Defenders of the eastern corridors

**Skeletal Archer S** (SniperAxisS)
- **Attack Range:** Linear along S-axis (2-5 tiles)
- **Coverage:** Creates threat "lanes" in the third hex direction
- **Lore:** Sentinels of the western passages

**Combined Archer Threat:** Three archer variants together can cover all six hex directions, creating a deadly crossfire that requires careful pathfinding through safe zones.

### Available Range Patterns (for future enemy types)

The engine supports these additional range patterns:
- **RangeDiagonal** - Diagonal lines (2-6 tiles), alternating directions
- **RangeHex** - Hex ring at specific distance (tiles exactly N steps away)
- **RangeExplosion** - Area of effect (all tiles within radius)
- **RangeNGon** - Polygon pattern (triangular shapes in alternating directions)

---

## Map Generation

### Crypt Chamber Generation

Each level represents a unique chamber of the ancient crypt:

**Current Implementation:**
- **Chamber Size:** 5-tile radius hex grid (configurable per level, scales 4-7 tiles)
- **Obstacles:** 24 randomly placed structural elements
- **Tile Variation:** Visual variety through different flagstone patterns (index 20-90)
- **Player Entry:** Fixed spawn point at chamber entrance (0, 4, -4)
- **Undead Positions:** Random skeleton placement with exclusion radius around player entry

### Map Elements

**Hexagonal Crypt Architecture:**
- **Walkable Tiles** - Ancient hexagonal flagstones, cracked and weathered
- **Blocked Tiles** - Structural obstacles creating tactical chokepoints:
  - Stone pillars supporting the ceiling
  - Crumbled sections of collapsed walls
  - Stone sarcophagi (unopened tombs of other undead)
  - Piles of rubble and debris
  - Broken statues and monuments
- **Traversable Indicator** - Clear visual distinction (lighter stone vs dark impassable areas)

**Thematic Level Variation:**
- **Early Levels** (1-3): Smaller, well-preserved chambers with sparse obstacles
- **Mid Levels** (4-6): Larger chambers with more complex obstacle layouts
- **Late Levels** (7-8): Massive, heavily damaged crypts with treacherous terrain

> **Future Enhancement:** Hand-crafted obstacle layouts for specific tactical puzzles in later levels

---

## Visual & Animation System

### Animation States

**Skeletal Enemy Animations** (Implemented):
- **Idle** - Skeleton standing menacingly, subtle breathing/swaying
- **Walking** - Skeletal march toward the player
- **Attack** - Weapon swing or spell casting motion
- **Hurt** - Recoil from damage
- **Die** - Collapse into bone pile
- **Spawn/Awaken** - Rising from the ground or sarcophagus
- **Taunt** - Threatening gestures (optional flourish)

All skeleton enemies use rigged character models with procedural animations.

**Player Animations** (To be implemented):
- Basic movement and combat animations
- Dash ability visual (roll/shadow step effect)
- Block ability visual (shield raise/defensive pose)

### Visual Feedback

**Tactical Information:**
- **Movement Range** - Highlighted walkable flagstone tiles
- **Threat Zones** - Enemy attack ranges clearly marked with ominous overlays
- **Dash Range** - Special highlight for dash ability escape routes
- **Block Indicator** - Shield icon or defensive aura when block is active
- **Health Display** - Current HP visible on all units

**Medieval Fantasy Aesthetics:**
- **Hex Tiles** - Ancient hexagonal flagstones with weathering
- **Blocked Tiles** - Crumbling pillars, stone sarcophagi, collapsed debris
- **Lighting** - Torch-lit atmosphere with dynamic shadows
- **Effects** - Particle effects for magic, dust when skeletons spawn

### Lighting & Atmosphere

**Gothic Crypt Ambience:**
- **SSAO** - Screen-space ambient occlusion emphasizing dark corners
- **SSIL** - Screen-space indirect lighting for torch glow
- **Ambient Lighting** - Dim, eerie blue-green ambient for undead atmosphere
- **Dynamic Lighting** - Flickering torches on pillars
- **Fog/Mist** - Optional atmospheric fog in deeper levels

**Color Palette:**
- Stone grays and browns for architecture
- Sickly greens and blues for undead magic
- Warm orange/yellow for torchlight contrast
- Dark shadows for ominous mood

---

## Progression & Difficulty

### The Eight Crypts of Undergang

The game features **8 handcrafted levels** representing increasingly dangerous chambers of the cursed crypt:

**Level Structure:**
1. **The Awakening** - Tutorial level with 2 skeletal warriors
2. **Distant Threats** - Introduction to ranged enemies (mages)
3. **Line of Sight** - Learn to navigate archer firing lanes
4. **Convergence** - Multiple enemy types working together
5. **Hardened Foes** - Tougher enemies with bonus health
6. **The Gauntlet** - Overwhelming numbers and complexity
7. **Deadly Force** - Enemies hit harder (+1 damage)
8. **Final Stand** - Everything combined, no safety net (rewind disabled)

**Difficulty Scaling:**
- **Map Size:** Grows from 4-tile radius to 7-tile radius
- **Enemy Count:** Increases from 2 to 12+ enemies
- **Enemy Stats:** Progressive bonuses to health and damage
- **Ability Cooldowns:** Dash and Block cooldowns increase in later levels
- **Player Health:** Decreases from 5 HP to 3 HP in later levels
- **Safe Space:** Enemy spawn exclusion radius shrinks (4 tiles → 2 tiles)

**Progression Features:**
- Each level introduces new tactical challenges
- Enemy combinations become more complex and coordinated
- Later levels feature veteran undead with enhanced stats
- Final level removes safety mechanics (no rewind ability)

> **[IN PLANNING]** - See `LEVEL_SYSTEM_PLAN.md` for full technical implementation details

---

## Win/Loss Conditions

### Victory
**Per Level:** Defeat all skeletal enemies in the crypt chamber
- Clear tactical combat puzzle
- No time pressure, pure positioning and strategy
- Progress to deeper crypt levels

**Campaign Victory:** Complete all 8 levels
- Congratulations screen
- Unlock replay mode / level select
- Statistics tracking (turns taken, enemies defeated, etc.)

### Defeat
**Player Death:** Health reaches zero
- Immediate game over for current level
- Option to restart current level
- Return to level select (if unlocked)

**No Permadeath:** Can retry any level
- Levels remain unlocked once reached
- Encourages experimentation and learning
- Suits puzzle-like tactical nature

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
- **AnimationSystem** - Character animation states (skeleton animations, state transitions)
- **DashSystem** - Dash ability logic
- **BlockSystem** - Block ability logic
- **GameStateManager** - Game state history and progression tracking
- **UISystem** - User interface, HUD, ability cooldowns display
- **VictorySystem** - Win condition detection (planned)

### Hex Grid Mathematics
- **Cube Coordinates** - Vector3I for hex positions
- **Distance Calculation** - Accurate hex range measurement
- **Pathfinding** - A* algorithm on hex grid
- **Range Patterns** - Multiple geometric patterns for attack ranges

---

## Future Features & Expansion Areas

### Potential Feature Additions

#### Medieval Fantasy Content
- [ ] **More Undead Types:**
  - Skeletal Champions (mini-bosses with unique abilities)
  - Wraiths (phase through obstacles)
  - Zombie Brutes (high HP, slow movement)
  - Lich (boss enemy with spell variety)
- [ ] **Equipment System:**
  - Different weapons (sword, mace, spear) with varying ranges
  - Armor types affecting mobility vs defense
  - Magical artifacts with special effects
- [ ] **Environmental Theming:**
  - Different crypt types (burial chamber, throne room, torture chamber)
  - Environmental storytelling through architecture
  - Ancient treasure visible in background

#### Gameplay Enhancements
- [ ] More player abilities (Holy Smite, Whirlwind Attack, Healing Prayer)
- [ ] Consumables found in crypts (health potions, holy water, spell scrolls)
- [ ] Environmental hazards (cursed tiles, collapsing floors, magical traps)
- [ ] Interactive elements (lever-activated doors, teleportation circles, hidden paths)
- [ ] Line-of-sight mechanics (hide behind pillars, ambush from darkness)
- [ ] Combo system (reward aggressive multi-kill chains)
- [ ] Multiple playable classes (Knight, Rogue, Cleric)

#### Progression & Replayability
- [ ] New Game+ mode with remixed enemy placements
- [ ] Challenge modes (no abilities, time attack, pacifist run)
- [ ] Persistent upgrades between runs
- [ ] Achievement/trophy system
- [ ] Leaderboards and score tracking
- [ ] Daily challenge crypts

#### Polish & Atmosphere
- [ ] Bone-shattering particle effects
- [ ] Screen shake on heavy impacts
- [ ] Dynamic camera tilt for dramatic moments
- [ ] Medieval fantasy soundtrack (haunting strings, ominous choirs)
- [ ] Combat sound effects (clashing swords, skeleton bone rattles)
- [ ] Ambient crypt sounds (dripping water, distant moans, chains)
- [ ] Death animations for player character
- [ ] Victory pose animations

#### UI/UX Improvements
- [ ] Interactive tutorial level with tooltips
- [ ] Visual ability cooldown timers
- [ ] Turn counter display
- [ ] Enemy intent indicators (show what they'll do next turn)
- [ ] Undo last move (limited uses per level)
- [ ] Pause menu with level restart option
- [ ] Settings (volume, camera sensitivity, visual effects)
- [ ] Codex with enemy lore and tactical tips

#### Narrative Elements
- [ ] Brief text intros for each level
- [ ] Discoverable lore through environmental details
- [ ] Mystery of why the dead won't stay dead
- [ ] Optional story mode vs arcade mode
- [ ] Ending cinematics based on performance

#### Advanced Features (Ambitious)
- [ ] Procedural crypt generation mode (endless mode)
- [ ] Custom level editor
- [ ] Mod support for custom enemies/abilities
- [ ] Asynchronous multiplayer challenge sharing

---

## Design Questions to Resolve

### Balance & Tuning
1. ✓ **Theme Established** - Medieval fantasy crypts with undead enemies
2. Are ability cooldowns balanced across all 8 levels?
3. Should player health regenerate between levels or carry over?
4. Should there be checkpoints (every 2-3 levels)?
5. Is the difficulty curve too steep or too gradual?
6. Should blocked tiles (sarcophagi) sometimes contain surprises?

### Player Experience
1. Target session length per level: 3-5 minutes or 5-10 minutes?
2. Should there be difficulty modes (Easy/Normal/Hard)?
3. How much procedural generation vs hand-crafted puzzles?
4. Should there be a score/ranking system?
5. Is level replay necessary or just "restart" and "continue"?

### Content Questions
1. Should the player character be a specific class or customizable?
2. How much narrative/lore vs pure tactical gameplay?
3. Environmental storytelling vs text exposition?
4. Victory animations/cinematics for level completion?

---

## Development Roadmap

### ✅ Completed (Current State)
- Core tactical combat system with Hoplite-style reactive attacks
- Hex-based grid movement and pathfinding
- ECS architecture with organized component system
- Dash and Block abilities with cooldowns
- Multiple enemy types (Grunt, Sniper variants)
- Skeleton enemy animations (Idle, Walk, Attack, Die, Spawn)
- Animation system with state management
- Visual feedback for movement and threat ranges
- Medieval fantasy theme established

### 🚧 In Progress / Planned
1. **Level System Implementation** (See `LEVEL_SYSTEM_PLAN.md`)
   - LevelDefinition data structure
   - LevelManager service
   - VictorySystem for win conditions
   - Level transition UI
   - All 8 levels configured

2. **Player Character Visuals**
   - Player character model (knight/warrior)
   - Player animation states
   - Dash and Block visual effects

3. **Wizard Enemy Type**
   - Complete implementation of AoE attack pattern
   - Skeletal mage visual distinction
   - Magic effect particles

4. **UI Polish**
   - Level select screen
   - Game over and victory screens
   - Ability cooldown visual indicators
   - Health bars and status displays
   - Tutorial tooltips

5. **Audio**
   - Medieval fantasy soundtrack
   - Combat sound effects
   - Ambient crypt atmosphere
   - UI feedback sounds

6. **Polish Pass**
   - Particle effects (dust, magic, impacts)
   - Screen shake on hits
   - Better tile highlights and indicators
   - Death animations refinement
   - Camera improvements

7. **Balancing & Playtesting**
   - Test all 8 levels for difficulty
   - Tune ability cooldowns
   - Adjust enemy stats
   - Iterate based on feedback

### 🎯 Future Enhancements (Post-Launch)
- Endless/procedural mode
- Additional enemy types
- Equipment/upgrade system
- Challenge modes
- Story/narrative expansion

---

## Conclusion

**Undergang: The Crypt of the Fallen** is a medieval fantasy turn-based tactics game with a strong tactical combat foundation. The unique Hoplite-style reactive combat mechanics create engaging puzzle-like scenarios where every movement matters. The hex-based grid, multiple enemy attack patterns, and cooldown-based abilities provide deep strategic gameplay.

### Current Strengths
- **Solid Core Systems** - Combat, movement, and abilities all functional
- **Clear Theme** - Medieval fantasy crypts with undead skeleton enemies
- **Structured Progression** - 8-level campaign with escalating difficulty planned
- **Technical Foundation** - Modular ECS architecture supports easy expansion
- **Visual Identity** - Skeleton animations and gothic atmosphere established

### Next Phase Priorities
1. **Complete Level System** - Implement the 8-crypt campaign structure
2. **Victory & Game Over** - Win conditions and level transitions
3. **Player Visuals** - Character model and animations
4. **Audio Integration** - Music and sound effects for atmosphere
5. **UI Polish** - Menus, HUD, and feedback systems
6. **Balance & Testing** - Playtest all levels and tune difficulty

### Vision

Undergang aims to deliver tight, strategic turn-based combat in a dark fantasy setting. Each of the eight crypt levels presents a unique tactical puzzle where players must use positioning, timing, and limited abilities to overcome increasingly dangerous undead guardians. The game combines the tactical depth of Hoplite with the atmospheric storytelling of classic dungeon crawlers.

The modular architecture and clear design direction position Undergang well for focused development toward a polished, complete experience.

---

**Document Version:** 2.0
**Last Updated:** 2025-12-23
**Status:** Medieval fantasy theme established, level progression planned, core systems implemented
