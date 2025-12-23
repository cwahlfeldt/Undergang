using System.Collections.Generic;
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
        private GameStateManager _gameStateManager;
        private Dictionary<int, Vector3I> _positionsBeforeAction = new();  // Track positions before units act

        public override void Initialize()
        {
            _movementSystem = Systems.Get<MovementSystem>();
            _animationSystem = Systems.Get<AnimationSystem>();
            _dashSystem = Systems.Get<DashSystem>();
            _gameStateManager = Systems.Get<GameStateManager>();

            // Set the TurnSystem reference in GameStateManager to avoid circular dependency
            _gameStateManager.SetTurnSystem(this);

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
            // Check if player is dead - if so, stop turn cycle
            var player = Entities.Query<Player>().FirstOrDefault();
            if (player != null && player.Has<Health>() && player.Get<Health>() <= 0)
            {
                // Player is dead - game over, don't advance turns
                return;
            }

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

                // Wait for spawn animations to complete before starting the game
                Events.SpawnsComplete += OnSpawnsComplete;
            }
        }

        private void OnSpawnsComplete()
        {
            Events.SpawnsComplete -= OnSpawnsComplete;
            AdvanceToNextUnit();
        }

        private void StartUnitTurn(Entity unit)
        {
            // Store current positions before any actions
            if (unit.Has<Coordinate>())
            {
                _positionsBeforeAction[unit.Id] = unit.Get<Coordinate>().Value;
            }

            // Capture snapshot at START of player's turn (BEFORE they act)
            // This ensures health, position, and all state is captured before any combat
            if (unit.Has<Player>())
            {
                _gameStateManager.CaptureSnapshot(_currentTurnIndex);
                _gameStateManager.TickCooldown();
            }

            unit.Add(new CurrentTurn());
            unit.Add(new WaitingForAction());
            Events.OnTurnChanged(unit);  // Notify UI and other systems
        }

        /// <summary>
        /// Restarts the player's turn after a rewind
        /// </summary>
        public void RestartPlayerTurn(int restoredTurnIndex)
        {
            // Find player and reset to their turn
            var player = Entities.Query<Player>().FirstOrDefault();
            if (player == null) return;

            // Clear any existing turn state from all units
            foreach (var unit in Entities.Query<TurnOrder>())
            {
                unit.Remove<CurrentTurn>();
                unit.Remove<WaitingForAction>();
            }

            // Restore the turn index from the snapshot
            _currentTurnIndex = restoredTurnIndex;

            // DON'T call StartUnitTurn - it would overwrite _positionsBeforeAction
            // Instead, manually set up the player's turn state
            player.Add(new CurrentTurn());
            player.Add(new WaitingForAction());

            // Store the restored position as the "before action" position
            // so that rewind from this point works correctly
            if (player.Has<Coordinate>())
            {
                _positionsBeforeAction[player.Id] = player.Get<Coordinate>().Value;
            }

            // Use a special event that indicates this is a rewind restart
            // This prevents ability systems from ticking their cooldowns
            Events.OnTurnRestarted(player);
        }
    }
}
