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

        public override void Initialize()
        {
            _combatSystem = Systems.Get<CombatSystem>();
            _animationSystem = Systems.Get<AnimationSystem>();
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

            // Check if player was already in attack range of an enemy before moving (for player attacks)
            Entity enemyInRange = null;
            if (mover.Has<Player>() && mover.Has<CurrentTurn>())
            {
                enemyInRange = CheckIfPlayerWasInEnemyRange(mover, origin);
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

            // ENEMY ATTACKS: Check if PLAYER moved into enemy attack range (not if enemy moved)
            // Multiple enemies can attack if their threat zones overlap
            if (mover.Has<Player>() && mover.Has<CurrentTurn>())
            {
                // Get all enemies whose attack range includes the destination
                var attackingEnemies = Entities.Query<Enemy, Coordinate>()
                    .Where(enemy =>
                    {
                        // Skip the enemy we were already fighting (they don't get a reactive attack)
                        if (enemyInRange != null && enemy.Id == enemyInRange.Id)
                            return false;

                        // Check if destination is in this enemy's attack range
                        var enemyAttackRange = RangeSystem.GetAttackRangeTiles(enemy, enemy.Get<Coordinate>());
                        return enemyAttackRange.Contains(destination);
                    })
                    .ToList();

                // Process attacks from all threatening enemies
                foreach (var attacker in attackingEnemies)
                {
                    GD.Print($"Player moved into enemy {attacker.Id} attack range at {destination}!");
                    await _combatSystem.ResolveCombat(attacker, mover);

                    // Check if player was defeated after each attack
                    if (!mover.Has<Health>() || mover.Get<Health>() <= 0)
                    {
                        GD.Print("Player defeated by enemy attack!");
                        return true; // Unit defeated
                    }
                }
            }

            // PLAYER ATTACKS: Player attacks ALL enemies in range when moving within range
            if (mover.Has<Player>() && mover.Has<CurrentTurn>())
            {
                // Get all enemies in attack range at destination
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
                    // Only attack if we were already fighting this enemy (moved within their range)
                    // OR if we just entered their range (covered by reactive attack above)
                    if (enemyInRange != null && enemy.Id == enemyInRange.Id)
                    {
                        GD.Print($"Player attacks enemy {enemy.Id} while moving within attack range!");
                        await _combatSystem.ResolveCombat(mover, enemy);
                    }
                }
            }

            return false; // Unit survived
        }

        /// <summary>
        /// Get attack range tiles for an entity at a given position
        /// </summary>
        private IReadOnlyList<Vector3I> GetAttackRangeTiles(Entity entity, Vector3I position)
        {
            return RangeSystem.GetAttackRangeTiles(entity, position).ToList();
        }

        /// <summary>
        /// Check if player is currently in attack range of any enemy
        /// </summary>
        private Entity CheckIfPlayerWasInEnemyRange(Entity player, Vector3I playerCoord)
        {
            if (player == null) return null;

            // Get all tiles within player's attack range (based on player's range type)
            var attackRangeTiles = GetAttackRangeTiles(player, playerCoord);

            // Check each tile for enemies
            foreach (var coord in attackRangeTiles)
            {
                var enemiesAtCoord = Entities.Query<Enemy, Coordinate>()
                    .Where(e => e.Get<Coordinate>() == coord)
                    .FirstOrDefault();

                if (enemiesAtCoord != null)
                {
                    return enemiesAtCoord; // Return first enemy found in range
                }
            }

            return null;
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
