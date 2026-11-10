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
using System.Linq;
using Godot;

namespace CellOut;

public partial class CellVisual3D : Node3D
{
    public const float FLATTEN = 0.6f;               // cells are flattened spheres, also used by the editor raycast
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
    //   2. Transform  Follows position, facing and size of the body,
    //                 moves divided cells out of their mothers.
    //   3. Animate    Membrane stretch and flash, pseudopods, flagella, eyes and DNA.
    //

    private sealed record SplitCell(Node3D Root, Vector3 From, Vector3 To);

    private const float SPLIT_SECONDS = 0.7f;

    private int _signature;
    private float _time;
    private readonly List<ShaderMaterial> _membranes = new();
    private readonly List<AnimationPlayer> _whips = new();          // placed flagellum models, faster with the speed
    private readonly List<Node3D> _eyes = new();
    private readonly List<Node3D> _spinners = new();
    private Node3D _dna;
    private readonly List<SplitCell> _splitting = new();
    private float _splitTime = SPLIT_SECONDS;
    private int _builtCellCount;

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
        //   Follows position, facing and size of the body,
        //   moves divided cells out of their mothers.
        // ---------------------------------------------------------------------------

        GlobalPosition = Plane3D.To3D(Body.GlobalPosition, -Plane3D.Drop(Body.GlobalPosition));
        Rotation       = new Vector3(0f, Plane3D.FacingToYaw(Body.Facing), 0f);
        Scale          = Vector3.One * Body.Radius;
        Visible        = !Body.IsHidden;

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
        //   Membrane stretch and flash, flagella, cilia, eyes and DNA.
        // ---------------------------------------------------------------------------

        // Squash and stretch along the movement
        float stretch = Mathf.Clamp(Body.Velocity.Length() / (Body.Radius * 30f), 0f, 0.2f);
        // Pseudopods towards a cell being swallowed, in the turned unit space of the body
        Vector2 toPrey     = (Body.EngulfPoint - Body.GlobalPosition).Rotated(-Body.Facing);
        var     engulfDir  = Body.EngulfReach > 0f && toPrey.LengthSquared() > 0.0001f ? toPrey.Normalized() : Vector2.Right;
        foreach (ShaderMaterial membrane in _membranes)
        {
            membrane.SetShaderParameter("stretch", stretch);
            membrane.SetShaderParameter("flash", Body.HurtFlash);
            membrane.SetShaderParameter("engulf_dir", engulfDir);
            membrane.SetShaderParameter("engulf", Body.EngulfReach);
        }

        AnimateParts();
    }

    /// <summary>Hash over everything that changes the built meshes.</summary>
    private int ComputeSignature()
    {
        var hash = new System.HashCode();
        hash.Add(Body.IsProtocell);
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
        // Flagella: the whip runs faster with the speed
        float frequency = 8f + Body.Velocity.Length() / Mathf.Max(Body.Radius, 1f) * 0.6f;
        foreach (AnimationPlayer whip in _whips)
        {
            whip.SpeedScale = frequency / Mathf.Tau;
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
    //   BuildCell         Shadow proxy, membrane, inner life, face and parts of one cell.
    //   BuildInnerLife    Organelles and the nucleus.
    //   BuildDna          The protocell's double helix with colored base pairs.
    //   BuildFace         Face eyes on the upper front (CellBody.EyeCount, 0 by default).
    //   BuildEye          One eye, pupil looking forward and up.
    //   IsOuterCell       Whether a cell sits at the edge of its cluster.
    //   BuildPlacedParts  The placed parts on one cell, rim parts on the facing outer cells of big clusters,
    //                     each on its own pose node.
    //   PosePlaced        Shift and turn of a placed part (gizmo of the CellEditor).
    //   PartAt            The placed part under a ray, for picking in the editor.
    //   TryGetPartAnchor  Where the gizmo of a placed part sits.
    //   CELL_MODELS       Model file and animation of every editor part (tools/blender, Core/PartModels.cs).
    //   BuildPlaced       A part placed in the editor at its position, as its generated model.
    //   BuildCiliaModels  A row of beating cilium models along an arc of the rim.
    //   AttachPart        Adds a part model facing outwards along a rim angle, tilted to the membrane normal.
    //   RimPoint          Point on the membrane for a rim angle and lift.
    //   RimNormal         Outward normal of the membrane at a point on it.
    //   RimTilt           Tilt of that normal above the horizon.
    //   SurfaceY          Height of the membrane top at a unit point, outline and heights included.
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
        _whips.Clear();
        _eyes.Clear();
        _spinners.Clear();
        _poses.Clear();
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
        Log.Verbose("CellVisual3D", $"Rebuilt '{Body.Name}' with {Body.CellLayout().Count} cell(s).");
    }

    /// <returns>The root node of the cell, positioned at its place in the cluster.</returns>
    private Node3D BuildCell(CellPart part)
    {
        var root = new Node3D { Position = part.Position };
        AddChild(root);
        Vector3 center  = root.Position;
        Color   color   = Body.BodyColor;

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
            MeshInstance3D proxy = AddMesh(root, SphereMesh(true), Material(color), Vector3.Zero, new Vector3(0.9f, FLATTEN * 0.9f * Body.Design.Heights.Average(), 0.9f));
            proxy.CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly;
        }

        // Membrane
        Color membraneColor = color;
        if (Body.IsProtocell)
        {
            membraneColor.A *= 0.8f;
        }
        var membraneMaterial = new ShaderMaterial { Shader = MEMBRANE_SHADER };
        membraneMaterial.SetShaderParameter("body_color", membraneColor);
        membraneMaterial.SetShaderParameter("seed", Body.AnimSeed + center.X * 1.3f + center.Z * 0.7f);
        membraneMaterial.SetShaderParameter("core_alpha", Body.IsProtocell ? 0.2f : 0.42f);
        membraneMaterial.SetShaderParameter("shape", System.Linq.Enumerable.ToArray(Body.Design.Shape));
        membraneMaterial.SetShaderParameter("heights", System.Linq.Enumerable.ToArray(Body.Design.Heights));
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

        // Face and the placed parts on every cell (NPC cells carry editor parts too)
        if (isFullDetail)
        {
            BuildFace(root);
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
        for (int index = 0; index < Body.Design.Parts.Count; index++)
        {
            PlacedPart placed   = Body.Design.Parts[index];
            bool       isFacing = Mathf.Abs(Mathf.Wrap(placed.Angle - outward, -Mathf.Pi, Mathf.Pi)) <= RIM_PART_ARC;
            bool       isShown  = Placement.Rim == placed.Info.Placement
                ? isAll || (isOuter && isFacing)
                : Body.CellCount <= FULL_DETAIL_LIMIT || isOuter;
            if (isShown)
            {
                var pose = new Node3D();
                root.AddChild(pose);
                BuildPlaced(pose, placed, cellColor);
                PosePlaced(index, pose, placed);
            }
        }
    }

    // Pose nodes of the placed parts per design index, for picking and the gizmo of the CellEditor
    private readonly List<(int Part, Node3D Pose, Vector3 Anchor)> _poses = new();

    /// <summary>
    /// Turns the pose node of a placed part around the center of its pieces and shifts it
    /// (PlacedPart.Offset, Rotation). The pieces keep their own animation (spinners, cilia)
    /// below the pose node, so the pose survives every frame.
    /// </summary>
    private void PosePlaced(int index, Node3D pose, PlacedPart placed)
    {
        Vector3 anchor = Vector3.Zero;
        int     count  = 0;
        foreach (Node3D piece in pose.GetChildren().OfType<Node3D>())
        {
            anchor += piece.Position;
            count++;
        }
        anchor = count > 0 ? anchor / count : Vector3.Zero;
        Basis turn = Basis.FromEuler(placed.Rotation);
        pose.Transform = new Transform3D(turn, anchor + placed.Offset - turn * anchor);
        _poses.Add((index, pose, anchor));
    }

    /// <summary>The placed part whose mesh bounds a ray hits first (CreatureVisual3D.RayEntry).</summary>
    /// <param name="from">(Required) Ray origin in global space.</param>
    /// <param name="direction">(Required) Ray direction in global space.</param>
    /// <returns>Index in Body.Design.Parts, -1 when the ray hits no part.</returns>
    public int PartAt(Vector3 from, Vector3 direction)
    {
        int   closest  = -1;
        float bestDist = float.PositiveInfinity;
        foreach ((int part, Node3D pose, _) in _poses)
        {
            if (!IsInstanceValid(pose) || !pose.IsVisibleInTree())
            {
                continue;
            }
            foreach (MeshInstance3D mesh in pose.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            {
                float? distance = CreatureVisual3D.RayEntry(mesh.GlobalTransform * mesh.GetAabb(), from, direction);
                if (null != distance && distance.Value < bestDist)
                {
                    bestDist = distance.Value;
                    closest  = part;
                }
            }
        }
        return closest;
    }

    /// <summary>Where the gizmo of a placed part sits: the shifted center of its first built copy.</summary>
    /// <param name="index">(Required) Index in Body.Design.Parts.</param>
    /// <param name="anchor">The anchor in global space.</param>
    /// <returns>False when the part is not built (yet).</returns>
    public bool TryGetPartAnchor(int index, out Vector3 anchor)
    {
        foreach ((int part, Node3D pose, Vector3 local) in _poses)
        {
            if (index == part && IsInstanceValid(pose) && pose.IsInsideTree())
            {
                anchor = pose.GlobalTransform * local;
                return true;
            }
        }
        anchor = Vector3.Zero;
        return false;
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

    private void BuildFace(Node3D root)
    {
        // Eyes side by side on the upper front, pupils look forward and up
        for (int e = 0; e < Body.EyeCount; e++)
        {
            float lateral  = (e - (Body.EyeCount - 1) * 0.5f) * 0.44f;
            var   position = new Vector3(0.3f, 0f, lateral);
            position.Y = SurfaceY(position.X, position.Z) - 0.05f;

            BuildEye(root, position, 1f);
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

    /*
      Model space of the cell parts (tools/blender/build.models.py, CELL PARTS): unit
      space of a cell. Rim parts have their base at the origin and point outwards along
      +X, inner parts are centered. CELL_PART_SIZE makes them bold micro monsters.
    */
    public const string CELL_MODEL_DIR = "res://assets/models/cell/";
    public static readonly IReadOnlyDictionary<PartKind, (string File, string Animation)> CELL_MODELS =
        new Dictionary<PartKind, (string File, string Animation)>
        {
            [PartKind.Chloroplast] = ("cell.chloroplast.glb", null),
            [PartKind.FilterMouth] = ("cell.filtermouth.glb", null),
            [PartKind.ArmorPlate]  = ("cell.armorplate.glb", null),
            [PartKind.Flagellum]   = ("cell.flagellum.glb", "Whip"),
            [PartKind.Eyespot]     = ("cell.eyespot.glb", null),
            [PartKind.Vacuole]     = ("cell.vacuole.glb", null),
            [PartKind.Cilia]       = ("cell.cilium.glb", "Beat"),
            [PartKind.Spike]       = ("cell.spike.glb", null),
            [PartKind.Jaw]         = ("cell.jaw.glb", "Chomp"),
            [PartKind.Toxin]       = ("cell.toxin.glb", null),
        };

    private const float CELL_PART_SIZE = 1.25f;
    private const float CILIA_SPAN = 1.4f;           // radians of rim one cilia part covers
    private const int CILIA_PER_PART = 9;
    private const float BEAT_RATE = 1.6f;            // cilia beats per second
    private const float CHOMP_RATE = 0.9f;

    /// <summary>A new instance of a cell part model, recolored (Core/PartModels.cs).</summary>
    /// <param name="kind">(Required) An editor part.</param>
    /// <param name="cellColor">(Required) Color of the cell, the skin of the part follows it.</param>
    /// <example><code>Node3D jaw = CellVisual3D.NewPartModel(PartKind.Jaw, color);</code></example>
    public static Node3D NewPartModel(PartKind kind, Color cellColor)
    {
        return PartModels.New(CELL_MODEL_DIR + CELL_MODELS[kind].File, cellColor);
    }

    private const float EYE_SINK = 0.06f;            // the eyespot sits this deep in the membrane, along its normal

    /// <summary>
    /// A part placed in the editor as its generated model. Rim parts stand on the membrane
    /// at their angle and lift, pointing along its normal; inner parts float at their
    /// position inside the membrane.
    /// </summary>
    private void BuildPlaced(Node3D root, PlacedPart part, Color cellColor)
    {
        float   angle = part.Angle;
        float   lift  = part.Lift;
        float   tilt  = RimTilt(angle, lift);
        var     inner = new Vector3(part.Position.X, 0f, part.Position.Y);
        Color   skin  = cellColor.Darkened(0.2f);

        switch (part.Kind)
        {
            // Inner parts: centered at their spot, organelles drift
            case PartKind.Chloroplast:
            case PartKind.Vacuole:
            case PartKind.Toxin:
                _spinners.Add(AttachPart(root, part.Kind, skin, inner, angle, CELL_PART_SIZE));
                break;

            // Rim parts: base on the membrane, pointing outwards along its normal
            case PartKind.ArmorPlate:
            case PartKind.Spike:
                AttachPart(root, part.Kind, skin, RimPoint(angle, lift, 0.97f), angle, CELL_PART_SIZE, tilt);
                break;
            case PartKind.Jaw:
                Node3D jaw = AttachPart(root, part.Kind, skin, RimPoint(angle, lift, 0.97f), angle, CELL_PART_SIZE, tilt);
                PartModels.PlayLoop(jaw, CELL_MODELS[part.Kind].Animation, Body.AnimSeed, CHOMP_RATE);
                break;
            case PartKind.Flagellum:
                Node3D flagellum = AttachPart(root, part.Kind, skin, RimPoint(angle, lift, 0.92f), angle, CELL_PART_SIZE, tilt);
                AnimationPlayer whip = PartModels.PlayLoop(flagellum, CELL_MODELS[part.Kind].Animation, angle / Mathf.Tau, 1f);
                if (null != whip)
                {
                    _whips.Add(whip);
                }
                break;
            case PartKind.FilterMouth:
                AttachPart(root, part.Kind, skin, RimPoint(angle, lift, 0.95f), angle, CELL_PART_SIZE, tilt);
                BuildCiliaModels(root, angle, lift, CILIA_SPAN * 0.4f, 5, skin);
                break;
            case PartKind.Cilia:
                BuildCiliaModels(root, angle, lift, CILIA_SPAN, CILIA_PER_PART, skin);
                break;

            // Eyespot: the round eye sinks into the membrane and looks outwards along its normal, a wrapper node blinks
            case PartKind.Eyespot:
                Vector3 surface = RimPoint(angle, lift, 1f);
                var blink = new Node3D { Position = surface - RimNormal(surface, angle) * EYE_SINK };
                root.AddChild(blink);
                AttachPart(blink, part.Kind, skin, Vector3.Zero, angle, CELL_PART_SIZE, tilt);
                _eyes.Add(blink);
                break;

            default:
                throw new System.ArgumentOutOfRangeException(nameof(part), $"part must be a known PartKind, got '{part.Kind}'.");
        }
    }

    /// <summary>A row of cilium models along an arc of the rim at one lift, beating in a wave (metachronal).</summary>
    private void BuildCiliaModels(Node3D root, float centerAngle, float lift, float span, int count, Color color)
    {
        for (int i = 0; i < count; i++)
        {
            float  angle  = centerAngle + ((float)i / (count - 1) - 0.5f) * span;
            Node3D cilium = AttachPart(root, PartKind.Cilia, color, RimPoint(angle, lift, 0.95f), angle, CELL_PART_SIZE, RimTilt(angle, lift));
            PartModels.PlayLoop(cilium, CELL_MODELS[PartKind.Cilia].Animation, i * 0.11f, BEAT_RATE);
        }
    }

    /// <summary>Adds a part model at a position, its +X turned to the rim angle and tilted up (or down) by tilt.</summary>
    /// <param name="tilt">(Optional) Turn of +X towards +Y in radians, applied before the rim angle. Defaults to 0.</param>
    private static Node3D AttachPart(Node3D parent, PartKind kind, Color color, Vector3 position, float angle, float size, float tilt = 0f)
    {
        Node3D model = NewPartModel(kind, color);
        model.Position = position;
        model.Basis    = (new Basis(Vector3.Up, -angle) * new Basis(Vector3.Back, tilt)).Scaled(Vector3.One * size);
        parent.AddChild(model);
        return model;
    }

    /// <summary>Point on the membrane for a rim angle and lift (CellDesign.SurfacePoint), pulled in by depth.</summary>
    private Vector3 RimPoint(float angle, float lift, float depth)
    {
        return Body.Design.SurfacePoint(angle, lift) * depth;
    }

    /// <summary>Outward normal of the membrane at a point on it: the gradient (p.x / S^2, p.y / H^2, p.z / S^2).</summary>
    private Vector3 RimNormal(Vector3 point, float angle)
    {
        float stretch = ShapeAt(angle);
        float around  = new Vector2(point.X, point.Z).Length() / stretch;
        float height  = Body.Design.MembraneHeight(angle, around);
        return new Vector3(point.X / (stretch * stretch), point.Y / (height * height), point.Z / (stretch * stretch)).Normalized();
    }

    /// <summary>Tilt of the membrane normal above the horizon at a rim angle and lift, in radians.</summary>
    private float RimTilt(float angle, float lift)
    {
        Vector3 normal = RimNormal(RimPoint(angle, lift, 1f), angle);
        return Mathf.Atan2(normal.Y, new Vector2(normal.X, normal.Z).Length());
    }

    /// <summary>Height of the membrane top above a unit point, outline and heights of the design included.</summary>
    private float SurfaceY(float x, float z)
    {
        return Body.Design.TopHeight(new Vector2(x, z));
    }

    /// <summary>Radius of the outline at an angle, 1 for critters and round cells.</summary>
    private float ShapeAt(float angle)
    {
        return Body.Design.ShapeRadius(angle);
    }
}
