# Porting Undergang from C#/ECS to GDScript/Nodes

This document outlines how to port Undergang from its current C# Entity-Component-System (ECS) architecture to a traditional GDScript workflow using Godot's node-based patterns.

---

## Table of Contents

1. [Architecture Comparison](#architecture-comparison)
2. [Directory Structure](#directory-structure)
3. [Component to Property Translation](#component-to-property-translation)
4. [Entity to Node Translation](#entity-to-node-translation)
5. [System to Autoload/Manager Translation](#system-to-autoloadmanager-translation)
6. [Services Layer](#services-layer)
7. [Turn-Based Flow](#turn-based-flow)
8. [Combat System](#combat-system)
9. [Abilities System](#abilities-system)
10. [Rewind System](#rewind-system)
11. [Scene Structure](#scene-structure)
12. [Configuration](#configuration)
13. [Event System](#event-system)
14. [Code Examples](#code-examples)
15. [Migration Steps](#migration-steps)

---

## Architecture Comparison

| C# ECS Pattern | GDScript Node Pattern |
|----------------|----------------------|
| `Entity` (ID + component dictionary) | `Node` (scene instance with properties) |
| `Component` (readonly record struct) | `@export` properties on node scripts |
| `System` (logic processor class) | Autoload singleton or manager node |
| `Entities.Query<T>()` | `get_tree().get_nodes_in_group("group_name")` |
| `Services` (static singletons) | Autoload singletons |
| `Events` (C# events/delegates) | Godot signals |
| `async/await` | `await` with signals or coroutines |

### Key Philosophy Change

**C# ECS**: Data (components) is separate from behavior (systems). Entities are just IDs with attached component data. Systems iterate over entities with specific components.

**GDScript Nodes**: Data and behavior live together on nodes. Nodes are self-contained units with properties and methods. Communication happens through signals and direct method calls.

---

## Directory Structure

### Current C# Structure
```
src/
├── Components/     # Data definitions
├── Systems/        # Logic processors
├── Services/       # Singletons
├── Game/          # Entry point
├── Lib/           # Utilities
└── Scenes/        # .tscn files
```

### Proposed GDScript Structure
```
scripts/
├── autoloads/      # Global singletons (Game, Events, PathFinder, Config)
├── units/          # Unit scripts (BaseUnit, Player, Enemy types)
├── tiles/          # Tile scripts (HexTile)
├── managers/       # System managers (TurnManager, CombatManager, etc.)
├── ui/             # UI scripts
└── lib/            # Utilities (HexGrid, Tweener)

scenes/
├── units/          # Unit scenes (.tscn)
├── tiles/          # Tile scenes
├── ui/             # UI scenes
└── main.tscn       # Entry scene
```

---

## Component to Property Translation

Components become `@export` properties or regular properties on node scripts.

### C# Components → GDScript Properties

#### Core Properties
```gdscript
# On HexTile.gd
class_name HexTile
extends Area3D

@export var coordinate: Vector3i  # Was: Coordinate(Vector3I)
@export var tile_index: int       # Was: TileIndex(int)
@export var is_traversable: bool = true  # Was: Traversable/Untraversable markers

var is_selected: bool = false     # Was: SelectedTile marker
var threatening_unit_id: int = -1 # Was: AttackRangeTile(unitId)
var is_in_dash_range: bool = false # Was: DashRangeTile marker
```

#### Combat Properties
```gdscript
# On BaseUnit.gd
class_name BaseUnit
extends Node3D

@export var max_health: int = 3
@export var current_health: int = 3   # Was: Health(int)
@export var damage: int = 1           # Was: Damage(int)
@export var attack_range: int = 1     # Was: AttackRange(int)
@export var move_range: int = 1       # Was: MoveRange(int)
```

#### Ability Properties
```gdscript
# On Player.gd (extends BaseUnit)
class_name Player
extends BaseUnit

var dash_cooldown: int = 0     # Was: DashCooldown(int)
var dash_mode_active: bool = false  # Was: DashModeActive marker
var block_cooldown: int = 0    # Was: BlockCooldown(int)
var block_active: bool = false # Was: BlockActive marker
```

#### Turn State
```gdscript
# Managed by TurnManager autoload, not on units
# TurnManager.gd
var current_turn_unit: BaseUnit = null
var turn_order: Array[BaseUnit] = []
var waiting_for_action: bool = false
```

### Marker Components → Boolean Properties or Groups

C# marker components (empty structs for tagging) become either:
1. Boolean properties: `is_enemy`, `is_player`
2. Node groups: `add_to_group("enemies")`, `add_to_group("units")`
3. Class inheritance: `class_name Grunt extends Enemy`

```gdscript
# Option 1: Boolean properties
var is_player: bool = false
var is_enemy: bool = false

# Option 2: Groups (preferred for queries)
func _ready():
    add_to_group("units")
    add_to_group("enemies")  # if enemy
    add_to_group("players")  # if player

# Option 3: Class hierarchy
class_name Grunt extends Enemy  # Enemy extends BaseUnit
```

### Range Pattern Components → Enum + Method

```gdscript
# In BaseUnit.gd or separate RangeHelper.gd
enum RangePattern {
    CIRCLE,      # Adjacent 6 tiles
    DIAGONAL,    # Hoplite archer style
    HEX_RING,    # Ring at exact distance
    EXPLOSION,   # Area of effect
    NGON,        # Polygon pattern
    AXIS_Q,      # Q-axis aligned
    AXIS_R,      # R-axis aligned
    AXIS_S       # S-axis aligned
}

@export var range_pattern: RangePattern = RangePattern.CIRCLE

func get_attack_range_tiles(from_coord: Vector3i) -> Array[Vector3i]:
    match range_pattern:
        RangePattern.CIRCLE:
            return HexGrid.get_neighbors(from_coord)
        RangePattern.DIAGONAL:
            return HexGrid.get_diagonal_range(from_coord, Config.DIAGONAL_MIN, Config.DIAGONAL_MAX)
        # ... etc
```

---

## Entity to Node Translation

### C# Entity Class
```csharp
public class Entity
{
    public int Id { get; }
    private Dictionary<Type, object> _components = new();

    public void Add<T>(T component) => _components[typeof(T)] = component;
    public T Get<T>() => (T)_components[typeof(T)];
    public bool Has<T>() => _components.ContainsKey(typeof(T));
}
```

### GDScript Node Approach

Entities become scene instances with attached scripts. No separate Entity class needed.

```gdscript
# BaseUnit.gd - Base class for all units
class_name BaseUnit
extends Node3D

# Identity
var unit_id: int = -1  # Unique ID assigned by manager

# Combat properties
@export var max_health: int = 3
var current_health: int:
    get: return current_health
    set(value):
        current_health = value
        health_changed.emit(current_health)

@export var damage: int = 1
@export var attack_range: int = 1
@export var move_range: int = 1
@export var range_pattern: RangePattern = RangePattern.CIRCLE

# Position
var hex_coordinate: Vector3i:
    get: return hex_coordinate
    set(value):
        hex_coordinate = value
        position = HexGrid.hex_to_world(value)

# Signals
signal health_changed(new_health: int)
signal unit_defeated()

func _ready():
    add_to_group("units")
    current_health = max_health

func take_damage(amount: int):
    if amount <= 0:
        return
    current_health = max(0, current_health - amount)
    if current_health <= 0:
        unit_defeated.emit()

func is_alive() -> bool:
    return current_health > 0
```

### Entity Queries → Group Queries

```gdscript
# C#: Entities.Query<Player>().FirstOrDefault()
# GDScript:
func get_player() -> Player:
    var players = get_tree().get_nodes_in_group("players")
    return players[0] if players.size() > 0 else null

# C#: Entities.Query<Unit, Enemy>()
# GDScript:
func get_enemies() -> Array[BaseUnit]:
    var result: Array[BaseUnit] = []
    for node in get_tree().get_nodes_in_group("enemies"):
        result.append(node as BaseUnit)
    return result

# C#: Entities.Query<Tile, Traversable>()
# GDScript:
func get_traversable_tiles() -> Array[HexTile]:
    var result: Array[HexTile] = []
    for tile in get_tree().get_nodes_in_group("tiles"):
        if tile.is_traversable:
            result.append(tile)
    return result
```

---

## System to Autoload/Manager Translation

C# Systems become either:
1. **Autoload singletons** (for global access)
2. **Manager nodes** (children of main scene)

### Autoload Singletons (project.godot)

```ini
[autoload]
Config="*res://scripts/autoloads/config.gd"
Events="*res://scripts/autoloads/events.gd"
Game="*res://scripts/autoloads/game.gd"
HexGrid="*res://scripts/autoloads/hex_grid.gd"
PathFinder="*res://scripts/autoloads/path_finder.gd"
```

### Manager Nodes (Main Scene Children)

```
Main (Node3D)
├── TurnManager
├── CombatManager
├── MovementManager
├── RangeManager
├── DashManager
├── BlockManager
├── AnimationManager
├── UIManager
├── GameStateManager
├── Board (Node3D - holds tiles)
└── Units (Node3D - holds unit instances)
```

### System Translation Examples

#### TurnSystem → TurnManager

```gdscript
# scripts/managers/turn_manager.gd
class_name TurnManager
extends Node

signal turn_changed(unit: BaseUnit)
signal turn_restarted()

var turn_order: Array[BaseUnit] = []
var current_turn_index: int = 0
var waiting_for_action: bool = false

@onready var combat_manager: CombatManager = $"../CombatManager"
@onready var movement_manager: MovementManager = $"../MovementManager"
@onready var game_state_manager: GameStateManager = $"../GameStateManager"

func _ready():
    # Connect to player input
    Events.tile_selected.connect(_on_tile_selected)

func initialize_turn_order(units: Array[BaseUnit]):
    turn_order = units.duplicate()
    # Sort by turn order property if needed
    current_turn_index = 0
    start_next_turn()

func start_next_turn():
    if turn_order.is_empty():
        return

    var current_unit = turn_order[current_turn_index]

    # Capture snapshot at start of player turn
    if current_unit is Player:
        game_state_manager.capture_snapshot(current_turn_index)
        # Tick ability cooldowns
        current_unit.tick_cooldowns()

    waiting_for_action = current_unit is Player
    turn_changed.emit(current_unit)

func complete_unit_turn():
    current_turn_index = (current_turn_index + 1) % turn_order.size()
    start_next_turn()

func _on_tile_selected(tile: HexTile):
    if not waiting_for_action:
        return

    var player = get_tree().get_first_node_in_group("players") as Player
    if player == null:
        return

    waiting_for_action = false
    await execute_player_action(player, tile.coordinate)
    complete_unit_turn()

func execute_player_action(player: Player, destination: Vector3i):
    if player.dash_mode_active:
        await movement_manager.execute_dash(player, destination)
    else:
        await movement_manager.execute_move(player, destination)
```

#### MovementSystem → MovementManager

```gdscript
# scripts/managers/movement_manager.gd
class_name MovementManager
extends Node

signal move_started(unit: BaseUnit)
signal move_completed(unit: BaseUnit)

@onready var combat_manager: CombatManager = $"../CombatManager"
@onready var range_manager: RangeManager = $"../RangeManager"

func execute_move(unit: BaseUnit, destination: Vector3i):
    var path = PathFinder.find_path(unit.hex_coordinate, destination, unit.move_range)
    if path.is_empty():
        return

    move_started.emit(unit)

    # Process movement tile by tile for combat checks
    for i in range(1, path.size()):
        var from_coord = path[i - 1]
        var to_coord = path[i]

        # Check for reactive enemy attacks (entering threat zones)
        if unit is Player:
            var threats = range_manager.get_threatening_enemies(to_coord)
            for enemy in threats:
                if not range_manager.was_in_range(unit, enemy, from_coord):
                    # Entering NEW enemy range - they attack
                    await combat_manager.resolve_combat(enemy, unit)
                    if not unit.is_alive():
                        move_completed.emit(unit)
                        return

        # Check for player counter-attack (staying in range)
        if unit is Player:
            var adjacent_before = range_manager.get_adjacent_enemies(from_coord)
            var adjacent_after = range_manager.get_adjacent_enemies(to_coord)
            for enemy in adjacent_before:
                if enemy in adjacent_after:
                    # Player attacks enemy they were already fighting
                    await combat_manager.resolve_combat(unit, enemy)

        # Animate movement to this tile
        await animate_move_step(unit, to_coord)

    unit.hex_coordinate = destination
    move_completed.emit(unit)
    range_manager.update_ranges()

func execute_dash(unit: BaseUnit, destination: Vector3i):
    # Dash skips combat checks
    move_started.emit(unit)
    await animate_dash(unit, destination)
    unit.hex_coordinate = destination
    move_completed.emit(unit)
    range_manager.update_ranges()

func animate_move_step(unit: BaseUnit, to_coord: Vector3i):
    var tween = create_tween()
    var target_pos = HexGrid.hex_to_world(to_coord)
    tween.tween_property(unit, "position", target_pos, 0.3)
    await tween.finished

func animate_dash(unit: BaseUnit, to_coord: Vector3i):
    var tween = create_tween()
    var target_pos = HexGrid.hex_to_world(to_coord)
    tween.tween_property(unit, "position", target_pos, 0.15)
    await tween.finished
```

---

## Services Layer

### Events → Signals Autoload

```gdscript
# scripts/autoloads/events.gd
extends Node

# Unit events
signal unit_defeated(unit: BaseUnit)
signal unit_spawned(unit: BaseUnit)

# Movement events
signal move_started(unit: BaseUnit)
signal move_completed(unit: BaseUnit)

# Turn events
signal turn_changed(unit: BaseUnit)
signal turn_restarted()

# Tile events
signal tile_selected(tile: HexTile)
signal tile_hovered(tile: HexTile)
signal tile_unhovered(tile: HexTile)

# Combat events
signal combat_started(attacker: BaseUnit, defender: BaseUnit)
signal combat_resolved(attacker: BaseUnit, defender: BaseUnit, damage_dealt: int)

# Game events
signal game_over(player_won: bool)
signal game_ready()
signal rewind_completed(turn_number: int)
```

### PathFinder → Autoload with AStar3D

```gdscript
# scripts/autoloads/path_finder.gd
extends Node

var astar: AStar3D = AStar3D.new()
var coord_to_point_id: Dictionary = {}  # Vector3i -> int

func initialize(tiles: Array[HexTile]):
    astar.clear()
    coord_to_point_id.clear()

    var point_id = 0
    for tile in tiles:
        if tile.is_traversable:
            coord_to_point_id[tile.coordinate] = point_id
            astar.add_point(point_id, HexGrid.hex_to_world(tile.coordinate))
            point_id += 1

    # Connect neighbors
    for tile in tiles:
        if not tile.is_traversable:
            continue
        var this_id = coord_to_point_id.get(tile.coordinate, -1)
        if this_id < 0:
            continue
        for neighbor in HexGrid.get_neighbors(tile.coordinate):
            var neighbor_id = coord_to_point_id.get(neighbor, -1)
            if neighbor_id >= 0:
                astar.connect_points(this_id, neighbor_id)

func find_path(from: Vector3i, to: Vector3i, max_range: int = -1) -> Array[Vector3i]:
    var from_id = coord_to_point_id.get(from, -1)
    var to_id = coord_to_point_id.get(to, -1)

    if from_id < 0 or to_id < 0:
        return []

    var point_path = astar.get_point_path(from_id, to_id)
    if point_path.is_empty():
        return []

    var result: Array[Vector3i] = []
    for pos in point_path:
        result.append(HexGrid.world_to_hex(pos))

    if max_range > 0 and result.size() > max_range + 1:
        result.resize(max_range + 1)

    return result

func get_reachable_coords(start: Vector3i, max_range: int) -> Array[Vector3i]:
    var result: Array[Vector3i] = []
    var visited: Dictionary = {}
    var queue: Array = [[start, 0]]

    while queue.size() > 0:
        var current = queue.pop_front()
        var coord: Vector3i = current[0]
        var distance: int = current[1]

        if visited.has(coord):
            continue
        visited[coord] = true
        result.append(coord)

        if distance < max_range:
            for neighbor in HexGrid.get_neighbors(coord):
                if coord_to_point_id.has(neighbor) and not visited.has(neighbor):
                    queue.append([neighbor, distance + 1])

    return result

func set_point_disabled(coord: Vector3i, disabled: bool):
    var point_id = coord_to_point_id.get(coord, -1)
    if point_id >= 0:
        astar.set_point_disabled(point_id, disabled)
```

### HexGrid → Autoload Utility

```gdscript
# scripts/autoloads/hex_grid.gd
extends Node

const HEX_SIZE: float = 1.05

# Cube coordinate directions (q, r, s)
const DIRECTIONS: Array[Vector3i] = [
    Vector3i(1, -1, 0),   # East
    Vector3i(1, 0, -1),   # Northeast
    Vector3i(0, 1, -1),   # Northwest
    Vector3i(-1, 1, 0),   # West
    Vector3i(-1, 0, 1),   # Southwest
    Vector3i(0, -1, 1)    # Southeast
]

func hex_to_world(hex: Vector3i) -> Vector3:
    var x = HEX_SIZE * (3.0 / 2.0 * hex.x)
    var z = HEX_SIZE * (sqrt(3) / 2.0 * hex.x + sqrt(3) * hex.y)
    return Vector3(x, 0, z)

func world_to_hex(world: Vector3) -> Vector3i:
    var q = (2.0 / 3.0 * world.x) / HEX_SIZE
    var r = (-1.0 / 3.0 * world.x + sqrt(3) / 3.0 * world.z) / HEX_SIZE
    return cube_round(Vector3(q, r, -q - r))

func cube_round(cube: Vector3) -> Vector3i:
    var q = round(cube.x)
    var r = round(cube.y)
    var s = round(cube.z)

    var q_diff = abs(q - cube.x)
    var r_diff = abs(r - cube.y)
    var s_diff = abs(s - cube.z)

    if q_diff > r_diff and q_diff > s_diff:
        q = -r - s
    elif r_diff > s_diff:
        r = -q - s
    else:
        s = -q - r

    return Vector3i(int(q), int(r), int(s))

func get_neighbors(center: Vector3i) -> Array[Vector3i]:
    var result: Array[Vector3i] = []
    for dir in DIRECTIONS:
        result.append(center + dir)
    return result

func get_distance(a: Vector3i, b: Vector3i) -> int:
    return (abs(a.x - b.x) + abs(a.y - b.y) + abs(a.z - b.z)) / 2

func get_hexes_in_range(center: Vector3i, range_val: int) -> Array[Vector3i]:
    var result: Array[Vector3i] = []
    for q in range(-range_val, range_val + 1):
        for r in range(max(-range_val, -q - range_val), min(range_val, -q + range_val) + 1):
            var s = -q - r
            result.append(center + Vector3i(q, r, s))
    return result

func generate_hex_coordinates(map_size: int) -> Array[Vector3i]:
    return get_hexes_in_range(Vector3i.ZERO, map_size)

func get_diagonal_range(center: Vector3i, min_dist: int, max_dist: int) -> Array[Vector3i]:
    var result: Array[Vector3i] = []
    for dir in DIRECTIONS:
        for dist in range(min_dist, max_dist + 1):
            result.append(center + dir * dist)
    return result

func get_axis_range(center: Vector3i, axis: int, min_dist: int, max_dist: int) -> Array[Vector3i]:
    var result: Array[Vector3i] = []
    var positive_dir: Vector3i
    var negative_dir: Vector3i

    match axis:
        0:  # Q axis
            positive_dir = Vector3i(1, -1, 0)
            negative_dir = Vector3i(-1, 1, 0)
        1:  # R axis
            positive_dir = Vector3i(0, 1, -1)
            negative_dir = Vector3i(0, -1, 1)
        2:  # S axis
            positive_dir = Vector3i(1, 0, -1)
            negative_dir = Vector3i(-1, 0, 1)

    for dist in range(min_dist, max_dist + 1):
        result.append(center + positive_dir * dist)
        result.append(center + negative_dir * dist)

    return result
```

---

## Turn-Based Flow

### C# Flow (Event + Direct Calls)
```
TurnSystem.StartUnitTurn()
  → CaptureSnapshot (player turn)
  → Events.OnTurnChanged()
    → PlayerSystem waits for TileSelect
    → TileSelect event
    → TurnSystem.ExecutePlayerAction()
      → MovementSystem.ExecuteMove() (async)
        → CombatSystem.ResolveCombat() (async)
    → TurnSystem.CompleteUnitTurn()
      → Start next unit turn
```

### GDScript Flow (Signals + Awaits)

```gdscript
# TurnManager.gd
func start_next_turn():
    var unit = turn_order[current_turn_index]

    if unit is Player:
        game_state_manager.capture_snapshot(current_turn_index)
        unit.tick_cooldowns()
        waiting_for_action = true
        # Player turn - wait for tile selection signal
    else:
        # Enemy turn - execute immediately
        await execute_enemy_turn(unit)
        complete_unit_turn()

    turn_changed.emit(unit)

func _on_tile_selected(tile: HexTile):
    if not waiting_for_action:
        return

    var player = get_player()
    waiting_for_action = false

    await execute_player_action(player, tile.coordinate)
    complete_unit_turn()

func execute_enemy_turn(enemy: BaseUnit):
    var player = get_player()
    var distance = HexGrid.get_distance(enemy.hex_coordinate, player.hex_coordinate)

    if distance <= enemy.attack_range:
        # In range - pass turn (Hoplite style)
        return
    else:
        # Out of range - move toward player
        var path = PathFinder.find_path(enemy.hex_coordinate, player.hex_coordinate, enemy.move_range)
        if path.size() > 1:
            var destination = path[min(enemy.move_range, path.size() - 1)]
            await movement_manager.execute_move(enemy, destination)
```

---

## Combat System

```gdscript
# scripts/managers/combat_manager.gd
class_name CombatManager
extends Node

@onready var animation_manager: AnimationManager = $"../AnimationManager"
@onready var block_manager: BlockManager = $"../BlockManager"

func resolve_combat(attacker: BaseUnit, defender: BaseUnit):
    Events.combat_started.emit(attacker, defender)

    # Play attack animation
    await animation_manager.play_attack(attacker, defender)

    # Check for block
    if defender is Player and block_manager.is_block_active(defender):
        block_manager.consume_block(defender)
        Events.combat_resolved.emit(attacker, defender, 0)
        return

    # Apply damage
    var damage_dealt = attacker.damage
    defender.take_damage(damage_dealt)

    Events.combat_resolved.emit(attacker, defender, damage_dealt)

    if not defender.is_alive():
        await handle_defeat(defender)

func handle_defeat(unit: BaseUnit):
    await animation_manager.play_death(unit)
    Events.unit_defeated.emit(unit)

    # Update pathfinding
    PathFinder.set_point_disabled(unit.hex_coordinate, false)

    # Remove from turn order
    var turn_manager = get_node("../TurnManager") as TurnManager
    turn_manager.remove_from_turn_order(unit)

    # Remove from scene
    unit.queue_free()
```

---

## Abilities System

### Player Script with Abilities

```gdscript
# scripts/units/player.gd
class_name Player
extends BaseUnit

signal dash_state_changed(active: bool, cooldown: int)
signal block_state_changed(active: bool, cooldown: int)

var dash_cooldown: int = 0
var dash_mode_active: bool = false
var block_cooldown: int = 0
var block_active: bool = false

func _ready():
    super._ready()
    add_to_group("players")

func tick_cooldowns():
    if dash_cooldown > 0:
        dash_cooldown -= 1
        dash_state_changed.emit(dash_mode_active, dash_cooldown)
    if block_cooldown > 0:
        block_cooldown -= 1
        block_state_changed.emit(block_active, block_cooldown)

# Dash ability
func can_dash() -> bool:
    return dash_cooldown == 0

func toggle_dash_mode():
    if not can_dash():
        return
    dash_mode_active = not dash_mode_active
    dash_state_changed.emit(dash_mode_active, dash_cooldown)

func use_dash():
    dash_mode_active = false
    dash_cooldown = Config.DASH_COOLDOWN
    dash_state_changed.emit(dash_mode_active, dash_cooldown)

# Block ability
func can_block() -> bool:
    return block_cooldown == 0 and not block_active

func toggle_block():
    if block_active:
        # Can always turn off block
        block_active = false
    elif can_block():
        block_active = true
    block_state_changed.emit(block_active, block_cooldown)

func consume_block():
    block_active = false
    block_cooldown = Config.BLOCK_COOLDOWN
    block_state_changed.emit(block_active, block_cooldown)
```

---

## Rewind System

```gdscript
# scripts/managers/game_state_manager.gd
class_name GameStateManager
extends Node

signal rewind_started()
signal rewind_completed(turn_number: int)

var snapshot_history: Array[Dictionary] = []
var cooldown_remaining: int = 0

@onready var turn_manager: TurnManager = $"../TurnManager"

func capture_snapshot(turn_index: int):
    var snapshot: Dictionary = {
        "turn_number": turn_index,
        "timestamp": Time.get_unix_time_from_system(),
        "units": [],
        "tiles": []
    }

    # Capture unit states
    for unit in get_tree().get_nodes_in_group("units"):
        snapshot["units"].append({
            "scene_path": unit.scene_file_path,
            "coordinate": unit.hex_coordinate,
            "health": unit.current_health,
            "max_health": unit.max_health,
            "damage": unit.damage,
            "attack_range": unit.attack_range,
            "move_range": unit.move_range,
            "is_player": unit is Player,
            "dash_cooldown": unit.dash_cooldown if unit is Player else 0,
            "block_cooldown": unit.block_cooldown if unit is Player else 0,
            "block_active": unit.block_active if unit is Player else false,
            "unit_type": unit.unit_type if unit.has_method("get_unit_type") else ""
        })

    # Capture tile states (only traversability changes)
    for tile in get_tree().get_nodes_in_group("tiles"):
        snapshot["tiles"].append({
            "coordinate": tile.coordinate,
            "is_traversable": tile.is_traversable
        })

    snapshot_history.append(snapshot)

    # Cap history
    while snapshot_history.size() > Config.MAX_HISTORY_DEPTH:
        snapshot_history.pop_front()

func can_rewind() -> bool:
    return snapshot_history.size() >= 2 and cooldown_remaining == 0

func tick_cooldown():
    if cooldown_remaining > 0:
        cooldown_remaining -= 1

func rewind_one_turn() -> Dictionary:
    if not can_rewind():
        return {"success": false, "reason": "Cannot rewind"}

    rewind_started.emit()

    # Get previous snapshot (not current one)
    snapshot_history.pop_back()  # Remove current
    var target_snapshot = snapshot_history.back()

    await restore_snapshot(target_snapshot)

    cooldown_remaining = Config.REWIND_COOLDOWN
    rewind_completed.emit(target_snapshot["turn_number"])

    return {"success": true, "turn_rewinded_to": target_snapshot["turn_number"]}

func restore_snapshot(snapshot: Dictionary):
    # Clear current units
    for unit in get_tree().get_nodes_in_group("units"):
        unit.queue_free()

    await get_tree().process_frame  # Wait for queue_free to process

    # Respawn units from snapshot
    var units_node = get_node("/root/Main/Units")
    for unit_data in snapshot["units"]:
        var scene = load(unit_data["scene_path"])
        var unit = scene.instantiate()
        units_node.add_child(unit)

        unit.hex_coordinate = unit_data["coordinate"]
        unit.current_health = unit_data["health"]
        unit.max_health = unit_data["max_health"]
        unit.damage = unit_data["damage"]
        unit.attack_range = unit_data["attack_range"]
        unit.move_range = unit_data["move_range"]

        if unit is Player:
            unit.dash_cooldown = unit_data["dash_cooldown"]
            unit.block_cooldown = unit_data["block_cooldown"]
            unit.block_active = unit_data["block_active"]

    # Restore tile states
    for tile_data in snapshot["tiles"]:
        var tile = get_tile_at(tile_data["coordinate"])
        if tile:
            tile.is_traversable = tile_data["is_traversable"]

    # Rebuild pathfinding
    var tiles: Array[HexTile] = []
    for tile in get_tree().get_nodes_in_group("tiles"):
        tiles.append(tile)
    PathFinder.initialize(tiles)

    # Rebuild turn order
    var units: Array[BaseUnit] = []
    for unit in get_tree().get_nodes_in_group("units"):
        units.append(unit)
    turn_manager.initialize_turn_order(units)

func get_tile_at(coord: Vector3i) -> HexTile:
    for tile in get_tree().get_nodes_in_group("tiles"):
        if tile.coordinate == coord:
            return tile
    return null
```

---

## Scene Structure

### Main.tscn
```
Main (Node3D)
├── Lighting
│   ├── DirectionalLight3D
│   └── DirectionalLight3D2
├── Camera3D
├── Ground (MeshInstance3D)
├── TurnManager (script: turn_manager.gd)
├── CombatManager (script: combat_manager.gd)
├── MovementManager (script: movement_manager.gd)
├── RangeManager (script: range_manager.gd)
├── AnimationManager (script: animation_manager.gd)
├── GameStateManager (script: game_state_manager.gd)
├── UIManager (script: ui_manager.gd)
│   └── UI (CanvasLayer)
│       ├── HealthDisplay
│       ├── DashButton
│       ├── BlockButton
│       ├── RewindButton
│       └── FPSLabel
├── Board (Node3D)
│   └── [HexTile instances generated at runtime]
└── Units (Node3D)
    └── [Unit instances generated at runtime]
```

### HexTile.tscn
```
HexTile (Area3D, script: hex_tile.gd)
├── MeshInstance3D (hex_grass.gltf or hex_water.gltf)
└── CollisionShape3D (CylinderShape3D)
```

### BaseUnit.tscn (template)
```
BaseUnit (Node3D, script: base_unit.gd)
├── Model (imported GLTF/FBX)
├── AnimationPlayer
└── CollisionShape3D (optional, for mouse picking)
```

---

## Configuration

```gdscript
# scripts/autoloads/config.gd
extends Node

# Player
const PLAYER_START := Vector3i(0, 0, 0)
const PLAYER_SPAWN_EXCLUSION_RADIUS := 2

# Map
const DEFAULT_MAP_SIZE := 5
const BLOCKED_TILES_AMOUNT := 24

# Abilities
const DASH_RANGE := 2
const DASH_COOLDOWN := 4
const BLOCK_COOLDOWN := 3

# Ranges
const DIAGONAL_MIN := 2
const DIAGONAL_MAX := 6
const HEX_RING_DISTANCE := 2
const EXPLOSION_RADIUS := 2
const AXIS_MIN := 2
const AXIS_MAX := 5

# Rewind
const REWIND_COOLDOWN := 3
const MAX_HISTORY_DEPTH := 100
const REWIND_ANIMATION_SPEED := 0.3
const RESPAWN_FADE_DURATION := 0.5

# Animation
const MOVE_ANIMATION_SPEED := 0.3
const DASH_ANIMATION_SPEED := 0.15
const ATTACK_LUNGE_DISTANCE := 0.5
const ATTACK_ANIMATION_SPEED := 0.2

# UI
const TILE_SELECT_DURATION_MS := 500
```

---

## Event System

### Connecting Signals

```gdscript
# In _ready() of any script that needs to listen
func _ready():
    Events.unit_defeated.connect(_on_unit_defeated)
    Events.turn_changed.connect(_on_turn_changed)
    Events.tile_selected.connect(_on_tile_selected)

func _on_unit_defeated(unit: BaseUnit):
    # Handle unit defeat
    pass

# Emitting signals
Events.unit_defeated.emit(defeated_unit)
Events.turn_changed.emit(current_unit)
```

### Input Handling on Tiles

```gdscript
# scripts/tiles/hex_tile.gd
class_name HexTile
extends Area3D

func _ready():
    add_to_group("tiles")
    input_event.connect(_on_input_event)
    mouse_entered.connect(_on_mouse_entered)
    mouse_exited.connect(_on_mouse_exited)

func _on_input_event(_camera, event, _position, _normal, _shape_idx):
    if event is InputEventMouseButton:
        if event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
            Events.tile_selected.emit(self)

func _on_mouse_entered():
    Events.tile_hovered.emit(self)

func _on_mouse_exited():
    Events.tile_unhovered.emit(self)
```

---

## Code Examples

### Complete Player Script

```gdscript
# scripts/units/player.gd
class_name Player
extends BaseUnit

signal dash_state_changed(active: bool, cooldown: int)
signal block_state_changed(active: bool, cooldown: int)

var dash_cooldown: int = 0
var dash_mode_active: bool = false
var block_cooldown: int = 0
var block_active: bool = false

func _ready():
    super._ready()
    add_to_group("players")

    # Default player stats
    max_health = 3
    current_health = 3
    damage = 1
    attack_range = 1
    move_range = 1
    range_pattern = RangePattern.CIRCLE

func _input(event):
    if event.is_action_pressed("dash"):
        toggle_dash_mode()
    elif event.is_action_pressed("block"):
        toggle_block()

func tick_cooldowns():
    if dash_cooldown > 0:
        dash_cooldown -= 1
        dash_state_changed.emit(dash_mode_active, dash_cooldown)
    if block_cooldown > 0:
        block_cooldown -= 1
        block_state_changed.emit(block_active, block_cooldown)

func can_dash() -> bool:
    return dash_cooldown == 0

func toggle_dash_mode():
    if not can_dash() and not dash_mode_active:
        return
    dash_mode_active = not dash_mode_active
    dash_state_changed.emit(dash_mode_active, dash_cooldown)

func use_dash():
    dash_mode_active = false
    dash_cooldown = Config.DASH_COOLDOWN
    dash_state_changed.emit(dash_mode_active, dash_cooldown)

func can_block() -> bool:
    return block_cooldown == 0 and not block_active

func toggle_block():
    if block_active:
        block_active = false
    elif can_block():
        block_active = true
    block_state_changed.emit(block_active, block_cooldown)

func consume_block():
    block_active = false
    block_cooldown = Config.BLOCK_COOLDOWN
    block_state_changed.emit(block_active, block_cooldown)

func get_dash_range_tiles() -> Array[Vector3i]:
    return HexGrid.get_hexes_in_range(hex_coordinate, Config.DASH_RANGE)
```

### Complete Enemy Script

```gdscript
# scripts/units/enemy.gd
class_name Enemy
extends BaseUnit

enum EnemyType { GRUNT, WIZARD, SNIPER_Q, SNIPER_R, SNIPER_S }

@export var enemy_type: EnemyType = EnemyType.GRUNT

func _ready():
    super._ready()
    add_to_group("enemies")
    _setup_stats_for_type()

func _setup_stats_for_type():
    match enemy_type:
        EnemyType.GRUNT:
            max_health = 1
            damage = 1
            attack_range = 1
            move_range = 1
            range_pattern = RangePattern.CIRCLE
        EnemyType.WIZARD:
            max_health = 2
            damage = 1
            attack_range = 6
            move_range = 1
            range_pattern = RangePattern.DIAGONAL
        EnemyType.SNIPER_Q:
            max_health = 1
            damage = 2
            attack_range = 5
            move_range = 1
            range_pattern = RangePattern.AXIS_Q
        EnemyType.SNIPER_R:
            max_health = 1
            damage = 2
            attack_range = 5
            move_range = 1
            range_pattern = RangePattern.AXIS_R
        EnemyType.SNIPER_S:
            max_health = 1
            damage = 2
            attack_range = 5
            move_range = 1
            range_pattern = RangePattern.AXIS_S

    current_health = max_health
```

---

## Migration Steps

### Phase 1: Setup Project Structure
1. Create `scripts/` directory with subdirectories
2. Set up autoloads in `project.godot`:
   - Config, Events, HexGrid, PathFinder, Game
3. Create base scene structure in Main.tscn

### Phase 2: Core Systems
1. Implement `HexGrid` autoload (coordinate conversions)
2. Implement `Config` autoload (constants)
3. Implement `Events` autoload (signals)
4. Implement `PathFinder` autoload (A* pathfinding)

### Phase 3: Tiles
1. Create `HexTile.gd` script with properties
2. Update `HexTile.tscn` with new script
3. Implement grid generation in `Game` autoload

### Phase 4: Units
1. Create `BaseUnit.gd` with common properties/methods
2. Create `Player.gd` extending BaseUnit
3. Create `Enemy.gd` extending BaseUnit
4. Update unit scenes with new scripts

### Phase 5: Managers
1. Implement `TurnManager` (turn orchestration)
2. Implement `MovementManager` (movement + combat triggers)
3. Implement `CombatManager` (combat resolution)
4. Implement `RangeManager` (threat zones)
5. Implement `AnimationManager` (animations)

### Phase 6: Abilities
1. Implement dash ability in Player.gd
2. Implement block ability in Player.gd
3. Create UI buttons for abilities

### Phase 7: Rewind System
1. Implement `GameStateManager`
2. Create snapshot capture logic
3. Implement state restoration
4. Add UI for rewind

### Phase 8: Polish
1. Implement tile highlighting
2. Implement health display
3. Add input handling (keyboard shortcuts)
4. Test and fix edge cases

### Phase 9: Cleanup
1. Remove all C# files
2. Remove `Undergang.csproj`
3. Update `project.godot` to remove C# references
4. Test full game loop

---

## Summary

The key differences when porting from C#/ECS to GDScript/Nodes:

1. **Data on Nodes**: Instead of separate component dictionaries, data lives as properties on node scripts
2. **Groups for Queries**: Use `get_nodes_in_group()` instead of type-based entity queries
3. **Signals for Events**: Use Godot signals instead of C# events
4. **Await for Async**: Use `await` with signals and tweens instead of Task-based async
5. **Autoloads for Singletons**: Use Godot's autoload system for global services
6. **Class Inheritance**: Use class inheritance for shared behavior instead of component composition
7. **Scene Instancing**: Units and tiles are scene instances, not abstract entity IDs

The core game logic and mechanics remain the same - only the architecture patterns change to be more idiomatic for GDScript and Godot's node-based design.
