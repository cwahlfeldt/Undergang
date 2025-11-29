# Migration Guide: ECS to Godot-Native Architecture

## Table of Contents
1. [Overview](#overview)
2. [Migration Strategy](#migration-strategy)
3. [Phase 1: Component Migration](#phase-1-component-migration)
4. [Phase 2: Entity to Scene Migration](#phase-2-entity-to-scene-migration)
5. [Phase 3: System Migration](#phase-3-system-migration)
6. [Phase 4: Service Layer Refactor](#phase-4-service-layer-refactor)
7. [Phase 5: Event System Migration](#phase-5-event-system-migration)
8. [Phase 6: Final Integration](#phase-6-final-integration)
9. [Testing Strategy](#testing-strategy)
10. [Rollback Plan](#rollback-plan)

---

## Overview

### Current Architecture Problems

1. **Pseudo-ECS**: Components stored as dictionaries with poor performance
2. **Global Service Confusion**: Unclear ownership and lifecycle
3. **LINQ Performance**: Heavy queries every frame
4. **Fighting Godot**: Not leveraging engine strengths
5. **Debugging Difficulty**: State scattered across services

### Target Architecture Benefits

1. **Godot-Native**: Leverage scene tree and node system
2. **Clear Ownership**: GameWorld owns all state
3. **Inspector Visibility**: Debug state in editor
4. **Type Safety**: Signals instead of string-based events
5. **Better Performance**: Scene tree optimized for spatial queries

### Migration Timeline Estimate

- **Phase 1-2**: 2-3 days (Components + Entities)
- **Phase 3-4**: 3-4 days (Systems + Services)
- **Phase 5-6**: 2-3 days (Events + Integration)
- **Testing**: 2-3 days
- **Total**: ~10-12 development days

---

## Migration Strategy

### Parallel Development Approach

**DO NOT** delete old code immediately. Instead:

1. Create new architecture alongside old
2. Port systems one at a time
3. Run both in parallel with feature flags
4. Compare outputs for correctness
5. Delete old code only when new is proven

### Directory Structure

```
src/
├── Legacy/              # Move old code here during migration
│   ├── Components/
│   ├── Systems/
│   └── Services/
├── Nodes/               # New node-based components
│   ├── Components/
│   ├── Systems/
│   └── Units/
└── Core/                # Core game logic
    ├── GameWorld.cs
    ├── TurnManager.cs
    └── Commands/
```

### Feature Flags

Add to `Config.cs`:
```csharp
public static class Config
{
    public const bool USE_NEW_ARCHITECTURE = false; // Toggle during migration
}
```

---

## Phase 1: Component Migration

### Step 1.1: Create Base Component Class

**File**: `src/Nodes/Components/GameComponent.cs`

```csharp
using Godot;

namespace Undergang.Nodes.Components;

/// <summary>
/// Base class for all game components.
/// Components are Nodes attached to unit entities.
/// </summary>
public partial class GameComponent : Node
{
    /// <summary>
    /// The unit this component is attached to.
    /// </summary>
    public Unit Owner { get; private set; }

    public override void _Ready()
    {
        Owner = GetParent<Unit>();
        if (Owner == null)
        {
            GD.PushError($"{Name} must be child of Unit node");
            QueueFree();
        }
    }
}
```

### Step 1.2: Convert Components to Nodes

#### Example: Health Component

**Old** (`src/Components/Components.cs`):
```csharp
public readonly record struct Health(int Value)
{
    public static implicit operator int(Health health) => health.Value;
}
```

**New** (`src/Nodes/Components/HealthComponent.cs`):
```csharp
using Godot;

namespace Undergang.Nodes.Components;

public partial class HealthComponent : GameComponent
{
    [Signal]
    public delegate void DiedEventHandler();

    [Signal]
    public delegate void DamagedEventHandler(int amount, int newHealth);

    [Signal]
    public delegate void HealedEventHandler(int amount, int newHealth);

    [Export]
    public int MaxHealth { get; set; } = 10;

    private int _current;

    [Export]
    public int Current
    {
        get => _current;
        set
        {
            var old = _current;
            _current = Mathf.Clamp(value, 0, MaxHealth);

            if (_current < old)
                EmitSignal(SignalName.Damaged, old - _current, _current);
            else if (_current > old)
                EmitSignal(SignalName.Healed, _current - old, _current);

            if (_current == 0)
                EmitSignal(SignalName.Died);
        }
    }

    public bool IsAlive => Current > 0;
    public float HealthPercent => (float)Current / MaxHealth;

    public override void _Ready()
    {
        base._Ready();
        _current = MaxHealth;
    }

    public void TakeDamage(int amount)
    {
        Current -= amount;
    }

    public void Heal(int amount)
    {
        Current += amount;
    }

    public void ResetToMax()
    {
        Current = MaxHealth;
    }
}
```

#### Component Migration Checklist

Convert each component following this pattern:

- [ ] **HealthComponent** (example above)
- [ ] **PositionComponent**
- [ ] **DamageComponent**
- [ ] **MovementComponent**
- [ ] **RangeComponent**

**Template for simple data components**:

```csharp
using Godot;

namespace Undergang.Nodes.Components;

public partial class DamageComponent : GameComponent
{
    [Export]
    public int Value { get; set; } = 1;
}
```

**Template for position component**:

```csharp
using Godot;

namespace Undergang.Nodes.Components;

public partial class PositionComponent : GameComponent
{
    [Signal]
    public delegate void PositionChangedEventHandler(Vector3I oldPos, Vector3I newPos);

    private Vector3I _hexPosition;

    public Vector3I HexPosition
    {
        get => _hexPosition;
        set
        {
            var old = _hexPosition;
            _hexPosition = value;
            EmitSignal(SignalName.PositionChanged, old, value);
        }
    }

    public Vector3 WorldPosition
    {
        get => Owner.GlobalPosition;
        set => Owner.GlobalPosition = value;
    }
}
```

### Step 1.3: Create Component Helper Extensions

**File**: `src/Nodes/Components/ComponentExtensions.cs`

```csharp
using Godot;

namespace Undergang.Nodes.Components;

public static class ComponentExtensions
{
    public static T GetComponent<T>(this Node node) where T : GameComponent
    {
        return node.GetNodeOrNull<T>(typeof(T).Name.Replace("Component", ""));
    }

    public static bool HasComponent<T>(this Node node) where T : GameComponent
    {
        return GetComponent<T>(node) != null;
    }

    public static bool TryGetComponent<T>(this Node node, out T component) where T : GameComponent
    {
        component = GetComponent<T>(node);
        return component != null;
    }
}
```

**Usage**:
```csharp
// Clean API similar to old system
var health = unit.GetComponent<HealthComponent>();
if (unit.TryGetComponent<DamageComponent>(out var damage))
{
    // Use damage
}
```

---

## Phase 2: Entity to Scene Migration

### Step 2.1: Create Base Unit Class

**File**: `src/Nodes/Units/Unit.cs`

```csharp
using Godot;
using Undergang.Nodes.Components;

namespace Undergang.Nodes.Units;

/// <summary>
/// Base class for all units (Player, Enemy, etc.)
/// Units are CharacterBody3D nodes with component children.
/// </summary>
public partial class Unit : CharacterBody3D
{
    [Export]
    public UnitType Type { get; set; }

    // Component shortcuts (cached for performance)
    public HealthComponent Health { get; private set; }
    public PositionComponent Position { get; private set; }
    public DamageComponent Damage { get; private set; }
    public MovementComponent Movement { get; private set; }

    public override void _Ready()
    {
        // Cache component references
        Health = GetComponent<HealthComponent>();
        Position = GetComponent<PositionComponent>();
        Damage = GetComponent<DamageComponent>();
        Movement = GetComponent<MovementComponent>();

        // Subscribe to death
        if (Health != null)
        {
            Health.Died += OnDied;
        }
    }

    protected virtual void OnDied()
    {
        // Play death animation, then remove
        // Override in subclasses for specific behavior
        QueueFree();
    }

    public override void _ExitTree()
    {
        // Cleanup
        if (Health != null)
        {
            Health.Died -= OnDied;
        }
    }
}
```

### Step 2.2: Create Unit Scenes

#### Player.tscn Structure

```
Player (Unit/CharacterBody3D)
├── Health (HealthComponent)
├── Position (PositionComponent)
├── Damage (DamageComponent)
├── Movement (MovementComponent)
├── PlayerTag (Node) - Marker component
├── CollisionShape3D
├── MeshInstance3D (or AnimatedModel)
└── AnimationPlayer
```

**Create via script**:

**File**: `src/Nodes/Units/Player.cs`

```csharp
using Godot;
using Undergang.Nodes.Components;

namespace Undergang.Nodes.Units;

public partial class Player : Unit
{
    public override void _Ready()
    {
        Type = UnitType.Player;
        base._Ready();
    }

    protected override void OnDied()
    {
        // Player-specific death behavior
        GD.Print("Player died! Game Over");
        GetTree().CallDeferred("reload_current_scene");
    }
}
```

#### Enemy.tscn Structure

```
Enemy (Unit/CharacterBody3D)
├── Health (HealthComponent)
├── Position (PositionComponent)
├── Damage (DamageComponent)
├── Movement (MovementComponent)
├── Range (RangeComponent)
├── EnemyTag (Node) - Marker component
├── AI (AIComponent) - Enemy-specific
├── CollisionShape3D
├── MeshInstance3D
└── AnimationPlayer
```

**File**: `src/Nodes/Units/Enemy.cs`

```csharp
using Godot;
using Undergang.Nodes.Components;

namespace Undergang.Nodes.Units;

public partial class Enemy : Unit
{
    public AIComponent AI { get; private set; }
    public RangeComponent Range { get; private set; }

    public override void _Ready()
    {
        Type = UnitType.Grunt; // Or set via export
        base._Ready();

        AI = GetComponent<AIComponent>();
        Range = GetComponent<RangeComponent>();
    }

    protected override void OnDied()
    {
        // Enemy-specific death
        // Maybe drop loot, update score, etc.
        base.OnDied();
    }
}
```

### Step 2.3: Create Unit Factory

**File**: `src/Core/UnitFactory.cs`

```csharp
using Godot;
using Undergang.Nodes.Units;
using Undergang.Nodes.Components;

namespace Undergang.Core;

/// <summary>
/// Factory for creating units with components.
/// </summary>
public static class UnitFactory
{
    private static PackedScene _playerScene;
    private static PackedScene _enemyScene;

    public static void Initialize()
    {
        _playerScene = GD.Load<PackedScene>("res://scenes/units/Player.tscn");
        _enemyScene = GD.Load<PackedScene>("res://scenes/units/Enemy.tscn");
    }

    public static Player CreatePlayer(Vector3I hexPos)
    {
        var player = _playerScene.Instantiate<Player>();

        // Configure player stats
        player.Health.MaxHealth = 10;
        player.Health.Current = 10;
        player.Damage.Value = 3;
        player.Position.HexPosition = hexPos;

        return player;
    }

    public static Enemy CreateEnemy(UnitType type, Vector3I hexPos)
    {
        var enemy = _enemyScene.Instantiate<Enemy>();
        enemy.Type = type;

        // Configure based on type
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

        return enemy;
    }
}
```

### Step 2.4: Create Unit Manager

**File**: `src/Core/UnitManager.cs`

```csharp
using Godot;
using System.Collections.Generic;
using System.Linq;
using Undergang.Nodes.Units;

namespace Undergang.Core;

/// <summary>
/// Manages all units in the game.
/// Replaces the old Entities service.
/// </summary>
public partial class UnitManager : Node
{
    private Node _unitsContainer;

    public override void _Ready()
    {
        _unitsContainer = new Node { Name = "Units" };
        AddChild(_unitsContainer);
    }

    public void AddUnit(Unit unit)
    {
        _unitsContainer.AddChild(unit);
    }

    public void RemoveUnit(Unit unit)
    {
        unit.QueueFree();
    }

    // Query methods (replaces old Entities.Query)
    public IEnumerable<Unit> GetAllUnits()
    {
        return _unitsContainer.GetChildren().OfType<Unit>();
    }

    public Player GetPlayer()
    {
        return _unitsContainer.GetChildren().OfType<Player>().FirstOrDefault();
    }

    public IEnumerable<Enemy> GetEnemies()
    {
        return _unitsContainer.GetChildren().OfType<Enemy>();
    }

    public IEnumerable<Unit> GetUnitsAt(Vector3I hexPos)
    {
        return GetAllUnits().Where(u => u.Position.HexPosition == hexPos);
    }

    public Unit GetUnitAt(Vector3I hexPos)
    {
        return GetUnitsAt(hexPos).FirstOrDefault();
    }

    public IEnumerable<T> GetUnitsWithComponent<T>() where T : GameComponent
    {
        return GetAllUnits().Where(u => u.HasComponent<T>());
    }
}
```

---

## Phase 3: System Migration

### Step 3.1: Create Base System Class

**File**: `src/Nodes/Systems/GameSystem.cs`

```csharp
using Godot;
using Undergang.Core;

namespace Undergang.Nodes.Systems;

/// <summary>
/// Base class for all game systems.
/// Systems are Nodes that process game logic.
/// </summary>
public partial class GameSystem : Node
{
    protected GameWorld World { get; private set; }
    protected UnitManager Units { get; private set; }
    protected TurnManager Turns { get; private set; }

    public override void _Ready()
    {
        World = GetParent<GameWorld>();
        Units = World.GetNode<UnitManager>("UnitManager");
        Turns = World.GetNode<TurnManager>("TurnManager");

        Initialize();
    }

    /// <summary>
    /// Called once when system is ready.
    /// </summary>
    protected virtual void Initialize() { }

    /// <summary>
    /// Called each turn.
    /// </summary>
    public virtual void ProcessTurn() { }

    /// <summary>
    /// Called when system is removed.
    /// </summary>
    protected virtual void Cleanup() { }

    public override void _ExitTree()
    {
        Cleanup();
    }
}
```

### Step 3.2: Migrate Combat System

**File**: `src/Nodes/Systems/CombatSystem.cs`

```csharp
using Godot;
using Undergang.Nodes.Units;
using Undergang.Nodes.Components;

namespace Undergang.Nodes.Systems;

/// <summary>
/// Handles combat resolution between units.
/// </summary>
public partial class CombatSystem : GameSystem
{
    [Signal]
    public delegate void CombatStartedEventHandler(Unit attacker, Unit defender);

    [Signal]
    public delegate void CombatEndedEventHandler(Unit attacker, Unit defender, bool defenderDied);

    /// <summary>
    /// Execute combat between attacker and defender.
    /// </summary>
    public void ExecuteCombat(Unit attacker, Unit defender)
    {
        if (attacker == null || defender == null) return;
        if (!attacker.Health.IsAlive || !defender.Health.IsAlive) return;

        EmitSignal(SignalName.CombatStarted, attacker, defender);

        // Get damage values
        var damage = attacker.Damage?.Value ?? 1;

        GD.Print($"Combat: {attacker.Type} attacks {defender.Type} for {damage} damage");

        // Play attack animation (non-blocking)
        PlayAttackAnimation(attacker, defender);

        // Apply damage after animation
        GetTree().CreateTimer(0.3).Timeout += () =>
        {
            if (IsInstanceValid(defender) && defender.Health != null)
            {
                defender.Health.TakeDamage(damage);

                var died = !defender.Health.IsAlive;
                EmitSignal(SignalName.CombatEnded, attacker, defender, died);

                if (died)
                {
                    GD.Print($"{defender.Type} was defeated!");
                }
            }
        };
    }

    private void PlayAttackAnimation(Unit attacker, Unit defender)
    {
        // Lunge toward defender
        var originalPos = attacker.GlobalPosition;
        var targetPos = defender.GlobalPosition;
        var direction = (targetPos - originalPos).Normalized();
        var lungePos = originalPos + direction * 1.5f;

        var tween = CreateTween();
        tween.TweenProperty(attacker, "global_position", lungePos, 0.15);
        tween.TweenProperty(attacker, "global_position", originalPos, 0.15);

        // Play attack animation on AnimationPlayer if exists
        var animPlayer = attacker.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (animPlayer != null && animPlayer.HasAnimation("Attack"))
        {
            animPlayer.Play("Attack");
        }
    }
}
```

### Step 3.3: Migrate Movement System

**File**: `src/Nodes/Systems/MovementSystem.cs`

```csharp
using Godot;
using System.Collections.Generic;
using Undergang.Nodes.Units;
using Undergang.Lib;

namespace Undergang.Nodes.Systems;

/// <summary>
/// Handles unit movement and pathfinding.
/// </summary>
public partial class MovementSystem : GameSystem
{
    [Signal]
    public delegate void MovementStartedEventHandler(Unit unit, Vector3I from, Vector3I to);

    [Signal]
    public delegate void MovementCompletedEventHandler(Unit unit, Vector3I destination);

    private PathFinder _pathfinder;
    private CombatSystem _combat;
    private RangeSystem _range;

    protected override void Initialize()
    {
        _pathfinder = new PathFinder();
        _combat = World.GetNode<CombatSystem>("CombatSystem");
        _range = World.GetNode<RangeSystem>("RangeSystem");
    }

    /// <summary>
    /// Move unit along a path.
    /// Triggers combat based on Hoplite rules.
    /// </summary>
    public void MoveUnit(Unit unit, List<Vector3I> path)
    {
        if (path == null || path.Count == 0) return;
        if (!unit.Health.IsAlive) return;

        var startPos = unit.Position.HexPosition;
        var endPos = path[^1];

        EmitSignal(SignalName.MovementStarted, unit, startPos, endPos);

        // Check for combat triggers
        CheckCombatTriggers(unit, startPos, endPos);

        // Animate movement
        AnimateMovement(unit, path, () =>
        {
            // Update position
            unit.Position.HexPosition = endPos;
            EmitSignal(SignalName.MovementCompleted, unit, endPos);

            // Update pathfinding
            _pathfinder.UpdateOccupiedTiles(Units.GetAllUnits());
        });
    }

    private void CheckCombatTriggers(Unit unit, Vector3I from, Vector3I to)
    {
        if (unit is Player)
        {
            // Player entering enemy range: enemy attacks player
            var threatUnit = _range.GetThreateningUnit(to);
            if (threatUnit != null && !_range.IsInRange(from, threatUnit))
            {
                _combat.ExecuteCombat(threatUnit, unit);
            }

            // Player moving within enemy range: player attacks enemy
            var adjacentEnemy = GetAdjacentEnemy(from);
            if (adjacentEnemy != null && _range.IsInRange(to, adjacentEnemy))
            {
                _combat.ExecuteCombat(unit, adjacentEnemy);
            }
        }
    }

    private Enemy GetAdjacentEnemy(Vector3I pos)
    {
        var neighbors = HexGrid.GetNeighbors(pos);
        foreach (var neighbor in neighbors)
        {
            var unit = Units.GetUnitAt(neighbor);
            if (unit is Enemy enemy)
                return enemy;
        }
        return null;
    }

    private void AnimateMovement(Unit unit, List<Vector3I> path, System.Action onComplete)
    {
        var tween = CreateTween();

        foreach (var hexPos in path)
        {
            var worldPos = HexGrid.HexToWorld(hexPos);
            tween.TweenProperty(unit, "global_position", worldPos, 0.3f);
        }

        tween.TweenCallback(Callable.From(onComplete));
    }

    public List<Vector3I> FindPath(Vector3I from, Vector3I to)
    {
        return _pathfinder.FindPath(from, to);
    }

    public List<Vector3I> GetReachableTiles(Unit unit, int range)
    {
        var pos = unit.Position.HexPosition;
        return _pathfinder.GetReachableTiles(pos, range);
    }
}
```

### Step 3.4: Create System Manager

**File**: `src/Core/SystemManager.cs`

```csharp
using Godot;
using System.Collections.Generic;
using Undergang.Nodes.Systems;

namespace Undergang.Core;

/// <summary>
/// Manages game systems and execution order.
/// </summary>
public partial class SystemManager : Node
{
    private readonly List<GameSystem> _systems = new();

    public T GetSystem<T>() where T : GameSystem
    {
        return GetNode<T>(typeof(T).Name);
    }

    public void RegisterSystem<T>() where T : GameSystem, new()
    {
        var system = new T { Name = typeof(T).Name };
        AddChild(system);
        _systems.Add(system);
    }

    public void ProcessTurnSystems()
    {
        foreach (var system in _systems)
        {
            system.ProcessTurn();
        }
    }
}
```

---

## Phase 4: Service Layer Refactor

### Step 4.1: Create GameWorld

**File**: `src/Core/GameWorld.cs`

```csharp
using Godot;

namespace Undergang.Core;

/// <summary>
/// Root node that owns all game state and systems.
/// This is the single source of truth for the game.
/// </summary>
public partial class GameWorld : Node
{
    // Managers
    public UnitManager Units { get; private set; }
    public TurnManager Turns { get; private set; }
    public SystemManager Systems { get; private set; }

    // Services
    public HexGrid Grid { get; private set; }
    public PathFinder PathFinder { get; private set; }

    public override void _Ready()
    {
        InitializeManagers();
        InitializeServices();
        InitializeSystems();
        InitializeGame();
    }

    private void InitializeManagers()
    {
        // Create managers
        Units = new UnitManager { Name = "UnitManager" };
        AddChild(Units);

        Turns = new TurnManager { Name = "TurnManager" };
        AddChild(Turns);

        Systems = new SystemManager { Name = "SystemManager" };
        AddChild(Systems);
    }

    private void InitializeServices()
    {
        Grid = new HexGrid();
        PathFinder = new PathFinder();

        UnitFactory.Initialize();
    }

    private void InitializeSystems()
    {
        // Register systems in execution order
        Systems.RegisterSystem<RangeSystem>();
        Systems.RegisterSystem<CombatSystem>();
        Systems.RegisterSystem<MovementSystem>();
        Systems.RegisterSystem<AISystem>();
        Systems.RegisterSystem<AnimationSystem>();
        Systems.RegisterSystem<UISystem>();
    }

    private void InitializeGame()
    {
        // Create initial game state
        var player = UnitFactory.CreatePlayer(new Vector3I(0, 0, 0));
        Units.AddUnit(player);

        var enemy1 = UnitFactory.CreateEnemy(UnitType.Grunt, new Vector3I(3, -3, 0));
        Units.AddUnit(enemy1);

        var enemy2 = UnitFactory.CreateEnemy(UnitType.Sniper, new Vector3I(-2, 5, -3));
        Units.AddUnit(enemy2);

        // Start first turn
        Turns.StartGame();
    }
}
```

### Step 4.2: Create Turn Manager

**File**: `src/Core/TurnManager.cs`

```csharp
using Godot;
using System.Collections.Generic;
using System.Linq;
using Undergang.Nodes.Units;

namespace Undergang.Core;

/// <summary>
/// Manages turn order and game flow.
/// </summary>
public partial class TurnManager : Node
{
    [Signal]
    public delegate void TurnStartedEventHandler(Unit unit);

    [Signal]
    public delegate void TurnEndedEventHandler(Unit unit);

    [Signal]
    public delegate void RoundCompletedEventHandler(int roundNumber);

    private Queue<Unit> _turnQueue;
    private Unit _currentUnit;
    private int _roundNumber;
    private GameWorld _world;

    public Unit CurrentUnit => _currentUnit;
    public int RoundNumber => _roundNumber;

    public override void _Ready()
    {
        _world = GetParent<GameWorld>();
        _turnQueue = new Queue<Unit>();
    }

    public void StartGame()
    {
        _roundNumber = 1;
        BuildTurnQueue();
        NextTurn();
    }

    public void EndCurrentTurn()
    {
        if (_currentUnit == null) return;

        EmitSignal(SignalName.TurnEnded, _currentUnit);
        _currentUnit = null;

        NextTurn();
    }

    private void NextTurn()
    {
        // Check win/lose conditions
        if (IsGameOver()) return;

        // Rebuild queue if empty (new round)
        if (_turnQueue.Count == 0)
        {
            _roundNumber++;
            BuildTurnQueue();
            EmitSignal(SignalName.RoundCompleted, _roundNumber);
        }

        // Get next unit
        _currentUnit = _turnQueue.Dequeue();

        // Skip if dead
        if (!_currentUnit.Health.IsAlive)
        {
            NextTurn();
            return;
        }

        EmitSignal(SignalName.TurnStarted, _currentUnit);
    }

    private void BuildTurnQueue()
    {
        _turnQueue.Clear();

        var units = _world.Units.GetAllUnits()
            .Where(u => u.Health.IsAlive)
            .OrderBy(u => u.Type); // Player goes first

        foreach (var unit in units)
        {
            _turnQueue.Enqueue(unit);
        }
    }

    private bool IsGameOver()
    {
        var player = _world.Units.GetPlayer();
        var enemies = _world.Units.GetEnemies().ToList();

        if (player == null || !player.Health.IsAlive)
        {
            GD.Print("Game Over - Player Died");
            return true;
        }

        if (!enemies.Any(e => e.Health.IsAlive))
        {
            GD.Print("Victory - All Enemies Defeated");
            return true;
        }

        return false;
    }
}
```

---

## Phase 5: Event System Migration

### Step 5.1: Replace Global Events with Signals

**Old Pattern**:
```csharp
Events.Emit(nameof(UnitMoved), unit, position);
Events.On(nameof(UnitMoved), (Entity unit, Vector3I pos) => { ... });
```

**New Pattern**:
```csharp
// In MovementSystem
EmitSignal(SignalName.MovementCompleted, unit, position);

// In other system
movementSystem.MovementCompleted += OnUnitMoved;
```

### Step 5.2: Create Event Bus (Optional)

For global events that don't belong to a specific system:

**File**: `src/Core/EventBus.cs`

```csharp
using Godot;

namespace Undergang.Core;

/// <summary>
/// Global event bus for game-wide events.
/// Use sparingly - prefer direct signals when possible.
/// </summary>
public partial class EventBus : Node
{
    private static EventBus _instance;
    public static EventBus Instance => _instance;

    [Signal]
    public delegate void GamePausedEventHandler();

    [Signal]
    public delegate void GameResumedEventHandler();

    [Signal]
    public delegate void GameOverEventHandler(bool victory);

    public override void _EnterTree()
    {
        _instance = this;
    }

    public override void _ExitTree()
    {
        _instance = null;
    }
}
```

### Step 5.3: Migration Mapping

| Old Event | New Signal Location | New Signal Name |
|-----------|-------------------|-----------------|
| `UnitMoved` | `MovementSystem` | `MovementCompleted` |
| `UnitDied` | `HealthComponent` | `Died` |
| `CombatResolved` | `CombatSystem` | `CombatEnded` |
| `TurnStarted` | `TurnManager` | `TurnStarted` |
| `TurnEnded` | `TurnManager` | `TurnEnded` |
| `PlayerAction` | `InputSystem` | `ActionRequested` |

---

## Phase 6: Final Integration

### Step 6.1: Update Main Scene

**Main.tscn** structure:
```
Main (Node)
└── GameWorld (GameWorld)
    ├── UnitManager (UnitManager)
    │   └── Units (Node) - populated at runtime
    ├── TurnManager (TurnManager)
    ├── SystemManager (SystemManager)
    │   ├── RangeSystem
    │   ├── CombatSystem
    │   ├── MovementSystem
    │   ├── AISystem
    │   └── UISystem
    ├── Board (Node3D) - visual representation
    └── Camera (Camera3D)
```

### Step 6.2: Update Input Handling

**File**: `src/Nodes/Systems/InputSystem.cs`

```csharp
using Godot;
using Undergang.Nodes.Units;

namespace Undergang.Nodes.Systems;

/// <summary>
/// Handles player input and converts to actions.
/// </summary>
public partial class InputSystem : GameSystem
{
    [Signal]
    public delegate void MoveRequestedEventHandler(Vector3I destination);

    private MovementSystem _movement;
    private Camera3D _camera;

    protected override void Initialize()
    {
        _movement = World.Systems.GetSystem<MovementSystem>();
        _camera = GetViewport().GetCamera3D();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            HandleMouseClick(mouseEvent.Position);
        }
    }

    private void HandleMouseClick(Vector2 screenPos)
    {
        // Only process during player's turn
        if (Turns.CurrentUnit is not Player player) return;

        // Raycast to find clicked hex
        var hexPos = GetClickedHex(screenPos);
        if (hexPos == null) return;

        // Try to move
        var path = _movement.FindPath(player.Position.HexPosition, hexPos.Value);
        if (path != null && path.Count > 0)
        {
            _movement.MoveUnit(player, path);

            // End turn after movement
            _movement.MovementCompleted += OnMovementComplete;
        }
    }

    private void OnMovementComplete(Unit unit, Vector3I destination)
    {
        _movement.MovementCompleted -= OnMovementComplete;
        Turns.EndCurrentTurn();
    }

    private Vector3I? GetClickedHex(Vector2 screenPos)
    {
        // Implement raycast to hex grid
        // Return hex coordinate or null
        // (Implementation depends on your board setup)
        return null;
    }
}
```

### Step 6.3: Remove Old Code

Once everything is working:

1. **Delete old files**:
   ```bash
   rm -rf src/Components
   rm -rf src/Systems  # old systems
   rm -rf src/Services/Entities.cs
   rm -rf src/Services/Events.cs
   ```

2. **Update references**:
   - Search for `using Undergang.Components`
   - Replace with `using Undergang.Nodes.Components`
   - Search for `Entities.Query`
   - Replace with `Units.Get...`

3. **Remove feature flags**:
   ```csharp
   // Delete from Config.cs
   public const bool USE_NEW_ARCHITECTURE = true;
   ```

---

## Testing Strategy

### Unit Tests

Create tests for each component:

**File**: `tests/ComponentTests.cs`

```csharp
using Godot;
using GdUnit4;
using Undergang.Nodes.Components;

[TestSuite]
public class HealthComponentTests
{
    [TestCase]
    public void TakeDamage_ReducesHealth()
    {
        var health = new HealthComponent();
        health.MaxHealth = 10;
        health.Current = 10;

        health.TakeDamage(3);

        Assert.That(health.Current).IsEqual(7);
    }

    [TestCase]
    public void TakeDamage_EmitsDiedSignal_WhenHealthReachesZero()
    {
        var health = new HealthComponent();
        health.MaxHealth = 5;
        health.Current = 5;

        var died = false;
        health.Died += () => died = true;

        health.TakeDamage(10);

        Assert.That(died).IsTrue();
        Assert.That(health.Current).IsEqual(0);
    }
}
```

### Integration Tests

Test system interactions:

```csharp
[TestSuite]
public class CombatIntegrationTests
{
    [TestCase]
    public void Combat_KillsDefender_WhenHealthReachesZero()
    {
        var world = new GameWorld();
        var attacker = UnitFactory.CreatePlayer(Vector3I.Zero);
        var defender = UnitFactory.CreateEnemy(UnitType.Grunt, new Vector3I(1, 0, -1));

        attacker.Damage.Value = 10;
        defender.Health.Current = 5;

        world.Units.AddUnit(attacker);
        world.Units.AddUnit(defender);

        var combat = world.Systems.GetSystem<CombatSystem>();
        combat.ExecuteCombat(attacker, defender);

        // Wait for combat to complete
        await ToSignal(combat, CombatSystem.SignalName.CombatEnded);

        Assert.That(defender.Health.IsAlive).IsFalse();
    }
}
```

### Manual Testing Checklist

- [ ] Player can move on hex grid
- [ ] Player attacks enemies when moving within range
- [ ] Enemies attack player when player enters their range
- [ ] Enemies move toward player on their turn
- [ ] Health updates correctly in UI
- [ ] Death animations play
- [ ] Game over triggers when player dies
- [ ] Victory triggers when all enemies die
- [ ] Turn order is correct
- [ ] Pathfinding works around obstacles

---

## Rollback Plan

### If Migration Fails

1. **Keep old code in `Legacy/` folder**
2. **Git branches**:
   ```bash
   git checkout -b migration-godot-native
   # Work in branch
   # If fails:
   git checkout main
   ```

3. **Feature flag**:
   ```csharp
   if (Config.USE_NEW_ARCHITECTURE)
       InitializeNewSystems();
   else
       InitializeLegacySystems();
   ```

### Partial Migration

You can migrate incrementally:

1. **Phase 1 only**: Use new components with old entities
2. **Phase 1-2**: Use new units but old systems
3. **Phase 1-3**: Use new units + systems, keep old events

Each phase is independently functional.

---

## Post-Migration Improvements

Once migration is complete, consider:

### 1. Command Pattern for Actions

```csharp
public interface ICommand
{
    void Execute();
    void Undo();
}

public class MoveCommand : ICommand
{
    private Unit _unit;
    private Vector3I _from;
    private Vector3I _to;

    public void Execute() { /* ... */ }
    public void Undo() { /* ... */ }
}
```

### 2. Save/Load System

```csharp
public partial class SaveManager : Node
{
    public void SaveGame(string filename)
    {
        var saveData = new SaveData
        {
            PlayerPosition = world.Units.GetPlayer().Position.HexPosition,
            Enemies = world.Units.GetEnemies().Select(SerializeEnemy).ToList()
        };

        // Serialize to JSON
    }
}
```

### 3. Ability System

```csharp
public partial class AbilityComponent : GameComponent
{
    [Export]
    public Ability[] Abilities { get; set; }
}

public abstract class Ability
{
    public abstract void Execute(Unit caster, Vector3I target);
}
```

### 4. Better UI Bindings

```csharp
public partial class UnitUI : Control
{
    private Unit _unit;

    public void BindToUnit(Unit unit)
    {
        _unit = unit;
        _unit.Health.Damaged += OnHealthChanged;
        UpdateHealthBar();
    }
}
```

---

## Conclusion

This migration transforms your architecture from a pseudo-ECS to a Godot-native node-based system. The key benefits:

1. **Better performance**: No dictionary lookups, direct node references
2. **Easier debugging**: Inspector shows live state
3. **Type safety**: Signals instead of string events
4. **Less code**: Leverage Godot's built-in features
5. **Clearer ownership**: GameWorld owns everything

Take it one phase at a time, test thoroughly, and don't delete old code until the new system is proven. Good luck!
