# Multi-Level System with Increasing Difficulty

## Overview

This document outlines a plan for adding a multi-level progression system to Undergang. The system will feature multiple levels with increasing difficulty, victory conditions, level transitions, and player progression.

---

## Phase 1: Core Level Infrastructure

### 1.1 Level Definition Data Structure

Create a new file `src/Lib/LevelDefinition.cs`:

```csharp
public record LevelDefinition
{
    public int LevelNumber { get; init; }
    public string LevelName { get; init; }

    // Map Configuration
    public int MapSize { get; init; }
    public int BlockedTilesCount { get; init; }

    // Enemy Configuration
    public Dictionary<UnitType, int> EnemyCounts { get; init; }
    public int EnemyHealthBonus { get; init; }      // Added to base health
    public int EnemyDamageBonus { get; init; }      // Added to base damage

    // Player Configuration
    public int PlayerStartHealth { get; init; }     // Override for this level
    public Vector3I? PlayerStartPosition { get; init; }  // null = use default

    // Difficulty Modifiers
    public float EnemySpawnExclusionRadius { get; init; }  // Smaller = harder
    public bool AllowRewind { get; init; }          // Disable on hard levels?
    public int RewindCooldown { get; init; }        // Override cooldown
}
```

### 1.2 Level Definitions in Config

Extend `src/Lib/Config.cs` with level definitions:

```csharp
public static class Config
{
    // ... existing config ...

    public static readonly LevelDefinition[] Levels = new[]
    {
        // Level 1: Tutorial
        new LevelDefinition
        {
            LevelNumber = 1,
            LevelName = "The Awakening",
            MapSize = 4,
            BlockedTilesCount = 12,
            EnemyCounts = new() { { UnitType.Grunt, 2 } },
            EnemyHealthBonus = 0,
            EnemyDamageBonus = 0,
            PlayerStartHealth = 5,
            EnemySpawnExclusionRadius = 4,
            AllowRewind = true,
            RewindCooldown = 2
        },

        // Level 2: Introduction to Ranged
        new LevelDefinition
        {
            LevelNumber = 2,
            LevelName = "Distant Threats",
            MapSize = 5,
            BlockedTilesCount = 18,
            EnemyCounts = new() { { UnitType.Grunt, 2 }, { UnitType.Wizard, 1 } },
            EnemyHealthBonus = 0,
            EnemyDamageBonus = 0,
            PlayerStartHealth = 5,
            EnemySpawnExclusionRadius = 3,
            AllowRewind = true,
            RewindCooldown = 3
        },

        // Level 3: Sniper Introduction
        new LevelDefinition
        {
            LevelNumber = 3,
            LevelName = "Line of Sight",
            MapSize = 5,
            BlockedTilesCount = 20,
            EnemyCounts = new()
            {
                { UnitType.Grunt, 2 },
                { UnitType.SniperAxisQ, 1 }
            },
            EnemyHealthBonus = 0,
            EnemyDamageBonus = 0,
            PlayerStartHealth = 4,
            EnemySpawnExclusionRadius = 3,
            AllowRewind = true,
            RewindCooldown = 3
        },

        // Level 4: Mixed Threats
        new LevelDefinition
        {
            LevelNumber = 4,
            LevelName = "Convergence",
            MapSize = 5,
            BlockedTilesCount = 24,
            EnemyCounts = new()
            {
                { UnitType.Grunt, 3 },
                { UnitType.Wizard, 1 },
                { UnitType.SniperAxisR, 1 }
            },
            EnemyHealthBonus = 0,
            EnemyDamageBonus = 0,
            PlayerStartHealth = 4,
            EnemySpawnExclusionRadius = 3,
            AllowRewind = true,
            RewindCooldown = 3
        },

        // Level 5: Tougher Enemies
        new LevelDefinition
        {
            LevelNumber = 5,
            LevelName = "Hardened Foes",
            MapSize = 6,
            BlockedTilesCount = 28,
            EnemyCounts = new()
            {
                { UnitType.Grunt, 4 },
                { UnitType.Wizard, 2 }
            },
            EnemyHealthBonus = 1,  // Enemies have +1 HP
            EnemyDamageBonus = 0,
            PlayerStartHealth = 4,
            EnemySpawnExclusionRadius = 3,
            AllowRewind = true,
            RewindCooldown = 4
        },

        // Level 6: The Gauntlet
        new LevelDefinition
        {
            LevelNumber = 6,
            LevelName = "The Gauntlet",
            MapSize = 6,
            BlockedTilesCount = 30,
            EnemyCounts = new()
            {
                { UnitType.Grunt, 5 },
                { UnitType.Wizard, 2 },
                { UnitType.SniperAxisQ, 1 },
                { UnitType.SniperAxisS, 1 }
            },
            EnemyHealthBonus = 1,
            EnemyDamageBonus = 0,
            PlayerStartHealth = 3,
            EnemySpawnExclusionRadius = 2,
            AllowRewind = true,
            RewindCooldown = 4
        },

        // Level 7: Deadly Force
        new LevelDefinition
        {
            LevelNumber = 7,
            LevelName = "Deadly Force",
            MapSize = 6,
            BlockedTilesCount = 32,
            EnemyCounts = new()
            {
                { UnitType.Grunt, 4 },
                { UnitType.Wizard, 3 },
                { UnitType.SniperAxisR, 2 }
            },
            EnemyHealthBonus = 1,
            EnemyDamageBonus = 1,  // Enemies deal +1 damage
            PlayerStartHealth = 3,
            EnemySpawnExclusionRadius = 2,
            AllowRewind = true,
            RewindCooldown = 5
        },

        // Level 8: Final Stand
        new LevelDefinition
        {
            LevelNumber = 8,
            LevelName = "Final Stand",
            MapSize = 7,
            BlockedTilesCount = 36,
            EnemyCounts = new()
            {
                { UnitType.Grunt, 6 },
                { UnitType.Wizard, 3 },
                { UnitType.SniperAxisQ, 1 },
                { UnitType.SniperAxisR, 1 },
                { UnitType.SniperAxisS, 1 }
            },
            EnemyHealthBonus = 2,
            EnemyDamageBonus = 1,
            PlayerStartHealth = 3,
            EnemySpawnExclusionRadius = 2,
            AllowRewind = false,  // No rewind on final level!
            RewindCooldown = 0
        }
    };

    public static LevelDefinition GetLevel(int levelNumber) =>
        Levels.FirstOrDefault(l => l.LevelNumber == levelNumber)
        ?? Levels[0];
}
```

---

## Phase 2: Level Manager Service

### 2.1 Create LevelManager Service

Create `src/Services/LevelManager.cs`:

```csharp
public class LevelManager
{
    private readonly Entities _entities;
    private readonly Systems _systems;
    private LevelDefinition _currentLevel;
    private int _currentLevelNumber = 1;

    public LevelDefinition CurrentLevel => _currentLevel;
    public int CurrentLevelNumber => _currentLevelNumber;
    public bool IsLastLevel => _currentLevelNumber >= Config.Levels.Length;

    public LevelManager(Entities entities, Systems systems)
    {
        _entities = entities;
        _systems = systems;
    }

    /// <summary>
    /// Load a specific level by number
    /// </summary>
    public void LoadLevel(int levelNumber)
    {
        _currentLevelNumber = levelNumber;
        _currentLevel = Config.GetLevel(levelNumber);

        // Clear existing game state
        ClearCurrentLevel();

        // Create new level
        CreateLevel(_currentLevel);

        // Fire level loaded event
        Events.OnLevelLoaded(_currentLevel);
    }

    /// <summary>
    /// Advance to the next level
    /// </summary>
    public void LoadNextLevel()
    {
        if (!IsLastLevel)
        {
            LoadLevel(_currentLevelNumber + 1);
        }
        else
        {
            Events.OnGameComplete();
        }
    }

    /// <summary>
    /// Restart the current level
    /// </summary>
    public void RestartLevel()
    {
        LoadLevel(_currentLevelNumber);
    }

    private void ClearCurrentLevel()
    {
        // Remove all entities
        var allEntities = _entities.Query<Entity>().ToList();
        foreach (var entity in allEntities)
        {
            // Free Godot nodes
            if (entity.Has<Instance>())
            {
                entity.Get<Instance>().Node?.Free();
            }
            _entities.RemoveEntity(entity);
        }

        // Reset systems that need it
        _systems.Get<GameStateManager>().ClearHistory();
        _systems.Get<TurnSystem>().Reset();
        _systems.Get<TileHighlightSystem>().ClearCache();
    }

    private void CreateLevel(LevelDefinition level)
    {
        // Create grid with level-specific parameters
        _entities.Factory.CreateGrid(
            mapSize: level.MapSize,
            blockedTilesAmt: level.BlockedTilesCount
        );

        // Create player with level-specific health
        var player = _entities.Factory.CreatePlayer();
        player.Update(new Health(level.PlayerStartHealth));

        if (level.PlayerStartPosition.HasValue)
        {
            player.Update(new Coordinate(level.PlayerStartPosition.Value));
        }

        // Create enemies based on level definition
        foreach (var (unitType, count) in level.EnemyCounts)
        {
            for (int i = 0; i < count; i++)
            {
                var enemy = _entities.Factory.CreateEnemy(unitType);

                // Apply difficulty modifiers
                if (level.EnemyHealthBonus > 0)
                {
                    var baseHealth = enemy.Get<Health>().Value;
                    enemy.Update(new Health(baseHealth + level.EnemyHealthBonus));
                }

                if (level.EnemyDamageBonus > 0)
                {
                    var baseDamage = enemy.Get<Damage>().Value;
                    enemy.Update(new Damage(baseDamage + level.EnemyDamageBonus));
                }
            }
        }

        // Configure rewind based on level
        var gameStateManager = _systems.Get<GameStateManager>();
        gameStateManager.SetRewindEnabled(level.AllowRewind);
        gameStateManager.SetCooldown(level.RewindCooldown);
    }
}
```

### 2.2 Level State Component

Add to `src/Components/State.cs`:

```csharp
public readonly record struct CurrentLevel(int LevelNumber);
public readonly record struct LevelComplete;
```

---

## Phase 3: Victory Condition System

### 3.1 Create VictorySystem

Create `src/Systems/VictorySystem.cs`:

```csharp
public class VictorySystem : System
{
    private Entities _entities;
    private LevelManager _levelManager;

    public override void Initialize()
    {
        _entities = Systems.Get<Entities>();
        _levelManager = Systems.Get<LevelManager>();

        // Subscribe to unit defeat event
        Events.UnitDefeated += OnUnitDefeated;
    }

    private void OnUnitDefeated(Entity defeatedUnit)
    {
        // Check if all enemies are defeated
        var remainingEnemies = _entities.Query<Unit, Enemy>().Count();

        if (remainingEnemies == 0)
        {
            // Level complete!
            GD.Print($"Level {_levelManager.CurrentLevelNumber} Complete!");
            Events.OnLevelComplete(_levelManager.CurrentLevel);
        }
    }

    public override void Cleanup()
    {
        Events.UnitDefeated -= OnUnitDefeated;
    }
}
```

### 3.2 Add Victory Events

Extend `src/Services/Events.cs`:

```csharp
// Level Events
public static event Action<LevelDefinition> LevelLoaded;
public static event Action<LevelDefinition> LevelComplete;
public static event Action GameComplete;  // All levels beaten
public static event Action<Entity> UnitDefeated;

public static void OnLevelLoaded(LevelDefinition level) => LevelLoaded?.Invoke(level);
public static void OnLevelComplete(LevelDefinition level) => LevelComplete?.Invoke(level);
public static void OnGameComplete() => GameComplete?.Invoke();
public static void OnUnitDefeated(Entity unit) => UnitDefeated?.Invoke(unit);
```

### 3.3 Update CombatSystem to Fire UnitDefeated

In `src/Systems/CombatSystem.cs`, add to defeat handling:

```csharp
if (defender.Get<Health>().Value <= 0)
{
    // ... existing defeat logic ...

    // Fire unit defeated event
    Events.OnUnitDefeated(defender);
}
```

---

## Phase 4: Level Transition UI

### 4.1 Level Complete Screen

Add to `src/Systems/UISystem.cs`:

```csharp
private Control _levelCompletePanel;
private Label _levelCompleteLabel;
private Button _nextLevelButton;
private Button _restartButton;

private void CreateLevelCompleteUI()
{
    _levelCompletePanel = new Control();
    _levelCompletePanel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    _levelCompletePanel.Visible = false;

    // Semi-transparent background
    var bg = new ColorRect();
    bg.Color = new Color(0, 0, 0, 0.7f);
    bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    _levelCompletePanel.AddChild(bg);

    // Victory text
    _levelCompleteLabel = new Label();
    _levelCompleteLabel.Text = "LEVEL COMPLETE!";
    _levelCompleteLabel.HorizontalAlignment = HorizontalAlignment.Center;
    // ... styling ...

    // Next Level button
    _nextLevelButton = new Button();
    _nextLevelButton.Text = "NEXT LEVEL";
    _nextLevelButton.Pressed += OnNextLevelPressed;

    // Restart button
    _restartButton = new Button();
    _restartButton.Text = "RESTART";
    _restartButton.Pressed += OnRestartPressed;

    // ... layout and add to scene ...
}

private void ShowLevelComplete(LevelDefinition level)
{
    _levelCompleteLabel.Text = $"{level.LevelName}\nCOMPLETE!";
    _nextLevelButton.Visible = !_levelManager.IsLastLevel;
    _levelCompletePanel.Visible = true;
}

private void OnNextLevelPressed()
{
    _levelCompletePanel.Visible = false;
    _levelManager.LoadNextLevel();
}

private void OnRestartPressed()
{
    _levelCompletePanel.Visible = false;
    _levelManager.RestartLevel();
}
```

### 4.2 Level HUD

Add current level display to `UISystem`:

```csharp
private Label _levelLabel;

private void UpdateLevelDisplay()
{
    var level = _levelManager.CurrentLevel;
    _levelLabel.Text = $"Level {level.LevelNumber}: {level.LevelName}";
}
```

### 4.3 Game Complete Screen

```csharp
private void ShowGameComplete()
{
    _levelCompleteLabel.Text = "CONGRATULATIONS!\n\nYou have conquered\nthe Undergang!";
    _nextLevelButton.Visible = false;
    _restartButton.Text = "PLAY AGAIN";
    _levelCompletePanel.Visible = true;
}
```

---

## Phase 5: Game Over & Restart

### 5.1 Update GameManager

Modify `src/Game/GameManager.cs`:

```csharp
private LevelManager _levelManager;

public override void _Ready()
{
    // ... existing setup ...

    _levelManager = new LevelManager(entityManager, _systems);
    _systems.Register(_levelManager);

    // Subscribe to events
    Events.LevelComplete += OnLevelComplete;
    Events.GameComplete += OnGameComplete;
    Events.GameOver += OnGameOver;

    // Start at level 1
    _levelManager.LoadLevel(1);

    _systems.Initialize();
}

private void OnLevelComplete(LevelDefinition level)
{
    GD.Print($"Level {level.LevelNumber} complete!");
    // UI handles the transition
}

private void OnGameComplete()
{
    GD.Print("All levels complete! Game finished!");
}

private void OnGameOver()
{
    GD.Print("Game Over - Player defeated");
    // Show game over UI with restart option
}
```

### 5.2 Restart Functionality

Add restart handling:

```csharp
// In UISystem or GameManager
public void OnRestartPressed()
{
    _levelManager.RestartLevel();
}

public void OnRestartFromBeginning()
{
    _levelManager.LoadLevel(1);
}
```

---

## Phase 6: Difficulty Scaling Refinements

### 6.1 Enemy AI Difficulty Scaling

Extend `EnemySystem` to use level difficulty:

```csharp
// In EnemySystem
private void ExecuteEnemyAction(Entity enemy)
{
    var level = _levelManager.CurrentLevel;

    // Higher level enemies might have smarter AI
    if (level.LevelNumber >= 5)
    {
        // Use advanced pathfinding (avoid player attack range)
        ExecuteAdvancedMovement(enemy);
    }
    else
    {
        // Basic pathfinding
        ExecuteBasicMovement(enemy);
    }
}

private void ExecuteAdvancedMovement(Entity enemy)
{
    // Consider player's dash range
    // Prefer tiles that maximize attack opportunities
    // Coordinate with other enemies
}
```

### 6.2 Map Generation Difficulty

Extend `EntityFactory.CreateGrid` for difficulty-aware generation:

```csharp
public IEnumerable<Entity> CreateGrid(LevelDefinition level)
{
    // Higher levels might have:
    // - More strategic blocked tile placement
    // - Chokepoints near player spawn
    // - Open areas for ranged enemies

    if (level.LevelNumber >= 5)
    {
        return CreateStrategicGrid(level);
    }
    else
    {
        return CreateRandomGrid(level.MapSize, level.BlockedTilesCount);
    }
}
```

### 6.3 Dynamic Difficulty Modifiers

Add optional difficulty curve within levels:

```csharp
public record LevelDefinition
{
    // ... existing properties ...

    // Optional: Difficulty curve within the level
    public int TurnsTillReinforcements { get; init; }  // 0 = no reinforcements
    public Dictionary<UnitType, int> ReinforcementCounts { get; init; }
}
```

---

## Phase 7: Player Progression (Optional Enhancement)

### 7.1 Persistent Player Stats

Create `src/Services/PlayerProgression.cs`:

```csharp
public class PlayerProgression
{
    public int HighestLevelReached { get; private set; } = 1;
    public int TotalEnemiesDefeated { get; private set; } = 0;
    public int TotalLevelsCompleted { get; private set; } = 0;

    // Unlockable abilities
    public bool HasUnlockedDash => TotalLevelsCompleted >= 1;
    public bool HasUnlockedBlock => TotalLevelsCompleted >= 2;
    public bool HasUnlockedRewind => TotalLevelsCompleted >= 3;

    public void OnLevelComplete(int levelNumber)
    {
        TotalLevelsCompleted++;
        if (levelNumber > HighestLevelReached)
        {
            HighestLevelReached = levelNumber;
        }
        Save();
    }

    public void OnEnemyDefeated()
    {
        TotalEnemiesDefeated++;
    }

    private void Save()
    {
        // Save to file or PlayerPrefs equivalent
    }

    public static PlayerProgression Load()
    {
        // Load from file
    }
}
```

### 7.2 Level Select Screen

Add level select UI for replay:

```csharp
private void CreateLevelSelectUI()
{
    for (int i = 0; i < Config.Levels.Length; i++)
    {
        var level = Config.Levels[i];
        var button = new Button();
        button.Text = $"Level {level.LevelNumber}: {level.LevelName}";

        // Lock levels player hasn't reached
        bool unlocked = _progression.HighestLevelReached >= level.LevelNumber;
        button.Disabled = !unlocked;

        int levelNum = level.LevelNumber;  // Capture for lambda
        button.Pressed += () => _levelManager.LoadLevel(levelNum);

        _levelSelectContainer.AddChild(button);
    }
}
```

---

## Implementation Order

### MVP (Minimum Viable Product)

1. **Phase 1**: LevelDefinition data structure + Config levels
2. **Phase 2**: LevelManager service (load/clear/create)
3. **Phase 3**: VictorySystem + Events
4. **Phase 4**: Basic level complete UI
5. **Phase 5**: Game over restart

### Polish

6. **Phase 4 continued**: Full UI with transitions
7. **Phase 6**: AI difficulty scaling
8. **Phase 7**: Player progression (optional)

---

## File Changes Summary

### New Files
- `src/Lib/LevelDefinition.cs` - Level data structure
- `src/Services/LevelManager.cs` - Level loading/management
- `src/Systems/VictorySystem.cs` - Win condition checking
- `src/Services/PlayerProgression.cs` (optional) - Persistent progression

### Modified Files
- `src/Lib/Config.cs` - Add level definitions array
- `src/Components/State.cs` - Add CurrentLevel, LevelComplete components
- `src/Services/Events.cs` - Add level events
- `src/Game/GameManager.cs` - Integrate LevelManager, remove hardcoded setup
- `src/Systems/CombatSystem.cs` - Fire UnitDefeated event
- `src/Systems/UISystem.cs` - Add level UI, complete screen, restart
- `src/Systems/EnemySystem.cs` - Optional AI scaling
- `src/Services/EntityFactory.cs` - Accept LevelDefinition parameter
- `src/Services/GameStateManager.cs` - Add SetRewindEnabled, SetCooldown methods

---

## Testing Checklist

- [ ] Level 1 loads correctly with correct enemy count
- [ ] Player health matches level definition
- [ ] Enemies spawn with correct stats (health + bonuses, damage + bonuses)
- [ ] Victory triggers when all enemies defeated
- [ ] Level complete UI appears
- [ ] Next level button loads next level
- [ ] Restart button reloads current level
- [ ] Game over allows restart
- [ ] All 8 levels playable
- [ ] Final level shows game complete
- [ ] Rewind disabled on levels where specified
- [ ] Map size changes per level
- [ ] Blocked tile count changes per level
- [ ] Game state properly cleared between levels

---

## Difficulty Curve Rationale

| Level | Enemies | Enemy HP | Enemy Dmg | Player HP | Notes |
|-------|---------|----------|-----------|-----------|-------|
| 1 | 2 Grunts | 1 | 1 | 5 | Tutorial: Learn movement/combat |
| 2 | 2 Grunts, 1 Wizard | 1 | 1 | 5 | Learn ranged threats |
| 3 | 2 Grunts, 1 Sniper | 1 | 1 | 4 | Learn axis threats |
| 4 | 3 Grunts, 1 Wizard, 1 Sniper | 1 | 1 | 4 | Mixed threats |
| 5 | 4 Grunts, 2 Wizards | 2 | 1 | 4 | Tougher enemies |
| 6 | 5 Grunts, 2 Wizards, 2 Snipers | 2 | 1 | 3 | The gauntlet |
| 7 | 4 Grunts, 3 Wizards, 2 Snipers | 2 | 2 | 3 | Deadly damage |
| 8 | 6 Grunts, 3 Wizards, 3 Snipers | 3 | 2 | 3 | Final boss level, no rewind |

Each level introduces a new challenge:
1. Basic combat
2. Ranged enemies (diagonal)
3. Axis-aligned snipers
4. Multiple threat types
5. Enemies survive first hit
6. Outnumbered
7. Enemies hit harder
8. Everything combined, no safety net

---

## Future Enhancements

- **Boss enemies**: Special unit with unique mechanics on certain levels
- **Environmental hazards**: Tiles that damage units, teleporters, etc.
- **Objectives**: "Reach the exit tile" or "Survive X turns"
- **Endless mode**: Procedurally generated levels with scaling difficulty
- **Daily challenge**: Fixed seed levels with leaderboards
- **Ability unlocks**: New abilities gained through progression
- **Equipment/upgrades**: Persistent improvements between levels
