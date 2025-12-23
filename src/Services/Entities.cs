using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Game.Components;
using Godot;

namespace Game
{
    public partial class Entities(Node3D rootNode) : Node3D, ISystem
    {
        private readonly Dictionary<int, Entity> _entities = [];
        private int _nextId = 0;
        private readonly Node3D _rootNode = rootNode;
        private EntityFactory _factory;

        public EntityFactory Factory => _factory ??= new EntityFactory(this);

        public int GetNextId()
        {
            return _nextId++;
        }

        /// <summary>
        /// Gets the current next ID value without incrementing (for snapshots)
        /// </summary>
        public int GetNextIdValue() => _nextId;

        /// <summary>
        /// Sets the next ID value (for restoring from snapshots)
        /// </summary>
        public void SetNextId(int value) => _nextId = value;

        public Entity AddEntity(Entity entity)
        {
            return _entities[entity.Id] = entity;
        }

        public Dictionary<int, Entity> GetEntities()
        {
            return _entities;
        }

        public Node3D GetRootNode()
        {
            return _rootNode;
        }

        public void RemoveEntity(Entity entity) =>
            _entities.Remove(entity.Id);

        public Entity GetEntity(int id) => _entities[id];

        public Entity GetPlayer() =>
            Query<Player>().FirstOrDefault();

        public IEnumerable<Entity> GetEnemies() =>
            Query<Unit, Enemy>();

        public Entity GetRandomTileEntity()
        {
            var rand = new Random();
            var entitiesAwayFromPlayer = Query<Coordinate>()
                .Where(e =>
                    !e.Has<Unit>() &&
                    e.Has<Traversable>() &&
                    !HexGrid.GetHexesInRange(Config.PlayerStart, Config.PlayerSpawnExclusionRadius).Contains(e.Get<Coordinate>()));

            return entitiesAwayFromPlayer
            .ElementAtOrDefault(rand.Next(0, entitiesAwayFromPlayer.Count()));
        }

        public IEnumerable<Entity> GetTiles() =>
            Query<Tile>();

        public Entity GetAt(Vector3I coord) =>
            Query<Tile>().FirstOrDefault(e => e.Get<Coordinate>() == coord);

        public bool IsTileOccupied(Vector3I coord)
        {
            var tile = GetAt(coord);
            var unit = Query<Unit>().FirstOrDefault(e => e.Get<Coordinate>() == coord);
            return unit != null;
        }

        public IEnumerable<Entity> GetTilesInRange(Vector3I coord, int range)
        {
            var coordsInRange = HexGrid.GetHexesInRange(coord, range);
            return Query<Tile>().Where(tile => coordsInRange.Contains(tile.Get<Coordinate>()));
        }

        public IEnumerable<Entity> Query<T1>() =>
            _entities.Values.Where(e =>
                e.Has<T1>());
        public IEnumerable<Entity> Query<T1, T2>() =>
            _entities.Values.Where(e =>
                e.Has<T1>() &&
                e.Has<T2>());
        public IEnumerable<Entity> Query<T1, T2, T3>() =>
            _entities.Values.Where(e =>
                e.Has<T1>() &&
                e.Has<T2>() &&
                e.Has<T3>());
        public IEnumerable<Entity> Query<T1, T2, T3, T4>() =>
            _entities.Values.Where(e =>
                e.Has<T1>() &&
                e.Has<T2>() &&
                e.Has<T3>() &&
                e.Has<T4>());
    }
}
