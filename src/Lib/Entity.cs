using System;
using System.Collections.Generic;
using Game.Components;
using Godot;

namespace Game
{
    public class Entity(int id)
    {
        public int Id { get; } = id;
        private readonly Dictionary<Type, object> _components = [];

        // Cache for hot components (frequently accessed)
        private Vector3I? _cachedCoordinate;
        private int? _cachedHealth;
        private bool _coordinateCached;
        private bool _healthCached;

        public bool Has<T>() => _components.ContainsKey(typeof(T));

        public void Add<T>(T component)
        {
            _components[typeof(T)] = component;
            InvalidateCache<T>(component);
            Events.Instance.OnComponentChanged(Id, typeof(T), component);
        }

        /// <summary>
        /// Adds a component using runtime type information (for deserialization)
        /// </summary>
        public void AddComponent(Type type, object component)
        {
            _components[type] = component;
            Events.Instance.OnComponentChanged(Id, type, component);
        }

        public T Update<T>(T newComponent)
        {
            var result = (T)(_components[typeof(T)] = newComponent);
            InvalidateCache<T>(newComponent);
            Events.Instance.OnComponentChanged(Id, typeof(T), newComponent);
            return result;
        }

        public void Remove<T>()
        {
            var type = typeof(T);
            if (_components.ContainsKey(type))
            {
                _components.Remove(type);
                InvalidateCacheOnRemove<T>();
                Events.Instance.OnComponentChanged(Id, typeof(T), null);
            }
        }

        public T Get<T>()
        {
            // Fast path for cached hot components
            if (typeof(T) == typeof(Coordinate))
            {
                if (!_coordinateCached && _components.TryGetValue(typeof(Coordinate), out var coordComp))
                {
                    _cachedCoordinate = ((Coordinate)coordComp).Value;
                    _coordinateCached = true;
                }
                return _coordinateCached ? (T)(object)new Coordinate(_cachedCoordinate.Value) : default;
            }

            if (typeof(T) == typeof(Health))
            {
                if (!_healthCached && _components.TryGetValue(typeof(Health), out var healthComp))
                {
                    _cachedHealth = ((Health)healthComp).Value;
                    _healthCached = true;
                }
                return _healthCached ? (T)(object)new Health(_cachedHealth.Value) : default;
            }

            // Standard path for other components
            var type = typeof(T);
            return _components.TryGetValue(type, out var component)
                ? (T)component
                : default;
        }

        private void InvalidateCache<T>(T component)
        {
            if (typeof(T) == typeof(Coordinate))
            {
                _cachedCoordinate = ((Coordinate)(object)component).Value;
                _coordinateCached = true;
            }
            else if (typeof(T) == typeof(Health))
            {
                _cachedHealth = ((Health)(object)component).Value;
                _healthCached = true;
            }
        }

        private void InvalidateCacheOnRemove<T>()
        {
            if (typeof(T) == typeof(Coordinate))
                _coordinateCached = false;
            else if (typeof(T) == typeof(Health))
                _healthCached = false;
        }

        public Dictionary<Type, object> GetComponents()
        {
            return _components;
        }
    }
}
