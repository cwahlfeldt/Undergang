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
        private Control _abilitiesContainer;
        private Button _dashButton;
        private Label _dashCooldownLabel;
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

            // Create container for abilities
            _abilitiesContainer = canvasLayer.GetNodeOrNull<Control>("AbilitiesContainer");
            if (_abilitiesContainer == null)
            {
                _abilitiesContainer = new Control
                {
                    Name = "AbilitiesContainer",
                    Position = new Vector2(20, 100),
                    Size = new Vector2(200, 80)
                };
                canvasLayer.AddChild(_abilitiesContainer);
            }

            CreateDashButton();

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

        private void CreateDashButton()
        {
            // Create dash button
            _dashButton = new Button
            {
                Name = "DashButton",
                Text = "DASH",
                Position = new Vector2(0, 0),
                Size = new Vector2(120, 40)
            };

            // Style the button
            _dashButton.AddThemeColorOverride("font_color", new Color(1, 1, 1));
            _dashButton.AddThemeColorOverride("font_hover_color", new Color(0.8f, 0.9f, 1));
            _dashButton.AddThemeColorOverride("font_pressed_color", new Color(0.6f, 0.7f, 0.8f));
            _dashButton.AddThemeColorOverride("font_disabled_color", new Color(0.5f, 0.5f, 0.5f));

            _dashButton.Pressed += OnDashButtonPressed;
            _abilitiesContainer.AddChild(_dashButton);

            // Create cooldown label
            _dashCooldownLabel = new Label
            {
                Name = "DashCooldownLabel",
                Position = new Vector2(0, 45),
                Size = new Vector2(120, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _dashCooldownLabel.AddThemeColorOverride("font_color", new Color(1, 0.8f, 0.2f));
            _abilitiesContainer.AddChild(_dashCooldownLabel);
        }

        private void OnDashButtonPressed()
        {
            var player = Entities.Query<Player>().FirstOrDefault();
            if (player == null) return;

            // Check if dash is ready
            if (!player.Has<DashReady>())
            {
                GD.Print("Dash is on cooldown!");
                return;
            }

            // Toggle dash mode
            if (player.Has<DashMode>())
            {
                player.Remove<DashMode>();
                GD.Print("Dash mode deactivated");
            }
            else
            {
                player.Add(new DashMode());
                GD.Print("Dash mode activated");
            }

            Events.OnDashModeToggled();
            UpdateDashButton();
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

            // Update dash button state
            UpdateDashButton();

            await Task.CompletedTask;
        }

        private void UpdateDashButton()
        {
            if (_dashButton == null || _player == null) return;

            // Check if dash is ready
            bool isDashReady = _player.Has<DashReady>();
            bool isDashMode = _player.Has<DashMode>();

            // Enable/disable button
            _dashButton.Disabled = !isDashReady;

            // Update button appearance based on mode
            if (isDashMode)
            {
                _dashButton.Text = "DASH (ACTIVE)";
                _dashButton.Modulate = new Color(0.5f, 1f, 0.5f); // Green tint
            }
            else
            {
                _dashButton.Text = "DASH";
                _dashButton.Modulate = isDashReady ? Colors.White : new Color(0.6f, 0.6f, 0.6f);
            }

            // Update cooldown label
            if (_player.Has<AbilityCooldown>())
            {
                int cooldown = _player.Get<AbilityCooldown>();
                _dashCooldownLabel.Text = $"Cooldown: {cooldown}";
                _dashCooldownLabel.Visible = true;
            }
            else
            {
                _dashCooldownLabel.Text = "";
                _dashCooldownLabel.Visible = false;
            }
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

            if (_dashButton != null)
            {
                _dashButton.Pressed -= OnDashButtonPressed;
                _dashButton.QueueFree();
            }

            if (_dashCooldownLabel != null)
            {
                _dashCooldownLabel.QueueFree();
            }

            foreach (var heart in _hearts)
            {
                heart.QueueFree();
            }
            _hearts.Clear();
        }
    }
}