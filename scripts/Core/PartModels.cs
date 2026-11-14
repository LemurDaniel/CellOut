// NOTE:
//   Loader for the generated part models (tools/blender/build.models.py, assets/models/):
//   instances a .glb once loaded, recolors every surface by its Blender material name
//   (Skin follows the body color, Claw, Brain, Chloro, ... have fixed colors) and starts
//   its looping animation at a phase. Shared by CellVisual3D and CreatureVisual3D.
//
//   Node3D legs = PartModels.New("res://assets/models/creature/creature.legs.glb", color);
//   PartModels.PlayLoop(legs, "Walk", phase: 0.25f, rate: 1f);

//
//   Chapters:
//     LOAD AND RECOLOR   Cached scenes, instancing and the material per name.
//     ANIMATE            The looping animation of a model.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public static class PartModels
{
    // ===========================================================================
    // ==========================   LOAD AND RECOLOR   ===========================
    // ===========================================================================
    //
    //   New            A model instance, every surface recolored.
    //   MaterialFor    Game material of a Blender material name.
    //   TwoSided       Cached two-sided material.
    //   Membrane       Cached fin and wing membrane (shaders/fin.gdshader): root color to light edge, rays.
    //

    public static readonly Color CLAW_COLOR = new(0.95f, 0.92f, 0.82f);
    public static readonly Color BRAIN_COLOR = new(0.95f, 0.62f, 0.7f);
    private static readonly Color PAD_COLOR = new(0.95f, 0.62f, 0.62f);

    private static readonly Dictionary<string, PackedScene> _scenes = new();
    private static readonly Dictionary<Color, StandardMaterial3D> _twoSided = new();

    /// <summary>A new instance of a model, every surface recolored by its material name.</summary>
    /// <param name="path">(Required) res:// path of the .glb.</param>
    /// <param name="bodyColor">(Required) Color of skin, pads, feathers and membrane.</param>
    /// <param name="overrides">(Optional) Materials by Blender name that replace the recolor (the fin edge in the paint color). Defaults to none.</param>
    /// <returns>The model root, its AnimationPlayer (if any) not playing yet.</returns>
    /// <example><code>Node3D spike = PartModels.New("res://assets/models/cell/cell.spike.glb", color);</code></example>
    public static Node3D New(string path, Color bodyColor, IReadOnlyDictionary<string, Material> overrides = null)
    {
        if (!_scenes.TryGetValue(path, out PackedScene scene))
        {
            scene = GD.Load<PackedScene>(path);
            _scenes[path] = scene ?? throw new System.ArgumentException($"path must point to an imported .glb, got '{path}'.", nameof(path));
        }
        var root = scene.Instantiate<Node3D>();
        foreach (MeshInstance3D mesh in root.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;    // blocky shadow texels at this size
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                string name = (mesh.Mesh.SurfaceGetMaterial(i)?.ResourceName ?? "").Split('.')[0];
                Material custom = null;
                bool isOverridden = null != overrides && overrides.TryGetValue(name, out custom);
                mesh.SetSurfaceOverrideMaterial(i, isOverridden ? custom : MaterialFor(name, bodyColor));
            }
        }
        return root;
    }

    /// <summary>Material names of the Blender generator to game materials, unknown names are skin.</summary>
    private static Material MaterialFor(string name, Color bodyColor)
    {
        return name switch
        {
            // Creature parts
            "Claw"        => CellVisual3D.Material(CLAW_COLOR, 0f, 0.35f),
            "Pad"         => CellVisual3D.Material(bodyColor.Lerp(PAD_COLOR, 0.55f), 0f, 0.8f),
            "Feather"     => Membrane(bodyColor.Lightened(0.35f), bodyColor.Lightened(0.75f), MembranePlane.Flat, reach: 1f, rays: 0f),
            "Membrane"    => Membrane(bodyColor.Darkened(0.1f), bodyColor.Lightened(0.45f), MembranePlane.Flat, reach: 1f, rays: 9f),
            "Fin"         => Membrane(bodyColor, bodyColor.Lightened(0.55f), MembranePlane.Free, reach: 0.45f, rays: 0f),
            "FinRay"      => CellVisual3D.Material(bodyColor.Lightened(0.35f), 0f, 0.45f),
            "Brain"       => CellVisual3D.Material(BRAIN_COLOR, 0.1f, 0.6f),
            "EyeWhite"    => CellVisual3D.Material(new Color(0.97f, 0.97f, 0.97f), 0.25f, 0.2f),
            "Pupil"       => CellVisual3D.Material(new Color(0.06f, 0.06f, 0.1f), 0f, 0.1f),
            "Glint"       => CellVisual3D.Material(Colors.White, 1.5f),
            // Cell parts
            "Chitin"      => CellVisual3D.Material(new Color(0.35f, 0.12f, 0.1f), 0f, 0.3f),
            "Armor"       => CellVisual3D.Material(new Color(0.5f, 0.55f, 0.62f), 0f, 0.25f),
            "Chloro"      => CellVisual3D.Material(new Color(0.25f, 0.7f, 0.3f), 0.35f),
            "Grana"       => CellVisual3D.Material(new Color(0.12f, 0.45f, 0.18f), 0.2f),
            "Vacuole"     => CellVisual3D.Material(new Color(0.55f, 0.8f, 1f, 0.55f), 0.3f, 0.1f),
            "Droplet"     => CellVisual3D.Material(new Color(0.85f, 0.95f, 1f), 0.5f, 0.1f),
            "Toxin"       => CellVisual3D.Material(new Color(0.6f, 0.25f, 0.75f, 0.7f), 0.6f, 0.1f),
            "Venom"       => CellVisual3D.Material(new Color(0.75f, 1f, 0.35f), 1.2f),
            "Iris"        => CellVisual3D.Material(new Color(0.25f, 0.55f, 0.85f), 0.3f, 0.2f),
            "Dark"        => CellVisual3D.Material(new Color(0.12f, 0.04f, 0.06f)),
            "Mito"        => CellVisual3D.Material(new Color(0.95f, 0.45f, 0.3f, 0.6f), 0.25f, 0.2f),
            "Cristae"     => CellVisual3D.Material(new Color(0.7f, 0.2f, 0.15f), 0.15f),
            "Starch"      => CellVisual3D.Material(new Color(0.96f, 0.92f, 0.78f), 0.1f, 0.8f),
            "StarchCore"  => CellVisual3D.Material(new Color(0.85f, 0.75f, 0.55f), 0f, 0.7f),
            "Slime"       => CellVisual3D.Material(new Color(0.7f, 0.95f, 0.6f, 0.45f), 0.3f, 0.05f),
            "SlimeBubble" => CellVisual3D.Material(new Color(0.9f, 1f, 0.85f), 0.6f, 0.1f),
            "Pigment"     => CellVisual3D.Material(new Color(0.22f, 0.14f, 0.28f), 0f, 0.6f),
            "PigmentLight"=> CellVisual3D.Material(new Color(0.65f, 0.55f, 0.4f), 0f, 0.6f),
            "Nettle"      => CellVisual3D.Material(new Color(0.95f, 0.85f, 0.55f, 0.75f), 0.3f, 0.15f),
            _             => CellVisual3D.Material(bodyColor.Darkened(0.1f), 0f, 0.7f),
        };
    }

    private static readonly Shader MEMBRANE_SHADER = GD.Load<Shader>("res://shaders/fin.gdshader");
    private static readonly Dictionary<(Color, Color, MembranePlane, float, float), ShaderMaterial> _membranes = new();

    /// <summary>Where the membrane lies in model space: XY (upright), XZ (flat, wings) or anywhere (fin models).</summary>
    public enum MembranePlane { Upright, Flat, Free }

    /// <summary>A see-through, backlit membrane for fins and wings, cached per look.</summary>
    /// <param name="root">(Required) Color at the root, usually the body color.</param>
    /// <param name="edge">(Required) Color at the free edge.</param>
    /// <param name="plane">(Required) Where the membrane lies, Free measures the edge by the 3D distance from the root.</param>
    /// <param name="reach">(Required) Distance from the root where the edge color is full, model units.</param>
    /// <param name="rays">(Required) Rays per full turn around the root, 0 for none.</param>
    /// <example><code>Material fin = PartModels.Membrane(color, color.Lightened(0.5f), PartModels.MembranePlane.Free, reach: 0.45f, rays: 0f);</code></example>
    public static ShaderMaterial Membrane(Color root, Color edge, MembranePlane plane, float reach, float rays)
    {
        var key = (root, edge, plane, reach, rays);
        if (_membranes.TryGetValue(key, out ShaderMaterial cached))
        {
            return cached;
        }
        var material = new ShaderMaterial { Shader = MEMBRANE_SHADER };
        material.SetShaderParameter("base_color", root);
        material.SetShaderParameter("edge_color", edge);
        material.SetShaderParameter("plane", (int)plane);
        material.SetShaderParameter("reach", reach);
        material.SetShaderParameter("rays", rays);
        _membranes[key] = material;
        return material;
    }

    /// <summary>Two sided, slightly see-through at the edges, translucent with a translucent body color.</summary>
    private static StandardMaterial3D TwoSided(Color color)
    {
        if (_twoSided.TryGetValue(color, out StandardMaterial3D cached))
        {
            return cached;
        }
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness   = 0.6f,
            CullMode    = BaseMaterial3D.CullModeEnum.Disabled,
            RimEnabled  = true,
            Rim         = 0.4f,
        };
        if (color.A < 0.99f)
        {
            material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        }
        _twoSided[color] = material;
        return material;
    }

    // ===========================================================================
    // ===============================   ANIMATE   ===============================
    // ===========================================================================
    //
    //   PlayLoop   Starts the looping animation of a model at a share of its cycle.
    //

    /// <summary>Starts the looping animation of a model at a share of its cycle.</summary>
    /// <param name="model">(Required) Root from New.</param>
    /// <param name="animation">(Required) Animation name, e.g. "Walk".</param>
    /// <param name="phase">(Required) Start share of the cycle, 0..1, so pairs and rows do not move in lockstep.</param>
    /// <param name="rate">(Required) Playback speed.</param>
    /// <returns>The playing AnimationPlayer, null when the model has none.</returns>
    public static AnimationPlayer PlayLoop(Node3D model, string animation, float phase, float rate)
    {
        AnimationPlayer player = model.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().FirstOrDefault();
        if (null == player)
        {
            Log.Verbose("PartModels", $"Model {model.Name} has no AnimationPlayer -> stays still.");
            return null;
        }
        // A model built by an older exporter names its only animation "Animation": take that one
        if (!player.HasAnimation(animation) && 1 == player.GetAnimationList().Length)
        {
            Log.Verbose("PartModels", $"Model {model.Name} has no '{animation}', playing its only animation '{player.GetAnimationList()[0]}'.");
            animation = player.GetAnimationList()[0];
        }
        if (!player.HasAnimation(animation))
        {
            Log.Verbose("PartModels", $"Model {model.Name} has no animation '{animation}' -> stays still.");
            return null;
        }
        player.GetAnimation(animation).LoopMode = Animation.LoopModeEnum.Linear;
        player.Play(animation);
        player.Seek(Mathf.PosMod(phase, 1f) * (float)player.CurrentAnimationLength, true);
        player.SpeedScale = rate;
        return player;
    }
}
