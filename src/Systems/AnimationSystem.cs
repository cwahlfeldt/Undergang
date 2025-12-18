using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Components;
using Godot;

namespace Game
{
    /// <summary>
    /// System responsible for managing unit animations based on their state
    /// Supports both standard naming convention and custom animation mappings
    /// </summary>
    public class AnimationSystem : System
    {
        /// <summary>
        /// Maps unit types to their custom animation naming schemes
        /// If a unit type is not in this map, it uses the standard pattern: "{UnitType}_{AnimationState}"
        /// </summary>
        private readonly Dictionary<UnitType, Dictionary<AnimationState, string>> _animationMappings = new()
        {
            // Player uses library-based animations with custom names
            {
                UnitType.Player, new Dictionary<AnimationState, string>
                {
                    { AnimationState.Spawn, "Character/Spawn_Air" },
                    { AnimationState.Idle, "Character/Idle_B" },
                    { AnimationState.Move, "Movement/Running_A" },  // Running animation from Movement library
                    { AnimationState.Attack, "Character/Interact" },  // Fallback until Slash is added
                    { AnimationState.Hurt, "Character/Hit_A" },
                    { AnimationState.Die, "Character/Death_A" },
                }
            }
            // Add more unit types here as needed, e.g.:
            // { UnitType.Grunt, new Dictionary<AnimationState, string> { ... } }
        };
        public override void Initialize()
        {
            // Subscribe to events that should trigger animations
            Events.MoveCompleted += OnMoveCompleted;
            Events.UnitDefeated += OnUnitDefeated;

            // Play spawn animation for all units (fire and forget - will auto-transition to Idle)
            foreach (var unit in Entities.Query<Unit, Instance>())
            {
                _ = PlaySpawnAnimationAsync(unit);
            }
        }

        /// <summary>
        /// Registers a custom animation mapping for a specific unit type
        /// This allows you to override the default naming convention with custom animation names
        /// </summary>
        /// <example>
        /// RegisterCustomAnimation(UnitType.Grunt, AnimationState.Attack, "GruntSpecialAttack");
        /// </example>
        public void RegisterCustomAnimation(UnitType unitType, AnimationState state, string animationName)
        {
            if (!_animationMappings.TryGetValue(unitType, out var mapping))
            {
                mapping = [];
                _animationMappings[unitType] = mapping;
            }
            mapping[state] = animationName;
        }

        /// <summary>
        /// Registers multiple custom animation mappings for a unit type at once
        /// </summary>
        /// <example>
        /// RegisterCustomAnimations(UnitType.Sniper, new Dictionary&lt;AnimationState, string&gt;
        /// {
        ///     { AnimationState.Idle, "Sniper/StandReady" },
        ///     { AnimationState.Attack, "Sniper/Shoot" }
        /// });
        /// </example>
        public void RegisterCustomAnimations(UnitType unitType, Dictionary<AnimationState, string> animations)
        {
            _animationMappings[unitType] = animations;
        }

        public override async Task Update()
        {
            // Process any pending animation state changes
            await Task.CompletedTask;
        }

        /// <summary>
        /// Sets the animation state for a unit and plays the appropriate animation
        /// </summary>
        public void SetAnimationState(Entity unit, AnimationState state)
        {
            if (!unit.Has<Unit>())
                return;

            // Update the current animation component
            if (unit.Has<CurrentAnimation>())
            {
                unit.Update(new CurrentAnimation(state));
            }
            else
            {
                unit.Add(new CurrentAnimation(state));
            }

            // Play the animation if AnimationPlayer exists
            PlayAnimation(unit, state);
        }

        /// <summary>
        /// Plays an animation for the given unit and state
        ///
        /// Animation resolution order:
        /// 1. Custom mapping from _animationMappings dictionary (if defined for unit type)
        /// 2. Standard pattern: "{UnitType}_{AnimationState}" (e.g., "Grunt_Attack")
        /// 3. Generic state name fallback (e.g., "Attack")
        /// 4. Graceful degradation if no animation found
        /// </summary>
        private void PlayAnimation(Entity unit, AnimationState state)
        {
            // Get the AnimationPlayer node from the unit's scene
            var unitNode = unit.Get<Instance>().Node;
            var animationPlayer = unitNode.GetNodeOrNull<Godot.AnimationPlayer>("AnimationPlayer")
                ?? unitNode.GetNodeOrNull<Godot.AnimationPlayer>("Knight/AnimationPlayer");

            if (animationPlayer == null)
            {
                // No AnimationPlayer found - animations not set up yet
                // This is expected during development before animations are added
                return;
            }

            // Store reference to AnimationPlayer if not already stored
            if (!unit.Has<Components.AnimationPlayer>())
            {
                unit.Add(new Components.AnimationPlayer(animationPlayer));
            }

            var unitType = unit.Get<Unit>().Type;
            bool shouldLoop = state == AnimationState.Idle || state == AnimationState.Move;

            // Try to get animation name, attempting multiple resolution strategies
            string animationName = GetAnimationName(unitType, state, animationPlayer);

            if (animationName != null)
            {
                // Set loop mode for the animation
                var animation = animationPlayer.GetAnimation(animationName);
                animation.LoopMode = shouldLoop
                    ? Godot.Animation.LoopModeEnum.Linear
                    : Godot.Animation.LoopModeEnum.None;

                animationPlayer.Play(animationName);
            }
            // If no animation found, silently continue (graceful degradation)
        }

        /// <summary>
        /// Resolves the animation name for a given unit type and state
        /// Tries multiple strategies in order of priority
        /// </summary>
        private string GetAnimationName(UnitType unitType, AnimationState state, Godot.AnimationPlayer animationPlayer)
        {
            // Strategy 1: Check custom mappings
            if (_animationMappings.TryGetValue(unitType, out var customMapping))
            {
                if (customMapping.TryGetValue(state, out var customName))
                {
                    if (animationPlayer.HasAnimation(customName))
                        return customName;
                }
            }

            // Strategy 2: Standard pattern "{UnitType}_{AnimationState}"
            var standardName = $"{unitType}_{state}";
            if (animationPlayer.HasAnimation(standardName))
                return standardName;

            // Strategy 3: Generic state name fallback
            var genericName = state.ToString();
            if (animationPlayer.HasAnimation(genericName))
                return genericName;

            // No animation found
            return null;
        }

        /// <summary>
        /// Plays spawn animation for a unit, then transitions to Idle after animation completes
        /// Uses Godot's animation_finished signal to avoid blocking
        /// </summary>
        private async Task PlaySpawnAnimationAsync(Entity unit)
        {
            if (!unit.Has<Unit>())
                return;

            // Set to Spawn state and play animation
            SetAnimationState(unit, AnimationState.Spawn);

            // Get animation player
            var animationPlayer = unit.Has<Components.AnimationPlayer>()
                ? unit.Get<Components.AnimationPlayer>().Player
                : null;

            if (animationPlayer != null)
            {
                var unitType = unit.Get<Unit>().Type;
                var animationName = GetAnimationName(unitType, AnimationState.Spawn, animationPlayer);

                if (animationName != null)
                {
                    // Wait for spawn animation to complete using Godot's signal
                    var tcs = new TaskCompletionSource<bool>();
                    void OnAnimationFinished(StringName anim)
                    {
                        animationPlayer.AnimationFinished -= OnAnimationFinished;
                        tcs.SetResult(true);
                    }
                    animationPlayer.AnimationFinished += OnAnimationFinished;
                    await tcs.Task;
                }
                else
                {
                    // No spawn animation exists, immediately transition to Idle
                    SetAnimationState(unit, AnimationState.Idle);
                    return;
                }
            }
            else
            {
                // No animation player, immediately transition to Idle
                SetAnimationState(unit, AnimationState.Idle);
                return;
            }

            // Transition to Idle state after animation completes
            SetAnimationState(unit, AnimationState.Idle);
        }

        /// <summary>
        /// Trigger attack animation for attacker and hurt animation for defender
        /// </summary>
        public async Task PlayAttackAnimation(Entity attacker, Entity defender)
        {
            if (!attacker.Has<Unit>() || !defender.Has<Unit>())
                return;

            // Set attacker to Attack state
            SetAnimationState(attacker, AnimationState.Attack);

            // Set defender to Hurt state
            SetAnimationState(defender, AnimationState.Hurt);

            // Get animation durations
            var attackerPlayer = attacker.Has<Components.AnimationPlayer>()
                ? attacker.Get<Components.AnimationPlayer>().Player
                : null;

            if (attackerPlayer != null)
            {
                var attackAnimName = GetAnimationName(attacker.Get<Unit>().Type, AnimationState.Attack, attackerPlayer);
                if (attackAnimName != null)
                {
                    // Wait for attack animation to complete
                    var attackDuration = attackerPlayer.GetAnimation(attackAnimName).Length;
                    await Task.Delay((int)(attackDuration * 1000));
                }
                else
                {
                    // Fallback duration if no animation
                    await Task.Delay(300);
                }
            }
            else
            {
                // Fallback duration if no animation player
                await Task.Delay(300);
            }

            // Return both units to Idle
            SetAnimationState(attacker, AnimationState.Idle);

            if (!defender.Has<Health>() || defender.Get<Health>() > 0)
            {
                SetAnimationState(defender, AnimationState.Idle);
            }
        }

        private void OnMoveCompleted(Entity unit, Vector3I from, Vector3I to)
        {
            // Return to idle after movement completes
            if (unit.Has<Unit>())
            {
                SetAnimationState(unit, AnimationState.Idle);
            }
        }

        private void OnUnitDefeated(Entity unit)
        {
            // Play death animation when unit is defeated
            if (unit.Has<Unit>())
            {
                SetAnimationState(unit, AnimationState.Die);
            }
        }

        public override void Cleanup()
        {
            Events.MoveCompleted -= OnMoveCompleted;
            Events.UnitDefeated -= OnUnitDefeated;
        }
    }
}
