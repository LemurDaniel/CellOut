// NOTE:
//   The look of a body plan: its custom clay color, the pattern painted in a second
//   color and the dots of spray paint. Pure logic without engine calls, owned by a
//   CreatureDesign: the design turns a hit point on the body into spine share and ring
//   angle (it knows the geometry) and counts the changes of color and pattern.
//
//   var look = new CreatureLook();
//   look.SetPattern(CreaturePattern.Stripes, new Color(0.2f, 0.15f, 0.3f));
//   look.AddSplat(0.5f, Mathf.Pi * 0.5f, 0.12f, Colors.Purple, look.NewStroke());   // a dot on the back

//
//   Chapters:
//     PAINT PATTERN   The patterns the second color is painted in, and spray paint dots.
//     CREATURE LOOK   Body color, pattern, spray paint and copies.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

// ===========================================================================
// ============================   PAINT PATTERN   ============================
// ===========================================================================
//
//   CreaturePattern   How the second color lies on the back and flanks (shaders/clay.gdshaderinc).
//   PaintSplat        One dot of spray paint, stored relative to the spine so it follows reshaping.
//

/// <summary>Paint of the second color: none, stripes across, round spots, big patches or a dark saddle.</summary>
public enum CreaturePattern { None, Stripes, Spots, Patches, Saddle }

/// <summary>One dot of spray paint on the body.</summary>
/// <param name="T">Spine share of its center, 0 tail .. 1 nose (outside on the caps).</param>
/// <param name="Angle">Angle around the ring: 0 right flank (+Z), Pi/2 back, -Pi/2 belly.</param>
/// <param name="Radius">Size of the dot in unit space.</param>
/// <param name="Color">Paint color.</param>
/// <param name="Stroke">Stroke the dot belongs to, undo removes the last stroke.</param>
public readonly record struct PaintSplat(float T, float Angle, float Radius, Color Color, int Stroke);

public sealed class CreatureLook
{
    // ===========================================================================
    // ============================   CREATURE LOOK   ============================
    // ===========================================================================
    //
    //   Colors        Custom body color, the paint pattern and its color.
    //   Spray paint   Dots by spine share and ring angle, strokes and undo.
    //   Copies        Copy from another look.
    //

    // ---------------------------------------------------------------------------
    // Colors
    //   Custom body color, the paint pattern and its color.
    // ---------------------------------------------------------------------------

    /// <summary>Custom clay color, null keeps Player.ORGANISM_COLOR. Alpha below 1 is translucent like a cell.</summary>
    public Color? Color { get; private set; }

    /// <summary>How the second color is painted on, CreaturePattern.None for a plain body.</summary>
    public CreaturePattern Pattern { get; private set; }

    /// <summary>Second color of the paint, null for a darker shade of the body color.</summary>
    public Color? PatternColor { get; private set; }

    /// <summary>Picks the body color, null goes back to the default.</summary>
    /// <param name="color">(Required) The clay color, null for the default.</param>
    public void SetColor(Color? color)
    {
        Color = color;
    }

    /// <summary>Paints the body: a pattern in a second color, free of charge, looks only.</summary>
    /// <param name="pattern">(Required) The pattern, None for a plain body.</param>
    /// <param name="color">(Required) The second color, null for a darker shade of the body color.</param>
    /// <example><code>look.SetPattern(CreaturePattern.Stripes, new Color(0.2f, 0.15f, 0.3f));</code></example>
    public void SetPattern(CreaturePattern pattern, Color? color)
    {
        Pattern      = pattern;
        PatternColor = color;
    }

    // ---------------------------------------------------------------------------
    // Spray paint
    //   Dots by spine share and ring angle, strokes and undo.
    // ---------------------------------------------------------------------------

    /*
      Spray paint: dots stored by spine share and angle around the ring, so they stay on
      the skin when the body is reshaped. Only PaintVersion counts their changes: the
      visual then updates the shader dots, the meshes stay (spraying is fast).
      The shader holds MAX_SPLATS dots (shaders/clay.gdshaderinc), a full body sprays no more.
    */
    public const int MAX_SPLATS = 160;
    public const float MIN_SPLAT_RADIUS = 0.04f;
    public const float MAX_SPLAT_RADIUS = 0.35f;

    private readonly List<PaintSplat> _splats = new();
    private int _lastStroke;

    public IReadOnlyList<PaintSplat> Splats => _splats;

    /// <summary>Counts every spray change, the visual updates the paint on it.</summary>
    public int PaintVersion { get; private set; }

    /// <summary>A new stroke id, the dots of one drag share it.</summary>
    public int NewStroke()
    {
        return ++_lastStroke;
    }

    /// <summary>Sprays one dot at a spine share and ring angle.</summary>
    /// <param name="t">(Required) Spine share of the center, 0 tail .. 1 nose.</param>
    /// <param name="angle">(Required) Angle around the ring: 0 right flank, Pi/2 back, -Pi/2 belly.</param>
    /// <param name="radius">(Required) Dot size, clamped to MIN..MAX_SPLAT_RADIUS.</param>
    /// <param name="color">(Required) Paint color.</param>
    /// <param name="stroke">(Required) Stroke id from NewStroke.</param>
    /// <returns>False when MAX_SPLATS dots are on the body already.</returns>
    public bool AddSplat(float t, float angle, float radius, Color color, int stroke)
    {
        if (_splats.Count >= MAX_SPLATS)
        {
            return false;
        }
        _splats.Add(new PaintSplat(t, angle, Mathf.Clamp(radius, MIN_SPLAT_RADIUS, MAX_SPLAT_RADIUS), color, stroke));
        PaintVersion++;
        return true;
    }

    /// <summary>Removes the dots of the last stroke.</summary>
    /// <returns>False when there is no paint.</returns>
    public bool UndoStroke()
    {
        if (0 == _splats.Count)
        {
            return false;
        }
        int last = _splats.Max(splat => splat.Stroke);
        _splats.RemoveAll(splat => last == splat.Stroke);
        PaintVersion++;
        return true;
    }

    /// <summary>Wipes all spray paint.</summary>
    public void ClearPaint()
    {
        _splats.Clear();
        PaintVersion++;
    }

    // ---------------------------------------------------------------------------
    // Copies
    //   Copy from another look.
    // ---------------------------------------------------------------------------

    /// <summary>Replaces colors, pattern and spray paint with those of another look.</summary>
    /// <param name="other">(Required) The look copied.</param>
    public void CopyFrom(CreatureLook other)
    {
        Color        = other.Color;
        Pattern      = other.Pattern;
        PatternColor = other.PatternColor;
        _splats.Clear();
        _splats.AddRange(other._splats);
        _lastStroke = other._lastStroke;
        PaintVersion++;
    }
}
