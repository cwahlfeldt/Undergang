using System.Linq;
using Game.Components;
using Godot;

namespace Game
{
    public class TurnSystem : System
    {
        private int _currentTurnIndex = -1;  // Add this field to track current turn

        public override void Initialize()
        {
            Events.OnUnitActionComplete += OnUnitActionComplete;
            SetupInitialTurnOrder();
        }

        private void SetupInitialTurnOrder()
        {
            var player = Entities.Query<Player>().FirstOrDefault();
            var enemies = Entities.Query<Enemy>();
            var units = new[] { player }.Concat(enemies).ToList();

            for (int i = 0; i < units.Count; i++)
            {
                units[i].Add(new TurnOrder(i));
            }

            if (units.Any())
            {
                _currentTurnIndex = -1; // Will become 0 after first advancement
                AdvanceTurn();
            }
        }

        private void StartUnitTurn(Entity unit)
        {
            // This is potentially expensive as hell...???
            PathFinder.SetupPathfinding();

            unit.Add(new CurrentTurn());
            unit.Add(new WaitingForAction());
            Events.OnTurnChanged(unit);
        }

        private void OnUnitActionComplete(Entity entity)
        {
            if (entity.Has<CurrentTurn>())
            {
                entity.Remove<CurrentTurn>();
                entity.Remove<WaitingForAction>();
                AdvanceTurn();
            }
        }

        private void AdvanceTurn()
        {
            var allUnits = Entities.Query<TurnOrder>()
                .OrderBy(e => e.Get<TurnOrder>())
                .ToList();

            _currentTurnIndex = (_currentTurnIndex + 1) % allUnits.Count;

            var nextUnit = allUnits[_currentTurnIndex];
            StartUnitTurn(nextUnit);
        }

        public override void Cleanup()
        {
            Events.OnUnitActionComplete -= OnUnitActionComplete;
        }
    }
}
