// NOTE:
//   The small props of the coast (Creature/Coast.cs), a component of it: seaweed and rocks
//   under water, pebbles, shells and driftwood on the beach, grass, bushes and flowers on
//   the meadow. Seeded per grid cell, so a cell shows the same props every time. Small ones
//   are three primitive multimeshes (shaders/coast_plant.gdshader), shrubs, ferns, flowers
//   and boulders are generated models (Core/PlantModels.cs), one multimesh per plant; all
//   are refilled around the player.
//
//   var props = new CoastProps();
//   props.Setup(terrain);
//   AddChild(props);
//   props.Follow(playerPosition, playerSize, viewRadius);   // every frame

//
//   Chapters:
//     BUILD    The three prop multimeshes and one per plant model.
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
    //              Builds the three prop multimeshes and the plant multimeshes in world space.
    //   AddProps   One prop multimesh with the plant shader.
    //   AddPlants  One multimesh of a plant model, petals colored per instance (custom data).
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

        // Plants: one multimesh per model, shrubs and boulders cast shadows, the flowers are too thin for it
        for (int i = 0; i < PlantModels.COUNT; i++)
        {
            _plants[i] = AddPlants(this, (PlantKind)i);
        }
    }

    private const int MAX_PLANTS = 3000;             // per plant model

    private readonly MultiMesh[] _plants = new MultiMesh[PlantModels.COUNT];
    private readonly int[] _plantCounts = new int[PlantModels.COUNT];

    private static MultiMesh AddPlants(Node3D parent, PlantKind kind)
    {
        var multimesh = new MultiMesh
        {
            TransformFormat      = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors            = true,
            UseCustomData        = PlantModels.HAS_BLOOM[(int)kind],
            Mesh                 = PlantModels.MeshOf(kind),
            InstanceCount        = MAX_PLANTS,
            VisibleInstanceCount = 0,
        };
        bool isCasting = kind is PlantKind.Shrub or PlantKind.BerryBush or PlantKind.Boulder;
        parent.AddChild(new MultiMeshInstance3D
        {
            Name       = kind.ToString(),
            Multimesh  = multimesh,
            CastShadow = isCasting ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
        });
        return multimesh;
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
    //   Refresh  Refills on the next Follow (the ground was leveled).
    //   Follow   Refills the props when the player moved a few Units or grew noticeably.
    //

    private const float PROP_REACH = 45f;            // props at most this far, in Unit

    private Vector2 _propsCenter = new(float.NaN, float.NaN);
    private float _propsSize;

    /// <summary>Refills on the next Follow, e.g. after the ground was leveled for a building.</summary>
    public void Refresh()
    {
        _propsCenter = new Vector2(float.NaN, float.NaN);
    }

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
    //   AddPlant    One plant model instance, a flower with its bloom color.
    //   Leafy       A near-white tint, neighboring plants differ a little.
    //
    //   Bands (d = LandDepth in Unit), per cell of PROP_CELL Units, on land every size
    //   times NATURE_SCALE (under water they would reach out of the water):
    //     -16 .. -1.5   seaweed clumps, rocks
    //     -1.5 .. 2.5   pebbles, shells, driftwood
    //     2.5 .. 5      tall dune grass
    //     5 ..          grass tufts, shrubs and ferns, clumps of flowers, a few boulders (plant models)
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
        System.Array.Clear(_plantCounts);
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

                // Foundations of buildings are trampled bare
                if (_terrain.IsOnPad(point, _terrain.Unit))
                {
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
                    // Undergrowth: mostly leafy shrubs, now and then berries or a ground fern
                    float     pick  = _rng.Randf();
                    PlantKind kind  = pick < 0.5f ? PlantKind.Shrub : pick < 0.72f ? PlantKind.BerryBush : PlantKind.FernBush;
                    float     tall  = _rng.RandfRange(0.3f, 0.5f) * big;
                    var       shape = new Vector3(tall * _rng.RandfRange(0.9f, 1.2f), tall, tall * _rng.RandfRange(0.9f, 1.2f));
                    AddPlant(kind, Place(point, shape, -0.03f * tall, size, tilt: 0.08f), Leafy());
                }
                else if (roll < 0.9f)
                {
                    // Flowers: a clump of one kind, in a color of its own palette
                    int       pick    = _rng.RandiRange(0, 2);
                    PlantKind kind    = (PlantKind)((int)PlantKind.Daisy + pick);
                    Color[]   palette = BLOOMS[pick];
                    int       clumps  = _rng.RandiRange(1, 3);
                    Color     bloom   = palette[_rng.RandiRange(0, palette.Length - 1)];
                    for (int k = 0; k < clumps; k++)
                    {
                        float tall = _rng.RandfRange(0.2f, 0.32f) * big;
                        AddPlant(kind, Place(Jitter(point, 0.25f * NATURE_SCALE), Vector3.One * tall, 0f, size, tilt: 0.12f), Leafy(), bloom);
                    }
                }
                else if (roll < 0.94f)
                {
                    float rock = _rng.RandfRange(0.15f, 0.35f) * big;
                    AddPlant(PlantKind.Boulder, Place(point, new Vector3(rock, rock * _rng.RandfRange(0.6f, 1f), rock * _rng.RandfRange(0.8f, 1.1f)), -0.05f * rock, size, tilt: 0.15f),
                        new Color(1f, 1f, 1f).Lightened(_rng.RandfRange(-0.12f, 0f)));
                }
            }
        }

        _blades.VisibleInstanceCount = _bladeCount;
        _lumps.VisibleInstanceCount  = _lumpCount;
        _sticks.VisibleInstanceCount = _stickCount;
        for (int i = 0; i < PlantModels.COUNT; i++)
        {
            _plants[i].VisibleInstanceCount = _plantCounts[i];
        }
    }

    // Bloom colors per flower (daisy, bell, lupine), sRGB
    private static readonly Color[][] BLOOMS =
    {
        new[] { new Color(1f, 0.98f, 0.94f), new Color(1f, 0.86f, 0.3f), new Color(0.98f, 0.62f, 0.78f) },
        new[] { new Color(0.55f, 0.6f, 1f), new Color(0.75f, 0.55f, 0.98f), new Color(1f, 0.98f, 0.96f) },
        new[] { new Color(0.62f, 0.45f, 0.95f), new Color(0.98f, 0.5f, 0.72f), new Color(1f, 0.82f, 0.32f) },
    };

    /// <summary>A tint close to white, so neighboring plants differ a little in their green.</summary>
    private Color Leafy()
    {
        float light = _rng.RandfRange(0.85f, 1.05f);
        return new Color(light * _rng.RandfRange(0.95f, 1.08f), light, light * _rng.RandfRange(0.88f, 1f));
    }

    /// <summary>One plant model instance, ignored when its multimesh is full.</summary>
    /// <param name="bloom">(Optional) Petal color (sRGB) of a flower. Defaults to none.</param>
    private void AddPlant(PlantKind kind, Transform3D transform, Color tint, Color? bloom = null)
    {
        int i = (int)kind;
        if (_plantCounts[i] >= MAX_PLANTS)
        {
            return;
        }
        _plants[i].SetInstanceTransform(_plantCounts[i], transform);
        _plants[i].SetInstanceColor(_plantCounts[i], tint);
        if (null != bloom)
        {
            _plants[i].SetInstanceCustomData(_plantCounts[i], bloom.Value);
        }
        _plantCounts[i]++;
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
