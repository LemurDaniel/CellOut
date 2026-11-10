// NOTE:
//   Loader for the generated tree models (tools/blender/models/trees.py, assets/models/tree/):
//   merges every mesh of a .glb into one ArrayMesh (one surface per Blender material) for
//   multimeshes and gives every surface the wind shader (shaders/tree.gdshader) with the
//   color of its material name. Used by Creature/Coast.cs (TREES).
//
//   ArrayMesh fern = TreeModels.MeshOf(TreeSpecies.Fern);
//   float trunk = TreeModels.TRUNK_SHARE[(int)TreeSpecies.Fern] * height;   // trunk radius

//
//   Chapters:
//     SPECIES            The five trees, their files and trunk sizes.
//     LOAD AND MERGE     Cached meshes, merged from the model, one material per name.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public enum TreeSpecies { Fern, Cycad, Horsetail, Broadleaf, Giant }

public static class TreeModels
{
    // ===========================================================================
    // ===============================   SPECIES   ===============================
    // ===========================================================================
    //
    //   COUNT         Number of species.
    //   PATHS         Model per species.
    //   TRUNK_SHARE   Trunk radius per unit of tree height, the collision of Creature/Coast.cs.
    //

    public const int COUNT = 5;

    private static readonly string[] PATHS =
    {
        "res://assets/models/tree/tree.fern.glb",
        "res://assets/models/tree/tree.cycad.glb",
        "res://assets/models/tree/tree.horsetail.glb",
        "res://assets/models/tree/tree.broadleaf.glb",
        "res://assets/models/tree/tree.giant.glb",
    };

    public static readonly float[] TRUNK_SHARE = { 0.07f, 0.08f, 0.06f, 0.1f, 0.08f };

    // ===========================================================================
    // ============================   LOAD AND MERGE   ===========================
    // ===========================================================================
    //
    //   MeshOf        The merged mesh of a species, loaded once.
    //   Merge         Every mesh of a model in one ArrayMesh, transformed into the model root.
    //   MaterialFor   Wind shader material of a Blender material name, cached.
    //

    private static readonly Shader TREE_SHADER = GD.Load<Shader>("res://shaders/tree.gdshader");
    private static readonly Dictionary<TreeSpecies, ArrayMesh> _meshes = new();
    private static readonly Dictionary<string, ShaderMaterial> _materials = new();

    /// <summary>The merged mesh of a species, about 1 unit high, standing on the origin.</summary>
    /// <example><code>multimesh.Mesh = TreeModels.MeshOf(TreeSpecies.Giant);</code></example>
    public static ArrayMesh MeshOf(TreeSpecies species)
    {
        if (_meshes.TryGetValue(species, out ArrayMesh cached))
        {
            return cached;
        }
        string path  = PATHS[(int)species];
        var    scene = GD.Load<PackedScene>(path) ?? throw new System.ArgumentException($"species must have an imported model at '{path}'.", nameof(species));
        ArrayMesh merged = Merge(scene.Instantiate<Node3D>());
        _meshes[species] = merged;
        Log.Verbose("TreeModels", $"{species}: {merged.GetSurfaceCount()} surfaces from '{path}'.");
        return merged;
    }

    /// <summary>Every surface of every mesh below root, vertices and normals moved into root space.</summary>
    private static ArrayMesh Merge(Node3D root)
    {
        var merged = new ArrayMesh();
        foreach (MeshInstance3D instance in root.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            // Transform of the mesh relative to the model root
            Transform3D toRoot = instance.Transform;
            for (Node parent = instance.GetParent(); null != parent && parent != root; parent = parent.GetParent())
            {
                toRoot = ((Node3D)parent).Transform * toRoot;
            }

            for (int i = 0; i < instance.Mesh.GetSurfaceCount(); i++)
            {
                Godot.Collections.Array arrays = instance.Mesh.SurfaceGetArrays(i);
                if (!toRoot.IsEqualApprox(Transform3D.Identity))
                {
                    Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                    Vector3[] normals  = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        vertices[v] = toRoot * vertices[v];
                    }
                    for (int v = 0; v < normals.Length; v++)
                    {
                        normals[v] = (toRoot.Basis * normals[v]).Normalized();
                    }
                    arrays[(int)Mesh.ArrayType.Vertex]  = vertices;
                    arrays[(int)Mesh.ArrayType.Normal]  = normals;
                    arrays[(int)Mesh.ArrayType.Tangent] = default;    // stale after the transform, the shader needs none
                }
                string name = (instance.Mesh.SurfaceGetMaterial(i)?.ResourceName ?? "").Split('.')[0];
                merged.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                merged.SurfaceSetMaterial(merged.GetSurfaceCount() - 1, MaterialFor(name));
            }
        }
        root.Free();
        return merged;
    }

    /// <summary>Material names of the generator to the wind shader: color, roughness, sway and shiver.</summary>
    private static ShaderMaterial MaterialFor(string name)
    {
        if (_materials.TryGetValue(name, out ShaderMaterial cached))
        {
            return cached;
        }
        (Color color, float roughness, float flutter) = name switch
        {
            "Leaf"      => (new Color(0.3f, 0.55f, 0.2f), 0.8f, 1f),
            "LeafDark"  => (new Color(0.2f, 0.4f, 0.16f), 0.85f, 1f),
            "Frond"     => (new Color(0.36f, 0.6f, 0.22f), 0.7f, 1.4f),
            "Horsetail" => (new Color(0.42f, 0.52f, 0.26f), 0.6f, 0f),
            "Needle"    => (new Color(0.3f, 0.52f, 0.2f), 0.75f, 1.2f),
            "Cone"      => (new Color(0.85f, 0.48f, 0.18f), 0.5f, 0f),
            _           => (new Color(0.36f, 0.26f, 0.19f), 0.95f, 0f),   // Bark
        };
        var material = new ShaderMaterial { Shader = TREE_SHADER };
        material.SetShaderParameter("albedo", color);
        material.SetShaderParameter("roughness", roughness);
        material.SetShaderParameter("flutter", flutter);
        _materials[name] = material;
        return material;
    }
}
