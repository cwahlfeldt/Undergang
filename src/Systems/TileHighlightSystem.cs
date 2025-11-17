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
        private StandardMaterial3D _attackRangeMaterial;
        private Entity _selectedTile;

        public override void Initialize()
        {
            _highlightMaterial = ResourceLoader.Load<StandardMaterial3D>("res://assets/materials/HexTileHighlight.tres");
            _selectedMaterial = ResourceLoader.Load<StandardMaterial3D>("res://assets/materials/HexTileSelect.tres");
            _defaultMaterial = ResourceLoader.Load<StandardMaterial3D>("res://assets/materials/HexTileBase.tres");
            _attackRangeMaterial = ResourceLoader.Load<StandardMaterial3D>("res://assets/materials/HexTileAttackRange.tres");

            Events.TileHover += OnTileHover;
            Events.TileUnhover += OnTileUnhover;
            Events.UnitHover += OnUnitHover;
            Events.UnitUnhover += OnUnitUnhover;
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

        private void OnUnitHover(Entity unit)
        {
            if (unit != null && unit.Has<Unit>())
            {
                // Clear any previous highlights
                ClearHighlightedTiles();

                // Get the unit's attack range tiles
                var unitCoord = unit.Get<Coordinate>();
                var attackRangeTiles = RangeSystem.GetAttackRangeTiles(unit, unitCoord).ToList();

                // Highlight all tiles in attack range
                foreach (var coord in attackRangeTiles)
                {
                    var tile = Entities.GetAt(coord);
                    if (tile != null && tile.Has<Traversable>())
                    {
                        SetTileMaterial(tile, _attackRangeMaterial);
                        _highlightedTiles.Add(tile);
                    }
                }
            }
        }

        private void OnUnitUnhover(Entity unit)
        {
            if (unit != null)
            {
                ClearHighlightedTiles();
            }
        }

        private void ClearHighlightedTiles()
        {
            foreach (Entity t in _highlightedTiles)
            {
                ClearTileMaterial(t);
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
                ClearTileMaterial(_selectedTile);
                _selectedTile = null;
            }

            foreach (var tile in _highlightedTiles)
            {
                ClearTileMaterial(tile);
            }
            _highlightedTiles.Clear();
        }

        private void SetTileMaterial(Entity tile, StandardMaterial3D material)
        {
            var tileNode = tile.Get<Instance>().Node;
            if (tileNode is Node3D node)
            {
                // Find the "Mesh" node (which may be a container for GLTF instances)
                var meshContainer = node.GetNode<Node3D>("Mesh");
                if (meshContainer != null)
                {
                    // Recursively apply material to all MeshInstance3D children
                    // This handles GLTF instances that have nested mesh structures
                    ApplyMaterialToMeshes(meshContainer, material);
                }
            }
        }

        private void ApplyMaterialToMeshes(Node node, StandardMaterial3D material)
        {
            // If this node is a MeshInstance3D with a mesh, apply the material
            if (node is MeshInstance3D meshInstance && meshInstance.Mesh != null)
            {
                meshInstance.MaterialOverride = material;
            }

            // Recursively process all children
            foreach (Node child in node.GetChildren())
            {
                ApplyMaterialToMeshes(child, material);
            }
        }

        private void ClearTileMaterial(Entity tile)
        {
            var tileNode = tile.Get<Instance>().Node;
            if (tileNode is Node3D node)
            {
                // Find the "Mesh" node (which may be a container for GLTF instances)
                var meshContainer = node.GetNode<Node3D>("Mesh");
                if (meshContainer != null)
                {
                    // Recursively clear material overrides on all MeshInstance3D children
                    // This restores the original GLTF materials (textures)
                    ClearMaterialOverrides(meshContainer);
                }
            }
        }

        private void ClearMaterialOverrides(Node node)
        {
            // If this node is a MeshInstance3D, clear its material override
            if (node is MeshInstance3D meshInstance && meshInstance.Mesh != null)
            {
                meshInstance.MaterialOverride = null;
            }

            // Recursively process all children
            foreach (Node child in node.GetChildren())
            {
                ClearMaterialOverrides(child);
            }
        }

        public override void Cleanup()
        {
            // EventBus.Instance.TileSelect -= OnTileSelect;
            Events.TileHover -= OnTileHover;
            Events.TileUnhover -= OnTileUnhover;
            Events.UnitHover -= OnUnitHover;
            Events.UnitUnhover -= OnUnitUnhover;
            ClearSelection();
        }
    }
}
