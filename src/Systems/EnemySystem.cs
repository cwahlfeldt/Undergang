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

                if (IsRangedUnitType(unit))
                {
                    targetPosition = FindSniperTargetPosition(unit, enemyCoord, playerCoord, unit.Get<MoveRange>());
                }
                else
                {
                    targetPosition = playerCoord;  // Grunt: move toward player
                }

                // Execute movement
                await _turnSystem.ExecuteEnemyAction(unit, targetPosition);
            }
        }

        /// <summary>
        /// Check if unit is any sniper variant (including Wizard)
        /// </summary>
        private bool IsRangedUnitType(Entity unit)
        {
            return unit.Has<Wizard>() ||
                   unit.Has<SniperAxisQ>() ||
                   unit.Has<SniperAxisR>() ||
                   unit.Has<SniperAxisS>();
        }

        /// <summary>
        /// Find ideal position for Sniper: in their attack range of player, closest to range 3
        /// Based on Hoplite Archer AI behavior
        /// </summary>
        private Vector3I FindSniperTargetPosition(Entity sniper, Vector3I sniperCoord, Vector3I playerCoord, int moveRange)
        {
            // Get all tiles that would be in this sniper's attack range from the player's position
            // This uses the sniper's specific range pattern (diagonal, axis Q/R/S, etc.)
            var idealPositions = new List<Vector3I>();

            // Get the sniper's attack range pattern from player's perspective
            // We want positions where if the sniper stood there, the player would be in range
            var potentialAttackRangeTiles = RangeSystem.GetAttackRangeTiles(sniper, playerCoord);

            foreach (var position in potentialAttackRangeTiles)
            {
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