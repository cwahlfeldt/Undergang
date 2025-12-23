using System.Collections.Generic;
using System.Linq;
using Game.Components;
using Godot;

namespace Game
{
    public class DashSystem : System
    {
        private TileHighlightSystem _tileHighlightSystem;

        public override void Initialize()
        {
            _tileHighlightSystem = Systems.Get<TileHighlightSystem>();
            Events.TurnChanged += OnTurnChanged;

            // Initialize player with dash ability (cooldown = 0, ready to use)
            var player = Entities.Query<Player>().FirstOrDefault();
            if (player != null && !player.Has<DashCooldown>())
            {
                player.Add(new DashCooldown(0));
            }
        }

        /// <summary>
        /// Toggle dash mode on/off for the player
        /// </summary>
        public void ToggleDashMode()
        {
            var player = Entities.Query<Player>().FirstOrDefault();
            if (player == null) return;

            // Can't enable dash mode if on cooldown
            if (!player.Has<DashModeActive>() && !IsDashAvailable(player))
            {
                return;
            }

            if (player.Has<DashModeActive>())
            {
                // Disable dash mode
                player.Remove<DashModeActive>();
                ClearDashRangeTiles();
            }
            else
            {
                // Enable dash mode
                player.Add(new DashModeActive());
                UpdateDashRangeTiles(player);
            }
        }

        /// <summary>
        /// Check if dash is available (not on cooldown)
        /// </summary>
        public bool IsDashAvailable(Entity player)
        {
            if (!player.Has<DashCooldown>()) return true;
            return player.Get<DashCooldown>() == 0;
        }

        /// <summary>
        /// Get remaining cooldown turns
        /// </summary>
        public int GetRemainingCooldown(Entity player)
        {
            if (!player.Has<DashCooldown>()) return 0;
            return player.Get<DashCooldown>();
        }

        /// <summary>
        /// Calculate valid dash destinations (all tiles within dash range radius)
        /// </summary>
        public List<Vector3I> GetDashRangeTiles(Vector3I fromPosition)
        {
            var tiles = new List<Vector3I>();

            // Get all tiles within dash range (radius)
            var tilesInRange = HexGrid.GetHexesInRange(fromPosition, Config.DashRange);

            foreach (var coord in tilesInRange)
            {
                // Skip the center tile (current position)
                if (coord == fromPosition)
                    continue;

                // Check if tile exists and is traversable
                var tile = Entities.GetAt(coord);
                if (tile != null && tile.Has<Traversable>())
                {
                    tiles.Add(coord);
                }
            }

            return tiles;
        }

        /// <summary>
        /// Execute a dash move
        /// </summary>
        public void ExecuteDash(Entity player, Vector3I destination)
        {
            // Start cooldown
            player.Update(new DashCooldown(Config.DashCooldown));

            // Disable dash mode
            player.Remove<DashModeActive>();
            ClearDashRangeTiles();

            // Clear visual highlights
            _tileHighlightSystem.RefreshDashVisualization();
        }

        /// <summary>
        /// Check if a destination is a valid dash target
        /// </summary>
        public bool IsValidDashDestination(Vector3I from, Vector3I to)
        {
            var validTiles = GetDashRangeTiles(from);
            return validTiles.Contains(to);
        }

        /// <summary>
        /// Update dash range tile markers
        /// </summary>
        private void UpdateDashRangeTiles(Entity player)
        {
            ClearDashRangeTiles();

            if (!player.Has<DashModeActive>()) return;

            var playerPos = player.Get<Coordinate>();
            var dashTiles = GetDashRangeTiles(playerPos);

            foreach (var coord in dashTiles)
            {
                var tile = Entities.GetAt(coord);
                if (tile != null)
                {
                    tile.Add(new DashRangeTile());
                }
            }
        }

        /// <summary>
        /// Clear all dash range tile markers
        /// </summary>
        private void ClearDashRangeTiles()
        {
            foreach (var tile in Entities.Query<DashRangeTile>())
            {
                tile.Remove<DashRangeTile>();
            }
        }

        /// <summary>
        /// Reduce cooldown when player's turn starts
        /// </summary>
        private void OnTurnChanged(Entity unit)
        {
            // Only process when player's turn starts
            if (!unit.Has<Player>()) return;

            if (unit.Has<DashCooldown>())
            {
                var currentCooldown = unit.Get<DashCooldown>();
                if (currentCooldown > 0)
                {
                    unit.Update(new DashCooldown(currentCooldown - 1));
                }
            }

            // Update dash range if dash mode is active
            if (unit.Has<DashModeActive>())
            {
                UpdateDashRangeTiles(unit);
            }
        }

        public override void Cleanup()
        {
            Events.TurnChanged -= OnTurnChanged;
        }
    }
}
