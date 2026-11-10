// NOTE:
//   Tracks how herbivore or carnivore the player plays: eaten plant biomass
//   (molecules, light) against eaten meat (cells and their remains). Old meals fade
//   with a half-life, so the player can still swing over. Owned by Player.
//
//   var tracker = new DietTracker();
//   tracker.Add(BiomassSource.Meat, 20f);
//   float diet = tracker.Value(design.Lean(), hasParts: true);   // -1 herbivore .. 1 carnivore

//
//   Chapters:
//     SOURCES       Where biomass comes from.
//     DIET VALUE    Collects meals, fades them and mixes them with the part lean.
//

using Godot;

namespace CellOut;

// ===========================================================================
// ===============================   SOURCES   ===============================
// ===========================================================================
//
//   BiomassSource   Plant (molecules), Meat (cells), Light (photosynthesis), Raw (no diet, no digestion).
//

public enum BiomassSource { Plant, Meat, Light, Raw }

public sealed class DietTracker
{
    // ===========================================================================
    // =============================   DIET VALUE   ==============================
    // ===========================================================================
    //
    //   Add      Counts a meal towards plant or meat.
    //   Decay    Fades old meals with HALF_LIFE.
    //   Reset    Forgets every meal, e.g. when the nucleus forms.
    //   Value    Mix of eating lean and part lean, -1 .. 1.
    //

    private const float HALF_LIFE = 90f;          // seconds until a meal counts half
    private const float LIGHT_WEIGHT = 0.5f;      // photosynthesis counts half as plant
    private const float EATING_WEIGHT = 0.6f;     // share of the eating lean once parts exist
    private const float NEUTRAL_MASS = 5f;        // damps the value while little was eaten

    private float _plant;
    private float _meat;

    /// <summary>Counts a meal towards plant or meat, Raw is ignored.</summary>
    /// <param name="source">(Required) Where the biomass came from.</param>
    /// <param name="amount">(Required) Gained biomass.</param>
    public void Add(BiomassSource source, float amount)
    {
        switch (source)
        {
            case BiomassSource.Plant:
                _plant += amount;
                break;
            case BiomassSource.Light:
                _plant += amount * LIGHT_WEIGHT;
                break;
            case BiomassSource.Meat:
                _meat += amount;
                break;
        }
    }

    /// <summary>Fades old meals, called once per physics frame.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Decay(float dt)
    {
        float factor = Mathf.Pow(0.5f, dt / HALF_LIFE);
        _plant *= factor;
        _meat  *= factor;
    }

    public void Reset()
    {
        _plant = 0f;
        _meat  = 0f;
    }

    /// <summary>The diet value shown on the bar.</summary>
    /// <param name="partsLean">(Required) CellDesign.Lean() of the current design.</param>
    /// <param name="hasParts">(Required) Without parts only the eating counts.</param>
    /// <returns>-1 pure herbivore .. 0 omnivore .. 1 pure carnivore.</returns>
    /// <example><code>tracker.Value(0f, hasParts: false)  // 0 before the first meal</code></example>
    public float Value(float partsLean, bool hasParts)
    {
        float eatingLean = (_meat - _plant) / (_meat + _plant + NEUTRAL_MASS);
        float value      = hasParts ? Mathf.Lerp(partsLean, eatingLean, EATING_WEIGHT) : eatingLean;
        return Mathf.Clamp(value, -1f, 1f);
    }
}
