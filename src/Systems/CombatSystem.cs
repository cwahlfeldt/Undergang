using System.Threading.Tasks;
using Game.Components;
using Godot;
using System.Linq;

namespace Game
{
	public class CombatSystem : System
	{
		private Tweener _tweener;
		private AnimationSystem _animationSystem;

		public override void Initialize()
		{
			Events.UnitDefeated += OnUnitDefeated;
			_tweener = Tweener.Instance;
			_animationSystem = Systems.Get<AnimationSystem>();
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
				GD.PrintErr("CombatSystem.ResolveCombat: null attacker or defender");
				return;
			}

			if (!attacker.Has<Damage>() || !defender.Has<Health>())
			{
				GD.PrintErr($"CombatSystem.ResolveCombat: missing Damage or Health components");
				return;
			}

			// Get combat values
			int damage = attacker.Get<Damage>();
			int currentHealth = defender.Get<Health>();
			int newHealth = currentHealth - damage;

			// Debug: Print detailed combat info
			GD.Print($"=== COMBAT TRIGGERED ===");
			GD.Print($"Attacker: {attacker.Id} (Enemy: {attacker.Has<Enemy>()}, Player: {attacker.Has<Player>()})");
			GD.Print($"Defender: {defender.Id} (Enemy: {defender.Has<Enemy>()}, Player: {defender.Has<Player>()})");
			GD.Print($"Damage: {damage}, Current Health: {currentHealth} -> New Health: {newHealth}");

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
			if (newHealth <= 0)
			{
				// Defender is defeated
				GD.Print($"Unit {defender.Id} defeated!");
				Events.OnUnitDefeated(defender);
			}
			else
			{
				// Update defender's health
				defender.Update(new Health(newHealth));
			}

			// Apply knockback if attacker is player
			if (attacker.Has<Player>() && defender.Has<Enemy>())
			{
				await ApplyKnockback(attacker, defender);
			}
		}

		/// <summary>
		/// Applies knockback to the defender, pushing them away from the attacker
		/// </summary>
		private async Task ApplyKnockback(Entity attacker, Entity defender)
		{
			if (!attacker.Has<Coordinate>() || !defender.Has<Coordinate>())
			{
				GD.PrintErr("ApplyKnockback: Missing Coordinate component");
				return;
			}

			if (!defender.Has<Instance>())
			{
				GD.PrintErr("ApplyKnockback: Defender has no Instance component");
				return;
			}

			// Get default knockback distance (1 tile by default, can be customized per unit)
			int knockbackDistance = attacker.Has<Knockback>() ? attacker.Get<Knockback>() : 1;

			var attackerPos = attacker.Get<Coordinate>().Value;
			var defenderPos = defender.Get<Coordinate>().Value;

			// Calculate direction vector from attacker to defender
			var direction = defenderPos - attackerPos;

			// Normalize the direction to get the primary axis
			// In hex coordinates, we need to push along one of the hex directions
			var knockbackDirection = GetHexDirection(direction);

			GD.Print($"=== KNOCKBACK ===");
			GD.Print($"Attacker at: {attackerPos}, Defender at: {defenderPos}");
			GD.Print($"Direction: {direction}, Knockback Direction: {knockbackDirection}");

			// Calculate knockback destination
			var knockbackTarget = defenderPos + (knockbackDirection * knockbackDistance);

			// Check if knockback destination is valid (exists and is traversable)
			var targetTile = Entities.GetAt(knockbackTarget);
			if (targetTile == null || !targetTile.Has<Traversable>())
			{
				GD.Print($"Knockback blocked: tile at {knockbackTarget} is invalid or untraversable");
				return;
			}

			// Check if another unit occupies the target tile
			var occupant = Entities.Query<Unit, Coordinate>()
				.Where(e => e.Get<Coordinate>().Value == knockbackTarget && e.Id != defender.Id)
				.FirstOrDefault();

			if (occupant != null)
			{
				GD.Print($"Knockback blocked: tile at {knockbackTarget} is occupied by unit {occupant.Id}");
				return;
			}

			// Apply knockback animation and position update
			GD.Print($"Knocking back unit {defender.Id} from {defenderPos} to {knockbackTarget}");
			defender.Add(new KnockedBack());

			var defenderNode = defender.Get<Instance>().Node;
			var targetWorldPos = HexGrid.HexToWorld(knockbackTarget);
			await _tweener.MoveToPosition(defenderNode, targetWorldPos);

			// Update defender's coordinate
			defender.Update(new Coordinate(knockbackTarget));
			defender.Remove<KnockedBack>();

			// Update pathfinding to reflect new position
			PathFinder.UpdateConnections();

			GD.Print($"Knockback complete: unit {defender.Id} now at {knockbackTarget}");
		}

		/// <summary>
		/// Gets the primary hex direction from a direction vector
		/// </summary>
		private Vector3I GetHexDirection(Vector3I direction)
		{
			// Find which hex direction component is strongest
			int absX = Mathf.Abs(direction.X);
			int absY = Mathf.Abs(direction.Y);
			int absZ = Mathf.Abs(direction.Z);

			// Normalize to the primary hex direction
			if (absX >= absY && absX >= absZ)
			{
				return new Vector3I(Mathf.Sign(direction.X), 0, -Mathf.Sign(direction.X));
			}
			else if (absY >= absX && absY >= absZ)
			{
				return new Vector3I(0, Mathf.Sign(direction.Y), -Mathf.Sign(direction.Y));
			}
			else
			{
				return new Vector3I(-Mathf.Sign(direction.Z), 0, Mathf.Sign(direction.Z));
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

			GD.Print($"CombatSystem.OnUnitDefeated: Removing unit {unit.Id}");

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
