using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Game
{
    public partial class Events : Node, ISystem
    {
        public event Action<Entity> UnitDefeated;
        public event Action<Entity, Vector3I, Vector3I> MoveCompleted;
        public event Action<Entity> TurnChanged;
        public event Action<Entity> TurnRestarted;  // Fired after rewind, doesn't tick cooldowns
        public event Action<Entity> TurnEnd;
        public event Action<Entity> TileSelect;
        public event Action<Entity> TileHover;
        public event Action<Entity> TileUnhover;
        public event Action<Entity> UnitSelect;
        public event Action<Entity> UnitHover;
        public event Action<Entity> UnitUnhover;
        public event Action<Entity> UnitRightClick;
        public event Action<Entity> EntityRightClick;
        public event Action<Entity> OnUnitActionComplete;
        public event Action<IEnumerable<Entity>> GridReady;
        public event Action<int, Type, object> ComponentChanged;
        public event Action GameOver;
        public event Action SpawnsComplete;

        public static Events Instance { get; private set; }

        public override void _Ready()
        {
            Instance = this;
        }

        public void UnitActionComplete(Entity unit)
        {
            OnUnitActionComplete?.Invoke(unit);
        }

        public void OnGridReady(IEnumerable<Entity> grid)
        {
            GridReady?.Invoke(grid);
        }

        public void OnMoveCompleted(Entity unit, Vector3I from, Vector3I to)
        {
            MoveCompleted?.Invoke(unit, from, to);
        }

        public void OnUnitDefeated(Entity unit)
        {
            UnitDefeated?.Invoke(unit);
        }

        public void OnComponentChanged(int id, Type type, object obj)
        {
            ComponentChanged?.Invoke(id, type, obj);
        }

        public void OnTileSelect(Entity tile)
        {
            TileSelect?.Invoke(tile);
        }

        public void OnTurnChanged(Entity tile)
        {
            TurnChanged?.Invoke(tile);
        }

        /// <summary>
        /// Fired after rewind to restart player's turn without ticking cooldowns
        /// </summary>
        public void OnTurnRestarted(Entity unit)
        {
            TurnRestarted?.Invoke(unit);
        }

        public void EndTurn(Entity tile)
        {
            TurnEnd?.Invoke(tile);
        }

        public void OnTileHover(Entity tile)
        {
            TileHover?.Invoke(tile);
        }

        public void OnTileUnhover(Entity tile)
        {
            TileUnhover?.Invoke(tile);
        }

        public void OnUnitHover(Entity tile)
        {
            UnitHover?.Invoke(tile);
        }

        public void OnUnitUnhover(Entity tile)
        {
            UnitUnhover?.Invoke(tile);
        }

        internal void OnUnitSelect(Entity entity)
        {
            UnitSelect?.Invoke(entity);
        }

        public void OnUnitRightClick(Entity entity)
        {
            UnitRightClick?.Invoke(entity);
        }

        public void OnEntityRightClick(Entity entity)
        {
            EntityRightClick?.Invoke(entity);
        }

        public void OnGameOver()
        {
            GameOver?.Invoke();
        }

        public void OnSpawnsComplete()
        {
            SpawnsComplete?.Invoke();
        }
    }
}
