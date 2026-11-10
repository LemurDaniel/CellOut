// NOTE:
//   3D rendering of an organism (stage 4): a clay body lofted along the spine of its
//   CreatureDesign (shaders/clay.gdshader) plus fins, eyes, jaw, spines, blowhole,
//   legs, hands, a brain, the nostrils of a lung and wings. Legs, hands, wings, brain and
//   stalk eyes are rigged models from tools/blender (assets/models/creature, Models),
//   recolored with the body color and looping their own animation.
//   Built in unit space (+X forward, contact radius 1) and scaled to Body.Radius. The
//   body swims with a wave that grows towards the tail, side to side for fish, up and
//   down for marine mammals; the parts ride the same wave (Bend). On land (Body.IsOnLand)
//   the wave calms down, legs lift the body and walk, without legs it crawls on its belly.
//   Wings flap slowly, hands grab, stalk eyes sway all the time.
//   Two modes:
//     - In the world: created by CellBody.ShowCreature, follows Body like CellVisual3D.
//     - Standalone (editor preview, icons): Body stays null, Design and BodyColor are set.
//
//   var preview = new CreatureVisual3D { Design = draft, BodyColor = color, Knead = 0.8f };
//   viewport.AddChild(preview);

//
//   Chapters:
//     SETTINGS          Inputs, shared meshes, materials and the part models.
//     FOLLOW AND SWIM   Rebuild on change, follow the body, swim wave, blink and spout.
//     BUILD CREATURE    Lofted body mesh and every part along the spine.
//     POSE AND PICK     Side, size, shift and turn of the parts, picking them by ray.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class CreatureVisual3D : Node3D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Inputs      Body or Design and BodyColor, clay look and swimming.
    //   Meshes      Fin outlines as flat meshes, cached once.
    //   Materials   Fin membrane with the edge in the paint color.
    //   Models      Creature part models from tools/blender, loaded and recolored by Core/PartModels.cs.
    //

    // ---------------------------------------------------------------------------
    // Inputs
    //   Body or Design and BodyColor, clay look and swimming.
    // ---------------------------------------------------------------------------

    public const float LIVING_KNEAD = 0.2f;          // the clay look left on a living creature
    public const float EDITOR_KNEAD = 0.75f;         // fresh clay in the creature editor
    private const int RINGS = 96;                   // rings along the body, fine enough for a smooth silhouette
    private const int SEGMENTS = 40;
    private const float RING_TILT = 0.6f;           // share of the spine slope the rings tilt by: full tilt folds the inside of a bend
    private const float IDLE_AMP = 0.05f;            // swim wave at rest, in unit space
    private const float SWIM_AMP = 0.13f;            // added at full speed
    private const float IDLE_FREQUENCY = 2.5f;       // wave phase per second at rest
    private const float SWIM_FREQUENCY = 9f;         // added at full speed
    private const float SPOUT_PERIOD = 5f;           // a marine mammal breathes every few seconds
    private const float SPOUT_SECONDS = 0.9f;
    private const float LAND_LIFT = 0.32f;           // legs lift the body this far above its swim height on land: stilts, the legs grow with it
    private const float LAND_WAVE = 0.25f;           // share of the swim wave left on land
    private const float FLAP_RATE = 0.5f;            // wing beats per second
    private const float GRAB_RATE = 0.6f;            // hands grab and let go
    private const float SWAY_RATE = 0.8f;            // stalk eyes look around
    private const float END_SIZE = 1.5f;             // feet and hands on the limb tip, in limb space

    /*
      Base size of every part before the size slider of the editor (CreaturePart.Size):
      bold and monstrous by design, the slider then goes 60% .. 300% of it. Legs stay at
      1, they get their bulk from LEG_WIDTH so the soles keep touching the ground.
    */
    private static readonly IReadOnlyDictionary<CreatureCategory, float> BASE_PART_SIZES = new Dictionary<CreatureCategory, float>
    {
        [CreatureCategory.Fins]    = 1.6f,
        [CreatureCategory.Weapons] = 1.9f,
        [CreatureCategory.Eyes]    = 2f,
        [CreatureCategory.Limbs]   = 1f,             // sculpted, the joints already set the length
        [CreatureCategory.Feet]    = 1f,
        [CreatureCategory.Hands]   = 1f,             // on a sculpted limb: the joints set the length, END_SIZE the hand
        [CreatureCategory.Brain]   = 1.4f,
        [CreatureCategory.Lungs]   = 1.3f,
        [CreatureCategory.Wings]   = 1.8f,
    };
    private static readonly Shader CLAY_SHADER = GD.Load<Shader>("res://shaders/clay.gdshader");
    private static readonly Shader TRANSLUCENT_SHADER = GD.Load<Shader>("res://shaders/clay_translucent.gdshader");

    /// <summary>(Optional) The body this visual renders in the world, its Creature is the design.</summary>
    public CellBody Body { get; set; }

    /// <summary>(Optional) Design of a standalone visual (editor, icons), ignored when Body is set.</summary>
    public CreatureDesign Design { get; set; }

    /// <summary>(Optional) Color of a standalone visual, ignored when Body is set.</summary>
    public Color BodyColor { get; set; } = new(0.8f, 0.55f, 0.45f);

    /// <summary>(Optional) 1 fresh clay, 0 smooth skin. Defaults to LIVING_KNEAD.</summary>
    public float Knead { get; set; } = LIVING_KNEAD;

    /// <summary>(Optional) Seed of lumps and grooves of a standalone visual.</summary>
    public float Seed { get; set; }

    /// <summary>(Optional) Scales the swim wave, low in the editor so the handles stay on the body. Defaults to 1.</summary>
    public float SwimScale { get; set; } = 1f;

    /// <summary>(Optional) Draws only the parts, no body (palette tiles of the creature editor). Defaults to false.</summary>
    public bool IsPartsOnly { get; set; }

    /// <summary>(Optional) A camera that frames the parts after every rebuild (palette tiles). Defaults to none.</summary>
    public Camera3D FitCamera { get; set; }

    private CreatureDesign Current => null != Body ? Body.Creature : Design;
    private Color CurrentColor => null != Body ? Body.BodyColor : BodyColor;

    // ---------------------------------------------------------------------------
    // Materials
    //   Fin membranes: body color at the root, the paint color (or a light shade) at the edge.
    // ---------------------------------------------------------------------------

    private const float FIN_REACH = 0.45f;          // the fin models reach about this far from their root
    private const float FLUTTER_RATE = 1f;           // fin flutter cycles per second

    /// <summary>The webbing of a fin model (Core/PartModels.cs), the edge takes the paint color of the design.</summary>
    private static ShaderMaterial FinMaterial(CreatureDesign design, Color bodyColor)
    {
        Color edge = CreaturePattern.None != design.Pattern && null != design.PatternColor
            ? design.PatternColor.Value.Lightened(0.25f)
            : bodyColor.Lightened(0.55f);
        return PartModels.Membrane(bodyColor, new Color(edge, bodyColor.A), PartModels.MembranePlane.Free, FIN_REACH, 0f);
    }

    // ---------------------------------------------------------------------------
    // Models
    //   Creature part models from tools/blender, loaded and recolored by Core/PartModels.cs.
    // ---------------------------------------------------------------------------

    /*
      Model space of every part (tools/blender/build.models.py): Godot axes,
      the joint to the body at the origin, unit size.
      - Feet:       ankle at the origin, sole below, toes along +X. No animation, they stand flat.
      - Hands:      wrist at the origin, reaching forward (+X), thumb towards -Z. "Grab", 2 s.
                    Feet and hands sit on the tip of a sculpted limb (BuildLimb).
      - Wings:      root at the origin, spreading along +Z, swept back. "Flap", 1 s.
      - Stalk eye:  base at the origin, eye at y = 1.2, pupil towards +X and +Z. "Sway", 3 s.
      - Brain:      two lobes around the origin, no animation.
      - Fins:       root at the origin. Tail fin upright and flukes flat, both reaching back (-X);
                    back fin upright, rising (+Y) and swept back; paddle flat, reaching out (+Z).
                    Real rays (FinRay) on a thick webbing (Fin). "Flutter", 1 s.
      Parts on the right flank (+Z) are used as they are, the left ones mirrored on Z.
    */
    public const string MODEL_DIR = "res://assets/models/creature/";
    public static readonly IReadOnlyDictionary<CreaturePartKind, (string File, string Animation)> MODELS =
        new Dictionary<CreaturePartKind, (string File, string Animation)>
        {
            [CreaturePartKind.Legs]          = ("creature.paw.glb", null),
            [CreaturePartKind.StubbyLegs]    = ("creature.columnfoot.glb", null),
            [CreaturePartKind.ClawLegs]      = ("creature.talonfoot.glb", null),
            [CreaturePartKind.CrabLegs]      = ("creature.crabtip.glb", null),
            [CreaturePartKind.Hands]         = ("creature.hand.glb", "Grab"),
            [CreaturePartKind.Claws]         = ("creature.clawpaw.glb", "Grab"),
            [CreaturePartKind.MembraneWings] = ("creature.membranewings.glb", "Flap"),
            [CreaturePartKind.FeatherWings]  = ("creature.featherwings.glb", "Flap"),
            [CreaturePartKind.SmallBrain]    = ("creature.brain.glb", null),
            [CreaturePartKind.BigBrain]      = ("creature.brain.glb", null),
            [CreaturePartKind.StalkEye]      = ("creature.stalkeye.glb", "Sway"),
            [CreaturePartKind.CaudalFin]     = ("creature.caudalfin.glb", "Flutter"),
            [CreaturePartKind.Fluke]         = ("creature.fluke.glb", "Flutter"),
            [CreaturePartKind.DorsalFin]     = ("creature.dorsalfin.glb", "Flutter"),
            [CreaturePartKind.PectoralFin]   = ("creature.pectoralfin.glb", "Flutter"),
        };

    /// <summary>A new instance of a creature part model, recolored (Core/PartModels.cs).</summary>
    /// <param name="kind">(Required) A part listed in MODELS.</param>
    /// <param name="bodyColor">(Required) Color of skin, pads, feathers and membrane.</param>
    /// <param name="overrides">(Optional) Materials by Blender name replacing the recolor. Defaults to none.</param>
    /// <example><code>Node3D legs = CreatureVisual3D.NewModel(CreaturePartKind.Legs, color);</code></example>
    public static Node3D NewModel(CreaturePartKind kind, Color bodyColor, IReadOnlyDictionary<string, Material> overrides = null)
    {
        if (!MODELS.TryGetValue(kind, out (string File, string Animation) model))
        {
            throw new System.ArgumentOutOfRangeException(nameof(kind), $"kind must have a model in MODELS, got '{kind}'.");
        }
        return PartModels.New(MODEL_DIR + model.File, bodyColor, overrides);
    }

    private static AnimationPlayer PlayLoop(Node3D model, string animation, float phase, float rate)
    {
        return PartModels.PlayLoop(model, animation, phase, rate);
    }

    /// <summary>Adds a model to a parent, mirrored on Z for the left side.</summary>
    /// <param name="width">(Optional) Thickens limbs across their length (X and Z). Defaults to 1.</param>
    /// <param name="overrides">(Optional) Materials by Blender name replacing the recolor. Defaults to none.</param>
    private static Node3D AttachModel(Node3D parent, CreaturePartKind kind, Color color, Vector3 position, float size, float side,
        float width = 1f, IReadOnlyDictionary<string, Material> overrides = null)
    {
        Node3D model = NewModel(kind, color, overrides);
        model.Position = position;
        model.Scale    = new Vector3(size * width, size, size * width * side);
        parent.AddChild(model);
        return model;
    }

    // ===========================================================================
    // ===========================   FOLLOW AND SWIM   ===========================
    // ===========================================================================
    //
    //   1. Rebuild     Rebuilds when design or color changed.
    //   2. Transform   Paints the spray dots, follows position, facing and size of the body.
    //   3. Swim        Wave phase and amplitude from the speed, body shader and parts follow.
    //   4. Details     Leg animation speed, threat ring, eye blink and the blowhole spout.
    //

    private sealed record Attached(Node3D Pivot, float X, float Y);   // Y: height of the bent spine there

    private readonly List<Attached> _attached = new();
    private readonly List<Node3D> _eyes = new();
    private readonly List<MeshInstance3D> _spouts = new();
    private float _lift;
    private float _sink;                             // eases to Plane3D.SeaDrop in the water, 0 on land
    private ShaderMaterial _clay;
    private int _signature;
    private float _time;
    private float _phase;

    public override void _Process(double delta)
    {
        float           dt     = (float)delta;
        CreatureDesign  design = Current;
        _time += dt;
        if (null == design || 0 == design.Girths.Count)
        {
            Visible = false;
            return;
        }

        // ---------------------------------------------------------------------------
        // Rebuild
        //   Rebuilds when design or color changed.
        // ---------------------------------------------------------------------------

        int signature = System.HashCode.Combine(design, design.Version, design.Type, CurrentColor, Knead);   // a new design instance (drag ghost) rebuilds too
        if (signature != _signature || null == _clay)
        {
            _signature = signature;
            Rebuild(design);
            _paintVersion = -1;                      // the new body needs its paint
            if (null != FitCamera)
            {
                FitCameraToParts();
            }
        }

        // Spray paint: only the shader dots change, the meshes stay
        if (design.PaintVersion != _paintVersion)
        {
            _paintVersion = design.PaintVersion;
            UpdatePaint(design);
        }

        // ---------------------------------------------------------------------------
        // Transform
        //   Follows position, facing and size of the body.
        // ---------------------------------------------------------------------------

        float speedShare = 0f;
        bool  isOnLand   = null != Body && Body.IsOnLand;
        _lift = Mathf.Lerp(_lift, isOnLand && design.LegPairs > 0 ? LAND_LIFT : 0f, Mathf.Clamp(dt * 4f, 0f, 1f));
        _sink = Mathf.Lerp(_sink, isOnLand ? 0f : Plane3D.SeaDrop, Mathf.Clamp(dt * 4f, 0f, 1f));
        if (null != Body)
        {
            GlobalPosition = Plane3D.To3D(Body.GlobalPosition, _lift * Body.Radius + Body.GroundLift - _sink - Plane3D.Drop(Body.GlobalPosition));
            Rotation       = new Vector3(0f, Plane3D.FacingToYaw(Body.Facing), 0f);
            Scale          = Vector3.One * Body.Radius;
            Visible        = !Body.IsHidden;
            speedShare     = Mathf.Clamp(Body.Velocity.Length() / CellBody.SwimSpeed(Body.Radius), 0f, 1f);
        }
        else
        {
            Visible = true;
        }

        // ---------------------------------------------------------------------------
        // Swim
        //   Wave phase and amplitude from the speed, body shader and parts follow.
        // ---------------------------------------------------------------------------

        _phase += dt * (IDLE_FREQUENCY + SWIM_FREQUENCY * speedShare);
        float amplitude  = (IDLE_AMP + SWIM_AMP * speedShare) * SwimScale * (isOnLand ? LAND_WAVE : 1f);
        bool  isVertical = CreatureType.MarineMammal == design.Type;
        SetClay("swim_phase", _phase);
        SetClay("swim_amp", amplitude);
        SetClay("flash", null != Body ? Body.HurtFlash : 0f);
        foreach (Attached part in _attached)
        {
            float bend  = Bend(part.X, design.HalfLength, amplitude, _phase);
            float slope = (Bend(part.X + 0.01f, design.HalfLength, amplitude, _phase) - Bend(part.X - 0.01f, design.HalfLength, amplitude, _phase)) / 0.02f;
            part.Pivot.Position = isVertical ? new Vector3(part.X, part.Y + bend, 0f) : new Vector3(part.X, part.Y, bend);
            part.Pivot.Rotation = isVertical ? new Vector3(0f, 0f, Mathf.Atan(slope)) : new Vector3(0f, -Mathf.Atan(slope), 0f);
        }

        // ---------------------------------------------------------------------------
        // Details
        //   Eye blink and the blowhole spout.
        // ---------------------------------------------------------------------------

        // Eyes blink every few seconds
        float seed       = null != Body ? Body.AnimSeed : Seed;
        bool  isBlinking = (_time + seed) % 3.7f < 0.12f;
        foreach (Node3D eye in _eyes)
        {
            eye.Scale = new Vector3(1f, isBlinking ? 0.15f : 1f, 1f);
        }

        // Spout: three droplets rise from the blowhole and fade
        float spout = ((_time + seed) % SPOUT_PERIOD) / SPOUT_SECONDS;
        for (int i = 0; i < _spouts.Count; i++)
        {
            float t = spout - i * 0.12f;
            _spouts[i].Visible  = t is > 0f and < 1f;
            _spouts[i].Position = new Vector3((i - 1) * 0.03f * t, 0.05f + t * 0.45f, (i - 1) * 0.04f * t);
            _spouts[i].Scale    = Vector3.One * 0.05f * (1f - t * 0.6f);
        }
    }

    private void SetClay(string name, Variant value)
    {
        _clay.SetShaderParameter(name, value);
    }

    private int _paintVersion = -1;

    /// <summary>Hands the spray dots of the design to the body shader, centers on the current body shape.</summary>
    private void UpdatePaint(CreatureDesign design)
    {
        var positions = new Vector4[CreatureDesign.MAX_SPLATS];
        var colors    = new Vector4[CreatureDesign.MAX_SPLATS];
        int count     = Mathf.Min(design.Splats.Count, CreatureDesign.MAX_SPLATS);
        for (int i = 0; i < count; i++)
        {
            PaintSplat splat  = design.Splats[i];
            Vector3    center = design.SplatPoint(splat);
            positions[i] = new Vector4(center.X, center.Y, center.Z, splat.Radius);
            colors[i]    = new Vector4(splat.Color.R, splat.Color.G, splat.Color.B, 1f);
        }
        SetClay("splat_pos", positions);
        SetClay("splat_color", colors);
        SetClay("splat_count", count);
    }

    /// <summary>
    /// Sideways (fish) or vertical (marine mammal) offset of the spine at an X. Zero at
    /// the nose, growing quadratically to the tail. The same formula as shaders/clay.gdshader.
    /// </summary>
    /// <param name="x">(Required) Spine X in unit space.</param>
    /// <param name="halfLength">(Required) CreatureDesign.HalfLength.</param>
    /// <param name="amplitude">(Required) Offset at the tail.</param>
    /// <param name="phase">(Required) Wave phase.</param>
    public static float Bend(float x, float halfLength, float amplitude, float phase)
    {
        float t = Mathf.Clamp((halfLength - x) / (2f * halfLength), 0f, 1f);
        return amplitude * t * t * Mathf.Sin(x * 4f - phase);
    }

    // ===========================================================================
    // ===========================   BUILD CREATURE   ============================
    // ===========================================================================
    //
    //   FitCameraToParts  Frames FitCamera on the built meshes (palette tiles).
    //   Rebuild         Clears everything, builds body (unless IsPartsOnly), posed parts and (in the world) the threat ring.
    //   BuildBodyMesh   The lofted clay body: elliptic rings along the spine.
    //   BuildPart       One placed part on a pivot that rides the swim wave.
    //   BuildWings      A pair of membrane or feather wings on the back.
    //   BuildLimb       A sculpted limb per flank: a clay tube through its joints, a foot or hand on the tip.
    //   RootOn          Where a limb starts on a flank: the body, or the posed joint of its parent (a branch).
    //   AttachEnd       The foot (flat) or hand (along the last segment) on the limb tip.
    //   LimbMesh        The tube mesh along a smoothed joint path.
    //   TryGetLimbFrame Where a sculpted limb sits, for the joint handles of the editor.
    //   BuildEye        One eye on the flank, pupil forward and outwards.
    //   AddFillet       A soft clay ball where a part grows out of the body.
    //   AddPivot        A pivot at a spine X, moved by the swim wave.
    //

    private const float FIT_MARGIN = 1.1f;
    private static readonly Vector3 FIT_DIRECTION = new Vector3(0.35f, 0.55f, 1f).Normalized();

    private const float LEFT_SIDE_Z = -0.02f;

    /// <summary>
    /// Points FitCamera at the bounds of every mesh built (rest pose of the models), from
    /// the front flank and slightly above, far enough that the bounding sphere fits the view.
    /// Parts only: the left half of a pair is hidden, one limb fills the tile better.
    /// </summary>
    private void FitCameraToParts()
    {
        Aabb? bounds = null;
        foreach (MeshInstance3D mesh in FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            // Local transform up to this visual, the visual itself stays at the origin of its studio
            Transform3D toVisual = Transform3D.Identity;
            for (Node node = mesh; node != this && node is Node3D node3D; node = node.GetParent())
            {
                toVisual = node3D.Transform * toVisual;
            }
            Aabb box = toVisual * mesh.GetAabb();
            if (IsPartsOnly && box.GetCenter().Z < LEFT_SIDE_Z)
            {
                mesh.Visible = false;
                continue;
            }
            bounds = null == bounds ? box : bounds.Value.Merge(box);
        }
        if (null == bounds)
        {
            return;
        }
        Vector3 center   = bounds.Value.GetCenter();
        float   radius   = Mathf.Max(bounds.Value.Size.Length() * 0.5f, 0.05f);
        float   distance = radius / Mathf.Sin(Mathf.DegToRad(FitCamera.Fov) * 0.5f) * FIT_MARGIN;
        Vector3 eye      = center + FIT_DIRECTION * distance;
        FitCamera.Transform = new Transform3D(Basis.LookingAt(center - eye, Vector3.Up), eye);
    }

    private void Rebuild(CreatureDesign design)
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        _attached.Clear();
        _limbPieces.Clear();
        _limbFrames.Clear();
        _jointPoints.Clear();
        _eyes.Clear();
        _spouts.Clear();

        // Body
        Color color = CurrentColor;
        _clay      ??= new ShaderMaterial();
        _clay.Shader = color.A < 0.99f ? TRANSLUCENT_SHADER : CLAY_SHADER;
        SetClay("pattern", (int)design.Pattern);
        SetClay("pattern_color", design.PatternColor ?? color.Darkened(0.45f));
        SetClay("clay_color", color);
        SetClay("seed", null != Body ? Body.AnimSeed : Seed);
        SetClay("knead", Knead);
        SetClay("swim_vertical", CreatureType.MarineMammal == design.Type ? 1f : 0f);
        SetClay("half_length", Mathf.Max(design.HalfLength, 0.1f));
        if (!IsPartsOnly)
        {
            CellVisual3D.AddMesh(this, BuildBodyMesh(design), _clay, Vector3.Zero, Vector3.One);
        }

        // Clay of sculpted limbs: same look, no swim wave of its own (the limb rides its pivot)
        _limbClay = (ShaderMaterial)_clay.Duplicate();
        _limbClay.SetShaderParameter("swim_amp", 0f);
        _limbClay.SetShaderParameter("splat_count", 0);  // spray paint lives on the body only
        _knuckleClay = (ShaderMaterial)_limbClay.Duplicate();
        _knuckleClay.SetShaderParameter("pattern", 0);   // the paint would shrink to specks on the small knuckles

        // Parts, each posed by side, size, shift and turn (POSE AND PICK)
        _partMeshes.Clear();
        _partAnchors.Clear();
        for (int index = 0; index < design.Parts.Count; index++)
        {
            int firstPivot = _attached.Count;
            BuildPart(design, design.Parts[index], color);
            PosePart(index, design.Parts[index], firstPivot);

            // Sculpted limbs remember their posed shoulder per flank (joint handles of the editor)
            // and where their joints ended up (branches grow from them, the parent is built first)
            foreach ((Node3D piece, int flank) in _limbPieces)
            {
                if (null == piece.GetParent())
                {
                    continue;
                }
                _limbFrames.Add((index, flank, piece));
                var points = new List<Vector3> { ToLocal(piece.GlobalTransform * Vector3.Zero) };
                points.AddRange(design.Parts[index].Joints.Select(joint => ToLocal(piece.GlobalTransform * joint)));
                _jointPoints[(index, flank)] = points;
            }
            _limbPieces.Clear();
        }
        Log.Verbose("CreatureVisual3D", $"Rebuilt {design.Type} with {design.Girths.Count} vertebrae and {design.Parts.Count} part(s).");
    }

    /// <summary>
    /// One elliptic ring per step along X from the tail tip to the nose tip, the radius
    /// from CreatureDesign.RadiusAt. The steps bunch up at both tips (cosine spacing),
    /// where the profile bends fastest. The tip rings collapse to a point.
    /// Quad winding: A (ring i), C (next segment), B (ring i + 1) is clockwise seen
    /// from outside, the front face in Godot.
    /// </summary>
    /// <param name="design">(Required) The design with at least MIN_VERTEBRAE girths.</param>
    /// <returns>The body mesh in unit space.</returns>
    public static ArrayMesh BuildBodyMesh(CreatureDesign design)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i <= RINGS; i++)
        {
            float u      = 0.5f - 0.5f * Mathf.Cos(Mathf.Pi * i / RINGS);
            float x      = Mathf.Lerp(design.TailTipX, design.NoseTipX, u);
            float radius = 0 == i || RINGS == i ? 0f : design.RadiusAt(x);
            // The ring sits on the bent spine and tilts with it: its up axis is perpendicular to the slope
            var     center = new Vector3(x, design.LiftAt(x), 0f);
            Vector3 up     = new Vector3(-design.SlopeAt(x) * RING_TILT, 1f, 0f).Normalized();
            for (int j = 0; j < SEGMENTS; j++)
            {
                float angle = Mathf.Tau * j / SEGMENTS;
                surface.AddVertex(center + up * (Mathf.Sin(angle) * radius * design.HeightScale) + Vector3.Back * (Mathf.Cos(angle) * radius * design.WidthScale));
            }
        }
        for (int i = 0; i < RINGS; i++)
        {
            for (int j = 0; j < SEGMENTS; j++)
            {
                int a = i * SEGMENTS + j;
                int b = (i + 1) * SEGMENTS + j;
                int c = i * SEGMENTS + (j + 1) % SEGMENTS;
                int d = (i + 1) * SEGMENTS + (j + 1) % SEGMENTS;
                surface.AddIndex(a);
                surface.AddIndex(c);
                surface.AddIndex(b);
                surface.AddIndex(b);
                surface.AddIndex(c);
                surface.AddIndex(d);
            }
        }
        surface.GenerateNormals();
        return surface.Commit();
    }

    private void BuildPart(CreatureDesign design, CreaturePart part, Color color)
    {
        var                finSkin = new Dictionary<string, Material> { ["Fin"] = FinMaterial(design, color) };
        Color              outline = color.Darkened(0.5f);
        float              x       = design.XAt(part.T);
        float              radius  = design.RadiusAt(x);
        float              top     = design.TopAt(x);
        float              flank   = design.FlankAt(x);
        float              finSize = 0.6f + design.MeanGirth * 0.5f;

        switch (part.Kind)
        {
            // Tail: a forked upright fin (fish) or flat flukes (marine mammal) behind the tail tip, fluttering
            case CreaturePartKind.CaudalFin:
            case CreaturePartKind.Fluke:
                Node3D tail = AttachModel(AddPivot(design.TailTipX + 0.06f), part.Kind, color, Vector3.Zero, finSize, 1f, overrides: finSkin);
                PlayLoop(tail, MODELS[part.Kind].Animation, 0f, FLUTTER_RATE);
                break;

            // Back: a swept sail, a row of spines or the blowhole with its spout
            case CreaturePartKind.DorsalFin:
                Node3D sail = AttachModel(AddPivot(x), part.Kind, color, new Vector3(0f, top - 0.03f, 0f), 0.5f + radius * 0.6f, 1f, overrides: finSkin);
                PlayLoop(sail, MODELS[part.Kind].Animation, 0.3f, FLUTTER_RATE);
                break;
            case CreaturePartKind.Spines:
                for (int i = -2; i <= 2; i++)
                {
                    float   spineX = x + i * 0.09f;
                    Node3D  pivot  = AddPivot(spineX);
                    MeshInstance3D spine = CellVisual3D.AddMesh(pivot, CellVisual3D.ConeMesh(), CellVisual3D.Material(color.Lightened(0.4f), 0f, 0.35f),
                        new Vector3(0f, design.TopAt(spineX) + 0.08f, 0f), new Vector3(0.6f, 0.55f, 0.6f));
                    spine.Rotation = new Vector3(0f, 0f, 0.35f);   // swept back a little
                    foreach (float side in new[] { -1f, 1f })
                    {
                        MeshInstance3D flankSpine = CellVisual3D.AddMesh(pivot, CellVisual3D.ConeMesh(), CellVisual3D.Material(color.Lightened(0.4f), 0f, 0.35f),
                            new Vector3(0f, design.TopAt(spineX) * 0.3f, side * (design.FlankAt(spineX) + 0.06f)), new Vector3(0.5f, 0.45f, 0.5f));
                        flankSpine.Rotation = new Vector3(side * Mathf.Pi * 0.4f, 0f, 0f);
                    }
                }
                break;
            case CreaturePartKind.Blowhole:
                Node3D hole = AddPivot(x);
                CellVisual3D.AddMesh(hole, CellVisual3D.SphereMesh(), CellVisual3D.Material(outline), new Vector3(0f, top - 0.005f, 0f), new Vector3(0.05f, 0.02f, 0.035f));
                var spout = new Node3D { Position = new Vector3(0f, top, 0f) };
                hole.AddChild(spout);
                for (int i = 0; i < 3; i++)
                {
                    MeshInstance3D drop = CellVisual3D.AddMesh(spout, CellVisual3D.SphereMesh(), CellVisual3D.Material(new Color(0.85f, 0.95f, 1f, 0.75f), 0.4f, 0.1f), Vector3.Zero, Vector3.One * 0.05f);
                    drop.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                    _spouts.Add(drop);
                }
                break;

            // Flanks: a pair of paddles tilted down, or a pair of eyes
            case CreaturePartKind.PectoralFin:
                Node3D fins = AddPivot(x);
                foreach (float side in new[] { -1f, 1f })
                {
                    // Tilted down a little, mirrored for the left flank, both flutter in step
                    var paddle = new Node3D { Position = new Vector3(0f, -top * 0.3f, side * flank * 0.85f), Rotation = new Vector3(side * 0.45f, 0f, 0f) };
                    fins.AddChild(paddle);
                    Node3D model = AttachModel(paddle, part.Kind, color, Vector3.Zero, 0.55f + radius * 0.6f, side, overrides: finSkin);
                    PlayLoop(model, MODELS[part.Kind].Animation, 0.15f, FLUTTER_RATE);
                }
                break;
            case CreaturePartKind.Eye:
                Node3D eyes = AddPivot(x);
                foreach (float side in new[] { -1f, 1f })
                {
                    BuildEye(eyes, new Vector3(0f, top * 0.3f, side * flank * 0.88f), side, 0.08f + radius * 0.12f);
                }
                break;

            // Flanks: two dark glossy pits per side
            case CreaturePartKind.PitEye:
                Node3D pits = AddPivot(x);
                foreach (float side in new[] { -1f, 1f })
                {
                    for (int i = 0; i < 2; i++)
                    {
                        CellVisual3D.AddMesh(pits, CellVisual3D.SphereMesh(), CellVisual3D.Material(new Color(0.05f, 0.05f, 0.08f), 0f, 0.1f),
                            new Vector3(i * 0.07f - 0.035f, top * (0.35f + i * 0.1f), side * flank * 0.93f), Vector3.One * 0.035f);
                    }
                }
                break;

            // Top of the head: two swaying stalks leaning outwards with a big eye on top
            case CreaturePartKind.StalkEye:
                Node3D stalks      = AddPivot(x);
                float  stalkLength = 0.3f + radius * 0.2f;
                foreach (float side in new[] { -1f, 1f })
                {
                    var stalk = new Node3D { Position = new Vector3(0f, top * 0.8f, side * flank * 0.35f), Rotation = new Vector3(side * 0.45f, 0f, -0.2f) };
                    stalks.AddChild(stalk);
                    Node3D model = AttachModel(stalk, part.Kind, color, Vector3.Zero, stalkLength, side);
                    AddFillet(stalk, Vector3.Zero, stalkLength * STALK_FILLET);
                    PlayLoop(model, MODELS[part.Kind].Animation, side * 0.3f, SWAY_RATE);
                }
                break;

            // Sculpted limb: a clay tube through its joints, on both flanks
            case CreaturePartKind.StubLimb:
            case CreaturePartKind.BentLimb:
            case CreaturePartKind.Limb:
            case CreaturePartKind.Tentacle:
            // Feet and hands: a sculpted limb with the foot or hand model on its tip
            case CreaturePartKind.Legs:
            case CreaturePartKind.StubbyLegs:
            case CreaturePartKind.ClawLegs:
            case CreaturePartKind.CrabLegs:
            case CreaturePartKind.Hands:
            case CreaturePartKind.Claws:
                BuildLimb(design, part, color);
                break;

            // Skull: two folded pink lobes bulging from the top of the head
            case CreaturePartKind.SmallBrain:
            case CreaturePartKind.BigBrain:
                float  brainX    = design.XAt(0.78f);
                float  brainY    = design.TopAt(brainX) * 0.9f;
                float  brainSize = (CreaturePartKind.BigBrain == part.Kind ? 1f : 0.7f) * (0.1f + design.RadiusAt(brainX) * 0.22f) * 1.8f;
                AttachModel(AddPivot(brainX), part.Kind, color, new Vector3(0f, brainY, 0f), brainSize, 1f);
                break;

            // Back: a pair of wings
            case CreaturePartKind.MembraneWings:
            case CreaturePartKind.FeatherWings:
                BuildWings(part.Kind, x, top, flank, radius, color);
                break;

            // Lung: two nostrils on top of the nose, wherever it was placed inside.
            // The big lung has wider nostrils and a puffed throat sac under the chin.
            case CreaturePartKind.Lung:
            case CreaturePartKind.BigLung:
                bool   isBigLung = CreaturePartKind.BigLung == part.Kind;
                float  nostrilX  = design.SpineEndX + (design.NoseTipX - design.SpineEndX) * 0.45f;
                Node3D nose      = AddPivot(nostrilX);
                foreach (float side in new[] { -1f, 1f })
                {
                    CellVisual3D.AddMesh(nose, CellVisual3D.SphereMesh(), CellVisual3D.Material(outline),
                        new Vector3(0f, design.TopAt(nostrilX) * 0.92f, side * (isBigLung ? 0.07f : 0.05f)), new Vector3(0.03f, 0.015f, 0.022f) * (isBigLung ? 1.6f : 1f));
                }
                if (isBigLung)
                {
                    float throatX = design.SpineEndX - 0.1f;
                    CellVisual3D.AddMesh(AddPivot(throatX), CellVisual3D.SphereMesh(), CellVisual3D.Material(color.Lightened(0.3f), 0f, 0.5f),
                        new Vector3(0f, -design.TopAt(throatX) * 0.75f, 0f), new Vector3(0.18f, 0.12f, design.FlankAt(throatX) * 0.6f));
                }
                break;

            // Nose: a dark mouth slit with a row of teeth
            case CreaturePartKind.Jaw:
                float  noseX = design.NoseTipX;
                float  mouthX = noseX - Mathf.Max(0.1f, (noseX - design.SpineEndX) * 0.45f);
                Node3D jaw   = AddPivot(mouthX);
                float  mouthTop   = design.TopAt(mouthX);
                float  mouthFlank = design.FlankAt(mouthX);
                CellVisual3D.AddMesh(jaw, CellVisual3D.SphereMesh(), CellVisual3D.Material(new Color(0.25f, 0.06f, 0.08f)),
                    new Vector3(0.02f, -mouthTop * 0.25f, 0f), new Vector3(0.16f, 0.035f, mouthFlank * 0.95f));
                for (int i = -2; i <= 2; i++)
                {
                    float          along = i / 2f;
                    MeshInstance3D tooth = CellVisual3D.AddMesh(jaw, CellVisual3D.ConeMesh(), CellVisual3D.Material(new Color(0.97f, 0.95f, 0.88f), 0.1f, 0.3f),
                        new Vector3(0.08f - Mathf.Abs(along) * 0.08f, -mouthTop * 0.25f + 0.02f, along * mouthFlank * 0.8f), new Vector3(0.35f, 0.15f, 0.35f));
                    tooth.Rotation = new Vector3(Mathf.Pi, 0f, 0f);   // tip down
                }
                break;

            default:
                throw new System.ArgumentOutOfRangeException(nameof(part), $"part must be a known CreaturePartKind, got '{part.Kind}'.");
        }
    }

    /// <summary>A pair of wing models on the back, flapping together.</summary>
    private void BuildWings(CreaturePartKind kind, float x, float top, float flank, float radius, Color color)
    {
        Node3D shoulders = AddPivot(x);
        float  span      = (0.7f + radius * 0.8f) * 1.15f;
        foreach (float side in new[] { -1f, 1f })
        {
            Node3D model = AttachModel(shoulders, kind, color, new Vector3(0f, top * 0.8f, side * flank * 0.4f), span, side);
            AddFillet(shoulders, new Vector3(0f, top * 0.8f, side * flank * 0.4f), span * WING_FILLET);
            PlayLoop(model, MODELS[kind].Animation, 0f, FLAP_RATE);
        }
    }

    /*
      A sculpted limb per flank: a piece at the shoulder (CreatureDesign.LimbRoot), mirrored
      on Z for the left flank like the models, holding one clay tube from the shoulder
      through every joint, a knuckle at each joint and a round tip. Procedural instead of a
      Blender model on purpose: the joints are free, no fixed model fits them.
    */
    private const float LIMB_ROOT_RADIUS = 0.14f;            // chunky on purpose, cartoon monsters
    private const float LIMB_TIP_RADIUS = 0.08f;

    private ShaderMaterial _limbClay;
    private ShaderMaterial _knuckleClay;
    private readonly List<(Node3D Piece, int Flank)> _limbPieces = new();
    private readonly Dictionary<(int Part, int Flank), List<Vector3>> _jointPoints = new();   // shoulder + joints, visual space
    private readonly List<(int Part, int Flank, Node3D Piece)> _limbFrames = new();

    private void BuildLimb(CreatureDesign design, CreaturePart part, Color color)
    {
        bool    hasEnd = part.Info.IsEnd;
        Vector3 root   = RootOn(design, part, 1);
        Node3D  pivot  = AddPivot(root.X);
        var     path  = new List<Vector3> { Vector3.Zero };
        path.AddRange(part.Joints);
        ArrayMesh tube = LimbMesh(path);
        foreach (float side in new[] { -1f, 1f })
        {
            var piece = new Node3D { Position = RootOn(design, part, (int)side) - pivot.Position, Scale = new Vector3(1f, 1f, side) };
            pivot.AddChild(piece);
            CellVisual3D.AddMesh(piece, tube, _limbClay, Vector3.Zero, Vector3.One);
            AddFillet(piece, Vector3.Zero, LIMB_ROOT_RADIUS * LIMB_FILLET);
            for (int i = 1; i < path.Count; i++)
            {
                bool  isTip  = i == path.Count - 1;
                if (isTip && hasEnd)
                {
                    continue;                        // the foot or hand takes the place of the tip knob
                }
                float radius = Mathf.Lerp(LIMB_ROOT_RADIUS, LIMB_TIP_RADIUS, (float)i / (path.Count - 1)) * (isTip ? 1.35f : 1.12f);
                CellVisual3D.AddMesh(piece, CellVisual3D.SphereMesh(), _knuckleClay, path[i], Vector3.One * radius);
            }
            if (hasEnd)
            {
                AttachEnd(piece, part.Kind, color, path, side);
            }
            _limbPieces.Add((piece, (int)side));
        }
    }

    /*
      Where a limb starts on one flank, in visual space:
      > A branch: the posed joint of its parent on that flank (the parent is built first);
        a parent that is a single half has only one flank, the other mirrors it.
      > A limb on the body: its shoulder on the lower flank (CreatureDesign.LimbRoot).
    */
    private Vector3 RootOn(CreatureDesign design, CreaturePart part, int side)
    {
        int parentIndex = design.IndexOfId(part.ParentId);
        if (parentIndex >= 0)
        {
            int joint = 1 + Mathf.Clamp(part.ParentJoint, 0, design.Parts[parentIndex].Joints.Count - 1);
            if (_jointPoints.TryGetValue((parentIndex, side), out List<Vector3> points))
            {
                return points[joint];
            }
            if (_jointPoints.TryGetValue((parentIndex, -side), out List<Vector3> mirrored))
            {
                return mirrored[joint] * new Vector3(1f, 1f, -1f);
            }
        }
        Vector3 root = design.LimbRoot(part);
        return new Vector3(root.X, root.Y, side * root.Z);
    }

    /*
      The end on the limb tip, in limb space (the piece mirrors the left flank already):
      - Feet stand flat, toes forward (+X), whatever the last segment does.
      - Hands follow the last segment: +X along it, the palm facing down.
    */
    private void AttachEnd(Node3D piece, CreaturePartKind kind, Color color, IReadOnlyList<Vector3> path, float side)
    {
        Node3D  end = NewModel(kind, color);
        Vector3 tip = path[^1];
        Basis   turn = Basis.Identity;
        if (CreatureCategory.Hands == CreatureCatalog.Get(kind).Category)
        {
            Vector3 along = (tip - path[^2]).Normalized();
            Vector3 aside = along.Cross(Vector3.Up);
            aside = aside.LengthSquared() < 1e-4f ? Vector3.Back : aside.Normalized();
            turn  = new Basis(along, aside.Cross(along), aside);
        }
        end.Transform = new Transform3D(turn * Basis.FromScale(Vector3.One * END_SIZE), tip);
        piece.AddChild(end);
        if (null != MODELS[kind].Animation)
        {
            PlayLoop(end, MODELS[kind].Animation, side * 0.2f, GRAB_RATE);
        }
    }

    /*
      Tube along a path: the path is smoothed (Catmull-Rom, LIMB_STEPS per segment), every
      sample gets a ring of LIMB_SEGMENTS points around its tangent. The ring frame is
      carried along by parallel transport, so the tube does not twist. (tangent, normal,
      binormal) is right handed like (X, Y, Z) of the body loft, so the same winding faces out.
    */
    private const int LIMB_STEPS = 10;
    private const int LIMB_SEGMENTS = 18;

    private static ArrayMesh LimbMesh(IReadOnlyList<Vector3> path)
    {
        // Smoothed samples, thick at the shoulder, thin at the tip
        var samples = new List<Vector3>();
        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 before = path[Mathf.Max(i - 1, 0)];
            Vector3 after  = path[Mathf.Min(i + 2, path.Count - 1)];
            for (int k = 0; k < LIMB_STEPS; k++)
            {
                samples.Add(path[i].CubicInterpolate(path[i + 1], before, after, (float)k / LIMB_STEPS));
            }
        }
        samples.Add(path[^1]);

        var     surface = new SurfaceTool();
        Vector3 normal  = Vector3.Zero;
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < samples.Count; i++)
        {
            Vector3 tangent = (samples[Mathf.Min(i + 1, samples.Count - 1)] - samples[Mathf.Max(i - 1, 0)]).Normalized();
            if (Vector3.Zero == normal)
            {
                normal = Mathf.Abs(tangent.Dot(Vector3.Up)) > 0.9f ? Vector3.Right : Vector3.Up;
            }
            normal = (normal - tangent * normal.Dot(tangent)).Normalized();   // parallel transport
            Vector3 binormal = tangent.Cross(normal);
            float   radius   = Mathf.Lerp(LIMB_ROOT_RADIUS, LIMB_TIP_RADIUS, (float)i / (samples.Count - 1));
            for (int j = 0; j < LIMB_SEGMENTS; j++)
            {
                float angle = Mathf.Tau * j / LIMB_SEGMENTS;
                surface.AddVertex(samples[i] + (normal * Mathf.Sin(angle) + binormal * Mathf.Cos(angle)) * radius);
            }
        }
        for (int i = 0; i < samples.Count - 1; i++)
        {
            for (int j = 0; j < LIMB_SEGMENTS; j++)
            {
                int a = i * LIMB_SEGMENTS + j;
                int b = (i + 1) * LIMB_SEGMENTS + j;
                int c = i * LIMB_SEGMENTS + (j + 1) % LIMB_SEGMENTS;
                int d = (i + 1) * LIMB_SEGMENTS + (j + 1) % LIMB_SEGMENTS;
                surface.AddIndex(a);
                surface.AddIndex(c);
                surface.AddIndex(b);
                surface.AddIndex(b);
                surface.AddIndex(c);
                surface.AddIndex(d);
            }
        }
        surface.GenerateNormals();
        return surface.Commit();
    }

    /// <summary>The posed shoulder of a sculpted limb on one flank: joints in limb space times this frame are world points.</summary>
    /// <param name="index">(Required) Index in Design.Parts, a Limb.</param>
    /// <param name="flank">(Required) -1 or 1.</param>
    /// <param name="frame">The global transform of the limb piece (pose, size and mirror included).</param>
    /// <returns>False when the limb has no piece on that flank.</returns>
    public bool TryGetLimbFrame(int index, int flank, out Transform3D frame)
    {
        foreach ((int part, int pieceFlank, Node3D piece) in _limbFrames)
        {
            if (index == part && flank == pieceFlank && IsInstanceValid(piece) && piece.IsInsideTree())
            {
                frame = piece.GlobalTransform;
                return true;
            }
        }
        frame = Transform3D.Identity;
        return false;
    }


    /// <param name="side">(Required) -1 left flank, 1 right flank, the pupil looks outwards there.</param>
    private void BuildEye(Node3D pivot, Vector3 position, float side, float size)
    {
        var eye = new Node3D { Position = position, Scale = Vector3.One };
        pivot.AddChild(eye);
        CellVisual3D.AddMesh(eye, CellVisual3D.SphereMesh(), CellVisual3D.Material(new Color(0.97f, 0.97f, 0.97f), 0.25f, 0.2f), Vector3.Zero, Vector3.One * size);
        CellVisual3D.AddMesh(eye, CellVisual3D.SphereMesh(), CellVisual3D.Material(new Color(0.06f, 0.06f, 0.1f), 0f, 0.1f),
            new Vector3(size * 0.35f, size * 0.15f, side * size * 0.6f), Vector3.One * size * 0.55f);
        CellVisual3D.AddMesh(eye, CellVisual3D.SphereMesh(), CellVisual3D.Material(Colors.White, 1.5f),
            new Vector3(size * 0.5f, size * 0.55f, side * size * 0.75f), Vector3.One * size * 0.18f);
        _eyes.Add(eye);
    }

    /*
      Fillet: a flat swelling of plain clay where a limb, wing or stalk grows out of the
      body, half sunk into it, so the part swells out of the skin instead of sticking in
      like a pipe. Flattened along the local Z of the part node (away from the body), so
      it reads as skin, not as a ball. A child of the part's own node: it follows side,
      pose and size. Sizes as share of the part length, tuned on renders.
    */
    private const float WING_FILLET = 0.08f;
    private const float STALK_FILLET = 0.24f;
    private const float LIMB_FILLET = 1.6f;          // of the root radius of the tube
    private const float FILLET_FLATTEN = 0.55f;

    private void AddFillet(Node3D parent, Vector3 position, float radius)
    {
        MeshInstance3D fillet = CellVisual3D.AddMesh(parent, CellVisual3D.SphereMesh(), _knuckleClay, position, new Vector3(radius, radius, radius * FILLET_FLATTEN));
        fillet.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    /// <summary>A pivot on the bent spine at an X: parts built on it sit at the height of the spine there.</summary>
    private Node3D AddPivot(float x)
    {
        float y     = Current.LiftAt(x);
        var   pivot = new Node3D { Position = new Vector3(x, y, 0f) };
        AddChild(pivot);
        _attached.Add(new Attached(pivot, x, y));
        return pivot;
    }

    // ===========================================================================
    // ============================   POSE AND PICK   ============================
    // ===========================================================================
    //
    //   PosePart          Applies side, size, shift and turn of a part to its built pieces.
    //   FlankOf           Which flank a piece sits on, 0 for the midline.
    //   RemovePiece       Drops the other half of a single side part.
    //   PartAt            The placed part under a ray, for picking in the editor.
    //   RayEntry          Where a ray enters a box (slab test), null on a miss.
    //   TryGetPartAnchor  Where the gizmo of a part sits.
    //

    private const float FLANK_EPSILON = 0.005f;
    private static readonly Basis MIRROR = Basis.FromScale(new Vector3(1f, 1f, -1f));

    private readonly List<(int Part, int Flank, MeshInstance3D Mesh)> _partMeshes = new();
    private readonly List<(int Part, int Flank, Vector3 Anchor)> _partAnchors = new();

    /*
      The pieces of a part are the direct children of the pivots BuildPart added for it.
      > Single half (Side -1 or 1): the pieces of the other flank are dropped.
      > Groups: a paired part splits into its two flanks, any other part is one group.
        Every group turns rigidly around its anchor (mean of its piece positions) and
        moves by the offset. The left flank of a paired part takes the mirror image
        (offset Z negated, turn mirrored: MIRROR * turn * MIRROR).
      > Size grows every piece around its own origin (eyes stay on the flank, legs on the hip).
    */
    private void PosePart(int index, CreaturePart part, int firstPivot)
    {
        CreaturePartInfo info         = part.Info;
        Basis            grow         = Basis.FromScale(Vector3.One * BASE_PART_SIZES[info.Category] * part.Size);
        Basis            turn         = Basis.FromEuler(part.Rotation);
        Basis            turnMirrored = MIRROR * turn * MIRROR;

        // Pieces with their flank, the other half of a single side part goes
        var pieces = new List<(Node3D Pivot, Node3D Piece, int Flank)>();
        for (int i = firstPivot; i < _attached.Count; i++)
        {
            Node3D pivot = _attached[i].Pivot;
            foreach (Node3D piece in pivot.GetChildren().OfType<Node3D>())
            {
                int flank = info.IsPaired ? FlankOf(piece) : 0;
                if (0 != part.Side && flank == -part.Side)
                {
                    RemovePiece(piece);
                    continue;
                }
                pieces.Add((pivot, piece, flank));
            }
        }

        // Every group turns around its anchor and shifts, the left flank mirrored
        foreach (int flank in new[] { -1, 0, 1 })
        {
            var group = pieces.Where(entry => flank == entry.Flank).ToList();
            if (0 == group.Count)
            {
                continue;
            }
            Vector3 anchor = Vector3.Zero;
            foreach ((Node3D pivot, Node3D piece, _) in group)
            {
                anchor += pivot.Position + piece.Position;
            }
            anchor /= group.Count;

            bool    isLeft = flank < 0;
            Basis   rotate = isLeft ? turnMirrored : turn;
            Vector3 shift  = isLeft ? MIRROR * part.Offset : part.Offset;
            foreach ((Node3D pivot, Node3D piece, _) in group)
            {
                Vector3 at = pivot.Position + piece.Position;
                piece.Position = anchor + rotate * (at - anchor) + shift - pivot.Position;
                piece.Basis    = rotate * piece.Basis * grow;
                if (piece is MeshInstance3D mesh)
                {
                    _partMeshes.Add((index, flank, mesh));
                }
                foreach (MeshInstance3D child in piece.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
                {
                    _partMeshes.Add((index, flank, child));
                }
            }
            _partAnchors.Add((index, flank, anchor + shift));
        }
    }

    private static int FlankOf(Node3D piece)
    {
        if (piece.Position.Z > FLANK_EPSILON) return 1;
        if (piece.Position.Z < -FLANK_EPSILON) return -1;
        return 0;
    }

    private void RemovePiece(Node3D piece)
    {
        _eyes.RemoveAll(eye => eye == piece || piece.IsAncestorOf(eye));
        piece.GetParent().RemoveChild(piece);
        piece.QueueFree();
    }

    /// <summary>
    /// The placed part whose mesh bounds a ray hits first. Bounding boxes, not triangles:
    /// coarse, but enough to pick fins, eyes and limbs on the turntable.
    /// </summary>
    /// <param name="from">(Required) Ray origin in global space.</param>
    /// <param name="direction">(Required) Ray direction in global space.</param>
    /// <param name="flank">The flank of the hit piece, 0 for a midline part.</param>
    /// <returns>Index in Design.Parts, -1 when the ray hits no part.</returns>
    public int PartAt(Vector3 from, Vector3 direction, out int flank)
    {
        int   closest  = -1;
        float bestDist = float.PositiveInfinity;
        flank = 0;
        foreach ((int part, int pieceFlank, MeshInstance3D mesh) in _partMeshes)
        {
            if (!IsInstanceValid(mesh) || !mesh.IsVisibleInTree())
            {
                continue;
            }
            float? distance = RayEntry(mesh.GlobalTransform * mesh.GetAabb(), from, direction);
            if (null != distance && distance.Value < bestDist)
            {
                bestDist = distance.Value;
                closest  = part;
                flank    = pieceFlank;
            }
        }
        return closest;
    }

    /// <summary>
    /// Slab test: per axis the ray enters and leaves the pair of box planes; the ray hits
    /// the box when the latest entry comes before the earliest exit.
    /// </summary>
    /// <returns>Ray length to the entry point (0 inside the box), null on a miss.</returns>
    public static float? RayEntry(Aabb box, Vector3 from, Vector3 direction)
    {
        float enter = 0f;
        float leave = float.PositiveInfinity;
        for (int axis = 0; axis < 3; axis++)
        {
            float origin = from[axis];
            float step   = direction[axis];
            float low    = box.Position[axis];
            float high   = box.End[axis];
            // Parallel to the slab: inside it or a miss
            if (Mathf.Abs(step) < 1e-6f)
            {
                if (origin < low || origin > high) return null;
                continue;
            }
            float t1 = (low - origin) / step;
            float t2 = (high - origin) / step;
            enter = Mathf.Max(enter, Mathf.Min(t1, t2));
            leave = Mathf.Min(leave, Mathf.Max(t1, t2));
        }
        return enter <= leave ? enter : null;
    }

    /// <summary>Where the gizmo of a part sits: its shifted anchor (rest pose, without the swim wave).</summary>
    /// <param name="index">(Required) Index in Design.Parts.</param>
    /// <param name="flank">(Required) -1 or 1 for one half of a pair, 0 for a midline part.</param>
    /// <param name="anchor">The anchor in global space.</param>
    /// <returns>False when the part has no group on that flank (not built yet).</returns>
    public bool TryGetPartAnchor(int index, int flank, out Vector3 anchor)
    {
        foreach ((int part, int groupFlank, Vector3 local) in _partAnchors)
        {
            if (index == part && flank == groupFlank)
            {
                anchor = ToGlobal(local);
                return true;
            }
        }
        anchor = Vector3.Zero;
        return false;
    }
}
