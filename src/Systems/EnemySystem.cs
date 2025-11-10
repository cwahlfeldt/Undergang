using System.Linq;
using System.Threading.Tasks;
using Game.Components;
using Godot;

namespace Game
{
    public class EnemySystem : System
    {
        private CombatSystem _combatSystem;

        public override void Initialize()
        {
            _combatSystem = Systems.Get<CombatSystem>();
        }

        public override async Task Update()
        {
            var enemy = Entities.Query<Enemy, CurrentTurn>().FirstOrDefault();
            var player = Entities.Query<Player>().FirstOrDefault();

            if (enemy == null || player == null)
                return;

            // Only act if enemy is waiting for action
            if (!enemy.Has<WaitingForAction>() || enemy.Has<Movement>())
                return;

            var enemyCoord = enemy.Get<Coordinate>();
            var playerCoord = player.Get<Coordinate>();

            // In Hoplite-style combat, enemies ONLY attack when player moves into their range
            // On the enemy's turn, they ONLY move (they don't attack proactively)
            // This creates the tactical puzzle where the player must avoid enemy threat zones

            // Check if player is within this enemy's attack range (uses enemy's range type)
            var attackRangeTiles = RangeSystem.GetAttackRangeTiles(enemy, enemyCoord).ToList();
            bool playerInRange = attackRangeTiles.Contains(playerCoord);

            if (playerInRange)
            {
                // Player is already in range - enemy just waits/passes turn
                // (Enemy already attacked when player moved into range on player's turn)
                GD.Print($"Enemy {enemy.Id} passes turn (player already in range)");
                enemy.Remove<WaitingForAction>();
                Events.UnitActionComplete(enemy);
            }
            else
            {
                // Player is not in range - MOVE towards player
                GD.Print($"Enemy {enemy.Id} moves towards player");
                enemy.Add(new Movement(
                    enemyCoord,
                    playerCoord
                ));

                enemy.Remove<WaitingForAction>();
            }
        }
    }
}