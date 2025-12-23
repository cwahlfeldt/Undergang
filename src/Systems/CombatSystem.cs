using System.Threading.Tasks;
using Game.Components;
using Godot;

namespace Game
{
	public class CombatSystem : System
	{
		private Tweener _tweener;
		private AnimationSystem _animationSystem;
		private BlockSystem _blockSystem;

		public override void Initialize()
		{
			Events.UnitDefeated += OnUnitDefeated;
			_tweener = Tweener.Instance;
			_animationSystem = Systems.Get<AnimationSystem>();
			_blockSystem = Systems.Get<BlockSystem>();
		}

		public override async Task Update()
		{
			// CombatSystem is now primarily triggered via ResolveCombat calls from MovementSystem
			// This Update method can remain empty or be used for passive combat checks
			await Task.CompletedTask;
		}

		/// <summary>
		/// Resolves combat between an attacker and defender
		/// </summary>
		public async Task ResolveCombat(Entity attacker, Entity defender)
		{
			if (attacker == null || defender == null)
			{
					return;
			}

			if (!attacker.Has<Damage>() || !defender.Has<Health>())
			{
					return;
			}

			// Check if nodes are still valid (could be disposed during rewind)
			if (attacker.Has<Instance>())
			{
				var node = attacker.Get<Instance>().Node;
				if (node == null || !GodotObject.IsInstanceValid(node))
					return;
			}
			if (defender.Has<Instance>())
			{
				var node = defender.Get<Instance>().Node;
				if (node == null || !GodotObject.IsInstanceValid(node))
					return;
			}

			// Check if defender has block active
			bool hasBlock = defender.Has<BlockActive>();

			// Play attack animation (uses AnimationSystem for state-based animations)
			if (attacker.Has<Unit>() && defender.Has<Unit>())
			{
				await _animationSystem.PlayAttackAnimation(attacker, defender);
			}
			else if (attacker.Has<Instance>() && defender.Has<Instance>())
			{
				// Fallback to Tweener animation if no Unit component (shouldn't happen normally)
				var attackerNode = attacker.Get<Instance>().Node;
				var defenderNode = defender.Get<Instance>().Node;

				if (attackerNode != null && defenderNode != null)
				{
					await _tweener.AttackAnimation(attackerNode, defenderNode.GlobalPosition);
				}
			}

			// Apply damage after animation
			if (hasBlock)
			{
				// Block negates the attack completely
				_blockSystem.ConsumeBlock(defender);

				// Update UI to reflect block being consumed
				Events.OnTurnChanged(defender);
			}
			else
			{
				// Get combat values
				int damage = attacker.Get<Damage>();
				int currentHealth = defender.Get<Health>();
				int newHealth = currentHealth - damage;

				if (newHealth <= 0)
				{
					// Defender is defeated - set health to 0 first so checks work properly
					defender.Update(new Health(0));
					Events.OnUnitDefeated(defender);
				}
				else
				{
					// Update defender's health
					defender.Update(new Health(newHealth));
				}
			}
		}

		/// <summary>
		/// Checks if an attacker can target a defender (basic validity check)
		/// </summary>
		public bool CanAttack(Entity attacker, Entity defender)
		{
			if (attacker == null || defender == null) return false;
			if (!attacker.Has<Damage>()) return false;
			if (!defender.Has<Health>()) return false;

			// Can't attack yourself
			if (attacker.Id == defender.Id) return false;

			// Can't attack if already defeated
			if (!defender.Has<Health>() || defender.Get<Health>() <= 0) return false;

			return true;
		}

		private void OnUnitDefeated(Entity unit)
		{
			if (unit == null) return;

			// Check if this is the player
			if (unit.Has<Player>())
			{
				// Player defeated - trigger game over
				// Death animation is already playing from AnimationSystem
				Events.OnGameOver();

				// Don't remove the player - let them stay visible in death animation
				return;
			}

			// For enemies, remove them as normal
			// Remove visual representation
			if (unit.Has<Instance>())
			{
				var instance = unit.Get<Instance>();
				instance.Node?.QueueFree();
			}

			// Remove unit from entity system
			Entities.RemoveEntity(unit);

			// PathFinder and RangeSystem will update on their next cycle
		}

		public override void Cleanup()
		{
			Events.UnitDefeated -= OnUnitDefeated;
		}
	}
}
