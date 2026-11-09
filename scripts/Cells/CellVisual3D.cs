// NOTE:
//   3D rendering of a CellBody, built procedurally from primitive meshes and
//   shaders/membrane.gdshader. Built in unit size (cell radius 1, +X forward, +Y up)
//   and scaled to Body.Radius. Every cell of a cluster is a full cell with the placed
//   parts. A change of the traits rebuilds the whole visual, a division animates the
//   daughters out of their mother cells.
//   Created by CellBody._Ready, nothing else has to set it up.

//
//   Chapters:
//     SHARED RESOURCES   Meshes and materials shared by every cell, food and effect.
//     FOLLOW THE BODY    Rebuild on change, follow position, facing and size, animate.
//     BUILD CELLS        Membrane, inner life, role extras and appendages per cell.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class CellVisual3D : Node3D
{
    public const float FLATTEN = 0.6f;               // cells are flattened spheres, also used by the editor raycast
    private const int FLAGELLUM_BEADS = 16;
    private const int CILIA_COUNT = 22;
    private const int FULL_PARTS_LIMIT = 4;          // up to 4 cells every cell shows every part
    private const int FULL_DETAIL_LIMIT = 32;        // above 32 cells inner cells drop face, organelles and shadow
    private const float OUTER_BAND = 1.6f;           // cells this close to the cluster edge (cell radii) count as outer
    private const float RIM_PART_ARC = 1.1f;         // a rim part shows on outer cells facing its direction within this angle
    private const int DNA_PAIRS = 14;                // base pairs of the protocell's DNA helix
    private const float DNA_LENGTH = 1.05f;
    private const float DNA_RADIUS = 0.15f;
    private const float DNA_TURNS = 1.75f;
    private static readonly Color DNA_BACKBONE = new(0.95f, 0.55f, 0.75f);
    private static readonly Color[] DNA_BASES =
    {
        new(0.98f, 0.85f, 0.35f),   // A
        new(0.45f, 0.8f, 1f),       // T
        new(0.55f, 0.95f, 0.55f),   // G
        new(1f, 0.6f, 0.35f),       // C
    };
    private static readonly Shader MEMBRANE_SHADER = GD.Load<Shader>("res://shaders/membrane.gdshader");

    /// <summary>(Required) The body this visual renders, set before AddChild.</summary>
    public CellBody Body { get; set; }

    // ===========================================================================
    // ==========================   SHARED RESOURCES   ===========================
    // ===========================================================================
    //
    //   SphereMesh   Unit sphere, smooth or low poly.
    //   TorusMesh    Flat ring with outer radius 1.
    //   ConeMesh     Spike cone, tip at +Y.
    //   RodMesh      Unit cylinder along Y, scaled per use.
    //   Material     Cached standard material per color, glow and roughness.
    //   AddMesh      Adds a mesh instance with material, position and scale.
    //

    private static SphereMesh _sphere;
    private static SphereMesh _sphereLowPoly;
    private static TorusMesh _torus;
    private static CylinderMesh _cone;
    private static CylinderMesh _rod;
    private static readonly Dictionary<string, StandardMaterial3D> _materials = new();

    /// <summary>The shared unit sphere (radius 1).</summary>
    /// <param name="isLowPoly">(Optional) Fewer segments for small parts. Defaults to false.</param>
    public static SphereMesh SphereMesh(bool isLowPoly = false)
    {
        if (null == _sphere)
        {
            _sphere        = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 64, Rings = 32 };
            _sphereLowPoly = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 10, Rings = 6 };
        }
        return isLowPoly ? _sphereLowPoly : _sphere;
    }

    /// <summary>The shared flat ring, outer radius 1.</summary>
    public static TorusMesh TorusMesh()
    {
        _torus ??= new TorusMesh { InnerRadius = 0.86f, OuterRadius = 1f, Rings = 32, RingSegments = 8 };
        return _torus;
    }

    /// <summary>The shared spike cone, base at -Y, tip at +Y, height 0.6.</summary>
    public static CylinderMesh ConeMesh()
    {
        _cone ??= new CylinderMesh { TopRadius = 0f, BottomRadius = 0.13f, Height = 0.6f, RadialSegments = 8, Rings = 1 };
        return _cone;
    }

    /// <summary>The shared unit cylinder along Y (radius 1, height 1).</summary>
    public static CylinderMesh RodMesh()
    {
        _rod ??= new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 1f, RadialSegments = 6, Rings = 1 };
        return _rod;
    }

    /// <summary>A cached standard material, transparent when the color has alpha.</summary>
    /// <param name="color">(Required) Albedo color.</param>
    /// <param name="glow">(Optional) Emission energy in the albedo color, 0 for none. Defaults to 0.</param>
    /// <param name="roughness">(Optional) Surface roughness. Defaults to 0.45.</param>
    /// <example><code>CellVisual3D.Material(Colors.White, 0.25f, 0.2f)</code></example>
    public static StandardMaterial3D Material(Color color, float glow = 0f, float roughness = 0.45f)
    {
        string key = $"{color.ToHtml()}|{glow:F2}|{roughness:F2}";
        if (_materials.TryGetValue(key, out StandardMaterial3D cached))
        {
            return cached;
        }

        var material = new StandardMaterial3D { AlbedoColor = color, Roughness = roughness };
        if (color.A < 0.99f)
        {
            material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        }
        if (glow > 0f)
        {
            material.EmissionEnabled          = true;
            material.Emission                 = color;
            material.EmissionEnergyMultiplier = glow;
        }
        _materials[key] = material;
        return material;
    }

    /// <summary>Adds a mesh instance below parent.</summary>
    /// <param name="parent">(Required) The node the mesh is added to.</param>
    /// <param name="mesh">(Required) The mesh, usually a shared one from this chapter.</param>
    /// <param name="material">(Required) The material override.</param>
    /// <param name="position">(Required) Local position.</param>
    /// <param name="scale">(Required) Local scale.</param>
    /// <returns>The new mesh instance.</returns>
    public static MeshInstance3D AddMesh(Node3D parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var instance = new MeshInstance3D
        {
            Mesh             = mesh,
            MaterialOverride = material,
            Position         = position,
            Scale            = scale,
        };
        parent.AddChild(instance);
        return instance;
    }

    // ===========================================================================
    // ===========================   FOLLOW THE BODY   ===========================
    // ===========================================================================
    //
    //   1. Rebuild    Rebuilds the visual when the traits of the body changed.
    //   2. Transform  Follows position, facing and size of the body, blinks when ghosted,
    //                 moves divided cells out of their mothers.
    //   3. Animate    Membrane stretch and flash, threat ring, flagella, cilia, eyes and DNA.
    //

    private sealed record Flagellum(Vector3 Direction, float Start, List<MeshInstance3D> Beads);
    private sealed record SplitCell(Node3D Root, Vector3 From, Vector3 To);

    private const float SPLIT_SECONDS = 0.7f;

    private int _signature;
    private float _time;
    private readonly List<ShaderMaterial> _membranes = new();
    private readonly List<Flagellum> _flagella = new();
    private readonly List<(Node3D Pivot, float Angle)> _cilia = new();
    private readonly List<Node3D> _eyes = new();
    private readonly List<Node3D> _spinners = new();
    private Node3D _dna;
    private readonly List<SplitCell> _splitting = new();
    private float _splitTime = SPLIT_SECONDS;
    private int _builtCellCount;
    private MeshInstance3D _ring;
    private StandardMaterial3D _ringMaterial;

    public override void _Process(double delta)
    {
        if (null == Body)
        {
            return;
        }
        _time += (float)delta;

        // An organism is drawn by its CreatureVisual3D
        if (null != Body.Creature)
        {
            Visible = false;
            return;
        }

        // ---------------------------------------------------------------------------
        // Rebuild
        //   Rebuilds the visual when the traits of the body changed.
        // ---------------------------------------------------------------------------

        int signature = ComputeSignature();
        if (signature != _signature)
        {
            _signature = signature;
            Rebuild();
        }

        // ---------------------------------------------------------------------------
        // Transform
        //   Follows position, facing and size of the body, blinks when ghosted,
        //   moves divided cells out of their mothers.
        // ---------------------------------------------------------------------------

        GlobalPosition = Plane3D.To3D(Body.GlobalPosition);
        Rotation       = new Vector3(0f, Plane3D.FacingToYaw(Body.Facing), 0f);
        Scale          = Vector3.One * Body.Radius;
        Visible        = !Body.IsHidden && !(Body.IsGhost && _time % 0.2f < 0.1f);

        // Divided cells glide out of their mothers with a little overshoot
        if (_splitTime < SPLIT_SECONDS)
        {
            _splitTime += (float)delta;
            float t    = Mathf.Clamp(_splitTime / SPLIT_SECONDS, 0f, 1f);
            float u    = t - 1f;
            float ease = 1f + 2.70158f * u * u * u + 1.70158f * u * u;
            foreach (SplitCell cell in _splitting)
            {
                cell.Root.Position = cell.From.Lerp(cell.To, ease);
            }
        }

        // ---------------------------------------------------------------------------
        // Animate
        //   Membrane stretch and flash, threat ring, flagella, cilia, eyes and DNA.
        // ---------------------------------------------------------------------------

        // Squash and stretch along the movement
        float stretch = Mathf.Clamp(Body.Velocity.Length() / (Body.Radius * 30f), 0f, 0.2f);
        foreach (ShaderMaterial membrane in _membranes)
        {
            membrane.SetShaderParameter("stretch", stretch);
            membrane.SetShaderParameter("flash", Body.HurtFlash);
        }

        // Threat ring: red = dangerous, green = edible
        _ring.Visible = 0 != Body.Threat;
        if (_ring.Visible)
        {
            _ringMaterial.AlbedoColor = Body.Threat > 0 ? new Color(1f, 0.35f, 0.3f, 0.55f) : new Color(0.5f, 1f, 0.6f, 0.45f);
        }

        AnimateParts();
    }

    /// <summary>Hash over everything that changes the built meshes.</summary>
    private int ComputeSignature()
    {
        var hash = new System.HashCode();
        hash.Add(Body.IsProtocell);
        hash.Add(Body.Flagella);
        hash.Add(Body.HasCilia);
        hash.Add(Body.HasSpikes);
        hash.Add(Body.HasChloroplasts);
        hash.Add(Body.EyeCount);
        hash.Add(Body.IsPartsOnly);
        hash.Add(Body.CellCount);
        hash.Add(Body.LookVersion);
        hash.Add(Body.BodyColor);
        hash.Add(Body.Design.Version);
        return hash.ToHashCode();
    }

    /// <summary>Moves the animated parts built in BUILD CELLS.</summary>
    private void AnimateParts()
    {
        // Flagella: a sine wave that runs faster with the speed
        float frequency = 8f + Body.Velocity.Length() / Mathf.Max(Body.Radius, 1f) * 0.6f;
        foreach (Flagellum flagellum in _flagella)
        {
            Vector3 dir  = flagellum.Direction;
            var     perp = new Vector3(-dir.Z, 0f, dir.X);
            for (int i = 0; i < flagellum.Beads.Count; i++)
            {
                float t = (float)i / (flagellum.Beads.Count - 1);
                flagellum.Beads[i].Position = dir * (flagellum.Start + 1.8f * t)
                    + perp * Mathf.Sin(t * 7f - _time * frequency) * 0.22f * t
                    + Vector3.Up * Mathf.Sin(t * 5f - _time * frequency * 0.7f) * 0.08f * t;
            }
        }

        // Cilia wave around their base angle
        for (int i = 0; i < _cilia.Count; i++)
        {
            _cilia[i].Pivot.Rotation = new Vector3(0f, -_cilia[i].Angle + Mathf.Sin(_time * 10f + i * 0.9f) * 0.5f, 0f);
        }

        // Organelles drift slowly
        foreach (Node3D spinner in _spinners)
        {
            spinner.Rotation = new Vector3(0f, _time * 0.15f + Mathf.Sin(_time * 0.5f) * 0.25f, 0f);
        }

        // Eyes blink every few seconds
        bool isBlinking = (_time + Body.AnimSeed) % 3.7f < 0.12f;
        foreach (Node3D eye in _eyes)
        {
            eye.Scale = new Vector3(1f, isBlinking ? 0.15f : 1f, 1f);
        }

        // DNA of the protocell: drifts on a slow loop through the cell, tumbles and spins around its axis
        if (null != _dna)
        {
            float drift = _time * 0.35f + Body.AnimSeed;
            _dna.Position = new Vector3(Mathf.Sin(drift) * 0.22f, 0.04f + Mathf.Sin(drift * 1.7f) * 0.03f, Mathf.Sin(drift * 0.8f + 1f) * 0.2f);
            _dna.Basis    = new Basis(Vector3.Up, _time * 0.25f + Body.AnimSeed)
                * new Basis(Vector3.Back, Mathf.Sin(_time * 0.4f) * 0.35f)
                * new Basis(Vector3.Right, _time * 0.9f);
        }
    }

    // ===========================================================================
    // =============================   BUILD CELLS   =============================
    // ===========================================================================
    //
    //   Rebuild           Clears all parts, builds every cell of the cluster plus the threat ring,
    //                     on a division the daughters start at their mothers.
    //   BuildCell         Shadow proxy, membrane, inner life, appendages, face and parts of one cell.
    //   BuildInnerLife    Organelles, chloroplasts and the nucleus.
    //   BuildDna          The protocell's double helix with colored base pairs.
    //   BuildFlagellum    A chain of beads, animated in FOLLOW THE BODY.
    //   BuildCilia        Short rods along an arc of the rim.
    //   BuildSpikes       Cones pointing outwards.
    //   BuildFace         Eyes and a smile on the upper front.
    //   BuildEye          One eye, pupil looking forward and up.
    //   IsOuterCell       Whether a cell sits at the edge of its cluster.
    //   BuildPlacedParts  The placed parts on one cell, rim parts on the facing outer cells of big clusters.
    //   BuildPlaced       A part placed in the editor at its position.
    //   SurfaceY          Height of the flattened membrane at a unit point, outline included.
    //   ShapeAt           Radius of the stretched outline at an angle.
    //

    private void Rebuild()
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        _membranes.Clear();
        _flagella.Clear();
        _cilia.Clear();
        _eyes.Clear();
        _spinners.Clear();
        _dna = null;
        _splitting.Clear();

        // A division: each daughter starts at its mother, in the new (smaller) unit space
        bool      isDivision = _builtCellCount > 0 && Body.CellCount > _builtCellCount;
        Vector3[] mothers    = isDivision ? CellBody.ClusterLayout(_builtCellCount) : null;
        float     toNewUnits = isDivision ? CellBody.ClusterExtent(Body.CellCount) / CellBody.ClusterExtent(_builtCellCount) : 1f;
        foreach (CellPart part in Body.CellLayout())
        {
            Node3D root = BuildCell(part);
            if (isDivision)
            {
                Vector3 from = mothers[part.Index * _builtCellCount / Body.CellCount] * toNewUnits;
                _splitting.Add(new SplitCell(root, from, root.Position));
                root.Position = from;
            }
        }
        _splitTime      = isDivision ? 0f : SPLIT_SECONDS;
        _builtCellCount = Body.CellCount;

        _ringMaterial = new StandardMaterial3D
        {
            ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        float extent = CellBody.ClusterExtent(Body.CellCount);
        _ring = AddMesh(this, TorusMesh(), _ringMaterial, Vector3.Zero, new Vector3(1.3f * extent, 0.4f, 1.3f * extent));
        _ring.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        Log.Verbose("CellVisual3D", $"Rebuilt '{Body.Name}' with {Body.CellLayout().Count} cell(s).");
    }

    /// <returns>The root node of the cell, positioned at its place in the cluster.</returns>
    private Node3D BuildCell(CellPart part)
    {
        var root = new Node3D { Position = part.Position };
        AddChild(root);
        Vector3 center  = root.Position;
        Color   color   = Body.BodyColor;
        Color   outline = color.Darkened(0.45f);

        // Parts only (editor drag icon and placement ghost): no membrane, inner life or face
        if (Body.IsPartsOnly)
        {
            BuildPlacedParts(root, color, part);
            return root;
        }

        // Detail level: big clusters only dress the cells you can see, see IsOuterCell
        bool isOuter      = IsOuterCell(part);
        bool isFullDetail = Body.CellCount <= FULL_DETAIL_LIMIT || isOuter;

        // Shadow proxy, the transparent membrane casts none itself
        if (isFullDetail)
        {
            MeshInstance3D proxy = AddMesh(root, SphereMesh(true), Material(color), Vector3.Zero, new Vector3(0.9f, FLATTEN * 0.9f, 0.9f));
            proxy.CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly;
        }

        // Membrane
        Color membraneColor = color;
        if (Body.IsProtocell)
        {
            membraneColor.A = 0.8f;
        }
        var membraneMaterial = new ShaderMaterial { Shader = MEMBRANE_SHADER };
        membraneMaterial.SetShaderParameter("body_color", membraneColor);
        membraneMaterial.SetShaderParameter("seed", Body.AnimSeed + center.X * 1.3f + center.Z * 0.7f);
        membraneMaterial.SetShaderParameter("core_alpha", Body.IsProtocell ? 0.2f : 0.42f);
        membraneMaterial.SetShaderParameter("shape", System.Linq.Enumerable.ToArray(Body.Design.Shape));
        MeshInstance3D membrane = AddMesh(root, SphereMesh(), membraneMaterial, Vector3.Zero, new Vector3(1f, FLATTEN, 1f));
        membrane.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _membranes.Add(membraneMaterial);

        // Inner life: a floating DNA helix in the protocell, otherwise organelles and a nucleus
        if (Body.IsProtocell)
        {
            _dna = BuildDna(root);
        }
        else
        {
            BuildInnerLife(root, isFullDetail);
        }

        // Appendages of critters, face and the placed parts on every cell
        for (int f = 0; f < Body.Flagella; f++)
        {
            float offset = (f - (Body.Flagella - 1) * 0.5f) * 0.5f;
            BuildFlagellum(root, Vector3.Left.Rotated(Vector3.Up, offset), outline);
        }
        if (Body.HasCilia)
        {
            BuildCilia(root, 0f, Mathf.Tau, CILIA_COUNT, outline);
        }
        if (Body.HasSpikes)
        {
            BuildSpikes(root, Vector3.Right, 5, 0.38f, outline);
        }
        if (isFullDetail)
        {
            BuildFace(root, outline);
        }
        BuildPlacedParts(root, color, part);
        return root;
    }

    /// <summary>
    /// A cell near the edge of the cluster, within OUTER_BAND of it. Clusters up to
    /// FULL_PARTS_LIMIT cells have no inside, every cell counts as outer.
    /// </summary>
    private bool IsOuterCell(CellPart part)
    {
        if (Body.CellCount <= FULL_PARTS_LIMIT)
        {
            return true;
        }
        float fromCenter = new Vector2(part.Position.X, part.Position.Z).Length();
        return fromCenter >= CellBody.ClusterExtent(Body.CellCount) - 1f - OUTER_BAND;
    }

    /// <summary>
    /// The placed parts on one cell. Up to FULL_PARTS_LIMIT cells every cell carries all of
    /// them. In bigger clusters a rim part only sits on outer cells that face its direction
    /// (a spike in front grows on the front cells), inner parts follow the detail level.
    /// </summary>
    private void BuildPlacedParts(Node3D root, Color cellColor, CellPart cell)
    {
        bool  isOuter = IsOuterCell(cell);
        bool  isAll   = Body.CellCount <= FULL_PARTS_LIMIT;
        float outward = new Vector2(cell.Position.X, cell.Position.Z).Angle();
        foreach (PlacedPart placed in Body.Design.Parts)
        {
            bool isFacing = Mathf.Abs(Mathf.Wrap(placed.Angle - outward, -Mathf.Pi, Mathf.Pi)) <= RIM_PART_ARC;
            bool isShown  = Placement.Rim == placed.Info.Placement
                ? isAll || (isOuter && isFacing)
                : Body.CellCount <= FULL_DETAIL_LIMIT || isOuter;
            if (isShown)
            {
                BuildPlaced(root, placed, cellColor);
            }
        }
    }

    /// <param name="isFullDetail">(Required) False for hidden inner cells of big clusters: nucleus only.</param>
    private void BuildInnerLife(Node3D root, bool isFullDetail)
    {
        // Organelles
        if (isFullDetail)
        {
            var spinner = new Node3D();
            root.AddChild(spinner);
            _spinners.Add(spinner);
            foreach (Organelle organelle in Body.Organelles)
            {
                var position = new Vector3(organelle.Position.X, organelle.Y, organelle.Position.Y);
                AddMesh(spinner, SphereMesh(true), Material(organelle.Color, 0.35f), position, Vector3.One * organelle.Size);
            }
        }

        // Chloroplasts of photosynthesizing critters
        if (Body.HasChloroplasts)
        {
            var spinner = new Node3D();
            root.AddChild(spinner);
            _spinners.Add(spinner);
            for (int i = 0; i < 4; i++)
            {
                float angle = Mathf.Tau * i / 4f + 0.6f;
                MeshInstance3D chloroplast = AddMesh(spinner, SphereMesh(true), Material(new Color(0.25f, 0.7f, 0.3f), 0.3f),
                    new Vector3(Mathf.Cos(angle), 0.05f, Mathf.Sin(angle)) * 0.55f, new Vector3(0.16f, 0.08f, 0.1f));
                chloroplast.Rotation = new Vector3(0f, -angle, 0f);
            }
        }

        // Nucleus with nucleolus, slightly behind the center of the cell
        var nucleus = new Vector3(-0.15f, 0f, 0f);
        AddMesh(root, SphereMesh(), Material(Body.BodyColor.Darkened(0.25f), 0.15f), nucleus, new Vector3(0.3f, 0.24f, 0.3f));
        AddMesh(root, SphereMesh(true), Material(Body.BodyColor.Darkened(0.55f)), nucleus + new Vector3(0.06f, 0.1f, -0.05f), Vector3.One * 0.1f);
    }

    /// <summary>
    /// Double helix along local X: two backbones of beads, opposite each other, turning
    /// DNA_TURNS times over DNA_LENGTH. Each pair is joined by a rung of two colored
    /// halves (the bases). Animated as a whole in FOLLOW THE BODY.
    /// </summary>
    private static Node3D BuildDna(Node3D root)
    {
        var dna = new Node3D();
        root.AddChild(dna);
        StandardMaterial3D backbone = Material(DNA_BACKBONE, 0.5f, 0.3f);
        for (int i = 0; i < DNA_PAIRS; i++)
        {
            float t     = (float)i / (DNA_PAIRS - 1);
            float x     = (t - 0.5f) * DNA_LENGTH;
            float phase = t * DNA_TURNS * Mathf.Tau;
            foreach (float side in new[] { 0f, Mathf.Pi })
            {
                // Backbone bead and the half rung from the axis to it
                var direction = new Vector3(0f, Mathf.Sin(phase + side), Mathf.Cos(phase + side));
                AddMesh(dna, SphereMesh(true), backbone, new Vector3(x, 0f, 0f) + direction * DNA_RADIUS, Vector3.One * 0.05f);
                Color baseColor = DNA_BASES[(i * 2 + (side > 0f ? 1 : 0)) % DNA_BASES.Length];
                MeshInstance3D rung = AddMesh(dna, RodMesh(), Material(baseColor, 0.6f, 0.4f), new Vector3(x, 0f, 0f) + direction * DNA_RADIUS * 0.5f, Vector3.One);
                rung.Basis = new Basis(Vector3.Right, Mathf.Pi * 0.5f - (phase + side)) * Basis.FromScale(new Vector3(0.022f, DNA_RADIUS, 0.022f));
            }
        }
        return dna;
    }

    /// <param name="start">(Optional) Distance of the first bead from the cell center. Defaults to 0.8.</param>
    private void BuildFlagellum(Node3D root, Vector3 direction, Color color, float start = 0.8f)
    {
        var beads = new List<MeshInstance3D>();
        for (int i = 0; i < FLAGELLUM_BEADS; i++)
        {
            float t = (float)i / (FLAGELLUM_BEADS - 1);
            beads.Add(AddMesh(root, SphereMesh(true), Material(color), Vector3.Zero, Vector3.One * Mathf.Lerp(0.075f, 0.03f, t)));
        }
        _flagella.Add(new Flagellum(direction, start, beads));
    }

    /// <summary>Rods along an arc of the rim, a full turn for amoebas.</summary>
    private void BuildCilia(Node3D root, float centerAngle, float span, int count, Color color)
    {
        bool isFullTurn = span >= Mathf.Tau - 0.01f;
        for (int i = 0; i < count; i++)
        {
            float t     = isFullTurn ? (float)i / count : (float)i / (count - 1) - 0.5f;
            float angle = centerAngle + t * span;
            var pivot = new Node3D { Position = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.93f * ShapeAt(angle) };
            root.AddChild(pivot);

            // The rod lies along Y, a -90° turn around Z points it outwards along +X
            MeshInstance3D rod = AddMesh(pivot, RodMesh(), Material(color), new Vector3(0.11f, 0f, 0f), new Vector3(0.02f, 0.24f, 0.02f));
            rod.Rotation = new Vector3(0f, 0f, -Mathf.Pi * 0.5f);
            _cilia.Add((pivot, angle));
        }
    }

    private void BuildSpikes(Node3D root, Vector3 direction, int count, float spread, Color color)
    {
        float baseAngle = Mathf.Atan2(direction.Z, direction.X);
        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + (i - (count - 1) * 0.5f) * spread;
            var pivot = new Node3D { Rotation = new Vector3(0f, -angle, 0f) };
            root.AddChild(pivot);

            // The cone tip lies at +Y, a -90° turn around Z points it outwards along +X
            MeshInstance3D cone = AddMesh(pivot, ConeMesh(), Material(color.Lightened(0.25f), 0f, 0.35f), new Vector3(1.05f * ShapeAt(angle), 0f, 0f), Vector3.One);
            cone.Rotation = new Vector3(0f, 0f, -Mathf.Pi * 0.5f);
        }
    }

    private void BuildFace(Node3D root, Color outline)
    {
        // Eyes side by side on the upper front, pupils look forward and up
        for (int e = 0; e < Body.EyeCount; e++)
        {
            float lateral  = (e - (Body.EyeCount - 1) * 0.5f) * 0.44f;
            var   position = new Vector3(0.3f, 0f, lateral);
            position.Y = SurfaceY(position.X, position.Z) - 0.05f;

            BuildEye(root, position, 1f);
        }

        // Smile, only once a nucleus exists
        if (Body.IsProtocell)
        {
            return;
        }
        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.Lerp(-1.1f, 1.1f, i / 4f);
            var   point = new Vector3(0.56f + Mathf.Cos(angle) * 0.12f, 0f, Mathf.Sin(angle) * 0.12f);
            point.Y = SurfaceY(point.X, point.Z) - 0.01f;
            AddMesh(root, SphereMesh(true), Material(outline), point, Vector3.One * 0.035f);
        }
    }

    private void BuildEye(Node3D root, Vector3 position, float size)
    {
        var eye = new Node3D { Position = position, Scale = Vector3.One * size };
        root.AddChild(eye);
        AddMesh(eye, SphereMesh(), Material(new Color(0.97f, 0.97f, 0.97f), 0.25f, 0.2f), Vector3.Zero, Vector3.One * 0.2f);
        AddMesh(eye, SphereMesh(true), Material(new Color(0.06f, 0.06f, 0.1f), 0f, 0.1f), new Vector3(0.1f, 0.09f, 0f), Vector3.One * 0.1f);
        AddMesh(eye, SphereMesh(true), Material(Colors.White, 1.5f), new Vector3(0.12f, 0.17f, -0.04f), Vector3.One * 0.035f);
        _eyes.Add(eye);
    }

    /// <summary>
    /// A part placed in the editor. Rim parts point outwards from their rim angle,
    /// inner parts float at their position inside the membrane.
    /// </summary>
    private void BuildPlaced(Node3D root, PlacedPart part, Color cellColor)
    {
        float angle    = part.Angle;
        var   rimDir   = new Vector3(part.Position.X, 0f, part.Position.Y).Normalized() * ShapeAt(angle);   // to the stretched outline
        var   inner    = new Vector3(part.Position.X, 0f, part.Position.Y);
        var   yaw      = new Vector3(0f, -angle, 0f);
        Color outline  = cellColor.Darkened(0.45f);

        switch (part.Kind)
        {
            // Herbivore
            case PartKind.Chloroplast:
                MeshInstance3D chloroplast = AddMesh(root, SphereMesh(true), Material(new Color(0.25f, 0.7f, 0.3f), 0.35f), inner, new Vector3(0.17f, 0.09f, 0.11f));
                chloroplast.Rotation = yaw;
                break;
            case PartKind.FilterMouth:
                MeshInstance3D mouth = AddMesh(root, SphereMesh(true), Material(new Color(0.4f, 0.24f, 0.12f)), rimDir * 0.9f + Vector3.Up * 0.12f, new Vector3(0.12f, 0.1f, 0.26f));
                mouth.Rotation = yaw;
                BuildCilia(root, angle, 0.5f, 5, outline);
                break;
            case PartKind.ArmorPlate:
                MeshInstance3D plate = AddMesh(root, SphereMesh(true), Material(new Color(0.5f, 0.55f, 0.62f), 0f, 0.25f), rimDir * 0.95f, new Vector3(0.14f, 0.4f, 0.55f));
                plate.Rotation = yaw;
                break;
            // Omnivore
            case PartKind.Flagellum:
                BuildFlagellum(root, rimDir.Normalized(), outline, 0.8f * ShapeAt(angle));
                break;
            case PartKind.Eyespot:
                Vector3 eyePosition = rimDir * 0.72f;
                eyePosition.Y = SurfaceY(eyePosition.X, eyePosition.Z) - 0.04f;
                BuildEye(root, eyePosition, 0.75f);
                break;
            case PartKind.Vacuole:
                AddMesh(root, SphereMesh(true), Material(new Color(0.55f, 0.8f, 1f, 0.55f), 0.3f, 0.1f), inner, Vector3.One * 0.17f);
                break;
            case PartKind.Cilia:
                BuildCilia(root, angle, 1.4f, 9, outline);
                break;
            // Carnivore
            case PartKind.Spike:
                BuildSpikes(root, rimDir.Normalized(), 1, 0f, outline);
                break;
            case PartKind.Jaw:
                BuildJaw(root, angle);
                break;
            case PartKind.Toxin:
                AddMesh(root, SphereMesh(true), Material(new Color(0.6f, 0.25f, 0.75f, 0.7f), 0.6f, 0.1f), inner, Vector3.One * 0.15f);
                break;
        }
    }

    /// <summary>Two dark mandibles at the rim, tilted towards each other.</summary>
    private void BuildJaw(Node3D root, float angle)
    {
        foreach (float side in new[] { -1f, 1f })
        {
            var pivot = new Node3D { Rotation = new Vector3(0f, -(angle + side * 0.22f), 0f) };
            root.AddChild(pivot);
            MeshInstance3D mandible = AddMesh(pivot, ConeMesh(), Material(new Color(0.35f, 0.12f, 0.1f), 0f, 0.3f), new Vector3(1.1f * ShapeAt(angle), 0.05f, 0f), new Vector3(1.3f, 1.1f, 1.3f));
            mandible.Basis = new Basis(Vector3.Up, side * 0.55f) * new Basis(Vector3.Back, -Mathf.Pi * 0.5f) * Basis.FromScale(new Vector3(1.3f, 1.1f, 1.3f));
        }
    }

    /// <summary>Height of the membrane top above a unit point, the outline stretched by the design.</summary>
    private float SurfaceY(float x, float z)
    {
        float radius = ShapeAt(Mathf.Atan2(z, x));
        return FLATTEN * Mathf.Sqrt(Mathf.Max(0f, 1f - (x * x + z * z) / (radius * radius)));
    }

    /// <summary>Radius of the outline at an angle, 1 for critters and round cells.</summary>
    private float ShapeAt(float angle)
    {
        return Body.Design.ShapeRadius(angle);
    }
}
