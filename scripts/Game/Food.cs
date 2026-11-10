// NOTE:
//   Food on the plane, eaten when the player touches it. Logic in 2D, rendered in 3D by
//   habitat (set by Main before AddChild):
//     - Soup   drifting biomolecules of the primordial soup: sugar ring, amino chain, lipid
//     - Sea    from the organism on, under the water surface: algae ball, glowing plankton,
//              fish roe, meat chunks of popped creatures
//     - Land   on the islands, lying on the ground (GroundHeight from Main): fruit,
//              mushroom, berries; it does not drift
//   Spawned by Main.
//
//   var food = new Food { Habitat = FoodHabitat.Land, GroundHeight = height };
//   food.Setup(size, FoodKind.Sugar);

//
//   Chapters:
//     SETUP AND DRIFT   Kind, habitat, size, value and the drift on the plane.
//     BUILD VISUAL      Molecule, sea food or land food of the kind, in unit size.
//

using Godot;

namespace CellOut;

public enum FoodKind { Sugar, Amino, Lipid }

public enum FoodHabitat { Soup, Sea, Land }

public partial class Food : Node2D
{
    // ===========================================================================
    // ===========================   SETUP AND DRIFT   ===========================
    // ===========================================================================
    //
    //   Setup       Size, kind, biomass value and a random drift.
    //   ColorOf     Main color per habitat and kind, meat chunks are red.
    //   BaseValue   Biomass per kind before scaling by size.
    //   _Ready      Builds the visual, see BUILD VISUAL.
    //   _Process    Drifts (not on land), bobs, spins, sits on the ground on land.
    //

    public FoodKind Kind { get; private set; } = FoodKind.Sugar;
    public float Size { get; private set; } = 4f;
    public float Value { get; set; } = 1f;

    /// <summary>(Optional) Remains of a cell or creature, count as meat for the diet. Defaults to false (plant).</summary>
    public bool IsMeat { get; set; }

    /// <summary>(Optional) Where the food lives, set before AddChild. Defaults to the soup.</summary>
    public FoodHabitat Habitat { get; set; } = FoodHabitat.Soup;

    /// <summary>(Optional) On land: the ground below in world units, kept up to date by Main. Defaults to 0.</summary>
    public float GroundHeight { get; set; }

    /// <summary>(Optional) Drawn this much bigger than Size, the value stays. Sea and land food fit the creatures. Defaults to 1.</summary>
    public float LookScale { get; set; } = 1f;

    private Vector2 _drift = Vector2.Zero;
    private float _spin;

    /// <summary>Sets size, kind and value, called before AddChild.</summary>
    /// <param name="size">(Required) Size on the plane.</param>
    /// <param name="kind">(Required) Sugar, amino acid or lipid (fruit, mushroom, berries on land).</param>
    public void Setup(float size, FoodKind kind)
    {
        Size   = size;
        Kind   = kind;
        Value  = BaseValue(kind) * Mathf.Pow(size / 3.5f, 1.5f) * 0.5f;
        _drift = Vector2.FromAngle(Rand.Angle()) * size * Rand.Range(0.4f, 1.5f);
        _spin  = Rand.Range(-1.5f, 1.5f);
    }

    /// <summary>Main color of the food (meat chunks are red), also used for the splash when eaten.</summary>
    public Color ColorOf()
    {
        if (IsMeat)
        {
            return MEAT_COLOR;
        }
        return (Habitat, Kind) switch
        {
            (FoodHabitat.Land, FoodKind.Sugar) => FRUIT_COLOR,
            (FoodHabitat.Land, FoodKind.Amino) => MUSHROOM_COLOR,
            (FoodHabitat.Land, _)              => BERRY_COLOR,
            (FoodHabitat.Sea, FoodKind.Sugar)  => ALGAE_COLOR,
            (FoodHabitat.Sea, FoodKind.Amino)  => PLANKTON_COLOR,
            (FoodHabitat.Sea, _)               => ROE_COLOR,
            (_, FoodKind.Sugar)                => new Color(1f, 0.85f, 0.35f),
            (_, FoodKind.Amino)                => new Color(0.45f, 0.75f, 1f),
            _                                  => new Color(1f, 0.55f, 0.75f),
        };
    }

    private static float BaseValue(FoodKind kind)
    {
        return kind switch
        {
            FoodKind.Sugar => 1f,
            FoodKind.Amino => 1.5f,
            _              => 2f,
        };
    }

    private static readonly Color MEAT_COLOR = new(0.85f, 0.35f, 0.3f);
    private static readonly Color FRUIT_COLOR = new(0.92f, 0.3f, 0.2f);
    private static readonly Color MUSHROOM_COLOR = new(0.85f, 0.2f, 0.18f);
    private static readonly Color BERRY_COLOR = new(0.42f, 0.3f, 0.85f);
    private static readonly Color ALGAE_COLOR = new(0.3f, 0.62f, 0.25f);
    private static readonly Color PLANKTON_COLOR = new(0.45f, 0.95f, 0.95f);
    private static readonly Color ROE_COLOR = new(1f, 0.55f, 0.2f, 0.75f);

    private Node3D _visual;
    private Vector3 _spinAxis = Vector3.Up;
    private const float GROW_IN_SECONDS = 0.4f;
    private const float SEA_DEPTH = 0.6f;            // sea food floats this many sizes below the body plane

    private float _bob;
    private float _age;

    public override void _Ready()
    {
        // Land food lies still and only turns around its stalk
        if (FoodHabitat.Land == Habitat)
        {
            _drift    = Vector2.Zero;
            _spin    *= 0.2f;
        }
        BuildVisual();

        // Placed right away, food spawned during a pause would wait at the origin otherwise
        _visual.GlobalPosition = VisualPosition();
        _visual.Scale          = Vector3.One * 0.001f;   // grows in, see GrowIn
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Position += _drift * dt;
        _bob     += dt;
        _age     += dt;
        _visual.GlobalPosition = VisualPosition();
        _visual.Rotate(_spinAxis, _spin * dt);
        _visual.Scale = Vector3.One * Size * LookScale * GrowIn();
    }

    /// <summary>Soup and sea food bob (the sea food under the surface), land food sits on the ground.</summary>
    private Vector3 VisualPosition()
    {
        float height = Habitat switch
        {
            FoodHabitat.Land => GroundHeight,
            FoodHabitat.Sea  => (Mathf.Sin(_bob * 1.3f) * 0.3f - SEA_DEPTH) * Size * LookScale - Plane3D.SeaDrop,
            _                => Mathf.Sin(_bob * 1.3f) * Size * 0.4f,
        };
        return Plane3D.To3D(GlobalPosition, height - Plane3D.Drop(GlobalPosition));
    }

    /// <summary>0 to 1 over the first GROW_IN_SECONDS with a little overshoot, new food pops in.</summary>
    private float GrowIn()
    {
        float t = Mathf.Clamp(_age / GROW_IN_SECONDS, 0f, 1f);
        float u = t - 1f;
        return Mathf.Max(1f + 2.70158f * u * u * u + 1.70158f * u * u, 0.001f);
    }

    // ===========================================================================
    // =============================   BUILD VISUAL   ============================
    // ===========================================================================
    //
    //   BuildVisual     The visual node, orientation and spin by habitat, then the shape.
    //   BuildMolecule   Soup: sugar ring, amino chain or lipid with two tails.
    //   BuildSeaFood    Sea: algae ball, plankton cloud or fish roe.
    //   BuildLandFood   Land: fruit, mushroom or berries, standing on y = 0.
    //   BuildMeat       A meat chunk with a fat rim and a bit of bone, soup, sea and land alike.
    //

    private void BuildVisual()
    {
        bool isLand = FoodHabitat.Land == Habitat;
        _visual = new Node3D
        {
            Basis = isLand
                ? new Basis(Vector3.Up, Rand.Angle())
                : Basis.FromEuler(new Vector3(Rand.Angle(), Rand.Angle(), 0f)),
        };
        _spinAxis = isLand ? Vector3.Up : new Vector3(Rand.Range(-1f, 1f), 1f, Rand.Range(-1f, 1f)).Normalized();
        _bob      = Rand.Angle();
        AddChild(_visual);

        if (IsMeat && FoodHabitat.Soup != Habitat)
        {
            BuildMeat();
            return;
        }
        switch (Habitat)
        {
            case FoodHabitat.Sea:
                BuildSeaFood();
                break;
            case FoodHabitat.Land:
                BuildLandFood();
                break;
            default:
                BuildMolecule();
                break;
        }
    }

    private void BuildMolecule()
    {
        Color              color = ColorOf();
        StandardMaterial3D atom  = CellVisual3D.Material(color, 0.45f, 0.3f);
        StandardMaterial3D bond  = CellVisual3D.Material(color.Darkened(0.35f), 0.2f, 0.4f);
        SphereMesh         ball  = CellVisual3D.SphereMesh(true);

        switch (Kind)
        {
            // Ring of six atoms
            case FoodKind.Sugar:
                CellVisual3D.AddMesh(_visual, CellVisual3D.TorusMesh(), bond, Vector3.Zero, new Vector3(0.9f, 1.2f, 0.9f));
                for (int i = 0; i < 6; i++)
                {
                    float angle = Mathf.Tau * i / 6f;
                    CellVisual3D.AddMesh(_visual, ball, atom, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.9f, Vector3.One * 0.32f);
                }
                break;
            // Short chain
            case FoodKind.Amino:
                for (int i = 0; i < 3; i++)
                {
                    var position = new Vector3((i - 1f) * 1.1f, Mathf.Sin(i * 2f) * 0.3f, 0f);
                    CellVisual3D.AddMesh(_visual, ball, atom, position, Vector3.One * 0.55f);
                }
                break;
            // Head with two fatty acid tails, the rods lie along Y and turn to +X
            case FoodKind.Lipid:
                CellVisual3D.AddMesh(_visual, ball, atom, Vector3.Zero, Vector3.One * 0.7f);
                foreach (float side in new[] { -1f, 1f })
                {
                    MeshInstance3D tail = CellVisual3D.AddMesh(_visual, CellVisual3D.RodMesh(), bond,
                        new Vector3(1.2f, 0f, side * 0.28f), new Vector3(0.12f, 1.8f, 0.12f));
                    tail.Rotation = new Vector3(0f, 0f, -Mathf.Pi * 0.5f);
                }
                break;
        }
    }

    private void BuildSeaFood()
    {
        SphereMesh ball = CellVisual3D.SphereMesh(true);

        switch (Kind)
        {
            // Algae ball: a fuzzy green sphere with darker tufts
            case FoodKind.Sugar:
                CellVisual3D.AddMesh(_visual, CellVisual3D.SphereMesh(), CellVisual3D.Material(ALGAE_COLOR, 0f, 0.95f), Vector3.Zero, Vector3.One * 0.6f);
                StandardMaterial3D tuft = CellVisual3D.Material(ALGAE_COLOR.Darkened(0.3f), 0f, 1f);
                for (int i = 0; i < 9; i++)
                {
                    Vector3 direction = new Vector3(Rand.Range(-1f, 1f), Rand.Range(-1f, 1f), Rand.Range(-1f, 1f)).Normalized();
                    CellVisual3D.AddMesh(_visual, ball, tuft, direction * 0.55f, Vector3.One * Rand.Range(0.1f, 0.18f));
                }
                break;
            // Plankton: a small cloud of glowing specks
            case FoodKind.Amino:
                StandardMaterial3D glow = CellVisual3D.Material(PLANKTON_COLOR, 1.4f, 0.3f);
                for (int i = 0; i < 9; i++)
                {
                    var position = new Vector3(Rand.Range(-0.7f, 0.7f), Rand.Range(-0.4f, 0.4f), Rand.Range(-0.7f, 0.7f));
                    CellVisual3D.AddMesh(_visual, ball, glow, position, Vector3.One * Rand.Range(0.1f, 0.2f));
                }
                break;
            // Fish roe: see-through orange eggs, each with a dark dot
            case FoodKind.Lipid:
                StandardMaterial3D egg = CellVisual3D.Material(ROE_COLOR, 0.15f, 0.2f);
                StandardMaterial3D dot = CellVisual3D.Material(new Color(0.15f, 0.08f, 0.05f), 0f, 0.5f);
                for (int i = 0; i < 7; i++)
                {
                    float   angle    = Mathf.Tau * i / 7f;
                    Vector3 position = 0 == i ? Vector3.Zero : new Vector3(Mathf.Cos(angle), Rand.Range(-0.3f, 0.3f), Mathf.Sin(angle)) * 0.5f;
                    CellVisual3D.AddMesh(_visual, ball, egg, position, Vector3.One * 0.3f);
                    CellVisual3D.AddMesh(_visual, ball, dot, position + new Vector3(0.12f, 0.12f, 0f), Vector3.One * 0.08f);
                }
                break;
        }
    }

    private void BuildLandFood()
    {
        SphereMesh ball  = CellVisual3D.SphereMesh(true);
        Color      color = ColorOf();
        StandardMaterial3D leaf = CellVisual3D.Material(new Color(0.25f, 0.55f, 0.2f), 0f, 0.8f);

        switch (Kind)
        {
            // Fruit: a round, shiny fruit with stalk and leaf
            case FoodKind.Sugar:
                CellVisual3D.AddMesh(_visual, CellVisual3D.SphereMesh(), CellVisual3D.Material(color, 0f, 0.25f), new Vector3(0f, 0.5f, 0f), new Vector3(0.55f, 0.5f, 0.55f));
                CellVisual3D.AddMesh(_visual, CellVisual3D.RodMesh(), CellVisual3D.Material(new Color(0.4f, 0.28f, 0.15f), 0f, 0.9f), new Vector3(0f, 1.05f, 0f), new Vector3(0.04f, 0.25f, 0.04f));
                MeshInstance3D blade = CellVisual3D.AddMesh(_visual, ball, leaf, new Vector3(0.15f, 1.1f, 0f), new Vector3(0.22f, 0.05f, 0.1f));
                blade.Rotation = new Vector3(0f, 0f, 0.4f);
                break;
            // Mushroom: pale stem, red cap with white dots
            case FoodKind.Amino:
                CellVisual3D.AddMesh(_visual, CellVisual3D.RodMesh(), CellVisual3D.Material(new Color(0.95f, 0.92f, 0.85f), 0f, 0.8f), new Vector3(0f, 0.3f, 0f), new Vector3(0.16f, 0.6f, 0.16f));
                CellVisual3D.AddMesh(_visual, CellVisual3D.SphereMesh(), CellVisual3D.Material(color, 0f, 0.5f), new Vector3(0f, 0.65f, 0f), new Vector3(0.55f, 0.3f, 0.55f));
                StandardMaterial3D speck = CellVisual3D.Material(Colors.White, 0f, 0.6f);
                for (int i = 0; i < 5; i++)
                {
                    float angle = Mathf.Tau * i / 5f + 0.3f;
                    CellVisual3D.AddMesh(_visual, ball, speck, new Vector3(Mathf.Cos(angle) * 0.32f, 0.85f, Mathf.Sin(angle) * 0.32f), Vector3.One * 0.07f);
                }
                break;
            // Berries: a cluster of shiny berries between two leaves
            case FoodKind.Lipid:
                StandardMaterial3D berry = CellVisual3D.Material(color, 0.05f, 0.2f);
                for (int i = 0; i < 6; i++)
                {
                    float angle  = Mathf.Tau * i / 6f;
                    float radius = 0 == i % 2 ? 0.3f : 0.18f;
                    CellVisual3D.AddMesh(_visual, ball, berry, new Vector3(Mathf.Cos(angle) * radius, 0.2f + (i % 3) * 0.12f, Mathf.Sin(angle) * radius), Vector3.One * 0.2f);
                }
                foreach (float side in new[] { -1f, 1f })
                {
                    CellVisual3D.AddMesh(_visual, ball, leaf, new Vector3(side * 0.45f, 0.08f, 0f), new Vector3(0.3f, 0.04f, 0.15f));
                }
                break;
        }
    }

    private void BuildMeat()
    {
        StandardMaterial3D meat = CellVisual3D.Material(MEAT_COLOR, 0f, 0.45f);
        StandardMaterial3D fat  = CellVisual3D.Material(new Color(0.98f, 0.88f, 0.8f), 0f, 0.6f);
        float lift = FoodHabitat.Land == Habitat ? 0.35f : 0f;
        CellVisual3D.AddMesh(_visual, CellVisual3D.SphereMesh(), meat, new Vector3(0f, lift, 0f), new Vector3(0.7f, 0.38f, 0.5f));
        CellVisual3D.AddMesh(_visual, CellVisual3D.TorusMesh(), fat, new Vector3(0f, lift + 0.05f, 0f), new Vector3(0.66f, 1.4f, 0.47f));
        MeshInstance3D bone = CellVisual3D.AddMesh(_visual, CellVisual3D.RodMesh(), fat, new Vector3(0.7f, lift, 0f), new Vector3(0.08f, 0.6f, 0.08f));
        bone.Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f);
    }
}
