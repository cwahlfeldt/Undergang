using System.Linq;
using System.Threading.Tasks;
using Game.Components;
using Godot;

namespace Game
{
    public class DashSystem : System
    {
        private AnimationSystem _animationSystem;

        public override void Initialize()
        {
            _animationSystem = Systems.Get<AnimationSystem>();
        }

        public override async Task Update()
        {
            var dasher = Entities.Query<Dash, CurrentTurn>().FirstOrDefault();

            if (dasher == null)
                return;

            var (from, to) = dasher.Get<Dash>();

            // Validate dash destination exists and is traversable
            var destinationTile = Entities.GetAt(to);
            if (destinationTile == null || !destinationTile.Has<Traversable>())
            {
                GD.Print($"Dash destination {to} is not traversable!");
                dasher.Remove<Dash>();
                return;
            }

            // Set to Move animation state (or could use a special Dash animation)
            if (dasher.Has<Unit>())
            {
                _animationSystem.SetAnimationState(dasher, AnimationState.Move);
            }

            // Perform quick dash animation - direct movement, no pathfinding
            var startPos = HexGrid.HexToWorld(from);
            var endPos = HexGrid.HexToWorld(to);

            // Fast dash: 0.25s vs normal movement 0.5s+
            await Tweener.MoveTo(dasher.Get<Instance>().Node, endPos, 0.25f);

            // Update position
            dasher.Update(new Coordinate(to));

            // Remove dash component
            dasher.Remove<Dash>();

            // Animation system will set back to Idle via DashCompleted event

            // Fire dash completed event
            Events.OnDashCompleted(dasher, from, to);

            // Complete the action
            Events.UnitActionComplete(dasher);

            GD.Print($"Dash completed from {from} to {to}");
        }
    }
}
