using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Game.Components;
using Godot;

namespace Game
{
    public class EnemySystem : System
    {
        private CombatSystem _combatSystem;

        public override void Initialize()
        {
            _combatSystem = Systems.Get<CombatSystem>();
        }

        public override async Task Update()
        {
            var enemy = Entities.Query<Enemy, CurrentTurn>().FirstOrDefault();
            var player = Entities.Query<Player>().FirstOrDefault();

            if (enemy == null || player == null)
                return;

            // Only act if enemy is waiting for action
            if (!enemy.Has<WaitingForAction>() || enemy.Has<Movement>())
                return;

            var enemyCoord = enemy.Get<Coordinate>();
            var playerCoord = player.Get<Coordinate>();

            // In Hoplite-style combat, enemies ONLY attack when player moves into their range
            // On the enemy's turn, they ONLY move (they don't attack proactively)
            // This creates the tactical puzzle where the player must avoid enemy threat zones

            // Check if player is within this enemy's attack range (uses enemy's range type)
            var attackRangeTiles = RangeSystem.GetAttackRangeTiles(enemy, enemyCoord).ToList();
            bool playerInRange = attackRangeTiles.Contains(playerCoord);

            if (playerInRange)
            {
                // Player is already in range - enemy just waits/passes turn
                // (Enemy already attacked when player moved into range on player's turn)
                GD.Print($"Enemy {enemy.Id} passes turn (player already in range)");
                enemy.Remove<WaitingForAction>();
                Events.UnitActionComplete(enemy);
            }
            else
            {
                // Different behavior for different enemy types
                if (enemy.Has<Sniper>())
                {
                    // Sniper AI: Move to position that is in range 2-5 of player AND closest to range 3
                    var targetPosition = FindSniperTargetPosition(enemyCoord, playerCoord, enemy.Get<MoveRange>());
                    GD.Print($"Sniper {enemy.Id} moves towards ideal position");
                    enemy.Add(new Movement(enemyCoord, targetPosition));
                }
                else
                {
                    // Grunt AI: Simple move directly towards player
                    GD.Print($"Enemy {enemy.Id} moves towards player");
                    enemy.Add(new Movement(enemyCoord, playerCoord));
                }

                enemy.Remove<WaitingForAction>();
            }
        }

        /// <summary>
        /// Find ideal position for Sniper: in range 2-5 of player, closest to range 3
        /// Based on Hoplite Archer AI behavior
        /// </summary>
        private Vector3I FindSniperTargetPosition(Vector3I sniperCoord, Vector3I playerCoord, int moveRange)
        {
            // Get all tiles in the 6 directions from player at range 2-5
            var idealPositions = new List<Vector3I>();

            foreach (var direction in HexGrid.Directions.Values)
            {
                for (int distance = 2; distance <= 5; distance++)
                {
                    var position = playerCoord + direction * distance;

                    // Check if tile exists and is traversable
                    var tile = Entities.GetAt(position);
                    if (tile != null && tile.Has<Traversable>())
                    {
                        // Check if tile is not occupied by another unit
                        var occupant = Entities.Query<Unit, Coordinate>()
                            .FirstOrDefault(u => u.Get<Coordinate>() == position);

                        if (occupant == null)
                        {
                            idealPositions.Add(position);
                        }
                    }
                }
            }

            // If we found ideal positions, pick the one closest to range 3 from player
            if (idealPositions.Any())
            {
                // Sort by: 1) How close to range 3, 2) How close to sniper current position
                var bestPosition = idealPositions
                    .OrderBy(pos => Math.Abs(HexGrid.GetDistance(pos, playerCoord) - 3))
                    .ThenBy(pos => HexGrid.GetDistance(pos, sniperCoord))
                    .First();

                return bestPosition;
            }

            // Fallback: If no ideal position found, move toward range 3 from player
            // Find a position that reduces distance to the "ring" at range 3
            var allReachableTiles = HexGrid.GetHexesInRange(sniperCoord, moveRange);
            var validTiles = allReachableTiles
                .Where(pos =>
                {
                    var tile = Entities.GetAt(pos);
                    if (tile == null || !tile.Has<Traversable>())
                        return false;

                    var occupant = Entities.Query<Unit, Coordinate>()
                        .FirstOrDefault(u => u.Get<Coordinate>() == pos);

                    return occupant == null || pos == sniperCoord;
                })
                .ToList();

            if (validTiles.Any())
            {
                // Move toward range 3 from player
                var bestFallback = validTiles
                    .OrderBy(pos => Math.Abs(HexGrid.GetDistance(pos, playerCoord) - 3))
                    .First();

                return bestFallback;
            }

            // Last resort: stay in place
            return sniperCoord;
        }
    }
}