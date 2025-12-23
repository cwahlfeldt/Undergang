using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Components;
using Godot;

namespace Game
{
    /// <summary>
    /// System responsible for managing unit animations based on their state.
    /// Animation mappings are configured in AnimationConfig.cs.
    /// Supports runtime overrides via RegisterCustomAnimation methods.
    /// </summary>
    public class AnimationSystem : System
    {
        private readonly Dictionary<UnitType, Dictionary<AnimationState, string>> _runtimeOverrides = new();

        public override void Initialize()
        {
            // Subscribe to events that should trigger animations
            Events.MoveCompleted += OnMoveCompleted;
            Events.UnitDefeated += OnUnitDefeated;

            // Play spawn animations with staggered timing
            _ = PlayStaggeredSpawnAnimations();
        }

        /// <summary>
        /// Plays spawn animations for all units with staggered timing based on turn order.
        /// Player spawns first and completes before enemies start spawning.
        /// Enemies are hidden initially and made visible when their spawn animation plays.
        /// </summary>
        private async Task PlayStaggeredSpawnAnimations()
        {
            var units = Entities.Query<Unit, Instance>().ToList();

            // Spawn player first
            var player = units.FirstOrDefault(u => u.Has<Player>());
            if (player != null)
            {
                // Start player spawn but don't wait for full completion
                SetAnimationState(player, AnimationState.Spawn);
                _ = WaitForSpawnAndTransitionToIdle(player);

                // Brief delay so player is visible first, then start enemies
                await Task.Delay(Config.PlayerSpawnDelayMs);
            }

            // Then spawn enemies in turn order with stagger
            var enemies = units
                .Where(u => u.Has<Enemy>())
                .OrderBy(u => u.Has<TurnOrder>() ? u.Get<TurnOrder>().Value : int.MaxValue)
                .ToList();

            foreach (var enemy in enemies)
            {
                // Start spawn animation while still hidden, then reveal
                var node = enemy.Get<Instance>().Node;

                // Set animation state first (starts the animation while hidden)
                SetAnimationState(enemy, AnimationState.Spawn);

                // Small delay to ensure animation has started before showing
                await Task.Delay(4); // ~1 frame at 60fps

                // Now make visible - animation is already playing
                node.Visible = true;

                // Continue with spawn animation completion in background
                _ = ContinueSpawnAnimationAsync(enemy);
                await Task.Delay(Config.SpawnStaggerDelayMs);
            }

            // Notify that all spawns are complete - game can now start
            Events.OnSpawnsComplete();
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
            if (!_runtimeOverrides.TryGetValue(unitType, out var mapping))
            {
                mapping = [];
                _runtimeOverrides[unitType] = mapping;
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
            _runtimeOverrides[unitType] = animations;
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
            // Check if unit still has a valid Instance component
            if (!unit.Has<Instance>())
                return;

            // Get the AnimationPlayer node from the unit's scene
            var unitNode = unit.Get<Instance>().Node;

            // Check if node has been disposed (can happen during rewind)
            if (unitNode == null || !GodotObject.IsInstanceValid(unitNode))
                return;

            var animationPlayer = FindAnimationPlayer(unitNode);

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

                // Use crossfade blend for smooth transitions between animation states
                animationPlayer.Play(animationName, Config.AnimationBlendTime);
            }
            // If no animation found, silently continue (graceful degradation)
        }

        /// <summary>
        /// Resolves the animation name for a given unit type and state
        /// Tries multiple strategies in order of priority
        /// </summary>
        private string GetAnimationName(UnitType unitType, AnimationState state, Godot.AnimationPlayer animationPlayer)
        {
            // Strategy 1: Check runtime overrides (highest priority)
            if (_runtimeOverrides.TryGetValue(unitType, out var runtimeMapping))
            {
                if (runtimeMapping.TryGetValue(state, out var runtimeName))
                {
                    if (animationPlayer.HasAnimation(runtimeName))
                        return runtimeName;
                }
            }

            // Strategy 2: Check AnimationConfig mappings
            var configName = AnimationConfig.GetAnimation(unitType, state);
            if (configName != null && animationPlayer.HasAnimation(configName))
                return configName;

            // Strategy 3: Standard pattern "{UnitType}_{AnimationState}"
            var standardName = $"{unitType}_{state}";
            if (animationPlayer.HasAnimation(standardName))
                return standardName;

            // Strategy 4: Generic state name fallback
            var genericName = state.ToString();
            if (animationPlayer.HasAnimation(genericName))
                return genericName;

            // No animation found
            return null;
        }

        /// <summary>
        /// Recursively searches for an AnimationPlayer node in the scene tree.
        /// This handles different character model structures where AnimationPlayer
        /// may be nested under various parent nodes (e.g., Knight/, Skeleton_Warrior/, etc.)
        /// </summary>
        private Godot.AnimationPlayer FindAnimationPlayer(Node root)
        {
            // Check if node has been disposed (can happen during rewind)
            if (root == null || !GodotObject.IsInstanceValid(root))
                return null;

            // First check if root itself is an AnimationPlayer
            if (root is Godot.AnimationPlayer ap)
                return ap;

            // Check direct children first for performance
            var directChild = root.GetNodeOrNull<Godot.AnimationPlayer>("AnimationPlayer");
            if (directChild != null)
                return directChild;

            // Recursively search all children
            foreach (var child in root.GetChildren())
            {
                var found = FindAnimationPlayer(child);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// Continues a spawn animation that was already started.
        /// Waits for animation completion and transitions to Idle.
        /// </summary>
        private async Task ContinueSpawnAnimationAsync(Entity unit)
        {
            if (!unit.Has<Unit>())
                return;

            await WaitForSpawnAndTransitionToIdle(unit);
        }

        /// <summary>
        /// Waits for spawn animation to complete and transitions to Idle state.
        /// </summary>
        private async Task WaitForSpawnAndTransitionToIdle(Entity unit)
        {
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

            // Check if nodes are still valid (can be disposed during rewind)
            if (!attacker.Has<Instance>() || !defender.Has<Instance>())
                return;

            var attackerNode = attacker.Get<Instance>().Node;
            var defenderNode = defender.Get<Instance>().Node;

            if (attackerNode == null || !GodotObject.IsInstanceValid(attackerNode) ||
                defenderNode == null || !GodotObject.IsInstanceValid(defenderNode))
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
                    await Task.Delay(Config.FallbackAttackDurationMs);
                }
            }
            else
            {
                await Task.Delay(Config.FallbackAttackDurationMs);
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
