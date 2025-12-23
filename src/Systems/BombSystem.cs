using System.Linq;
using System.Threading.Tasks;
using Game.Components;
using Godot;

namespace Game
{
    /// <summary>
    /// Manages bomb countdown and explosions
    /// </summary>
    public class BombSystem : System
    {
        private CombatSystem _combatSystem;
        private AnimationSystem _animationSystem;

        public override void Initialize()
        {
            _combatSystem = Systems.Get<CombatSystem>();
            _animationSystem = Systems.Get<AnimationSystem>();
            Events.TurnChanged += OnTurnChanged;
        }

        private async void OnTurnChanged(Entity unit)
        {
            // Only tick bombs at the start of player's turn
            if (!unit.Has<Player>())
                return;

            // Get all bombs
            var bombs = Entities.Query<Bomb, BombTimer, Coordinate>().ToList();

            foreach (var bomb in bombs)
            {
                var timer = bomb.Get<BombTimer>();
                var newTimer = timer.Value - 1;

                if (newTimer <= 0)
                {
                    // Bomb explodes!
                    await ExplodeBomb(bomb);
                }
                else
                {
                    // Update timer
                    bomb.Update(new BombTimer(newTimer));
                    GD.Print($"[BombSystem] Bomb at {bomb.Get<Coordinate>()} ticks down to {newTimer}");
                }
            }
        }

        /// <summary>
        /// Handles bomb explosion - deals damage to all units in radius
        /// </summary>
        private async Task ExplodeBomb(Entity bomb)
        {
            var bombCoord = bomb.Get<Coordinate>();
            var explosionRadius = bomb.Get<BombExplosionRadius>();
            var damage = bomb.Get<Damage>();

            GD.Print($"[BombSystem] BOOM! Bomb at {bombCoord} explodes with radius {explosionRadius}");

            // Get all tiles in explosion radius
            var explosionTiles = HexGrid.GetHexesInRange(bombCoord, explosionRadius);

            // Find all units in explosion radius
            var unitsInRange = Entities.Query<Unit, Coordinate, Health>()
                .Where(u => explosionTiles.Contains(u.Get<Coordinate>().Value))
                .ToList();

            // Deal damage to each unit
            foreach (var unit in unitsInRange)
            {
                var unitCoord = unit.Get<Coordinate>();
                GD.Print($"[BombSystem] {unit.Get<Name>()} at {unitCoord} caught in explosion!");

                // Apply damage
                var currentHealth = unit.Get<Health>();
                var newHealth = currentHealth.Value - damage.Value;
                unit.Update(new Health(newHealth));

                GD.Print($"[BombSystem] {unit.Get<Name>()} takes {damage} damage ({currentHealth} -> {newHealth})");

                // Play hurt animation
                if (unit.Has<AnimationPlayer>())
                {
                    _animationSystem.SetAnimationState(unit, AnimationState.Hurt);
                    await Task.Delay(Config.FallbackAttackDurationMs);
                }

                // Check if unit is defeated
                if (newHealth <= 0)
                {
                    GD.Print($"[BombSystem] {unit.Get<Name>()} defeated by explosion!");

                    // Play death animation
                    if (unit.Has<AnimationPlayer>())
                    {
                        _animationSystem.SetAnimationState(unit, AnimationState.Die);
                        await Task.Delay(Config.FallbackAttackDurationMs);
                    }

                    // Remove the defeated unit
                    if (unit.Has<Instance>())
                    {
                        var instance = unit.Get<Instance>();
                        instance.Value?.QueueFree();
                    }

                    Entities.RemoveEntity(unit);
                }
                else
                {
                    // Return to idle
                    if (unit.Has<AnimationPlayer>())
                    {
                        _animationSystem.SetAnimationState(unit, AnimationState.Idle);
                    }
                }
            }

            // Remove the bomb
            if (bomb.Has<Instance>())
            {
                var instance = bomb.Get<Instance>();
                instance.Value?.QueueFree();
            }

            Entities.RemoveEntity(bomb);
        }

        public override void Cleanup()
        {
            Events.TurnChanged -= OnTurnChanged;
        }
    }
}
