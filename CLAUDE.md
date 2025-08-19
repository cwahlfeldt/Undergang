# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Undergang is a turn-based tactical game built with Godot 4.4 and C#. The game features hex-based grid movement, entity-component-system (ECS) architecture, and tactical combat between players and enemies.

## Build and Development Commands

### Building the Project
```bash
dotnet build
```
The project uses .NET 8.0 and builds to `.godot/mono/temp/bin/Debug/Undergang.dll`.

### Running the Game
Open the project in Godot 4.4 and run from the editor, or use Godot's export functionality.

## Architecture

### Entity-Component-System (ECS)
The game uses a custom ECS architecture:

- **Entities**: Simple containers with unique IDs that hold components (`src/Lib/Entity.cs`)
- **Components**: Data structures defined as readonly record structs (`src/Components/Components.cs`)
- **Systems**: Game logic processors that operate on entities with specific components (`src/Systems/`)

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
- **Entities**: Entity management and queries (`src/Services/Entites.cs`)
- **PathFinder**: A* pathfinding on the hex grid (`src/Services/PathFinder.cs`)
- **Materials**: Material management for visual effects (`src/Services/Materials.cs`)
- **Tweener**: Animation and interpolation system (`src/Services/Tweener.cs`)

### Game Flow
1. `GameManager` initializes the systems and creates the initial game state
2. Events trigger system updates through the turn-based cycle
3. Systems process entities and update game state
4. Visual systems render the current state to the screen

## Key Patterns

### Component Design
Components are implemented as readonly record structs with implicit operators:
```csharp
public record struct Health(int Value) { public static implicit operator int(Health health) => health.Value; }
```

### Entity Queries
The `Entities` service provides LINQ-style queries:
```csharp
var enemies = entities.Query<Unit, Enemy>();
var player = entities.Query<Player>().FirstOrDefault();
```

### System Dependencies
Systems receive dependencies through constructor injection managed by `SystemDependencies`.

### Event-Driven Architecture
Systems communicate through the global `Events` service rather than direct coupling.

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
3. Implement required methods: `Initialize()`, `Update()`, `Process()`, `Cleanup()`

### Adding New Components
1. Define in `src/Components/Components.cs` as readonly record structs
2. Add implicit operators for convenience
3. Use marker components (empty structs) for entity tagging

### Entity Management
- Use `Entities.Query<T>()` methods for component-based entity selection
- Entity creation helpers are available in `Entities` service
- Always clean up entities when removing them from the game