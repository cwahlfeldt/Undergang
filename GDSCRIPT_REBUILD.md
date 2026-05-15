# Rebuilding Undergang in GDScript

A practical guide to rebuilding this turn-based tactics game using standard Godot patterns.

---

## What You're Building

- Hex-grid tactics game
- Turn-based: player moves, then enemies move
- Hoplite-style combat: enemies attack when you walk into their range
- Abilities: Dash (escape), Block (negate hit)
- Rewind to undo mistakes

---

## Project Structure

```
scenes/
├── main.tscn           # Entry point
├── hex_tile.tscn       # Single hex tile
├── player.tscn         # Player unit
├── grunt.tscn          # Basic enemy
├── wizard.tscn         # Ranged enemy
└── ui.tscn             # HUD

scripts/
├── game.gd             # Autoload - game state & turn logic
├── hex_grid.gd         # Autoload - hex math utilities
├── hex_tile.gd
├── player.gd
├── enemy.gd
└── ui.gd
```

---

## Autoloads (Project Settings → Autoload)

### game.gd - Central Game Controller

```gdscript
extends Node

signal turn_started(unit)
signal player_died
signal enemy_died(enemy)

var player: Node3D
var enemies: Array[Node3D] = []
var tiles: Dictionary = {}  # Vector3i -> HexTile

var current_unit = null
var is_player_turn := true
var history: Array[Dictionary] = []  # For rewind

func _ready():
    pass

# Called after scene loads
func start_game():
    player = get_tree().get_first_node_in_group("player")
    enemies = get_tree().get_nodes_in_group("enemies").duplicate()

    for tile in get_tree().get_nodes_in_group("tiles"):
        tiles[tile.coord] = tile

    save_state()
    start_player_turn()

func start_player_turn():
    is_player_turn = true
    player.start_turn()
    turn_started.emit(player)

func end_player_turn():
    is_player_turn = false
    await do_enemy_turns()
    save_state()
    start_player_turn()

func do_enemy_turns():
    for enemy in enemies:
        if not is_instance_valid(enemy):
            continue
        await enemy.take_turn()
        await get_tree().create_timer(0.2).timeout

func get_tile(coord: Vector3i) -> Node:
    return tiles.get(coord)

func is_tile_blocked(coord: Vector3i) -> bool:
    var tile = tiles.get(coord)
    if not tile or not tile.walkable:
        return true
    # Check if unit is there
    if player.coord == coord:
        return true
    for e in enemies:
        if is_instance_valid(e) and e.coord == coord:
            return true
    return false

func get_enemy_at(coord: Vector3i) -> Node3D:
    for e in enemies:
        if is_instance_valid(e) and e.coord == coord:
            return e
    return null

func remove_enemy(enemy):
    enemies.erase(enemy)
    enemy_died.emit(enemy)
    enemy.queue_free()

# === REWIND ===
func save_state():
    var state = {
        "player_coord": player.coord,
        "player_hp": player.hp,
        "player_dash_cd": player.dash_cooldown,
        "player_block_cd": player.block_cooldown,
        "player_block_active": player.block_active,
        "enemies": []
    }
    for e in enemies:
        if is_instance_valid(e):
            state.enemies.append({
                "scene": e.scene_file_path,
                "coord": e.coord,
                "hp": e.hp
            })
    history.append(state)
    if history.size() > 50:
        history.pop_front()

func rewind():
    if history.size() < 2:
        return
    history.pop_back()  # Remove current
    var state = history.back()

    # Restore player
    player.coord = state.player_coord
    player.hp = state.player_hp
    player.dash_cooldown = state.player_dash_cd
    player.block_cooldown = state.player_block_cd
    player.block_active = state.player_block_active
    player.position = HexGrid.to_world(state.player_coord)

    # Clear enemies
    for e in enemies:
        if is_instance_valid(e):
            e.queue_free()
    enemies.clear()

    await get_tree().process_frame

    # Respawn enemies
    var units_parent = get_tree().get_first_node_in_group("units_container")
    for e_data in state.enemies:
        var scene = load(e_data.scene)
        var enemy = scene.instantiate()
        units_parent.add_child(enemy)
        enemy.coord = e_data.coord
        enemy.hp = e_data.hp
        enemy.position = HexGrid.to_world(e_data.coord)
        enemies.append(enemy)

    start_player_turn()
```

### hex_grid.gd - Hex Math

```gdscript
extends Node

const SIZE := 1.05

const DIRS := [
    Vector3i(1, -1, 0), Vector3i(1, 0, -1), Vector3i(0, 1, -1),
    Vector3i(-1, 1, 0), Vector3i(-1, 0, 1), Vector3i(0, -1, 1)
]

func to_world(hex: Vector3i) -> Vector3:
    var x = SIZE * 1.5 * hex.x
    var z = SIZE * sqrt(3) * (hex.y + hex.x / 2.0)
    return Vector3(x, 0, z)

func neighbors(hex: Vector3i) -> Array[Vector3i]:
    var result: Array[Vector3i] = []
    for d in DIRS:
        result.append(hex + d)
    return result

func distance(a: Vector3i, b: Vector3i) -> int:
    return (abs(a.x - b.x) + abs(a.y - b.y) + abs(a.z - b.z)) / 2

func in_range(center: Vector3i, radius: int) -> Array[Vector3i]:
    var result: Array[Vector3i] = []
    for q in range(-radius, radius + 1):
        for r in range(max(-radius, -q - radius), min(radius, -q + radius) + 1):
            var s = -q - r
            result.append(center + Vector3i(q, r, s))
    return result

func line(from: Vector3i, dir: Vector3i, length: int) -> Array[Vector3i]:
    var result: Array[Vector3i] = []
    for i in range(1, length + 1):
        result.append(from + dir * i)
    return result
```

---

## Scenes & Scripts

### hex_tile.gd

```gdscript
extends Area3D

@export var coord: Vector3i
@export var walkable := true

var highlighted := false

func _ready():
    add_to_group("tiles")
    input_event.connect(_on_click)

func _on_click(_cam, event, _pos, _normal, _idx):
    if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
        if Game.is_player_turn:
            Game.player.try_move_to(coord)

func set_highlight(on: bool, color := Color.WHITE):
    highlighted = on
    # Set material or modulate
    $MeshInstance3D.get_surface_override_material(0).albedo_color = color if on else Color.WHITE
```

### player.gd

```gdscript
extends Node3D

signal died

@export var max_hp := 3
@export var move_range := 1
@export var damage := 1

var coord: Vector3i
var hp: int

var dash_cooldown := 0
var dash_mode := false
var block_cooldown := 0
var block_active := false

func _ready():
    add_to_group("player")
    hp = max_hp

func start_turn():
    if dash_cooldown > 0:
        dash_cooldown -= 1
    if block_cooldown > 0:
        block_cooldown -= 1
    dash_mode = false

func _input(event):
    if not Game.is_player_turn:
        return
    if event.is_action_pressed("dash"):
        toggle_dash()
    if event.is_action_pressed("block"):
        toggle_block()
    if event.is_action_pressed("rewind"):
        Game.rewind()

func toggle_dash():
    if dash_cooldown > 0:
        return
    dash_mode = not dash_mode

func toggle_block():
    if block_active:
        block_active = false
    elif block_cooldown == 0:
        block_active = true

func try_move_to(target: Vector3i):
    if not Game.is_player_turn:
        return

    var dist = HexGrid.distance(coord, target)

    if dash_mode:
        if dist <= 2 and not Game.is_tile_blocked(target):
            await do_dash(target)
            Game.end_player_turn()
    else:
        if dist <= move_range and not Game.is_tile_blocked(target):
            await do_move(target)
            Game.end_player_turn()

func do_move(target: Vector3i):
    var old_coord = coord

    # Check enemies we're entering range of (they attack us)
    for enemy in Game.enemies:
        if not is_instance_valid(enemy):
            continue
        var dominated_before = enemy.dominates(old_coord)
        var dominated_after = enemy.dominates(target)
        if dominated_after and not dominated_before:
            # Entering new enemy range - they attack
            await enemy.attack(self)
            if hp <= 0:
                return

    # Check if we can counter-attack (already adjacent, staying adjacent)
    for enemy in Game.enemies:
        if not is_instance_valid(enemy):
            continue
        var adj_before = HexGrid.distance(old_coord, enemy.coord) == 1
        var adj_after = HexGrid.distance(target, enemy.coord) == 1
        if adj_before and adj_after:
            await attack(enemy)

    # Move
    coord = target
    var tween = create_tween()
    tween.tween_property(self, "position", HexGrid.to_world(target), 0.25)
    await tween.finished

func do_dash(target: Vector3i):
    dash_mode = false
    dash_cooldown = 4
    coord = target
    var tween = create_tween()
    tween.tween_property(self, "position", HexGrid.to_world(target), 0.15)
    await tween.finished

func attack(target):
    # Lunge animation
    var dir = (target.position - position).normalized()
    var tween = create_tween()
    tween.tween_property(self, "position", position + dir * 0.3, 0.1)
    tween.tween_property(self, "position", position, 0.1)
    await tween.finished

    target.take_damage(damage)

func take_damage(amount: int):
    if block_active:
        block_active = false
        block_cooldown = 3
        return

    hp -= amount
    if hp <= 0:
        died.emit()
```

### enemy.gd (base class)

```gdscript
extends Node3D
class_name Enemy

@export var max_hp := 1
@export var damage := 1
@export var attack_range := 1  # For melee enemies

var coord: Vector3i
var hp: int

func _ready():
    add_to_group("enemies")
    hp = max_hp

# Override in subclasses for different patterns
func get_threat_tiles() -> Array[Vector3i]:
    return HexGrid.neighbors(coord)

func dominates(target_coord: Vector3i) -> bool:
    return target_coord in get_threat_tiles()

func take_turn():
    var player = Game.player
    var dist = HexGrid.distance(coord, player.coord)

    if dominates(player.coord):
        # Player in range - wait (Hoplite style)
        pass
    else:
        # Move toward player
        await move_toward(player.coord)

func move_toward(target: Vector3i):
    var best_tile = coord
    var best_dist = HexGrid.distance(coord, target)

    for neighbor in HexGrid.neighbors(coord):
        if Game.is_tile_blocked(neighbor):
            continue
        var d = HexGrid.distance(neighbor, target)
        if d < best_dist:
            best_dist = d
            best_tile = neighbor

    if best_tile != coord:
        coord = best_tile
        var tween = create_tween()
        tween.tween_property(self, "position", HexGrid.to_world(best_tile), 0.25)
        await tween.finished

func attack(target):
    var dir = (target.position - position).normalized()
    var tween = create_tween()
    tween.tween_property(self, "position", position + dir * 0.3, 0.1)
    tween.tween_property(self, "position", position, 0.1)
    await tween.finished

    target.take_damage(damage)

func take_damage(amount: int):
    hp -= amount
    if hp <= 0:
        Game.remove_enemy(self)
```

### wizard.gd (ranged enemy example)

```gdscript
extends Enemy

func get_threat_tiles() -> Array[Vector3i]:
    # Diagonal lines in all 6 directions, range 2-5
    var tiles: Array[Vector3i] = []
    for dir in HexGrid.DIRS:
        for dist in range(2, 6):
            tiles.append(coord + dir * dist)
    return tiles
```

### sniper.gd (axis enemy example)

```gdscript
extends Enemy

@export var axis := 0  # 0=Q, 1=R, 2=S

func get_threat_tiles() -> Array[Vector3i]:
    var tiles: Array[Vector3i] = []
    var dir1: Vector3i
    var dir2: Vector3i

    match axis:
        0: dir1 = Vector3i(1, -1, 0); dir2 = Vector3i(-1, 1, 0)
        1: dir1 = Vector3i(0, 1, -1); dir2 = Vector3i(0, -1, 1)
        2: dir1 = Vector3i(1, 0, -1); dir2 = Vector3i(-1, 0, 1)

    for dist in range(2, 6):
        tiles.append(coord + dir1 * dist)
        tiles.append(coord + dir2 * dist)

    return tiles
```

---

## Main Scene Setup

```
Main (Node3D)
├── Camera3D
├── DirectionalLight3D
├── Ground (MeshInstance3D)
├── Board (Node3D)          # Parent for tiles
├── Units (Node3D)          # Parent for player & enemies, add to group "units_container"
│   ├── Player (player.tscn)
│   ├── Grunt1 (grunt.tscn)
│   ├── Grunt2 (grunt.tscn)
│   └── Wizard (wizard.tscn)
└── UI (CanvasLayer)
    ├── HealthLabel
    ├── DashButton
    ├── BlockButton
    └── RewindButton
```

### main.gd - Level Setup

```gdscript
extends Node3D

@export var tile_scene: PackedScene
@export var map_size := 5

func _ready():
    generate_grid()
    position_units()
    Game.start_game()

func generate_grid():
    var board = $Board
    for hex in HexGrid.in_range(Vector3i.ZERO, map_size):
        var tile = tile_scene.instantiate()
        tile.coord = hex
        tile.position = HexGrid.to_world(hex)
        board.add_child(tile)

        # Random blocked tiles
        if randf() < 0.15 and hex != Vector3i.ZERO:
            tile.walkable = false
            # Swap to water mesh or whatever

func position_units():
    var player = $Units/Player
    player.coord = Vector3i.ZERO
    player.position = HexGrid.to_world(Vector3i.ZERO)

    # Position enemies randomly
    var spawnable = []
    for tile in get_tree().get_nodes_in_group("tiles"):
        if tile.walkable and HexGrid.distance(tile.coord, Vector3i.ZERO) > 2:
            spawnable.append(tile.coord)
    spawnable.shuffle()

    var i := 0
    for enemy in get_tree().get_nodes_in_group("enemies"):
        if i < spawnable.size():
            enemy.coord = spawnable[i]
            enemy.position = HexGrid.to_world(spawnable[i])
            i += 1
```

---

## UI Script

```gdscript
extends CanvasLayer

@onready var health_label = $HealthLabel
@onready var dash_btn = $DashButton
@onready var block_btn = $BlockButton
@onready var rewind_btn = $RewindButton

func _ready():
    Game.turn_started.connect(_on_turn)
    dash_btn.pressed.connect(func(): Game.player.toggle_dash())
    block_btn.pressed.connect(func(): Game.player.toggle_block())
    rewind_btn.pressed.connect(func(): Game.rewind())

func _process(_delta):
    var p = Game.player
    if not p:
        return

    health_label.text = "HP: %d/%d" % [p.hp, p.max_hp]

    dash_btn.text = "DASH" if p.dash_cooldown == 0 else "DASH (%d)" % p.dash_cooldown
    dash_btn.disabled = p.dash_cooldown > 0

    var block_text = "BLOCK"
    if p.block_active:
        block_text = "BLOCK (ON)"
    elif p.block_cooldown > 0:
        block_text = "BLOCK (%d)" % p.block_cooldown
    block_btn.text = block_text

func _on_turn(unit):
    pass  # Update UI state if needed
```

---

## Input Map (Project Settings → Input Map)

| Action | Key |
|--------|-----|
| dash | D |
| block | B |
| rewind | R |

---

## That's It

This is ~400 lines total vs the ~3000+ lines of C# ECS code. The key simplifications:

1. **No ECS** - Data lives on nodes directly
2. **No component queries** - Use groups and direct references
3. **No systems** - Logic lives on the units themselves or in the Game autoload
4. **No entity factory** - Just instantiate scenes
5. **Simple rewind** - Save a dictionary, restore from it

The game logic is identical, just expressed more directly.

### Build Order

1. Set up autoloads (Game, HexGrid)
2. Create hex_tile.tscn with hex_tile.gd
3. Create player.tscn with player.gd
4. Create enemy scenes (grunt, wizard, sniper) with their scripts
5. Build main.tscn with grid generation
6. Add UI
7. Test and iterate

### What You Lose

- Type safety of C# components
- Clean separation of data/logic
- Ability to easily query "all entities with X and Y components"

### What You Gain

- 80% less code
- Easier to understand
- Standard Godot patterns (others can help you)
- Faster iteration
- No C# build step
