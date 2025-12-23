using System.Collections.Generic;
using Godot;
using System.Threading.Tasks;
using System;
using Game.Components;
using System.Linq;

namespace Game
{
    /// <summary>
    /// Highlight layers in priority order (higher = displayed on top)
    /// </summary>
    public enum HighlightLayer
    {
        None = 0,
        DashRange = 1,
        AttackRange = 2,
        Hover = 3,
        Selected = 4
    }

    public class TileHighlightSystem : System
    {
        // Track which layers are active on each tile
        private readonly Dictionary<int, HashSet<HighlightLayer>> _tileHighlights = new();

        // Materials for each layer
        private readonly Dictionary<HighlightLayer, StandardMaterial3D> _layerMaterials = new();

        private Entity _lastHoveredTile;
        private Entity _selectedTile;
        private DashSystem _dashSystem;

        // Mesh caching for material application
        private readonly Dictionary<int, List<MeshInstance3D>> _tileMeshCache = new();

        public override void Initialize()
        {
            // Load/create materials for each layer
            _layerMaterials[HighlightLayer.Hover] = ResourceLoader.Load<StandardMaterial3D>("res://assets/materials/HexTileHighlight.tres");
            _layerMaterials[HighlightLayer.Selected] = ResourceLoader.Load<StandardMaterial3D>("res://assets/materials/HexTileSelect.tres");
            _layerMaterials[HighlightLayer.AttackRange] = ResourceLoader.Load<StandardMaterial3D>("res://assets/materials/HexTileAttackRange.tres");

            // Dash range - subtle blue-gray similar to hover
            _layerMaterials[HighlightLayer.DashRange] = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.5f, 0.6f, 0.8f, 0.8f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };

            _dashSystem = Systems.Get<DashSystem>();

            Events.TileHover += OnTileHover;
            Events.TileUnhover += OnTileUnhover;
            Events.UnitHover += OnUnitHover;
            Events.UnitUnhover += OnUnitUnhover;
        }

        /// <summary>
        /// Clears the mesh cache - called after rewind when visual nodes are rebuilt
        /// </summary>
        public void ClearMeshCache()
        {
            _tileMeshCache.Clear();
            _tileHighlights.Clear();
            _selectedTile = null;
            _lastHoveredTile = null;
        }

        #region Event Handlers

        private void OnTileHover(Entity tile)
        {
            if (tile == _lastHoveredTile) return;

            // Remove hover from previous tile
            if (_lastHoveredTile != null)
            {
                RemoveHighlight(_lastHoveredTile, HighlightLayer.Hover);
            }

            _lastHoveredTile = tile;

            // Add hover to new tile if traversable
            if (tile != null && tile.Has<Traversable>())
            {
                AddHighlight(tile, HighlightLayer.Hover);
            }
        }

        private void OnTileUnhover(Entity tile)
        {
            // Keep hover visible until next tile is hovered
        }

        private void OnUnitHover(Entity unit)
        {
            if (unit == null || !unit.Has<Unit>()) return;

            // Clear any previous attack range highlights
            ClearLayer(HighlightLayer.AttackRange);

            // Highlight all tiles in attack range
            var unitCoord = unit.Get<Coordinate>();
            foreach (var coord in RangeSystem.GetAttackRangeTiles(unit, unitCoord))
            {
                var tile = Entities.GetAt(coord);
                if (tile != null && tile.Has<Traversable>())
                {
                    AddHighlight(tile, HighlightLayer.AttackRange);
                }
            }
        }

        private void OnUnitUnhover(Entity unit)
        {
            if (unit != null)
            {
                ClearLayer(HighlightLayer.AttackRange);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Update dash range visualization - call when entering/exiting dash mode
        /// </summary>
        public void RefreshDashVisualization()
        {
            // Always clear existing dash highlights first
            ClearLayer(HighlightLayer.DashRange);

            var player = Entities.Query<Player>().FirstOrDefault();
            if (player != null && player.Has<DashModeActive>())
            {
                // Show dash range tiles
                var dashTiles = _dashSystem.GetDashRangeTiles(player.Get<Coordinate>());
                foreach (var coord in dashTiles)
                {
                    var tile = Entities.GetAt(coord);
                    if (tile != null)
                    {
                        AddHighlight(tile, HighlightLayer.DashRange);
                    }
                }
            }
        }

        /// <summary>
        /// Briefly highlight a tile as selected (for click feedback)
        /// </summary>
        public async void SelectTile(Entity entity)
        {
            if (_selectedTile != null)
            {
                RemoveHighlight(_selectedTile, HighlightLayer.Selected);
            }

            _selectedTile = entity;
            AddHighlight(_selectedTile, HighlightLayer.Selected);

            await Task.Delay(Config.TileSelectDurationMs);

            if (_selectedTile == entity)
            {
                RemoveHighlight(_selectedTile, HighlightLayer.Selected);
                _selectedTile = null;
            }
        }

        #endregion

        #region Core Highlight Logic

        /// <summary>
        /// Add a highlight layer to a tile
        /// </summary>
        private void AddHighlight(Entity tile, HighlightLayer layer)
        {
            if (tile == null) return;

            if (!_tileHighlights.TryGetValue(tile.Id, out var layers))
            {
                layers = new HashSet<HighlightLayer>();
                _tileHighlights[tile.Id] = layers;
            }

            layers.Add(layer);
            UpdateTileMaterial(tile);
        }

        /// <summary>
        /// Remove a highlight layer from a tile
        /// </summary>
        private void RemoveHighlight(Entity tile, HighlightLayer layer)
        {
            if (tile == null) return;

            if (_tileHighlights.TryGetValue(tile.Id, out var layers))
            {
                layers.Remove(layer);
                UpdateTileMaterial(tile);

                // Clean up empty entries
                if (layers.Count == 0)
                {
                    _tileHighlights.Remove(tile.Id);
                }
            }
        }

        /// <summary>
        /// Clear all tiles with a specific highlight layer
        /// </summary>
        private void ClearLayer(HighlightLayer layer)
        {
            // Get all tiles with this layer
            var tilesToUpdate = _tileHighlights
                .Where(kvp => kvp.Value.Contains(layer))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var tileId in tilesToUpdate)
            {
                try
                {
                    var tile = Entities.GetEntity(tileId);
                    RemoveHighlight(tile, layer);
                }
                catch
                {
                    // Entity no longer exists, clean up
                    _tileHighlights.Remove(tileId);
                }
            }
        }

        /// <summary>
        /// Update the visual material based on highest priority active layer
        /// </summary>
        private void UpdateTileMaterial(Entity tile)
        {
            if (!_tileHighlights.TryGetValue(tile.Id, out var layers) || layers.Count == 0)
            {
                // No highlights - clear material
                ClearTileMaterial(tile);
                return;
            }

            // Get highest priority layer
            var highestLayer = layers.Max();

            if (_layerMaterials.TryGetValue(highestLayer, out var material))
            {
                SetTileMaterial(tile, material);
            }
        }

        #endregion

        #region Material Application

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

        #endregion

        public override void Cleanup()
        {
            Events.TileHover -= OnTileHover;
            Events.TileUnhover -= OnTileUnhover;
            Events.UnitHover -= OnUnitHover;
            Events.UnitUnhover -= OnUnitUnhover;

            // Clear all highlights
            foreach (var tileId in _tileHighlights.Keys.ToList())
            {
                try
                {
                    var tile = Entities.GetEntity(tileId);
                    ClearTileMaterial(tile);
                }
                catch
                {
                    // Entity no longer exists, skip
                }
            }
            _tileHighlights.Clear();
        }
    }
}
