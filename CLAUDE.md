# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Project Overview

Undergang is a turn-based tactical game built with Godot 4.5 and C#. The game features hex-based grid movement, node-based component architecture, and tactical combat between players and enemies.

**CRITICAL: This codebase is designed for 100% code-first development. You (Claude) can create, modify, and refactor everything without needing the Godot editor.**

---

## Architecture Philosophy

### Core Principle: Everything is Code

This project uses a **Godot-native, LLM-friendly architecture** where:
- Components are Nodes (not data structs)
- Signals replace string-based events
- Units are CharacterBody3D with component children
- Systems are Nodes that process logic
- Everything auto-wires and self-registers

### Why This Architecture?

1. **LLM-Friendly**: You can read, write, and modify all code
2. **No Editor Required**: Everything builds programmatically
3. **Type-Safe**: Compile-time checks, no runtime strings
4. **Inspector-Visible**: State visible in Godot inspector for debugging
5. **Git-Friendly**: No binary .tscn files with merge conflicts

---

## File Structure

```
src/
├── Main.cs                     # Entry point
├── Core/
│   ├── GameWorld.cs           # Root node, owns all state
│   ├── UnitManager.cs         # Unit queries and management
│   ├── TurnManager.cs         # Turn order and game flow
│   └── UnitFactory.cs         # Unit creation
├── Components/
│   ├── IComponent.cs          # Marker interface
│   ├── HealthComponent.cs     # One file per component
│   ├── PositionComponent.cs
│   ├── DamageComponent.cs
│   └── ...
├── Units/
│   ├── Unit.cs                # Base unit class
│   ├── Player.cs              # Player unit
│   └── Enemy.cs               # Enemy unit
├── Systems/
│   ├── ISystem.cs             # System interface
│   ├── CombatSystem.cs        # One file per system
│   ├── MovementSystem.cs
│   ├── RangeSystem.cs
│   ├── AISystem.cs
│   └── ...
└── Services/
    ├── HexGrid.cs             # Hex coordinate math
    ├── PathFinder.cs          # A* pathfinding
    └── ...
```

---

## Code Conventions (CRITICAL - Always Follow)

### Naming Conventions

| Type | Pattern | Example |
|------|---------|---------|
| Components | `[Name]Component` | `HealthComponent` |
| Systems | `[Name]System` | `CombatSystem` |
| Units | `[Type]` | `Player`, `Boss` |
| Signals | `[Action][Subject]` | `UnitMoved`, `HealthChanged` |
| Methods | `[Verb][Noun]` | `GetPlayer()`, `MoveUnit()` |
| Events | Past tense | `Died`, `Damaged`, `TurnEnded` |

### File Naming

- One concept per file
- File name matches class name
- Place in correct directory (Components/, Systems/, Units/)

### Code Structure

**Components** must follow this pattern:
```csharp
using Godot;

namespace Undergang.Components;

/// <summary>
/// [What this component does]
/// </summary>
public partial class [Name]Component : Node, IComponent
{
    // Signals (if needed)
    [Signal]
    public delegate void [Event]EventHandler([params]);

    // Properties
    [Export]  // Use [Export] for inspector-visible values
    public [Type] [Property] { get; set; } = [default];

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
        // Unsubscribe signals
    }
}
```

**Systems** must follow this pattern:
```csharp
using Godot;
using Undergang.Core;

namespace Undergang.Systems;

/// <summary>
/// [What this system does]
/// </summary>
[AutoRegister]  // IMPORTANT: Use this for auto-registration
public partial class [Name]System : Node, ISystem
{
    // Signals
    [Signal]
    public delegate void [Event]EventHandler([params]);

    // Dependencies
    private GameWorld _world;
    private UnitManager _units;
    private TurnManager _turns;

    public override void _Ready()
    {
        _world = GetParent<GameWorld>();
        _units = _world.Units;
        _turns = _world.Turns;

        Initialize();
    }

    public void Initialize()
    {
        // Subscribe to events
    }

    public void ProcessTurn()
    {
        // Per-turn logic (if needed)
    }

    public void Cleanup()
    {
        // Unsubscribe events
    }

    public override void _ExitTree()
    {
        Cleanup();
    }
}
```

**Units** must follow this pattern:
```csharp
using Godot;
using Undergang.Components;

namespace Undergang.Units;

/// <summary>
/// [What this unit type does]
/// </summary>
public partial class [Name] : Unit
{
    // Type-specific components (cached)
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
        // Set stats
        Health.MaxHealth = [value];
        Damage.Value = [value];
    }

    protected override void OnDied()
    {
        // Custom death behavior
        base.OnDied();
    }
}
```

---

## How to Work with This Codebase

### When Asked to Add a Component

1. **Create file**: `src/Components/[Name]Component.cs`
2. **Follow component template** (see above)
3. **Add XML documentation**
4. **Use [Export] for tweakable values**
5. **Define signals for important events**
6. **Emit signals when state changes**

### When Asked to Add a System

1. **Create file**: `src/Systems/[Name]System.cs`
2. **Follow system template** (see above)
3. **Add `[AutoRegister]` attribute** (it will auto-wire)
4. **Get dependencies from GameWorld** in `_Ready()`
5. **Subscribe to events in `Initialize()`**
6. **Unsubscribe in `Cleanup()`**

### When Asked to Add a Unit Type

1. **Create file**: `src/Units/[Name].cs`
2. **Extend Unit, Player, or Enemy**
3. **Add type-specific components in `_Ready()`**
4. **Configure stats in `Configure()`**
5. **Override methods for custom behavior**

### When Asked to Modify Existing Code

1. **Read the file first** (use Read tool)
2. **Understand existing patterns**
3. **Follow the same style**
4. **Update XML documentation**
5. **Maintain signal connections/disconnections**

---

## Important Patterns

### Signal Usage (CRITICAL)

**Define signals:**
```csharp
[Signal]
public delegate void DiedEventHandler();

[Signal]
public delegate void DamagedEventHandler(int amount, int remaining);
```

**Emit signals:**
```csharp
EmitSignal(SignalName.Died);
EmitSignal(SignalName.Damaged, amount, remaining);
```

**Subscribe to signals (C# style - preferred):**
```csharp
health.Died += OnDied;
health.Damaged += (amount, remaining) => GD.Print($"Took {amount} damage");
```

**Unsubscribe (CRITICAL - always do this in _ExitTree):**
```csharp
public override void _ExitTree()
{
    health.Died -= OnDied;
}
```

### Component Queries

```csharp
// Get single component
var health = unit.GetComponent<HealthComponent>();

// Check if has component
if (unit.HasComponent<ShieldComponent>())

// Try get component
if (unit.TryGetComponent<DamageComponent>(out var damage))
```

### Unit Queries

```csharp
// From UnitManager
var player = _units.GetPlayer();
var enemies = _units.GetEnemies();
var allUnits = _units.GetAllUnits();
var unitAt = _units.GetUnitAt(hexPos);
var inRange = _units.GetUnitsInRange(center, range);
```

### Auto-Registration

Systems with `[AutoRegister]` attribute automatically register themselves:

```csharp
[AutoRegister]
public partial class NewSystem : Node, ISystem
{
    // System auto-wires itself on game start
}
```

---

## Combat System (Hoplite-Style)

The game implements **Hoplite-style tactical combat** where positioning is critical.

### Attack Triggers

1. **Enemy Reactive Attacks**: Enemies attack when player enters their threat range
   - Only triggers when entering a NEW enemy's range
   - Enemy doesn't move, just attacks

2. **Player Attacks**: Player attacks when moving WITHIN an enemy's range
   - Must be adjacent to enemy before moving
   - Moving to another adjacent tile triggers attack

3. **Enemy Turn**: Enemies never attack on their own turn
   - If player in range: Enemy waits
   - If player NOT in range: Enemy moves toward player

### Key Files

- `src/Systems/CombatSystem.cs` - Combat resolution
- `src/Systems/MovementSystem.cs` - Combat triggers during movement
- `src/Systems/RangeSystem.cs` - Threat zone calculation
- `src/Systems/AISystem.cs` - Enemy AI behavior

---

## Reference Documentation

When you need more details, check these files:

- **MIGRATION_GUIDE.md** - Architecture explanation and migration steps
- **CODE_FIRST_MIGRATION.md** - How to do everything in code (no editor)
- **LLM_FRIENDLY_ARCHITECTURE.md** - Architecture patterns optimized for AI
- **AI_PROMPT_GUIDE.md** - Templates and examples for common tasks

---

## Common Tasks

### Adding a New Component

See **AI_PROMPT_GUIDE.md** → Component Prompts

**Example**: Adding StaminaComponent
```csharp
using Godot;

namespace Undergang.Components;

public partial class StaminaComponent : Node, IComponent
{
    [Signal]
    public delegate void StaminaDepletedEventHandler();

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

    public void Regenerate()
    {
        Current += RegenPerTurn;
    }
}
```

### Adding a New System

See **AI_PROMPT_GUIDE.md** → System Prompts

**Example**: Adding PoisonSystem
```csharp
using Godot;
using Undergang.Core;
using Undergang.Components;

namespace Undergang.Systems;

[AutoRegister]
public partial class PoisonSystem : Node, ISystem
{
    [Signal]
    public delegate void PoisonAppliedEventHandler(Unit unit, int damage);

    private GameWorld _world;
    private UnitManager _units;

    public override void _Ready()
    {
        _world = GetParent<GameWorld>();
        _units = _world.Units;

        Initialize();
    }

    public void Initialize()
    {
        _world.Turns.TurnEnded += OnTurnEnded;
    }

    private void OnTurnEnded(Unit unit)
    {
        // Apply poison to all units with PoisonComponent
        foreach (var u in _units.GetAllUnits())
        {
            if (u.TryGetComponent<PoisonComponent>(out var poison) && u.Health.IsAlive)
            {
                u.Health.TakeDamage(poison.DamagePerTurn);
                EmitSignal(SignalName.PoisonApplied, u, poison.DamagePerTurn);

                poison.DecrementDuration();
            }
        }
    }

    public void Cleanup()
    {
        _world.Turns.TurnEnded -= OnTurnEnded;
    }

    public override void _ExitTree()
    {
        Cleanup();
    }
}
```

### Modifying Existing Systems

1. **Read the file first**
2. **Find the method to modify**
3. **Understand the existing logic**
4. **Add your changes following the same patterns**
5. **Update documentation**

**Example**: Adding shield check to CombatSystem
```csharp
public void ExecuteCombat(Unit attacker, Unit defender)
{
    var damage = attacker.Damage.Value;

    // NEW: Check for shield
    if (defender.TryGetComponent<ShieldComponent>(out var shield) && shield.IsActive)
    {
        damage = shield.AbsorbDamage(damage);
    }

    // Apply remaining damage
    if (damage > 0)
    {
        defender.Health.TakeDamage(damage);
    }
}
```

---

## Testing

### Manual Testing Checklist

When you add a feature, verify:
- [ ] Code compiles (dotnet build)
- [ ] No runtime errors in console
- [ ] Feature works as expected
- [ ] Signals fire correctly
- [ ] No memory leaks (signals unsubscribed)
- [ ] Inspector shows correct values

### Adding Debug Output

```csharp
public void SomeMethod()
{
    GD.Print($"[{GetType().Name}] Doing something with {variable}");
}
```

---

## Build Commands

```bash
# Build project
dotnet build

# Run in Godot
# Open project in Godot 4.5, press F5
```

---

## Key Godot Concepts

### Node Lifecycle

```csharp
_EnterTree()   // Added to scene tree
_Ready()       // All children ready
_Process()     // Every frame
_ExitTree()    // Removed from scene tree
```

**CRITICAL**: Always unsubscribe signals in `_ExitTree()` to prevent memory leaks!

### Deferred Calls

When you need to call something after current frame:
```csharp
CallDeferred(nameof(MethodName));
```

### Tweens (Animation)

```csharp
var tween = CreateTween();
tween.TweenProperty(node, "position", targetPos, duration);
tween.TweenCallback(Callable.From(OnComplete));
```

---

## Common Pitfalls

### ❌ DON'T: Forget to unsubscribe signals
```csharp
public override void _Ready()
{
    someComponent.SomeEvent += Handler;
    // MISSING: Unsubscribe in _ExitTree()
}
// Result: Memory leak!
```

### ✅ DO: Always unsubscribe
```csharp
public override void _Ready()
{
    someComponent.SomeEvent += Handler;
}

public override void _ExitTree()
{
    someComponent.SomeEvent -= Handler;
}
```

---

### ❌ DON'T: Access nodes before _Ready()
```csharp
public partial class MyClass : Node
{
    private SomeNode _node = GetNode<SomeNode>("SomePath"); // NULL!
}
```

### ✅ DO: Access in _Ready() or later
```csharp
public partial class MyClass : Node
{
    private SomeNode _node;

    public override void _Ready()
    {
        _node = GetNode<SomeNode>("SomePath"); // Works!
    }
}
```

---

### ❌ DON'T: Use string-based events
```csharp
Events.Emit("UnitMoved", unit); // Type-unsafe, error-prone
```

### ✅ DO: Use signals
```csharp
EmitSignal(SignalName.UnitMoved, unit); // Type-safe!
```

---

### ❌ DON'T: Hardcode values
```csharp
if (distance < 5) // What is 5?
```

### ✅ DO: Use named constants
```csharp
public const int ATTACK_RANGE = 5;
if (distance < ATTACK_RANGE) // Clear intent
```

---

## When You Get Stuck

1. **Read existing code** - Find similar feature and copy pattern
2. **Check reference docs** - MIGRATION_GUIDE.md, CODE_FIRST_MIGRATION.md
3. **Add debug output** - GD.Print() everywhere
4. **Ask for clarification** - If requirements unclear, ask user

---

## Your Workflow (Claude)

### When User Asks You To Create Something:

1. ✅ **Check if you need to read files first**
   - Use Read tool on relevant files
   - Understand existing patterns

2. ✅ **Follow the conventions**
   - Use correct naming (ComponentName + "Component")
   - Place in correct directory
   - Follow the templates above

3. ✅ **Write complete, working code**
   - Include all necessary using statements
   - Add XML documentation
   - Handle edge cases
   - Subscribe AND unsubscribe signals

4. ✅ **Use Write or Edit tool**
   - Create new files with Write
   - Modify existing files with Edit

5. ✅ **Explain what you did**
   - Tell user what files you created/modified
   - Explain how it works
   - Note any caveats or next steps

### When User Asks You To Debug:

1. ✅ **Read the relevant files**
2. ✅ **Identify the issue**
3. ✅ **Add debug output if needed**
4. ✅ **Fix the issue**
5. ✅ **Explain what was wrong and how you fixed it**

### When User Asks You To Refactor:

1. ✅ **Read all affected files**
2. ✅ **Plan the refactoring**
3. ✅ **Make changes incrementally**
4. ✅ **Ensure nothing breaks**
5. ✅ **Update documentation**

---

## Quick Reference

### Component Lifecycle
```csharp
_Ready()       → Initialize, subscribe signals
_Process()     → Per-frame logic (avoid if possible)
_ExitTree()    → Cleanup, unsubscribe signals
```

### Signal Pattern
```csharp
[Signal] delegate void EventHandler(params);  // Define
EmitSignal(SignalName.Event, args);           // Emit
obj.Event += Handler;                          // Subscribe
obj.Event -= Handler;                          // Unsubscribe
```

### System Dependencies
```csharp
_world = GetParent<GameWorld>();
_units = _world.Units;
_turns = _world.Turns;
_otherSystem = _world.GetSystem<OtherSystem>();
```

### Unit Creation
```csharp
var unit = new UnitType();
_units.AddUnit(unit); // Adds to scene, calls _Ready()
```

---

## Remember

- **Everything is code** - You can create/modify anything
- **Follow conventions** - Naming, structure, patterns
- **Use templates** - Copy existing patterns
- **Read first** - Understand before modifying
- **Test mentally** - Think through the code flow
- **Document** - Add XML comments
- **Clean up** - Unsubscribe signals

You're building a **type-safe, inspectable, LLM-friendly** codebase. Every piece of code should be clear, predictable, and follow the established patterns.

---

## Example: Complete Feature Implementation

**User Request**: "Add a shield system where shields absorb damage before health"

**Your Process**:

1. **Create ShieldComponent.cs**:
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
        return damage - absorbed;
    }

    public void Regenerate()
    {
        Current += RegenPerTurn;
    }
}
```

2. **Modify CombatSystem.cs**:
```csharp
public void ExecuteCombat(Unit attacker, Unit defender)
{
    var damage = attacker.Damage.Value;

    // Check for shield
    if (defender.TryGetComponent<ShieldComponent>(out var shield) && shield.IsActive)
    {
        damage = shield.AbsorbDamage(damage);
        GD.Print($"Shield absorbed damage, {damage} overflow");
    }

    // Apply remaining damage
    if (damage > 0)
    {
        defender.Health.TakeDamage(damage);
    }

    EmitSignal(SignalName.CombatEnded, attacker, defender, !defender.Health.IsAlive);
}
```

3. **Create ShieldRegenSystem.cs**:
```csharp
using Godot;
using Undergang.Core;
using Undergang.Components;

namespace Undergang.Systems;

/// <summary>
/// Regenerates shields for all units each turn.
/// </summary>
[AutoRegister]
public partial class ShieldRegenSystem : Node, ISystem
{
    private GameWorld _world;
    private UnitManager _units;

    public override void _Ready()
    {
        _world = GetParent<GameWorld>();
        _units = _world.Units;

        Initialize();
    }

    public void Initialize()
    {
        _world.Turns.TurnEnded += OnTurnEnded;
    }

    private void OnTurnEnded(Unit unit)
    {
        foreach (var u in _units.GetAllUnits())
        {
            if (u.TryGetComponent<ShieldComponent>(out var shield) && u.Health.IsAlive)
            {
                shield.Regenerate();
            }
        }
    }

    public void Cleanup()
    {
        _world.Turns.TurnEnded -= OnTurnEnded;
    }

    public override void _ExitTree()
    {
        Cleanup();
    }
}
```

4. **Respond to user**:
   - "I've added the shield system with three components:"
   - "1. ShieldComponent - tracks shield value and absorbs damage"
   - "2. Modified CombatSystem - checks shields before applying damage"
   - "3. ShieldRegenSystem - regenerates shields each turn"
   - "To use, add ShieldComponent to any unit in their _Ready() method"

---

That's it! Follow these patterns and you'll create clean, maintainable, LLM-friendly code.

Happy coding! 🚀
