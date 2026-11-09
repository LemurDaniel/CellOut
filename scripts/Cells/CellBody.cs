// NOTE:
//   Base of every cell: movement, facing and the visual traits unlocked by mutations.
//   A body is one cell or, after divisions, a sticky cluster of CellCount equal cells,
//   or an organism shaped from clay (Creature, drawn by CreatureVisual3D).
//   The game logic runs in 2D on the plane, a CellVisual3D child renders the body in 3D.
//   Player and Critter derive from it.

//
//   Chapters:
//     DATA TYPES    Cells of a body and organelles.
//     CELL BODY     State, lifecycle and the layout handed to CellVisual3D.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

// ===========================================================================
// =============================   DATA TYPES   ==============================
// ===========================================================================
//
//   CellPart    One cell of a body in unit space, read by CellVisual3D.
//   Organelle   One organelle inside a cell in unit space.
//

/// <summary>One cell of a body in unit space (cell radius 1, +X forward, Y up for the dome of a cluster).</summary>
/// <param name="Position">Center of the cell in unit space.</param>
/// <param name="Index">Place in the cluster, 0 is the first cell (the only one of a single cell).</param>
public readonly record struct CellPart(Vector3 Position, int Index)
{
    public bool IsFirst => 0 == Index;
}

/// <summary>One organelle in unit space, Y is the height inside the flattened cell.</summary>
public readonly record struct Organelle(Vector2 Position, float Y, float Size, Color Color);

public partial class CellBody : Node2D
{
    // ===========================================================================
    // ==============================   CELL BODY   ==============================
    // ===========================================================================
    //
    //   State        Size, movement and the visual traits.
    //   Lifecycle    Creates the 3D visual (cell or creature), decays the hurt flash and turns towards the movement.
    //   Layout       Swim speed, the cluster layout for CellVisual3D and the body size.
    //

    // ---------------------------------------------------------------------------
    // State
    //   Size, movement and the visual traits.
    // ---------------------------------------------------------------------------

    public const float REFERENCE_SIZE = 14f;        // size at which speed and damage are 1:1
    private const float BASE_SWIM_SPEED = 112.5f;   // plane units per second at REFERENCE_SIZE
    private const float SWIM_SPEED_EXPONENT = 0.85f;
    private const int MIN_ORGANELLES = 3;
    private const int MAX_ORGANELLES = 5;
    private const float FACING_SPEED = 6f;
    private const float HURT_FLASH_DECAY = 3f;

    public float Radius { get; set; } = 14f;
    public Color BodyColor { get; set; } = new(0.45f, 0.85f, 0.65f);
    public Vector2 Velocity { get; set; } = Vector2.Zero;
    public float Facing { get; set; }

    // Visual traits of critters, the player uses its Design instead
    public bool IsProtocell { get; set; }
    public int Flagella { get; set; }
    public bool HasCilia { get; set; }
    public bool HasSpikes { get; set; }
    public bool HasChloroplasts { get; set; }
    public int EyeCount { get; set; } = 1;
    public float HurtFlash { get; set; }
    public int Threat { get; set; }           // -1 edible, 0 neutral, 1 dangerous (ring around the cell)
    public bool IsGhost { get; set; }         // blinks, e.g. while invulnerable
    public bool IsHidden { get; set; }        // not rendered at all, e.g. the player during the title menu
    public bool IsPartsOnly { get; set; }     // renders only the placed parts, no core cell (editor drag icon and ghost)
    public bool IsOnLand { get; set; }        // set by Main at the coast, creatures walk or crawl there

    // Inner life, LookVersion counts up when it changes so the visual rebuilds
    public float AnimSeed { get; private set; }
    public int LookVersion { get; private set; }
    public List<Organelle> Organelles { get; } = new();
    public CellVisual3D Visual { get; private set; }

    /// <summary>Placed editor parts, every cell of the cluster carries them, empty for critters.</summary>
    public CellDesign Design { get; set; } = new();

    /// <summary>Cells of the body: 1, after each division twice as many (player only).</summary>
    public int CellCount { get; set; } = 1;

    /// <summary>Body plan of an organism, null for a cell. Set before AddChild or followed by ShowCreature.</summary>
    public CreatureDesign Creature { get; set; }
    public CreatureVisual3D CreatureVisual { get; private set; }

    // ---------------------------------------------------------------------------
    // Lifecycle
    //   Creates the 3D visual (cell or creature), decays the hurt flash and turns towards the movement.
    // ---------------------------------------------------------------------------

    public override void _Ready()
    {
        AnimSeed = GD.Randf() * 100f;
        RegenOrganelles();
        // The visual keeps wiggling while the game is paused (title menu, editor)
        Visual = new CellVisual3D { Body = this, ProcessMode = ProcessModeEnum.Always };
        AddChild(Visual);
        if (null != Creature)
        {
            ShowCreature();
        }
    }

    /// <summary>Renders the body as the organism of its Creature, the cell visual hides itself.</summary>
    public void ShowCreature()
    {
        if (null != CreatureVisual)
        {
            return;
        }
        CreatureVisual = new CreatureVisual3D { Body = this, ProcessMode = ProcessModeEnum.Always };
        AddChild(CreatureVisual);
    }

    /// <summary>Rolls a new set of organelles, e.g. when the nucleus forms.</summary>
    public void RegenOrganelles()
    {
        Organelles.Clear();
        int count = MIN_ORGANELLES + Rand.Index(MAX_ORGANELLES - MIN_ORGANELLES + 1);
        for (int i = 0; i < count; i++)
        {
            Color color = Rand.Chance(0.5f) ? new Color(0.95f, 0.72f, 0.3f) : new Color(0.62f, 0.4f, 0.75f);
            Organelles.Add(new Organelle(
                Vector2.FromAngle(Rand.Angle()) * Rand.Range(0.3f, 0.6f),
                Rand.Range(-0.12f, 0.12f),
                Rand.Range(0.07f, 0.12f),
                color));
        }
        LookVersion++;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        HurtFlash = Mathf.Max(0f, HurtFlash - dt * HURT_FLASH_DECAY);
        if (Velocity.LengthSquared() > 4f)
        {
            Facing = Mathf.LerpAngle(Facing, Velocity.Angle(), Mathf.Clamp(dt * FACING_SPEED, 0f, 1f));
        }
    }

    // ---------------------------------------------------------------------------
    // Layout
    //   Swim speed, the cluster layout for CellVisual3D and the body size.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Full swim speed for a body of the given size, the one formula for player and critters.
    /// Grows a little slower than the size, so big cells feel heavier.
    /// </summary>
    /// <param name="size">(Required) Body size on the plane.</param>
    /// <returns>Speed in plane units per second, BASE_SWIM_SPEED at REFERENCE_SIZE.</returns>
    /// <example><code>CellBody.SwimSpeed(CellBody.REFERENCE_SIZE)  // 112.5</code></example>
    public static float SwimSpeed(float size)
    {
        return BASE_SWIM_SPEED * Mathf.Pow(size / REFERENCE_SIZE, SWIM_SPEED_EXPONENT);
    }

    private const float CLUSTER_SPACING = 1.1f;      // sunflower spacing in cell radii, neighbors overlap a little (sticky)
    private const float PAIR_OFFSET = 0.85f;         // 2 cells side by side, 4 cells in a square
    private static readonly float GOLDEN_ANGLE = Mathf.Pi * (3f - Mathf.Sqrt(5f));
    private static readonly Dictionary<int, Vector3[]> _clusters = new();

    /// <summary>
    /// Cell centers of a cluster in unit space (cell radius 1). 1 cell sits in the middle,
    /// 2 side by side, 4 in a square, more on a sunflower spiral: cell i at radius
    /// CLUSTER_SPACING * sqrt(i + 0.5) and angle i * golden angle. Bigger clusters bulge
    /// into a dome (Y) like a morula. Cell i of a cluster lies near cell i / 2 of the half
    /// as big cluster, the division animation relies on that.
    /// </summary>
    /// <param name="count">(Required) Number of cells, at least 1.</param>
    /// <example><code>CellBody.ClusterLayout(2)  // (0, 0, -0.85), (0, 0, 0.85)</code></example>
    public static Vector3[] ClusterLayout(int count)
    {
        if (count < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(count), $"count must be at least 1, got '{count}'.");
        }
        if (_clusters.TryGetValue(count, out Vector3[] cached))
        {
            return cached;
        }

        var cells = new Vector3[count];
        switch (count)
        {
            case 1:
                break;
            case 2:
                cells[0] = new Vector3(0f, 0f, -PAIR_OFFSET);
                cells[1] = new Vector3(0f, 0f, PAIR_OFFSET);
                break;
            case 4:
                cells[0] = new Vector3(-PAIR_OFFSET, 0.15f, -PAIR_OFFSET);
                cells[1] = new Vector3(PAIR_OFFSET, 0.15f, -PAIR_OFFSET);
                cells[2] = new Vector3(-PAIR_OFFSET, 0.15f, PAIR_OFFSET);
                cells[3] = new Vector3(PAIR_OFFSET, 0.15f, PAIR_OFFSET);
                break;
            default:
                float outer = CLUSTER_SPACING * Mathf.Sqrt(count - 0.5f) + 1f;
                float dome  = count >= 8 ? 0.7f : 0.3f;
                for (int i = 0; i < count; i++)
                {
                    float radius = CLUSTER_SPACING * Mathf.Sqrt(i + 0.5f);
                    float angle  = i * GOLDEN_ANGLE;
                    float height = dome * (1f - radius * radius / (outer * outer));
                    cells[i] = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
                }
                break;
        }
        _clusters[count] = cells;
        return cells;
    }

    /// <summary>Distance from the cluster center to its outer edge in cell radii, 1 for a single cell.</summary>
    public static float ClusterExtent(int count)
    {
        float extent = 0f;
        foreach (Vector3 cell in ClusterLayout(count))
        {
            extent = Mathf.Max(extent, new Vector2(cell.X, cell.Z).Length());
        }
        return extent + 1f;
    }

    /// <summary>The cells of this body in unit space (cell radius 1, +X forward).</summary>
    public List<CellPart> CellLayout()
    {
        var parts = new List<CellPart>();
        Vector3[] cells = ClusterLayout(CellCount);
        for (int i = 0; i < cells.Length; i++)
        {
            parts.Add(new CellPart(cells[i], i));
        }
        return parts;
    }

    /// <summary>Radius of the whole body, the cluster included. Everything in the world scales with it.</summary>
    public float BodySize()
    {
        return Radius * ClusterExtent(CellCount);
    }
}
