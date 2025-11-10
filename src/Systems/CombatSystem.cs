using System.Threading.Tasks;
using Game.Components;
using Godot;

namespace Game
{
	public class CombatSystem : System
	{
		public override void Initialize()
		{
			Events.UnitDefeated += OnUnitDefeated;
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
		public void ResolveCombat(Entity attacker, Entity defender)
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

			// TODO: Add attack animation/VFX here
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
