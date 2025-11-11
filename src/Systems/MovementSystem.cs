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

        public override async Task Update()
        {
            var mover = Entities.Query<Movement, CurrentTurn>().FirstOrDefault();

            if (mover == null)
                return;

            var (from, to) = mover.Get<Movement>();
            var path = PathFinder.FindPath(from, to, mover.Get<MoveRange>());

            // Check for combat along the path
            bool unitDefeated = await ProcessMovementWithCombat(mover, path);

            if (unitDefeated)
            {
                // Unit was defeated during movement, don't complete the action
                mover.Remove<Movement>();
                return;
            }

            var fromTile = Entities.GetAt(path.First());
            var toTile = Entities.GetAt(path.Last());

            mover.Remove<Movement>();

            Events.OnMoveCompleted(mover, fromTile.Get<Coordinate>(), toTile.Get<Coordinate>());
            Events.UnitActionComplete(mover);
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
                enemyInRange = CheckIfPlayerWasInEnemyRange(origin);
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
            // Only trigger if player moved into a NEW threat zone (wasn't already there)
            if (destinationTile != null && destinationTile.Has<AttackRangeTile>() && mover.Has<Player>() && mover.Has<CurrentTurn>())
            {
                int attackerId = destinationTile.Get<AttackRangeTile>();
                Entity attacker = Entities.GetEntity(attackerId);

                // Only attack if this is a DIFFERENT enemy than the one we were already fighting
                // OR if we weren't fighting anyone before
                if (attacker != null && attacker.Has<Enemy>() && (enemyInRange == null || enemyInRange.Id != attacker.Id))
                {
                    GD.Print($"Player moved into enemy {attackerId} attack range at {destination}!");
                    await _combatSystem.ResolveCombat(attacker, mover);

                    // Check if player was defeated
                    if (!mover.Has<Health>() || mover.Get<Health>() <= 0)
                    {
                        GD.Print("Player defeated by enemy attack!");
                        return true; // Unit defeated
                    }
                }
            }

            // PLAYER ATTACKS: Player attacks if they were already in range and moved to another tile in range
            if (enemyInRange != null && mover.Has<Player>() && mover.Has<CurrentTurn>())
            {
                // Check if player is still in range of that enemy at destination
                if (IsInAttackRange(destination, enemyInRange.Get<Coordinate>()))
                {
                    if (_combatSystem.CanAttack(mover, enemyInRange))
                    {
                        GD.Print($"Player attacks enemy {enemyInRange.Id} while moving within attack range!");
                        await _combatSystem.ResolveCombat(mover, enemyInRange);
                    }
                }
            }

            return false; // Unit survived
        }

        /// <summary>
        /// Check if player is currently in attack range of any enemy
        /// </summary>
        private Entity CheckIfPlayerWasInEnemyRange(Vector3I playerCoord)
        {
            var player = Entities.Query<Player>().FirstOrDefault();
            if (player == null) return null;

            // Get all tiles within player's attack range (based on player's range type)
            var attackRangeTiles = RangeSystem.GetAttackRangeTiles(player, playerCoord).ToList();

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
        private bool IsInAttackRange(Vector3I attackerCoord, Vector3I targetCoord)
        {
            var player = Entities.Query<Player>().FirstOrDefault();
            if (player == null) return false;

            var tilesInRange = RangeSystem.GetAttackRangeTiles(player, attackerCoord).ToList();
            return tilesInRange.Contains(targetCoord);
        }
    }
}
