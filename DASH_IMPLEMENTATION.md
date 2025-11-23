# Dash Mechanic Implementation

## Overview
This document describes the implementation of the dash mechanic for Undergang, allowing players to quickly move 2 tiles in a straight line with a cooldown system.

## Features Implemented

### 1. Dash Ability
- **Range**: Player can dash exactly 2 tiles in any of the 6 hex directions
- **Mechanics**: Bypasses pathfinding, ignores occupied tiles (can dash through enemies)
- **Restriction**: Destination tile must be traversable
- **Animation**: Fast movement (0.25s vs normal 0.5s+)

### 2. Cooldown System
- **Initial State**: Dash is available at game start
- **Cooldown**: 3 movement actions
- **Reduction**: Cooldown decreases by 1 each time player moves (normal movement)
- **Visual Feedback**: UI button shows cooldown state

### 3. UI Components
- **Dash Button**:
  - Located below health hearts (top-left)
  - Shows "DASH" when ready
  - Shows "DASH (ACTIVE)" when dash mode is activated
  - Disabled and greyed out when on cooldown
  - Green tint when dash mode is active
- **Cooldown Label**:
  - Displays "Cooldown: X" turns remaining
  - Hidden when dash is ready

### 4. Visual Feedback
- **Dash Range Highlighting**:
  - Blue material (HexTileDashRange.tres)
  - Highlights all 6 valid dash tiles when button is clicked
  - Clears when dash is used or mode is toggled off
- **Mode Toggle**:
  - Click button to enter dash mode (shows range)
  - Click again to exit dash mode
  - Automatically exits after dash is used

## How to Use

1. **Activate Dash Mode**: Click the "DASH" button (if available)
2. **View Range**: 6 tiles at distance 2 will be highlighted in blue
3. **Execute Dash**: Click on a highlighted tile to dash to it
4. **Cooldown**: Wait 3 movement actions before dash is available again

## Technical Implementation

### New Components (Components.cs)
```csharp
- Dash(Vector3I From, Vector3I To) - Marks entity as performing dash
- DashReady - Marker that dash ability is available
- AbilityCooldown(int TurnsRemaining) - Tracks turns until ability is ready
- DashMode - Marker that player is in dash selection mode
```

### New Systems
1. **DashSystem.cs**
   - Processes dash movement
   - Fast animation (0.25s)
   - Validates destination
   - Fires DashCompleted event

2. **CooldownSystem.cs**
   - Manages ability cooldowns
   - Reduces cooldown on player movement
   - Activates cooldown when dash is used
   - Sets initial DashReady state

### Modified Systems
1. **PlayerSystem.cs**
   - Added dash mode detection
   - Added `IsValidDashTarget()` validation
   - Creates Dash component instead of Movement when in dash mode

2. **TileHighlightSystem.cs**
   - Added dash range highlighting
   - Shows 6 tiles at distance 2 in blue
   - Listens to DashModeToggled event

3. **UISystem.cs**
   - Added dash button and cooldown label
   - Updates button state based on cooldown
   - Handles dash mode toggling

4. **AnimationSystem.cs**
   - Added DashCompleted event handler
   - Returns unit to Idle after dash

### New Events (Events.cs)
```csharp
- DashCompleted(Entity, Vector3I from, Vector3I to)
- DashModeToggled()
```

### New Materials
- **HexTileDashRange.tres**: Blue transparent material for dash tiles

## Combat Interaction
- Dashing does NOT trigger enemy reactive attacks (you're too fast!)
- Dash allows safe repositioning through enemy lines
- Creates tactical depth for escaping or aggressive positioning

## System Registration Order (GameManager.cs)
```csharp
RenderSystem
AnimationSystem
UISystem
TurnSystem
CooldownSystem      // NEW - Before PlayerSystem
EnemySystem
PlayerSystem
RangeSystem
MovementSystem
DashSystem          // NEW - After MovementSystem
CombatSystem
```

## File Changes Summary

### New Files
- `src/Systems/DashSystem.cs`
- `src/Systems/CooldownSystem.cs`
- `assets/materials/HexTileDashRange.tres`

### Modified Files
- `src/Components/Components.cs` - Added 4 new components
- `src/Services/Events.cs` - Added 2 new events
- `src/Systems/PlayerSystem.cs` - Dash initiation logic
- `src/Systems/TileHighlightSystem.cs` - Dash range visualization
- `src/Systems/UISystem.cs` - Dash button and cooldown UI
- `src/Systems/AnimationSystem.cs` - DashCompleted handler
- `src/Services/PathFinder.cs` - DashCompleted handler
- `src/Game/GameManager.cs` - System registration

## Testing Checklist

When testing in Godot:

1. ✓ Dash button appears in top-left UI
2. ✓ Dash is ready at game start (button enabled, white)
3. ✓ Clicking dash button shows 6 blue tiles at distance 2
4. ✓ Clicking dash button again toggles off dash mode
5. ✓ Clicking a blue tile executes dash (fast animation)
6. ✓ After dash, button shows cooldown (greyed, "Cooldown: 3")
7. ✓ Each normal movement reduces cooldown by 1
8. ✓ After 3 movements, dash becomes ready again
9. ✓ Dash works in all 6 hex directions
10. ✓ Dash cannot target non-traversable tiles
11. ✓ Dashing through enemy range does NOT trigger attack
12. ✓ Dash mode clears after executing dash
13. ✓ Button shows "DASH (ACTIVE)" with green tint in dash mode

## Future Enhancements

Potential improvements:
- Add special dash animation state (vs reusing Move)
- Sound effects for dash
- Particle trail during dash
- Different cooldowns for different abilities
- More abilities using the cooldown system
- Dash upgrade (longer range, shorter cooldown)
- Enemy units with dash ability
