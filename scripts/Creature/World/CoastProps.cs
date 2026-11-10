// NOTE:
//   The small props of the coast (Creature/Coast.cs), a component of it: seaweed and rocks
//   under water, pebbles, shells and driftwood on the beach, grass, bushes and flowers on
//   the meadow. Seeded per grid cell, so a cell shows the same props every time, drawn as
//   three multimeshes (shaders/coast_plant.gdshader) that are refilled around the player.
//
//   var props = new CoastProps();
//   props.Setup(terrain);
//   AddChild(props);
//   props.Follow(playerPosition, playerSize, viewRadius);   // every frame

//
//   Chapters:
//     BUILD    The three prop multimeshes.
//     FOLLOW   Refills the props when the player moved or grew.
//     PROPS    Seeded props per grid cell, by band.
//

using Godot;

namespace CellOut;

public partial class CoastProps : Node3D
{
    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   Setup      (Required) terrain: land depth, ground height and the unit of the coast.
    //              Builds the three prop multimeshes in world space.
    //   AddProps   One prop multimesh with the plant shader.
    //

    private const int MAX_PROPS = 16000;
    private static readonly Shader PLANT_SHADER = GD.Load<Shader>("res://shaders/coast_plant.gdshader");

    private CoastTerrain _terrain;
    private MultiMesh _blades;
    private MultiMesh _lumps;
    private MultiMesh _sticks;

    /// <summary>Builds the prop multimeshes, called once before AddChild.</summary>
    /// <param name="terrain">(Required) The terrain of the coast.</param>
    public void Setup(CoastTerrain terrain)
    {
        _terrain = terrain;

        // Props in world space: blades (cones) sway, lumps (spheres) and sticks (rods) do not
        _blades = AddProps(this, CellVisual3D.ConeMesh(), 0.12f);
        _lumps  = AddProps(this, CellVisual3D.SphereMesh(true), 0f);
        _sticks = AddProps(this, CellVisual3D.RodMesh(), 0f);
    }

    private static MultiMesh AddProps(Node3D parent, Mesh mesh, float sway)
    {
        var multimesh = new MultiMesh
        {
            TransformFormat      = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors            = true,
            Mesh                 = mesh,
            InstanceCount        = MAX_PROPS,
            VisibleInstanceCount = 0,
        };
        var material = new ShaderMaterial { Shader = PLANT_SHADER };
        material.SetShaderParameter("sway", sway);
        // No shadows: thousands of thin props only print blocky shadow texels at this scale
        parent.AddChild(new MultiMeshInstance3D { Multimesh = multimesh, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return multimesh;
    }

    // ===========================================================================
    // ===============================   FOLLOW   ================================
    // ===========================================================================
    //
    //   Follow   Refills the props when the player moved a few Units or grew noticeably.
    //

    private const float PROP_REACH = 45f;            // props at most this far, in Unit

    private Vector2 _propsCenter = new(float.NaN, float.NaN);
    private float _propsSize;

    /// <summary>Keeps the props around the player, called every frame.</summary>
    /// <param name="playerPosition">(Required) Plane position of the player.</param>
    /// <param name="playerSize">(Required) Player.BodySize(), heights scale with it.</param>
    /// <param name="viewRadius">(Required) Visible radius on the plane, the same the Backdrop gets.</param>
    public void Follow(Vector2 playerPosition, float playerSize, float viewRadius)
    {
        float unit     = _terrain.Unit;
        bool  hasMoved = float.IsNaN(_propsCenter.X) || playerPosition.DistanceTo(_propsCenter) > unit * 3f;
        bool  hasGrown = Mathf.Abs(playerSize / Mathf.Max(_propsSize, 0.001f) - 1f) > 0.05f;
        if (!hasMoved && !hasGrown)
        {
            return;
        }
        _propsCenter = playerPosition;
        _propsSize   = playerSize;
        FillProps(playerPosition, playerSize, Mathf.Min(viewRadius * 1.3f + unit * 4f, PROP_REACH * unit));
    }

    // ===========================================================================
    // ================================   PROPS   ================================
    // ===========================================================================
    //
    //   FillProps   Seeds every grid cell around the player and places its props by band.
    //   Jitter      A point moved randomly by a few Units.
    //   AddTuft     A tuft of grass blades.
    //   Place       World transform of a prop at a plane point, on the ground.
    //   Add         One instance into a multimesh, ignored when it is full.
    //
    //   Bands (d = LandDepth in Unit), per cell of PROP_CELL Units, on land every size
    //   times NATURE_SCALE (under water they would reach out of the water):
    //     -16 .. -1.5   seaweed clumps, rocks
    //     -1.5 .. 2.5   pebbles, shells, driftwood
    //     2.5 .. 5      tall dune grass
    //     5 ..          grass tufts, bushes, flowers, a few rocks
    //

    private const float PROP_CELL = 2.5f;            // in Unit
    private const float NATURE_SCALE = CoastTerrain.NATURE_SCALE;

    private readonly RandomNumberGenerator _rng = new();
    private int _bladeCount;
    private int _lumpCount;
    private int _stickCount;

    private void FillProps(Vector2 center, float size, float reach)
    {
        _bladeCount = 0;
        _lumpCount  = 0;
        _stickCount = 0;
        float cell = PROP_CELL * _terrain.Unit;
        int   x0   = Mathf.FloorToInt((center.X - reach) / cell);
        int   x1   = Mathf.CeilToInt((center.X + reach) / cell);
        int   y0   = Mathf.FloorToInt((center.Y - reach) / cell);
        int   y1   = Mathf.CeilToInt((center.Y + reach) / cell);

        for (int gx = x0; gx <= x1; gx++)
        {
            for (int gy = y0; gy <= y1; gy++)
            {
                // Same cell -> same seed -> the same props every time
                _rng.Seed = (ulong)(uint)System.HashCode.Combine(gx, gy, 31);
                var   point = new Vector2(gx + _rng.Randf(), gy + _rng.Randf()) * cell;
                float roll  = _rng.Randf();
                float d     = _terrain.LandDepth(point) / _terrain.Unit;
                if (d < -16f || point.DistanceSquaredTo(center) > reach * reach)
                {
                    continue;
                }

                // Under water: seaweed clumps reaching up, mossy rocks
                if (d < -1.5f)
                {
                    if (roll < 0.35f)
                    {
                        int blades = _rng.RandiRange(3, 6);
                        for (int k = 0; k < blades; k++)
                        {
                            float height = _rng.RandfRange(0.6f, 1.4f);
                            Color kelp   = new Color(0.16f, 0.36f, 0.22f).Lerp(new Color(0.42f, 0.36f, 0.16f), _rng.Randf());
                            Add(_blades, ref _bladeCount, Place(Jitter(point, 0.3f), new Vector3(0.45f, height / 0.6f, 0.3f) * size * 0.25f,
                                height * 0.5f * size, size, tilt: 0.25f), kelp);
                        }
                    }
                    else if (roll < 0.5f)
                    {
                        float rock = _rng.RandfRange(0.15f, 0.45f);
                        Add(_lumps, ref _lumpCount, Place(point, new Vector3(rock, rock * 0.6f, rock * 0.8f) * size, 0f, size, tilt: 0f),
                            new Color(0.3f, 0.36f, 0.33f).Lightened(_rng.RandfRange(-0.1f, 0.15f)));
                    }
                    continue;
                }

                // On land everything is NATURE_SCALE bigger against the body
                float big = size * NATURE_SCALE;

                // Beach: pebbles, shells and now and then driftwood
                if (d < 2.5f)
                {
                    if (roll < 0.22f)
                    {
                        int pebbles = _rng.RandiRange(2, 4);
                        for (int k = 0; k < pebbles; k++)
                        {
                            float pebble = _rng.RandfRange(0.03f, 0.08f);
                            Add(_lumps, ref _lumpCount, Place(Jitter(point, 0.4f * NATURE_SCALE), new Vector3(pebble, pebble * 0.55f, pebble * 0.8f) * big, 0f, size, tilt: 0f),
                                new Color(0.55f, 0.52f, 0.48f).Lightened(_rng.RandfRange(-0.2f, 0.25f)));
                        }
                    }
                    else if (roll < 0.34f)
                    {
                        Color shell = _rng.Randf() < 0.5f ? new Color(0.97f, 0.82f, 0.78f) : new Color(0.98f, 0.94f, 0.85f);
                        Add(_lumps, ref _lumpCount, Place(point, new Vector3(0.07f, 0.025f, 0.055f) * big, 0f, size, tilt: 0f), shell);
                    }
                    else if (roll < 0.38f)
                    {
                        float length = _rng.RandfRange(0.5f, 1.1f);
                        Transform3D wood = Place(point, new Vector3(0.045f, length, 0.045f) * big, 0.03f * big, size, tilt: 0f,
                            lay: new Basis(Vector3.Back, Mathf.Pi * 0.5f));   // the rod lies down
                        Add(_sticks, ref _stickCount, wood, new Color(0.5f, 0.4f, 0.3f));
                    }
                    continue;
                }

                // Dunes: sparse, tall pale grass
                if (d < 5f)
                {
                    if (roll < 0.5f)
                    {
                        AddTuft(point, size, new Color(0.62f, 0.66f, 0.42f), 0.25f * NATURE_SCALE, 0.45f * NATURE_SCALE);
                    }
                    continue;
                }

                // Meadow: grass tufts, bushes, flowers, a few rocks
                if (roll < 0.62f)
                {
                    AddTuft(point, size, new Color(0.25f, 0.45f, 0.2f).Lerp(new Color(0.42f, 0.58f, 0.26f), _rng.Randf()), 0.15f * NATURE_SCALE, 0.32f * NATURE_SCALE);
                }
                else if (roll < 0.76f)
                {
                    Color leaves = new Color(0.18f, 0.36f, 0.17f).Lightened(_rng.RandfRange(-0.05f, 0.12f));
                    for (int k = 0; k < 3; k++)
                    {
                        float leaf = _rng.RandfRange(0.14f, 0.24f);
                        Add(_lumps, ref _lumpCount, Place(Jitter(point, 0.15f * NATURE_SCALE), Vector3.One * leaf * big, leaf * 0.7f * big, size, tilt: 0f),
                            leaves.Lightened(k * 0.06f));
                    }
                }
                else if (roll < 0.9f)
                {
                    Color bloom = new[] { new Color(0.97f, 0.86f, 0.3f), new Color(0.93f, 0.48f, 0.68f), new Color(0.6f, 0.62f, 0.98f), Colors.White }[_rng.RandiRange(0, 3)];
                    int   heads = _rng.RandiRange(2, 4);
                    for (int k = 0; k < heads; k++)
                    {
                        Vector2 spot = Jitter(point, 0.3f * NATURE_SCALE);
                        float   stem = _rng.RandfRange(0.15f, 0.25f);
                        Add(_blades, ref _bladeCount, Place(spot, new Vector3(0.08f, stem / 0.6f, 0.08f) * big * 0.25f, stem * 0.5f * big, size, tilt: 0.1f),
                            new Color(0.3f, 0.5f, 0.22f));
                        Add(_lumps, ref _lumpCount, Place(spot, Vector3.One * 0.035f * big, stem * big, size, tilt: 0f), bloom);
                    }
                }
                else if (roll < 0.94f)
                {
                    float rock = _rng.RandfRange(0.12f, 0.3f);
                    Add(_lumps, ref _lumpCount, Place(point, new Vector3(rock, rock * 0.65f, rock * 0.85f) * big, 0f, size, tilt: 0f),
                        new Color(0.5f, 0.5f, 0.47f).Lightened(_rng.RandfRange(-0.1f, 0.1f)));
                }
            }
        }

        _blades.VisibleInstanceCount = _bladeCount;
        _lumps.VisibleInstanceCount  = _lumpCount;
        _sticks.VisibleInstanceCount = _stickCount;
    }

    /// <summary>A point moved randomly by up to spread Units on both axes.</summary>
    private Vector2 Jitter(Vector2 point, float spread)
    {
        return point + new Vector2(_rng.RandfRange(-spread, spread), _rng.RandfRange(-spread, spread)) * _terrain.Unit;
    }

    /// <summary>A tuft of grass blades of one color, heights between min and max player sizes.</summary>
    private void AddTuft(Vector2 point, float size, Color color, float minHeight, float maxHeight)
    {
        int blades = _rng.RandiRange(4, 7);
        for (int k = 0; k < blades; k++)
        {
            float height = _rng.RandfRange(minHeight, maxHeight);
            Add(_blades, ref _bladeCount, Place(Jitter(point, 0.12f * NATURE_SCALE), new Vector3(0.3f, height / 0.6f, 0.2f) * size * 0.25f,
                height * 0.5f * size, size, tilt: 0.35f), color.Lightened(_rng.RandfRange(-0.08f, 0.1f)));
        }
    }

    /// <summary>A transform at a plane point, standing on the ground, randomly turned and tilted.</summary>
    /// <param name="lift">(Required) Height of the prop center above the ground, in world units.</param>
    /// <param name="tilt">(Required) Largest random lean in radians.</param>
    /// <param name="lay">(Optional) Turn of the mesh before the lean, e.g. a rod lying down. Defaults to none.</param>
    private Transform3D Place(Vector2 point, Vector3 scale, float lift, float size, float tilt, Basis? lay = null)
    {
        float ground = _terrain.GroundHeight(point) * size;
        Basis basis  = new Basis(Vector3.Up, _rng.Randf() * Mathf.Tau)
            * new Basis(Vector3.Right, _rng.RandfRange(-tilt, tilt))
            * (lay ?? Basis.Identity)
            * Basis.FromScale(scale);
        return new Transform3D(basis, Plane3D.To3D(point, ground + lift));
    }

    private static void Add(MultiMesh multimesh, ref int count, Transform3D transform, Color color)
    {
        if (count >= MAX_PROPS)
        {
            return;
        }
        multimesh.SetInstanceTransform(count, transform);
        multimesh.SetInstanceColor(count, color);
        count++;
    }
}
