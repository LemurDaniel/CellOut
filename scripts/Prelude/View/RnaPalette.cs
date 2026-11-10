// NOTE:
//   Colors and shared materials of the RNA prelude (Prelude/RnaWorld.cs): one glowing
//   material per base, one for good and one for bad mutations and the ring of the player
//   head that glows in the wanted color. RnaWorld creates one palette and hands it to its
//   components (strands, nucleotides, HUD), so equal beads share one material.
//
//   var palette = new RnaPalette();
//   bead.MaterialOverride = palette.LinkMaterial(RnaBase.G, mutation: null);
//   palette.ShowWanted(RnaBase.C);   // the ring of the player head turns orange

//
//   Chapters:
//     COLORS      The hues of the bases, strands and mutations.
//     MATERIALS   The shared glowing materials.
//

using Godot;

namespace CellOut;

public sealed class RnaPalette
{
    // ===========================================================================
    // ===============================   COLORS   ================================
    // ===========================================================================
    //
    //   BACKBONE_COLOR   The player strand.
    //   RIVAL_COLOR      The rival strands.
    //   ColorOf          The hue of a base.
    //

    // Same hues as CellVisual3D.DNA_BASES, U takes the blue of T
    private static readonly Color[] BASE_COLORS =
    {
        new(0.98f, 0.85f, 0.35f),   // A
        new(0.45f, 0.8f, 1f),       // U
        new(0.55f, 0.95f, 0.55f),   // G
        new(1f, 0.6f, 0.35f),       // C
    };
    public static readonly Color BACKBONE_COLOR = new(0.95f, 0.55f, 0.75f);
    public static readonly Color RIVAL_COLOR = new(0.6f, 0.5f, 0.95f);
    private static readonly Color GOOD_MUTATION_COLOR = new(0.95f, 1f, 1f);
    private static readonly Color BAD_MUTATION_COLOR = new(1f, 0.25f, 0.3f);

    /// <summary>The hue of a base.</summary>
    /// <param name="kind">(Required) The base.</param>
    /// <example><code>RnaPalette.ColorOf(RnaBase.U)  // light blue</code></example>
    public static Color ColorOf(RnaBase kind)
    {
        return BASE_COLORS[(int)kind];
    }

    // ===========================================================================
    // ==============================   MATERIALS   ==============================
    // ===========================================================================
    //
    //   RnaPalette     Creates the shared materials.
    //   Ring           Material of the ring around the player head.
    //   BaseMaterial   Bead material of a base.
    //   LinkMaterial   Bead material of a link: base color, white for a good mutation, red for a bad one.
    //   ShowWanted     The ring glows in the color of the wanted base.
    //   Glow           A new glowing material.
    //

    private readonly StandardMaterial3D[] _baseMaterials = new StandardMaterial3D[BASE_COLORS.Length];
    private readonly StandardMaterial3D _goodMutationMaterial;
    private readonly StandardMaterial3D _badMutationMaterial;

    public StandardMaterial3D Ring { get; }

    public RnaPalette()
    {
        for (int kind = 0; kind < BASE_COLORS.Length; kind++)
        {
            _baseMaterials[kind] = Glow(BASE_COLORS[kind], 0.5f);
        }
        _goodMutationMaterial = Glow(GOOD_MUTATION_COLOR, 1.6f);
        _badMutationMaterial  = Glow(BAD_MUTATION_COLOR, 1.2f);
        Ring                  = Glow(BASE_COLORS[(int)RnaBase.A], 1.4f);
    }

    /// <summary>Bead material of a base, shared by every bead of that base.</summary>
    /// <param name="kind">(Required) The base.</param>
    public StandardMaterial3D BaseMaterial(RnaBase kind)
    {
        return _baseMaterials[(int)kind];
    }

    /// <summary>Bead material of a link: its base color, white for a good mutation, red for a bad one.</summary>
    /// <param name="kind">(Required) The base of the link.</param>
    /// <param name="mutation">(Required) null for a paired link, else the trait of the mutation.</param>
    public StandardMaterial3D LinkMaterial(RnaBase kind, MutationKind? mutation)
    {
        if (null == mutation)
        {
            return _baseMaterials[(int)kind];
        }
        return Mutation.Get(mutation.Value).IsGood ? _goodMutationMaterial : _badMutationMaterial;
    }

    /// <summary>Colors the ring of the player head in the hue of the wanted base.</summary>
    /// <param name="wanted">(Required) The base the next link needs.</param>
    public void ShowWanted(RnaBase wanted)
    {
        Ring.AlbedoColor = BASE_COLORS[(int)wanted];
        Ring.Emission    = BASE_COLORS[(int)wanted];
    }

    /// <summary>A new glowing material.</summary>
    /// <param name="color">(Required) Albedo and emission color.</param>
    /// <param name="glow">(Required) Emission energy.</param>
    public static StandardMaterial3D Glow(Color color, float glow)
    {
        return new StandardMaterial3D
        {
            AlbedoColor              = color,
            Roughness                = 0.35f,
            EmissionEnabled          = true,
            Emission                 = color,
            EmissionEnergyMultiplier = glow,
        };
    }
}
