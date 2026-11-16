// NOTE:
//   The template library of the creature editor: ready-made animals the clay can become
//   in the first shaping, shown as cards where the choice between fish and marine mammal
//   used to be. Every template is one file in data/creatures/ (JSON): type (look and
//   swim motion only), spine, color, pattern and parts with their pose. The part at the
//   tail tip and the eyes may be marked free like on the starter bodies, every other part is paid with
//   its normal evolution points, so a template is a shortcut, never a gift: building
//   the same animal by hand costs the same.
//   Parsing calls no engine: Parse and Use take the JSON text. Only the default read of
//   the game goes through Godot (DirAccess, FileAccess).
//
//   CreatureTemplate shark = CreatureTemplates.Get("SHARK");
//   CreatureDesign   body  = shark.Build();
//   int              cost  = CreatureTemplates.CostOf(body);     // what its paid parts cost
//
//   A template file (data/creatures/shark.json), optional fields marked:
//   {
//     "key": "SHARK",                  name in UPPER_SNAKE, translated as CREATURE_TEMPLATE_SHARK
//     "order": 50,                     position of the card, low first
//     "type": "Fish",                  Fish or MarineMammal: cross section and swim motion
//     "girths": [0.3, 0.5, 0.75],      girth per vertebra, tail first (4 to 9 vertebrae)
//     "lifts": [0, 0, 0.2],            (optional) height of the spine per vertebra
//     "sways": [0.4, 0.1, 0],          (optional) sideways offset per vertebra (+ is the right flank): the body curls, step between neighbors up to 0.38
//     "flats": [0, 0, 0.8],            (optional) cross-section per vertebra on top of "flat": 1 flat and wide, -1 tall and narrow (a flat head on a round trunk)
//     "gaps": [0.36, 0.2],            (optional) distance between neighbors, one less than girths, 0.162 to 0.648 (default 0.36):
//                                      short gaps make a ball or let the spine climb steeply
//     "color": "#6b859e",              (optional) body color, else the color of the player
//     "pattern": "Saddle",             (optional) None, Stripes, Spots, Patches, Saddle or Net (dark fields with thin light lines between them)
//     "patternWrap": true,             (optional) the pattern runs all around, belly and legs too, instead of fading out towards the belly
//     "pitch": 90,                     (optional) posture of the whole body in degrees: 0 level, 90 upright with the nose up, -90 nose down
//     "flat": 0.8,                     (optional) cross-section: 0 as the type gives it, 1 pressed flat and wide, -1 tall and narrow
//     "hover": 0.6,                    (optional) how high the body floats above the ground on land, in body sizes (0 to 1.5, default 0: its lowest part rests on the ground)
//     "bellyColor": "#f2ead0",         (optional) color zones on top of the body color: the underside (fading up the flanks),
//     "headColor": "#d05a2c",          the head, "limbColor" every limb with its foot or hand, "tailColor" the tail end
//     "skin": "Scales",                (optional) Smooth, Scales (reptile) or Warts (toad)
//     "patternColor": "#f2f2eb",       (optional) second color, else a dark shade of the body
//     "patternScale": 2.2,             (optional) how fine the pattern is, 0.4 to 4 (default 1): 2 gives twice as many stripes, spots or patches
//     "mouthLine": false,              (optional) a body without a mouth part shows a plain closed mouth line; false leaves the face bare
//     joints: a limb has as many joints as its kind brings (StubLimb 1, BentLimb 2, Limb/Feeler/JointedLeg/Neck and every foot or hand 3, Tentacle 4), more throw
//     "parts": [
//       { "kind": "CaudalFin", "t": 0, "free": true },
//       { "kind": "Jaw", "t": 1 },
//       { "kind": "Legs", "t": 0.3,
//         "side": 0,                   (optional) 0 pair, 1 right half, -1 left half
//         "size": 1.2,                 (optional) visual size, 0.6 to 3
//         "stretch": [1.5, 1, 0.6],    (optional) extra factor per axis on top of size, 0.3 to 3 (x along the body, y up, z out of the flank)
//         "offset": [0, 0.1, 0],       (optional) shift in unit space (x forward, y up, z out of the right flank)
//         "rotation": [0, 0, 0.3],     (optional) euler turn in radians
//         "widths": [1.6, 1.4, 0.8, 0.7], (optional) limbs: thickness factor at the shoulder and at every joint (0.35 to 2.6): a fat thigh, a slim shin
//         "middle": true,               (optional) a limb grows from the middle of the back instead of the flanks (a neck, a trunk), a part on a limb joint sits on the axis of the limb (one eye, not a pair)
//         "radial": 8,                  (optional) point symmetry: the part stands this many times around the spine (2 to 8), each copy at the price of a single half
//         "limb": "JointedLeg",        (optional) feet and hands: the look of their limb (a sculpted limb kind)
//         "joints": [[0.04, -0.12, 0.26], [0.1, -0.36, 0.34], [0.12, -0.6, 0.34]] },   (optional) sculpted limbs: every joint from the shoulder outwards
//       { "kind": "Tentacle", "onPart": 2, "onJoint": 1 }       (optional) a branch: grows from a joint of the part with that index
//     ]
//   }

//
//   Chapters:
//     TYPES       One template and the shape of its file.
//     LIBRARY     The templates, their cost and the plain body of a type.
//     BUILD       Turns the file of a template into a body plan.
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   CreatureTemplatePart   One part of a template file.
//   CreatureTemplateFile   A template file as it is written.
//   CreatureTemplate       A named recipe for a body plan.
//

/// <summary>One part of a template file, see the NOTE of this file for the fields.</summary>
public sealed record CreatureTemplatePart
{
    public required CreaturePartKind Kind { get; init; }
    public float T { get; init; }
    public bool Free { get; init; }
    public int Side { get; init; }
    public float Size { get; init; } = 1f;
    public float[] Stretch { get; init; }
    public float[] Offset { get; init; }
    public float[] Rotation { get; init; }
    public float[][] Joints { get; init; }
    public CreaturePartKind? Limb { get; init; }
    public int Radial { get; init; }
    public float[] Widths { get; init; }
    public bool Middle { get; init; }
    public int? OnPart { get; init; }
    public int OnJoint { get; init; }
}

/// <summary>A template file as it is written, see the NOTE of this file for the fields.</summary>
public sealed record CreatureTemplateFile
{
    public required string Key { get; init; }
    public int Order { get; init; }
    public required CreatureType Type { get; init; }
    public required float[] Girths { get; init; }
    public float[] Lifts { get; init; }
    public float[] Sways { get; init; }
    public float[] Flats { get; init; }
    public float[] Gaps { get; init; }
    public string Color { get; init; }
    public CreaturePattern Pattern { get; init; }
    public string PatternColor { get; init; }
    public string BellyColor { get; init; }
    public string HeadColor { get; init; }
    public string LimbColor { get; init; }
    public string TailColor { get; init; }
    public CreatureSkin Skin { get; init; }
    public float Pitch { get; init; }
    public float Hover { get; init; }
    public float PatternScale { get; init; } = 1f;
    public bool MouthLine { get; init; } = true;
    public bool PatternWrap { get; init; }
    public float Flat { get; init; }
    public required CreatureTemplatePart[] Parts { get; init; }
}

/// <summary>One template of the library.</summary>
/// <param name="Key">Name in UPPER_SNAKE, the translation key is CREATURE_TEMPLATE_&lt;Key&gt;.</param>
/// <param name="Order">Position of its card, low first.</param>
/// <param name="Build">Builds a fresh design of the template, every call a new one.</param>
public sealed record CreatureTemplate(string Key, int Order, Func<CreatureDesign> Build)
{
    public string NameKey => $"CREATURE_TEMPLATE_{Key}";
}

public static class CreatureTemplates
{
    // ===========================================================================
    // ===============================   LIBRARY   ===============================
    // ===========================================================================
    //
    //   All      Every template in card order, loaded from data/creatures on first use.
    //   Use      Replaces the library, for unit tests that run without Godot.
    //   Parse    One template from the text of its file.
    //   Get      The template with a key.
    //   Plain    The plain starter body of a type (cost 0).
    //   CostOf   Evolution points a template costs: what its parts were paid with.
    //

    public const string FOLDER_PATH = "res://data/creatures";
    public const string PLAIN_FISH_KEY = "FISH";
    public const string PLAIN_MAMMAL_KEY = "DOLPHIN";

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,     // a typo in a file fails instead of being ignored
        Converters             = { new JsonStringEnumConverter() },
    };

    /// <summary>What a template may bring for free, like the starter bodies: whatever sits at the tail tip, and the eyes.</summary>
    private static bool IsFreeKind(CreaturePartKind kind)
    {
        CreaturePartInfo info = CreatureCatalog.Get(kind);
        return CreatureMount.Tail == info.Mount || (CreatureCategory.Eyes == info.Category && CreaturePartKind.Lure != kind);
    }

    private static IReadOnlyList<CreatureTemplate> _all;

    /// <summary>Every template in card order, loaded from FOLDER_PATH on first use.</summary>
    public static IReadOnlyList<CreatureTemplate> All => _all ??= Sorted(ReadProjectFiles().Select(Parse));

    /// <summary>Replaces the library, for unit tests that run without Godot.</summary>
    /// <param name="files">(Required) Content of every template file.</param>
    /// <example><code>CreatureTemplates.Use(Directory.GetFiles("data/creatures", "*.json").Select(File.ReadAllText));</code></example>
    public static void Use(IEnumerable<string> files)
    {
        _all = Sorted(files.Select(Parse));
    }

    /// <summary>Parses and checks one template file. The body itself is only built by Build.</summary>
    /// <param name="json">(Required) Content of a file of data/creatures.</param>
    /// <returns>The template, its file checked for what cannot work.</returns>
    public static CreatureTemplate Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("json must be the content of a creature template file, got an empty text.", nameof(json));
        }

        CreatureTemplateFile file;
        try
        {
            file = JsonSerializer.Deserialize<CreatureTemplateFile>(json, JSON_OPTIONS);
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"A creature template must be valid JSON with known fields: {error.Message}", error);
        }

        // ---------------------------------------------------------------------------
        // Validations
        //   Fails early on what no body can be built from.
        // ---------------------------------------------------------------------------

        if (string.IsNullOrWhiteSpace(file.Key) || file.Key != file.Key.ToUpperInvariant())
        {
            throw new InvalidOperationException($"The key of a creature template must be UPPER_SNAKE, got '{file.Key}'.");
        }
        if (file.Girths.Length < CreatureDesign.MIN_VERTEBRAE || file.Girths.Length > CreatureDesign.MAX_VERTEBRAE)
        {
            throw new InvalidOperationException($"Template {file.Key} must have {CreatureDesign.MIN_VERTEBRAE} to {CreatureDesign.MAX_VERTEBRAE} girths, got {file.Girths.Length}.");
        }
        if (null != file.Gaps && file.Gaps.Length != file.Girths.Length - 1)
        {
            throw new InvalidOperationException($"Template {file.Key} must have one gap between every two girths ({file.Girths.Length - 1}), got {file.Gaps.Length}.");
        }
        if (null != file.Lifts && file.Lifts.Length != file.Girths.Length)
        {
            throw new InvalidOperationException($"Template {file.Key} must have one lift per girth ({file.Girths.Length}), got {file.Lifts.Length}.");
        }
        foreach (CreatureTemplatePart part in file.Parts.Where(part => part.Free))
        {
            if (!IsFreeKind(part.Kind))
            {
                throw new InvalidOperationException($"Template {file.Key}: only a tail part and a pair of eyes may be free, got '{part.Kind}'.");
            }
        }
        if (file.Parts.Count(part => part.Free) > 2)
        {
            throw new InvalidOperationException($"Template {file.Key} may bring two free parts (a tail part and eyes), got {file.Parts.Count(part => part.Free)}.");
        }
        return new CreatureTemplate(file.Key, file.Order, () => BuildFrom(file));
    }

    /// <summary>The template with a key.</summary>
    /// <param name="key">(Required) Key of a template file, e.g. "SHARK".</param>
    /// <example><code>CreatureTemplates.Get("SHARK").Build()</code></example>
    public static CreatureTemplate Get(string key)
    {
        return All.FirstOrDefault(template => key == template.Key)
            ?? throw new ArgumentOutOfRangeException(nameof(key), $"key must name a template of {FOLDER_PATH}, got '{key}'.");
    }

    /// <summary>The plain starter body of a type, the first templates of the library.</summary>
    /// <param name="type">(Required) Fish or marine mammal.</param>
    /// <example><code>CreatureTemplates.Plain(CreatureType.Fish).Key   // "FISH"</code></example>
    public static CreatureTemplate Plain(CreatureType type)
    {
        return Get(CreatureType.Fish == type ? PLAIN_FISH_KEY : PLAIN_MAMMAL_KEY);
    }

    /// <summary>Evolution points a body plan costs as a template: the points its parts were paid with.</summary>
    /// <param name="design">(Required) A design built by a template.</param>
    /// <example><code>CreatureTemplates.CostOf(CreatureTemplates.Plain(CreatureType.Fish).Build())   // 0</code></example>
    public static int CostOf(CreatureDesign design)
    {
        return design.Parts.Sum(part => part.PaidCost);
    }

    private static IReadOnlyList<CreatureTemplate> Sorted(IEnumerable<CreatureTemplate> templates)
    {
        List<CreatureTemplate> sorted = templates.OrderBy(template => template.Order).ThenBy(template => template.Key, StringComparer.Ordinal).ToList();
        string twice = sorted.GroupBy(template => template.Key).FirstOrDefault(group => group.Count() > 1)?.Key;
        if (null != twice)
        {
            throw new InvalidOperationException($"Every creature template must have its own key, '{twice}' is used twice.");
        }
        return sorted;
    }

    /// <summary>Reads every template file through Godot, so it also works from an exported pack.</summary>
    private static IEnumerable<string> ReadProjectFiles()
    {
        string[] names = DirAccess.GetFilesAt(FOLDER_PATH).Where(name => name.EndsWith(".json", StringComparison.Ordinal)).ToArray();
        if (0 == names.Length)
        {
            throw new InvalidOperationException($"{FOLDER_PATH} must hold the creature templates (*.json), found none.");
        }
        return names.Select(name => Godot.FileAccess.GetFileAsString($"{FOLDER_PATH}/{name}"));
    }

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   BuildFrom   Spine, parts with their pose, color and pattern of a template file.
    //   ToVector    Three numbers of a file as a vector.
    //   ToColor     "#rrggbb" of a file as a color.
    //

    /*
      1. Spine    Girths and lifts.
      2. Parts    In file order: on the body or on a joint of an earlier limb, free or at
                  its catalog price, then size, pose and joints.
      3. Look     Body color and pattern.
      A part that finds no room fails with the template and the index of the part.
    */
    private static CreatureDesign BuildFrom(CreatureTemplateFile file)
    {
        CreatureDesign design = CreatureDesign.FromGirths(file.Type, file.Girths, file.Gaps);
        for (int i = 0; null != file.Lifts && i < file.Lifts.Length; i++)
        {
            design.SetLift(i, file.Lifts[i]);
        }
        for (int i = 0; null != file.Sways && i < file.Sways.Length && i < file.Girths.Length; i++)
        {
            design.SetSway(i, file.Sways[i]);
        }
        for (int i = 0; null != file.Flats && i < file.Flats.Length && i < file.Girths.Length; i++)
        {
            design.SetFlat(i, file.Flats[i]);
        }

        for (int i = 0; i < file.Parts.Length; i++)
        {
            CreatureTemplatePart part = file.Parts[i];
            // Point symmetry: a ring of single halves, each at its price
            bool isRing   = part.Radial >= 2;
            // A limb from the middle of the back is one limb, not a pair: it pays the single half too
            int  side     = (isRing || part.Middle) && CreatureCatalog.Get(part.Kind).IsPaired ? 1 : part.Side;
            int  cost     = part.Free ? 0 : CreatureCatalog.Get(part.Kind).CostFor(side) * (isRing ? Mathf.Min(part.Radial, CreatureDesign.MAX_RADIAL) : 1);
            bool isPlaced = null == part.OnPart
                ? design.TryPlace(part.Kind, part.T, cost, side)
                : design.TryPlaceOnLimb(part.Kind, part.OnPart.Value, part.OnJoint, cost);
            if (!isPlaced)
            {
                throw new InvalidOperationException($"Template {file.Key} must find room for every part, part {i} ('{part.Kind}' at {part.T}) did not fit.");
            }
            int index = design.Parts.Count - 1;
            if (isRing && !design.SetPartRadial(index, part.Radial))
            {
                throw new InvalidOperationException($"Template {file.Key}: 'radial' needs a kind that may stand more than once on the body, part {i} is '{part.Kind}'.");
            }
            design.SetPartSize(index, part.Size);
            if (null != part.Stretch)
            {
                design.SetPartStretch(index, ToVector(file, part.Stretch));
            }
            if (null != part.Offset || null != part.Rotation)
            {
                design.SetPartPose(index, ToVector(file, part.Offset), ToVector(file, part.Rotation));
            }
            if (null != part.Limb && !design.SetLimbKind(index, part.Limb.Value))
            {
                throw new InvalidOperationException($"Template {file.Key}: 'limb' needs a foot or hand as kind and a sculpted limb as value, part {i} is '{part.Kind}' with limb '{part.Limb}'.");
            }
            if (part.Middle && !design.SetPartCentered(index, true))
            {
                throw new InvalidOperationException($"Template {file.Key}: 'middle' needs a limb or a part on a limb joint, part {i} is '{part.Kind}'.");
            }
            for (int point = 0; null != part.Widths && point < part.Widths.Length; point++)
            {
                design.SetLimbWidth(index, point, part.Widths[point]);
            }
            for (int joint = 0; null != part.Joints && joint < part.Joints.Length; joint++)
            {
                design.SetLimbJoint(index, joint, ToVector(file, part.Joints[joint]));
            }
        }

        if (null != file.Color)
        {
            design.SetColor(ToColor(file, file.Color));
        }
        design.SetPattern(file.Pattern, null == file.PatternColor ? null : ToColor(file, file.PatternColor));
        design.SetSkin(file.Skin);
        design.SetBellyColor(null == file.BellyColor ? null : ToColor(file, file.BellyColor));
        design.SetZoneColor(CreatureZone.Head, null == file.HeadColor ? null : ToColor(file, file.HeadColor));
        design.SetZoneColor(CreatureZone.Limbs, null == file.LimbColor ? null : ToColor(file, file.LimbColor));
        design.SetZoneColor(CreatureZone.Tail, null == file.TailColor ? null : ToColor(file, file.TailColor));
        design.SetPitch(Mathf.DegToRad(file.Pitch));
        design.SetHover(file.Hover);
        design.SetPatternScale(file.PatternScale);
        design.SetMouthLine(file.MouthLine);
        design.SetPatternWraps(file.PatternWrap);
        design.SetFlatness(file.Flat);
        return design;
    }

    private static Vector3 ToVector(CreatureTemplateFile file, float[] numbers)
    {
        if (null == numbers)
        {
            return Vector3.Zero;
        }
        if (3 != numbers.Length)
        {
            throw new InvalidOperationException($"Template {file.Key}: a vector must be three numbers [x, y, z], got {numbers.Length}.");
        }
        return new Vector3(numbers[0], numbers[1], numbers[2]);
    }

    private static Color ToColor(CreatureTemplateFile file, string html)
    {
        string hex = html.TrimStart('#');
        if (6 != hex.Length || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
        {
            throw new InvalidOperationException($"Template {file.Key}: a color must be written as '#rrggbb', got '{html}'.");
        }
        return new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);
    }
}
