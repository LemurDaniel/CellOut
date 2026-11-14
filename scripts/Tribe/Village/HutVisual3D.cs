// NOTE:
//   The look of a hut from its blueprint (HutDesign), in unit size standing on y = 0:
//   the wall (smooth clay, woven wattle, stone blocks), the roof (grass cone, layered
//   reed, stepped shingles), the door and every add-on (storehouse at the side, a market
//   awning in front, a totem pole by the door, a palisade ring, a watchtower). Primitives
//   only, the Blender models come later. Used by Hut in the world and by the HutEditor
//   preview, so both show exactly the same hut.
//
//   var visual = new HutVisual3D();
//   visual.Build(design);
//   AddChild(visual);

//
//   Chapters:
//     BUILD HUT   Wall, roof, door and the add-ons of a design.
//

using Godot;

namespace CellOut;

public partial class HutVisual3D : Node3D
{
    // ===========================================================================
    // ==============================   BUILD HUT   ==============================
    // ===========================================================================
    //
    //   HUT_RADIUS     Radius of the wall in units, every other size follows it.
    //   Build          Clears and rebuilds everything from a design.
    //   BuildWall      Clay, wattle or stone.
    //   BuildRoof      Grass, reed or shingles.
    //   BuildAddons    Storehouse, market, totem, palisade, watchtower.
    //

    public const float HUT_RADIUS = 1f;
    public const float WALL_HEIGHT = 0.85f;
    public const float ROOF_HEIGHT = 0.95f;
    public const float PALISADE_RADIUS = 1.9f;        // the ring of stakes, in HUT_RADIUS units
    public const float TOWER_HEIGHT = 2.6f;

    private static readonly Color CLAY_COLOR = new(0.72f, 0.55f, 0.38f);
    private static readonly Color WATTLE_COLOR = new(0.55f, 0.42f, 0.25f);
    private static readonly Color TWIG_COLOR = new(0.42f, 0.3f, 0.17f);
    private static readonly Color STONE_COLOR = new(0.55f, 0.55f, 0.57f);
    private static readonly Color STONE_DARK = new(0.42f, 0.42f, 0.45f);
    private static readonly Color GRASS_COLOR = new(0.62f, 0.56f, 0.28f);
    private static readonly Color REED_COLOR = new(0.78f, 0.7f, 0.42f);
    private static readonly Color SHINGLE_COLOR = new(0.45f, 0.26f, 0.18f);
    private static readonly Color DOOR_COLOR = new(0.2f, 0.14f, 0.08f);
    private static readonly Color AWNING_COLOR = new(0.85f, 0.35f, 0.3f);
    private static readonly Color TOTEM_COLORS0 = new(0.9f, 0.6f, 0.2f);
    private static readonly Color TOTEM_COLORS1 = new(0.25f, 0.55f, 0.75f);
    private static readonly Color TOTEM_COLORS2 = new(0.85f, 0.85f, 0.85f);

    /// <summary>Clears the hut and builds it again from a design.</summary>
    /// <param name="design">(Required) The blueprint.</param>
    public void Build(HutDesign design)
    {
        foreach (Node child in GetChildren())
        {
            child.QueueFree();
        }
        BuildWall(design.Wall);
        BuildRoof(design.Roof);
        // Door facing +X
        CellVisual3D.AddMesh(this, CellVisual3D.RodMesh(), CellVisual3D.Material(DOOR_COLOR, 0f, 1f), new Vector3(HUT_RADIUS * 0.98f, WALL_HEIGHT * 0.4f, 0f), new Vector3(0.32f, WALL_HEIGHT * 0.8f, 0.32f));
        BuildAddons(design);
    }

    private void BuildWall(WallKind wall)
    {
        switch (wall)
        {
            // Smooth clay cylinder
            case WallKind.Clay:
                CellVisual3D.AddMesh(this, CellVisual3D.RodMesh(), CellVisual3D.Material(CLAY_COLOR, 0f, 0.95f), new Vector3(0f, WALL_HEIGHT * 0.5f, 0f), new Vector3(HUT_RADIUS, WALL_HEIGHT, HUT_RADIUS));
                break;
            // Woven wattle: a darker cylinder with twig rings and upright poles
            case WallKind.Wattle:
                CellVisual3D.AddMesh(this, CellVisual3D.RodMesh(), CellVisual3D.Material(WATTLE_COLOR, 0f, 1f), new Vector3(0f, WALL_HEIGHT * 0.5f, 0f), new Vector3(HUT_RADIUS * 0.96f, WALL_HEIGHT, HUT_RADIUS * 0.96f));
                StandardMaterial3D twig = CellVisual3D.Material(TWIG_COLOR, 0f, 0.9f);
                for (int i = 0; i < 4; i++)
                {
                    CellVisual3D.AddMesh(this, CellVisual3D.TorusMesh(), twig, new Vector3(0f, WALL_HEIGHT * (0.15f + 0.23f * i), 0f), new Vector3(HUT_RADIUS * 0.98f, 0.5f, HUT_RADIUS * 0.98f));
                }
                for (int i = 0; i < 8; i++)
                {
                    float angle = Mathf.Tau * i / 8f + 0.2f;
                    CellVisual3D.AddMesh(this, CellVisual3D.RodMesh(), twig, new Vector3(Mathf.Cos(angle), WALL_HEIGHT * 0.5f, Mathf.Sin(angle)) * HUT_RADIUS, new Vector3(0.07f, WALL_HEIGHT * 1.05f, 0.07f));
                }
                break;
            // Stone: a grey cylinder with blocks bulging out of it
            case WallKind.Stone:
                CellVisual3D.AddMesh(this, CellVisual3D.RodMesh(), CellVisual3D.Material(STONE_COLOR, 0f, 0.9f), new Vector3(0f, WALL_HEIGHT * 0.5f, 0f), new Vector3(HUT_RADIUS, WALL_HEIGHT, HUT_RADIUS));
                StandardMaterial3D block = CellVisual3D.Material(STONE_DARK, 0f, 0.85f);
                for (int i = 0; i < 14; i++)
                {
                    float angle  = Mathf.Tau * i / 14f + (i % 2) * 0.2f;
                    float height = WALL_HEIGHT * (0.2f + 0.3f * (i % 3));
                    CellVisual3D.AddMesh(this, CellVisual3D.SphereMesh(true), block, new Vector3(Mathf.Cos(angle), height, Mathf.Sin(angle)) * HUT_RADIUS * 0.97f, new Vector3(0.22f, 0.14f, 0.22f));
                }
                break;
        }
    }

    private void BuildRoof(RoofKind roof)
    {
        float baseY = WALL_HEIGHT;
        switch (roof)
        {
            // One grass cone
            case RoofKind.Grass:
                CellVisual3D.AddMesh(this, CellVisual3D.ConeMesh(), CellVisual3D.Material(GRASS_COLOR, 0f, 1f), new Vector3(0f, baseY + ROOF_HEIGHT * 0.5f, 0f), new Vector3(HUT_RADIUS * 1.25f, ROOF_HEIGHT, HUT_RADIUS * 1.25f));
                break;
            // Reed: a taller cone with three bound rings
            case RoofKind.Reed:
                float reedHeight = ROOF_HEIGHT * 1.25f;
                CellVisual3D.AddMesh(this, CellVisual3D.ConeMesh(), CellVisual3D.Material(REED_COLOR, 0f, 1f), new Vector3(0f, baseY + reedHeight * 0.5f, 0f), new Vector3(HUT_RADIUS * 1.3f, reedHeight, HUT_RADIUS * 1.3f));
                StandardMaterial3D band = CellVisual3D.Material(TWIG_COLOR, 0f, 0.9f);
                for (int i = 0; i < 3; i++)
                {
                    float share = 0.15f + 0.28f * i;
                    CellVisual3D.AddMesh(this, CellVisual3D.TorusMesh(), band, new Vector3(0f, baseY + reedHeight * share, 0f), new Vector3(HUT_RADIUS * 1.3f * (1f - share), 0.35f, HUT_RADIUS * 1.3f * (1f - share)));
                }
                break;
            // Shingles: four stepped cone layers, dark wood
            case RoofKind.Shingle:
                StandardMaterial3D shingle = CellVisual3D.Material(SHINGLE_COLOR, 0f, 0.7f);
                for (int i = 0; i < 4; i++)
                {
                    float share  = i / 4f;
                    float radius = HUT_RADIUS * 1.3f * (1f - share * 0.75f);
                    float height = ROOF_HEIGHT * 0.4f;
                    CellVisual3D.AddMesh(this, CellVisual3D.ConeMesh(), shingle, new Vector3(0f, baseY + ROOF_HEIGHT * share + height * 0.5f, 0f), new Vector3(radius, height, radius));
                }
                break;
        }
    }

    private void BuildAddons(HutDesign design)
    {
        StandardMaterial3D wood = CellVisual3D.Material(TWIG_COLOR, 0f, 0.9f);

        // Storehouse: a small box at the left side with its own little roof
        if (design.Has(HutAddon.Storehouse))
        {
            var spot = new Vector3(0f, 0f, -HUT_RADIUS * 1.35f);
            CellVisual3D.AddMesh(this, CellVisual3D.RodMesh(), CellVisual3D.Material(CLAY_COLOR.Darkened(0.15f), 0f, 0.95f), spot + new Vector3(0f, WALL_HEIGHT * 0.3f, 0f), new Vector3(HUT_RADIUS * 0.55f, WALL_HEIGHT * 0.6f, HUT_RADIUS * 0.55f));
            CellVisual3D.AddMesh(this, CellVisual3D.ConeMesh(), CellVisual3D.Material(GRASS_COLOR, 0f, 1f), spot + new Vector3(0f, WALL_HEIGHT * 0.6f + 0.25f, 0f), new Vector3(HUT_RADIUS * 0.7f, 0.5f, HUT_RADIUS * 0.7f));
        }

        // Market: two poles and a red awning in front of the door
        if (design.Has(HutAddon.Market))
        {
            float front = HUT_RADIUS * 1.9f;
            foreach (float side in new[] { -0.6f, 0.6f })
            {
                CellVisual3D.AddMesh(this, CellVisual3D.RodMesh(), wood, new Vector3(front, WALL_HEIGHT * 0.45f, side * HUT_RADIUS), new Vector3(0.06f, WALL_HEIGHT * 0.9f, 0.06f));
            }
            CellVisual3D.AddMesh(this, CellVisual3D.RodMesh(), CellVisual3D.Material(AWNING_COLOR, 0f, 0.9f), new Vector3(front - HUT_RADIUS * 0.35f, WALL_HEIGHT * 0.92f, 0f), new Vector3(HUT_RADIUS * 0.8f, 0.05f, HUT_RADIUS * 0.8f));
            CellVisual3D.AddMesh(this, CellVisual3D.RodMesh(), wood, new Vector3(front - HUT_RADIUS * 0.3f, WALL_HEIGHT * 0.35f, 0f), new Vector3(HUT_RADIUS * 0.5f, WALL_HEIGHT * 0.1f, HUT_RADIUS * 0.5f));
        }

        // Totem: a tall pole by the door with three painted rings and a crest
        if (design.Has(HutAddon.Totem))
        {
            var foot = new Vector3(HUT_RADIUS * 1.3f, 0f, HUT_RADIUS * 0.9f);
            float height = WALL_HEIGHT * 2.1f;
            CellVisual3D.AddMesh(this, CellVisual3D.RodMesh(), wood, foot + new Vector3(0f, height * 0.5f, 0f), new Vector3(0.14f, height, 0.14f));
            Color[] rings = { TOTEM_COLORS0, TOTEM_COLORS1, TOTEM_COLORS2 };
            for (int i = 0; i < rings.Length; i++)
            {
                CellVisual3D.AddMesh(this, CellVisual3D.TorusMesh(), CellVisual3D.Material(rings[i], 0.1f, 0.6f), foot + new Vector3(0f, height * (0.3f + 0.22f * i), 0f), new Vector3(0.16f, 0.6f, 0.16f));
            }
            CellVisual3D.AddMesh(this, CellVisual3D.ConeMesh(), CellVisual3D.Material(TOTEM_COLORS0, 0.1f, 0.6f), foot + new Vector3(0f, height + 0.12f, 0f), new Vector3(0.22f, 0.25f, 0.22f));
        }

        // Palisade: a ring of sharpened stakes, a gap at the door
        if (design.Has(HutAddon.Palisade))
        {
            const int STAKES = 18;
            for (int i = 0; i < STAKES; i++)
            {
                float angle = Mathf.Tau * i / STAKES;
                if (Mathf.Abs(Mathf.Wrap(angle, -Mathf.Pi, Mathf.Pi)) < 0.35f)
                {
                    continue;     // the gap in front of the door
                }
                var spot = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * HUT_RADIUS * PALISADE_RADIUS;
                CellVisual3D.AddMesh(this, CellVisual3D.RodMesh(), wood, spot + new Vector3(0f, WALL_HEIGHT * 0.45f, 0f), new Vector3(0.1f, WALL_HEIGHT * 0.9f, 0.1f));
                CellVisual3D.AddMesh(this, CellVisual3D.ConeMesh(), wood, spot + new Vector3(0f, WALL_HEIGHT * 0.9f + 0.08f, 0f), new Vector3(0.1f, 0.16f, 0.1f));
            }
        }

        // Watchtower: a pole with a platform and a small roof behind the hut
        if (design.Has(HutAddon.Watchtower))
        {
            var foot = new Vector3(-HUT_RADIUS * 1.4f, 0f, HUT_RADIUS * 0.6f);
            CellVisual3D.AddMesh(this, CellVisual3D.RodMesh(), wood, foot + new Vector3(0f, TOWER_HEIGHT * 0.5f, 0f), new Vector3(0.12f, TOWER_HEIGHT, 0.12f));
            CellVisual3D.AddMesh(this, CellVisual3D.RodMesh(), wood, foot + new Vector3(0f, TOWER_HEIGHT, 0f), new Vector3(0.45f, 0.06f, 0.45f));
            CellVisual3D.AddMesh(this, CellVisual3D.TorusMesh(), wood, foot + new Vector3(0f, TOWER_HEIGHT + 0.12f, 0f), new Vector3(0.42f, 0.25f, 0.42f));
            CellVisual3D.AddMesh(this, CellVisual3D.ConeMesh(), CellVisual3D.Material(GRASS_COLOR, 0f, 1f), foot + new Vector3(0f, TOWER_HEIGHT + 0.5f, 0f), new Vector3(0.5f, 0.4f, 0.5f));
        }
    }
}
