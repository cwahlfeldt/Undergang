# Architecture Guide: Turn Flow Orchestration

## Current Architecture Overview

Undergang uses a **hybrid ECS (Entity-Component-System) architecture** optimized for turn-based tactical gameplay. While the game uses ECS patterns for entity management and queries, it employs **explicit orchestration** for turn flow to maintain clarity and debuggability.

---

## The "Strangeness" Problem

### Current Event-Driven Flow (Implicit)

```
PlayerSystem → TileSelect event
    ↓
PlayerSystem.Update() → Adds Movement component
    ↓
MovementSystem.Update() → Detects Movement component → Processes → Fires MoveCompleted event
    ↓
RangeSystem → Listens to MoveCompleted → Updates ranges
    ↓
AnimationSystem → Listens to MoveCompleted → Sets Idle
    ↓
TurnSystem → Listens to UnitActionComplete → Advances turn
```

**Problems:**
- ❌ Flow is hidden across events and Update() calls
- ❌ Hard to trace "what happens when I click a tile?"
- ❌ Polling pattern (Update checks for components) wastes cycles
- ❌ Debugging requires breakpoints in 6+ different systems

---

## Option 2: Turn Orchestration Pattern

### Philosophy

**ECS where it helps. Imperative where it's clearer.**

- ✅ Use ECS for: Entity storage, component queries, data modeling
- ✅ Use direct calls for: Turn flow, action sequencing, game logic

### Proposed Architecture

```
PlayerSystem → OnTileSelected()
    ↓
TurnSystem.ExecutePlayerAction(player, destination)
    ├─→ MovementSystem.Move(player, dest)
    ├─→ CombatSystem.CheckAndResolveCombat(player)
    ├─→ AnimationSystem.SetIdle(player)
    └─→ TurnSystem.AdvanceToNextUnit()
```

**Benefits:**
- ✅ Flow is explicit and traceable
- ✅ One function shows complete action sequence
- ✅ Easy to debug (single breakpoint, step through)
- ✅ No polling - actions execute when triggered
- ✅ Still uses ECS for queries and component management

---

## Implementation Guide

### Step 1: Add Action Execution to TurnSystem

**Current TurnSystem:**
```csharp
// Only manages turn order, doesn't orchestrate actions
private void OnUnitActionComplete(Entity entity)
{
    entity.Remove<CurrentTurn>();
    AdvanceTurn();
}
```

**Updated TurnSystem:**
```csharp
public class TurnSystem : System
{
    private MovementSystem _movementSystem;
    private CombatSystem _combatSystem;
    private AnimationSystem _animationSystem;

    public override void Initialize()
    {
        // Get system references
        _movementSystem = Systems.Get<MovementSystem>();
        _combatSystem = Systems.Get<CombatSystem>();
        _animationSystem = Systems.Get<AnimationSystem>();

        SetupInitialTurnOrder();
    }

    /// <summary>
    /// Orchestrates a complete player action from start to finish
    /// </summary>
    public async Task ExecutePlayerAction(Entity player, Vector3I destination)
    {
        // Clear waiting state
        player.Remove<WaitingForAction>();

        // Execute movement with combat
        bool playerDefeated = await _movementSystem.ExecuteMove(player, destination);

        if (playerDefeated)
        {
            // Handle player defeat
            GD.Print("Player defeated!");
            return;
        }

        // Set animation back to idle
        if (player.Has<Unit>())
        {
            _animationSystem.SetAnimationState(player, AnimationState.Idle);
        }

        // Complete the turn
        CompleteUnitTurn(player);
    }

    /// <summary>
    /// Orchestrates a complete enemy action
    /// </summary>
    public async Task ExecuteEnemyAction(Entity enemy, Vector3I destination)
    {
        enemy.Remove<WaitingForAction>();

        // Enemy movement (no combat on enemy turn in Hoplite-style)
        await _movementSystem.ExecuteMove(enemy, destination);

        // Set animation
        if (enemy.Has<Unit>())
        {
            _animationSystem.SetAnimationState(enemy, AnimationState.Idle);
        }

        // Complete the turn
        CompleteUnitTurn(enemy);
    }

    /// <summary>
    /// Enemy passes turn without acting
    /// </summary>
    public void ExecuteEnemyPass(Entity enemy)
    {
        GD.Print($"Enemy {enemy.Id} passes turn (player in range)");
        enemy.Remove<WaitingForAction>();
        CompleteUnitTurn(enemy);
    }

    private void CompleteUnitTurn(Entity unit)
    {
        unit.Remove<CurrentTurn>();
        AdvanceToNextUnit();
    }

    private void AdvanceToNextUnit()
    {
        var allUnits = Entities.Query<TurnOrder>()
            .OrderBy(e => e.Get<TurnOrder>())
            .ToList();

        _currentTurnIndex = (_currentTurnIndex + 1) % allUnits.Count;

        var nextUnit = allUnits[_currentTurnIndex];
        StartUnitTurn(nextUnit);
    }

    private void StartUnitTurn(Entity unit)
    {
        PathFinder.SetupPathfinding();
        unit.Add(new CurrentTurn());
        unit.Add(new WaitingForAction());
        Events.OnTurnChanged(unit);  // For UI updates
    }
}
```

---

### Step 2: Simplify PlayerSystem

**Current PlayerSystem:**
```csharp
public override async Task Update()
{
    var selectedTile = Entities.Query<SelectedTile>().FirstOrDefault();
    // ... polls for components and adds Movement
}
```

**Updated PlayerSystem:**
```csharp
public class PlayerSystem : System
{
    private TurnSystem _turnSystem;

    public override void Initialize()
    {
        _turnSystem = Systems.Get<TurnSystem>();
        Events.TileSelect += OnTileSelect;
    }

    private async void OnTileSelect(Entity tile)
    {
        var player = Entities.Query<Player>().FirstOrDefault();

        // Validation
        if (player == null || !player.Has<WaitingForAction>())
            return;

        if (!tile.Has<Tile>() || !tile.Has<Traversable>())
            return;

        var destination = tile.Get<Coordinate>();

        // Direct orchestration - clear and traceable
        await _turnSystem.ExecutePlayerAction(player, destination);
    }

    public override void Cleanup()
    {
        Events.TileSelect -= OnTileSelect;
    }
}
```

**Remove Update()** - No longer needed! Action happens on event, not polling.

---

### Step 3: Simplify EnemySystem

**Current EnemySystem:**
```csharp
public override async Task Update()
{
    var enemy = Entities.Query<Enemy, CurrentTurn>().FirstOrDefault();
    // ... complex AI logic, adds Movement component
}
```

**Updated EnemySystem:**
```csharp
public class EnemySystem : System
{
    private TurnSystem _turnSystem;

    public override void Initialize()
    {
        _turnSystem = Systems.Get<TurnSystem>();
        Events.TurnChanged += OnTurnChanged;
    }

    private async void OnTurnChanged(Entity unit)
    {
        // Only process enemy turns
        if (!unit.Has<Enemy>())
            return;

        var player = Entities.Query<Player>().FirstOrDefault();
        if (player == null)
            return;

        var enemyCoord = unit.Get<Coordinate>();
        var playerCoord = player.Get<Coordinate>();

        // Check if player is in attack range
        var attackRangeTiles = RangeSystem.GetAttackRangeTiles(unit, enemyCoord);
        bool playerInRange = attackRangeTiles.Contains(playerCoord);

        if (playerInRange)
        {
            // Pass turn - player already in range
            _turnSystem.ExecuteEnemyPass(unit);
        }
        else
        {
            // Determine movement target based on enemy type
            Vector3I targetPosition;

            if (unit.Has<Sniper>())
            {
                targetPosition = FindSniperTargetPosition(enemyCoord, playerCoord, unit.Get<MoveRange>());
            }
            else
            {
                targetPosition = playerCoord;  // Grunt: move toward player
            }

            // Execute movement
            await _turnSystem.ExecuteEnemyAction(unit, targetPosition);
        }
    }

    private Vector3I FindSniperTargetPosition(Vector3I sniperCoord, Vector3I playerCoord, int moveRange)
    {
        // ... existing sniper AI logic
    }

    public override void Cleanup()
    {
        Events.TurnChanged -= OnTurnChanged;
    }
}
```

**Remove Update()** - Actions happen on TurnChanged event, not polling.

---

### Step 4: Update MovementSystem Interface

**Current MovementSystem:**
```csharp
public override async Task Update()
{
    var mover = Entities.Query<Movement, CurrentTurn>().FirstOrDefault();
    // ... processes movement
}
```

**Updated MovementSystem:**
```csharp
public class MovementSystem : System
{
    private CombatSystem _combatSystem;
    private AnimationSystem _animationSystem;

    /// <summary>
    /// Executes a unit's movement from current position to destination
    /// Handles pathfinding, animation, and combat resolution
    /// Returns true if unit was defeated during movement
    /// </summary>
    public async Task<bool> ExecuteMove(Entity mover, Vector3I destination)
    {
        var origin = mover.Get<Coordinate>();
        var path = PathFinder.FindPath(origin, destination, mover.Get<MoveRange>());

        // Check for combat along the path
        bool unitDefeated = await ProcessMovementWithCombat(mover, path);

        if (unitDefeated)
        {
            return true;  // Unit defeated
        }

        var fromTile = Entities.GetAt(path.First());
        var toTile = Entities.GetAt(path.Last());

        // Fire event for UI updates, range recalculation
        Events.OnMoveCompleted(mover, fromTile.Get<Coordinate>(), toTile.Get<Coordinate>());

        return false;  // Unit survived
    }

    private async Task<bool> ProcessMovementWithCombat(Entity mover, List<Vector3I> path)
    {
        // ... existing combat logic
    }
}
```

**Remove Update()** - Called directly by TurnSystem.

---

## Key Changes Summary

### Before (Event-Driven Polling)

```
System.Update() {
    Query for component
    If component exists → Process
    Fire events
}
```

### After (Direct Orchestration)

```
TurnSystem.ExecuteAction(unit, target) {
    MovementSystem.Move(unit, target)
    CombatSystem.Resolve(unit)
    AnimationSystem.SetIdle(unit)
    CompleteUnitTurn(unit)
}
```

---

## What Stays the Same

✅ **Component queries** - Still use ECS patterns
✅ **Entity management** - EntityFactory, Entities service unchanged
✅ **Events for notifications** - UI still listens to TurnChanged, MoveCompleted
✅ **System organization** - Systems still separated by concern
✅ **Async/await** - Animation timing still works

---

## What Changes

🔄 **Turn flow** - Explicit orchestration instead of event chain
🔄 **System.Update()** - Removed from Player/Enemy/Movement systems
🔄 **Action execution** - Direct calls instead of component polling
🔄 **Dependencies** - Systems reference TurnSystem for orchestration

---

## Benefits of This Approach

### 1. **Debuggability**
```csharp
// Single breakpoint shows entire action flow
public async Task ExecutePlayerAction(Entity player, Vector3I dest)
{
    await MovementSystem.Move(player, dest);      // ← Step here
    CombatSystem.Resolve(player);                 // ← See exact sequence
    AnimationSystem.SetIdle(player);              // ← No hidden events
    CompleteUnitTurn(player);                     // ← Clear end
}
```

### 2. **Traceability**
Ask "What happens when I click a tile?"
→ Open `TurnSystem.ExecutePlayerAction()`
→ Read 10 lines of code
→ Complete understanding ✅

### 3. **Turn-Based Fit**
Matches how turn-based games actually work:
1. Player takes action
2. Action resolves completely
3. Next unit's turn

### 4. **Performance**
No more polling Update() loops checking for components that rarely exist.

### 5. **Maintainability**
Adding new actions is obvious:
```csharp
public async Task ExecuteSkillAction(Entity unit, SkillType skill)
{
    // New action type - clear where it goes
    SkillSystem.UseSkill(unit, skill);
    AnimationSystem.PlaySkill(unit, skill);
    CompleteUnitTurn(unit);
}
```

---

## Migration Path

### Phase 1: Add Orchestration Methods
Add `ExecutePlayerAction()`, `ExecuteEnemyAction()` to TurnSystem

### Phase 2: Update Event Handlers
Change PlayerSystem/EnemySystem to call TurnSystem directly

### Phase 3: Simplify MovementSystem
Add `ExecuteMove()` method, keep Update() for compatibility

### Phase 4: Remove Polling
Once working, remove Update() from systems that no longer need it

### Phase 5: Test
Verify turn flow, combat, animations all work

---

## Code Impact

**Files to modify:** 4
**Lines added:** ~100
**Lines removed:** ~50
**Net change:** ~50 LOC

**Modified files:**
- `src/Systems/TurnSystem.cs` - Add orchestration methods
- `src/Systems/PlayerSystem.cs` - Remove Update(), call TurnSystem
- `src/Systems/EnemySystem.cs` - Remove Update(), call TurnSystem
- `src/Systems/MovementSystem.cs` - Add ExecuteMove() method

---

## When NOT to Use This Pattern

This orchestration pattern is ideal for turn-based games, but **avoid it if:**

- ❌ Building a real-time game (stick to pure ECS Update loops)
- ❌ Managing thousands of entities (orchestration doesn't scale)
- ❌ Need pure ECS for performance (direct calls add overhead)
- ❌ Actions are truly independent (events work better)

**For Undergang**: ✅ Perfect fit. 10-20 units, turn-based, sequential actions.

---

## Further Reading

- `CLAUDE.md` - Project overview and current patterns
- `src/Systems/TurnSystem.cs` - Turn management implementation
- `src/Systems/MovementSystem.cs` - Movement and combat flow
- `ANIMATIONS.md` - Animation integration guide

---

## Questions?

**Q: Doesn't this break ECS principles?**
A: "Pure" ECS is for real-time games with thousands of entities. Turn-based games benefit from explicit sequencing. Use the right tool for the job.

**Q: What about testing?**
A: Direct calls are easier to test than event chains. Mock TurnSystem, test PlayerSystem in isolation.

**Q: Can I still use events?**
A: Yes! Events are great for notifications (UI updates). Just not for control flow.

**Q: What if I want undo/replay?**
A: Add command pattern AFTER implementing this. Commands can call TurnSystem methods.

**Q: Is this scalable?**
A: For turn-based games with <100 units? Absolutely. For 10,000 units? Use pure ECS.
