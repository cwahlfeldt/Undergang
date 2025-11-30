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
- `TurnSystem`: Manages turn order and progression
- `PlayerSystem`: Handles player input and actions
- `EnemySystem`: AI behavior for enemy units
- `MovementSystem`: Handles unit movement on the hex grid
- `CombatSystem`: Manages combat resolution
- `RenderSystem`: Visual representation of game state

### Hex Grid System
The game uses a hex-based coordinate system (`src/Lib/HexGrid.cs`) with:
- Cube coordinates (Vector3I) for hex positions
- Range calculations for movement and attack
- Pathfinding integration

### Services
- **Events**: Global event system for decoupled communication (`src/Services/Events.cs`)
- **Entities**: Entity storage, management, and queries (`src/Services/Entities.cs`)
- **EntityFactory**: Entity creation (grid, tiles, units) - accessed via `Entities.Factory` (`src/Services/EntityFactory.cs`)
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
- `src/Components/Core.cs` - Tile, Instance, Name, Coordinate, TileIndex
- `src/Components/Combat.cs` - Health, Damage, AttackRange, Attacker, Target
- `src/Components/Movement.cs` - Movement, MoveRange
- `src/Components/Range.cs` - RangeCircle, RangeDiagonal, RangeExplosion, RangeHex, RangeNGon
- `src/Components/Units.cs` - Player, Enemy, Grunt, Sniper, Unit
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

1. **Single Attack Per Movement**: Only one enemy attacks per player movement, even if multiple enemies threaten the destination
2. **Player Counter-Attack**: Player only counter-attacks the enemy they were ALREADY fighting
3. **Death During Movement**: If player dies from enemy attack, movement stops immediately
4. **Turn Completion**: Combat completes before `UnitActionComplete` event fires
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

Undergang is a well-structured turn-based tactical game using a hybrid ECS architecture. The codebase has been recently refactored (Phases 1-3) for improved maintainability, with centralized configuration, organized components, and clear separation between entity creation and management.

**Key strengths:**
- Clean component-based design
- Flexible range system supporting multiple attack patterns
- Hoplite-style tactical combat mechanics
- Ready for Mixamo character integration
- Well-documented codebase with clear patterns

**For new developers:**
1. Start by reading this file completely
2. Review `ARCHITECTURE.md` for turn flow patterns
3. Explore `src/Components/` to understand data structures
4. Look at `src/Systems/` for game logic
5. Check `Config.cs` for game constants

**When making changes:**
- Use Config constants, never hardcode values
- Add components to appropriate domain file
- Use EntityFactory for entity creation
- Follow existing patterns for consistency
- See `ARCHITECTURE.md` for recommended turn orchestration approach