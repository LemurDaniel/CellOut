// NOTE:
//   Drifting biomolecules of the primordial soup. Logic in 2D, rendered as a 3D molecule.
//   Spawned by Main, eaten when the player touches it.
//
//   var food = new Food();
//   food.Setup(size, FoodKind.Sugar);

//
//   Chapters:
//     SETUP AND DRIFT   Kind, size, value and the drift on the plane.
//     BUILD MOLECULE    Sugar ring, amino chain or lipid with two tails, in unit size.
//

using Godot;

namespace CellOut;

public enum FoodKind { Sugar, Amino, Lipid }

public partial class Food : Node2D
{
    // ===========================================================================
    // ===========================   SETUP AND DRIFT   ===========================
    // ===========================================================================
    //
    //   Setup       Size, kind, biomass value and a random drift.
    //   ColorOf     Color per kind, meat chunks are red.
    //   BaseValue   Biomass per kind before scaling by size.
    //   _Ready      Builds the molecule, see BUILD MOLECULE.
    //   _Process    Drifts on the plane, bobs and spins in 3D.
    //

    public FoodKind Kind { get; private set; } = FoodKind.Sugar;
    public float Size { get; private set; } = 4f;
    public float Value { get; set; } = 1f;

    /// <summary>(Optional) Remains of a cell, count as meat for the diet. Defaults to false (plant).</summary>
    public bool IsMeat { get; set; }
    private Vector2 _drift = Vector2.Zero;
    private float _spin;

    /// <summary>Sets size, kind and value, called before AddChild.</summary>
    /// <param name="size">(Required) Size on the plane.</param>
    /// <param name="kind">(Required) Sugar, amino acid or lipid.</param>
    public void Setup(float size, FoodKind kind)
    {
        Size   = size;
        Kind   = kind;
        Value  = BaseValue(kind) * Mathf.Pow(size / 3.5f, 1.5f) * 0.5f;
        _drift = Vector2.FromAngle(Rand.Angle()) * size * Rand.Range(0.4f, 1.5f);
        _spin  = Rand.Range(-1.5f, 1.5f);
    }

    /// <summary>Color of the molecule (meat chunks are red), also used for the splash when eaten.</summary>
    public Color ColorOf()
    {
        if (IsMeat)
        {
            return MEAT_COLOR;
        }
        return Kind switch
        {
            FoodKind.Sugar => new Color(1f, 0.85f, 0.35f),
            FoodKind.Amino => new Color(0.45f, 0.75f, 1f),
            _              => new Color(1f, 0.55f, 0.75f),
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

    private Node3D _visual;
    private Vector3 _spinAxis = Vector3.Up;
    private const float GROW_IN_SECONDS = 0.4f;

    private float _bob;
    private float _age;

    public override void _Ready()
    {
        BuildMolecule();

        // Placed right away, a molecule spawned during a pause would wait at the origin otherwise
        _visual.GlobalPosition = Plane3D.To3D(GlobalPosition, Mathf.Sin(_bob * 1.3f) * Size * 0.4f - Plane3D.Drop(GlobalPosition));
        _visual.Scale          = Vector3.One * 0.001f;   // grows in, see GrowIn
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Position += _drift * dt;
        _bob     += dt;
        _age     += dt;
        _visual.GlobalPosition = Plane3D.To3D(GlobalPosition, Mathf.Sin(_bob * 1.3f) * Size * 0.4f - Plane3D.Drop(GlobalPosition));
        _visual.Rotate(_spinAxis, _spin * dt);
        _visual.Scale = Vector3.One * Size * GrowIn();
    }

    /// <summary>0 to 1 over the first GROW_IN_SECONDS with a little overshoot, new molecules pop into the soup.</summary>
    private float GrowIn()
    {
        float t = Mathf.Clamp(_age / GROW_IN_SECONDS, 0f, 1f);
        float u = t - 1f;
        return Mathf.Max(1f + 2.70158f * u * u * u + 1.70158f * u * u, 0.001f);
    }

    // ===========================================================================
    // ===========================   BUILD MOLECULE   ============================
    // ===========================================================================
    //
    //   BuildMolecule   Random orientation, then the shape of the kind in unit size.
    //

    private void BuildMolecule()
    {
        _visual = new Node3D { Basis = Basis.FromEuler(new Vector3(Rand.Angle(), Rand.Angle(), 0f)) };
        _spinAxis = new Vector3(Rand.Range(-1f, 1f), 1f, Rand.Range(-1f, 1f)).Normalized();
        _bob      = Rand.Angle();
        AddChild(_visual);

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
}
