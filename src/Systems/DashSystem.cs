using System.Linq;
using System.Threading.Tasks;
using Game.Components;
using Godot;

namespace Game
{
    public class DashSystem : System
    {
        private AnimationSystem _animationSystem;
        private CombatSystem _combatSystem;

        public override void Initialize()
        {
            _animationSystem = Systems.Get<AnimationSystem>();
            _combatSystem = Systems.Get<CombatSystem>();
        }

        public override async Task Update()
        {
            var dasher = Entities.Query<Dash, CurrentTurn>().FirstOrDefault();

            if (dasher == null)
                return;

            var (from, to) = dasher.Get<Dash>();

            // Validate dash destination exists and is traversable
            var tile = Entities.GetAt(to);
            if (tile == null || !tile.Has<Traversable>())
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
            var endPos = HexGrid.HexToWorld(to);

            // Fast dash: 0.25s vs normal movement 0.5s+
            var node = dasher.Get<Instance>().Node;
            await Tweener.Instance.MoveThrough(node, [endPos], 0.25f);

            // Update position
            dasher.Update(new Coordinate(to));

            // Remove dash component
            dasher.Remove<Dash>();

            // Check for enemy attacks if player dashed into enemy range
            if (dasher.Has<Player>() && dasher.Has<CurrentTurn>())
            {
                var destinationTile = Entities.GetAt(to);
                if (destinationTile != null && destinationTile.Has<AttackRangeTile>())
                {
                    int attackerId = destinationTile.Get<AttackRangeTile>();
                    Entity attacker = Entities.GetEntity(attackerId);

                    if (attacker != null && attacker.Has<Enemy>())
                    {
                        GD.Print($"Player dashed into enemy {attackerId} attack range at {to}!");
                        await _combatSystem.ResolveCombat(attacker, dasher);

                        // Check if player was defeated
                        if (!dasher.Has<Health>() || dasher.Get<Health>() <= 0)
                        {
                            GD.Print("Player defeated by enemy attack after dash!");
                            Events.OnDashCompleted(dasher, from, to);
                            Events.UnitActionComplete(dasher);
                            return;
                        }
                    }
                }
            }

            // Animation system will set back to Idle via DashCompleted event

            // Fire dash completed event
            Events.OnDashCompleted(dasher, from, to);

            // Complete the action
            Events.UnitActionComplete(dasher);

            GD.Print($"Dash completed from {from} to {to}");
        }
    }
}
