using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game
{
    /// <summary>
    /// Configuration for animation mappings per unit type.
    /// Centralizes all animation name definitions for easy customization.
    /// </summary>
    public static class AnimationConfig
    {
        /// <summary>
        /// Animation profile for skeleton-based character models.
        /// Uses the Character/Movement animation library structure from Mixamo imports.
        /// </summary>
        public static readonly IReadOnlyDictionary<AnimationState, string> PlayerProfile = new Dictionary<AnimationState, string>
        {
            { AnimationState.Spawn, "Character/Spawn_Air" },
            { AnimationState.Idle, "Character/Idle_B" },
            { AnimationState.Move, "Movement/Running_A" },
            { AnimationState.Attack, "Character/Interact" },
            { AnimationState.Hurt, "Character/Hit_A" },
            { AnimationState.Die, "Character/Death_A" },
            { AnimationState.Victory, "Character/Victory" },
            { AnimationState.Defeat, "Character/Defeat" },
        };

        /// <summary>
        /// Animation profile for skeleton-based character models.
        /// Uses the Character/Movement animation library structure from Mixamo imports.
        /// </summary>
        public static readonly IReadOnlyDictionary<AnimationState, string> SkeletonProfile = new Dictionary<AnimationState, string>
        {
            { AnimationState.Spawn, "Enemy/Skeletons_Awaken_Floor" },
            { AnimationState.Idle, "Enemy/Skeletons_Idle" },
            { AnimationState.Move, "Enemy/Skeletons_Walking" },
            { AnimationState.Attack, "Character/Interact" },
            { AnimationState.Hurt, "Character/Hit_A" },
            { AnimationState.Die, "Character/Death_A" },
            { AnimationState.Victory, "Character/Victory" },
            { AnimationState.Defeat, "Character/Defeat" },
        };

        public static readonly IReadOnlyDictionary<AnimationState, string> WizardProfile = new Dictionary<AnimationState, string>
        {
            { AnimationState.Spawn, "Character/Spawn_Ground" },
            { AnimationState.Idle, "Character/Idle_B" },
            { AnimationState.Move, "Movement/Running_A" },
            { AnimationState.Attack, "Character/Interact" },
            { AnimationState.Hurt, "Character/Hit_A" },
            { AnimationState.Die, "Character/Death_A" },
            { AnimationState.Victory, "Character/Victory" },
            { AnimationState.Defeat, "Character/Defeat" },
        };

        /// <summary>
        /// Maps each unit type to its animation profile.
        /// Override specific animations by creating a new profile based on an existing one.
        /// </summary>
        public static readonly IReadOnlyDictionary<UnitType, IReadOnlyDictionary<AnimationState, string>> UnitAnimations =
            new Dictionary<UnitType, IReadOnlyDictionary<AnimationState, string>>
            {
                { UnitType.Player, PlayerProfile },
                { UnitType.Grunt, SkeletonProfile },
                { UnitType.Wizard, WizardProfile },
                { UnitType.SniperAxisQ, SkeletonProfile },
                { UnitType.SniperAxisR, SkeletonProfile },
                { UnitType.SniperAxisS, SkeletonProfile },
                { UnitType.Enemy, SkeletonProfile }, // Default for generic enemies
            };

        /// <summary>
        /// Creates a custom animation profile by overriding specific animations from a base profile.
        /// </summary>
        /// <example>
        /// var wizardProfile = AnimationConfig.CreateProfile(AnimationConfig.SkeletonProfile, new()
        /// {
        ///     { AnimationState.Attack, "Magic/CastSpell" },
        ///     { AnimationState.Idle, "Magic/FloatIdle" }
        /// });
        /// </example>
        public static IReadOnlyDictionary<AnimationState, string> CreateProfile(
            IReadOnlyDictionary<AnimationState, string> baseProfile,
            Dictionary<AnimationState, string> overrides)
        {
            var result = new Dictionary<AnimationState, string>(baseProfile);
            foreach (var (state, animName) in overrides)
            {
                result[state] = animName;
            }
            return new ReadOnlyDictionary<AnimationState, string>(result);
        }

        /// <summary>
        /// Gets the animation name for a unit type and animation state.
        /// Returns null if no mapping exists.
        /// </summary>
        public static string GetAnimation(UnitType unitType, AnimationState state)
        {
            if (UnitAnimations.TryGetValue(unitType, out var profile))
            {
                if (profile.TryGetValue(state, out var animName))
                {
                    return animName;
                }
            }
            return null;
        }
    }
}
