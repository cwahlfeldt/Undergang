using System.Collections.Generic;
using System.Linq;
using Game.Components;
using Godot;

namespace Game
{
    public class PathFinder : ISystem
    {
        private readonly AStar3D _astar = new();
        private Dictionary<Vector3I, Entity> _tiles = [];
        private readonly Entities _entities;

        public PathFinder(Entities entities)
        {
            Events.Instance.MoveCompleted += OnMoveCompleted;
            Events.Instance.UnitDefeated += OnUnitDefeated;
            Events.Instance.GridReady += OnGridReady;

            _entities = entities;
        }

        private void OnGridReady(IEnumerable<Entity> enumerable)
        {
            SetupPathfinding();
        }

        public void SetupPathfinding()
        {
            _astar.Clear();
            _tiles = _entities
                .Query<Tile, Coordinate>()
                .ToDictionary(
                    entity => entity.Get<Coordinate>().Value,  // has to be explicit
                    entity => entity
                );

            AddPoints();
            ConnectPoints();
        }

        public List<Vector3I> FindPath(Vector3I from, Vector3I to, int maxRange)
        {
            if (!_tiles.TryGetValue(from, out var fromTile) || !_tiles.TryGetValue(to, out var toTile))
                return [];

            int fromIndex = fromTile.Get<TileIndex>();
            int toIndex = toTile.Get<TileIndex>();

            if (!_astar.HasPoint(fromIndex) || !_astar.HasPoint(toIndex))
                return [];

            // Temporarily connect the starting tile to its unoccupied neighbors
            // This allows units to path FROM their current (occupied) position
            var tempConnections = new List<int>();
            foreach (var dir in HexGrid.Directions.Values)
            {
                var neighborCoord = from + dir;
                if (_tiles.TryGetValue(neighborCoord, out var neighborTile))
                {
                    int neighborIndex = neighborTile.Get<TileIndex>();
                    if (_astar.HasPoint(neighborIndex) &&
                        !_astar.ArePointsConnected(fromIndex, neighborIndex) &&
                        !_entities.IsTileOccupied(neighborCoord))
                    {
                        _astar.ConnectPoints(fromIndex, neighborIndex);
                        tempConnections.Add(neighborIndex);
                    }
                }
            }

            var path = _astar.GetPointPath(fromIndex, toIndex);

            // Restore original state - disconnect temporary connections
            foreach (var neighborIndex in tempConnections)
            {
                _astar.DisconnectPoints(fromIndex, neighborIndex);
            }

            if (path == null || path.Length == 0)
                return [];

            var coordPath = path.Select(HexGrid.WorldToHex).ToList();

            return maxRange > 0 ? [.. coordPath.Take(maxRange + 1)] : coordPath;
        }

        private void AddPoints()
        {
            foreach (var (_, tile) in _tiles)
            {
                if (tile.Has<Traversable>())
                {
                    int index = tile.Get<TileIndex>();
                    _astar.AddPoint(index, tile.Get<Instance>().Node.Position);
                }
            }
        }

        private void ConnectPoints()
        {
            foreach (var (coord, tile) in _tiles)
            {
                int currentIndex = tile.Get<TileIndex>();
                if (!_astar.HasPoint(currentIndex))
                    continue;

                foreach (var dir in HexGrid.Directions.Values)
                {
                    var neighborCoord = coord + dir;
                    if (_tiles.TryGetValue(neighborCoord, out var neighborTile))
                    {
                        if (_entities.IsTileOccupied(neighborCoord))
                            continue;

                        int neighborIndex = neighborTile.Get<TileIndex>();
                        if (_astar.HasPoint(neighborIndex) && !_astar.ArePointsConnected(currentIndex, neighborIndex))
                        {
                            _astar.ConnectPoints(currentIndex, neighborIndex);
                        }
                    }
                }
            }
        }

        private void UpdateConnectionsForTile(Vector3I coord)
        {
            if (!_tiles.TryGetValue(coord, out var tile))
                return;

            int tileIndex = tile.Get<TileIndex>();
            bool tileIsOccupied = _entities.IsTileOccupied(coord);

            // Update connections to each neighbor
            foreach (var dir in HexGrid.Directions.Values)
            {
                var neighborCoord = coord + dir;
                if (!_tiles.TryGetValue(neighborCoord, out var neighborTile))
                    continue;

                int neighborIndex = neighborTile.Get<TileIndex>();
                if (!_astar.HasPoint(neighborIndex))
                    continue;

                bool neighborIsOccupied = _entities.IsTileOccupied(neighborCoord);
                bool shouldBeConnected = !tileIsOccupied && !neighborIsOccupied;

                bool isConnected = _astar.ArePointsConnected(tileIndex, neighborIndex);

                if (shouldBeConnected && !isConnected)
                {
                    _astar.ConnectPoints(tileIndex, neighborIndex);
                }
                else if (!shouldBeConnected && isConnected)
                {
                    _astar.DisconnectPoints(tileIndex, neighborIndex);
                }
            }
        }

        /**
        * Utilities
        */
        public List<Vector3I> GetReachableCoords(Vector3I start, int range)
        {
            var reachable = new List<Vector3I>();
            var visited = new HashSet<Vector3I>();
            var queue = new Queue<(Vector3I coord, int distance)>();

            queue.Enqueue((start, 0));
            visited.Add(start);

            while (queue.Count > 0)
            {
                var (current, distance) = queue.Dequeue();
                reachable.Add(current);

                if (distance >= range)
                    continue;

                foreach (var dir in HexGrid.Directions.Values)
                {
                    var neighborCoord = current + dir;
                    if (_tiles.TryGetValue(neighborCoord, out var neighborTile) &&
                        !visited.Contains(neighborCoord) &&
                        neighborTile.Has<Traversable>() &&
                        !_entities.IsTileOccupied(neighborCoord))
                    {
                        visited.Add(neighborCoord);
                        queue.Enqueue((neighborCoord, distance + 1));
                    }
                }
            }

            return reachable;
        }

        public bool HasConnection(Vector3I coord, Vector3I neighborCoord)
        {
            if (!_tiles.TryGetValue(coord, out var tile) || !_tiles.TryGetValue(neighborCoord, out var neighborTile))
                return false;

            int tileIndex = tile.Get<TileIndex>();
            int neighborIndex = neighborTile.Get<TileIndex>();

            return _astar.HasPoint(tileIndex) &&
                   _astar.HasPoint(neighborIndex) &&
                   _astar.ArePointsConnected(tileIndex, neighborIndex);
        }

        /**
        * Event listeners
        */
        private void OnMoveCompleted(Entity entity, Vector3I fromCoord, Vector3I toCoord)
        {
            UpdateConnectionsForTile(fromCoord);
            UpdateConnectionsForTile(toCoord);
        }

        private void OnUnitDefeated(Entity unit)
        {
            UpdateConnectionsForTile(unit.Get<Coordinate>());
        }
    }
}
