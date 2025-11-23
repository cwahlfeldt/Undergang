using System.Linq;
using System.Threading.Tasks;
using Game.Components;
using Godot;

namespace Game
{
    /// <summary>
    /// Manages ability cooldowns. Abilities go on cooldown after use,
    /// and cooldown decreases by 1 each time the player moves.
    /// </summary>
    public class CooldownSystem : System
    {
        private const int DASH_COOLDOWN_TURNS = 3; // Dash cooldown is 3 movements

        public override void Initialize()
        {
            Events.Instance.MoveCompleted += OnMoveCompleted;
            Events.Instance.DashCompleted += OnDashCompleted;

            // Initialize player with dash ready
            var player = Entities.Query<Player>().FirstOrDefault();
            if (player != null && !player.Has<DashReady>())
            {
                player.Add(new DashReady());
            }
        }

        public override async Task Update()
        {
            await Task.CompletedTask;
        }

        private void OnMoveCompleted(Entity unit, Vector3I from, Vector3I to)
        {
            // Only process player movements for cooldown reduction
            if (!unit.Has<Player>())
                return;

            // Reduce cooldown on movement
            if (unit.Has<AbilityCooldown>())
            {
                var cooldown = unit.Get<AbilityCooldown>();
                var newCooldown = cooldown.TurnsRemaining - 1;

                if (newCooldown <= 0)
                {
                    // Cooldown finished, dash is ready again
                    unit.Remove<AbilityCooldown>();
                    unit.Add(new DashReady());
                    GD.Print("Dash ability is ready!");
                }
                else
                {
                    // Update cooldown
                    unit.Update(new AbilityCooldown(newCooldown));
                    GD.Print($"Dash cooldown: {newCooldown} turns remaining");
                }
            }
        }

        private void OnDashCompleted(Entity unit, Vector3I from, Vector3I to)
        {
            // Put dash on cooldown after use
            if (unit.Has<Player>() && unit.Has<DashReady>())
            {
                unit.Remove<DashReady>();
                unit.Add(new AbilityCooldown(DASH_COOLDOWN_TURNS));
                GD.Print($"Dash used! Cooldown: {DASH_COOLDOWN_TURNS} turns");
            }
        }

        public override void Cleanup()
        {
            Events.Instance.MoveCompleted -= OnMoveCompleted;
            Events.Instance.DashCompleted -= OnDashCompleted;
        }
    }
}
