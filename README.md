# Undergang

A turn-based tactical game featuring hex-based grid movement and Hoplite-style combat mechanics, built with Godot 4.5 and C#.

## Overview

Undergang is a tactical roguelike where positioning and movement timing are critical. Inspired by games like Hoplite, enemies attack reactively when you move into their range, creating a dynamic puzzle-like combat experience.

## Features

- **Hex-Based Grid System** - Strategic movement and positioning on hexagonal tiles
- **Entity-Component-System Architecture** - Clean, modular game architecture
- **Hoplite-Style Combat** - Enemies attack when you enter their range, rewarding tactical positioning
- **Multiple Enemy Types** - Different attack ranges and patterns (adjacent, diagonal, ranged)
- **Animation System** - Support for Mixamo-rigged characters with automatic state transitions
- **Advanced Lighting** - SSAO, SSIL, and ambient lighting for atmospheric visuals

## Quick Start

### Prerequisites

- [Godot 4.5](https://godotengine.org/download) or later
- .NET 8.0 SDK

### Building

```bash
dotnet build
```

### Running

Open the project in Godot 4.5 and press F5 to run, or click the Play button in the editor.

## Project Structure

```
Undergang/
├── src/
│   ├── Components/      # ECS components (data structures)
│   ├── Systems/         # ECS systems (game logic)
│   ├── Services/        # Global services (Events, Entities, PathFinder)
│   ├── Lib/             # Core utilities and base classes
│   ├── Game/            # GameManager and main game entry point
│   └── Scenes/          # Godot scene files
├── assets/              # Game assets (models, textures, sounds)
└── Game.tscn            # Main game scene
```

## Core Systems

- **TurnSystem** - Manages turn order and game flow
- **PlayerSystem** - Handles player input and actions
- **EnemySystem** - AI behavior and decision making
- **MovementSystem** - Unit movement and pathfinding
- **CombatSystem** - Damage calculation and combat resolution
- **RangeSystem** - Attack range patterns and threat zones
- **AnimationSystem** - Character animation state management
- **RenderSystem** - Visual representation updates

## Combat Mechanics

### Attack Triggers

1. **Enemy Reactive Attacks** - Enemies attack when you move INTO their range
2. **Player Counter-Attacks** - You attack when moving WITHIN an enemy's range (already adjacent)
3. **Enemy Turns** - Enemies pass their turn if you're in range, or move closer if you're not

This creates a tactical puzzle where every move matters and positioning is key to survival.

## Development

### Adding New Systems

1. Create a class inheriting from `System` in `src/Systems/`
2. Register it in `GameManager._Ready()`
3. Implement: `Initialize()`, `Update()`, `Process()`, `Cleanup()`

### Adding New Components

Define components in `src/Components/Components.cs` as readonly record structs:

```csharp
public record struct Health(int Value) {
    public static implicit operator int(Health h) => h.Value;
}
```

### Entity Queries

```csharp
// Get all enemies
var enemies = Entities.Query<Unit, Enemy>();

// Get the player
var player = Entities.Query<Player>().FirstOrDefault();
```

## Documentation

- **CLAUDE.md** - Comprehensive technical reference for AI assistants
- **ANIMATIONS.md** - Animation system integration guide

## License

[License information here]

## Credits

Built with [Godot Engine](https://godotengine.org/)
