using System.Threading.Tasks;
using System.Linq;
using Game.Components;

namespace Game
{
    public class CombatSystem : System
    {
        private readonly bool _playerActionInProgress = false;

        // public override void Initialize()
        // {
        //     // Events.TurnChanged += OnTurnChanged;
        //     // Events.TileSelect += OnTileSelect;
        // }

        public override async Task Update()
        {
            var player = Entities.GetPlayer();
            var enemy = Entities.Query<Enemy, CurrentTurn>().FirstOrDefault();

            if (enemy == null || player == null)
                return;

            if (player.Has<WaitingForAction>())
                return;

            player.Add(new WaitingForAction());
            player.Add(new MoveRange(1));
        }

        // private async void OnTileSelect(Entity entity)
        // {
        //     if (_playerActionInProgress)
        //         return;

        //     var player = Entities.GetPlayer();

        //     if (entity.Get<TileComponent>().Coord == player.coord)
        //         return;

        //     // if (!Systems.Get<TurnSystem>().IsUnitTurn(player.entity))
        //     //     return;

        //     var path = PathFinder.FindPath(
        //         player.coord,
        //         entity.Get<TileComponent>().Coord,
        //         player.unit.MoveRange
        //     );

        //     if (path.Count > 0)
        //     {
        //         _playerActionInProgress = true;
        //         await ProcessPlayerTurn(player.entity, path);
        //         _playerActionInProgress = false;
        //         // Systems.Get<TurnSystem>().EndTurn();
        //     }
        // }

        // public void AttackUnit(Entity attacker, Entity target)
        // {
        //     var attackerComponent = attacker.Get<UnitComponent>();
        //     var targetComponent = target.Get<UnitComponent>();
        //     var damageDone = targetComponent.Health - attackerComponent.Damage;

        //     if (target.Get<UnitComponent>().Type == UnitType.Player)
        //         Systems.Get<UISystem>().RemoveHeart();

        //     if (damageDone <= 0)
        //     {
        //         // Systems.Get<TurnSystem>().RemoveUnit(target);
        //         targetComponent.Node.QueueFree();
        //         // target.Remove(targetComponent);
        //         return;
        //     }

        //     target.Update(targetComponent with { Health = damageDone });
        // }

        // private async Task ProcessPlayerTurn(Entity player, List<Vector3I> path)
        // {
        //     var startCoord = player.Get<TileComponent>().Coord;
        //     var initialAttackableEnemies = GetAttackableEnemies(startCoord);

        //     // await Systems.Get<MovementSystem>().MoveUnitTo(player, path);

        //     var finalCoord = path.Last();

        //     HandlePlayerAttacks(finalCoord, initialAttackableEnemies);
        //     HandleEnemyCounterattacks(finalCoord);
        // }

        // private void HandlePlayerAttacks(Vector3I playerCoord, List<Entity> initialEnemies)
        // {
        //     var (player, coord, entity) = Entities.GetPlayer();
        //     var attackableTiles = Entities.GetTilesInRange(playerCoord, 1);

        //     foreach (var enemy in initialEnemies)
        //     {
        //         if (attackableTiles.Contains(enemy))
        //         {
        //             AttackUnit(entity, enemy);
        //         }
        //     }
        // }

        // private void HandleEnemyCounterattacks(Vector3I playerCoord)
        // {
        //     var enemyCoords = Entities.GetEnemies()
        //         .Select(tile => tile.Get<TileComponent>().Coord);

        //     foreach (var enemyCoord in enemyCoords)
        //     {
        //         var enemyRange = Entities.GetTilesInRange(enemyCoord, 1);
        //         var playerTile = Entities.GetAt(playerCoord);

        //         if (enemyRange.Contains(playerTile))
        //         {
        //             AttackUnit(Entities.GetAt(enemyCoord), Entities.GetAt(playerCoord));
        //         }
        //     }
        // }

        // private List<Entity> GetAttackableEnemies(Vector3I position)
        // {
        //     return Entities.GetTilesInRange(position, 1)
        //         .Where(entity => entity.Has<UnitComponent>() &&
        //             entity.Get<UnitComponent>().Type == UnitType.Grunt)
        //         .ToList();
        // }

        // private async void OnTurnChanged(Entity entity)
        // {
        //     if (!entity.Has<UnitComponent>())
        //         return;

        //     GD.Print($"turn is {entity.Get<UnitComponent>().Name}");

        //     var player = Entities.GetPlayer();

        //     // if (player.entity != null && !Systems.Get<TurnSystem>().IsUnitTurn(player.entity))
        //     await ProcessEnemyTurn(entity);
        // }

        // private async Task ProcessEnemyTurn(Entity enemy)
        // {
        //     var (_, _, player) = Entities.GetPlayer();

        //     if (player == null)
        //         return;

        //     if (!IsPlayerInAttackRange(enemy, player))
        //         await MoveEnemyTowardsPlayer(enemy, player);

        //     // Systems.Get<TurnSystem>().EndTurn();
        // }

        // private async Task MoveEnemyTowardsPlayer(Entity enemy, Entity player)
        // {
        //     var path = PathFinder.FindPath(
        //         enemy.Get<TileComponent>().Coord,
        //         player.Get<TileComponent>().Coord,
        //         enemy.Get<UnitComponent>().MoveRange
        //     );

        //     // if (path.Count > 0)
        //     //     await Systems.Get<MovementSystem>().MoveUnitTo(enemy, path);
        // }

        // private bool IsPlayerInAttackRange(Entity enemy, Entity player)
        // {
        //     var attackableTiles = Entities.GetTilesInRange(
        //         enemy.Get<TileComponent>().Coord,
        //         enemy.Get<UnitComponent>().AttackRange
        //     );

        //     return attackableTiles.Contains(player);
        // }
    }
}
