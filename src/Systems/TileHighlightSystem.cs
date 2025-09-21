using System.Collections.Generic;
using Godot;
using System.Threading.Tasks;
using System;
using Game.Components;
using System.Linq;

namespace Game
{
    public class TileHighlightSystem : System
    {
        private readonly HashSet<Entity> _highlightedTiles = [];
        private StandardMaterial3D _highlightMaterial;
        private StandardMaterial3D _selectedMaterial;
        private StandardMaterial3D _defaultMaterial;
        private Entity _selectedTile;

        public override void Initialize()
        {
            _highlightMaterial = ResourceLoader.Load<StandardMaterial3D>("res://assets/materials/HexTileHighlight.tres");
            _selectedMaterial = ResourceLoader.Load<StandardMaterial3D>("res://assets/materials/HexTileSelect.tres");
            _defaultMaterial = ResourceLoader.Load<StandardMaterial3D>("res://assets/materials/HexTileBase.tres");

            Events.TileHover += OnTileHover;
            Events.TileUnhover += OnTileUnhover;
        }

        private void OnTileHover(Entity tile)
        {
            if (
                tile != _selectedTile &&
                !_highlightedTiles.Contains(tile))
            {
                var player = Entities.Query<Player>().FirstOrDefault();

                if (player.Has<CurrentTurn>())
                {
                    var path = PathFinder.FindPath(player.Get<Coordinate>(), tile.Get<Coordinate>(), player.Get<MoveRange>());

                    if (path.Count > 0)
                    {
                        // Clear previous highlights first
                        ClearHighlightedTiles();

                        // Highlight new tiles and add them to tracking
                        foreach (Vector3I t in path)
                        {
                            var tileTile = Entities.GetAt(t);

                            if (tileTile.Get<Coordinate>() != player.Get<Coordinate>())
                            {
                                SetTileMaterial(tileTile, _highlightMaterial);
                                _highlightedTiles.Add(tileTile); // Add to tracking
                            }
                        }
                    }
                }
            }
        }

        private void OnTileUnhover(Entity tile)
        {
            if (tile != null &&
                tile != _selectedTile)
            {
                ClearHighlightedTiles();
            }
        }

        private void ClearHighlightedTiles()
        {
            foreach (Entity t in _highlightedTiles)
            {
                SetTileMaterial(t, _defaultMaterial);
            }
            _highlightedTiles.Clear(); // Clear the tracking list
        }

        // private void OnTileSelect(Entity tile)
        // {
        //     if (tile.Get<TileComponent>().Type != TileType.Blocked)
        //     {
        //         SelectTile(tile);
        //     }
        // }

        public async void SelectTile(Entity entity)
        {
            ClearSelection();
            _selectedTile = entity;
            SetTileMaterial(_selectedTile, _selectedMaterial);
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            ClearSelection();
        }

        // private void SelectMoveRangeTiles(Entity entity)
        // {
        //     ClearSelection();
        //     var moveRangeMat = ResourceLoader.Load<StandardMaterial3D>("res://assets/materials/HexTileMoveRange.tres");

        //     // Highlight neighboring tiles
        //     // var rangedTiles = HexGrid.GetHexesInRange(entity.Get<HexCoordComponent>().Coord, entity.Get<MoveRangeComponent>().MoveRange);
        //     var rangedTiles = Entities
        //         .GetTilesInRange(entity.Get<TileComponent>().Coord, entity.Get<UnitComponent>().MoveRange);
        //     foreach (var tile in rangedTiles)
        //     {
        //         if (tile != _selectedTile)
        //         {
        //             _highlightedTiles.Add(tile);
        //             SetTileMaterial(tile, moveRangeMat);
        //         }
        //     }
        // }

        private void ClearSelection()
        {
            if (_selectedTile != null)
            {
                SetTileMaterial(_selectedTile, _defaultMaterial);
                _selectedTile = null;
            }

            foreach (var tile in _highlightedTiles)
            {
                SetTileMaterial(tile, _defaultMaterial);
            }
            _highlightedTiles.Clear();
        }

        private void SetTileMaterial(Entity tile, StandardMaterial3D material)
        {
            var tileNode = tile.Get<Instance>().Node;
            if (tileNode is Node3D node)
            {
                var mesh = node.GetNode<MeshInstance3D>("Mesh");
                if (mesh != null)
                {
                    mesh.MaterialOverride = material;
                }
            }
        }

        public override void Cleanup()
        {
            // EventBus.Instance.TileSelect -= OnTileSelect;
            Events.TileHover -= OnTileHover;
            Events.TileUnhover -= OnTileUnhover;
            ClearSelection();
        }
    }
}
