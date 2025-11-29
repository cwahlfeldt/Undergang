# Code-First Migration Guide

## Overview

This guide shows how to do the **entire migration in code** without using the Godot editor. Everything - scenes, signals, nodes, components - can be created programmatically.

---

## Signals in Code

### You DON'T Need the Editor for Signals

Signals are just C# events with the `[Signal]` attribute. They work completely in code:

**File**: `src/Nodes/Components/HealthComponent.cs`

```csharp
using Godot;

namespace Undergang.Nodes.Components;

public partial class HealthComponent : Node
{
    // Define signal - NO EDITOR NEEDED
    [Signal]
    public delegate void DiedEventHandler();

    [Signal]
    public delegate void DamagedEventHandler(int amount, int remaining);

    private int _current;

    public int MaxHealth { get; set; } = 10;

    public int Current
    {
        get => _current;
        set
        {
            var old = _current;
            _current = Mathf.Clamp(value, 0, MaxHealth);

            if (_current < old)
            {
                // Emit signal in code
                EmitSignal(SignalName.Damaged, old - _current, _current);
            }

            if (_current == 0)
            {
                // Emit signal in code
                EmitSignal(SignalName.Died);
            }
        }
    }

    public override void _Ready()
    {
        _current = MaxHealth;
    }
}
```

### Connecting Signals in Code

```csharp
// Subscribe to signal (C# event style)
healthComponent.Died += OnDied;
healthComponent.Damaged += (amount, remaining) => GD.Print($"Took {amount} damage");

// Or use Godot style
healthComponent.Connect(HealthComponent.SignalName.Died, Callable.From(OnDied));

// Unsubscribe
healthComponent.Died -= OnDied;
```

---

## Creating Scenes Programmatically

### You DON'T Need .tscn Files

Everything can be built in code:

**File**: `src/Nodes/Units/Unit.cs`

```csharp
using Godot;
using Undergang.Nodes.Components;

namespace Undergang.Nodes.Units;

/// <summary>
/// Base unit class - built entirely in code
/// </summary>
public partial class Unit : CharacterBody3D
{
    public UnitType Type { get; set; }

    // Component references
    public HealthComponent Health { get; private set; }
    public PositionComponent Position { get; private set; }
    public DamageComponent Damage { get; private set; }
    public MovementComponent Movement { get; private set; }

    public override void _Ready()
    {
        BuildScene();
        CacheComponents();
        ConnectSignals();
    }

    /// <summary>
    /// Build the entire scene hierarchy in code
    /// </summary>
    private void BuildScene()
    {
        // Add components as children
        Health = new HealthComponent { Name = "Health" };
        AddChild(Health);

        Position = new PositionComponent { Name = "Position" };
        AddChild(Position);

        Damage = new DamageComponent { Name = "Damage" };
        AddChild(Damage);

        Movement = new MovementComponent { Name = "Movement" };
        AddChild(Movement);

        // Add collision shape
        var collision = new CollisionShape3D { Name = "Collision" };
        var shape = new CapsuleShape3D { Height = 2.0f, Radius = 0.5f };
        collision.Shape = shape;
        AddChild(collision);

        // Add visual mesh
        var mesh = new MeshInstance3D { Name = "Mesh" };
        mesh.Mesh = new CapsuleMesh { Height = 2.0f, Radius = 0.5f };
        AddChild(mesh);
    }

    private void CacheComponents()
    {
        // Already cached during BuildScene
    }

    private void ConnectSignals()
    {
        Health.Died += OnDied;
        Position.PositionChanged += OnPositionChanged;
    }

    protected virtual void OnDied()
    {
        GD.Print($"{Type} died");
        QueueFree();
    }

    protected virtual void OnPositionChanged(Vector3I oldPos, Vector3I newPos)
    {
        // Update visual position
        GlobalPosition = HexGrid.HexToWorld(newPos);
    }

    public override void _ExitTree()
    {
        // Cleanup
        if (Health != null) Health.Died -= OnDied;
        if (Position != null) Position.PositionChanged -= OnPositionChanged;
    }
}
```

**File**: `src/Nodes/Units/Player.cs`

```csharp
using Godot;

namespace Undergang.Nodes.Units;

public partial class Player : Unit
{
    public override void _Ready()
    {
        Type = UnitType.Player;
        base._Ready();

        // Configure player-specific settings
        Health.MaxHealth = 10;
        Damage.Value = 3;

        // Add player-specific mesh material
        var mesh = GetNode<MeshInstance3D>("Mesh");
        var material = new StandardMaterial3D
        {
            AlbedoColor = Colors.Blue
        };
        mesh.SetSurfaceOverrideMaterial(0, material);
    }

    protected override void OnDied()
    {
        GD.Print("Player died - Game Over!");
        GetTree().CallDeferred("reload_current_scene");
    }
}
```

**File**: `src/Nodes/Units/Enemy.cs`

```csharp
using Godot;
using Undergang.Nodes.Components;

namespace Undergang.Nodes.Units;

public partial class Enemy : Unit
{
    public RangeComponent Range { get; private set; }
    public AIComponent AI { get; private set; }

    public override void _Ready()
    {
        Type = UnitType.Grunt; // Override in factory
        base._Ready(); // Calls BuildScene

        // Add enemy-specific components AFTER base._Ready()
        Range = new RangeComponent { Name = "Range" };
        AddChild(Range);

        AI = new AIComponent { Name = "AI" };
        AddChild(AI);

        CacheEnemyComponents();

        // Red mesh
        var mesh = GetNode<MeshInstance3D>("Mesh");
        var material = new StandardMaterial3D
        {
            AlbedoColor = Colors.Red
        };
        mesh.SetSurfaceOverrideMaterial(0, material);
    }

    private void CacheEnemyComponents()
    {
        Range = GetNode<RangeComponent>("Range");
        AI = GetNode<AIComponent>("AI");
    }
}
```

---

## Factory Pattern (No .tscn Files)

**File**: `src/Core/UnitFactory.cs`

```csharp
using Godot;
using Undergang.Nodes.Units;

namespace Undergang.Core;

/// <summary>
/// Create units entirely in code - NO .tscn FILES NEEDED
/// </summary>
public static class UnitFactory
{
    public static Player CreatePlayer(Vector3I hexPos)
    {
        var player = new Player();

        // _Ready() will build the scene
        // We just configure the data

        // Position will be set after _Ready()
        player.Ready += () =>
        {
            player.Health.MaxHealth = 10;
            player.Health.Current = 10;
            player.Damage.Value = 3;
            player.Position.HexPosition = hexPos;
        };

        return player;
    }

    public static Enemy CreateEnemy(UnitType type, Vector3I hexPos)
    {
        var enemy = new Enemy();

        enemy.Ready += () =>
        {
            enemy.Type = type;

            switch (type)
            {
                case UnitType.Grunt:
                    enemy.Health.MaxHealth = 5;
                    enemy.Damage.Value = 2;
                    enemy.Range.Pattern = RangePattern.Circle;
                    enemy.Range.Distance = 1;
                    break;

                case UnitType.Sniper:
                    enemy.Health.MaxHealth = 3;
                    enemy.Damage.Value = 3;
                    enemy.Range.Pattern = RangePattern.Diagonal;
                    enemy.Range.Distance = 5;
                    break;
            }

            enemy.Health.Current = enemy.Health.MaxHealth;
            enemy.Position.HexPosition = hexPos;
        };

        return enemy;
    }
}
```

---

## Component Definitions (Pure Code)

All components are just C# classes - no editor needed:

**File**: `src/Nodes/Components/PositionComponent.cs`

```csharp
using Godot;

namespace Undergang.Nodes.Components;

public partial class PositionComponent : Node
{
    [Signal]
    public delegate void PositionChangedEventHandler(Vector3I oldPos, Vector3I newPos);

    private Vector3I _hexPosition;

    public Vector3I HexPosition
    {
        get => _hexPosition;
        set
        {
            if (_hexPosition == value) return;

            var old = _hexPosition;
            _hexPosition = value;
            EmitSignal(SignalName.PositionChanged, old, value);
        }
    }
}
```

**File**: `src/Nodes/Components/DamageComponent.cs`

```csharp
using Godot;

namespace Undergang.Nodes.Components;

public partial class DamageComponent : Node
{
    public int Value { get; set; } = 1;
}
```

**File**: `src/Nodes/Components/MovementComponent.cs`

```csharp
using Godot;

namespace Undergang.Nodes.Components;

public partial class MovementComponent : Node
{
    public int MoveRange { get; set; } = 3;
    public float MoveSpeed { get; set; } = 1.0f;
}
```

**File**: `src/Nodes/Components/RangeComponent.cs`

```csharp
using Godot;

namespace Undergang.Nodes.Components;

public enum RangePattern
{
    Circle,      // Adjacent tiles
    Diagonal,    // Diagonal lines
    Line,        // Straight line
    Explosion    // Area of effect
}

public partial class RangeComponent : Node
{
    public RangePattern Pattern { get; set; } = RangePattern.Circle;
    public int Distance { get; set; } = 1;
}
```

**File**: `src/Nodes/Components/AIComponent.cs`

```csharp
using Godot;

namespace Undergang.Nodes.Components;

public enum AIBehavior
{
    Aggressive,  // Always move toward player
    Defensive,   // Wait until player is close
    Patrol       // Move in pattern
}

public partial class AIComponent : Node
{
    public AIBehavior Behavior { get; set; } = AIBehavior.Aggressive;
}
```

---

## Systems (Pure Code)

Systems are just nodes with logic:

**File**: `src/Nodes/Systems/GameSystem.cs`

```csharp
using Godot;
using Undergang.Core;

namespace Undergang.Nodes.Systems;

/// <summary>
/// Base system - created in code, no editor needed
/// </summary>
public partial class GameSystem : Node
{
    protected GameWorld World { get; private set; }
    protected UnitManager Units { get; private set; }
    protected TurnManager Turns { get; private set; }

    public override void _Ready()
    {
        World = GetParent<GameWorld>();
        Units = World.Units;
        Turns = World.Turns;

        Initialize();
    }

    protected virtual void Initialize() { }
    public virtual void ProcessTurn() { }
    protected virtual void Cleanup() { }

    public override void _ExitTree()
    {
        Cleanup();
    }
}
```

**File**: `src/Nodes/Systems/RangeSystem.cs`

```csharp
using Godot;
using System.Collections.Generic;
using System.Linq;
using Undergang.Nodes.Units;
using Undergang.Nodes.Components;
using Undergang.Lib;

namespace Undergang.Nodes.Systems;

public partial class RangeSystem : GameSystem
{
    private Dictionary<Vector3I, Unit> _threatMap = new();

    protected override void Initialize()
    {
        // Subscribe to position changes
        Turns.TurnStarted += OnTurnStarted;
    }

    private void OnTurnStarted(Unit unit)
    {
        UpdateThreatMap();
    }

    public void UpdateThreatMap()
    {
        _threatMap.Clear();

        var enemies = Units.GetEnemies();

        foreach (var enemy in enemies)
        {
            if (!enemy.Health.IsAlive) continue;

            var tiles = GetAttackRangeTiles(enemy);

            foreach (var tile in tiles)
            {
                // Only store if not already threatened (first enemy wins)
                if (!_threatMap.ContainsKey(tile))
                {
                    _threatMap[tile] = enemy;
                }
            }
        }
    }

    public IEnumerable<Vector3I> GetAttackRangeTiles(Unit unit)
    {
        if (unit.GetNodeOrNull<RangeComponent>("Range") is not RangeComponent range)
            return Enumerable.Empty<Vector3I>();

        var center = unit.Position.HexPosition;

        return range.Pattern switch
        {
            RangePattern.Circle => GetRangeCircle(center, range.Distance),
            RangePattern.Diagonal => GetRangeDiagonal(center, range.Distance),
            RangePattern.Line => GetRangeLine(center, range.Distance),
            _ => Enumerable.Empty<Vector3I>()
        };
    }

    private IEnumerable<Vector3I> GetRangeCircle(Vector3I center, int distance)
    {
        return HexGrid.GetNeighbors(center); // For distance 1
    }

    private IEnumerable<Vector3I> GetRangeDiagonal(Vector3I center, int distance)
    {
        var tiles = new List<Vector3I>();
        for (int i = 1; i <= distance; i++)
        {
            tiles.Add(center + new Vector3I(i, -i, 0));
            tiles.Add(center + new Vector3I(-i, i, 0));
            tiles.Add(center + new Vector3I(i, 0, -i));
            tiles.Add(center + new Vector3I(-i, 0, i));
            tiles.Add(center + new Vector3I(0, i, -i));
            tiles.Add(center + new Vector3I(0, -i, i));
        }
        return tiles;
    }

    private IEnumerable<Vector3I> GetRangeLine(Vector3I center, int distance)
    {
        // Implement straight line range
        return Enumerable.Empty<Vector3I>();
    }

    public Unit GetThreateningUnit(Vector3I tile)
    {
        return _threatMap.GetValueOrDefault(tile);
    }

    public bool IsThreatened(Vector3I tile)
    {
        return _threatMap.ContainsKey(tile);
    }

    protected override void Cleanup()
    {
        Turns.TurnStarted -= OnTurnStarted;
    }
}
```

---

## GameWorld Bootstrap (Pure Code)

**File**: `src/Core/GameWorld.cs`

```csharp
using Godot;
using Undergang.Nodes.Systems;

namespace Undergang.Core;

/// <summary>
/// Root game node - built entirely in code
/// NO SCENE FILES NEEDED
/// </summary>
public partial class GameWorld : Node
{
    public UnitManager Units { get; private set; }
    public TurnManager Turns { get; private set; }

    // Systems
    public RangeSystem Range { get; private set; }
    public CombatSystem Combat { get; private set; }
    public MovementSystem Movement { get; private set; }
    public AISystem AI { get; private set; }
    public InputSystem Input { get; private set; }

    public override void _Ready()
    {
        BuildWorld();
    }

    private void BuildWorld()
    {
        // Create managers
        Units = new UnitManager { Name = "UnitManager" };
        AddChild(Units);

        Turns = new TurnManager { Name = "TurnManager" };
        AddChild(Turns);

        // Create systems container
        var systemsNode = new Node { Name = "Systems" };
        AddChild(systemsNode);

        // Create systems
        Range = new RangeSystem { Name = "RangeSystem" };
        systemsNode.AddChild(Range);

        Combat = new CombatSystem { Name = "CombatSystem" };
        systemsNode.AddChild(Combat);

        Movement = new MovementSystem { Name = "MovementSystem" };
        systemsNode.AddChild(Movement);

        AI = new AISystem { Name = "AISystem" };
        systemsNode.AddChild(AI);

        Input = new InputSystem { Name = "InputSystem" };
        systemsNode.AddChild(Input);

        // Wait for all children to be ready, then initialize game
        CallDeferred(nameof(InitializeGame));
    }

    private void InitializeGame()
    {
        // Create player
        var player = UnitFactory.CreatePlayer(new Vector3I(0, 0, 0));
        Units.AddUnit(player);

        // Create enemies
        var grunt1 = UnitFactory.CreateEnemy(UnitType.Grunt, new Vector3I(3, -3, 0));
        Units.AddUnit(grunt1);

        var grunt2 = UnitFactory.CreateEnemy(UnitType.Grunt, new Vector3I(-2, 2, 0));
        Units.AddUnit(grunt2);

        var sniper = UnitFactory.CreateEnemy(UnitType.Sniper, new Vector3I(5, -5, 0));
        Units.AddUnit(sniper);

        // Start game
        Turns.StartGame();
    }
}
```

---

## Entry Point (Minimal Scene)

You ONLY need ONE minimal .tscn file - the entry point:

**Main.tscn** (created in editor):
```
Main (Node)
└── GameWorld (GameWorld) <- Attach GameWorld.cs script
```

That's it! Everything else builds itself in code.

**OR** do it 100% in code:

**File**: `src/Main.cs`

```csharp
using Godot;
using Undergang.Core;

namespace Undergang;

/// <summary>
/// Entry point - builds entire game in code
/// </summary>
public partial class Main : Node
{
    public override void _Ready()
    {
        // Build entire game world in code
        var world = new GameWorld { Name = "GameWorld" };
        AddChild(world);

        // Add camera
        var camera = new Camera3D
        {
            Name = "Camera",
            Position = new Vector3(0, 20, 20),
            Rotation = new Vector3(-45, 0, 0)
        };
        AddChild(camera);

        // Add lighting
        var light = new DirectionalLight3D
        {
            Name = "Sun",
            Rotation = new Vector3(-45, 30, 0),
            ShadowEnabled = true
        };
        AddChild(light);
    }
}
```

---

## Component Query Helpers (Pure Code)

**File**: `src/Core/UnitManager.cs`

```csharp
using Godot;
using System.Collections.Generic;
using System.Linq;
using Undergang.Nodes.Units;
using Undergang.Nodes.Components;

namespace Undergang.Core;

public partial class UnitManager : Node
{
    private Node _container;

    public override void _Ready()
    {
        _container = new Node { Name = "Units" };
        AddChild(_container);
    }

    public void AddUnit(Unit unit)
    {
        _container.AddChild(unit);
    }

    public void RemoveUnit(Unit unit)
    {
        unit.QueueFree();
    }

    // Queries - all in code
    public IEnumerable<Unit> GetAllUnits()
    {
        return _container.GetChildren().OfType<Unit>();
    }

    public Player GetPlayer()
    {
        return _container.GetChildren().OfType<Player>().FirstOrDefault();
    }

    public IEnumerable<Enemy> GetEnemies()
    {
        return _container.GetChildren().OfType<Enemy>();
    }

    public Unit GetUnitAt(Vector3I hexPos)
    {
        return GetAllUnits().FirstOrDefault(u => u.Position.HexPosition == hexPos);
    }

    public IEnumerable<Unit> GetUnitsInRange(Vector3I center, int range)
    {
        return GetAllUnits().Where(u =>
            HexGrid.Distance(u.Position.HexPosition, center) <= range
        );
    }
}
```

---

## When DO You Need the Editor?

### Minimal Editor Usage

You ONLY need the editor for:

1. **Visual assets** (if you want them):
   - Import 3D models (.glb, .fbx)
   - Import textures
   - Import animations
   - That's it!

2. **Project setup** (one time):
   - Create project
   - Set window size
   - Set autoload singletons (optional)

3. **Debugging** (optional):
   - Remote scene tree inspector
   - Watch values
   - Breakpoints (or use your IDE)

### Example: Adding a 3D Model

```csharp
public partial class Player : Unit
{
    public override void _Ready()
    {
        base._Ready();

        // Replace simple mesh with imported model
        var mesh = GetNode<MeshInstance3D>("Mesh");
        mesh.QueueFree(); // Remove simple mesh

        // Load imported model (this is the ONLY part needing editor import)
        var modelScene = GD.Load<PackedScene>("res://assets/models/player.glb");
        var model = modelScene.Instantiate<Node3D>();
        model.Name = "Model";
        AddChild(model);
    }
}
```

---

## Complete Code-First Workflow

### Step 1: Project Setup (Editor - 2 minutes)

1. Create new Godot project
2. Set C# as language
3. Set project settings (window size, etc.)
4. **Done with editor**

### Step 2: Write Code (VSCode/Rider)

All development happens in your IDE:

```
src/
├── Core/
│   ├── GameWorld.cs          # Root node
│   ├── UnitManager.cs        # Unit queries
│   ├── TurnManager.cs        # Turn logic
│   └── UnitFactory.cs        # Unit creation
├── Nodes/
│   ├── Components/
│   │   ├── HealthComponent.cs
│   │   ├── PositionComponent.cs
│   │   ├── DamageComponent.cs
│   │   └── ... (all components)
│   ├── Units/
│   │   ├── Unit.cs           # Base unit
│   │   ├── Player.cs         # Player unit
│   │   └── Enemy.cs          # Enemy unit
│   └── Systems/
│       ├── GameSystem.cs     # Base system
│       ├── CombatSystem.cs
│       ├── MovementSystem.cs
│       ├── RangeSystem.cs
│       └── AISystem.cs
└── Main.cs                   # Entry point
```

### Step 3: One Line in Editor

Create **Main.tscn**:
- Add Node
- Attach `Main.cs` script
- Set as main scene
- **Done**

### Step 4: Run

Press F5. Everything builds itself.

---

## Signal Cheat Sheet

### Define Signal (in class)

```csharp
[Signal]
public delegate void MyEventEventHandler();

[Signal]
public delegate void MyEventWithArgsEventHandler(int value, string name);
```

### Emit Signal

```csharp
EmitSignal(SignalName.MyEvent);
EmitSignal(SignalName.MyEventWithArgs, 42, "test");
```

### Subscribe to Signal (C# style - RECOMMENDED)

```csharp
component.MyEvent += OnMyEvent;
component.MyEventWithArgs += (value, name) => GD.Print($"{name}: {value}");
```

### Subscribe to Signal (Godot style)

```csharp
component.Connect(Component.SignalName.MyEvent, Callable.From(OnMyEvent));
```

### Unsubscribe

```csharp
component.MyEvent -= OnMyEvent;
// or
component.Disconnect(Component.SignalName.MyEvent, Callable.From(OnMyEvent));
```

---

## Advantages of Code-First

### 1. Version Control Friendly

```diff
+ Easy to read diffs
+ No binary .tscn files with merge conflicts
+ Clear code review
```

### 2. Refactoring

```csharp
// Rename component? IDE handles it
// Extract method? IDE handles it
// No broken scene references
```

### 3. Type Safety

```csharp
// Compile-time checks
var health = unit.Health; // Autocomplete works
health.Current = 10;      // Type-safe

// vs .tscn:
var health = unit.GetNode("Health"); // Runtime string, can break
```

### 4. Testable

```csharp
[Test]
public void TestUnitCreation()
{
    var unit = new Player();
    unit._Ready(); // Manually call lifecycle

    Assert.That(unit.Health.Current).IsEqual(10);
}
```

### 5. Portable

- Copy/paste code between projects
- No scene dependencies
- Works with any Godot version

---

## Disadvantages (and Solutions)

### 1. No Visual Preview

**Problem**: Can't see scene in editor

**Solution**:
```csharp
// Add debug visualization
public override void _Ready()
{
    base._Ready();

    if (OS.IsDebugBuild())
    {
        DrawDebugInfo();
    }
}
```

### 2. No Inspector Values

**Problem**: Can't tweak values in inspector

**Solution**:
```csharp
// Use [Export] for tweakable values
[Export]
public int MaxHealth { get; set; } = 10;

[Export]
public Color TeamColor { get; set; } = Colors.Blue;

// Or use config file
public override void _Ready()
{
    LoadFromConfig("res://config/player_stats.json");
}
```

### 3. Initial Setup Verbose

**Problem**: More code to write upfront

**Solution**: Use templates and generators
```csharp
// Create component template
// snippets in your IDE
```

---

## Hybrid Approach (Recommended)

**Code for logic, Editor for assets**:

```csharp
public partial class Player : Unit
{
    public override void _Ready()
    {
        // Logic in code
        Type = UnitType.Player;
        base._Ready();

        // Assets from editor (if you have them)
        LoadVisuals();
    }

    private void LoadVisuals()
    {
        // Only use editor-imported assets if they exist
        if (ResourceLoader.Exists("res://assets/models/player.glb"))
        {
            var model = GD.Load<PackedScene>("res://assets/models/player.glb");
            var instance = model.Instantiate();
            GetNode("Mesh").AddChild(instance);
        }
        else
        {
            // Fallback to code-generated mesh
            GD.Print("No model found, using default mesh");
        }
    }
}
```

---

## Summary

### ✅ You CAN Do in Code

- [x] Define signals
- [x] Emit signals
- [x] Connect to signals
- [x] Create nodes
- [x] Build scene hierarchy
- [x] Add components
- [x] Configure properties
- [x] Create systems
- [x] Manage lifecycle
- [x] **Everything except importing assets**

### ❌ You CANNOT Do in Code

- [ ] Import 3D models (need editor)
- [ ] Import textures (need editor)
- [ ] Import audio (need editor)
- [ ] Edit project settings (need editor or manual .godot file editing)

### Recommended Workflow

1. **Write all logic in code** (your IDE)
2. **Import visual assets in editor** (if needed)
3. **Run and debug** (editor or IDE debugger)

**Result**: 95% code, 5% editor. Best of both worlds!
