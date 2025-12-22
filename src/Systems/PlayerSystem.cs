using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Components;
using Godot;

namespace Game
{
    public class PlayerSystem : System
    {
        private TurnSystem _turnSystem;

        public override void Initialize()
        {
            _turnSystem = Systems.Get<TurnSystem>();
            Events.TileSelect += OnTileSelect;
        }

        private async void OnTileSelect(Entity tile)
        {
            var player = Entities.Query<Player>().FirstOrDefault();

            // Validation
            if (player == null || !player.Has<WaitingForAction>())
                return;

            // Don't allow player actions if they're dead
            if (player.Has<Health>() && player.Get<Health>() <= 0)
                return;

            if (!tile.Has<Tile>() || !tile.Has<Traversable>())
                return;

            var destination = tile.Get<Coordinate>();
            var playerCoord = player.Get<Coordinate>();

            // Don't allow moving to current position
            if (destination == playerCoord)
                return;

            // Direct orchestration - clear and traceable
            await _turnSystem.ExecutePlayerAction(player, destination);
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
        }
    }
}