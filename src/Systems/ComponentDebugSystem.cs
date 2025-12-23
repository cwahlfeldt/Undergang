using Game.Components;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Game
{
    public partial class ComponentDebugSystem : System
    {
        private RichTextLabel _debugText;
        private HashSet<int> _selected = [];

        public override void Initialize()
        {
            var panel = new PanelContainer
            {
                Position = new Vector2(10, 10),
                Size = new Vector2(400, 630)
            };

            _debugText = new RichTextLabel
            {
                CustomMinimumSize = new Vector2(400, 630),
                SizeFlagsHorizontal = Control.SizeFlags.Fill,
                SizeFlagsVertical = Control.SizeFlags.Fill,
                BbcodeEnabled = true,
                ScrollFollowing = false,
                Theme = new Theme()
            };

            // Apply custom styling
            _debugText.AddThemeFontSizeOverride("normal_font_size", 13);
            _debugText.AddThemeFontSizeOverride("bold_font_size", 14);
            _debugText.AddThemeColorOverride("default_color", new Color(0.9f, 0.9f, 0.9f));

            panel.AddChild(_debugText);
            Entities.GetRootNode().AddChild(panel);
            Events.Instance.UnitRightClick += OnUnitRightClick;
            Events.Instance.EntityRightClick += OnUnitRightClick;
            Events.Instance.TurnChanged += OnTurnChanged;
            Events.Instance.TurnRestarted += OnTurnChanged;
            _debugText.BbcodeEnabled = true;
            UpdateDebug();
        }

        private void OnTurnChanged(Entity _)
        {
            UpdateDebug();
        }

        private void OnUnitRightClick(Entity unit)
        {
            if (_selected.Contains(unit.Id))
            {
                _selected.Remove(unit.Id);
            }
            else
            {
                _selected.Add(unit.Id);
            }
            UpdateDebug();
        }

        private void OnUnitUnhover(Entity unit) { }

        private void UpdateDebug()
        {
            if (_selected.Count == 0)
            {
                _debugText.Text = "Right click units to inspect";
                return;
            }

            var table = new string[_selected.Count + 1][];
            var entities = _selected.Select(id => Entities.GetEntity(id)).Where(e => e != null).ToList();
            var allComponentTypes = new HashSet<Type>();

            // First pass: collect all component types
            foreach (var entity in entities)
            {
                var components = entity.GetType()
                    .GetField("_components", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(entity) as Dictionary<Type, object>;

                foreach (var (type, _) in components)
                {
                    if (type != typeof(Instance))
                        allComponentTypes.Add(type);
                }
            }

            int columnCount = entities.Count + 1; // +1 for the component names column
            string text = $"[table={columnCount}]\n";

            // Add the header row spanning all columns
            text += $"[cell][color=yellow][b]Components[/b][/color][/cell]";
            foreach (var entity in entities)
            {
                var name = entity.Get<Name>().Value;
                text += $"[cell ph=20][color=yellow][b]{name}[/b]\nEntity {entity.Id}[/color][/cell]";
            }

            // Component rows
            foreach (var componentType in allComponentTypes.OrderBy(t => t.Name))
            {
                text += $"\n[cell][color=aqua]{componentType.Name}[/color][/cell]";

                foreach (var entity in entities)
                {
                    var components = entity.GetType()
                        .GetField("_components", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.GetValue(entity) as Dictionary<Type, object>;

                    text += "[cell]";
                    if (components.TryGetValue(componentType, out var component))
                    {
                        bool first = true;
                        foreach (var prop in componentType.GetProperties())
                        {
                            var value = prop.GetValue(component);
                            var diff = entities.Count > 1 && entities.Any(e =>
                            {
                                if (e.Id == entity.Id) return false;
                                var method = typeof(Entity).GetMethod("Get").MakeGenericMethod(componentType);
                                var other = method.Invoke(e, null);
                                return other == null || !Equals(prop.GetValue(other), value);
                            });

                            if (!first) text += "\n";
                            text += value.ToString();
                            first = false;
                        }
                    }
                    else
                    {
                        text += "[color=#666666]null[/color]";
                    }
                    text += "[/cell]";
                }
            }

            text += "[/table]";
            _debugText.Text = text;
        }

        public override void Cleanup()
        {
            Events.Instance.UnitRightClick -= OnUnitRightClick;
            Events.Instance.TurnChanged -= OnTurnChanged;
            Events.Instance.TurnRestarted -= OnTurnChanged;
            _debugText?.QueueFree();
        }
    }
}