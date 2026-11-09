// NOTE:
//   3D rendering of an organism (stage 4): a clay body lofted along the spine of its
//   CreatureDesign (shaders/clay.gdshader) plus fins, eyes, jaw, spines, blowhole,
//   legs and the nostrils of a lung.
//   Built in unit space (+X forward, contact radius 1) and scaled to Body.Radius. The
//   body swims with a wave that grows towards the tail, side to side for fish, up and
//   down for marine mammals; the parts ride the same wave (Bend). On land (Body.IsOnLand)
//   the wave calms down, legs lift the body and walk, without legs it crawls on its belly.
//   Two modes:
//     - In the world: created by CellBody.ShowCreature, follows Body like CellVisual3D.
//     - Standalone (editor preview, icons): Body stays null, Design and BodyColor are set.
//
//   var preview = new CreatureVisual3D { Design = draft, BodyColor = color, Knead = 0.8f };
//   viewport.AddChild(preview);

//
//   Chapters:
//     SETTINGS          Inputs, shared meshes and materials.
//     FOLLOW AND SWIM   Rebuild on change, follow the body, swim wave, blink and spout.
//     BUILD CREATURE    Lofted body mesh and every part along the spine.
//

using System.Collections.Generic;
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
    //   Materials   Fin material per color, cached.
    //

    // ---------------------------------------------------------------------------
    // Inputs
    //   Body or Design and BodyColor, clay look and swimming.
    // ---------------------------------------------------------------------------

    public const float LIVING_KNEAD = 0.2f;          // the clay look left on a living creature
    public const float EDITOR_KNEAD = 0.75f;         // fresh clay in the creature editor
    private const int RINGS = 56;
    private const int SEGMENTS = 28;
    private const float IDLE_AMP = 0.05f;            // swim wave at rest, in unit space
    private const float SWIM_AMP = 0.13f;            // added at full speed
    private const float IDLE_FREQUENCY = 2.5f;       // wave phase per second at rest
    private const float SWIM_FREQUENCY = 9f;         // added at full speed
    private const float SPOUT_PERIOD = 5f;           // a marine mammal breathes every few seconds
    private const float SPOUT_SECONDS = 0.9f;
    private const float LAND_LIFT = 0.12f;           // legs lift the body this far above its swim height on land
    private const float LAND_WAVE = 0.25f;           // share of the swim wave left on land
    private const float WALK_SWING = 0.55f;          // leg swing in radians at full walking speed
    private const float LEG_SPRAWL = 0.7f;           // legs splay sideways like a salamander, visible from above
    private static readonly Shader CLAY_SHADER = GD.Load<Shader>("res://shaders/clay.gdshader");

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

    private CreatureDesign Current => null != Body ? Body.Creature : Design;
    private Color CurrentColor => null != Body ? Body.BodyColor : BodyColor;

    // ---------------------------------------------------------------------------
    // Meshes
    //   Fin outlines as flat meshes, cached once.
    // ---------------------------------------------------------------------------

    // Outlines around the base point (0, 0), ordered by angle so a triangle fan fills them
    private static readonly Vector2[] TAIL_OUTLINE =
    {
        new(-0.08f, 0.2f), new(-0.45f, 0.55f), new(-0.32f, 0.12f), new(-0.28f, 0f),
        new(-0.32f, -0.12f), new(-0.45f, -0.55f), new(-0.08f, -0.2f),
    };
    private static readonly Vector2[] DORSAL_OUTLINE =
    {
        new(0.2f, 0f), new(0.02f, 0.32f), new(-0.22f, 0.4f), new(-0.25f, 0.1f), new(-0.22f, 0f),
    };
    private static readonly Vector2[] PADDLE_OUTLINE =
    {
        new(0.12f, 0f), new(0.05f, 0.25f), new(-0.12f, 0.38f), new(-0.2f, 0.2f), new(-0.12f, 0f),
    };

    private static ArrayMesh _tailMesh;
    private static ArrayMesh _flukeMesh;
    private static ArrayMesh _dorsalMesh;
    private static ArrayMesh _paddleMesh;

    /// <summary>A flat two sided shape, vertical (XY plane) or horizontal (XZ plane).</summary>
    private static ArrayMesh FlatMesh(Vector2[] outline, bool isHorizontal)
    {
        Vector3 To3D(Vector2 point) => isHorizontal ? new Vector3(point.X, 0f, point.Y) : new Vector3(point.X, point.Y, 0f);

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetNormal(isHorizontal ? Vector3.Up : Vector3.Back);
        for (int i = 0; i < outline.Length - 1; i++)
        {
            surface.AddVertex(Vector3.Zero);
            surface.AddVertex(To3D(outline[i]));
            surface.AddVertex(To3D(outline[i + 1]));
        }
        return surface.Commit();
    }

    // ---------------------------------------------------------------------------
    // Materials
    //   Fin material per color, cached.
    // ---------------------------------------------------------------------------

    private static readonly Dictionary<Color, StandardMaterial3D> _finMaterials = new();

    /// <summary>Two sided, a bit lighter than the body, slightly see-through at the edges.</summary>
    private static StandardMaterial3D FinMaterial(Color bodyColor)
    {
        if (_finMaterials.TryGetValue(bodyColor, out StandardMaterial3D cached))
        {
            return cached;
        }
        var material = new StandardMaterial3D
        {
            AlbedoColor = bodyColor.Lightened(0.18f),
            Roughness   = 0.6f,
            CullMode    = BaseMaterial3D.CullModeEnum.Disabled,
            RimEnabled  = true,
            Rim         = 0.4f,
        };
        _finMaterials[bodyColor] = material;
        return material;
    }

    // ===========================================================================
    // ===========================   FOLLOW AND SWIM   ===========================
    // ===========================================================================
    //
    //   1. Rebuild     Rebuilds when design or color changed.
    //   2. Transform   Follows position, facing and size of the body, blinks when ghosted.
    //   3. Swim        Wave phase and amplitude from the speed, body shader and parts follow.
    //   4. Details     Legs, threat ring, eye blink and the blowhole spout.
    //

    private sealed record Attached(Node3D Pivot, float X);
    private sealed record Leg(Node3D Hip, float Side, int Pair);

    private readonly List<Attached> _attached = new();
    private readonly List<Node3D> _eyes = new();
    private readonly List<MeshInstance3D> _spouts = new();
    private readonly List<Leg> _legs = new();
    private float _walkPhase;
    private float _lift;
    private ShaderMaterial _clay;
    private MeshInstance3D _ring;
    private StandardMaterial3D _ringMaterial;
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

        int signature = System.HashCode.Combine(design.Version, design.Type, CurrentColor, Knead);
        if (signature != _signature || null == _clay)
        {
            _signature = signature;
            Rebuild(design);
        }

        // ---------------------------------------------------------------------------
        // Transform
        //   Follows position, facing and size of the body, blinks when ghosted.
        // ---------------------------------------------------------------------------

        float speedShare = 0f;
        bool  isOnLand   = null != Body && Body.IsOnLand;
        _lift = Mathf.Lerp(_lift, isOnLand && design.LegPairs > 0 ? LAND_LIFT : 0f, Mathf.Clamp(dt * 4f, 0f, 1f));
        if (null != Body)
        {
            GlobalPosition = Plane3D.To3D(Body.GlobalPosition, _lift * Body.Radius);
            Rotation       = new Vector3(0f, Plane3D.FacingToYaw(Body.Facing), 0f);
            Scale          = Vector3.One * Body.Radius;
            Visible        = !Body.IsHidden && !(Body.IsGhost && _time % 0.2f < 0.1f);
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
        _clay.SetShaderParameter("swim_phase", _phase);
        _clay.SetShaderParameter("swim_amp", amplitude);
        _clay.SetShaderParameter("flash", null != Body ? Body.HurtFlash : 0f);
        foreach (Attached part in _attached)
        {
            float bend  = Bend(part.X, design.HalfLength, amplitude, _phase);
            float slope = (Bend(part.X + 0.01f, design.HalfLength, amplitude, _phase) - Bend(part.X - 0.01f, design.HalfLength, amplitude, _phase)) / 0.02f;
            part.Pivot.Position = isVertical ? new Vector3(part.X, bend, 0f) : new Vector3(part.X, 0f, bend);
            part.Pivot.Rotation = isVertical ? new Vector3(0f, 0f, Mathf.Atan(slope)) : new Vector3(0f, -Mathf.Atan(slope), 0f);
        }

        // ---------------------------------------------------------------------------
        // Details
        //   Legs, threat ring, eye blink and the blowhole spout.
        // ---------------------------------------------------------------------------

        // Legs: on land they walk in a diagonal gait, in the water they paddle gently
        _walkPhase += dt * (isOnLand ? 2f + 10f * speedShare : 1.5f);
        foreach (Leg leg in _legs)
        {
            float phase = _walkPhase + leg.Side * Mathf.Pi * 0.5f + leg.Pair * Mathf.Pi;
            float swing = isOnLand ? WALK_SWING * Mathf.Clamp(speedShare * 2f, 0.15f, 1f) : 0.25f;
            leg.Hip.Rotation = new Vector3(-leg.Side * LEG_SPRAWL, 0f, Mathf.Sin(phase) * swing);
        }

        // Threat ring: red = dangerous, green = edible
        if (null != _ring)
        {
            _ring.Visible = 0 != Body.Threat;
            if (_ring.Visible)
            {
                _ringMaterial.AlbedoColor = Body.Threat > 0 ? new Color(1f, 0.35f, 0.3f, 0.55f) : new Color(0.5f, 1f, 0.6f, 0.45f);
            }
        }

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
    //   Rebuild         Clears everything, builds body, parts and (in the world) the threat ring.
    //   BuildBodyMesh   The lofted clay body: elliptic rings along the spine.
    //   BuildPart       One placed part on a pivot that rides the swim wave.
    //   BuildEye        One eye on the flank, pupil forward and outwards.
    //   AddPivot        A pivot at a spine X, moved by the swim wave.
    //

    private void Rebuild(CreatureDesign design)
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        _attached.Clear();
        _eyes.Clear();
        _spouts.Clear();
        _legs.Clear();

        // Body
        Color color = CurrentColor;
        _clay ??= new ShaderMaterial { Shader = CLAY_SHADER };
        _clay.SetShaderParameter("clay_color", color);
        _clay.SetShaderParameter("seed", null != Body ? Body.AnimSeed : Seed);
        _clay.SetShaderParameter("knead", Knead);
        _clay.SetShaderParameter("swim_vertical", CreatureType.MarineMammal == design.Type ? 1f : 0f);
        _clay.SetShaderParameter("half_length", Mathf.Max(design.HalfLength, 0.1f));
        CellVisual3D.AddMesh(this, BuildBodyMesh(design), _clay, Vector3.Zero, Vector3.One);

        // Parts
        foreach (CreaturePart part in design.Parts)
        {
            BuildPart(design, part, color);
        }

        // Threat ring around the contact circle, only in the world
        _ring = null;
        if (null != Body)
        {
            _ringMaterial = new StandardMaterial3D
            {
                ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            };
            _ring = CellVisual3D.AddMesh(this, CellVisual3D.TorusMesh(), _ringMaterial, Vector3.Zero, new Vector3(1.3f, 0.4f, 1.3f));
            _ring.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
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
            for (int j = 0; j < SEGMENTS; j++)
            {
                float angle = Mathf.Tau * j / SEGMENTS;
                surface.AddVertex(new Vector3(x, Mathf.Sin(angle) * radius * design.HeightScale, Mathf.Cos(angle) * radius * design.WidthScale));
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
        StandardMaterial3D fin     = FinMaterial(color);
        Color              outline = color.Darkened(0.5f);
        float              x       = design.XAt(part.T);
        float              radius  = design.RadiusAt(x);
        float              top     = design.TopAt(x);
        float              flank   = design.FlankAt(x);
        float              finSize = 0.6f + design.MeanGirth * 0.5f;

        switch (part.Kind)
        {
            // Tail: a forked vertical fin (fish) or horizontal flukes (marine mammal) behind the tail tip
            case CreaturePartKind.CaudalFin:
                _tailMesh ??= FlatMesh(TAIL_OUTLINE, isHorizontal: false);
                CellVisual3D.AddMesh(AddPivot(design.TailTipX + 0.06f), _tailMesh, fin, Vector3.Zero, Vector3.One * finSize);
                break;
            case CreaturePartKind.Fluke:
                _flukeMesh ??= FlatMesh(TAIL_OUTLINE, isHorizontal: true);
                CellVisual3D.AddMesh(AddPivot(design.TailTipX + 0.06f), _flukeMesh, fin, Vector3.Zero, Vector3.One * finSize);
                break;

            // Back: a swept sail, a row of spines or the blowhole with its spout
            case CreaturePartKind.DorsalFin:
                _dorsalMesh ??= FlatMesh(DORSAL_OUTLINE, isHorizontal: false);
                CellVisual3D.AddMesh(AddPivot(x), _dorsalMesh, fin, new Vector3(0f, top - 0.03f, 0f), Vector3.One * (0.5f + radius * 0.6f));
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
                CellVisual3D.AddMesh(hole, CellVisual3D.SphereMesh(true), CellVisual3D.Material(outline), new Vector3(0f, top - 0.005f, 0f), new Vector3(0.05f, 0.02f, 0.035f));
                var spout = new Node3D { Position = new Vector3(0f, top, 0f) };
                hole.AddChild(spout);
                for (int i = 0; i < 3; i++)
                {
                    MeshInstance3D drop = CellVisual3D.AddMesh(spout, CellVisual3D.SphereMesh(true), CellVisual3D.Material(new Color(0.85f, 0.95f, 1f, 0.75f), 0.4f, 0.1f), Vector3.Zero, Vector3.One * 0.05f);
                    drop.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                    _spouts.Add(drop);
                }
                break;

            // Flanks: a pair of paddles tilted down, or a pair of eyes
            case CreaturePartKind.PectoralFin:
                _paddleMesh ??= FlatMesh(PADDLE_OUTLINE, isHorizontal: true);
                Node3D fins = AddPivot(x);
                foreach (float side in new[] { -1f, 1f })
                {
                    MeshInstance3D paddle = CellVisual3D.AddMesh(fins, _paddleMesh, fin, new Vector3(0f, -top * 0.3f, side * flank * 0.85f), Vector3.One);
                    paddle.Basis = new Basis(Vector3.Right, side * 0.45f) * Basis.FromScale(new Vector3(1f, 1f, side) * (0.55f + radius * 0.6f));
                }
                break;
            case CreaturePartKind.Eye:
                Node3D eyes = AddPivot(x);
                foreach (float side in new[] { -1f, 1f })
                {
                    BuildEye(eyes, new Vector3(0f, top * 0.3f, side * flank * 0.88f), side, 0.08f + radius * 0.12f);
                }
                break;

            // Flanks: a pair of splayed legs, long enough to reach the ground of the coast with the body lifted
            case CreaturePartKind.Legs:
                Node3D hips   = AddPivot(x);
                float  hipY   = -top * 0.45f;
                float  length = Mathf.Max(0.18f, -Coast.GROUND_HEIGHT + LAND_LIFT + hipY) / Mathf.Cos(LEG_SPRAWL);
                int    pair   = _legs.Count / 2;
                foreach (float side in new[] { -1f, 1f })
                {
                    var hip = new Node3D { Position = new Vector3(0f, hipY, side * flank * 0.75f) };
                    hips.AddChild(hip);
                    StandardMaterial3D skin = CellVisual3D.Material(color.Darkened(0.15f), 0f, 0.7f);
                    CellVisual3D.AddMesh(hip, CellVisual3D.RodMesh(), skin, new Vector3(0f, -length * 0.25f, 0f), new Vector3(0.055f, length * 0.5f, 0.055f));
                    CellVisual3D.AddMesh(hip, CellVisual3D.RodMesh(), skin, new Vector3(0.03f, -length * 0.75f, 0f), new Vector3(0.045f, length * 0.5f, 0.045f));
                    CellVisual3D.AddMesh(hip, CellVisual3D.SphereMesh(true), CellVisual3D.Material(color.Darkened(0.35f), 0f, 0.8f),
                        new Vector3(0.06f, -length, 0f), new Vector3(0.1f, 0.04f, 0.07f));
                    _legs.Add(new Leg(hip, side, pair));
                }
                break;

            // Lung: two nostrils on top of the nose, wherever it was placed inside
            case CreaturePartKind.Lung:
                float  nostrilX = design.HalfLength + (design.NoseTipX - design.HalfLength) * 0.45f;
                Node3D nose     = AddPivot(nostrilX);
                foreach (float side in new[] { -1f, 1f })
                {
                    CellVisual3D.AddMesh(nose, CellVisual3D.SphereMesh(true), CellVisual3D.Material(outline),
                        new Vector3(0f, design.TopAt(nostrilX) * 0.92f, side * 0.05f), new Vector3(0.03f, 0.015f, 0.022f));
                }
                break;

            // Nose: a dark mouth slit with a row of teeth
            case CreaturePartKind.Jaw:
                float  noseX = design.NoseTipX;
                float  mouthX = noseX - Mathf.Max(0.1f, (noseX - design.HalfLength) * 0.45f);
                Node3D jaw   = AddPivot(mouthX);
                float  mouthTop   = design.TopAt(mouthX);
                float  mouthFlank = design.FlankAt(mouthX);
                CellVisual3D.AddMesh(jaw, CellVisual3D.SphereMesh(true), CellVisual3D.Material(new Color(0.25f, 0.06f, 0.08f)),
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

    /// <param name="side">(Required) -1 left flank, 1 right flank, the pupil looks outwards there.</param>
    private void BuildEye(Node3D pivot, Vector3 position, float side, float size)
    {
        var eye = new Node3D { Position = position, Scale = Vector3.One };
        pivot.AddChild(eye);
        CellVisual3D.AddMesh(eye, CellVisual3D.SphereMesh(), CellVisual3D.Material(new Color(0.97f, 0.97f, 0.97f), 0.25f, 0.2f), Vector3.Zero, Vector3.One * size);
        CellVisual3D.AddMesh(eye, CellVisual3D.SphereMesh(true), CellVisual3D.Material(new Color(0.06f, 0.06f, 0.1f), 0f, 0.1f),
            new Vector3(size * 0.35f, size * 0.15f, side * size * 0.6f), Vector3.One * size * 0.55f);
        CellVisual3D.AddMesh(eye, CellVisual3D.SphereMesh(true), CellVisual3D.Material(Colors.White, 1.5f),
            new Vector3(size * 0.5f, size * 0.55f, side * size * 0.75f), Vector3.One * size * 0.18f);
        _eyes.Add(eye);
    }

    private Node3D AddPivot(float x)
    {
        var pivot = new Node3D { Position = new Vector3(x, 0f, 0f) };
        AddChild(pivot);
        _attached.Add(new Attached(pivot, x));
        return pivot;
    }
}
