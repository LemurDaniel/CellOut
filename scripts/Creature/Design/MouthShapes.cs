// NOTE:
//   The mouth opening every mouth part cuts into the clay head: where the corners sit,
//   how high the mouth line runs, whether it smiles, how wide it gapes, how deep the
//   cavity goes, how fat the lips are and whether the lower jaw juts out. One entry per
//   mouth kind in data/mouths.json. The body mesh carves the opening (Visual/CreatureClay.cs),
//   the model of the mouth (teeth, lips, baleen) is placed into the same opening
//   (Visual/CreatureParts3D.cs), so both always fit together.
//   Parsing calls no engine: Parse and Use take the JSON text. Only the default read of
//   the game goes through Godot (FileAccess).
//
//   MouthShape shape = MouthShapes.For(design);          // PLAIN (a closed line) without a mouth part
//   float cornerX    = shape.CornerX(design);            // where the mouth corners sit
//
//   An entry of data/mouths.json, every number optional:
//     "Jaw": {
//       "length": 0.88,    corners behind the nose tip, in lengths of the head cap (1 = at the last vertebra, more runs back along the flanks)
//       "level": -0.18,    height of the mouth line at the nose, in heights of the head (0 = middle, negative = lower)
//       "smile": 0.0,      how far the line climbs towards the corners, in heights of the head (negative = frown)
//       "gape": 0.12,      half the opening at the nose, in heights of the head (0 = a closed line)
//       "depth": 0.25,     how deep the cavity is cut, in heights of the head
//       "lip": 0.03,       how far the lips bulge out around the opening, in heights of the head
//       "jut": 0.0,        how far the lower jaw is pushed forward, in heights of the head (negative = it sits back under the snout)
//       "teeth": 0,        teeth per row and flank, the game stands them on the lips of the opening (Design/MouthTeeth.cs)
//       "teethRows": "Both",   Both, Upper or Lower
//       "toothLength": 0.12,   root to tip, in heights of the head (never longer than the opening allows)
//       "toothWidth": 0.06,    width along the lip, in heights of the head
//       "toothFlat": 1.0,      thickness as share of the width: 1 a cone or needle, 0.3 a flat shark triangle
//       "toothRagged": 0.0     every second tooth is shorter by this share
//     }

//
//   Chapters:
//     TYPES         The opening of one mouth kind.
//     MOUTH SHAPES  The table of data/mouths.json and the shape of a design.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   MouthShape   The opening a mouth kind cuts into the head.
//

/// <summary>The opening a mouth kind cuts into the head, see the NOTE of this file for the fields.</summary>
public sealed record MouthShape
{
    public float Length { get; init; } = 0.88f;
    public float Level { get; init; } = -0.18f;
    public float Smile { get; init; }
    public float Gape { get; init; } = 0.12f;
    public float Depth { get; init; } = 0.25f;
    public float Lip { get; init; } = 0.03f;
    public float Jut { get; init; }
    public int Teeth { get; init; }
    public TeethRows TeethRows { get; init; } = TeethRows.Both;
    public float ToothLength { get; init; } = 0.12f;
    public float ToothWidth { get; init; } = 0.06f;
    public float ToothFlat { get; init; } = 1f;
    public float ToothRagged { get; init; }

    /// <summary>Even a closed mouth is a thin line: the least half opening, in heights of the head.</summary>
    public const float MIN_GAPE = 0.045f;

    /// <summary>X of the mouth corners: Length head caps behind the nose tip, never behind the middle of the body.</summary>
    /// <param name="design">(Required) The body the mouth sits on.</param>
    public float CornerX(CreatureDesign design)
    {
        float cap = design.NoseTipX - design.SpineEndX;
        return Godot.Mathf.Max(design.NoseTipX - cap * Length, (design.TailTipX + design.NoseTipX) * 0.5f);
    }
}

public static class MouthShapes
{
    // ===========================================================================
    // =============================   MOUTH SHAPES   ============================
    // ===========================================================================
    //
    //   Current   The table of the running game, loaded from FILE_PATH on first use.
    //   Use       Replaces the table, for unit tests that run without Godot.
    //   Parse     The table from the text of the file.
    //   For       The opening of a design: the shape of its mouth part, null without one.
    //

    public const string FILE_PATH = "res://data/mouths.json";

    /// <summary>A body without a mouth part still has a face: a short closed line with a hint of a smile.</summary>
    public static readonly MouthShape PLAIN = new() { Length = 0.95f, Level = -0.24f, Smile = 0.14f, Gape = 0f, Depth = 0.1f, Lip = 0.02f };

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,     // a typo in the file fails instead of being ignored
        Converters             = { new JsonStringEnumConverter() },
    };

    private static IReadOnlyDictionary<CreaturePartKind, MouthShape> _current;

    /// <summary>The table of the running game, loaded from FILE_PATH on first use.</summary>
    public static IReadOnlyDictionary<CreaturePartKind, MouthShape> Current => _current ??= Parse(ReadProjectFile());

    /// <summary>Replaces the table, for unit tests that run without Godot.</summary>
    /// <param name="json">(Required) Content of data/mouths.json.</param>
    /// <example><code>MouthShapes.Use(System.IO.File.ReadAllText("data/mouths.json"));</code></example>
    public static void Use(string json)
    {
        _current = Parse(json);
    }

    /// <summary>Parses and checks the table: every key must be a kind of the Mouths tab.</summary>
    /// <param name="json">(Required) Content of data/mouths.json.</param>
    public static IReadOnlyDictionary<CreaturePartKind, MouthShape> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("json must be the content of data/mouths.json, got an empty text.", nameof(json));
        }
        Dictionary<CreaturePartKind, MouthShape> table;
        try
        {
            table = JsonSerializer.Deserialize<Dictionary<CreaturePartKind, MouthShape>>(json, JSON_OPTIONS);
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"data/mouths.json must be valid JSON with known kinds and fields: {error.Message}", error);
        }
        CreaturePartKind? stranger = table.Keys.Cast<CreaturePartKind?>().FirstOrDefault(kind => CreatureCategory.Mouths != CreatureCatalog.Get(kind.Value).Category);
        if (null != stranger)
        {
            throw new InvalidOperationException($"data/mouths.json may only list kinds of the Mouths tab, got '{stranger}'.");
        }
        return table;
    }

    /// <summary>The opening the mouth part of a design cuts into its head.</summary>
    /// <param name="design">(Required) The body plan.</param>
    /// <returns>The shape of its mouth kind, PLAIN without a mouth part, null when its kind cuts no opening (a beak, a tube).</returns>
    public static MouthShape For(CreatureDesign design)
    {
        // A bare face (CreatureDesign.HasMouthLine off) cuts nothing, whatever mouth part it carries
        if (!design.HasMouthLine)
        {
            return null;
        }
        foreach (CreaturePart part in design.Parts)
        {
            // A mouth on a limb joint cuts into its own little head there, not into the body
            if (CreatureCategory.Mouths == part.Info.Category && !CreatureDesign.IsOnBud(part))
            {
                return Current.TryGetValue(part.Kind, out MouthShape shape) ? shape : null;
            }
        }
        return design.HasMouthLine ? PLAIN : null;
    }

    /// <summary>Reads the file through Godot, so it also works from an exported pack.</summary>
    private static string ReadProjectFile()
    {
        string json = Godot.FileAccess.GetFileAsString(FILE_PATH);
        if (string.IsNullOrEmpty(json))
        {
            throw new InvalidOperationException($"{FILE_PATH} must exist and be readable, Godot reported '{Godot.FileAccess.GetOpenError()}'.");
        }
        return json;
    }
}
