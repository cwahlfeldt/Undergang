using Godot;

namespace Game.Components
{
    /// <summary>
    /// Components for animation system
    /// See AnimationSystem.cs and ANIMATIONS.md for details
    /// </summary>

    /// <summary>
    /// Component to track current animation state of a unit
    /// </summary>
    public record struct CurrentAnimation(AnimationState State)
    {
        public static implicit operator AnimationState(CurrentAnimation animation) => animation.State;
    }

    /// <summary>
    /// Component to store reference to the AnimationPlayer node for a unit
    /// </summary>
    public record struct AnimationPlayer(Godot.AnimationPlayer Player)
    {
        public static implicit operator Godot.AnimationPlayer(AnimationPlayer player) => player.Player;
    }
}
