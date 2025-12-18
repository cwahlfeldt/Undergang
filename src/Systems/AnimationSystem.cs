using System.Linq;
using System.Threading.Tasks;
using Game.Components;
using Godot;

namespace Game
{
    /// <summary>
    /// System responsible for managing unit animations based on their state
    /// Animations are organized per-unit-type (Player, Grunt, Sniper, etc.)
    /// </summary>
    public class AnimationSystem : System
    {
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
        /// Animation names follow the pattern: "{UnitType}_{AnimationState}"
        /// e.g., "Player_Idle", "Grunt_Attack", "Sniper_Move"
        ///
        /// Special handling for Player unit with library-based animations:
        /// - Spawn -> "Character/Spawn_Ground"
        /// - Idle -> "Character/Idle_B"
        /// - Attack -> "Character/Slash_A" (if exists)
        /// - Hurt -> "Character/Hit_A"
        /// - Die -> "Character/Death_A"
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

            // Build animation name based on unit type and state
            var unitType = unit.Get<Unit>().Type;

            // Determine if animation should loop (Idle should always loop)
            bool shouldLoop = state == AnimationState.Idle;

            // Special handling for Player with library-based animations
            if (unitType == UnitType.Player)
            {
                var animationName = state switch
                {
                    AnimationState.Spawn => "Character/Spawn_Air",
                    AnimationState.Idle => "Character/Idle_B",
                    AnimationState.Attack => "Character/Interact",  // Fallback to Interact until Slash is added
                    AnimationState.Hurt => "Character/Hit_A",
                    AnimationState.Die => "Character/Death_A",
                    AnimationState.Move => "Character/Idle_B",      // Fallback to Idle until Walk is added
                    _ => $"Character/{state}_A"
                };

                if (animationPlayer.HasAnimation(animationName))
                {
                    // Set loop mode for the animation
                    var animation = animationPlayer.GetAnimation(animationName);
                    animation.LoopMode = shouldLoop
                        ? Godot.Animation.LoopModeEnum.Linear
                        : Godot.Animation.LoopModeEnum.None;

                    animationPlayer.Play(animationName);
                    return;
                }
            }

            // Standard pattern: {UnitType}_{AnimationState}
            var standardAnimationName = $"{unitType}_{state}";

            // Check if animation exists
            if (!animationPlayer.HasAnimation(standardAnimationName))
            {
                // Fallback to generic state name if unit-specific doesn't exist
                if (animationPlayer.HasAnimation(state.ToString()))
                {
                    var animation = animationPlayer.GetAnimation(state.ToString());
                    animation.LoopMode = shouldLoop
                        ? Godot.Animation.LoopModeEnum.Linear
                        : Godot.Animation.LoopModeEnum.None;

                    animationPlayer.Play(state.ToString());
                }
                else
                {
                    // Animation not found - this is expected during development
                }
                return;
            }

            // Set loop mode and play the animation
            var standardAnimation = animationPlayer.GetAnimation(standardAnimationName);
            standardAnimation.LoopMode = shouldLoop
                ? Godot.Animation.LoopModeEnum.Linear
                : Godot.Animation.LoopModeEnum.None;

            animationPlayer.Play(standardAnimationName);
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
                var animationName = unitType == UnitType.Player ? "Character/Spawn_Ground" : $"{unitType}_Spawn";

                if (animationPlayer.HasAnimation(animationName))
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

            if (attackerPlayer != null && attackerPlayer.HasAnimation($"{attacker.Get<Unit>().Type}_Attack"))
            {
                // Wait for attack animation to complete
                var attackDuration = attackerPlayer.GetAnimation($"{attacker.Get<Unit>().Type}_Attack").Length;
                await Task.Delay((int)(attackDuration * 1000));
            }
            else
            {
                // Fallback duration if no animation
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
