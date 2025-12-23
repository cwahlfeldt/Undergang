using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Components;
using Godot;

namespace Game
{
    /// <summary>
    /// Immutable snapshot of a single entity's state at a point in time
    /// </summary>
    public record EntityStateSnapshot
    {
        public int EntityId { get; init; }
        public bool IsUnit { get; init; }
        public Vector3I? AnimationOrigin { get; init; }
        public IReadOnlyDictionary<Type, object> Components { get; init; }
    }

    /// <summary>
    /// Complete game state at the start of a turn
    /// </summary>
    public record GameStateSnapshot
    {
        public int TurnNumber { get; init; }
        public DateTime CapturedAt { get; init; }
        public int NextEntityId { get; init; }
        public int CurrentTurnIndex { get; init; }
        public IReadOnlyList<EntityStateSnapshot> Entities { get; init; }
        public int UnitCount { get; init; }
        public int TileCount { get; init; }
    }

    /// <summary>
    /// Result of a rewind operation
    /// </summary>
    public record RewindResult
    {
        public bool Success { get; init; }
        public string FailureReason { get; init; }
        public int TurnRewindedTo { get; init; }
        public IReadOnlyList<int> RespawnedUnitIds { get; init; }
    }

    /// <summary>
    /// Central service for game state snapshots and time manipulation.
    /// Owns all snapshot capture, storage, and restoration logic.
    /// </summary>
    public class GameStateManager : System
    {
        // Dependencies
        private RenderSystem _renderSystem;
        private AnimationSystem _animationSystem;
        private TileHighlightSystem _tileHighlightSystem;
        private TurnSystem _turnSystem;

        // State
        private readonly List<GameStateSnapshot> _history = new();
        private int _cooldownRemaining = 0;

        // Component classification - authoritative list of what gets captured
        private static readonly HashSet<Type> _capturedComponentTypes = new()
        {
            // Core components
            typeof(Name), typeof(Coordinate), typeof(TileIndex), typeof(Tile),
            typeof(Traversable), typeof(Untraversable),

            // Combat components
            typeof(Health), typeof(Damage), typeof(AttackRange),

            // Movement components
            typeof(MoveRange), typeof(DashCooldown), typeof(BlockCooldown), typeof(BlockActive),

            // Unit components
            typeof(Player), typeof(Enemy), typeof(Grunt), typeof(Wizard),
            typeof(SniperAxisQ), typeof(SniperAxisR), typeof(SniperAxisS), typeof(Unit),

            // Range components
            typeof(RangeCircle), typeof(RangeDiagonal), typeof(RangeExplosion),
            typeof(RangeHex), typeof(RangeNGon), typeof(RangeAxisQ), typeof(RangeAxisR), typeof(RangeAxisS),

            // State components
            typeof(TurnOrder)
        };

        public override void Initialize()
        {
            _renderSystem = Systems.Get<RenderSystem>();
            _animationSystem = Systems.Get<AnimationSystem>();
            _tileHighlightSystem = Systems.Get<TileHighlightSystem>();
            // TurnSystem reference set later to avoid circular dependency
        }

        /// <summary>
        /// Sets the TurnSystem reference (called after all systems initialized)
        /// </summary>
        public void SetTurnSystem(TurnSystem turnSystem)
        {
            _turnSystem = turnSystem;
        }

        // ========== PUBLIC API ==========

        /// <summary>
        /// Captures the current game state as a snapshot.
        /// Called by TurnSystem at the START of player's turn (before they act).
        /// </summary>
        public void CaptureSnapshot(int currentTurnIndex)
        {
            var entitySnapshots = new List<EntityStateSnapshot>();
            var allEntities = Entities.GetEntities();
            int unitCount = 0, tileCount = 0;

            foreach (var (id, entity) in allEntities)
            {
                var components = new Dictionary<Type, object>();
                bool isUnit = entity.Has<Unit>();

                // Capture only Tier 1 components
                foreach (var (type, value) in entity.GetComponents())
                {
                    if (_capturedComponentTypes.Contains(type))
                    {
                        components[type] = value;
                    }
                }

                // Use current coordinate as the rewind target position
                // This is the position units should return to when rewinding
                Vector3I? rewindPosition = entity.Has<Coordinate>()
                    ? entity.Get<Coordinate>().Value
                    : null;

                entitySnapshots.Add(new EntityStateSnapshot
                {
                    EntityId = id,
                    IsUnit = isUnit,
                    AnimationOrigin = rewindPosition,
                    Components = components
                });

                if (isUnit) unitCount++; else tileCount++;
            }

            var snapshot = new GameStateSnapshot
            {
                TurnNumber = currentTurnIndex,
                CapturedAt = DateTime.UtcNow,
                NextEntityId = Entities.GetNextIdValue(),
                CurrentTurnIndex = currentTurnIndex,
                Entities = entitySnapshots,
                UnitCount = unitCount,
                TileCount = tileCount
            };

            // Add to history with trimming
            _history.Add(snapshot);
            while (_history.Count > Config.MaxHistoryDepth)
            {
                _history.RemoveAt(0);
            }

        }

        /// <summary>
        /// Executes a rewind to undo the current turn's action.
        /// We need to use the second-most-recent snapshot because:
        /// - The most recent snapshot was captured at the START of the current turn (after we already moved)
        /// - The second-most-recent snapshot has our position BEFORE we moved
        /// </summary>
        public async Task<RewindResult> RewindOneTurn()
        {
            // Need at least 2 snapshots to rewind (current turn's snapshot + previous turn's snapshot)
            if (_history.Count < 2)
            {
                return new RewindResult { Success = false, FailureReason = "No previous turn to rewind to" };
            }

            // Use second-to-last snapshot (the one from before the current turn)
            return await RewindToSnapshot(_history.Count - 2);
        }

        /// <summary>
        /// Executes a rewind to a specific turn number with visual animation.
        /// </summary>
        public async Task<RewindResult> RewindToTurn(int turnNumber)
        {
            var snapshotIndex = _history.FindIndex(s => s.TurnNumber == turnNumber);
            if (snapshotIndex < 0)
            {
                return new RewindResult { Success = false, FailureReason = $"No snapshot for turn {turnNumber}" };
            }

            return await RewindToSnapshot(snapshotIndex);
        }

        /// <summary>
        /// Internal method to rewind to a specific snapshot index
        /// </summary>
        private async Task<RewindResult> RewindToSnapshot(int snapshotIndex)
        {
            // Validation
            if (_cooldownRemaining > 0)
            {
                return new RewindResult { Success = false, FailureReason = $"Cooldown active: {_cooldownRemaining} turns" };
            }

            // Check for dead player
            var player = Entities.Query<Player>().FirstOrDefault();
            if (player != null && player.Has<Health>() && player.Get<Health>() <= 0)
            {
                return new RewindResult { Success = false, FailureReason = "Player is dead" };
            }

            var snapshot = _history[snapshotIndex];

            // Debug: Log player position before and target position from snapshot
            if (player != null && player.Has<Coordinate>())
            {
                var currentPos = player.Get<Coordinate>().Value;
                var snapshotPlayerEntity = snapshot.Entities.FirstOrDefault(e => e.Components.ContainsKey(typeof(Player)));
                var targetPos = snapshotPlayerEntity?.AnimationOrigin;
                GD.Print($"[GameStateManager] Rewinding player from {currentPos} to {targetPos}");
            }

            // 1. Identify units that need to be respawned (were in snapshot but dead now)
            var respawnedUnitIds = new List<int>();
            var currentUnitIds = Entities.Query<Unit>().Select(e => e.Id).ToHashSet();
            var snapshotUnitIds = snapshot.Entities
                .Where(s => s.IsUnit)
                .Select(s => s.EntityId)
                .ToHashSet();

            foreach (var id in snapshotUnitIds.Except(currentUnitIds))
            {
                respawnedUnitIds.Add(id);
            }

            // 2. Animate existing units back to their snapshot positions
            await AnimateUnitsToSnapshotPositions(snapshot);

            // 3. Restore entity state
            RestoreEntityState(snapshot);

            // 4. Rebuild visual nodes for all units
            RebuildVisualState();

            // 5. Setup input handlers
            foreach (var unit in Entities.Query<Unit>())
            {
                _renderSystem.SetupUnitInput(unit);
            }

            // 6. Apply fade-in effect to respawned units
            await ApplyRespawnFadeInEffects(respawnedUnitIds);

            // 7. Recalculate derived state (ranges, etc.)
            RecalculateDerivedState();

            // 8. Clear mesh caches
            _tileHighlightSystem.ClearMeshCache();

            // 9. Set animations to idle
            foreach (var unit in Entities.Query<Unit>())
            {
                _animationSystem.SetAnimationState(unit, AnimationState.Idle);
            }

            // 10. Remove snapshots from this point forward (can't redo)
            _history.RemoveRange(snapshotIndex, _history.Count - snapshotIndex);

            // 11. Start cooldown
            _cooldownRemaining = Config.RewindCooldownTurns;

            // Debug: Log player position after restore
            var restoredPlayer = Entities.Query<Player>().FirstOrDefault();
            if (restoredPlayer != null && restoredPlayer.Has<Coordinate>())
            {
                var restoredPos = restoredPlayer.Get<Coordinate>().Value;
                GD.Print($"[GameStateManager] Player position after restore: {restoredPos}");
            }

            // 12. Restart player turn
            _turnSystem?.RestartPlayerTurn(snapshot.CurrentTurnIndex);

            return new RewindResult
            {
                Success = true,
                TurnRewindedTo = snapshot.TurnNumber,
                RespawnedUnitIds = respawnedUnitIds
            };
        }

        /// <summary>
        /// Checks if rewind is currently available.
        /// Need at least 2 snapshots (current turn + previous turn to rewind to)
        /// </summary>
        public bool CanRewind => _history.Count >= 2 && _cooldownRemaining == 0;

        /// <summary>
        /// Gets the current cooldown remaining.
        /// </summary>
        public int CooldownRemaining => _cooldownRemaining;

        /// <summary>
        /// Advances cooldown by one turn. Called at start of each player turn.
        /// </summary>
        public void TickCooldown()
        {
            if (_cooldownRemaining > 0)
            {
                _cooldownRemaining--;
            }
        }

        /// <summary>
        /// Gets the number of snapshots in history.
        /// </summary>
        public int HistoryDepth => _history.Count;

        /// <summary>
        /// Clears all history. Used when starting a new game.
        /// </summary>
        public void ClearHistory()
        {
            _history.Clear();
            _cooldownRemaining = 0;
        }

        /// <summary>
        /// Gets a read-only view of the history for UI preview.
        /// </summary>
        public IReadOnlyList<GameStateSnapshot> GetHistory() => _history;

        /// <summary>
        /// Peeks at a snapshot without removing it. For UI preview.
        /// </summary>
        /// <param name="turnsBack">0 = most recent, 1 = one turn ago, etc.</param>
        public GameStateSnapshot PeekSnapshot(int turnsBack = 0)
        {
            var index = _history.Count - 1 - turnsBack;
            if (index < 0 || index >= _history.Count) return null;
            return _history[index];
        }

        // ========== HELPER METHODS ==========

        private async Task AnimateUnitsToSnapshotPositions(GameStateSnapshot snapshot)
        {
            var animationTasks = new List<Task>();

            foreach (var entitySnap in snapshot.Entities.Where(e => e.IsUnit && e.AnimationOrigin.HasValue))
            {
                var currentEntity = Entities.Query<Unit>()
                    .FirstOrDefault(e => e.Id == entitySnap.EntityId);

                if (currentEntity == null || !currentEntity.Has<Instance>() || !currentEntity.Has<Coordinate>())
                    continue;

                var currentPos = currentEntity.Get<Coordinate>().Value;
                var targetPos = entitySnap.AnimationOrigin.Value;

                if (currentPos == targetPos) continue;

                var instance = currentEntity.Get<Instance>().Node;
                if (instance == null) continue;

                var targetWorldPos = HexGrid.HexToWorld(new Coordinate(targetPos));
                var tween = instance.CreateTween();
                tween.TweenProperty(instance, "position", targetWorldPos, Config.RewindAnimationSpeed);

                var tcs = new TaskCompletionSource<bool>();
                tween.Finished += () => tcs.SetResult(true);
                animationTasks.Add(tcs.Task);
            }

            if (animationTasks.Count > 0)
            {
                await Task.WhenAll(animationTasks);
            }
        }

        private void RestoreEntityState(GameStateSnapshot snapshot)
        {
            // 1. Remove all unit visual nodes
            var unitIds = new HashSet<int>();
            foreach (var entity in Entities.Query<Unit>().ToList())
            {
                unitIds.Add(entity.Id);
                if (entity.Has<Instance>())
                {
                    var instance = entity.Get<Instance>().Node;
                    if (instance != null && GodotObject.IsInstanceValid(instance))
                    {
                        instance.GetParent()?.RemoveChild(instance);
                        instance.Free();
                    }
                }
            }

            // 2. Remove unit entities (keep tiles)
            foreach (var unitId in unitIds)
            {
                var entity = Entities.GetEntity(unitId);
                if (entity != null)
                {
                    Entities.RemoveEntity(entity);
                }
            }

            // 3. Reset entity ID counter
            Entities.SetNextId(snapshot.NextEntityId);

            // 4. Restore entities from snapshot
            foreach (var entitySnap in snapshot.Entities)
            {
                if (Entities.GetEntities().TryGetValue(entitySnap.EntityId, out var existingEntity))
                {
                    // Tile: restore components but keep Instance
                    RestoreTileComponents(existingEntity, entitySnap);
                }
                else
                {
                    // Unit: create new entity
                    var entity = new Entity(entitySnap.EntityId);
                    foreach (var (type, component) in entitySnap.Components)
                    {
                        entity.AddComponent(type, component);
                    }
                    // Override coordinate with animation origin
                    if (entitySnap.AnimationOrigin.HasValue)
                    {
                        entity.Update(new Coordinate(entitySnap.AnimationOrigin.Value));
                    }
                    Entities.AddEntity(entity);
                }
            }
        }

        private void RestoreTileComponents(Entity tile, EntityStateSnapshot snapshot)
        {
            var currentComponents = tile.GetComponents().Keys.ToList();

            // Remove all except Instance and Name
            foreach (var type in currentComponents)
            {
                if (type != typeof(Instance) && type != typeof(Name))
                {
                    // Use reflection to call Remove<T>()
                    var removeMethod = typeof(Entity).GetMethod(nameof(Entity.Remove));
                    var genericRemove = removeMethod.MakeGenericMethod(type);
                    genericRemove.Invoke(tile, null);
                }
            }

            // Restore from snapshot
            foreach (var (type, component) in snapshot.Components)
            {
                if (_capturedComponentTypes.Contains(type))
                {
                    tile.AddComponent(type, component);
                }
            }

            // Update coordinate
            if (snapshot.AnimationOrigin.HasValue)
            {
                tile.Update(new Coordinate(snapshot.AnimationOrigin.Value));
            }
        }

        private void RebuildVisualState()
        {
            var units = Entities.Query<Unit>().ToList();
            var rootNode = Entities.GetRootNode();
            var unitContainer = rootNode.GetNodeOrNull<Node3D>("Units");

            foreach (var entity in units)
            {
                var coord = entity.Get<Coordinate>();
                var unitType = entity.Get<Unit>().Type;
                var name = entity.Has<Name>() ? entity.Get<Name>().ToString() : $"Unit_{entity.Id}";

                var scenePath = GetUnitScenePath(unitType);
                var scene = ResourceLoader.Load<PackedScene>(scenePath);

                if (scene == null)
                {
                    GD.PrintErr($"[GameStateManager] Failed to load scene at {scenePath}");
                    continue;
                }

                var instance = scene.Instantiate<Node3D>();
                if (instance == null)
                {
                    GD.PrintErr($"[GameStateManager] Failed to instantiate scene");
                    continue;
                }

                instance.Name = name;
                instance.Position = HexGrid.HexToWorld(coord);

                if (unitContainer != null)
                    unitContainer.AddChild(instance);
                else
                    rootNode.AddChild(instance);

                entity.Add(new Instance(instance));

                var animPlayer = instance.GetNodeOrNull<Godot.AnimationPlayer>("AnimationPlayer");
                if (animPlayer != null)
                {
                    entity.Add(new Components.AnimationPlayer(animPlayer));
                }
            }
        }

        private async Task ApplyRespawnFadeInEffects(List<int> respawnedUnitIds)
        {
            if (respawnedUnitIds.Count == 0) return;

            var fadeTasks = new List<Task>();

            foreach (var unitId in respawnedUnitIds)
            {
                Entity entity;
                try
                {
                    entity = Entities.GetEntity(unitId);
                }
                catch
                {
                    continue;
                }

                if (entity == null || !entity.Has<Instance>()) continue;

                var instance = entity.Get<Instance>().Node;
                if (instance == null) continue;

                // Set initial transparency
                SetNodeTransparency(instance, 0f);

                // Animate fade-in
                var tween = instance.CreateTween();
                tween.TweenMethod(
                    Callable.From<float>(alpha => SetNodeTransparency(instance, alpha)),
                    0f, 1f, Config.RespawnFadeInDuration
                );

                var tcs = new TaskCompletionSource<bool>();
                tween.Finished += () => tcs.SetResult(true);
                fadeTasks.Add(tcs.Task);
            }

            if (fadeTasks.Count > 0)
            {
                await Task.WhenAll(fadeTasks);
            }
        }

        private void SetNodeTransparency(Node3D node, float alpha)
        {
            // Find MeshInstance3D children and set their material transparency
            foreach (var child in node.GetChildren())
            {
                if (child is MeshInstance3D mesh)
                {
                    for (int i = 0; i < mesh.GetSurfaceOverrideMaterialCount(); i++)
                    {
                        var material = mesh.GetSurfaceOverrideMaterial(i);
                        if (material is StandardMaterial3D stdMat)
                        {
                            var color = stdMat.AlbedoColor;
                            stdMat.AlbedoColor = new Color(color.R, color.G, color.B, alpha);
                            stdMat.Transparency = alpha < 1f
                                ? BaseMaterial3D.TransparencyEnum.Alpha
                                : BaseMaterial3D.TransparencyEnum.Disabled;
                        }
                    }

                    // Also check the mesh's active material
                    var activeMat = mesh.MaterialOverride;
                    if (activeMat is StandardMaterial3D activeStdMat)
                    {
                        var color = activeStdMat.AlbedoColor;
                        activeStdMat.AlbedoColor = new Color(color.R, color.G, color.B, alpha);
                        activeStdMat.Transparency = alpha < 1f
                            ? BaseMaterial3D.TransparencyEnum.Alpha
                            : BaseMaterial3D.TransparencyEnum.Disabled;
                    }
                }

                if (child is Node3D childNode)
                {
                    SetNodeTransparency(childNode, alpha);
                }
            }
        }

        private void RecalculateDerivedState()
        {
            // Trigger range recalculation by firing MoveCompleted event
            var player = Entities.Query<Player>().FirstOrDefault();
            if (player != null && player.Has<Coordinate>())
            {
                var coord = player.Get<Coordinate>().Value;
                Events.OnMoveCompleted(player, coord, coord);
            }
        }

        private string GetUnitScenePath(UnitType unitType)
        {
            return unitType switch
            {
                UnitType.Wizard => "res://src/Scenes/Wizard.tscn",
                UnitType.SniperAxisQ => "res://src/Scenes/Sniper.tscn",
                UnitType.SniperAxisR => "res://src/Scenes/Sniper.tscn",
                UnitType.SniperAxisS => "res://src/Scenes/Sniper.tscn",
                _ => $"res://src/Scenes/{unitType}.tscn"
            };
        }

        public override void Cleanup()
        {
            _history.Clear();
        }
    }
}
