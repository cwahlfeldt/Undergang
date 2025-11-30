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
        private TurnSystem _turnSystem;

        public override void Initialize()
        {
            _turnSystem = Systems.Get<TurnSystem>();
            Events.TurnChanged += OnTurnChanged;
        }

        private async void OnTurnChanged(Entity unit)
        {
            // Only process enemy turns
            if (!unit.Has<Enemy>())
                return;

            var player = Entities.Query<Player>().FirstOrDefault();
            if (player == null)
                return;

            var enemyCoord = unit.Get<Coordinate>();
            var playerCoord = player.Get<Coordinate>();

            // Check if player is in attack range
            var attackRangeTiles = RangeSystem.GetAttackRangeTiles(unit, enemyCoord);
            bool playerInRange = attackRangeTiles.Contains(playerCoord);

            if (playerInRange)
            {
                // Pass turn - player already in range
                _turnSystem.ExecuteEnemyPass(unit);
            }
            else
            {
                // Determine movement target based on enemy type
                Vector3I targetPosition;

                if (unit.Has<Sniper>())
                {
                    targetPosition = FindSniperTargetPosition(enemyCoord, playerCoord, unit.Get<MoveRange>());
                    GD.Print($"Sniper {unit.Id} moves towards ideal position");
                }
                else
                {
                    targetPosition = playerCoord;  // Grunt: move toward player
                    GD.Print($"Enemy {unit.Id} moves towards player");
                }

                // Execute movement
                await _turnSystem.ExecuteEnemyAction(unit, targetPosition);
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

        public override void Cleanup()
        {
            Events.TurnChanged -= OnTurnChanged;
        }
    }
}