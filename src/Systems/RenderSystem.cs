using System.Linq;
using Game.Components;
using Godot;

namespace Game
{
	public class RenderSystem : System
	{
		private readonly Node3D _boardContainer = new() { Name = "Board" };
		private readonly Node3D _unitContainer = new() { Name = "Units" };
		private readonly PackedScene _tileScene = ResourceLoader.Load<PackedScene>("res://src/Scenes/HexTile.tscn");
		private readonly PackedScene _waterHexScene = ResourceLoader.Load<PackedScene>("res://assets/models/environment/hex_water.gltf");

		public override void Initialize()
		{
			Entities.GetRootNode().AddChild(_boardContainer);
			Entities.GetRootNode().AddChild(_unitContainer);

			foreach (Entity entity in Entities.Query<Instance>())
			{
				if (entity.Has<Tile>())
				{
					var tileScene = _tileScene.Instantiate<Node3D>();
					var tileInstance = entity.Update(new Instance(tileScene));

					_boardContainer.AddChild(tileInstance.Node);

					tileInstance.Node.Position = HexGrid.HexToWorld(entity.Get<Coordinate>());
					tileInstance.Node.Name = entity.Get<Name>();

					// Replace grass with water for blocked (non-traversable) tiles
					if (!entity.Has<Traversable>())
					{
						var meshNode = tileInstance.Node.GetNode<MeshInstance3D>("Mesh");
						var grassModel = meshNode.GetChild<Node3D>(0);

						// Store the transform from the original grass model
						var originalTransform = grassModel.Transform;

						// Remove the grass model
						grassModel.QueueFree();
						meshNode.RemoveChild(grassModel);

						// Instantiate and add the water model
						var waterModel = _waterHexScene.Instantiate<Node3D>();
						waterModel.Transform = originalTransform;
						meshNode.AddChild(waterModel);
					}
				}
				SetupTileInput(entity);

				if (entity.Has<Unit>())
				{
					var scenePath = GetUnitScenePath(entity.Get<Unit>());
					var unitScene = ResourceLoader.Load<PackedScene>(scenePath);
					var unitSceneInstance = unitScene.Instantiate<Node3D>();
					var unitInstance = entity.Update(new Instance(unitSceneInstance));

					_unitContainer.AddChild(unitInstance.Node);
					unitInstance.Node.Position = HexGrid.HexToWorld(entity.Get<Coordinate>());
					unitInstance.Node.Name = entity.Get<Name>();

					SetupUnitInput(entity);
				}
			}

			Events.OnGridReady(Entities.Query<Tile>());
		}

		/// <summary>
		/// Maps unit types to their corresponding scene file paths.
		/// Wizard and all sniper variants use the base Sniper.tscn scene.
		/// </summary>
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

		public void SetupTileInput(Entity entity)
		{
			if (!entity.Has<Tile>())
				return;

			if (entity.Get<Instance>().Node is Area3D tileBody)
			{
				tileBody.InputEvent += (camera, @event, position, normal, shapeIdx) =>
				{
					if (@event is InputEventMouseButton mouseEvent &&
						mouseEvent.ButtonIndex == MouseButton.Left &&
						mouseEvent.Pressed)
					{
						Events.OnTileSelect(entity);
					}
				};

				tileBody.InputEvent += (camera, @event, position, normal, shapeIdx) =>
				{
					if (@event is InputEventMouseButton mouseEvent &&
						mouseEvent.ButtonIndex == MouseButton.Right &&
						mouseEvent.Pressed)
					{
						Events.OnUnitRightClick(entity);
					}
				};

				tileBody.MouseEntered += () =>
				{
					Events.OnTileHover(entity);
				};

				tileBody.MouseExited += () =>
				{
					Events.OnTileUnhover(entity);
				};
			}
		}

		public void SetupUnitInput(Entity entity)
		{
			if (!entity.Has<Unit>())
				return;

			if (entity.Get<Instance>().Node is Area3D unitBody)
			{
				unitBody.InputEvent += (camera, @event, position, normal, shapeIdx) =>
				{
					if (@event is InputEventMouseButton mouseEvent &&
						mouseEvent.ButtonIndex == MouseButton.Left &&
						mouseEvent.Pressed)
					{
						Events.OnUnitSelect(entity);
					}
				};

				unitBody.InputEvent += (camera, @event, position, normal, shapeIdx) =>
				{
					if (@event is InputEventMouseButton mouseEvent &&
						mouseEvent.ButtonIndex == MouseButton.Right &&
						mouseEvent.Pressed)
					{
						Events.OnUnitRightClick(entity);
					}
				};

				unitBody.MouseEntered += () =>
				{
					Events.OnUnitHover(entity);
				};

				unitBody.MouseExited += () =>
				{
					Events.OnUnitUnhover(entity);
				};
			}
		}
	}
}
