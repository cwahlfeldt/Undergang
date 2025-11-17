using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Components;
using Godot;

namespace Game
{
    public class UISystem : System
    {
        private readonly List<Control> _hearts = [];
        private Control _uiContainer;
        private const int HEART_SIZE = 48;
        private const int HEART_SPACING = 8;
        private int _currentPlayerHealth = 0;
        private Entity _player;

        public override void Initialize()
        {
            // Find or create UI container
            var rootNode = Entities.GetRootNode();

            // Create CanvasLayer for UI
            var canvasLayer = rootNode.GetNodeOrNull<CanvasLayer>("UI");
            if (canvasLayer == null)
            {
                canvasLayer = new CanvasLayer { Name = "UI" };
                rootNode.AddChild(canvasLayer);
            }

            // Create container for hearts
            _uiContainer = canvasLayer.GetNodeOrNull<Control>("HealthContainer");
            if (_uiContainer == null)
            {
                _uiContainer = new Control
                {
                    Name = "HealthContainer",
                    Position = new Vector2(20, 20),
                    Size = new Vector2(400, 60)
                };
                canvasLayer.AddChild(_uiContainer);
            }

            // Get player and initial health
            _player = Entities.Query<Player>().FirstOrDefault();
            if (_player != null && _player.Has<Health>())
            {
                _currentPlayerHealth = _player.Get<Health>();
                UpdateHearts(_currentPlayerHealth);
            }

            // Subscribe to component changes
            Events.ComponentChanged += OnComponentChanged;
        }

        public override async Task Update()
        {
            // Check if player health changed (backup check if event doesn't fire)
            if (_player != null && _player.Has<Health>())
            {
                int health = _player.Get<Health>();
                if (health != _currentPlayerHealth)
                {
                    _currentPlayerHealth = health;
                    UpdateHearts(_currentPlayerHealth);
                }
            }

            await Task.CompletedTask;
        }

        private void OnComponentChanged(int entityId, Type componentType, object newValue)
        {
            // Check if this is the player's health changing
            if (_player != null && entityId == _player.Id && componentType == typeof(Health))
            {
                if (newValue is Health newHealth)
                {
                    _currentPlayerHealth = newHealth.Value;
                    UpdateHearts(_currentPlayerHealth);
                }
            }
        }

        private void UpdateHearts(int healthCount)
        {
            // Remove existing hearts
            foreach (var heart in _hearts)
            {
                heart.QueueFree();
            }
            _hearts.Clear();

            // Create new hearts based on current health
            for (int i = 0; i < healthCount; i++)
            {
                AddHeart(i);
            }
        }

        private void AddHeart(int index)
        {
            var heartContainer = new Control
            {
                Position = new Vector2(index * (HEART_SIZE + HEART_SPACING), 0),
                Size = new Vector2(HEART_SIZE, HEART_SIZE)
            };

            // Create heart shape using ColorRect (simple red square with rotation to look like diamond)
            var heart = new ColorRect
            {
                Color = new Color(0.9f, 0.1f, 0.2f, 1), // Red color
                Size = new Vector2(HEART_SIZE * 0.7f, HEART_SIZE * 0.7f),
                Position = new Vector2(HEART_SIZE * 0.15f, HEART_SIZE * 0.15f)
            };

            // Add a border/outline effect
            var border = new ColorRect
            {
                Color = new Color(0.4f, 0.05f, 0.1f, 1), // Dark red outline
                Size = new Vector2(HEART_SIZE * 0.76f, HEART_SIZE * 0.76f),
                Position = new Vector2(HEART_SIZE * 0.12f, HEART_SIZE * 0.12f)
            };

            heartContainer.AddChild(border);
            heartContainer.AddChild(heart);

            _hearts.Add(heartContainer);
            _uiContainer.AddChild(heartContainer);
        }

        public override void Cleanup()
        {
            Events.ComponentChanged -= OnComponentChanged;

            foreach (var heart in _hearts)
            {
                heart.QueueFree();
            }
            _hearts.Clear();
        }
    }
}