using System.Linq;
using Game.Components;

namespace Game
{
    public class BlockSystem : System
    {
        public override void Initialize()
        {
            Events.TurnChanged += OnTurnChanged;

            // Initialize player with block ability (cooldown = 0, ready to use)
            var player = Entities.Query<Player>().FirstOrDefault();
            if (player != null && !player.Has<BlockCooldown>())
            {
                player.Add(new BlockCooldown(0));
            }
        }

        /// <summary>
        /// Toggle block on/off for the player
        /// Block will negate the next incoming attack
        /// </summary>
        public void ToggleBlock()
        {
            var player = Entities.Query<Player>().FirstOrDefault();
            if (player == null) return;

            // If block is already active, toggle it off
            if (player.Has<BlockActive>())
            {
                player.Remove<BlockActive>();
                return;
            }

            // Can't activate block if on cooldown
            if (!IsBlockAvailable(player))
            {
                return;
            }

            // Activate block (cooldown starts when block is consumed, not when activated)
            player.Add(new BlockActive());
        }

        /// <summary>
        /// Check if block is available (not on cooldown and not already active)
        /// </summary>
        public bool IsBlockAvailable(Entity player)
        {
            if (!player.Has<BlockCooldown>()) return true;
            return player.Get<BlockCooldown>() == 0;
        }

        /// <summary>
        /// Get remaining cooldown turns
        /// </summary>
        public int GetRemainingCooldown(Entity player)
        {
            if (!player.Has<BlockCooldown>()) return 0;
            return player.Get<BlockCooldown>();
        }

        /// <summary>
        /// Check if player currently has block active
        /// </summary>
        public bool IsBlockActive(Entity player)
        {
            return player.Has<BlockActive>();
        }

        /// <summary>
        /// Consume the block (called when an attack is blocked)
        /// Starts the cooldown after block is used
        /// </summary>
        public void ConsumeBlock(Entity player)
        {
            if (player.Has<BlockActive>())
            {
                player.Remove<BlockActive>();

                // Start cooldown AFTER block is consumed
                player.Update(new BlockCooldown(Config.BlockCooldown));
            }
        }

        /// <summary>
        /// Reduce cooldown when player's turn starts
        /// </summary>
        private void OnTurnChanged(Entity unit)
        {
            // Only process when player's turn starts
            if (!unit.Has<Player>()) return;

            if (unit.Has<BlockCooldown>())
            {
                var currentCooldown = unit.Get<BlockCooldown>();
                if (currentCooldown > 0)
                {
                    var newCooldown = currentCooldown - 1;
                    unit.Update(new BlockCooldown(newCooldown));
                }
            }

            // Note: BlockActive persists until consumed by an attack
            // It doesn't expire based on turns
        }

        public override void Cleanup()
        {
            Events.TurnChanged -= OnTurnChanged;
        }
    }
}
