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
        private StandardMaterial3D _dashRangeMaterial;
        private Entity _selectedTile;
        private DashSystem _dashSystem;

        // Track currently hovered tile
        private Entity _lastHoveredTile;

        // Mesh caching for material application
        private readonly Dictionary<int, List<MeshInstance3D>> _tileMeshCache = new();

        public override void Initialize()
        {
            _highlightMaterial = ResourceLoader.Load<StandardMaterial3D>("res://assets/materials/HexTileHighlight.tres");
            _selectedMaterial = ResourceLoader.Load<StandardMaterial3D>("res://assets/materials/HexTileSelect.tres");
            _defaultMaterial = ResourceLoader.Load<StandardMaterial3D>("res://assets/materials/HexTileBase.tres");
            _attackRangeMaterial = ResourceLoader.Load<StandardMaterial3D>("res://assets/materials/HexTileAttackRange.tres");

            // Create dash range material (blue)
            _dashRangeMaterial = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.2f, 0.5f, 1.0f, 0.6f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };

            _dashSystem = Systems.Get<DashSystem>();

            Events.TileHover += OnTileHover;
            Events.TileUnhover += OnTileUnhover;
            Events.UnitHover += OnUnitHover;
            Events.UnitUnhover += OnUnitUnhover;
            Events.TurnChanged += OnTurnChanged;
        }

        /// <summary>
        /// Clears the mesh cache - called after rewind when visual nodes are rebuilt
        /// </summary>
        public void ClearMeshCache()
        {
            _tileMeshCache.Clear();
            _highlightedTiles.Clear();
            _selectedTile = null;
        }

        private void OnTileHover(Entity tile)
        {
            // Skip if same tile or already highlighted
            if (tile == _lastHoveredTile)
            {
                return;
            }

            // Clear previous hover highlight
            ClearHighlightedTiles();

            _lastHoveredTile = tile;

            // Only highlight traversable tiles
            if (tile != null && tile.Has<Traversable>() && tile != _selectedTile)
            {
                SetTileMaterial(tile, _highlightMaterial);
                _highlightedTiles.Add(tile);
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

                // Highlight all tiles in attack range
                foreach (var coord in RangeSystem.GetAttackRangeTiles(unit, unitCoord))
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

        private void OnTurnChanged(Entity unit)
        {
            // Update dash range visualization when turn changes or dash mode toggles
            if (unit.Has<Player>() && unit.Has<DashModeActive>())
            {
                UpdateDashRangeVisualization(unit);
            }
            else
            {
                // Clear dash highlights if not in dash mode
                ClearHighlightedTiles();
            }
        }

        /// <summary>
        /// Public method to update dash visualization (called from UI)
        /// </summary>
        public void RefreshDashVisualization()
        {
            var player = Entities.Query<Player>().FirstOrDefault();
            if (player != null && player.Has<DashModeActive>())
            {
                UpdateDashRangeVisualization(player);
            }
            else
            {
                ClearHighlightedTiles();
            }
        }

        private void UpdateDashRangeVisualization(Entity player)
        {
            // Clear previous highlights
            ClearHighlightedTiles();

            // Get dash range tiles
            var dashTiles = _dashSystem.GetDashRangeTiles(player.Get<Coordinate>());

            // Highlight all dash range tiles in blue
            foreach (var coord in dashTiles)
            {
                var tile = Entities.GetAt(coord);
                if (tile != null)
                {
                    SetTileMaterial(tile, _dashRangeMaterial);
                    _highlightedTiles.Add(tile);
                }
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
            if (tileNode is not Node3D node) return;

            // Check cache first
            if (!_tileMeshCache.TryGetValue(tile.Id, out var meshes))
            {
                meshes = new List<MeshInstance3D>();
                var meshContainer = node.GetNode<Node3D>("Mesh");
                if (meshContainer != null)
                {
                    CollectMeshes(meshContainer, meshes);
                }
                _tileMeshCache[tile.Id] = meshes;
            }

            // Apply material to cached meshes
            foreach (var mesh in meshes)
            {
                mesh.MaterialOverride = material;
            }
        }

        private void CollectMeshes(Node node, List<MeshInstance3D> meshes)
        {
            if (node is MeshInstance3D meshInstance && meshInstance.Mesh != null)
            {
                meshes.Add(meshInstance);
            }

            foreach (Node child in node.GetChildren())
            {
                CollectMeshes(child, meshes);
            }
        }

        private void ClearTileMaterial(Entity tile)
        {
            if (_tileMeshCache.TryGetValue(tile.Id, out var meshes))
            {
                foreach (var mesh in meshes)
                {
                    mesh.MaterialOverride = null;
                }
            }
        }

        public override void Cleanup()
        {
            // EventBus.Instance.TileSelect -= OnTileSelect;
            Events.TileHover -= OnTileHover;
            Events.TileUnhover -= OnTileUnhover;
            Events.UnitHover -= OnUnitHover;
            Events.UnitUnhover -= OnUnitUnhover;
            Events.TurnChanged -= OnTurnChanged;
            ClearSelection();
        }
    }
}
