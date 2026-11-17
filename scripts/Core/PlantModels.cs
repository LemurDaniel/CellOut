// NOTE:
//   Loader for the generated undergrowth (tools/blender/models/plants.py, assets/models/plant/):
//   shrubs, ferns, flowers and boulders of the meadows, merged like the trees into one
//   ArrayMesh per model with the wind shader (Core/TreeModels.cs, shaders/tree.gdshader).
//   Petals are white in the model: a multimesh with custom data gives every instance its
//   bloom color (INSTANCE_CUSTOM). Used by Creature/World/CoastProps.cs.
//
//   ArrayMesh shrub = PlantModels.MeshOf(PlantKind.Shrub);
//   multimesh.UseCustomData = PlantModels.HAS_BLOOM[(int)PlantKind.Daisy];

//
//   Chapters:
//     KINDS   The plants, their files and which of them blossom.
//     LOAD    Cached meshes, merged from the model.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public enum PlantKind { Shrub, BerryBush, FernBush, Daisy, Bell, Lupine, Boulder }

public static class PlantModels
{
    // ===========================================================================
    // ================================   KINDS   ================================
    // ===========================================================================
    //
    //   COUNT       Number of plants.
    //   PATHS       Model per plant.
    //   HAS_BLOOM   Whether the plant has petals colored per instance.
    //

    public const int COUNT = 7;

    private static readonly string[] PATHS =
    {
        "res://assets/models/plant/plant.shrub.glb",
        "res://assets/models/plant/plant.berrybush.glb",
        "res://assets/models/plant/plant.fernbush.glb",
        "res://assets/models/plant/plant.daisy.glb",
        "res://assets/models/plant/plant.bell.glb",
        "res://assets/models/plant/plant.lupine.glb",
        "res://assets/models/plant/plant.boulder.glb",
    };

    public static readonly bool[] HAS_BLOOM = { false, false, false, true, true, true, false };

    // ===========================================================================
    // ================================   LOAD   =================================
    // ===========================================================================
    //
    //   MeshOf   The merged mesh of a plant, loaded once.
    //

    private static readonly Dictionary<PlantKind, ArrayMesh> _meshes = new();

    /// <summary>The merged mesh of a plant, about 1 unit high, standing on the origin.</summary>
    /// <example><code>multimesh.Mesh = PlantModels.MeshOf(PlantKind.Daisy);</code></example>
    public static ArrayMesh MeshOf(PlantKind kind)
    {
        if (_meshes.TryGetValue(kind, out ArrayMesh cached))
        {
            return cached;
        }
        string path  = PATHS[(int)kind];
        var    scene = GD.Load<PackedScene>(path) ?? throw new System.ArgumentException($"kind must have an imported model at '{path}'.", nameof(kind));
        ArrayMesh merged = TreeModels.Merge(scene.Instantiate<Node3D>());
        _meshes[kind] = merged;
        Log.Verbose("PlantModels", $"{kind}: {merged.GetSurfaceCount()} surfaces from '{path}'.");
        return merged;
    }
}
