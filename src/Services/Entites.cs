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

        public int GetNextId()
        {
            return _nextId++;
        }

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
                    !HexGrid.GetHexesInRange(Config.PlayerStart, 3).Contains(e.Get<Coordinate>()));

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

        public IEnumerable<Entity> GetTilesInRange(Vector3I coord, int range) =>
            GetTiles();

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

        public IEnumerable<Entity> CreateGrid(int mapSize = 5, int blockedTilesAmt = 16)
        {
            var randBlockedTileIndices = Utils.GenerateRandomIntArray(blockedTilesAmt);
            return [.. HexGrid.GenerateHexCoordinates(mapSize)
                .Select((coord, i) =>
                {
                    var tile = CreateTile(coord, i);

                    if (!(randBlockedTileIndices.Contains(i) && coord != Config.PlayerStart))
                        tile.Add(new Traversable());

                    return tile;
                })];
        }

        public Entity CreateTile(Vector3I coord, int index = 0)
        {
            var tile = AddEntity(new Entity(GetNextId()));

            tile.Add(new Name($"Tile {coord}"));
            tile.Add(new Tile());
            tile.Add(new Instance(new Node3D()));
            tile.Add(new Coordinate(coord));
            tile.Add(new TileIndex(index));

            return tile;
        }

        public Entity CreatePlayer()
        {
            var player = AddEntity(new Entity(GetNextId()));

            player.Add(new Name("Player"));
            player.Add(new Player());
            player.Add(new Unit(UnitType.Player));
            player.Add(new Instance(new Node3D()));
            player.Add(new Coordinate(Config.PlayerStart));
            player.Add(new RangeCircle());
            player.Add(new Damage(1));
            player.Add(new Health(3));
            player.Add(new MoveRange(1));
            player.Add(new AttackRange(1));

            return player;
        }

        public Entity CreateEnemy(UnitType unitType)
        {
            var enemy = AddEntity(new Entity(GetNextId()));

            enemy.Add(new Name(unitType.ToString()));
            enemy.Add(new Enemy());
            enemy.Add(new Unit(unitType));
            enemy.Add(new Instance(new Node3D()));
            enemy.Add(new Coordinate(GetRandomTileEntity().Get<Coordinate>()));

            // Configure stats and range based on enemy type
            switch (unitType)
            {
                case UnitType.Grunt:
                    enemy.Add(new Grunt());
                    enemy.Add(new RangeCircle());
                    enemy.Add(new Damage(1));
                    enemy.Add(new Health(1));
                    break;

                case UnitType.Sniper:
                    enemy.Add(new Sniper());
                    enemy.Add(new RangeDiagonal());
                    enemy.Add(new Damage(1));
                    enemy.Add(new Health(1));
                    break;

                case UnitType.SniperAxisQ:
                    enemy.Add(new SniperAxisQ());
                    enemy.Add(new RangeAxisQ());
                    enemy.Add(new Damage(1));
                    enemy.Add(new Health(1));
                    break;

                case UnitType.SniperAxisR:
                    enemy.Add(new SniperAxisR());
                    enemy.Add(new RangeAxisR());
                    enemy.Add(new Damage(1));
                    enemy.Add(new Health(1));
                    break;

                case UnitType.SniperAxisS:
                    enemy.Add(new SniperAxisS());
                    enemy.Add(new RangeAxisS());
                    enemy.Add(new Damage(1));
                    enemy.Add(new Health(1));
                    break;

                default:
                    // Default to Grunt behavior
                    enemy.Add(new Grunt());
                    enemy.Add(new RangeCircle());
                    enemy.Add(new Damage(1));
                    enemy.Add(new Health(1));
                    break;
            }

            enemy.Add(new MoveRange(1));
            enemy.Add(new AttackRange(1));

            return enemy;
        }
    }
}
