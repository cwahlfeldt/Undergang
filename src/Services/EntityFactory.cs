using System;
using System.Collections.Generic;
using System.Linq;
using Game.Components;
using Godot;

namespace Game
{
    /// <summary>
    /// Factory for creating game entities (tiles, players, enemies)
    /// </summary>
    public class EntityFactory
    {
        private readonly Entities _entities;

        public EntityFactory(Entities entities)
        {
            _entities = entities;
        }

        public IEnumerable<Entity> CreateGrid(int mapSize = -1, int blockedTilesAmt = -1)
        {
            mapSize = mapSize == -1 ? Config.DefaultMapSize : mapSize;
            blockedTilesAmt = blockedTilesAmt == -1 ? Config.DefaultBlockedTilesCount : blockedTilesAmt;

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
            var tile = _entities.AddEntity(new Entity(_entities.GetNextId()));

            tile.Add(new Name($"Tile {coord}"));
            tile.Add(new Tile());
            tile.Add(new Instance(new Node3D()));
            tile.Add(new Coordinate(coord));
            tile.Add(new TileIndex(index));

            return tile;
        }

        public Entity CreatePlayer()
        {
            var player = _entities.AddEntity(new Entity(_entities.GetNextId()));

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
            var enemy = _entities.AddEntity(new Entity(_entities.GetNextId()));

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

                case UnitType.Wizard:
                    enemy.Add(new Wizard());
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

        private Entity GetRandomTileEntity()
        {
            var rand = new Random();

            // Get all occupied coordinates (from units that have coordinates)
            var occupiedCoordinates = _entities.Query<Unit, Coordinate>()
                .Select(u => u.Get<Coordinate>().Value)
                .ToHashSet();

            var entitiesAwayFromPlayer = _entities.Query<Coordinate>()
                .Where(e =>
                    e.Has<Tile>() &&
                    e.Has<Traversable>() &&
                    !occupiedCoordinates.Contains(e.Get<Coordinate>().Value) &&
                    !HexGrid.GetHexesInRange(Config.PlayerStart, Config.PlayerSpawnExclusionRadius).Contains(e.Get<Coordinate>()));

            return entitiesAwayFromPlayer
            .ElementAtOrDefault(rand.Next(0, entitiesAwayFromPlayer.Count()));
        }
    }
}
