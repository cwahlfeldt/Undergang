using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Game
{
    public partial class Systems
    {
        private readonly Dictionary<Type, ISystem> _sequential = [];
        private readonly Dictionary<Type, ISystem> _concurrent = [];
        private readonly SystemDependencies _dependencies;
        private bool _initialized;
        public Systems(Node3D rootNode)
        {
            var entities = new Entities(rootNode);
            var pathfinder = new PathFinder(entities);
            var materials = new Materials();

            _dependencies = new SystemDependencies(
                entities,
                pathfinder,
                Events.Instance,
                Tweener.Instance,
                this,
                materials
            );

            _sequential[typeof(Events)] = Events.Instance;
            _sequential[typeof(Entities)] = entities;
            _sequential[typeof(Materials)] = materials;
            _sequential[typeof(PathFinder)] = pathfinder;
            _sequential[typeof(Tweener)] = Tweener.Instance;
        }

        public T Register<T>() where T : System, new()
        {
            var system = new T();
            system.InjectDependencies(_dependencies);
            _sequential[typeof(T)] = system;
            return system;
        }

        public T RegisterConcurrent<T>() where T : System, new()
        {
            var system = new T();
            system.InjectDependencies(_dependencies);
            _concurrent[typeof(T)] = system;
            return system;
        }

        public Entities GetEntityManager() => _dependencies.Entities;

        public T Get<T>() where T : ISystem
        {
            if (_sequential.TryGetValue(typeof(T), out var system) ||
                _concurrent.TryGetValue(typeof(T), out system))
                return (T)system;

            throw new InvalidOperationException($"System of type {typeof(T).Name} not found.");
        }

        public void Initialize()
        {
            if (_initialized) return;

            foreach (var system in _sequential.Values)
                system.Initialize();

            foreach (var system in _concurrent.Values)
                system.Initialize();

            _initialized = true;
        }

        public async Task Update()
        {
            // Run sequential systems
            foreach (var system in _sequential.Values)
            {
                if (system is System baseSystem)
                {
                    await baseSystem.Update();
                }
            }

            // Run concurrent systems
            var concurrentTasks = _concurrent.Values
                .Where(s => s is System)
                .Select(s => ((System)s).Update());

            await Task.WhenAll(concurrentTasks);
        }

        public void Cleanup()
        {
            foreach (var system in _sequential.Values.Concat(_concurrent.Values))
            {
                if (system is System baseSystem)
                {
                    baseSystem.Cleanup();
                }
            }

            _sequential.Clear();
            _concurrent.Clear();
            _initialized = false;
        }

        public IEnumerable<ISystem> GetAllSystems()
        {
            return _sequential.Values.Concat(_concurrent.Values);
        }

        public void RemoveSystem<T>() where T : ISystem
        {
            var type = typeof(T);
            if (_sequential.TryGetValue(type, out var system))
            {
                system.Cleanup();
                _sequential.Remove(type);
            }
            else if (_concurrent.TryGetValue(type, out system))
            {
                system.Cleanup();
                _concurrent.Remove(type);
            }
        }
    }
}




