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

			_systems = new Systems(this);

			var entityManager = _systems.GetEntityManager();

			_systems.RegisterConcurrent<ComponentDebugSystem>();
			_systems.RegisterConcurrent<DebugSystem>();
			_systems.RegisterConcurrent<TileHighlightSystem>();

			_systems.Register<RenderSystem>();
			_systems.Register<AnimationSystem>();
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
			entityManager.Factory.CreateEnemy(UnitType.SniperAxisQ);
			entityManager.Factory.CreateEnemy(UnitType.SniperAxisR);
			entityManager.Factory.CreateEnemy(UnitType.SniperAxisS);



			_systems.Initialize();
		}

		private async void OnTurnChanged(Entity entity)
		{
			await _systems.Update();
		}
	}
}
