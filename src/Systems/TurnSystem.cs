using System.Linq;
using System.Threading.Tasks;
using Game.Components;
using Godot;

namespace Game
{
    public class TurnSystem : System
    {
        private int _currentTurnIndex = -1;
        private MovementSystem _movementSystem;
        private AnimationSystem _animationSystem;
        private DashSystem _dashSystem;

        public override void Initialize()
        {
            _movementSystem = Systems.Get<MovementSystem>();
            _animationSystem = Systems.Get<AnimationSystem>();
            _dashSystem = Systems.Get<DashSystem>();

            SetupInitialTurnOrder();
        }

        /// <summary>
        /// Orchestrates a complete player action from start to finish
        /// </summary>
        public async Task ExecutePlayerAction(Entity player, Vector3I destination)
        {
            // Clear waiting state
            player.Remove<WaitingForAction>();

            bool playerDefeated;

            // Check if player is in dash mode
            if (player.Has<DashModeActive>())
            {
                // Execute dash (no combat, fast movement)
                playerDefeated = await _movementSystem.ExecuteDash(player, destination);
            }
            else
            {
                // Execute normal movement with combat
                playerDefeated = await _movementSystem.ExecuteMove(player, destination);
            }

            if (playerDefeated)
            {
                // Handle player defeat
                GD.Print("Player defeated!");
                return;
            }

            // Set animation back to idle
            if (player.Has<Unit>())
            {
                _animationSystem.SetAnimationState(player, AnimationState.Idle);
            }

            // Complete the turn
            CompleteUnitTurn(player);
        }

        /// <summary>
        /// Orchestrates a complete enemy action
        /// </summary>
        public async Task ExecuteEnemyAction(Entity enemy, Vector3I destination)
        {
            enemy.Remove<WaitingForAction>();

            // Enemy movement (no combat on enemy turn in Hoplite-style)
            await _movementSystem.ExecuteMove(enemy, destination);

            // Set animation
            if (enemy.Has<Unit>())
            {
                _animationSystem.SetAnimationState(enemy, AnimationState.Idle);
            }

            // Complete the turn
            CompleteUnitTurn(enemy);
        }

        /// <summary>
        /// Enemy passes turn without acting
        /// </summary>
        public void ExecuteEnemyPass(Entity enemy)
        {
            GD.Print($"Enemy {enemy.Id} passes turn (player in range)");
            enemy.Remove<WaitingForAction>();
            CompleteUnitTurn(enemy);
        }

        private void CompleteUnitTurn(Entity unit)
        {
            unit.Remove<CurrentTurn>();
            AdvanceToNextUnit();
        }

        private void AdvanceToNextUnit()
        {
            var allUnits = Entities.Query<TurnOrder>()
                .OrderBy(e => e.Get<TurnOrder>())
                .ToList();

            _currentTurnIndex = (_currentTurnIndex + 1) % allUnits.Count;

            var nextUnit = allUnits[_currentTurnIndex];
            StartUnitTurn(nextUnit);
        }

        private void SetupInitialTurnOrder()
        {
            var player = Entities.Query<Player>().FirstOrDefault();
            var enemies = Entities.Query<Enemy>();
            var units = new[] { player }.Concat(enemies).ToList();

            for (int i = 0; i < units.Count; i++)
            {
                units[i].Add(new TurnOrder(i));
            }

            if (units.Any())
            {
                _currentTurnIndex = -1; // Will become 0 after first advancement
                AdvanceToNextUnit();
            }
        }

        private void StartUnitTurn(Entity unit)
        {
            // Setup pathfinding for new turn
            PathFinder.SetupPathfinding();

            unit.Add(new CurrentTurn());
            unit.Add(new WaitingForAction());
            Events.OnTurnChanged(unit);  // Notify UI and other systems
        }
    }
}
