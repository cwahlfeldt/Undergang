# Turn Rewind Feature Plan

## Overview

Add a rewind feature that records game state at turn boundaries, allowing the player to restore to any previous turn state.

## Feasibility Assessment: ✅ Highly Feasible

The hybrid ECS architecture is well-suited for state snapshots:

- Components are readonly record structs (immutable data)
- Entity state is self-contained in component dictionaries
- Turn boundaries provide clear checkpoint moments
- Minimal system state beyond components (just `_currentTurnIndex`)

## Key Challenges & Solutions

| Challenge                                             | Solution                                                   |
| ----------------------------------------------------- | ---------------------------------------------------------- |
| Godot node references (`Instance`, `AnimationPlayer`) | Exclude from snapshots - rebuild visual state on restore   |
| Defeated entities (removed from game)                 | Store full entity state before removal; recreate on rewind |
| Entity ID continuity                                  | Capture `Entities._nextId`; reset on restore               |
| Animation state                                       | Reset to `Idle` on restore; let systems recalculate        |
| Range/pathfinding state                               | Recalculated automatically from positions                  |

## Design Decisions (Confirmed)

1. **Rewind Limit**: Cooldown-based (must wait N turns between rewinds)
2. **History Depth**: Last turn only (single undo, simpler implementation)
3. **Visual Effect**: Animated replay (show units moving back to positions)

---

## Implementation Plan

### Phase 1: Snapshot Data Structures

**New file: `src/Services/TurnHistory.cs`**

```csharp
public record EntitySnapshot
{
    public int EntityId { get; init; }
    public Vector3I? PreviousPosition { get; init; }  // For animated replay
    public Dictionary<Type, object> Components { get; init; }
}

public record TurnSnapshot
{
    public int TurnNumber { get; init; }
    public int NextEntityId { get; init; }
    public int CurrentTurnIndex { get; init; }
    public List<EntitySnapshot> Entities { get; init; }
}

public class TurnHistory
{
    private TurnSnapshot? _lastSnapshot;  // Single snapshot (last turn only)
    private int _cooldownRemaining = 0;

    public void CaptureSnapshot(TurnSnapshot snapshot) => _lastSnapshot = snapshot;
    public TurnSnapshot? GetSnapshot() => _lastSnapshot;
    public void ClearSnapshot() => _lastSnapshot = null;

    public bool CanRewind => _lastSnapshot != null && _cooldownRemaining == 0;
    public int CooldownRemaining => _cooldownRemaining;

    public void StartCooldown(int turns) => _cooldownRemaining = turns;
    public void TickCooldown() { if (_cooldownRemaining > 0) _cooldownRemaining--; }
}
```

**Add to `Config.cs`:**

```csharp
public static int RewindCooldownTurns = 3;  // Turns to wait between rewinds
```

### Phase 2: Snapshot Capture

**Modify: `src/Services/Entities.cs`**

Add methods to:

1. `CreateSnapshot()` - Serialize all entities and their serializable components
2. `RestoreFromSnapshot(TurnSnapshot)` - Rebuild entity state from snapshot

**Components to capture:**

- `Coordinate`, `Health`, `Damage`, `AttackRange`, `MoveRange`
- `DashCooldown`, `BlockCooldown`, `TurnOrder`
- `Unit(UnitType)`, all unit type markers (`Player`, `Enemy`, `Grunt`, etc.)
- All range type markers (`RangeCircle`, `RangeDiagonal`, etc.)

**Components to skip (rebuilt on restore):**

- `Instance`, `AnimationPlayer` (Godot nodes)
- `CurrentAnimation` (reset to Idle)
- `CurrentTurn`, `WaitingForAction`, `SelectedTile` (turn state)
- `AttackRangeTile`, `DashRangeTile` (recalculated by systems)

### Phase 3: Capture Triggers

**Modify: `src/Systems/TurnSystem.cs`**

Capture snapshot at turn boundaries:

```csharp
public void CompleteUnitTurn(Entity unit)
{
    // Existing code...

    // Capture state after player's turn ends (before enemy turns)
    if (unit.Has<Player>())
    {
        _turnHistory.Push(Entities.CreateSnapshot(_currentTurnIndex));
    }
}
```

### Phase 4: Restore Logic

**Modify: `src/Services/Entities.cs`**

```csharp
public void RestoreFromSnapshot(TurnSnapshot snapshot)
{
    // 1. Clear all existing entities
    foreach (var entity in _entities.Values.ToList())
        RemoveEntity(entity);

    // 2. Reset entity ID counter
    _nextId = snapshot.NextEntityId;

    // 3. Recreate all entities with components
    foreach (var entitySnap in snapshot.Entities)
    {
        var entity = new Entity(entitySnap.EntityId);
        foreach (var (type, component) in entitySnap.Components)
            entity.AddComponent(type, component);
        _entities[entitySnap.EntityId] = entity;
    }

    // 4. Rebuild visual nodes for units
    RebuildVisualState();
}
```

### Phase 5: Visual State Rebuild

**New method in `Entities.cs` or `EntityFactory.cs`**

After restoring component data, recreate Godot scene nodes:

```csharp
private void RebuildVisualState()
{
    foreach (var entity in Query<Unit>())
    {
        var coord = entity.Get<Coordinate>();
        var unitType = entity.Get<Unit>().Type;

        // Instantiate appropriate scene
        var scene = unitType == UnitType.Player ? PlayerScene : EnemyScene;
        var instance = scene.Instantiate<Node3D>();

        // Position at correct coordinate
        instance.Position = HexGrid.ToWorld(coord);

        // Attach to entity
        entity.Update(new Instance(instance));

        // Setup animation player if present
        SetupAnimationPlayer(entity, instance);
    }

    // Update range system to recalculate threat zones
    RangeSystem.UpdateRanges();
}
```

### Phase 6: Animated Replay System

**New file: `src/Systems/RewindSystem.cs`**

Handles the visual rewind animation:

```csharp
public class RewindSystem : System
{
    private TurnHistory _turnHistory;
    private Tweener _tweener;

    public async Task ExecuteRewind()
    {
        if (!_turnHistory.CanRewind) return;

        var snapshot = _turnHistory.GetSnapshot();

        // 1. Animate units moving back to previous positions
        var animations = new List<Task>();
        foreach (var entitySnap in snapshot.Entities)
        {
            var entity = Entities.GetById(entitySnap.EntityId);
            if (entity == null || !entitySnap.PreviousPosition.HasValue) continue;

            var instance = entity.Get<Instance>().Value;
            var targetPos = HexGrid.ToWorld(entitySnap.PreviousPosition.Value);

            // Reverse movement animation
            animations.Add(_tweener.MoveTo(instance, targetPos, 0.3f));
        }
        await Task.WhenAll(animations);

        // 2. Restore full state from snapshot
        Entities.RestoreFromSnapshot(snapshot);

        // 3. Respawn defeated enemies with fade-in effect
        await RespawnDefeatedUnits(snapshot);

        // 4. Start cooldown
        _turnHistory.StartCooldown(Config.RewindCooldownTurns);
        _turnHistory.ClearSnapshot();

        // 5. Restart turn
        _turnSystem.RestartPlayerTurn();
    }

    private async Task RespawnDefeatedUnits(TurnSnapshot snapshot)
    {
        // Find entities in snapshot that don't exist currently
        foreach (var entitySnap in snapshot.Entities)
        {
            if (Entities.GetById(entitySnap.EntityId) != null) continue;

            // Recreate enemy with fade-in animation
            var enemy = RecreateEntity(entitySnap);
            var instance = enemy.Get<Instance>().Value;
            instance.Modulate = new Color(1, 1, 1, 0);
            await _tweener.FadeTo(instance, 1.0f, 0.3f);
        }
    }
}
```

### Phase 7: Input Binding & Cooldown Tracking

**Modify: `src/Systems/PlayerSystem.cs`**

Add rewind input handling:

```csharp
public override void Update()
{
    if (Input.IsActionJustPressed("rewind"))
    {
        if (_turnHistory.CanRewind)
            _rewindSystem.ExecuteRewind();
        else if (_turnHistory.CooldownRemaining > 0)
            ShowCooldownMessage(_turnHistory.CooldownRemaining);
    }
}
```

**Modify: `src/Systems/TurnSystem.cs`**

Tick cooldown each player turn:

```csharp
public void CompleteUnitTurn(Entity unit)
{
    // Existing code...

    if (unit.Has<Player>())
    {
        _turnHistory.CaptureSnapshot(Entities.CreateSnapshot(_currentTurnIndex));
        _turnHistory.TickCooldown();  // Reduce cooldown each turn
    }
}
```

**Modify: `project.godot`**

Add input action:

```
[input]
rewind={
    "deadzone": 0.5,
    "events": [Object(InputEventKey,"keycode":4194305,"pressed":true)]  # Ctrl+Z
}
```

### Phase 8: UI Feedback

Add visual indicator showing:

- Cooldown remaining (if any)
- "Rewind available" indicator when ready
- Brief "Rewinding..." overlay during animation

---

## Files to Create/Modify

| File                          | Action | Purpose                                            |
| ----------------------------- | ------ | -------------------------------------------------- |
| `src/Services/TurnHistory.cs` | Create | Snapshot storage, cooldown tracking                |
| `src/Systems/RewindSystem.cs` | Create | Animated rewind execution                          |
| `src/Services/Entities.cs`    | Modify | Add `CreateSnapshot()` and `RestoreFromSnapshot()` |
| `src/Systems/TurnSystem.cs`   | Modify | Capture snapshots, tick cooldown                   |
| `src/Systems/PlayerSystem.cs` | Modify | Handle rewind input                                |
| `src/Game/GameManager.cs`     | Modify | Register RewindSystem                              |
| `src/Config.cs`               | Modify | Add `RewindCooldownTurns` constant                 |
| `project.godot`               | Modify | Add rewind input action                            |

## Testing Strategy

1. **Basic rewind**: Complete a turn, rewind, verify positions restored with animation
2. **Combat rewind**: Kill an enemy, rewind, verify enemy respawns with fade-in
3. **Cooldown enforcement**: Rewind once, verify blocked for N turns, verify re-enabled
4. **Edge cases**: Rewind at game start (no snapshot), rewind during cooldown (show message)

## Risks & Mitigations

| Risk                  | Mitigation                                         |
| --------------------- | -------------------------------------------------- |
| Node leaks on restore | Explicitly free old nodes before recreating        |
| Animation glitches    | Force all units to Idle before restoring positions |
| Pathfinding desync    | Call `RangeSystem.UpdateRanges()` after restore    |

## Estimated Complexity

- **Core implementation**: Moderate (~200-300 lines)
- **Visual rebuild**: Moderate (reusing existing EntityFactory patterns)
- **Animated replay**: Moderate (reuse Tweener for reverse movement)
- **Testing**: Straightforward (turn-based, deterministic)
