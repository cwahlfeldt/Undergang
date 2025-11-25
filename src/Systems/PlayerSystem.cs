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
        /// Check if the target is a valid dash destination (within 2-tile radius)
        /// </summary>
        private bool IsValidDashTarget(Vector3I from, Vector3I to)
        {
            // Get the distance between origin and destination
            var distance = HexGrid.GetDistance(from, to);

            // Must be within 2 tiles but not the current tile
            if (distance < 1 || distance > 2)
                return false;

            // Destination must be traversable
            var destinationTile = Entities.GetAt(to);
            return destinationTile != null && destinationTile.Has<Traversable>();
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