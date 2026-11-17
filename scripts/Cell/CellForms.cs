// NOTE:
//   Body forms of the NPC cells: the membrane takes any outline and height profile
//   (CellDesign.SetShape / SetHeights), the NPCs pick one from a set of base forms per
//   kind (rods, commas, teardrops, lobes, beans, discs) and wobble it a little, so no
//   two cells look alike. Pure logic without the engine: the caller hands in the
//   random generator, tests/unit checks the forms with a fixed seed.
//
//   CellForm form = CellForms.For(CritterKind.Bacterium, new System.Random(seed));
//   design.SetShape(form.Shape);
//   design.SetHeights(form.Heights);

//
//   Chapters:
//     TYPES        One rolled form.
//     BASE FORMS   The outlines every form starts from.
//     ROLL FORMS   Picks a base form by kind, mirrors, wobbles and evens it out, heights.
//

using System;
using System.Collections.Generic;
using System.Linq;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   CellForm   Outline radii and height factors, SHAPE_POINTS each.
//

/// <summary>A rolled body form: index 0 forward, counting like the part angles (CellDesign).</summary>
/// <param name="Shape">Radii of the outline, 1 = round.</param>
/// <param name="Heights">Height factors of the membrane, 1 = CellVisual3D.FLATTEN.</param>
public readonly record struct CellForm(float[] Shape, float[] Heights);

public static class CellForms
{
    // ===========================================================================
    // ============================   BASE FORMS   ===============================
    // ===========================================================================
    //
    //   ROD       Bacillus: long, slim flanks.
    //   COMMA     Vibrio: long and bent to one side.
    //   TEARDROP  Pointed front, round back, a hunter on the chase.
    //   BEAN      Kidney: one flank dented.
    //   DISC      Wider than long, swims sideways like a coin.
    //   LUMPY     Four bulges, a knobbly ball.
    //   ROUND     The plain cell, only the wobble shapes it.
    //
    //   Index: 0 front, 2 left flank, 4 back, 6 right flank (45° each).
    //

    private static readonly float[] ROD      = { 1.45f, 1.0f, 0.74f, 1.0f, 1.45f, 1.0f, 0.74f, 1.0f };
    private static readonly float[] COMMA    = { 1.4f, 1.15f, 0.8f, 0.74f, 1.3f, 0.92f, 0.74f, 1.02f };
    private static readonly float[] TEARDROP = { 1.45f, 1.05f, 0.88f, 0.96f, 1.0f, 0.96f, 0.88f, 1.05f };
    private static readonly float[] BEAN     = { 1.22f, 1.0f, 0.72f, 1.0f, 1.22f, 1.18f, 1.12f, 1.18f };
    private static readonly float[] DISC     = { 0.8f, 1.08f, 1.38f, 1.08f, 0.8f, 1.08f, 1.38f, 1.08f };
    private static readonly float[] LUMPY    = { 1.3f, 0.86f, 1.3f, 0.86f, 1.3f, 0.86f, 1.3f, 0.86f };
    private static readonly float[] ROUND    = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

    // ===========================================================================
    // ============================   ROLL FORMS   ===============================
    // ===========================================================================
    //
    //   For        1. Base       Picks a base form by kind (weights below), amoebas may roll free lobes.
    //              2. Wobble     Mirrors left and right by chance, jitters every radius, evens out the mean.
    //              3. Heights    A random base height, a hump or a flat snout, a little jitter.
    //

    private const float WOBBLE = 0.1f;            // radius jitter per point
    private const float HEIGHT_WOBBLE = 0.12f;
    private const float LOBE_MIN = 0.75f;         // free lobes of an amoeba
    private const float LOBE_MAX = 1.42f;

    /*
      Base form by kind (weights):
      - Bacterium:  rod 4, comma 3, teardrop 2, round 1
      - Amoeba:     free lobes 5, bean 2, disc 2, lumpy 1
      - Hunter:     teardrop 5, rod 2, lumpy 2, comma 1
      - Colony:     round 3, disc 1, bean 1, lumpy 1 (every cell of the cluster carries it)
    */
    private static readonly Dictionary<CritterKind, (float[] Form, int Weight)[]> BASES = new()
    {
        [CritterKind.Bacterium] = new[] { (ROD, 4), (COMMA, 3), (TEARDROP, 2), (ROUND, 1) },
        [CritterKind.Amoeba]    = new[] { ((float[])null, 5), (BEAN, 2), (DISC, 2), (LUMPY, 1) },
        [CritterKind.Hunter]    = new[] { (TEARDROP, 5), (ROD, 2), (LUMPY, 2), (COMMA, 1) },
        [CritterKind.Colony]    = new[] { (ROUND, 3), (DISC, 1), (BEAN, 1), (LUMPY, 1) },
    };

    /// <summary>A random body form for an NPC cell of the given kind.</summary>
    /// <param name="kind">(Required) Sets the pool of base forms.</param>
    /// <param name="random">(Required) The generator, a fixed seed gives the same form.</param>
    /// <returns>Outline and heights, every value inside the limits of CellDesign.</returns>
    /// <example><code>CellForms.For(CritterKind.Hunter, new System.Random(1)).Shape[0]   // the pointed front</code></example>
    public static CellForm For(CritterKind kind, Random random)
    {
        if (!BASES.TryGetValue(kind, out (float[] Form, int Weight)[] bases))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), $"kind must be a known CritterKind, got '{kind}'.");
        }

        // ---------------------------------------------------------------------------
        // Base
        //   Picks a base form by kind (weights below), amoebas may roll free lobes.
        // ---------------------------------------------------------------------------

        int     roll = random.Next(bases.Sum(entry => entry.Weight));
        float[] form = null;
        foreach ((float[] candidate, int weight) in bases)
        {
            if (roll < weight)
            {
                form = candidate ?? FreeLobes(random);
                break;
            }
            roll -= weight;
        }
        float[] shape = (float[])form.Clone();

        // ---------------------------------------------------------------------------
        // Wobble
        //   Mirrors left and right by chance, jitters every radius, evens out the mean.
        // ---------------------------------------------------------------------------

        // Mirror: index i swaps with 8 - i (left flank <-> right flank), front and back stay
        if (random.NextDouble() < 0.5)
        {
            for (int i = 1; i < CellDesign.SHAPE_POINTS / 2; i++)
            {
                (shape[i], shape[CellDesign.SHAPE_POINTS - i]) = (shape[CellDesign.SHAPE_POINTS - i], shape[i]);
            }
        }
        for (int i = 0; i < shape.Length; i++)
        {
            shape[i] += Jitter(random, WOBBLE);
        }
        // The mean radius stays about 1, so the cell keeps the size its contacts use
        float mean = shape.Average();
        for (int i = 0; i < shape.Length; i++)
        {
            shape[i] = Math.Clamp(shape[i] / mean, CellDesign.MIN_SHAPE, CellDesign.MAX_SHAPE);
        }

        // ---------------------------------------------------------------------------
        // Heights
        //   A random base height, a hump or a flat snout, a little jitter.
        // ---------------------------------------------------------------------------

        float baseHeight = 0.75f + (float)random.NextDouble() * 0.6f;      // pancake .. plump
        float hump       = Jitter(random, 0.35f);                           // > 0: high back, < 0: high front
        var   heights    = new float[CellDesign.SHAPE_POINTS];
        for (int i = 0; i < heights.Length; i++)
        {
            float backShare = -MathF.Cos(MathF.Tau * i / CellDesign.SHAPE_POINTS);     // -1 front .. 1 back
            heights[i] = Math.Clamp(baseHeight + hump * backShare + Jitter(random, HEIGHT_WOBBLE), CellDesign.MIN_HEIGHT, CellDesign.MAX_HEIGHT);
        }
        return new CellForm(shape, heights);
    }

    /// <summary>Free lobes of an amoeba: every radius on its own.</summary>
    private static float[] FreeLobes(Random random)
    {
        var lobes = new float[CellDesign.SHAPE_POINTS];
        for (int i = 0; i < lobes.Length; i++)
        {
            lobes[i] = LOBE_MIN + (float)random.NextDouble() * (LOBE_MAX - LOBE_MIN);
        }
        return lobes;
    }

    /// <summary>A uniform jitter between -amount and amount.</summary>
    private static float Jitter(Random random, float amount)
    {
        return ((float)random.NextDouble() * 2f - 1f) * amount;
    }
}
