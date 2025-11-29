# LLM-Friendly Architecture Guide

## Why This Matters for AI Development

**Problem**: LLMs (like Claude Code) can't interact with Godot's visual editor
**Solution**: 100% code-based architecture where AI can read, write, and modify everything

---

## Core Principles for LLM-Friendly Code

### 1. Everything is Code
- No `.tscn` files to edit manually
- No inspector properties to set
- No visual scene tree to build
- Pure C# that AI can generate

### 2. Self-Documenting Structure
- Clear file organization
- Predictable naming conventions
- Type-safe APIs
- Zero ambiguity

### 3. Composable Components
- Small, focused classes
- Easy to generate independently
- Clear dependencies
- Simple to test

---

## File Structure (LLM-Optimized)

```
src/
├── Main.cs                      # Entry point - AI starts here
├── Core/
│   ├── GameWorld.cs            # Root container - builds everything
│   ├── UnitManager.cs          # Query API for units
│   ├── TurnManager.cs          # Turn logic
│   └── UnitFactory.cs          # Unit creation
├── Components/
│   ├── IComponent.cs           # Marker interface
│   ├── HealthComponent.cs      # One file = one component
│   ├── PositionComponent.cs    # Easy for AI to generate
│   ├── DamageComponent.cs      # Easy for AI to modify
│   └── ...
├── Units/
│   ├── Unit.cs                 # Base class
│   ├── Player.cs               # Player unit
│   └── Enemy.cs                # Enemy unit
└── Systems/
    ├── ISystem.cs              # System interface
    ├── CombatSystem.cs         # One file = one system
    ├── MovementSystem.cs       # Easy for AI to understand
    └── ...
```

**Why This Works for AI**:
- One concept = one file
- AI can generate entire files from scratch
- AI can read one file and understand its purpose
- AI can modify one file without breaking others

---

## Template System (AI Can Fill In)

### Component Template

Every component follows this exact pattern:

**File**: `src/Components/[Name]Component.cs`

```csharp
using Godot;

namespace Undergang.Components;

/// <summary>
/// [DESCRIPTION]
/// AI: Fill in what this component does
/// </summary>
public partial class [Name]Component : Node, IComponent
{
    // Signals (if needed)
    [Signal]
    public delegate void [Event]EventHandler([params]);

    // Properties (exported if tweakable)
    [Export]
    public [Type] [Property] { get; set; } = [default];

    // Internal state
    private [Type] _[field];

    // Lifecycle
    public override void _Ready()
    {
        // Initialize
    }

    // Public API
    public void [Method]([params])
    {
        // Logic
        EmitSignal(SignalName.[Event], [args]);
    }

    // Cleanup
    public override void _ExitTree()
    {
        // Unsubscribe, cleanup
    }
}
```

**AI Prompt Example**:
```
Create a StaminaComponent with:
- MaxStamina property (default 100)
- Current stamina that regenerates 10 per turn
- StaminaDepleted signal when stamina reaches 0
- UseStamina(int amount) method
```

**AI Can Generate**:
```csharp
using Godot;

namespace Undergang.Components;

/// <summary>
/// Manages unit stamina for abilities and actions.
/// Regenerates over time.
/// </summary>
public partial class StaminaComponent : Node, IComponent
{
    [Signal]
    public delegate void StaminaDepletedEventHandler();

    [Signal]
    public delegate void StaminaChangedEventHandler(int current, int max);

    [Export]
    public int MaxStamina { get; set; } = 100;

    [Export]
    public int RegenPerTurn { get; set; } = 10;

    private int _current;

    public int Current
    {
        get => _current;
        private set
        {
            _current = Mathf.Clamp(value, 0, MaxStamina);
            EmitSignal(SignalName.StaminaChanged, _current, MaxStamina);

            if (_current == 0)
                EmitSignal(SignalName.StaminaDepleted);
        }
    }

    public override void _Ready()
    {
        _current = MaxStamina;
    }

    public bool UseStamina(int amount)
    {
        if (Current < amount) return false;

        Current -= amount;
        return true;
    }

    public void RegenerateTurn()
    {
        Current += RegenPerTurn;
    }
}
```

### System Template

**File**: `src/Systems/[Name]System.cs`

```csharp
using Godot;
using Undergang.Core;
using Undergang.Units;
using Undergang.Components;

namespace Undergang.Systems;

/// <summary>
/// [DESCRIPTION]
/// AI: Fill in what this system does
/// </summary>
public partial class [Name]System : Node, ISystem
{
    // Signals
    [Signal]
    public delegate void [Event]EventHandler([params]);

    // Dependencies (injected via GameWorld)
    private GameWorld _world;
    private UnitManager _units;

    // Other systems (if needed)
    private [Other]System _otherSystem;

    public override void _Ready()
    {
        _world = GetParent<GameWorld>();
        _units = _world.Units;
        _otherSystem = _world.GetSystem<[Other]System>();

        Initialize();
    }

    public void Initialize()
    {
        // Subscribe to events
        _world.Turns.TurnStarted += OnTurnStarted;
    }

    public void ProcessTurn()
    {
        // Per-turn logic
    }

    private void OnTurnStarted(Unit unit)
    {
        // React to turn changes
    }

    public override void _ExitTree()
    {
        Cleanup();
    }

    public void Cleanup()
    {
        _world.Turns.TurnStarted -= OnTurnStarted;
    }
}
```

### Unit Template

**File**: `src/Units/[Name].cs`

```csharp
using Godot;
using Undergang.Components;

namespace Undergang.Units;

/// <summary>
/// [DESCRIPTION]
/// AI: Fill in what this unit type does
/// </summary>
public partial class [Name] : Unit
{
    // Type-specific components
    public [Component] [Name] { get; private set; }

    public override void _Ready()
    {
        Type = UnitType.[Name];
        base._Ready(); // Builds base components

        // Add type-specific components
        [Name] = new [Component]();
        AddChild([Name]);

        Configure();
    }

    private void Configure()
    {
        // Set initial values
        Health.MaxHealth = [value];
        Damage.Value = [value];
        // etc.
    }

    protected override void OnDied()
    {
        // Custom death behavior
        base.OnDied();
    }
}
```

---

## AI-Friendly Patterns

### Pattern 1: Builder Pattern for Complex Setup

```csharp
public class UnitBuilder
{
    private Unit _unit;

    public static UnitBuilder Create<T>() where T : Unit, new()
    {
        return new UnitBuilder { _unit = new T() };
    }

    public UnitBuilder WithHealth(int max)
    {
        _unit.Ready += () => {
            _unit.Health.MaxHealth = max;
            _unit.Health.Current = max;
        };
        return this;
    }

    public UnitBuilder WithDamage(int value)
    {
        _unit.Ready += () => _unit.Damage.Value = value;
        return this;
    }

    public UnitBuilder At(Vector3I pos)
    {
        _unit.Ready += () => _unit.Position.HexPosition = pos;
        return this;
    }

    public Unit Build() => _unit;
}

// AI can generate readable unit creation
var boss = UnitBuilder.Create<Enemy>()
    .WithHealth(50)
    .WithDamage(10)
    .At(new Vector3I(0, 5, -5))
    .Build();
```

**AI Prompt**:
```
Create a boss enemy with 50 health, 10 damage, at position (0,5,-5)
```

### Pattern 2: Declarative Component Registration

```csharp
public partial class Unit : CharacterBody3D
{
    // AI can easily add to this list
    protected virtual ComponentDefinition[] GetComponents() => new[]
    {
        new ComponentDefinition<HealthComponent>(),
        new ComponentDefinition<PositionComponent>(),
        new ComponentDefinition<DamageComponent>(),
    };

    public override void _Ready()
    {
        foreach (var def in GetComponents())
        {
            var component = def.Create();
            AddChild(component);
        }
    }
}

public partial class Boss : Unit
{
    protected override ComponentDefinition[] GetComponents() =>
        base.GetComponents().Concat(new[]
        {
            new ComponentDefinition<ShieldComponent>(),
            new ComponentDefinition<EnrageComponent>(),
        }).ToArray();
}
```

**AI Prompt**:
```
Add Shield and Enrage components to Boss
```

### Pattern 3: Data-Driven Configuration

```csharp
// AI can generate JSON
// File: res://data/units/boss.json
{
  "type": "Boss",
  "health": 50,
  "damage": 10,
  "components": [
    { "type": "Shield", "value": 20 },
    { "type": "Enrage", "threshold": 0.3 }
  ]
}

// Code loads it
public static class UnitLoader
{
    public static Unit LoadFromJson(string path)
    {
        var json = FileAccess.GetFileAsString(path);
        var data = Json.ParseString(json).AsGodotDictionary();

        // Build unit from data
        var unit = CreateUnit(data["type"].AsString());
        // ... configure from data
        return unit;
    }
}
```

**AI Prompt**:
```
Create a boss unit with 50 health, 10 damage, 20 shield, enrages at 30% health
```

---

## AI Prompt Patterns (For You to Use)

### Adding a Component

**Prompt**:
```
Create a [Name]Component with:
- [Property1]: [Type] (default: [value])
- [Property2]: [Type] (default: [value])
- [Signal1]: fires when [condition]
- [Method1]([params]): [description]

Follow the component template in src/Components/
```

### Adding a System

**Prompt**:
```
Create a [Name]System that:
- Runs on [turn/event]
- Processes units with [Component]
- Emits [Signal] when [condition]
- Interacts with [OtherSystem]

Follow the system template in src/Systems/
```

### Adding a Unit Type

**Prompt**:
```
Create a [Name] unit (extends Unit) with:
- [Component1] configured to [value]
- [Component2] configured to [value]
- Custom behavior: [description]

Follow the unit template in src/Units/
```

### Modifying Existing Code

**Prompt**:
```
In [File]:
- Add [Feature]
- Modify [Method] to [NewBehavior]
- Emit [Signal] when [Condition]
```

---

## Queryable API (AI Can Reason About)

### Clear, Predictable Method Names

```csharp
// AI can guess these exist and use them
public class UnitManager
{
    public IEnumerable<Unit> GetAllUnits();
    public Player GetPlayer();
    public IEnumerable<Enemy> GetEnemies();
    public Unit GetUnitAt(Vector3I pos);
    public IEnumerable<Unit> GetUnitsInRange(Vector3I center, int range);
    public IEnumerable<Unit> GetUnitsWithComponent<T>() where T : IComponent;
    public IEnumerable<T> GetComponents<T>() where T : IComponent;
}
```

**AI Can Generate**:
```csharp
// Without being told the exact API, AI can infer:
var enemies = world.Units.GetEnemies();
var player = world.Units.GetPlayer();
var nearbyUnits = world.Units.GetUnitsInRange(player.Position.HexPosition, 3);
```

### Fluent Query API

```csharp
public static class UnitQueries
{
    public static IEnumerable<Unit> Alive(this IEnumerable<Unit> units)
        => units.Where(u => u.Health.IsAlive);

    public static IEnumerable<Unit> InRange(this IEnumerable<Unit> units, Vector3I center, int range)
        => units.Where(u => HexGrid.Distance(u.Position.HexPosition, center) <= range);

    public static IEnumerable<Unit> WithComponent<T>(this IEnumerable<Unit> units) where T : IComponent
        => units.Where(u => u.GetNodeOrNull<T>(typeof(T).Name) != null);
}

// AI can chain these intuitively
var targets = world.Units
    .GetEnemies()
    .Alive()
    .InRange(player.Position.HexPosition, 5)
    .WithComponent<ShieldComponent>();
```

---

## Self-Registering Systems (AI Just Creates, Doesn't Wire)

```csharp
// AI creates a system file
// System auto-registers via attribute

[AutoRegister]
public partial class NewSystem : Node, ISystem
{
    // System code
}

// GameWorld discovers and registers automatically
public partial class GameWorld : Node
{
    public override void _Ready()
    {
        AutoRegisterSystems();
    }

    private void AutoRegisterSystems()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var systemTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<AutoRegisterAttribute>() != null);

        foreach (var type in systemTypes)
        {
            var system = (Node)Activator.CreateInstance(type);
            system.Name = type.Name;
            GetNode("Systems").AddChild(system);
        }
    }
}
```

**AI Workflow**:
1. AI creates `NewSystem.cs`
2. Adds `[AutoRegister]` attribute
3. Done - no manual wiring needed

---

## Convention Over Configuration

### Naming Conventions AI Can Follow

```csharp
// Components: [Name]Component
HealthComponent
PositionComponent
DamageComponent

// Systems: [Name]System
CombatSystem
MovementSystem
RangeSystem

// Units: [Type]
Player
Enemy
Boss

// Events: [Action][Subject]
UnitMoved
HealthChanged
TurnStarted

// Methods: [Verb][Noun]
GetPlayer()
MoveUnit()
ApplyDamage()
```

**AI Knows**:
- If it ends in "Component", it's a component
- If it ends in "System", it's a system
- If it's a verb + noun, it's a method
- If it's past tense, it's an event

---

## Type-Safe Event System (AI Can Discover)

```csharp
// Instead of string-based events
Events.Emit("UnitMoved", unit, position); // AI has to guess parameters

// Use type-safe signals
public partial class MovementSystem : Node
{
    [Signal]
    public delegate void UnitMovedEventHandler(Unit unit, Vector3I position);

    public void MoveUnit(Unit unit, Vector3I to)
    {
        EmitSignal(SignalName.UnitMoved, unit, to);
    }
}

// AI can see the signature and use it correctly
movementSystem.UnitMoved += (unit, position) => {
    GD.Print($"{unit.Type} moved to {position}");
};
```

---

## Documentation AI Can Parse

### XML Comments (AI Reads These)

```csharp
/// <summary>
/// Manages combat resolution between units.
/// </summary>
/// <remarks>
/// Combat is triggered by movement into threat zones.
/// Follows Hoplite-style mechanics:
/// - Player entering enemy range: enemy attacks
/// - Player moving within enemy range: player attacks
/// </remarks>
public partial class CombatSystem : Node, ISystem
{
    /// <summary>
    /// Execute combat between attacker and defender.
    /// </summary>
    /// <param name="attacker">The attacking unit</param>
    /// <param name="defender">The defending unit</param>
    /// <remarks>
    /// Emits CombatStarted and CombatEnded signals.
    /// Applies damage after attack animation completes.
    /// </remarks>
    public void ExecuteCombat(Unit attacker, Unit defender)
    {
        // AI knows exactly what this does
    }
}
```

### Inline Comments for Complex Logic

```csharp
public void ProcessMovement(Unit unit, Vector3I destination)
{
    // AI INSTRUCTION: Check if destination is threatened
    var threat = _rangeSystem.GetThreateningUnit(destination);

    // AI INSTRUCTION: If entering new threat zone, enemy attacks first
    if (threat != null && !IsAlreadyThreatened(unit.Position.HexPosition))
    {
        _combatSystem.ExecuteCombat(threat, unit);
    }

    // AI INSTRUCTION: Move unit to destination
    AnimateMovement(unit, destination);
}
```

---

## Example: Full AI-Generated Feature

### Prompt to AI:
```
Add a shield system:

1. Create ShieldComponent with:
   - MaxShield (default 10)
   - Current shield
   - Regenerates 1 per turn
   - ShieldBroken signal when shield reaches 0

2. Modify CombatSystem to:
   - Apply damage to shield first
   - Only damage health when shield is depleted

3. Add ShieldRegenSystem that:
   - Regenerates all shields by 1 each turn
   - Runs after turn end

4. Add shield to Boss unit:
   - 20 max shield
   - 2 regen per turn
```

### AI Generates:

**File**: `src/Components/ShieldComponent.cs`
```csharp
using Godot;

namespace Undergang.Components;

/// <summary>
/// Shield that absorbs damage before health is affected.
/// Regenerates over time.
/// </summary>
public partial class ShieldComponent : Node, IComponent
{
    [Signal]
    public delegate void ShieldBrokenEventHandler();

    [Signal]
    public delegate void ShieldChangedEventHandler(int current, int max);

    [Export]
    public int MaxShield { get; set; } = 10;

    [Export]
    public int RegenPerTurn { get; set; } = 1;

    private int _current;

    public int Current
    {
        get => _current;
        set
        {
            var old = _current;
            _current = Mathf.Clamp(value, 0, MaxShield);
            EmitSignal(SignalName.ShieldChanged, _current, MaxShield);

            if (_current == 0 && old > 0)
                EmitSignal(SignalName.ShieldBroken);
        }
    }

    public bool IsActive => Current > 0;

    public override void _Ready()
    {
        _current = MaxShield;
    }

    public int AbsorbDamage(int damage)
    {
        var absorbed = Mathf.Min(damage, Current);
        Current -= absorbed;
        return damage - absorbed; // Return overflow damage
    }

    public void Regenerate()
    {
        Current += RegenPerTurn;
    }
}
```

**Modify**: `src/Systems/CombatSystem.cs`
```csharp
public void ExecuteCombat(Unit attacker, Unit defender)
{
    var damage = attacker.Damage.Value;

    // Check for shield
    var shield = defender.GetNodeOrNull<ShieldComponent>("Shield");
    if (shield != null && shield.IsActive)
    {
        damage = shield.AbsorbDamage(damage);
        GD.Print($"Shield absorbed damage, {damage} overflow");
    }

    // Apply remaining damage to health
    if (damage > 0)
    {
        defender.Health.TakeDamage(damage);
    }
}
```

**File**: `src/Systems/ShieldRegenSystem.cs`
```csharp
using Godot;
using Undergang.Core;
using Undergang.Components;

namespace Undergang.Systems;

[AutoRegister]
public partial class ShieldRegenSystem : Node, ISystem
{
    private GameWorld _world;

    public override void _Ready()
    {
        _world = GetParent<GameWorld>();
        _world.Turns.TurnEnded += OnTurnEnded;
    }

    private void OnTurnEnded(Unit unit)
    {
        // Regenerate shields for all units
        var units = _world.Units.GetAllUnits();

        foreach (var u in units)
        {
            var shield = u.GetNodeOrNull<ShieldComponent>("Shield");
            if (shield != null && u.Health.IsAlive)
            {
                shield.Regenerate();
            }
        }
    }

    public void Cleanup()
    {
        _world.Turns.TurnEnded -= OnTurnEnded;
    }
}
```

**Modify**: `src/Units/Boss.cs`
```csharp
public partial class Boss : Enemy
{
    public ShieldComponent Shield { get; private set; }

    public override void _Ready()
    {
        Type = UnitType.Boss;
        base._Ready();

        // Add shield component
        Shield = new ShieldComponent
        {
            Name = "Shield",
            MaxShield = 20,
            RegenPerTurn = 2
        };
        AddChild(Shield);

        Configure();
    }

    private void Configure()
    {
        Health.MaxHealth = 50;
        Health.Current = 50;
        Damage.Value = 10;
        Shield.Current = Shield.MaxShield;
    }
}
```

**AI did everything**:
- Created new component
- Modified existing system
- Created new system
- Modified unit type
- All type-safe, compiles, works

---

## AI Development Workflow

### Step 1: AI Reads Structure
```
Read src/ directory structure
Understand conventions
Identify templates
```

### Step 2: AI Generates Code
```
Follow template
Use conventions
Add documentation
Register automatically
```

### Step 3: AI Tests
```csharp
// AI can generate test
[Test]
public void ShieldAbsorbsDamage()
{
    var unit = new Player();
    unit._Ready();

    var shield = new ShieldComponent { MaxShield = 10 };
    unit.AddChild(shield);
    shield._Ready();

    var overflow = shield.AbsorbDamage(15);

    Assert.That(shield.Current).IsEqual(0);
    Assert.That(overflow).IsEqual(5);
}
```

### Step 4: You Run
```bash
dotnet build
# Press F5 in Godot
```

---

## Benefits for LLM Development

### 1. AI Can Explore Codebase
```
AI: "Show me all components"
You: Just read src/Components/*.cs
```

### 2. AI Can Modify Independently
```
AI: "Modify CombatSystem to add shields"
- Reads CombatSystem.cs
- Adds shield logic
- Done
```

### 3. AI Can Generate New Features
```
You: "Add poison damage over time"
AI:
  1. Creates PoisonComponent
  2. Creates PoisonSystem
  3. Modifies CombatSystem
  4. Done
```

### 4. AI Can Refactor
```
You: "Move all damage calculation to DamageCalculator"
AI:
  1. Creates DamageCalculator.cs
  2. Modifies CombatSystem to use it
  3. Updates tests
  4. Done
```

### 5. AI Can Debug
```
You: "Why doesn't shield regenerate?"
AI:
  1. Reads ShieldRegenSystem
  2. Checks if registered
  3. Checks if event connected
  4. Finds bug, fixes it
```

---

## Anti-Patterns (Things That Break AI)

### ❌ DON'T: Editor-Dependent Code
```csharp
// AI can't help with this
// "Manually set in inspector"
[Export] public int Health;
```

### ✅ DO: Code-Configured
```csharp
[Export]
public int Health { get; set; } = 10; // AI can see default
```

---

### ❌ DON'T: String-Based APIs
```csharp
// AI has to guess
Events.Emit("UnitMoved", unit, pos);
```

### ✅ DO: Type-Safe APIs
```csharp
// AI can autocomplete
EmitSignal(SignalName.UnitMoved, unit, pos);
```

---

### ❌ DON'T: Hidden Dependencies
```csharp
// Where does this come from?
var player = GlobalState.Player;
```

### ✅ DO: Explicit Dependencies
```csharp
// Clear dependency
private UnitManager _units;
public override void _Ready() {
    _units = GetParent<GameWorld>().Units;
}
```

---

### ❌ DON'T: Magic Numbers
```csharp
if (distance < 5) // AI doesn't know what 5 means
```

### ✅ DO: Named Constants
```csharp
public const int ATTACK_RANGE = 5;
if (distance < ATTACK_RANGE) // AI understands
```

---

## Summary: The Perfect AI Development Stack

```
┌─────────────────────────────────────┐
│  You: Give AI natural language      │
│  "Add shields to bosses"            │
└─────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────┐
│  AI: Reads conventions & templates  │
│  Generates type-safe C# code        │
└─────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────┐
│  Code: Self-documenting structure   │
│  Auto-registers, auto-wires         │
└─────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────┐
│  Godot: Builds & runs               │
│  No manual editor work needed       │
└─────────────────────────────────────┘
```

**Result**: AI becomes your pair programmer that actually understands and modifies your codebase!
