using Godot;
using System.Collections.Generic;

namespace Game
{
    public partial class Materials : Node3D, ISystem
    {
        private readonly Dictionary<string, Material> _materials = [];

        public Materials()
        {
            Load();
        }

        private void Load()
        {
            var dir = DirAccess.Open("res://assets/materials");
            if (dir == null)
            {
                return;
            }

            dir.ListDirBegin();
            string fileName = dir.GetNext();

            while (!string.IsNullOrEmpty(fileName))
            {
                if (fileName.EndsWith(".material"))
                {
                    var material = ResourceLoader.Load<Material>($"res://assets/materials/{fileName}");
                    if (material != null)
                    {
                        _materials[fileName.Replace(".material", "")] = material;
                    }
                }
                fileName = dir.GetNext();
            }
        }

        public void Swap(Node node, string materialName, float duration = 0.5f)
        {
            if (!_materials.ContainsKey(materialName))
            {
                return;
            }

            if (node is not MeshInstance3D meshInstance)
            {
                meshInstance = GetNodeInChildren<MeshInstance3D>(node);
            }

            if (meshInstance == null)
            {
                return;
            }

            var tween = CreateTween();
            var newMaterial = _materials[materialName];

            meshInstance.MaterialOverride = newMaterial;

            // Tween its opacity
            newMaterial.Set("shader_parameter/alpha", 0.0f);
            tween.TweenProperty(newMaterial, "shader_parameter/alpha", 1.0f, duration);
        }

        private MeshInstance3D GetNodeInChildren<T>(Node node) where T : Node
        {
            foreach (var child in node.GetChildren())
            {
                if (child is MeshInstance3D mesh)
                    return mesh;

                var result = GetNodeInChildren<T>(child);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}

// Example usage:
// public class ExampleUsage : Node3D
// {
//     private MaterialManager _materialManager;

//     public override void _Ready()
//     {
//         _materialManager = new MaterialManager();
//         AddChild(_materialManager);

//         // Change material with fade - works with any node that has a MeshInstance3D child
//         _materialManager.SwapMaterial(this, "metal");
//     }
// }