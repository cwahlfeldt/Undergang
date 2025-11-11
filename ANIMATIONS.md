# Animation System Documentation

## Overview

The game now has a comprehensive animation system ready to integrate unit-specific animations. Animations are automatically triggered based on unit state and actions.

**Target Platform: Mixamo Rigged Characters**
This system is designed to work seamlessly with Mixamo humanoid-rigged characters and animations. The architecture supports easy swapping of placeholder assets with production Mixamo FBX/GLB files.

## Animation States

Units can be in the following animation states (defined in `AnimationState` enum):

- **Idle** - Default resting state
- **Move** - Playing while unit is moving
- **Attack** - Playing when unit attacks
- **Hurt** - Playing when unit takes damage
- **Die** - Playing when unit is defeated
- **Spawn** - Initial spawn animation (optional)
- **Victory** - Win celebration (optional)
- **Defeat** - Loss animation (optional)

## Architecture

### Components
- **CurrentAnimation** - Tracks the current animation state of a unit
- **AnimationPlayer** - Stores reference to the Godot AnimationPlayer node

### Systems
- **AnimationSystem** - Manages animation state changes and playback
- Automatically integrated with:
  - **MovementSystem** - Triggers Move → Idle
  - **CombatSystem** - Triggers Attack/Hurt animations
  - **Unit defeat events** - Triggers Die animation

## Adding Animations to Unit Scenes

### Step 1: Add AnimationPlayer Node

In Godot editor, for each unit scene (Player.tscn, Grunt.tscn, Sniper.tscn):

1. Open the scene
2. Add an `AnimationPlayer` node as a child of the root node
3. Name it exactly `AnimationPlayer`

### Step 2: Create Animations

Animation naming convention: `{UnitType}_{AnimationState}`

**Per-unit animations (recommended):**
- `Player_Idle`
- `Player_Move`
- `Player_Attack`
- `Player_Hurt`
- `Player_Die`
- `Grunt_Idle`
- `Grunt_Move`
- `Grunt_Attack`
- `Grunt_Hurt`
- `Grunt_Die`
- `Sniper_Idle`
- `Sniper_Move`
- `Sniper_Attack`
- `Sniper_Hurt`
- `Sniper_Die`

**Generic fallback (optional):**
If a unit-specific animation doesn't exist, the system will look for:
- `Idle`
- `Move`
- `Attack`
- `Hurt`
- `Die`

### Step 3: Animation Properties

Animations can modify:
- Position/Rotation/Scale of mesh nodes
- Material properties
- Visibility
- Particle effects
- Sound effects (via AudioStreamPlayer)

**Example Move Animation:**
```
Track: Mesh/position
- 0.0s: (0, 0.7, 0)
- 0.1s: (0, 0.85, 0)  # Slight hop
- 0.2s: (0, 0.7, 0)   # Return to ground
```

**Example Attack Animation:**
```
Track: Mesh/rotation_degrees
- 0.0s: (0, 0, 0)
- 0.1s: (0, 0, -15)    # Wind up
- 0.15s: (0, 0, 15)    # Strike
- 0.25s: (0, 0, 0)     # Return
```

### Step 4: Animation Settings

- **Idle, Move**: Should loop
- **Attack, Hurt**: Should NOT loop
- **Die**: Should NOT loop, hold on last frame

## How It Works

### Automatic Triggers

The system automatically handles animation transitions:

1. **Movement**:
   ```
   Player clicks tile → Move animation starts → Unit moves → Idle animation
   ```

2. **Combat**:
   ```
   Unit enters range → Attack animation (attacker) + Hurt animation (defender) → Both return to Idle
   ```

3. **Defeat**:
   ```
   Health reaches 0 → Die animation → Unit removed from game
   ```

### Manual Triggers (for custom systems)

```csharp
var animationSystem = Systems.Get<AnimationSystem>();
animationSystem.SetAnimationState(unit, AnimationState.Attack);
```

## Current Behavior Without Animations

The system is **fully functional without animations**:
- If `AnimationPlayer` node doesn't exist → No error, just skips animation
- If animation name doesn't exist → Logs message, continues gameplay
- Tweener system still provides basic movement interpolation

## Testing

To test if animations are working:

1. Add an AnimationPlayer to any unit scene
2. Create a simple `{UnitType}_Idle` animation (e.g., gentle bobbing)
3. Run the game - unit should play idle animation immediately
4. Move the unit - should transition to Move then back to Idle
5. Engage in combat - should see Attack/Hurt animations

## Animation Timing

The system waits for animations to complete:
- **Attack animations**: CombatSystem waits for attack animation duration
- **Movement**: MovementSystem waits for Tweener movement, then transitions to Idle
- **Death**: Unit removal happens after Die animation starts

## Extending the System

### Adding New Animation States

1. Add to `AnimationState` enum in `src/Lib/Enums/AnimationState.cs`
2. Trigger via `AnimationSystem.SetAnimationState()`
3. Create corresponding animations in unit scenes

### Per-Enemy-Type Variations

Each enemy type automatically gets its own animation set:
```
Grunt_Attack  - Quick punch
Sniper_Attack - Ranged shot animation
```

This system is designed to be **future-proof** and **easy to extend** as animation assets become available.

---

## Mixamo Integration Guide

### Workflow: From Mixamo to Game

#### 1. Download Character from Mixamo

1. Go to [Mixamo](https://www.mixamo.com/)
2. Choose a character (e.g., "Y Bot", "X Bot")
3. Download with settings:
   - **Format**: FBX for Unity (.fbx)
   - **Skin**: With Skin
   - **Frames per second**: 30
   - **Download character once** (this is your base model)

#### 2. Download Animations from Mixamo

For each unit type (Player, Grunt, Sniper), download these animations:

**Required Animations:**
- **Idle**: "Breathing Idle", "Standing Idle", "Rifle Idle"
- **Move**: "Walking", "Running", "Rifle Run"
- **Attack**:
  - Melee: "Punch", "Sword Slash", "Kick"
  - Ranged: "Rifle Shoot", "Pistol Shoot", "Aiming"
- **Hurt**: "Hit Reaction", "React", "Staggered"
- **Die**: "Death", "Dying", "Fall Back"

Download settings for animations:
- **Format**: FBX for Unity (.fbx)
- **Skin**: Without Skin (animation only)
- **Frames per second**: 30
- **Download each animation separately**

#### 3. Import into Godot

**File Structure:**
```
assets/models/
  ├── characters/
  │   ├── player.fbx (character with skin)
  │   ├── grunt.fbx
  │   └── sniper.fbx
  └── animations/
      ├── player/
      │   ├── player_idle.fbx
      │   ├── player_move.fbx
      │   ├── player_attack.fbx
      │   ├── player_hurt.fbx
      │   └── player_die.fbx
      ├── grunt/
      │   ├── grunt_idle.fbx
      │   ├── grunt_move.fbx
      │   └── ...
      └── sniper/
          ├── sniper_idle.fbx
          └── ...
```

**Import Steps:**
1. Drag FBX files into Godot's `assets/` folder
2. Godot will auto-import as GLB scenes
3. For characters: Right-click → "Edit Import" → Make sure "Root Type" is "AnimationTree" compatible

#### 4. Setup Unit Scene with Mixamo Character

**For each unit (e.g., Player.tscn):**

1. **Replace Visual Mesh**:
   - Remove current mesh/visual node
   - Instance the Mixamo character scene (e.g., `player.glb`)
   - Position and scale as needed (Mixamo models are usually large)

2. **Add AnimationPlayer**:
   - Mixamo GLB imports come with AnimationPlayer
   - Or add one as child: `Add Node` → `AnimationPlayer`

3. **Import Animations**:
   ```
   Option A: Import animations into existing AnimationPlayer
   - Open AnimationPlayer panel
   - Click "Animation" dropdown → "Manage Animations"
   - Import each .fbx animation
   - Rename to match system: "Player_Idle", "Player_Move", etc.

   Option B: Use Animation Library
   - Create AnimationLibrary resource
   - Add all animations to library
   - Link to AnimationPlayer
   ```

4. **Scale Adjustment**:
   ```
   Typical Mixamo character scale in Godot:
   Scale: (0.01, 0.01, 0.01)  # Mixamo models are cm-based
   Position: (0, 0, 0)
   Rotation: (0, 180, 0)      # May need to face forward
   ```

#### 5. Animation Naming Convention

The system expects animations named: `{UnitType}_{AnimationState}`

**Mapping Mixamo animations:**
```
Mixamo Animation → Game Animation Name
----------------------------------------
"Idle" → "Player_Idle"
"Walking" → "Player_Move"
"Rifle Shoot" → "Player_Attack"
"Hit Reaction" → "Player_Hurt"
"Dying" → "Player_Die"

"Idle" → "Grunt_Idle"
"Punching" → "Grunt_Attack"
etc.
```

**Renaming in Godot:**
1. Select AnimationPlayer node
2. In Animation panel, click animation dropdown
3. Right-click animation → Rename
4. Follow naming convention

#### 6. Animation Loop Settings

After importing, configure loop settings:

**In AnimationPlayer panel:**
- **Idle**: Loop = On
- **Move**: Loop = On
- **Attack**: Loop = Off
- **Hurt**: Loop = Off
- **Die**: Loop = Off

### Mixamo Animation Recommendations

**Per Unit Type:**

**Player (Hero Character):**
- Character: "Y Bot" or heroic character
- Idle: "Breathing Idle"
- Move: "Running" (faster-paced)
- Attack: "Sword Slash" or "Punch"
- Hurt: "Hit Reaction Front"
- Die: "Death from Front"

**Grunt (Basic Enemy):**
- Character: Same rig, different skin/color
- Idle: "Standing Idle"
- Move: "Walking"
- Attack: "Punching"
- Hurt: "Hit Reaction"
- Die: "Dying"

**Sniper (Ranged Enemy):**
- Character: Same rig, different equipment
- Idle: "Rifle Idle"
- Move: "Rifle Run"
- Attack: "Rifle Shoot" or "Aiming"
- Hurt: "Hit Reaction"
- Die: "Death from Front"

### Placeholder Strategy

**During Development:**

Option 1: **No Animation (Current)**
- System gracefully handles missing animations
- Game uses Tweener for basic movement
- Ready to plug in Mixamo when available

Option 2: **Simple Placeholder Animations**
- Create basic bobbing/rotation animations manually
- Replace with Mixamo later without code changes

Option 3: **Mixamo Free Tier**
- Download 1-2 basic animations now
- Test the pipeline
- Add full animation set later

### Testing Mixamo Integration

1. **Download one Mixamo character + Idle animation**
2. **Import into Godot** (`assets/models/test_character.fbx`)
3. **Create test scene**:
   ```
   TestUnit.tscn:
   - Area3D (root)
     - Imported Mixamo model (scaled 0.01)
     - AnimationPlayer
       - "Player_Idle" animation
   ```
4. **Temporarily swap in GameManager**:
   ```csharp
   // Test: Replace Player.tscn with TestUnit.tscn in RenderSystem
   ```
5. **Run game** → Should see Mixamo character with working idle animation

### Common Mixamo Issues & Solutions

**Issue: Character appears huge**
- Solution: Scale to (0.01, 0.01, 0.01) - Mixamo uses centimeters

**Issue: Character facing wrong direction**
- Solution: Rotate Y-axis 180° or adjust in import settings

**Issue: Animations don't play**
- Solution: Check animation names match exactly: `{UnitType}_{State}`

**Issue: Animations look jerky**
- Solution: Import FPS should match (30 FPS recommended)

**Issue: Multiple AnimationPlayers**
- Solution: Imported GLB may have AnimationPlayer - use that one or merge animations

**Issue: Skeleton doesn't match**
- Solution: All animations must use same Mixamo character skeleton

### Future Production Notes

**When ready for final assets:**

1. Download full animation sets from Mixamo (per unit type)
2. Organize into `/assets/animations/{unitType}/` folders
3. Import all FBX files
4. Batch rename animations following convention
5. Update unit scenes to use new characters
6. Test each unit type individually
7. Polish: Add transition blending via AnimationTree (optional)

**AnimationTree Enhancement (Future):**
For smoother transitions between states:
- Replace AnimationPlayer with AnimationTree
- Create blend tree for smooth Idle ↔ Move transitions
- System code remains same, just change scene structure

This workflow ensures **zero code changes** when swapping from placeholder to Mixamo animations!
