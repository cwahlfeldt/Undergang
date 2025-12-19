using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Game.Components;
using Godot;

namespace Game
{
    public class MovementSystem : System
    {
        private CombatSystem _combatSystem;
        private AnimationSystem _animationSystem;
        private DashSystem _dashSystem;

        public override void Initialize()
        {
            _combatSystem = Systems.Get<CombatSystem>();
            _animationSystem = Systems.Get<AnimationSystem>();
            _dashSystem = Systems.Get<DashSystem>();
        }

        /// <summary>
        /// Executes a unit's movement from current position to destination
        /// Handles pathfinding, animation, and combat resolution
        /// Returns true if unit was defeated during movement
        /// </summary>
        public async Task<bool> ExecuteMove(Entity mover, Vector3I destination)
        {
            var origin = mover.Get<Coordinate>();
            var path = PathFinder.FindPath(origin, destination, mover.Get<MoveRange>());

            // Check for combat along the path
            bool unitDefeated = await ProcessMovementWithCombat(mover, path);

            if (unitDefeated)
            {
                return true;  // Unit defeated
            }

            var fromTile = Entities.GetAt(path.First());
            var toTile = Entities.GetAt(path.Last());

            // Fire event for UI updates, range recalculation
            Events.OnMoveCompleted(mover, fromTile.Get<Coordinate>(), toTile.Get<Coordinate>());

            return false;  // Unit survived
        }

        /// <summary>
        /// Executes a dash move - fast movement without enemy reactive attacks
        /// Player can still attack enemies in range after dashing
        /// </summary>
        public async Task<bool> ExecuteDash(Entity mover, Vector3I destination)
        {
            var origin = mover.Get<Coordinate>();

            // Validate dash destination
            if (!_dashSystem.IsValidDashDestination(origin, destination))
            {
                return false;
            }

            // Set to Move animation state
            if (mover.Has<Unit>())
            {
                _animationSystem.SetAnimationState(mover, AnimationState.Move);
            }

            // Fast dash animation - direct path, no pathfinding
            var locations = new List<Vector3> { HexGrid.HexToWorld(destination) };
            await Tweener.MoveThrough(mover.Get<Instance>().Node, locations, Config.DashAnimationSpeed);
            mover.Update(new Coordinate(destination));

            // Trigger cooldown
            _dashSystem.ExecuteDash(mover, destination);

            // PLAYER ATTACKS: After dashing, attack all enemies in range at destination
            if (mover.Has<Player>() && mover.Has<CurrentTurn>())
            {
                var enemiesInRange = Entities.Query<Enemy, Coordinate>()
                    .Where(enemy =>
                    {
                        // Check if enemy is in player's attack range from destination
                        return IsInAttackRange(mover, destination, enemy.Get<Coordinate>()) &&
                               _combatSystem.CanAttack(mover, enemy);
                    })
                    .ToList();

                // Attack all enemies in range
                foreach (var enemy in enemiesInRange)
                {
                    await _combatSystem.ResolveCombat(mover, enemy);
                }
            }

            // Fire event for UI updates, range recalculation
            var fromTile = Entities.GetAt(origin);
            var toTile = Entities.GetAt(destination);
            Events.OnMoveCompleted(mover, fromTile.Get<Coordinate>(), toTile.Get<Coordinate>());

            return false;  // Dash never results in defeat (no enemy reactive attacks)
        }

        // Legacy Update() - kept for backward compatibility
        // New code should use ExecuteMove() directly via TurnSystem orchestration
        public override async Task Update()
        {
            var mover = Entities.Query<Movement, CurrentTurn>().FirstOrDefault();

            if (mover == null)
                return;

            var (from, to) = mover.Get<Movement>();
            mover.Remove<Movement>();

            await ExecuteMove(mover, to);
        }

        private async Task<bool> ProcessMovementWithCombat(Entity mover, List<Vector3I> path)
        {
            var origin = path.First();
            var destination = path.Last();
            var destinationTile = Entities.GetAt(destination);

            // Track which enemies player was already in range of before moving
            List<int> enemiesAlreadyInRange = new List<int>();
            if (mover.Has<Player>() && mover.Has<CurrentTurn>())
            {
                // Get all enemies that could attack the player at origin
                var enemiesAtOrigin = Entities.Query<Enemy, Coordinate>()
                    .Where(enemy =>
                    {
                        var enemyAttackRange = RangeSystem.GetAttackRangeTiles(enemy, enemy.Get<Coordinate>());
                        return enemyAttackRange.Contains(origin);
                    })
                    .Select(e => e.Id)
                    .ToList();

                enemiesAlreadyInRange.AddRange(enemiesAtOrigin);
            }

            // Set to Move animation state
            if (mover.Has<Unit>())
            {
                _animationSystem.SetAnimationState(mover, AnimationState.Move);
            }

            // Animate movement
            var locations = path.Select(HexGrid.HexToWorld).ToList();
            await Tweener.MoveThrough(mover.Get<Instance>().Node, locations);
            mover.Update(new Coordinate(destination));

            // Animation system will set back to Idle via MoveCompleted event

            // ENEMY REACTIVE ATTACKS: Only enemies whose range the player NEWLY entered attack
            // Skip enemies that player was already in range of
            if (mover.Has<Player>() && mover.Has<CurrentTurn>())
            {
                // Process attacks from all threatening enemies that we NEWLY entered range of
                foreach (var attacker in Entities.Query<Enemy, Coordinate>()
                    .Where(enemy =>
                    {
                        // Skip enemies we were already in range of (they don't get reactive attacks)
                        if (enemiesAlreadyInRange.Contains(enemy.Id))
                            return false;

                        // Check if destination is in this enemy's attack range
                        var enemyAttackRange = RangeSystem.GetAttackRangeTiles(enemy, enemy.Get<Coordinate>());
                        return enemyAttackRange.Contains(destination);
                    }))
                {
                    await _combatSystem.ResolveCombat(attacker, mover);

                    // Check if player was defeated after each attack
                    if (!mover.Has<Health>() || mover.Get<Health>() <= 0)
                    {
                        return true; // Unit defeated
                    }
                }
            }

            // PLAYER ATTACKS: Player attacks ALL enemies in range at destination that they were ALREADY adjacent to
            if (mover.Has<Player>() && mover.Has<CurrentTurn>())
            {
                // Get all enemies in attack range at destination
                var enemiesInRange = Entities.Query<Enemy, Coordinate>()
                    .Where(enemy =>
                    {
                        // Only attack enemies we were already in range of (moving within their range)
                        if (!enemiesAlreadyInRange.Contains(enemy.Id))
                            return false;

                        // Check if enemy is still in player's attack range from destination
                        return IsInAttackRange(mover, destination, enemy.Get<Coordinate>()) &&
                               _combatSystem.CanAttack(mover, enemy);
                    })
                    .ToList();

                // Attack all enemies that we were already fighting
                foreach (var enemy in enemiesInRange)
                {
                    await _combatSystem.ResolveCombat(mover, enemy);

                    // No need to check if player died - player attacks happen after enemy attacks
                }
            }

            return false; // Unit survived
        }

        /// <summary>
        /// Get attack range tiles for an entity at a given position
        /// </summary>
        private IEnumerable<Vector3I> GetAttackRangeTiles(Entity entity, Vector3I position)
        {
            return RangeSystem.GetAttackRangeTiles(entity, position);
        }

        /// <summary>
        /// Check if target coordinate is within attacker's attack range
        /// </summary>
        private bool IsInAttackRange(Entity attacker, Vector3I attackerCoord, Vector3I targetCoord)
        {
            if (attacker == null) return false;

            var tilesInRange = GetAttackRangeTiles(attacker, attackerCoord);
            return tilesInRange.Contains(targetCoord);
        }
    }
}
