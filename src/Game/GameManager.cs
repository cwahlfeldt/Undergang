using Game.Components;
using Godot;

namespace Game
{
	public partial class GameManager : Node3D
	{
		private Systems _systems;

		public override void _Ready()
		{
			Events.Instance.TurnChanged += OnTurnChanged;
			Events.Instance.TurnRestarted += OnTurnChanged;  // Handle rewind restart same as turn change
			Events.Instance.GameOver += OnGameOver;

			_systems = new Systems(this);

			var entityManager = _systems.GetEntityManager();

			_systems.RegisterConcurrent<DebugSystem>();
			_systems.RegisterConcurrent<TileHighlightSystem>();

			_systems.Register<RenderSystem>();
			_systems.Register<AnimationSystem>();
			_systems.Register<DashSystem>();
			_systems.Register<BlockSystem>();
			_systems.Register<PositionHistoryRecorder>();
			_systems.Register<GameStateManager>();
			_systems.Register<UISystem>();
			_systems.Register<TurnSystem>();
			_systems.Register<EnemySystem>();
			_systems.Register<PlayerSystem>();
			_systems.Register<RangeSystem>();
			_systems.Register<MovementSystem>();
			_systems.Register<CombatSystem>();

			entityManager.Factory.CreateGrid(5);
			entityManager.Factory.CreatePlayer();
			entityManager.Factory.CreateEnemy(UnitType.Grunt);
			entityManager.Factory.CreateEnemy(UnitType.Grunt);
			entityManager.Factory.CreateEnemy(UnitType.Wizard);

			_systems.Initialize();

			// Wire up Tweener to PositionHistoryRecorder for transform recording
			var positionRecorder = _systems.Get<PositionHistoryRecorder>();
			Tweener.Instance.SetPositionRecorder(positionRecorder);
		}

		public override void _Process(double delta)
		{
			// Call Process on all systems for per-frame updates (e.g., position recording)
			_systems.Process((float)delta);
		}

		private async void OnTurnChanged(Entity entity)
		{
			await _systems.Update();
		}

		private void OnGameOver()
		{
			// Game over - could show UI, restart option, etc.
		}
	}
}
