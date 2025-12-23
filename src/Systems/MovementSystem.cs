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
            // Check if mover still has a valid Instance (could be disposed during rewind)
            if (!mover.Has<Instance>())
                return false;

            var moverNode = mover.Get<Instance>().Node;
            if (moverNode == null || !GodotObject.IsInstanceValid(moverNode))
                return false;

            var origin = mover.Get<Coordinate>();
            var path = PathFinder.FindPath(origin, destination, mover.Get<MoveRange>());

            // Handle case where no valid path exists (destination unreachable or occupied)
            if (path == null || path.Count == 0)
            {
                // No movement possible - fire event with current position and return
                Events.OnMoveCompleted(mover, origin, origin);
                return false;
            }

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
            // Check if mover still has a valid Instance (could be disposed during rewind)
            if (!mover.Has<Instance>())
                return false;

            var moverNode = mover.Get<Instance>().Node;
            if (moverNode == null || !GodotObject.IsInstanceValid(moverNode))
                return false;

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

            // Track which MELEE enemies player was already adjacent to before moving
            // Only melee enemies (RangeCircle) skip reactive attacks when player was already in range
            // Ranged enemies always get reactive attacks when player is in their range
            List<int> meleeEnemiesAlreadyAdjacent = new List<int>();
            if (mover.Has<Player>() && mover.Has<CurrentTurn>())
            {
                // Get all MELEE enemies that could attack the player at origin
                var meleeEnemiesAtOrigin = Entities.Query<Enemy, Coordinate>()
                    .Where(enemy =>
                    {
                        // Only apply "already in range" logic to melee enemies
                        if (!enemy.Has<RangeCircle>())
                            return false;

                        var enemyAttackRange = RangeSystem.GetAttackRangeTiles(enemy, enemy.Get<Coordinate>());
                        return enemyAttackRange.Contains(origin);
                    })
                    .Select(e => e.Id)
                    .ToList();

                meleeEnemiesAlreadyAdjacent.AddRange(meleeEnemiesAtOrigin);
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

            // ENEMY REACTIVE ATTACKS: Enemies attack when player enters/remains in their range
            // Exception: Melee enemies (RangeCircle) don't get reactive attacks if player was already adjacent
            if (mover.Has<Player>() && mover.Has<CurrentTurn>())
            {
                // Process attacks from all threatening enemies
                foreach (var attacker in Entities.Query<Enemy, Coordinate>()
                    .Where(enemy =>
                    {
                        // Skip melee enemies we were already adjacent to (they don't get reactive attacks)
                        if (meleeEnemiesAlreadyAdjacent.Contains(enemy.Id))
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

            // PLAYER ATTACKS: Player attacks melee enemies they were already adjacent to
            // (Hoplite-style: moving within an enemy's melee range triggers player counter-attack)
            if (mover.Has<Player>() && mover.Has<CurrentTurn>())
            {
                // Get all melee enemies we were adjacent to that are still in player's attack range
                var enemiesInRange = Entities.Query<Enemy, Coordinate>()
                    .Where(enemy =>
                    {
                        // Only attack melee enemies we were already adjacent to
                        if (!meleeEnemiesAlreadyAdjacent.Contains(enemy.Id))
                            return false;

                        // Check if enemy is still in player's attack range from destination
                        return IsInAttackRange(mover, destination, enemy.Get<Coordinate>()) &&
                               _combatSystem.CanAttack(mover, enemy);
                    })
                    .ToList();

                // Attack all melee enemies that we were already fighting
                foreach (var enemy in enemiesInRange)
                {
                    await _combatSystem.ResolveCombat(mover, enemy);

                    // No need to check if player died - player attacks happen after enemy attacks
                }
            }

            return false; // Unit survived
        }

        private bool IsInAttackRange(Entity attacker, Vector3I attackerCoord, Vector3I targetCoord)
        {
            if (attacker == null) return false;
            return RangeSystem.GetAttackRangeTiles(attacker, attackerCoord).Contains(targetCoord);
        }
    }
}
