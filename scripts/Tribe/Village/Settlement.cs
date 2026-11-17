// NOTE:
//   The fixed ground plan of a settlement (wish of the user 2026-11-17: no freely placed
//   buildings any more): around the fire lies one round foundation with building plots
//   on two rings, every plot looks at the fire. The player's tribe and the neighbor tribes
//   build on the same plan; the headquarters of a city stands in the middle, on the fire.
//   Counts and radii are in data/balance.json (tribe.village.slot*). Pure logic, no engine.
//
//   IReadOnlyList<SettlementSlot> plots = Settlement.SlotsAround(fire, coast.Unit);
//   float foundation = Settlement.Radius(coast.Unit);

//
//   Chapters:
//     PLAN   The plots around a fire, the size of the foundation, its ways and its wall.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

/// <summary>One building plot of a settlement.</summary>
/// <param name="Spot">Plane point of its middle.</param>
/// <param name="Turn">Turn of a building on it around the vertical axis in radians (Hut.Turn): its front looks at the fire.</param>
public readonly record struct SettlementSlot(Vector2 Spot, float Turn);

public static class Settlement
{
    // ===========================================================================
    // =================================   PLAN   ================================
    // ===========================================================================
    //
    //   SlotsAround    Every plot around a fire: the inner ring first, then the outer one.
    //   Radius         World units of the round foundation that holds every plot.
    //   GateAngles     Plane angles of the gates, a way runs from the fire through each.
    //   WallRadius     World units from the fire to the low wall.
    //   PlazaRadius    World units of the round place around the fire.
    //   RingWay        World units from the fire to the ring way between the plots.
    //   WayHalfWidth   World units of half the width of a way.
    //

    private const float YARD = 8.75f;                // Units of foundation beyond the middle of the outer plots: their buildings and a path around
    private const float START_ANGLE = 0.4f;          // the first plot of the inner ring, so no plot lies straight on an axis
    private const float WALL_INSET = 2f;             // Units the wall stands inside the edge of the foundation
    private const float PLAZA_SHARE = 0.5f;          // radius of the place around the fire against the inner ring of plots
    private const float WAY_HALF_WIDTH = 1.6f;       // Units, fits between the two rings of plots

    /// <summary>Angles of the gates against START_ANGLE: each lies between two inner and two outer plots, two pairs across the fire.</summary>
    private static readonly float[] GATE_TURNS = { 0.6f, 2.55f, 0.6f + Mathf.Pi, 2.55f + Mathf.Pi };

    private static VillageBalance Tune => Balance.Current.Tribe.Village;

    /*
      Two rings around the fire:
      - SlotInnerCount plots at SlotInnerRadius, evenly spread.
      - SlotOuterCount plots at SlotOuterRadius, turned by half a step against the inner ring,
        so no building hides the one behind it.
      A building on a plot is turned so its front (+X of its blocks) looks at the fire.
    */
    /// <summary>Every building plot around a fire, the inner ring first.</summary>
    /// <param name="fire">(Required) Plane point of the fire.</param>
    /// <param name="unit">(Required) World units of one coast unit.</param>
    /// <example><code>Settlement.SlotsAround(Vector2.Zero, 1f).Count   // SlotInnerCount + SlotOuterCount</code></example>
    public static IReadOnlyList<SettlementSlot> SlotsAround(Vector2 fire, float unit)
    {
        var slots = new List<SettlementSlot>();
        AddRing(slots, fire, Tune.SlotInnerCount, Tune.SlotInnerRadius * unit, START_ANGLE);
        AddRing(slots, fire, Tune.SlotOuterCount, Tune.SlotOuterRadius * unit, START_ANGLE + Mathf.Pi / Mathf.Max(Tune.SlotOuterCount, 1));
        return slots;
    }

    private static void AddRing(List<SettlementSlot> slots, Vector2 fire, int count, float radius, float start)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = start + Mathf.Tau * i / count;
            // The plane angle towards the fire is angle + Pi; a turn around the upright axis runs the other way round
            slots.Add(new SettlementSlot(fire + Vector2.FromAngle(angle) * radius, -(angle + Mathf.Pi)));
        }
    }

    /// <summary>World units of the round foundation around the fire that holds every plot.</summary>
    /// <param name="unit">(Required) World units of one coast unit.</param>
    public static float Radius(float unit)
    {
        return (Tune.SlotOuterRadius + YARD) * unit;
    }

    /*
      Ways and wall, drawn by the ground shader (coast_ground.gdshader) and Village/SettlementWall.cs:
      - a round place around the fire (PlazaRadius),
      - one ring way between the two rings of plots (RingWay), the fronts of the outer plots lie on it,
      - GATE_TURNS.Length straight ways from the place out through the gates of the wall (GateAngles),
        laid between the plots of 6 inner and 10 outer ones,
      - the low wall a little inside the edge of the foundation (WallRadius).
    */
    /// <summary>Plane angles of the gates in the wall, a straight way runs from the fire through each.</summary>
    /// <example><code>Settlement.GateAngles().Length   // 4</code></example>
    public static float[] GateAngles()
    {
        var angles = new float[GATE_TURNS.Length];
        for (int i = 0; i < angles.Length; i++)
        {
            angles[i] = START_ANGLE + GATE_TURNS[i];
        }
        return angles;
    }

    /// <summary>World units from the fire to the low wall around the settlement.</summary>
    /// <param name="unit">(Required) World units of one coast unit.</param>
    public static float WallRadius(float unit)
    {
        return (Tune.SlotOuterRadius + YARD - WALL_INSET) * unit;
    }

    /// <summary>World units of the round place around the fire.</summary>
    /// <param name="unit">(Required) World units of one coast unit.</param>
    public static float PlazaRadius(float unit)
    {
        return Tune.SlotInnerRadius * PLAZA_SHARE * unit;
    }

    /// <summary>World units from the fire to the middle of the ring way between the two rings of plots.</summary>
    /// <param name="unit">(Required) World units of one coast unit.</param>
    public static float RingWay(float unit)
    {
        return (Tune.SlotInnerRadius + Tune.SlotOuterRadius) * 0.5f * unit;
    }

    /// <summary>World units of half the width of a way.</summary>
    /// <param name="unit">(Required) World units of one coast unit.</param>
    public static float WayHalfWidth(float unit)
    {
        return WAY_HALF_WIDTH * unit;
    }
}
