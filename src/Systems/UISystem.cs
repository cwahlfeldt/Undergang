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
        // Button color themes
        private static readonly ButtonTheme DashTheme = new(
            normal: new Color(0.2f, 0.5f, 1.0f, 0.8f),
            hover: new Color(0.3f, 0.6f, 1.0f, 0.9f),
            pressed: new Color(0.1f, 0.4f, 0.8f, 1.0f),
            border: new Color(0.1f, 0.3f, 0.7f, 1.0f)
        );

        private static readonly ButtonTheme BlockTheme = new(
            normal: new Color(0.2f, 0.8f, 0.3f, 0.8f),
            hover: new Color(0.3f, 0.9f, 0.4f, 0.9f),
            pressed: new Color(0.1f, 0.6f, 0.2f, 1.0f),
            border: new Color(0.1f, 0.5f, 0.2f, 1.0f)
        );

        private static readonly ButtonTheme BlockActiveTheme = new(
            normal: new Color(0.8f, 1.0f, 0.2f, 1.0f),
            hover: new Color(0.8f, 1.0f, 0.2f, 1.0f),
            pressed: new Color(0.8f, 1.0f, 0.2f, 1.0f),
            border: new Color(0.5f, 0.7f, 0.1f, 1.0f),
            borderWidth: 3
        );

        private static readonly ButtonTheme RewindTheme = new(
            normal: new Color(0.6f, 0.2f, 0.8f, 0.8f),
            hover: new Color(0.7f, 0.3f, 0.9f, 0.9f),
            pressed: new Color(0.5f, 0.1f, 0.6f, 1.0f),
            border: new Color(0.4f, 0.1f, 0.5f, 1.0f)
        );

        private static readonly ButtonTheme DisabledTheme = new(
            normal: new Color(0.3f, 0.3f, 0.3f, 0.5f),
            hover: new Color(0.3f, 0.3f, 0.3f, 0.5f),
            pressed: new Color(0.3f, 0.3f, 0.3f, 0.5f),
            border: new Color(0.2f, 0.2f, 0.2f, 1.0f)
        );

        private record ButtonTheme(Color normal, Color hover, Color pressed, Color border, int borderWidth = 2);

        private static StyleBoxFlat CreateStyleBox(Color bgColor, Color borderColor, int borderWidth = 2)
        {
            return new StyleBoxFlat
            {
                BgColor = bgColor,
                BorderColor = borderColor,
                BorderWidthLeft = borderWidth,
                BorderWidthRight = borderWidth,
                BorderWidthTop = borderWidth,
                BorderWidthBottom = borderWidth,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8
            };
        }

        private static void ApplyButtonTheme(Button button, ButtonTheme theme)
        {
            button.AddThemeStyleboxOverride("normal", CreateStyleBox(theme.normal, theme.border, theme.borderWidth));
            button.AddThemeStyleboxOverride("hover", CreateStyleBox(theme.hover, theme.border, theme.borderWidth));
            button.AddThemeStyleboxOverride("pressed", CreateStyleBox(theme.pressed, theme.border, theme.borderWidth));
            button.AddThemeStyleboxOverride("disabled", CreateStyleBox(DisabledTheme.normal, DisabledTheme.border));
        }

        private readonly List<Control> _hearts = [];
        private Control _uiContainer;
        private Button _dashButton;
        private Label _dashCooldownLabel;
        private Button _blockButton;
        private Label _blockCooldownLabel;
        private Button _rewindButton;
        private Label _rewindCooldownLabel;
        private Label _fpsLabel;
        private DashSystem _dashSystem;
        private BlockSystem _blockSystem;
        private GameStateManager _gameStateManager;
        private TileHighlightSystem _tileHighlightSystem;
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

            // Get system references
            _dashSystem = Systems.Get<DashSystem>();
            _blockSystem = Systems.Get<BlockSystem>();
            _gameStateManager = Systems.Get<GameStateManager>();
            _tileHighlightSystem = Systems.Get<TileHighlightSystem>();

            // Create dash button
            CreateDashButton(canvasLayer);

            // Create block button
            CreateBlockButton(canvasLayer);

            // Create rewind button
            CreateRewindButton(canvasLayer);

            // Create FPS counter
            CreateFpsCounter(canvasLayer);

            // Subscribe to component changes
            Events.ComponentChanged += OnComponentChanged;
            Events.TurnChanged += OnTurnChanged;
            Events.TurnRestarted += OnTurnChanged;  // Handle rewind restart same as turn change for UI
            Events.UnitDefeated += OnUnitDefeated;
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

            // Update ability button states (ensures UI stays in sync)
            UpdateDashButtonState();
            UpdateBlockButtonState();
            UpdateRewindButtonState();

            // Update FPS counter
            UpdateFpsCounter();

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

        private void CreateDashButton(CanvasLayer canvasLayer)
        {
            // Create container for dash button
            var dashContainer = new Control
            {
                Name = "DashContainer",
                Position = new Vector2(20, 100),
                Size = new Vector2(200, 80)
            };
            canvasLayer.AddChild(dashContainer);

            // Create dash button
            _dashButton = new Button
            {
                Name = "DashButton",
                Text = "DASH (D)",
                Size = new Vector2(180, 50),
                Position = new Vector2(0, 0)
            };

            ApplyButtonTheme(_dashButton, DashTheme);
            _dashButton.Pressed += OnDashButtonPressed;
            dashContainer.AddChild(_dashButton);

            // Create cooldown label
            _dashCooldownLabel = new Label
            {
                Name = "DashCooldownLabel",
                Position = new Vector2(0, 55),
                Size = new Vector2(180, 25),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visible = false
            };

            _dashCooldownLabel.AddThemeFontSizeOverride("font_size", 18);
            _dashCooldownLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.3f, 0.3f, 1.0f));

            dashContainer.AddChild(_dashCooldownLabel);

            // Initial update
            UpdateDashButtonState();
        }

        private void CreateBlockButton(CanvasLayer canvasLayer)
        {
            // Create container for block button
            var blockContainer = new Control
            {
                Name = "BlockContainer",
                Position = new Vector2(20, 190),  // Below dash button
                Size = new Vector2(200, 80)
            };
            canvasLayer.AddChild(blockContainer);

            // Create block button
            _blockButton = new Button
            {
                Name = "BlockButton",
                Text = "BLOCK (B)",
                Size = new Vector2(180, 50),
                Position = new Vector2(0, 0)
            };

            ApplyButtonTheme(_blockButton, BlockTheme);
            _blockButton.Pressed += OnBlockButtonPressed;
            blockContainer.AddChild(_blockButton);

            // Create cooldown label
            _blockCooldownLabel = new Label
            {
                Name = "BlockCooldownLabel",
                Position = new Vector2(0, 55),
                Size = new Vector2(180, 25),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visible = false
            };

            _blockCooldownLabel.AddThemeFontSizeOverride("font_size", 18);
            _blockCooldownLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.3f, 0.3f, 1.0f));

            blockContainer.AddChild(_blockCooldownLabel);

            // Initial update
            UpdateBlockButtonState();
        }

        private void CreateRewindButton(CanvasLayer canvasLayer)
        {
            // Create container for rewind button
            var rewindContainer = new Control
            {
                Name = "RewindContainer",
                Position = new Vector2(20, 280),  // Below block button
                Size = new Vector2(200, 80)
            };
            canvasLayer.AddChild(rewindContainer);

            // Create rewind button
            _rewindButton = new Button
            {
                Name = "RewindButton",
                Text = "REWIND",
                Size = new Vector2(180, 50),
                Position = new Vector2(0, 0)
            };

            ApplyButtonTheme(_rewindButton, RewindTheme);
            _rewindButton.Pressed += OnRewindButtonPressed;
            rewindContainer.AddChild(_rewindButton);

            // Create cooldown label
            _rewindCooldownLabel = new Label
            {
                Name = "RewindCooldownLabel",
                Position = new Vector2(0, 55),
                Size = new Vector2(180, 25),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visible = false
            };

            _rewindCooldownLabel.AddThemeFontSizeOverride("font_size", 18);
            _rewindCooldownLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.3f, 0.3f, 1.0f));

            rewindContainer.AddChild(_rewindCooldownLabel);

            // Initial update
            UpdateRewindButtonState();
        }

        private async void OnRewindButtonPressed()
        {
            if (_player == null) return;

            if (_gameStateManager.CanRewind)
            {
                await _gameStateManager.RewindOneTurn();
                // Update button state after rewind
                UpdateRewindButtonState();
            }
        }

        private void OnBlockButtonPressed()
        {
            if (_player == null) return;

            // Toggle block on/off
            _blockSystem.ToggleBlock();

            // Update button visual state immediately
            UpdateBlockButtonState();
        }

        private void OnDashButtonPressed()
        {
            if (_player == null) return;

            // Toggle dash mode
            _dashSystem.ToggleDashMode();

            // Update button visual state
            UpdateDashButtonState();

            // Refresh visualization
            _tileHighlightSystem.RefreshDashVisualization();
        }

        private void OnTurnChanged(Entity unit)
        {
            // Refresh player reference in case entity was recreated (e.g., after rewind)
            _player = Entities.Query<Player>().FirstOrDefault();

            // Update health display
            if (_player != null && _player.Has<Health>())
            {
                int health = _player.Get<Health>();
                if (health != _currentPlayerHealth)
                {
                    _currentPlayerHealth = health;
                    UpdateHearts(_currentPlayerHealth);
                }
            }

            // Update ability buttons when turn changes
            if (unit.Has<Player>())
            {
                UpdateDashButtonState();
                UpdateBlockButtonState();
                UpdateRewindButtonState();
            }
        }

        private void UpdateDashButtonState()
        {
            if (_player == null || _dashButton == null) return;

            bool isDashAvailable = _dashSystem.IsDashAvailable(_player);
            int cooldown = _dashSystem.GetRemainingCooldown(_player);
            bool isDashActive = _player.Has<DashModeActive>();

            if (isDashActive)
            {
                // Dash mode is active
                _dashButton.Text = "DASH (ON)";
                _dashButton.Disabled = false;
                _dashCooldownLabel.Visible = false;
            }
            else if (isDashAvailable)
            {
                // Dash is ready
                _dashButton.Text = "DASH (D)";
                _dashButton.Disabled = false;
                _dashCooldownLabel.Visible = false;
            }
            else
            {
                // Dash is on cooldown
                _dashButton.Text = "DASH (D)";
                _dashButton.Disabled = true;
                _dashCooldownLabel.Text = $"Cooldown: {cooldown}";
                _dashCooldownLabel.Visible = true;
            }
        }

        private void UpdateBlockButtonState()
        {
            if (_player == null || _blockButton == null) return;

            bool isBlockAvailable = _blockSystem.IsBlockAvailable(_player);
            int cooldown = _blockSystem.GetRemainingCooldown(_player);
            bool isBlockActive = _blockSystem.IsBlockActive(_player);

            if (isBlockActive)
            {
                // Block is active - can be toggled off
                _blockButton.Text = "BLOCK (ACTIVE!)";
                _blockButton.Disabled = false;
                _blockCooldownLabel.Visible = false;
                _blockButton.AddThemeStyleboxOverride("normal", CreateStyleBox(BlockActiveTheme.normal, BlockActiveTheme.border, BlockActiveTheme.borderWidth));
            }
            else if (isBlockAvailable)
            {
                // Block is ready
                _blockButton.Text = "BLOCK (B)";
                _blockButton.Disabled = false;
                _blockCooldownLabel.Visible = false;
                _blockButton.AddThemeStyleboxOverride("normal", CreateStyleBox(BlockTheme.normal, BlockTheme.border));
            }
            else
            {
                // Block is on cooldown
                _blockButton.Text = "BLOCK (B)";
                _blockButton.Disabled = true;
                _blockCooldownLabel.Text = $"Cooldown: {cooldown}";
                _blockCooldownLabel.Visible = true;
            }
        }

        private void UpdateRewindButtonState()
        {
            if (_rewindButton == null || _gameStateManager == null) return;

            bool canRewind = _gameStateManager.CanRewind;
            int cooldown = _gameStateManager.CooldownRemaining;
            bool hasSnapshot = _gameStateManager.HistoryDepth > 0;

            if (canRewind)
            {
                // Rewind is available - show history depth
                int depth = _gameStateManager.HistoryDepth;
                _rewindButton.Text = depth > 1 ? $"REWIND ({depth})" : "REWIND";
                _rewindButton.Disabled = false;
                _rewindCooldownLabel.Visible = false;
            }
            else if (cooldown > 0)
            {
                // Rewind is on cooldown
                _rewindButton.Text = "REWIND";
                _rewindButton.Disabled = true;
                _rewindCooldownLabel.Text = $"Cooldown: {cooldown}";
                _rewindCooldownLabel.Visible = true;
            }
            else if (!hasSnapshot)
            {
                // No snapshot available yet
                _rewindButton.Text = "REWIND";
                _rewindButton.Disabled = true;
                _rewindCooldownLabel.Text = "No snapshot";
                _rewindCooldownLabel.Visible = true;
            }
        }

        private void CreateFpsCounter(CanvasLayer canvasLayer)
        {
            // Create FPS label in top-right corner
            _fpsLabel = new Label
            {
                Name = "FpsCounter",
                Position = new Vector2(0, 10),
                Size = new Vector2(100, 30),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Text = "FPS: 0"
            };

            // Style the FPS label
            _fpsLabel.AddThemeFontSizeOverride("font_size", 20);
            _fpsLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.2f, 1.0f)); // Green color
            _fpsLabel.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f, 1.0f)); // Black outline
            _fpsLabel.AddThemeConstantOverride("outline_size", 2);

            // Position in top-right corner - we'll update this in Update() to account for window size
            canvasLayer.AddChild(_fpsLabel);
        }

        private void UpdateFpsCounter()
        {
            if (_fpsLabel == null) return;

            // Get current FPS from Engine
            int fps = (int)Engine.GetFramesPerSecond();
            _fpsLabel.Text = $"FPS: {fps}";

            // Update position to stay in top-right corner
            var viewport = _fpsLabel.GetViewport();
            if (viewport != null)
            {
                var viewportSize = viewport.GetVisibleRect().Size;
                _fpsLabel.Position = new Vector2(viewportSize.X - 110, 10);
            }
        }

        private void OnUnitDefeated(Entity unit)
        {
            // Update UI when any unit is defeated (could affect block/dash states)
            if (_player != null)
            {
                UpdateDashButtonState();
                UpdateBlockButtonState();
                UpdateRewindButtonState();
            }
        }

        public override void Cleanup()
        {
            Events.ComponentChanged -= OnComponentChanged;
            Events.TurnChanged -= OnTurnChanged;
            Events.TurnRestarted -= OnTurnChanged;
            Events.UnitDefeated -= OnUnitDefeated;

            if (_dashButton != null)
            {
                _dashButton.Pressed -= OnDashButtonPressed;
            }

            if (_blockButton != null)
            {
                _blockButton.Pressed -= OnBlockButtonPressed;
            }

            if (_rewindButton != null)
            {
                _rewindButton.Pressed -= OnRewindButtonPressed;
            }

            foreach (var heart in _hearts)
            {
                heart.QueueFree();
            }
            _hearts.Clear();
        }
    }
}