// NOTE:
//   3D rendering of an organism (stage 4): a clay body lofted along the spine of its
//   CreatureDesign (shaders/clay.gdshader) plus fins, eyes, jaw, spines, blowhole,
//   legs, hands, a brain, the nostrils of a lung and wings. Legs, hands, wings, brain and
//   stalk eyes are rigged models from tools/blender (assets/models/creature, Models),
//   recolored with the body color and looping their own animation.
//   Built in unit space (+X forward, contact radius 1) and scaled to Body.Radius. The
//   body swims with a wave that grows towards the tail, side to side for fish, up and
//   down for marine mammals; the parts ride the same wave (Bend). On land (Body.IsOnLand)
//   the wave stops: legs lift the body and walk, without legs it crawls on its belly with
//   a rest of the wave.
//   Wings flap slowly, hands grab, stalk eyes sway all the time.
//   Coordinator of the classes in Visual/: it follows the body, owns the clay materials
//   and rebuilds on change; the parts are built, animated and picked by its component
//   CreatureParts3D (BuildPart, BASE_PART_SIZES), the meshes come from CreatureClay, the
//   models from CreatureModels (MODELS), the wave from SwimWave.
//   Two modes:
//     - In the world: created by CellBody.ShowCreature, follows Body like CellVisual3D.
//     - Standalone (editor preview, icons): Body stays null, Design and BodyColor are set.
//
//   var preview = new CreatureVisual3D { Design = draft, BodyColor = color, Knead = 0.8f };
//   viewport.AddChild(preview);

//
//   Chapters:
//     SETTINGS          Inputs, clay shaders and the part models.
//     FOLLOW AND SWIM   Rebuild on change, follow the body, swim wave, blink and spout.
//     BUILD CREATURE    Clay materials, the lofted body mesh and the parts component.
//     PICK PARTS        Picking the parts by ray, their gizmo anchors and limb frames.
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
    //   Inputs   Body or Design and BodyColor, clay look and swimming.
    //   Models   Creature part models from tools/blender, see Visual/CreatureModels.cs.
    //

    // ---------------------------------------------------------------------------
    // Inputs
    //   Body or Design and BodyColor, clay look and swimming.
    // ---------------------------------------------------------------------------

    public const float LIVING_KNEAD = 0.2f;          // the clay look left on a living creature
    public const float EDITOR_KNEAD = 0.75f;         // fresh clay in the creature editor
    private const float IDLE_AMP = 0.05f;            // swim wave at rest, in unit space
    private const float SWIM_AMP = 0.13f;            // added at full speed
    private const float IDLE_FREQUENCY = 2.5f;       // wave phase per second at rest
    private const float SWIM_FREQUENCY = 9f;         // added at full speed
    private const float SPOUT_PERIOD = 5f;           // a marine mammal breathes every few seconds
    private const float SPOUT_SECONDS = 0.9f;
    private const float MIN_LAND_LIFT = -0.45f;      // a flat body sinks at most this far to lay its belly on the ground
    private const float MAX_LAND_LIFT = 4f;          // an upright body with long arms rises at most this far
    private const float BREATH_MIN_GAPE = 0.03f;     // only an open mouth breathes, a closed line stays a line
    private const float BREATH_SHARE = 0.18f;        // how far the opening widens and narrows
    private const float BREATH_RATE = 1.7f;
    private const float POSTURE_EASE = 5f;           // how fast the body pitches into its posture (per second)
    private const float CRAWL_WAVE = 0.25f;          // share of the swim wave left on land without legs: the wiggle is its crawl
    private const float WAVE_EASE = 4f;              // per second, the wave fades in and out at the shore

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

    /// <summary>Whether the body stands in the posture of its design (CreatureDesign.Pitch). The editor sculpts it level and turns this off.</summary>
    public bool ShowsPosture { get; set; } = true;

    /// <summary>Whether the limbs move (walk, paddle, sway). The editor turns it off: its joint handles sit on the rest pose.</summary>
    public bool IsMoving { get; set; } = true;

    /// <summary>Without a body (editor): the speed the motion is shown at, 0 standing .. 1 full speed.</summary>
    public float PreviewSpeed { get; set; }

    /// <summary>Without a body (editor): true shows the motion on land (walk), false in the water (swim wave, paddle).</summary>
    public bool PreviewsLand { get; set; }

    /// <summary>(Optional) A camera that frames the parts after every rebuild (palette tiles). Defaults to none.</summary>
    public Camera3D FitCamera { get; set; }

    private CreatureDesign Current => null != Body ? Body.Creature : Design;
    private Color CurrentColor => null != Body ? Body.BodyColor : BodyColor;

    // ---------------------------------------------------------------------------
    // Models
    //   Creature part models from tools/blender, see Visual/CreatureModels.cs.
    // ---------------------------------------------------------------------------

    public const string MODEL_DIR = CreatureModels.MODEL_DIR;

    /// <summary>Model file and animation per part kind (CreatureModels.MODELS, new entries go there).</summary>
    public static IReadOnlyDictionary<CreaturePartKind, (string File, string Animation)> MODELS => CreatureModels.MODELS;

    /// <summary>A new instance of a creature part model, recolored (CreatureModels.NewModel).</summary>
    /// <param name="kind">(Required) A part listed in MODELS.</param>
    /// <param name="bodyColor">(Required) Color of skin, pads, feathers and membrane.</param>
    /// <param name="overrides">(Optional) Materials by Blender name replacing the recolor. Defaults to none.</param>
    /// <example><code>Node3D legs = CreatureVisual3D.NewModel(CreaturePartKind.Legs, color);</code></example>
    public static Node3D NewModel(CreaturePartKind kind, Color bodyColor, IReadOnlyDictionary<string, Material> overrides = null)
    {
        return CreatureModels.NewModel(kind, bodyColor, overrides);
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

    private float _lift;
    private float _sink;                             // eases to Plane3D.SeaDrop in the water, 0 on land
    private float _wave = 1f;                        // share of the swim wave: 1 in the water with a fin, 0 without one or walking on land
    private ShaderMaterial _clay;
    private int _signature;
    private float _time;
    private float _phase;
    private bool _wasMoving;

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
        bool  isOnLand   = null != Body ? Body.IsOnLand : PreviewsLand;
        // On land the lowest part of the body (in its posture) rests on the ground, raised by the hover height of the
        // design: legs lift the body as far as they reach, a jellyfish stands on its arms or floats above them
        float landLift = Mathf.Clamp(Coast.GROUND_HEIGHT - Lowest, MIN_LAND_LIFT, MAX_LAND_LIFT) + design.Hover;
        _lift = Mathf.Lerp(_lift, isOnLand ? landLift : 0f, Mathf.Clamp(dt * 4f, 0f, 1f));
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
            Visible    = true;
            speedShare = Mathf.Clamp(PreviewSpeed, 0f, 1f);
        }

        // ---------------------------------------------------------------------------
        // Swim
        //   Wave phase and amplitude from the speed, body shader and parts follow.
        // ---------------------------------------------------------------------------

        // Pace and size of the wave are part of the look of the animal (CreatureDesign.WaveSpeed, WaveAmount)
        _phase += dt * (IDLE_FREQUENCY + SWIM_FREQUENCY * speedShare) * design.WaveSpeed;
        // The parts decide the motion (decision of the user 2026-11-15), the phase keeps running so the wave fades in smoothly:
        // > In the water: the swim wave with a fin, fluke or tail, a stiff glide without one
        // > On land on legs: stiff, the legs walk
        // > On land without legs: a small wiggle, that is its crawl
        // > No up and down at all, in the water and on land (decision of the user 2026-11-16, it looked odd and harsh):
        //   a body whose wave would run up and down (marine mammal) stays stiff, only the side to side wave of a fish moves
        bool  isVertical = CreatureType.MarineMammal == design.Type;
        float waveGoal   = isVertical ? 0f : !isOnLand ? (design.SwimsWithWave ? 1f : 0f) : design.LegPairs > 0 ? 0f : CRAWL_WAVE;
        _wave = Mathf.Lerp(_wave, waveGoal, Mathf.Clamp(dt * WAVE_EASE, 0f, 1f));
        float amplitude  = (IDLE_AMP + SWIM_AMP * speedShare) * SwimScale * _wave * design.WaveAmount;
        SetClay("swim_phase", _phase);
        SetClay("swim_amp", amplitude);
        SetClay("flash", null != Body ? Body.HurtFlash : 0f);
        _parts.Swim(design.HalfLength, amplitude, _phase, isVertical);

        // Limbs: a walk on land, a paddle in the water; the sculpted pose of the editor stays still
        if (IsMoving)
        {
            _parts.MoveLimbs(_phase, speedShare, isOnLand);
        }
        else if (_wasMoving)
        {
            _parts.RestLimbs();                      // back to the sculpted pose once the motion is switched off
        }
        _wasMoving = IsMoving;

        // The mouth breathes: the opening widens and narrows a little
        float gape = MouthShapes.For(design)?.Gape ?? -1f;
        if (gape > BREATH_MIN_GAPE)
        {
            SetClay("mouth_gape", gape * (1f + (design.IsBreathing ? BREATH_SHARE * Mathf.Sin(_time * BREATH_RATE) : 0f)));
        }

        // ---------------------------------------------------------------------------
        // Posture
        //   The whole body pitches around its middle: upright jellyfish, level fish.
        // ---------------------------------------------------------------------------

        // The posture belongs to the animal, in the water and on land alike (decision of the user 2026-11-15:
        // the upright jellyfish fell over on land because its threads counted as legs)
        // Body language (CellBody.Strike: rearing up, bowing, ducking) rides on top of the posture
        float postureGoal = (ShowsPosture ? design.Pitch : 0f) + (Body?.Pose ?? 0f);
        _posture = Mathf.Lerp(_posture, postureGoal, Mathf.Clamp(dt * POSTURE_EASE, 0f, 1f));
        var pitched = new Basis(Vector3.Back, _posture);
        _parts.Basis = pitched;
        if (null != _bodyMesh && IsInstanceValid(_bodyMesh))
        {
            _bodyMesh.Basis = pitched;
        }

        // ---------------------------------------------------------------------------
        // Details
        //   Eye blink and the blowhole spout.
        // ---------------------------------------------------------------------------

        // Eyes blink every few seconds
        float seed       = null != Body ? Body.AnimSeed : Seed;
        bool  isBlinking = (_time + seed) % 3.7f < 0.12f;
        _parts.Blink(isBlinking);

        // Spout: three droplets rise from the blowhole and fade
        float spout = ((_time + seed) % SPOUT_PERIOD) / SPOUT_SECONDS;
        _parts.Spout(spout);
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
    /// Sideways (fish) or vertical (marine mammal) offset of the spine at an X (SwimWave.Bend).
    /// </summary>
    /// <param name="x">(Required) Spine X in unit space.</param>
    /// <param name="halfLength">(Required) CreatureDesign.HalfLength.</param>
    /// <param name="amplitude">(Required) Offset at the tail.</param>
    /// <param name="phase">(Required) Wave phase.</param>
    public static float Bend(float x, float halfLength, float amplitude, float phase)
    {
        return SwimWave.Bend(x, halfLength, amplitude, phase);
    }

    // ===========================================================================
    // ===========================   BUILD CREATURE   ============================
    // ===========================================================================
    //
    //   FitCameraToParts   Frames FitCamera on the built meshes (palette tiles).
    //   Rebuild            Clears everything, builds body (unless IsPartsOnly) and the posed parts.
    //   BuildBodyMesh      The lofted clay body: elliptic rings along the spine.
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

    private ShaderMaterial _limbClay;
    private ShaderMaterial _knuckleClay;
    private CreatureParts3D _parts;                  // a new component per rebuild, it frees with the other children
    private MeshInstance3D _bodyMesh;                // null for parts only

    /// <summary>
    /// The lowest point of the built body in unit space, standing in the posture of its design
    /// (also while ShowsPosture is off): what touches the ground on land and the turntable of the editor.
    /// </summary>
    public float Lowest { get; private set; }

    /// <summary>Measures Lowest from the bounds of every built mesh (rest pose), with the body turned into its posture for the moment.</summary>
    private void MeasureLowest(CreatureDesign design)
    {
        var posture = new Basis(Vector3.Back, design.Pitch);
        var shown   = new Basis(Vector3.Back, _posture);
        _parts.Basis = posture;
        if (null != _bodyMesh)
        {
            _bodyMesh.Basis = posture;
        }
        float lowest = float.PositiveInfinity;
        foreach (MeshInstance3D mesh in FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            Transform3D toVisual = Transform3D.Identity;
            for (Node node = mesh; node != this && node is Node3D node3D; node = node.GetParent())
            {
                toVisual = node3D.Transform * toVisual;
            }
            lowest = Mathf.Min(lowest, (toVisual * mesh.GetAabb()).Position.Y);
        }
        Lowest       = float.IsPositiveInfinity(lowest) ? 0f : lowest;
        _parts.Basis = shown;
        if (null != _bodyMesh)
        {
            _bodyMesh.Basis = shown;
        }
    }
    private float _posture;                          // the pitch shown, eases towards the posture of the design

    private void Rebuild(CreatureDesign design)
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        // Body
        Color color = CurrentColor;
        _clay      ??= new ShaderMaterial();
        _clay.Shader = color.A < 0.99f ? TRANSLUCENT_SHADER : CLAY_SHADER;
        SetClay("pattern", (int)design.Pattern);
        SetClay("pattern_color", design.PatternColor ?? color.Darkened(0.45f));
        SetClay("skin", (int)design.Skin);
        SetClay("pattern_scale", design.PatternScale);
        SetClay("pattern_wrap", design.PatternWraps ? 1f : 0f);
        // Color zones: alpha 0 switches a zone off
        var none = new Color(0f, 0f, 0f, 0f);
        SetClay("belly_color", design.BellyColor ?? none);
        SetClay("head_color", design.ZoneColor(CreatureZone.Head) ?? none);
        SetClay("tail_color", design.ZoneColor(CreatureZone.Tail) ?? none);
        SetClay("head_from", design.XAt(CreatureDesign.HEAD_SHARE));
        SetClay("tail_to", design.XAt(CreatureDesign.TAIL_SHARE));
        SetClay("clay_color", color);
        SetClay("seed", null != Body ? Body.AnimSeed : Seed);
        SetClay("knead", Knead);
        SetClay("swim_vertical", CreatureType.MarineMammal == design.Type ? 1f : 0f);
        SetClay("half_length", Mathf.Max(design.HalfLength, 0.1f));
        SetClay("mouth_gape", MouthShapes.For(design)?.Gape ?? -1f);
        SetClay("mouth_min_gape", MouthShape.MIN_GAPE);
        if (!IsPartsOnly)
        {
            _bodyMesh = CellVisual3D.AddMesh(this, BuildBodyMesh(design), _clay, Vector3.Zero, Vector3.One);
        }
        else
        {
            _bodyMesh = null;
        }

        // Clay of sculpted limbs: same look, no swim wave of its own (the limb rides its pivot)
        _limbClay = (ShaderMaterial)_clay.Duplicate();
        _limbClay.SetShaderParameter("swim_amp", 0f);
        _limbClay.SetShaderParameter("splat_count", 0);  // spray paint lives on the body only
        _knuckleClay = (ShaderMaterial)_limbClay.Duplicate();
        _knuckleClay.SetShaderParameter("pattern", 0);   // the paint would shrink to specks on the small knuckles

        // Parts, each posed by side, size, shift and turn (Visual/CreatureParts3D.cs, chapter BUILD PARTS)
        _parts = new CreatureParts3D();
        AddChild(_parts);
        _parts.Build(design, color, _limbClay, _knuckleClay);
        MeasureLowest(design);
        Log.Verbose("CreatureVisual3D", $"Rebuilt {design.Type} with {design.Girths.Count} vertebrae and {design.Parts.Count} part(s).");
    }

    /// <summary>The lofted clay body: one elliptic ring per step along the spine (CreatureClay.BuildBodyMesh).</summary>
    /// <param name="design">(Required) The design with at least MIN_VERTEBRAE girths.</param>
    /// <returns>The body mesh in unit space.</returns>
    public static ArrayMesh BuildBodyMesh(CreatureDesign design)
    {
        return CreatureClay.BuildBodyMesh(design);
    }

    // ===========================================================================
    // =============================   PICK PARTS   ==============================
    // ===========================================================================
    //
    //   PartAt             The placed part under a ray, for picking in the editor.
    //   RayEntry           Where a ray enters a box (slab test), null on a miss.
    //   TryGetPartAnchor   Where the gizmo of a part sits.
    //   TryGetPartFrame    The frame its pose is stored in.
    //   TryGetLimbFrame    Where a sculpted limb sits, for the joint handles of the editor.
    //

    /// <summary>
    /// The placed part whose mesh bounds a ray hits first. Bounding boxes, not triangles:
    /// coarse, but enough to pick fins, eyes and limbs on the turntable.
    /// </summary>
    /// <param name="from">(Required) Ray origin in global space.</param>
    /// <param name="direction">(Required) Ray direction in global space.</param>
    /// <param name="flank">The flank of the hit piece, 0 for a midline part.</param>
    /// <returns>Index in Design.Parts, -1 when the ray hits no part (or nothing is built yet).</returns>
    public int PartAt(Vector3 from, Vector3 direction, out int flank)
    {
        flank = 0;
        return null == _parts ? -1 : _parts.PartAt(from, direction, out flank);
    }

    /// <summary>Where a ray enters a box, null on a miss (RayPick.RayEntry).</summary>
    /// <returns>Ray length to the entry point (0 inside the box), null on a miss.</returns>
    public static float? RayEntry(Aabb box, Vector3 from, Vector3 direction)
    {
        return RayPick.RayEntry(box, from, direction);
    }

    /// <summary>Where the gizmo of a part sits: its shifted anchor (rest pose, without the swim wave).</summary>
    /// <param name="index">(Required) Index in Design.Parts.</param>
    /// <param name="flank">(Required) -1 or 1 for one half of a pair, 0 for a midline part.</param>
    /// <param name="anchor">The anchor in global space.</param>
    /// <returns>False when the part has no group on that flank (not built yet).</returns>
    public bool TryGetPartAnchor(int index, int flank, out Vector3 anchor)
    {
        anchor = Vector3.Zero;
        return null != _parts && _parts.TryGetPartAnchor(index, flank, out anchor);
    }

    /// <summary>The channels of the model animation of a part: one name per bone or node its loop moves (CreatureParts3D.ChannelsOf).</summary>
    /// <param name="index">(Required) Index in Design.Parts.</param>
    public System.Collections.Generic.IReadOnlyList<string> ChannelsOf(int index)
    {
        return null != _parts ? _parts.ChannelsOf(index) : System.Array.Empty<string>();
    }

    /// <summary>The frame the pose of a part is stored in, in global space: a world axis times its inverse is the axis of the stored offset, turn and stretch.</summary>
    /// <param name="index">(Required) Index in Design.Parts.</param>
    /// <param name="flank">(Required) -1 or 1 for one half of a pair, 0 for a midline part.</param>
    /// <returns>False when the part has no group on that flank (not built yet).</returns>
    public bool TryGetPartFrame(int index, int flank, out Basis frame)
    {
        frame = Basis.Identity;
        return null != _parts && _parts.TryGetPartFrame(index, flank, out frame);
    }

    /// <summary>The posed shoulder of a sculpted limb on one flank: joints in limb space times this frame are world points.</summary>
    /// <param name="index">(Required) Index in Design.Parts, a Limb.</param>
    /// <param name="flank">(Required) -1 or 1.</param>
    /// <param name="frame">The global transform of the limb piece (pose, size and mirror included).</param>
    /// <returns>False when the limb has no piece on that flank.</returns>
    public bool TryGetLimbFrame(int index, int flank, out Transform3D frame)
    {
        frame = Transform3D.Identity;
        return null != _parts && _parts.TryGetLimbFrame(index, flank, out frame);
    }
}
