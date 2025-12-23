# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Undergang is a turn-based tactical game built with Godot 4.5 and C#. The game features hex-based grid movement, entity-component-system (ECS) architecture, and tactical combat between players and enemies.

## Build and Development Commands

### Building the Project

```bash
dotnet build
```

The project uses .NET 8.0 and builds to `.godot/mono/temp/bin/Debug/Undergang.dll`.

### Running the Game

Open the project in Godot 4.5 and run from the editor, or use Godot's export functionality.

## Architecture

### Hybrid ECS Architecture

The game uses a **hybrid ECS architecture** optimized for turn-based gameplay:

- **Entities**: Simple containers with unique IDs that hold components (`src/Lib/Entity.cs`)
- **Components**: Data structures organized by domain in `src/Components/` (see Component Organization below)
- **Systems**: Game logic processors that operate on entities with specific components (`src/Systems/`)
- **Turn Orchestration**: Explicit action sequencing for clarity (see `ARCHITECTURE.md`)

**Key Philosophy**: Use ECS for entity management and queries. Use direct orchestration for turn flow and action sequencing.

### Core Systems

Systems are managed by the `Systems` class (`src/Services/Systems.cs`) and can be:

- **Sequential**: Execute one after another in turn-based updates
- **Concurrent**: Execute simultaneously for performance

Key systems include:

- `TurnSystem`: Manages turn order and progression, integrates with GameStateManager for snapshots
- `PlayerSystem`: Handles player input and tile selection
- `EnemySystem`: AI behavior for enemy units
- `MovementSystem`: Handles unit movement on the hex grid, integrates combat triggers
- `CombatSystem`: Manages combat resolution and damage application
- `DashSystem`: Player dash ability with cooldown management
- `BlockSystem`: Player block ability (negates next attack)
- `GameStateManager`: Complete game state snapshots and time rewind functionality
- `RangeSystem`: Attack range calculations and threat zone marking
- `AnimationSystem`: State-based animation controller
- `RenderSystem`: Visual representation of game state and input handling
- `UISystem`: Health display, ability buttons, and FPS counter
- `TileHighlightSystem`: Tile highlighting and mesh caching

### Hex Grid System

The game uses a hex-based coordinate system (`src/Lib/HexGrid.cs`) with:

- Cube coordinates (Vector3I) for hex positions
- Range calculations for movement and attack
- Pathfinding integration

### Services

- **Events**: Global event system for decoupled communication (`src/Services/Events.cs`)
- **Entities**: Entity storage, management, and queries (`src/Services/Entities.cs`)
- **EntityFactory**: Entity creation (grid, tiles, units) - accessed via `Entities.Factory` (`src/Services/EntityFactory.cs`)
- **GameStateManager**: Comprehensive game state snapshot and time manipulation system (`src/Services/GameStateManager.cs`)
- **PathFinder**: A* pathfinding on the hex grid (`src/Services/PathFinder.cs`)
- **Materials**: Material management for visual effects (`src/Services/Materials.cs`)
- **Tweener**: Animation and interpolation system (`src/Services/Tweener.cs`)
- **Systems**: System registry and lifecycle management (`src/Services/Systems.cs`)

### Game Flow

1. `GameManager` initializes the systems and creates the initial game state
2. Events trigger system updates through the turn-based cycle
3. Systems process entities and update game state
4. Visual systems render the current state to the screen

## Key Patterns

### Component Organization (Phase 3 Refactoring)

Components are organized by domain in separate files for better maintainability:

**File Structure:**

- `src/Components/Core.cs` - Tile, Traversable, Untraversable, Instance, Name, Coordinate, TileIndex
- `src/Components/Combat.cs` - Health, Damage, AttackRange, Attacker, Target, AttackRangeTile
- `src/Components/Movement.cs` - Movement, MoveRange, DashCooldown, DashModeActive, DashRangeTile, BlockCooldown, BlockActive
- `src/Components/Range.cs` - RangeCircle, RangeDiagonal, RangeExplosion, RangeHex, RangeNGon, RangeAxisQ/R/S
- `src/Components/Units.cs` - Player, Enemy, Grunt, Wizard, SniperAxisQ/R/S, Unit
- `src/Components/State.cs` - CurrentTurn, Active, TurnOrder, WaitingForAction, SelectedTile
- `src/Components/Animation.cs` - CurrentAnimation, AnimationPlayer

All components remain in the `Game.Components` namespace. Import with: `using Game.Components;`

### Component Design

Components are implemented as readonly record structs with implicit operators:

```csharp
public record struct Health(int Value) { public static implicit operator int(Health health) => health.Value; }
```

### Entity Creation (EntityFactory Pattern)

Use the EntityFactory for creating game entities:

```csharp
// Access factory via Entities service
var player = entityManager.Factory.CreatePlayer();
var enemy = entityManager.Factory.CreateEnemy(UnitType.Grunt);
var grid = entityManager.Factory.CreateGrid(mapSize: 5, blockedTilesAmt: 16);
```

### Entity Queries

The `Entities` service provides LINQ-style queries:

```csharp
var enemies = entities.Query<Unit, Enemy>();
var player = entities.Query<Player>().FirstOrDefault();
var tiles = entities.GetTilesInRange(coord, range);
```

### Configuration Management (Centralized)

All game constants are defined in `Config.cs`:

```csharp
Config.PlayerStart              // Player spawn position
Config.PlayerSpawnExclusionRadius  // Enemy spawn exclusion
Config.DefaultMapSize           // Map generation size
Config.DiagonalRangeMin/Max     // Range pattern configuration
```

**Best Practice**: Never hardcode game values. Always use Config constants.

### System Dependencies

Systems receive dependencies through lazy initialization:

```csharp
public override void Initialize()
{
    _combatSystem = Systems.Get<CombatSystem>();
    _animationSystem = Systems.Get<AnimationSystem>();
}
```

**Future Improvement**: See `ARCHITECTURE.md` for recommended constructor injection pattern.

### Event Usage

Events are used for **notifications**, not control flow:

- ✅ Use events for: UI updates, cross-system notifications
- ❌ Avoid events for: Turn sequencing, action orchestration

**See ARCHITECTURE.md** for recommended turn orchestration pattern.

## Scene Structure

- **Main.tscn**: Entry point scene
- **Board.tscn**: Game board visualization
- **Player.tscn**: Player unit representation
- **Enemy.tscn**: Enemy unit representation
- **HexTile.tscn**: Individual hex tile visualization

## Configuration

- `Config.cs`: Game configuration constants
- `project.godot`: Godot project settings
- `Undergang.csproj`: .NET project configuration with Godot.NET.Sdk

## Development Notes

### Adding New Systems

1. Create a class inheriting from `System` in `src/Systems/`
2. Register it in `GameManager._Ready()` using `_systems.Register<T>()` or `_systems.RegisterConcurrent<T>()`
3. Implement lifecycle methods:
   - `Initialize()` - Setup dependencies, subscribe to events
   - `Update()` - Turn-based processing (optional - see ARCHITECTURE.md)
   - `Cleanup()` - Unsubscribe from events, cleanup resources
4. Inject system dependencies in `Initialize()` using `Systems.Get<T>()`

**Note**: Consider whether your system needs `Update()` polling or should use direct method calls. See `ARCHITECTURE.md` for guidance.

### Adding New Components

1. Choose the appropriate domain file in `src/Components/`:
   - Core: Tiles, coordinates, basic properties
   - Combat: Health, damage, attack-related
   - Movement: Movement range, position changes
   - Range: Attack range patterns
   - Units: Unit types, classifications
   - State: Turn management, game state
   - Animation: Animation states, players
2. Define as readonly record struct with implicit operators:
   ```csharp
   public record struct MyComponent(int Value)
   {
       public static implicit operator int(MyComponent c) => c.Value;
   }
   ```
3. Use marker components (empty structs) for tagging:
   ```csharp
   public readonly record struct MyMarker;
   ```

**If adding a new domain**, create a new file following the existing pattern.

### Entity Management

- **Creation**: Use `Entities.Factory.CreateX()` methods
  ```csharp
  var enemy = Entities.Factory.CreateEnemy(UnitType.Sniper);
  ```
- **Queries**: Use `Entities.Query<T>()` for component-based selection
  ```csharp
  var enemies = Entities.Query<Unit, Enemy>();
  var player = Entities.Query<Player>().FirstOrDefault();
  ```
- **Modification**: Add/remove components directly on entities
  ```csharp
  entity.Add(new Health(5));
  entity.Remove<Movement>();
  entity.Update(new Coordinate(newPos));
  ```
- **Cleanup**: Always remove entities when defeated/destroyed
  ```csharp
  Entities.RemoveEntity(entity);
  ```

### Configuration

- **Always use Config constants** instead of hardcoding values
- Add new constants to appropriate section in `Config.cs`:
  - Player settings
  - Map generation settings
  - Range settings
- Example:

  ```csharp
  // Bad
  var range = 5;

  // Good
  public static int NewFeatureRange = 5;  // In Config.cs
  var range = Config.NewFeatureRange;      // In code
  ```

### Code Quality Best Practices

1. **No magic numbers** - Use Config constants
2. **DRY principle** - Extract duplicate code into helpers
3. **Clear naming** - Methods should describe their action
4. **Separation of concerns** - Keep systems focused on single responsibility
5. **Documentation** - Add XML comments for public methods and complex logic

### Recent Refactoring (Phases 1-3)

The codebase has undergone significant cleanup:

- **Phase 1**: Critical bug fixes, code deduplication
- **Phase 2**: Configuration centralization, EntityFactory separation, complete range implementations
- **Phase 3**: Component organization into domain-focused files

See git history for detailed changes.

## Combat System (Hoplite-Style)

The game implements **Hoplite-style tactical combat** where positioning and movement timing are critical.

### Core Combat Mechanics

#### Attack Triggers

1. **Enemy Reactive Attacks**: Enemies attack when the player moves INTO their threat range

   - Happens during player's turn, triggered by player movement
   - Enemy does NOT move when attacking reactively
   - Only triggers when entering a NEW enemy's range (not when already adjacent)

2. **Player Attacks**: Player attacks when moving WITHIN an enemy's range

   - Player must be ALREADY adjacent to an enemy before moving
   - Moving to another tile still adjacent to the same enemy triggers attack
   - Does NOT trigger when first entering enemy range

3. **Enemy Turn Behavior**: On enemy's own turn, enemies NEVER attack
   - If player is in range: Enemy passes turn (waits)
   - If player is NOT in range: Enemy moves toward player

### Combat Flow Implementation

**Key Files:**

- `src/Systems/CombatSystem.cs` - Combat resolution and damage application
- `src/Systems/MovementSystem.cs` - Combat trigger logic during movement
- `src/Systems/EnemySystem.cs` - Enemy AI and turn behavior
- `src/Systems/RangeSystem.cs` - Attack range calculation and threat marking

**Combat Resolution Steps:**

1. Check if combat should trigger (based on movement and position)
2. Play attack animation (lunge forward and back)
3. Apply damage to defender
4. Check if defender is defeated
5. Remove defeated units from game
6. Update pathfinding and range systems

### Animation System

The game features a comprehensive animation system designed for Mixamo-rigged characters:

**Files:**

- `src/Systems/AnimationSystem.cs` - State-based animation controller
- `src/Components/Components.cs` - Animation components (`CurrentAnimation`, `AnimationPlayer`)
- `src/Lib/Enums/AnimationState.cs` - Animation states enum
- `ANIMATIONS.md` - Complete animation integration guide

**Animation States:**

- `Idle` - Default resting state
- `Move` - Walking/running animation
- `Attack` - Attack animation
- `Hurt` - Taking damage animation
- `Die` - Death animation
- `Spawn`, `Victory`, `Defeat` - Optional states

**Automatic Triggers:**

- Movement → Sets `Move` state during movement, returns to `Idle` when complete
- Combat → Plays `Attack` (attacker) and `Hurt` (defender) animations
- Defeat → Triggers `Die` animation

**Animation Naming Convention:**
Animations must be named: `{UnitType}_{AnimationState}`

- Examples: `Player_Idle`, `Grunt_Attack`, `Sniper_Move`

**Fallback Behavior:**

- System works without animations (graceful degradation)
- Uses Tweener for basic movement interpolation as fallback
- No errors if AnimationPlayer or animations are missing

**Integration:**
The system is ready for Mixamo characters. See `ANIMATIONS.md` for complete workflow:

1. Download character + animations from Mixamo
2. Import FBX files into Godot
3. Rename animations following convention
4. Replace unit scene visuals with Mixamo character
5. Zero code changes required!

### Range System Architecture

The game supports multiple attack range patterns through components:

**Range Type Components (All Implemented):**

- `RangeCircle` - Adjacent tiles (6 hex neighbors)
- `RangeDiagonal` - Directional lines along 6 hex directions, distance 2-5 (Hoplite Archer style)
- `RangeHex` - Hex ring at specific distance (tiles exactly N steps away)
- `RangeExplosion` - Area of effect (all tiles within radius)
- `RangeNGon` - Polygon pattern (alternating directions forming triangular shape)

All range patterns are configurable via `Config.cs` constants.

**Dynamic Range Calculation:**

```csharp
// Automatically determines range based on unit's range type component
var attackTiles = RangeSystem.GetAttackRangeTiles(unit, position);
```

**Threat Zone Marking:**

- Each frame, `RangeSystem.UpdateRanges()` marks all tiles within each unit's attack range
- Tiles get `AttackRangeTile(unitId)` component indicating which unit threatens them
- Used by MovementSystem to detect when player enters enemy threat zones

### Combat Components

**Essential Combat Components:**

- `Health(int)` - Current hit points
- `Damage(int)` - Attack damage value
- `AttackRange(int)` - Attack range distance
- `AttackRangeTile(int unitId)` - Marks threatened tiles with attacker's ID
- `RangeCircle/Diagonal/etc` - Marker for attack pattern type
- `Enemy` - Marker for enemy units
- `Player` - Marker for player unit

### Adding New Enemy Types with Different Ranges

Example: Creating a ranged sniper enemy with diagonal range:

1. **Implement the range pattern** in `RangeSystem`:

```csharp
public static IEnumerable<Vector3I> GetRangeDiagonal(Vector3I center)
{
    var tiles = new List<Vector3I>();
    for (int i = 1; i <= 5; i++)  // 5 tiles range
    {
        tiles.Add(center + new Vector3I(i, -i, 0));   // NE
        tiles.Add(center + new Vector3I(-i, i, 0));   // SW
        tiles.Add(center + new Vector3I(i, 0, -i));   // SE
        tiles.Add(center + new Vector3I(-i, 0, i));   // NW
    }
    return tiles;
}
```

2. **Create the enemy** with the range component:

```csharp
var sniper = Entities.Factory.CreateEnemy(UnitType.Sniper);
sniper.Add(new RangeDiagonal());  // Automatically uses diagonal range
sniper.Add(new Damage(2));
sniper.Add(new Health(3));
```

3. **Combat system automatically handles it** - No additional code needed!

### Important Combat Rules

1. **Multiple Enemy Attacks**: ALL enemies attack when the player moves into their overlapping threat zones (changed from single attack)
2. **Player Counter-Attack**: Player counter-attacks the enemy they were ALREADY fighting when moving within that enemy's range
3. **Death During Combat**: If player dies from any attack during movement, movement stops immediately and no further attacks occur
4. **Turn Completion**: All combat resolves before `UnitActionComplete` event fires
5. **Visual Feedback**: Attack animations complete before damage is applied

### Debugging Combat

Debug output in `CombatSystem.ResolveCombat()` shows:

- Attacker/Defender IDs and types (Enemy/Player)
- Damage dealt and health changes
- Combat trigger location

Enable verbose logging to trace:

- When enemies pass turn vs move
- When player enters/exits threat zones
- When attacks trigger and why

---

## Player Abilities System

The game features special abilities that add tactical depth beyond basic movement and combat.

### Dash Ability

**Purpose**: Quick escape or repositioning - move 2 tiles in any direction, bypassing enemies without triggering combat.

**Key Files**:
- `src/Systems/DashSystem.cs` - Dash logic and cooldown management
- `src/Components/Movement.cs` - DashCooldown, DashModeActive, DashRangeTile components
- `src/Config.cs` - Dash configuration constants

**Mechanics**:
1. **Activation**: Player toggles dash mode (D key or UI button)
2. **Range**: All traversable tiles within radius of 2 (configurable via `Config.DashRange`)
3. **No Combat**: Dashing does NOT trigger enemy attacks (unlike normal movement)
4. **Cooldown**: 4 turns after use (configurable via `Config.DashCooldown`)
5. **Visual Feedback**: Tiles in dash range are highlighted differently

**Configuration**:
```csharp
Config.DashRange = 2;              // Dash distance
Config.DashCooldown = 4;           // Turns before dash available again
Config.DashAnimationSpeed = 0.25f; // Animation duration
```

**Usage**:
```csharp
// Check if dash is available
bool canDash = _dashSystem.IsDashAvailable(player);
int cooldown = _dashSystem.GetRemainingCooldown(player);

// Toggle dash mode
_dashSystem.ToggleDashMode();

// Execute dash (called automatically by TurnSystem)
await _movementSystem.ExecuteDash(player, destination);
```

**Cooldown Management**:
- Cooldown starts AFTER dash is used
- Cooldown ticks down at the START of each player turn
- UI shows cooldown counter when ability is unavailable

### Block Ability

**Purpose**: Defensive ability that negates the next incoming attack.

**Key Files**:
- `src/Systems/BlockSystem.cs` - Block logic and cooldown management
- `src/Components/Movement.cs` - BlockCooldown, BlockActive components
- `src/Systems/CombatSystem.cs` - Block consumption logic

**Mechanics**:
1. **Activation**: Player toggles block on/off (B key or UI button)
2. **Persistence**: Block stays active until consumed by an attack
3. **Consumption**: First incoming attack is negated, block is removed, cooldown starts
4. **Cooldown**: 3 turns after block is consumed (configurable via `Config.BlockCooldown`)
5. **Visual Feedback**: UI button glows when block is active

**Configuration**:
```csharp
Config.BlockCooldown = 3;  // Turns before block available again
```

**Usage**:
```csharp
// Check if block is available
bool canBlock = _blockSystem.IsBlockAvailable(player);
bool isActive = _blockSystem.IsBlockActive(player);
int cooldown = _blockSystem.GetRemainingCooldown(player);

// Toggle block
_blockSystem.ToggleBlock();

// Consume block (called automatically by CombatSystem)
_blockSystem.ConsumeBlock(player);
```

**Important Notes**:
- Block can be toggled OFF before being consumed (no cooldown penalty)
- Cooldown only starts AFTER block is consumed by an attack
- Block persists across turns until consumed
- Only ONE attack is blocked, then cooldown begins

### UI Integration

Both abilities have dedicated UI buttons managed by `UISystem`:

**Visual States**:
- **Available**: Full color, clickable
- **Active** (Dash mode or Block active): Bright highlight, special styling
- **Cooldown**: Grayed out, shows "Cooldown: X" label

**Keyboard Shortcuts**:
- `D` key: Toggle dash mode
- `B` key: Toggle block

---

## Game State Management & Rewind System

The game features a comprehensive time manipulation system that allows players to rewind turns, implemented through the `GameStateManager`.

### Architecture Overview

**Key File**: `src/Services/GameStateManager.cs`

The GameStateManager is the **single source of truth** for game state snapshots and time manipulation:

**Core Responsibilities**:
1. Capture complete game state at turn boundaries
2. Store snapshot history (up to 100 turns, configurable)
3. Restore previous game states with animations
4. Manage rewind cooldown and availability

**Philosophy**: Centralized snapshot management - all snapshot logic lives in GameStateManager, not scattered across systems.

### Snapshot System

#### What Gets Captured

**Tier 1 - Persistent Gameplay State** (ALWAYS captured):
```csharp
// Core components
Tile, Traversable, Untraversable, Coordinate, TileIndex, Name

// Combat components
Health, Damage, AttackRange

// Movement & abilities
MoveRange, DashCooldown, BlockCooldown, BlockActive

// Unit identity
Player, Enemy, Grunt, Wizard, SniperAxisQ/R/S, Unit

// Range patterns
RangeCircle, RangeDiagonal, RangeHex, RangeExplosion, RangeNGon, RangeAxisQ/R/S

// Turn management
TurnOrder
```

**Tier 2 - Transient State** (NOT captured - rebuilt on restore):
```csharp
// Godot visual nodes
Instance, AnimationPlayer

// UI state
CurrentAnimation, CurrentTurn, WaitingForAction, SelectedTile

// Derived state (recalculated)
AttackRangeTile, DashRangeTile, Movement
```

#### Snapshot Timing

**Capture Trigger**: At the **START** of player's turn (BEFORE they act)

```csharp
// In TurnSystem.StartUnitTurn()
if (unit.Has<Player>())
{
    _gameStateManager.CaptureSnapshot(_currentTurnIndex);
}
```

**Why start of turn?**
- Captures state BEFORE player makes mistakes
- Ensures health, position, abilities all captured correctly
- Allows rewinding to undo the action they're about to take

#### Snapshot Data Structure

```csharp
public record GameStateSnapshot
{
    public int TurnNumber { get; init; }
    public DateTime CapturedAt { get; init; }
    public int NextEntityId { get; init; }             // Entity ID counter
    public int CurrentTurnIndex { get; init; }         // Turn order position
    public IReadOnlyList<EntityStateSnapshot> Entities { get; init; }
    public int UnitCount { get; init; }                // Metadata for debugging
    public int TileCount { get; init; }
}

public record EntityStateSnapshot
{
    public int EntityId { get; init; }
    public bool IsUnit { get; init; }
    public Vector3I? AnimationOrigin { get; init; }    // Position to animate FROM
    public IReadOnlyDictionary<Type, object> Components { get; init; }
}
```

### Rewind Functionality

#### Basic Rewind (One Turn Back)

```csharp
// Rewind to previous turn
var result = await _gameStateManager.RewindOneTurn();

if (result.Success)
{
    GD.Print($"Rewound to turn {result.TurnRewindedTo}");
    GD.Print($"Respawned {result.RespawnedUnitIds.Count} units");
}
else
{
    GD.Print($"Rewind failed: {result.FailureReason}");
}
```

#### Advanced Rewind (Specific Turn)

```csharp
// Rewind to a specific turn number
var result = await _gameStateManager.RewindToTurn(turnNumber: 5);
```

#### Rewind Process (10 Steps)

1. **Validation**: Check cooldown, player alive, snapshot exists
2. **Identification**: Find units that need respawning (dead in current state, alive in snapshot)
3. **Animation**: Smoothly animate existing units back to snapshot positions
4. **State Restoration**:
   - Remove all current unit nodes
   - Reset entity ID counter
   - Recreate units from snapshot
   - Restore tile components (keep visual nodes)
5. **Visual Rebuild**: Instantiate Godot scenes for all units
6. **Input Setup**: Reattach mouse/click handlers to units
7. **Fade Effects**: Apply fade-in animation to respawned units
8. **Derived State**: Recalculate ranges, pathfinding, etc.
9. **Cleanup**: Clear mesh caches, set animations to Idle
10. **History Management**: Remove future snapshots (no redo), start cooldown, restart turn

#### Rewind Configuration

```csharp
Config.RewindCooldownTurns = 3;         // Turns to wait between rewinds
Config.MaxHistoryDepth = 100;           // Max snapshots retained
Config.RewindAnimationSpeed = 0.3f;     // Animation duration
Config.RespawnFadeInDuration = 0.5f;    // Fade-in effect for respawned units
```

### Rewind Availability

**Requirements for rewind**:
1. At least 2 snapshots exist (current turn + previous turn)
2. Cooldown is 0 (not recently rewound)
3. Player is alive

```csharp
bool canRewind = _gameStateManager.CanRewind;
int cooldown = _gameStateManager.CooldownRemaining;
int historyDepth = _gameStateManager.HistoryDepth;
```

**Cooldown Management**:
- Starts AFTER successful rewind
- Ticks down at START of each player turn
- Prevents spam rewinding (maintains challenge)

### Visual Effects

**Rewind Animations**:
1. **Position Interpolation**: Units smoothly glide back to previous positions
2. **Respawn Fade-In**: Dead units fade in from transparent to opaque
3. **Mesh Cache Clear**: Ensures fresh tile highlights after rewind

**UI Indicators**:
- Rewind button shows history depth: "REWIND (5)" means 5 snapshots available
- Cooldown counter: "Cooldown: 3" shows turns remaining
- Disabled state when no snapshots exist

### Integration with Systems

**TurnSystem Integration**:
```csharp
// Capture snapshot at start of player's turn
_gameStateManager.CaptureSnapshot(_currentTurnIndex);

// Tick cooldown each player turn
_gameStateManager.TickCooldown();

// Restart turn after rewind
public void RestartPlayerTurn(int restoredTurnIndex)
{
    _currentTurnIndex = restoredTurnIndex;
    player.Add(new CurrentTurn());
    player.Add(new WaitingForAction());
    Events.OnTurnChanged(player);
}
```

**UISystem Integration**:
```csharp
// Rewind button press
if (_gameStateManager.CanRewind)
{
    await _gameStateManager.RewindOneTurn();
    UpdateRewindButtonState();
}
```

### Memory Management

**Snapshot Lifecycle**:
- History is capped at `Config.MaxHistoryDepth` (default: 100 snapshots)
- Oldest snapshots automatically pruned when limit reached
- Each snapshot stores full entity state (components only, not visual nodes)

**Visual Node Management**:
- Old unit nodes freed using `Free()` (immediate, not `QueueFree()`)
- Prevents memory leaks during rewind
- Tiles keep their visual nodes (only components reset)

### Debugging & Diagnostics

**GameStateManager Logging**:
```
[GameStateManager] Captured snapshot: Turn 5, 4 units, 169 tiles, History depth: 6
[GameStateManager] === REWINDING to Turn 4 ===
[GameStateManager] Respawning 1 units
[GameStateManager] Animating unit 42 from (0, 2, -2) to (0, 3, -3)
[GameStateManager] Restored 173 entities
[GameStateManager] Rebuilt Grunt_1 at (1, 1, -2)
[GameStateManager] === REWIND COMPLETE ===
```

**Snapshot Inspection**:
```csharp
// Get read-only snapshot history
var history = _gameStateManager.GetHistory();

// Peek at specific snapshot
var lastSnapshot = _gameStateManager.PeekSnapshot(turnsBack: 0);
var twoTurnsAgo = _gameStateManager.PeekSnapshot(turnsBack: 2);
```

### Edge Cases & Failure Modes

**Rewind Blocked When**:
- No snapshots exist (first turn of game)
- Only 1 snapshot exists (need current + previous)
- Cooldown active (recently rewound)
- Player is dead (game over state)

**Respawn Logic**:
- Defeated enemies are fully restored (health, position, abilities)
- Visual fade-in effect distinguishes respawned units
- All component state matches snapshot exactly

### Best Practices

**DO**:
- ✅ Capture snapshots at START of player turn (before action)
- ✅ Store only Tier 1 components (gameplay state)
- ✅ Rebuild Tier 2 components (visual/derived state) on restore
- ✅ Use `Free()` for immediate node cleanup
- ✅ Recalculate ranges/pathfinding after restore

**DON'T**:
- ❌ Capture snapshots mid-action (capture BEFORE action)
- ❌ Store visual nodes in snapshots (rebuild them)
- ❌ Use `QueueFree()` for unit nodes (causes stale references)
- ❌ Skip derived state recalculation (causes desyncs)

---

## Additional Documentation

### Architecture & Patterns

- **ARCHITECTURE.md** - Detailed guide on turn flow orchestration and the recommended "Option 2" pattern for improving turn-based game flow. Read this for architectural guidance on explicit action sequencing vs event-driven patterns.

### Animation System

- **ANIMATIONS.md** - Complete guide for integrating Mixamo characters and animations

### Project Files

- **README.md** - Project overview and quick start guide
- **.gitignore** - Git ignore patterns
- **project.godot** - Godot engine configuration

---

## Summary

Undergang is a well-structured turn-based tactical game using a hybrid ECS architecture with comprehensive game state management. The codebase features centralized configuration, organized components, player abilities, and time manipulation mechanics.

**Key Features:**

- **Hybrid ECS Architecture**: Clean component-based design with explicit turn orchestration
- **Hoplite-Style Combat**: Reactive enemy attacks, player counter-attacks, positioning-based tactics
- **Player Abilities**: Dash (escape/reposition) and Block (negate attacks) with cooldown management
- **Time Rewind System**: Full game state snapshots with animated replay and enemy respawning
- **Flexible Range System**: Multiple attack patterns (circle, diagonal, hex ring, explosion, n-gon, axis-based)
- **Animation System**: State-based animations ready for Mixamo character integration
- **Comprehensive UI**: Health display, ability buttons with cooldowns, rewind interface, FPS counter

**Current Game Loop:**

1. Player turn starts → GameStateManager captures snapshot
2. Player chooses action: move, dash, or block
3. Movement triggers combat (enemy reactive attacks, player counter-attacks)
4. Abilities tick cooldowns
5. Enemy turns execute (move toward player or pass)
6. Repeat
7. Player can rewind to undo mistakes (with cooldown penalty)

**Architecture Strengths:**

- **Centralized State Management**: GameStateManager owns all snapshot logic
- **Component Organization**: Domain-focused files (Core, Combat, Movement, Range, Units, State, Animation)
- **Configuration-Driven**: All game constants in Config.cs (no magic numbers)
- **Event System**: Notifications only, not control flow (direct orchestration for turns)
- **Memory Efficient**: Immediate node cleanup, capped snapshot history, component-only storage

**For New Developers:**

1. **Start here**: Read this CLAUDE.md completely
2. **Architecture**: Review `ARCHITECTURE.md` for turn flow patterns
3. **Components**: Explore `src/Components/` to understand data structures
4. **Systems**: Study `src/Systems/` for game logic (start with TurnSystem, GameStateManager)
5. **Configuration**: Check `Config.cs` for all game constants
6. **Combat**: Read Combat System section to understand Hoplite mechanics
7. **Rewind**: Study GameStateManager section for snapshot system

**When Making Changes:**

**✅ DO:**
- Use Config constants (never hardcode values)
- Add components to appropriate domain file (Core, Combat, Movement, etc.)
- Use EntityFactory for entity creation (`Entities.Factory.CreateX()`)
- Capture Tier 1 components in GameStateManager when adding new gameplay state
- Follow existing patterns (readonly record structs, implicit operators)
- Add XML comments for public methods
- Test with rewind feature (ensure new state captured/restored correctly)

**❌ DON'T:**
- Hardcode game values (use Config constants)
- Create entities manually (use EntityFactory)
- Store visual nodes in snapshots (rebuild on restore)
- Add components without considering snapshot system
- Use events for control flow (use direct orchestration)
- Skip testing abilities + rewind interaction

**Key Systems to Understand:**

1. **TurnSystem** - Turn order, action orchestration, snapshot triggers
2. **GameStateManager** - State snapshots, rewind, history management
3. **MovementSystem** - Movement, dash, combat triggers
4. **CombatSystem** - Damage, block consumption, defeat handling
5. **DashSystem** - Dash mode, range calculation, cooldown
6. **BlockSystem** - Block activation, consumption, cooldown
7. **RangeSystem** - Threat zones, range patterns, derived state
8. **UISystem** - Health, ability buttons, rewind UI

**Testing Checklist for New Features:**

- [ ] Does it work with basic gameplay?
- [ ] Does it work with Dash ability?
- [ ] Does it work with Block ability?
- [ ] Does it work with Rewind (state captured/restored correctly)?
- [ ] Are Config constants used (no hardcoded values)?
- [ ] Is state added to GameStateManager._capturedComponentTypes if needed?
- [ ] Does UI update correctly?
- [ ] Are there memory leaks (check node cleanup)?
