// NOTE:
//   The color world of the water per stage: a dim, slate blue primordial soup around the
//   protocell, green-teal water around the single cell, the clear turquoise sea from the
//   cluster on (the coast shaders use that one too, so it stays as it is). Pure data,
//   FollowCamera eases from one palette to the next and hands it to the Backdrop and the lens.
//
//   SoupPalette target = SoupPalette.For(player.Stage);
//   _palette = _palette.Lerp(target, dt * 0.5f);

//
//   Chapters:
//     PALETTE   The colors of one stage, the stage table and the blend.
//

using Godot;

namespace CellOut;

/// <summary>Colors of the water around the player.</summary>
/// <param name="Background">(Required) Clear color and the fog of the depth.</param>
/// <param name="FloorShallow">(Required) Bright patches of the sea floor.</param>
/// <param name="FloorDeep">(Required) Dark patches of the sea floor.</param>
/// <param name="Ambient">(Required) Ambient light on cells and food.</param>
/// <param name="Light">(Required) Light shafts from above and the floating motes.</param>
/// <param name="Caustics">(Required) Light dancing on the sea floor.</param>
public readonly record struct SoupPalette(Color Background, Color FloorShallow, Color FloorDeep, Color Ambient, Color Light, Color Caustics)
{
    // ===========================================================================
    // ===============================   PALETTE   ===============================
    // ===========================================================================
    //
    //   PRIMORDIAL   Protocell: dim slate blue, cold light through thick soup (ochre was tried, too brown).
    //   POND         Single cell: green-teal, life has started to clear the water.
    //   SEA          Cluster and later: the turquoise sea, shared with the coast shaders.
    //   For          The palette of a stage.
    //   Lerp         Blend towards another palette.
    //

    public static readonly SoupPalette PRIMORDIAL = new(
        Background:   new Color(0.05f, 0.09f, 0.15f),
        FloorShallow: new Color(0.17f, 0.25f, 0.33f),
        FloorDeep:    new Color(0.04f, 0.07f, 0.13f),
        Ambient:      new Color(0.66f, 0.76f, 0.90f),
        Light:        new Color(0.78f, 0.86f, 1.00f),
        Caustics:     new Color(0.50f, 0.68f, 0.95f));

    public static readonly SoupPalette POND = new(
        Background:   new Color(0.04f, 0.14f, 0.11f),
        FloorShallow: new Color(0.14f, 0.36f, 0.25f),
        FloorDeep:    new Color(0.03f, 0.12f, 0.09f),
        Ambient:      new Color(0.60f, 0.86f, 0.72f),
        Light:        new Color(0.75f, 1.00f, 0.80f),
        Caustics:     new Color(0.45f, 0.90f, 0.55f));

    public static readonly SoupPalette SEA = new(
        Background:   new Color(0.04f, 0.13f, 0.17f),
        FloorShallow: new Color(0.10f, 0.32f, 0.34f),
        FloorDeep:    new Color(0.03f, 0.11f, 0.15f),
        Ambient:      new Color(0.50f, 0.80f, 0.85f),
        Light:        new Color(0.70f, 0.95f, 0.90f),
        Caustics:     new Color(0.40f, 0.90f, 0.80f));

    /// <summary>The palette of a player stage.</summary>
    /// <param name="stage">(Required) Player.Stage, 0 protocell .. 4 land animal.</param>
    /// <example><code>SoupPalette.For(Player.SINGLE_STAGE)   // POND</code></example>
    public static SoupPalette For(int stage)
    {
        return stage switch
        {
            <= 0                => PRIMORDIAL,
            Player.SINGLE_STAGE => POND,
            _                   => SEA,
        };
    }

    /// <summary>This palette blended towards another one.</summary>
    /// <param name="to">(Required) The target palette.</param>
    /// <param name="weight">(Required) 0 this palette .. 1 the target, clamped.</param>
    public SoupPalette Lerp(SoupPalette to, float weight)
    {
        float t = Mathf.Clamp(weight, 0f, 1f);
        return new SoupPalette(
            Background.Lerp(to.Background, t),
            FloorShallow.Lerp(to.FloorShallow, t),
            FloorDeep.Lerp(to.FloorDeep, t),
            Ambient.Lerp(to.Ambient, t),
            Light.Lerp(to.Light, t),
            Caustics.Lerp(to.Caustics, t));
    }
}
