using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Components;
using Godot;

namespace Game
{
    public class PlayerSystem : System
    {
        public override void Initialize()
        {
            Events.TileSelect += OnTileSelect;
            Events.OnUnitActionComplete += OnUnitActionComplete;
        }

        public override async Task Update()
        {
            var selectedTile = Entities.Query<SelectedTile>().FirstOrDefault();
            var player = Entities.Query<Player>().FirstOrDefault();

            if (selectedTile == null ||
                selectedTile.Get<Coordinate>() == player.Get<Coordinate>() ||
                player.Has<Movement>() ||
                player.Has<Dash>() ||
                !player.Has<WaitingForAction>())
                return;

            var destination = selectedTile.Get<Coordinate>();
            var origin = player.Get<Coordinate>();

            // Check if player is in dash mode
            if (player.Has<DashMode>())
            {
                // Validate dash target
                if (IsValidDashTarget(origin, destination))
                {
                    player.Add(new Dash(origin, destination));
                    player.Remove<WaitingForAction>();
                    player.Remove<DashMode>(); // Exit dash mode after initiating dash
                    GD.Print($"Dash initiated from {origin} to {destination}");
                }
                else
                {
                    GD.Print($"Invalid dash target: {destination}");
                }
            }
            else
            {
                // Normal movement
                player.Add(new Movement(origin, destination));
                player.Remove<WaitingForAction>();
            }
        }

        /// <summary>
        /// Check if the target is a valid dash destination (exactly 2 tiles away in a straight hex line)
        /// </summary>
        private bool IsValidDashTarget(Vector3I from, Vector3I to)
        {
            // Check each of the 6 hex directions
            foreach (var dir in HexGrid.Directions.Values)
            {
                var dashTarget = from + (dir * 2);
                if (dashTarget == to)
                {
                    // Valid dash target - 2 tiles in a straight line
                    var destinationTile = Entities.GetAt(to);
                    return destinationTile != null && destinationTile.Has<Traversable>();
                }
            }
            return false;
        }

        private async void OnTileSelect(Entity entity)
        {
            ClearSelectedTiles();
            var player = Entities.Query<Player>().FirstOrDefault();

            if (!player.Has<WaitingForAction>() || player.Has<Movement>())
                return;

            if (entity.Has<Tile>() && !entity.Has<SelectedTile>() && entity.Has<Traversable>())
            {
                ClearSelectedTiles();
                entity.Add(new SelectedTile());
                await Systems.Update();
            }
        }

        private void OnUnitActionComplete(Entity _)
        {
            ClearSelectedTiles();
        }

        private void ClearSelectedTiles()
        {
            foreach (var tile in Entities.Query<SelectedTile>())
                tile.Remove<SelectedTile>();
        }

        public override void Cleanup()
        {
            Events.TileSelect -= OnTileSelect;
            Events.OnUnitActionComplete -= OnUnitActionComplete;
        }
    }
}