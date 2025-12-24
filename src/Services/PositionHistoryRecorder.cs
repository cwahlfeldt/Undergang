using System.Collections.Generic;
using System.Linq;
using Game.Components;
using Godot;

namespace Game
{
    /// <summary>
    /// Records movement paths for all units, enabling smooth reverse playback during rewind.
    /// Records full transforms (position + rotation) at each waypoint for accurate reversal.
    /// </summary>
    public class PositionHistoryRecorder : System
    {
        /// <summary>
        /// A recorded movement containing the full transform path taken
        /// </summary>
        public record MovementRecord
        {
            public int EntityId { get; init; }
            public List<Transform3D> Transforms { get; init; }  // Full transforms along the path
        }

        // Store movements per turn
        private readonly Dictionary<int, List<MovementRecord>> _turnMovements = new();
        private int _currentTurnNumber = 0;

        public override void Initialize()
        {
            Events.TurnChanged += OnTurnChanged;
            Events.TurnRestarted += OnTurnRestarted;

            // Initialize turn 0 for movements before first TurnChanged event
            _turnMovements[0] = new List<MovementRecord>();
        }

        private void OnTurnChanged(Entity unit)
        {
            // Only increment turn on player's turn
            if (unit.Has<Player>())
            {
                _currentTurnNumber++;
                // Initialize new turn's movement list
                if (!_turnMovements.ContainsKey(_currentTurnNumber))
                {
                    _turnMovements[_currentTurnNumber] = new List<MovementRecord>();
                }
            }
        }

        private void OnTurnRestarted(Entity unit)
        {
            // After rewind, initialize the turn's movement list fresh
            if (!_turnMovements.ContainsKey(_currentTurnNumber))
            {
                _turnMovements[_currentTurnNumber] = new List<MovementRecord>();
            }
        }

        /// <summary>
        /// Records a full transform path for a unit (called by Tweener during movement animation).
        /// The transforms are stored as-is; they will be reversed during rewind playback.
        /// </summary>
        public void RecordPathWithTransforms(int entityId, List<Transform3D> transforms)
        {
            if (transforms == null || transforms.Count < 2)
                return;

            // Reverse the transforms for rewind playback (end -> start)
            var reversedTransforms = new List<Transform3D>(transforms);
            reversedTransforms.Reverse();

            var record = new MovementRecord
            {
                EntityId = entityId,
                Transforms = reversedTransforms
            };

            if (!_turnMovements.ContainsKey(_currentTurnNumber))
            {
                _turnMovements[_currentTurnNumber] = new List<MovementRecord>();
            }

            _turnMovements[_currentTurnNumber].Add(record);
            GD.Print($"[PositionRecorder] Turn {_currentTurnNumber}: Recorded {transforms.Count} transforms for entity {entityId}");
        }

        /// <summary>
        /// Sets the current turn number (used when restoring from snapshot)
        /// </summary>
        public void SetTurnNumber(int turnNumber)
        {
            _currentTurnNumber = turnNumber;
        }

        /// <summary>
        /// Gets all movement records for a specific turn (for rewind).
        /// When rewinding, we want the PREVIOUS turn's movements since:
        /// - Turn N starts → snapshot captured → player moves → movements recorded for turn N
        /// - Turn N+1 starts → we want to rewind to turn N's snapshot using turn N's movements
        /// </summary>
        public List<MovementRecord> GetMovementsForTurn(int turnNumber)
        {
            GD.Print($"[PositionRecorder] Looking for movements in turn {turnNumber}");
            if (_turnMovements.TryGetValue(turnNumber, out var movements))
            {
                GD.Print($"[PositionRecorder] Found {movements.Count} movement records for turn {turnNumber}");
                return movements;
            }
            GD.Print($"[PositionRecorder] No movements found for turn {turnNumber}");
            return new List<MovementRecord>();
        }

        /// <summary>
        /// Gets all movement records for the current turn (for rewind)
        /// </summary>
        public List<MovementRecord> GetMovementsForCurrentTurn()
        {
            return GetMovementsForTurn(_currentTurnNumber);
        }

        /// <summary>
        /// Gets the combined transform path for a specific entity for a given turn.
        /// Returns transforms in rewind order (current position -> original position).
        /// </summary>
        public List<Transform3D> GetEntityRewindTransforms(int entityId, int turnNumber)
        {
            var transforms = new List<Transform3D>();

            if (_turnMovements.TryGetValue(turnNumber, out var movements))
            {
                // Get all movements for this entity in reverse order (most recent first)
                var entityMovements = movements
                    .Where(m => m.EntityId == entityId)
                    .Reverse()
                    .ToList();

                foreach (var movement in entityMovements)
                {
                    transforms.AddRange(movement.Transforms);
                }
            }

            return transforms;
        }

        /// <summary>
        /// Gets the combined transform path for a specific entity across the current turn.
        /// Returns transforms in rewind order (current position -> original position).
        /// </summary>
        public List<Transform3D> GetEntityRewindTransforms(int entityId)
        {
            return GetEntityRewindTransforms(entityId, _currentTurnNumber);
        }

        /// <summary>
        /// Clears history for turns after the given turn number (used after rewind)
        /// </summary>
        public void ClearHistoryAfterTurn(int turnNumber)
        {
            var keysToRemove = _turnMovements.Keys.Where(k => k > turnNumber).ToList();
            foreach (var key in keysToRemove)
            {
                _turnMovements.Remove(key);
            }
        }

        /// <summary>
        /// Clears movements for the current turn (used when starting fresh)
        /// </summary>
        public void ClearCurrentTurn()
        {
            if (_turnMovements.ContainsKey(_currentTurnNumber))
            {
                _turnMovements[_currentTurnNumber].Clear();
            }
        }

        public int CurrentTurnNumber => _currentTurnNumber;

        public override void Cleanup()
        {
            Events.TurnChanged -= OnTurnChanged;
            Events.TurnRestarted -= OnTurnRestarted;
            _turnMovements.Clear();
        }
    }
}
