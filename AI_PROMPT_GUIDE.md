# AI Prompt Guide for Undergang Development

Quick reference for prompting AI to work with this codebase.

---

## Table of Contents

1. [Component Prompts](#component-prompts)
2. [System Prompts](#system-prompts)
3. [Unit Prompts](#unit-prompts)
4. [Feature Prompts](#feature-prompts)
5. [Debugging Prompts](#debugging-prompts)
6. [Refactoring Prompts](#refactoring-prompts)
7. [Architecture Prompts](#architecture-prompts)

---

## Component Prompts

### Creating a New Component

**Template:**
```
Create a [Name]Component with:
- [Property1]: [Type] (default: [value], [exported/private])
- [Property2]: [Type] (default: [value])
- Signal [SignalName] that fires when [condition]
- Method [MethodName]([params]) that [description]

Place in: src/Components/[Name]Component.cs
Follow the IComponent interface pattern
```

**Examples:**

**Stamina Component:**
```
Create a StaminaComponent with:
- MaxStamina: int (default: 100, exported)
- RegenPerTurn: int (default: 10, exported)
- Current: int (private, tracks current stamina)
- Signal StaminaDepleted fires when stamina reaches 0
- Signal StaminaChanged(int current, int max) fires on any change
- Method UseStamina(int amount) returns bool (true if enough stamina)
- Method Regenerate() adds RegenPerTurn to current

Place in: src/Components/StaminaComponent.cs
```

**Armor Component:**
```
Create an ArmorComponent with:
- ArmorValue: int (default: 0, exported)
- ArmorType: ArmorType enum (Light, Medium, Heavy)
- Method CalculateDamageReduction(int damage) returns int
- Light armor: 10% reduction
- Medium armor: 25% reduction
- Heavy armor: 50% reduction

Place in: src/Components/ArmorComponent.cs
```

**Buff Component:**
```
Create a BuffComponent with:
- BuffType: BuffType enum (Attack, Defense, Speed)
- Duration: int (turns remaining, exported)
- Multiplier: float (default: 1.5, exported)
- Signal BuffExpired fires when duration reaches 0
- Method DecrementDuration() called each turn
- Method IsActive() returns bool

Place in: src/Components/BuffComponent.cs
```

---

## System Prompts

### Creating a New System

**Template:**
```
Create a [Name]System that:
- Processes units with [Component1], [Component2]
- Runs on [turn start/turn end/specific event]
- Emits signal [SignalName] when [condition]
- Interacts with [OtherSystem] to [purpose]

Dependencies:
- [System1]: [why]
- [System2]: [why]

Place in: src/Systems/[Name]System.cs
Use [AutoRegister] attribute for auto-registration
```

**Examples:**

**Poison System:**
```
Create a PoisonSystem that:
- Processes units with PoisonComponent
- Runs on turn end
- Applies poison damage each turn
- Decrements poison duration
- Removes poison when duration reaches 0
- Emits signal PoisonApplied(Unit unit, int damage)
- Emits signal PoisonExpired(Unit unit)

Dependencies:
- HealthComponent: to apply damage
- TurnManager: to subscribe to TurnEnded event

Place in: src/Systems/PoisonSystem.cs
Use [AutoRegister] attribute
```

**Ability System:**
```
Create an AbilitySystem that:
- Processes units with AbilityComponent
- Handles ability activation on player input
- Checks cooldowns and costs (stamina/mana)
- Executes ability effects (damage, heal, buff)
- Updates cooldowns each turn
- Emits signal AbilityUsed(Unit caster, Ability ability, Vector3I target)
- Emits signal AbilityCooldownReady(Unit unit, Ability ability)

Dependencies:
- StaminaComponent: to check/consume stamina
- TurnManager: to update cooldowns
- RangeSystem: to validate ability range

Place in: src/Systems/AbilitySystem.cs
Use [AutoRegister] attribute
```

**Loot System:**
```
Create a LootSystem that:
- Spawns loot when enemies die
- Subscribes to HealthComponent.Died signal on all enemies
- Drops items based on enemy type
- Emits signal LootDropped(Vector3I position, Item item)
- Handles player pickup on movement
- Emits signal LootCollected(Player player, Item item)

Dependencies:
- UnitManager: to query units
- MovementSystem: to detect player movement over loot

Place in: src/Systems/LootSystem.cs
Use [AutoRegister] attribute
```

---

## Unit Prompts

### Creating a New Unit Type

**Template:**
```
Create a [Name] unit (extends [Unit/Player/Enemy]) with:

Components:
- [Component1]: configured to [value]
- [Component2]: configured to [value]

Stats:
- Health: [value]
- Damage: [value]
- [Other]: [value]

Special behavior:
- [Description of unique behavior]
- Override [method] to [custom logic]

Place in: src/Units/[Name].cs
```

**Examples:**

**Boss Unit:**
```
Create a Boss unit (extends Enemy) with:

Components:
- ShieldComponent: MaxShield = 20, RegenPerTurn = 2
- EnrageComponent: Threshold = 0.3 (30% health)
- RangeComponent: Pattern = Circle, Distance = 2

Stats:
- Health: 50
- Damage: 10

Special behavior:
- When enraged (below 30% health), damage doubles
- Immune to knockback
- Override OnDied() to spawn loot and play special animation

Place in: src/Units/Boss.cs
```

**Healer Unit:**
```
Create a Healer unit (extends Enemy) with:

Components:
- HealComponent: HealAmount = 5, HealRange = 2
- AIComponent: Behavior = Defensive

Stats:
- Health: 8
- Damage: 1

Special behavior:
- On its turn, heals nearby allies instead of moving
- Only moves if no allies in range
- Prioritizes lowest health ally

Place in: src/Units/Healer.cs
```

**Tank Unit:**
```
Create a Tank unit (extends Player/Enemy) with:

Components:
- ArmorComponent: ArmorValue = 10, Type = Heavy
- TauntComponent: Range = 3

Stats:
- Health: 30
- Damage: 3

Special behavior:
- Reduced movement range (2 instead of 3)
- Enemies within taunt range prioritize attacking tank
- Takes 50% reduced damage

Place in: src/Units/Tank.cs
```

---

## Feature Prompts

### Adding a Complete Feature

**Template:**
```
Add [Feature Name]:

Requirements:
1. [Component needs]
2. [System needs]
3. [UI needs]
4. [Unit modifications]

Behavior:
- [How it works]
- [When it triggers]
- [What it affects]

Integration:
- Modify [ExistingSystem] to [change]
- Connect to [ExistingComponent] via [signal/method]
```

**Examples:**

**Critical Hits:**
```
Add critical hit system:

Requirements:
1. CriticalHitComponent with:
   - CritChance: float (0.0 to 1.0)
   - CritMultiplier: float (default 2.0)

2. Modify CombatSystem to:
   - Check for critical hit before applying damage
   - Roll random number against CritChance
   - Multiply damage by CritMultiplier on crit
   - Emit signal CriticalHit(Unit attacker, Unit defender, int damage)

3. Add visual feedback:
   - Different damage numbers color (red for crit)
   - Larger text scale
   - Screen shake on crit

Behavior:
- On each attack, roll for crit
- If successful, multiply damage
- Show visual feedback

Integration:
- Add CriticalHitComponent to Player (20% chance)
- Add to some enemy types
- UISystem listens to CriticalHit signal for visual effects
```

**Elemental Damage:**
```
Add elemental damage system:

Requirements:
1. ElementComponent with:
   - ElementType: enum (Fire, Ice, Lightning, None)
   - ElementDamage: int

2. ResistanceComponent with:
   - Resistances: Dictionary<ElementType, float>

3. ElementalSystem that:
   - Calculates elemental damage
   - Applies resistance modifiers
   - Handles elemental effects (burn, freeze, stun)

4. Modify CombatSystem to:
   - Query ElementComponent on attacker
   - Query ResistanceComponent on defender
   - Call ElementalSystem.CalculateDamage()
   - Apply elemental effects

Behavior:
- Fire: deals damage over time (3 turns)
- Ice: slows movement (half range for 2 turns)
- Lightning: stuns target (skip next turn)

Integration:
- Add to specific weapons/units
- Boss has resistances
- Visual effects per element type
```

**Inventory System:**
```
Add inventory and equipment:

Requirements:
1. InventoryComponent with:
   - Items: List<Item>
   - MaxSlots: int
   - Methods: AddItem(), RemoveItem(), HasItem()

2. EquipmentComponent with:
   - Weapon: WeaponItem
   - Armor: ArmorItem
   - Accessory: AccessoryItem
   - Methods: Equip(), Unequip()

3. Item class hierarchy:
   - Item (base)
   - WeaponItem (adds damage)
   - ArmorItem (adds defense)
   - ConsumableItem (health potion, etc.)

4. InventorySystem that:
   - Handles item pickup
   - Manages equipment changes
   - Updates stats when equipment changes

5. UI:
   - Inventory grid
   - Equipment slots
   - Item tooltips

Behavior:
- Items dropped by enemies (LootSystem)
- Player picks up by moving over
- Equipment affects stats immediately
- Consumables used from inventory

Integration:
- Modify DamageComponent to read from equipped weapon
- Modify ArmorComponent to read from equipped armor
- LootSystem drops items
- UISystem shows inventory panel
```

---

## Debugging Prompts

### Finding Issues

**Template:**
```
Debug: [Issue description]

Expected behavior:
- [What should happen]

Actual behavior:
- [What is happening]

Relevant files:
- [File1]
- [File2]

Check:
1. [Specific thing to check]
2. [Another thing to check]
```

**Examples:**

**Shield Not Regenerating:**
```
Debug: Shield not regenerating each turn

Expected behavior:
- Shield should regenerate 1 point per turn
- Should regenerate for all units with ShieldComponent

Actual behavior:
- Shield never regenerates
- Stays at 0 after taking damage

Relevant files:
- src/Systems/ShieldRegenSystem.cs
- src/Components/ShieldComponent.cs
- src/Core/GameWorld.cs

Check:
1. Is ShieldRegenSystem registered in GameWorld?
2. Is TurnEnded signal connected properly?
3. Is Regenerate() method being called?
4. Add debug prints to ShieldRegenSystem.OnTurnEnded()
5. Check if ShieldComponent.Current setter has correct clamp logic
```

**Player Can't Move:**
```
Debug: Player cannot move when clicking tiles

Expected behavior:
- Click tile → player moves there
- Movement highlights visible tiles

Actual behavior:
- Nothing happens on click
- No highlights shown

Relevant files:
- src/Systems/InputSystem.cs
- src/Systems/MovementSystem.cs
- src/Systems/TileHighlightSystem.cs

Check:
1. Is InputSystem receiving mouse clicks? (add debug print)
2. Is raycast hitting the board?
3. Is MovementSystem.MoveUnit being called?
4. Is TurnManager.CurrentUnit == player?
5. Are there errors in console?
6. Is pathfinding returning valid path?
```

**Combat Not Triggering:**
```
Debug: Combat doesn't trigger when player enters enemy range

Expected behavior:
- Player moves into enemy threat zone
- Enemy attacks player
- Combat animation plays

Actual behavior:
- Player moves without combat
- No attack animation
- No damage dealt

Relevant files:
- src/Systems/MovementSystem.cs
- src/Systems/CombatSystem.cs
- src/Systems/RangeSystem.cs

Check:
1. Is RangeSystem updating threat map?
2. Print threat map contents in RangeSystem.UpdateThreatMap()
3. Is MovementSystem checking GetThreateningUnit()?
4. Is CombatSystem.ExecuteCombat being called?
5. Are Hoplite rules implemented correctly?
6. Is combat trigger checking old vs new position correctly?
```

---

## Refactoring Prompts

### Improving Code Structure

**Template:**
```
Refactor: [What to refactor]

Current problem:
- [Issue with current code]

Desired outcome:
- [How it should be]

Steps:
1. [Step 1]
2. [Step 2]
3. [Step 3]

Affected files:
- [File1]
- [File2]
```

**Examples:**

**Extract Damage Calculation:**
```
Refactor: Extract damage calculation to DamageCalculator

Current problem:
- Damage calculation scattered across CombatSystem
- Hard to add armor, buffs, critical hits
- No single source of truth

Desired outcome:
- Single DamageCalculator class
- Handles all damage modifiers
- Easy to add new modifiers
- Returns final damage value

Steps:
1. Create src/Services/DamageCalculator.cs
2. Add method: int Calculate(Unit attacker, Unit defender)
3. Move damage logic from CombatSystem
4. Check for ArmorComponent
5. Check for BuffComponent
6. Check for CriticalHitComponent
7. Apply all modifiers
8. Modify CombatSystem to use DamageCalculator

Affected files:
- src/Systems/CombatSystem.cs
- src/Services/DamageCalculator.cs (new)
```

**Simplify Unit Creation:**
```
Refactor: Use builder pattern for unit creation

Current problem:
- UnitFactory has repetitive code
- Hard to add new unit types
- Configuration scattered

Desired outcome:
- Fluent builder API
- Chainable configuration
- Less boilerplate

Steps:
1. Create src/Core/UnitBuilder.cs
2. Implement fluent methods:
   - WithHealth(int max)
   - WithDamage(int value)
   - WithComponent<T>(Action<T> configure)
   - At(Vector3I position)
   - Build()
3. Refactor UnitFactory to use builder
4. Update all unit creation code

Example usage:
var boss = UnitBuilder.Create<Boss>()
    .WithHealth(50)
    .WithDamage(10)
    .WithComponent<ShieldComponent>(s => s.MaxShield = 20)
    .At(new Vector3I(0, 5, -5))
    .Build();

Affected files:
- src/Core/UnitBuilder.cs (new)
- src/Core/UnitFactory.cs
- src/Core/GameWorld.cs
```

**Move to Event-Driven Architecture:**
```
Refactor: Replace direct method calls with events

Current problem:
- Systems tightly coupled
- Hard to add new reactions
- Order of execution matters

Desired outcome:
- Systems emit events
- Other systems subscribe
- Decoupled, extensible

Steps:
1. Identify direct system calls
2. Replace with signal emissions
3. Subscribe in dependent systems
4. Remove direct dependencies

Example:
Old:
  _combatSystem.ExecuteCombat(attacker, defender);
  _healthSystem.CheckDeath(defender);
  _animationSystem.PlayAnimation(defender, "Hurt");

New:
  Combat system emits: CombatResolved(attacker, defender, damage)
  HealthSystem subscribes and checks death
  AnimationSystem subscribes and plays animation

Affected files:
- All systems
- Look for GetSystem<>() calls
- Replace with signal subscriptions
```

---

## Architecture Prompts

### System-Wide Changes

**Template:**
```
Architecture: [Change description]

Goal:
- [High-level goal]

Benefits:
- [Benefit 1]
- [Benefit 2]

Migration plan:
1. [Phase 1]
2. [Phase 2]
3. [Phase 3]

Files to create:
- [New file 1]
- [New file 2]

Files to modify:
- [Existing file 1]
- [Existing file 2]
```

**Examples:**

**Add Command Pattern:**
```
Architecture: Implement command pattern for actions

Goal:
- All player/AI actions as commands
- Enable undo/redo
- Enable action preview
- Replay system

Benefits:
- Undo last move
- Preview move outcomes
- Record gameplay
- Easier AI implementation

Migration plan:
1. Create ICommand interface
2. Implement MoveCommand, AttackCommand
3. Create CommandQueue in TurnManager
4. Modify InputSystem to create commands
5. Modify AISystem to create commands
6. Execute commands through queue

Files to create:
- src/Commands/ICommand.cs
- src/Commands/MoveCommand.cs
- src/Commands/AttackCommand.cs
- src/Commands/CommandQueue.cs

Files to modify:
- src/Systems/InputSystem.cs
- src/Systems/AISystem.cs
- src/Core/TurnManager.cs
```

**Add Save/Load System:**
```
Architecture: Add save/load functionality

Goal:
- Save game state to JSON
- Load game state from JSON
- Support multiple save slots

Benefits:
- Players can save progress
- Enable cloud saves later
- Easy debugging (load specific state)

Migration plan:
1. Create SaveData class (serializable)
2. Create SaveManager service
3. Add ISaveable interface for components
4. Implement ToSaveData() on all components
5. Implement FromSaveData() on all components
6. Wire up save/load in GameWorld

Files to create:
- src/Services/SaveManager.cs
- src/Data/SaveData.cs
- src/Interfaces/ISaveable.cs

Files to modify:
- All components (add ISaveable)
- src/Core/GameWorld.cs
- src/Systems/UISystem.cs (add save/load buttons)

SaveData structure:
{
  "version": "1.0",
  "turnNumber": 5,
  "player": {
    "position": [0, 0, 0],
    "health": 8,
    "maxHealth": 10
  },
  "enemies": [...]
}
```

**Add Ability System:**
```
Architecture: Add full ability system

Goal:
- Units can have multiple abilities
- Abilities have costs, cooldowns, ranges
- Different ability types (damage, heal, buff, teleport)

Benefits:
- Rich tactical options
- Unique unit abilities
- Player skill expression

Migration plan:
1. Create Ability base class
2. Create specific abilities (Fireball, Heal, Dash)
3. Create AbilityComponent
4. Create AbilitySystem
5. Add ability UI panel
6. Wire up input handling

Files to create:
- src/Abilities/Ability.cs
- src/Abilities/FireballAbility.cs
- src/Abilities/HealAbility.cs
- src/Abilities/DashAbility.cs
- src/Components/AbilityComponent.cs
- src/Systems/AbilitySystem.cs
- src/UI/AbilityPanel.cs

Files to modify:
- src/Systems/InputSystem.cs (hotkeys)
- src/Systems/UISystem.cs (ability panel)
- src/Units/Player.cs (add abilities)

Ability interface:
- bool CanUse(Unit caster)
- void Execute(Unit caster, Vector3I target)
- List<Vector3I> GetValidTargets(Unit caster)
- int GetCooldown()
- int GetCost()
```

---

## Quick Reference Templates

### Investigation Prompts

```
Show me all [components/systems/units] in the codebase
```

```
Explain how [feature] works. Show relevant files.
```

```
List all signals emitted by [System/Component]
```

```
Show me all places where [Component] is used
```

### Modification Prompts

```
Add [property/method] to [Component/System]
```

```
Modify [System] to [behavior change]
```

```
Change [Unit] to have [different stats/behavior]
```

```
Connect [Signal] to [Handler method] in [System]
```

### Code Quality Prompts

```
Add XML documentation to [File]
```

```
Add error handling to [Method] in [File]
```

```
Add validation to [Component] properties
```

```
Add debug logging to [System] for [event]
```

---

## Combining Prompts (Complex Features)

### Example: Stealth System

```
Add stealth system with following components:

1. Create StealthComponent:
   - IsHidden: bool (private, starts false)
   - DetectionRange: int (default 2)
   - Signal Detected fires when enemy sees unit
   - Signal Hidden fires when entering stealth
   - Method EnterStealth() sets IsHidden = true
   - Method ExitStealth() sets IsHidden = false

2. Create DetectionSystem:
   - Runs each turn
   - Checks if any enemies can see player
   - Enemy can see if:
     * Within DetectionRange
     * Player not IsHidden
     * Direct line of sight (no obstacles)
   - Emits StealthBroken(Player, Enemy) if detected
   - Player can't attack while hidden (breaks stealth)

3. Modify AISystem:
   - Enemies can't target hidden player
   - Enemies move to last known position
   - Idle behavior when no target

4. Modify CombatSystem:
   - Attacking breaks stealth (call ExitStealth())
   - Hidden attacks deal 2x damage (backstab)

5. Add UI indicator:
   - Eye icon when visible
   - Shadow icon when hidden

Place in:
- src/Components/StealthComponent.cs
- src/Systems/DetectionSystem.cs
- Modify: src/Systems/AISystem.cs
- Modify: src/Systems/CombatSystem.cs
- Modify: src/Systems/UISystem.cs
```

---

## AI Response Validation Checklist

When AI responds, verify:

- [ ] Created files in correct directories
- [ ] Used proper namespaces
- [ ] Followed naming conventions
- [ ] Added XML documentation
- [ ] Used [AutoRegister] for systems
- [ ] Implemented signals correctly
- [ ] Connected/disconnected events properly
- [ ] Added error handling
- [ ] Used [Export] for tweakable values
- [ ] Followed existing code patterns

---

## Common Follow-Up Prompts

After AI implements a feature:

```
Add unit tests for [Component/System]
```

```
Add error handling for edge case: [scenario]
```

```
Optimize [System] performance
```

```
Add debug visualization for [feature]
```

```
Document [feature] in CLAUDE.md
```

---

## Meta Prompts (About This Codebase)

```
Explain the architecture philosophy of this project
```

```
What are the key conventions I should follow?
```

```
Show me examples of well-structured [component/system/unit]
```

```
What's the best way to add [type of feature]?
```

```
How do systems communicate in this architecture?
```

---

## Troubleshooting Prompts

```
Why isn't [Component] showing up in inspector?
Answer: Needs [Export] attribute
```

```
Why isn't [Signal] firing?
Check: EmitSignal(SignalName.Signal, params)
Check: Signal defined with [Signal] attribute
Check: Correct parameter types
```

```
Why can't I find [Component] on [Unit]?
Check: Component added in _Ready()?
Check: Component name matches GetNode<>() call
Check: Using correct node path
```

```
Why isn't [System] running?
Check: [AutoRegister] attribute present
Check: System inherits from Node, ISystem
Check: GameWorld.BuildWorld() adds Systems node
```

---

## Efficiency Tips for AI Prompts

### Good Prompts (Specific, Actionable)

✅ "Create HealthComponent with MaxHealth, Current, TakeDamage(), and Died signal"

✅ "Modify CombatSystem.ExecuteCombat() to check for shields before applying damage"

✅ "Add [AutoRegister] to PoisonSystem and subscribe to TurnEnded"

### Bad Prompts (Vague, Ambiguous)

❌ "Add health"
❌ "Make combat better"
❌ "Fix the bug"

### Best Prompt Structure

```
Action: [Create/Modify/Debug/Refactor]
Target: [specific file/class/method]
Details: [exact requirements]
Context: [why/how it fits]
```

---

## Summary

This architecture is designed so you can:

1. **Describe features in natural language**
2. **AI generates type-safe code**
3. **Code auto-wires itself**
4. **Press F5 and it works**

The key is **specificity**:
- Don't say "add health"
- Say "create HealthComponent with Max/Current properties and Died signal"

The AI knows the patterns, you provide the details.

Happy AI-driven development!
