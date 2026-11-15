// NOTE:
//   The creature part models from tools/blender (assets/models/creature): which model
//   and animation a part kind uses, a recolored instance of it and the fin membrane
//   material. Loading and recoloring itself is Core/PartModels.cs.
//   A new modelled part gets its entry in MODELS (see CLAUDE.md, "Neues Kreatur-Teil").
//
//   Node3D legs = CreatureModels.NewModel(CreaturePartKind.Legs, color);
//   Node3D wing = CreatureModels.Attach(shoulders, CreaturePartKind.FeatherWings, color, position, 1.2f, side: -1f);

//
//   Chapters:
//     PART MODELS   The model table, instances, mirrored attachment and the fin membrane.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public static class CreatureModels
{
    // ===========================================================================
    // =============================   PART MODELS   =============================
    // ===========================================================================
    //
    //   MODELS        Model file and animation per part kind.
    //   NewModel      A new recolored instance of a part model.
    //   Attach        Adds a model to a parent, mirrored on Z for the left side.
    //   FinMaterial   The webbing of a fin model, the edge in the paint color.
    //

    /*
      Model space of every part (tools/blender/build.models.py): Godot axes,
      the joint to the body at the origin, unit size.
      - Feet:       ankle at the origin, sole below, toes along +X. No animation, they stand flat.
      - Hands:      wrist at the origin, reaching forward (+X), thumb towards -Z. "Grab", 2 s.
                    The same for the pincer of a crab and the club of a squid tentacle.
                    Feet and hands sit on the tip of a sculpted limb (Visual/CreatureLimbs3D.cs).
      - Wings:      root at the origin, spreading along +Z, swept back. "Flap", 1 s.
      - Stalk eye:  base at the origin, eye at y = 1.2, pupil towards +X and +Z. "Sway", 3 s.
      - Brain:      two lobes around the origin, no animation.
      - Fins:       root at the origin. Tail fin upright and flukes flat, both reaching back (-X);
                    back fin upright, rising (+Y) and swept back; paddle flat, reaching out (+Z).
                    Real rays (FinRay) on a thick webbing (Fin). "Flutter", 1 s.
      - Mouths:     a horseshoe around the snout that opens to the back: its ends at x = 0,
                    z = -1 and 1 (the flanks), its front at x = 1 (the nose tip). The jaw chomps
                    ("Chomp", 2 s), whale mouth and beak stand still.
      - Spines, shell: around a unit sphere at the origin, stretched to the body there.
      - Gills:      on the skin of the right flank, out of the body is +Z, slits upright.
      - Lantern:    base at the origin, arching forward over the head. "Sway", 3 s.
      - Eye models: an eyeball of radius 1 at the origin on the right flank, +Z out of the head, +X forward:
                    the pupil looks out and a little forward. The game scales it to the eye size and blinks it (y).
      Parts on the right flank (+Z) are used as they are, the left ones mirrored on Z.
    */
    public const string MODEL_DIR = "res://assets/models/creature/";
    public static readonly IReadOnlyDictionary<CreaturePartKind, (string File, string Animation)> MODELS =
        new Dictionary<CreaturePartKind, (string File, string Animation)>
        {
            [CreaturePartKind.Legs]          = ("creature.paw.glb", null),
            [CreaturePartKind.StubbyLegs]    = ("creature.columnfoot.glb", null),
            [CreaturePartKind.ClawLegs]      = ("creature.talonfoot.glb", null),
            [CreaturePartKind.CrabLegs]      = ("creature.crabtip.glb", null),
            [CreaturePartKind.Hands]         = ("creature.hand.glb", "Grab"),
            [CreaturePartKind.Claws]         = ("creature.clawpaw.glb", "Grab"),
            [CreaturePartKind.Pincers]       = ("creature.pincer.glb", "Grab"),
            [CreaturePartKind.Suckers]       = ("creature.suckerclub.glb", "Grab"),
            [CreaturePartKind.MembraneWings] = ("creature.membranewings.glb", "Flap"),
            [CreaturePartKind.FeatherWings]  = ("creature.featherwings.glb", "Flap"),
            [CreaturePartKind.SmallBrain]    = ("creature.brain.glb", null),
            [CreaturePartKind.BigBrain]      = ("creature.brain.glb", null),
            [CreaturePartKind.StalkEye]      = ("creature.stalkeye.glb", "Sway"),
            [CreaturePartKind.CaudalFin]     = ("creature.caudalfin.glb", "Flutter"),
            [CreaturePartKind.Fluke]         = ("creature.fluke.glb", "Flutter"),
            [CreaturePartKind.DorsalFin]     = ("creature.dorsalfin.glb", "Flutter"),
            [CreaturePartKind.PectoralFin]   = ("creature.pectoralfin.glb", "Flutter"),
            [CreaturePartKind.Jaw]           = ("creature.jaw.glb", "Chomp"),
            [CreaturePartKind.FilterMouth]   = ("creature.filtermouth.glb", null),
            [CreaturePartKind.Beak]          = ("creature.beak.glb", null),
            [CreaturePartKind.Spines]        = ("creature.spines.glb", null),
            [CreaturePartKind.Shell]         = ("creature.shell.glb", null),
            [CreaturePartKind.Gills]         = ("creature.gills.glb", null),
            [CreaturePartKind.Lure]          = ("creature.lure.glb", "Sway"),
            [CreaturePartKind.SharkTail]     = ("creature.sharktail.glb", "Flutter"),
            [CreaturePartKind.NewtTail]      = ("creature.newttail.glb", "Flutter"),
            [CreaturePartKind.MantleFins]    = ("creature.mantlefins.glb", "Flutter"),
            [CreaturePartKind.FanTail]       = ("creature.fantail.glb", "Flutter"),
            [CreaturePartKind.CurlTail]      = ("creature.curltail.glb", "Curl"),
            [CreaturePartKind.SharkFin]      = ("creature.sharkfin.glb", "Flutter"),
            [CreaturePartKind.TallFin]       = ("creature.tallfin.glb", "Flutter"),
            [CreaturePartKind.Flipper]       = ("creature.flipper.glb", "Flutter"),
            [CreaturePartKind.Paddle]        = ("creature.paddle.glb", "Stroke"),
            [CreaturePartKind.NeedleMaw]     = ("creature.needlemaw.glb", "Chomp"),
            [CreaturePartKind.PufferMouth]   = ("creature.puffermouth.glb", null),
            [CreaturePartKind.TubeSnout]     = ("creature.tubesnout.glb", null),
            [CreaturePartKind.Carapace]      = ("creature.carapace.glb", null),
            [CreaturePartKind.FeatherGills]  = ("creature.feathergills.glb", "Sway"),
            [CreaturePartKind.Siphon]        = ("creature.siphon.glb", "Puff"),
            // Round 3 (eyes, mouths, fins): no fixed animation name, a rigged one plays its only loop (CreatureParts3D.BuildPlaced)
            [CreaturePartKind.SickleFin]     = ("creature.sicklefin.glb", "Flutter"),
            [CreaturePartKind.RoundFlipper]  = ("creature.roundflipper.glb", "Flutter"),
            [CreaturePartKind.HindPaddle]    = ("creature.hindpaddle.glb", "Stroke"),
            [CreaturePartKind.FinRibbon]     = ("creature.finribbon.glb", null),
            [CreaturePartKind.SharkMaw]      = ("creature.sharkmaw.glb", null),
            [CreaturePartKind.OrcaMaw]       = ("creature.orcamaw.glb", null),
            [CreaturePartKind.GapeMaw]       = ("creature.gapemaw.glb", null),
            [CreaturePartKind.SquidBeak]     = ("creature.squidbeak.glb", null),
            [CreaturePartKind.CrabMouth]     = ("creature.crabmouth.glb", null),
            [CreaturePartKind.Smile]         = ("creature.smile.glb", null),
            [CreaturePartKind.BeadEye]       = ("creature.beadeye.glb", null),
            [CreaturePartKind.GhostEye]      = ("creature.ghosteye.glb", null),
            [CreaturePartKind.BulgeEye]      = ("creature.bulgeeye.glb", null),
            [CreaturePartKind.PatchEye]      = ("creature.patcheye.glb", null),
            [CreaturePartKind.SquidEye]      = ("creature.squideye.glb", null),
            [CreaturePartKind.PegEye]        = ("creature.pegeye.glb", null),
            [CreaturePartKind.LidEye]        = ("creature.lideye.glb", null),
            [CreaturePartKind.TurretEye]     = ("creature.turreteye.glb", null),
            [CreaturePartKind.FrogEye]       = ("creature.frogeye.glb", null),
        };

    /// <summary>A new instance of a creature part model, recolored (Core/PartModels.cs).</summary>
    /// <param name="kind">(Required) A part listed in MODELS.</param>
    /// <param name="bodyColor">(Required) Color of skin, pads, feathers and membrane.</param>
    /// <param name="overrides">(Optional) Materials by Blender name replacing the recolor. Defaults to none.</param>
    /// <example><code>Node3D legs = CreatureModels.NewModel(CreaturePartKind.Legs, color);</code></example>
    public static Node3D NewModel(CreaturePartKind kind, Color bodyColor, IReadOnlyDictionary<string, Material> overrides = null)
    {
        if (!MODELS.TryGetValue(kind, out (string File, string Animation) model))
        {
            throw new System.ArgumentOutOfRangeException(nameof(kind), $"kind must have a model in MODELS, got '{kind}'.");
        }
        return PartModels.New(MODEL_DIR + model.File, bodyColor, overrides);
    }

    /// <summary>Adds a model to a parent, mirrored on Z for the left side.</summary>
    /// <param name="parent">(Required) The node the model hangs on.</param>
    /// <param name="kind">(Required) A part listed in MODELS.</param>
    /// <param name="color">(Required) The body color.</param>
    /// <param name="position">(Required) Position in the space of the parent.</param>
    /// <param name="size">(Required) Uniform size of the model.</param>
    /// <param name="side">(Required) 1 right flank or midline, -1 left flank (mirrored).</param>
    /// <param name="width">(Optional) Thickens limbs across their length (X and Z). Defaults to 1.</param>
    /// <param name="overrides">(Optional) Materials by Blender name replacing the recolor. Defaults to none.</param>
    /// <returns>The model, already a child of the parent.</returns>
    public static Node3D Attach(Node3D parent, CreaturePartKind kind, Color color, Vector3 position, float size, float side,
        float width = 1f, IReadOnlyDictionary<string, Material> overrides = null)
    {
        Node3D model = NewModel(kind, color, overrides);
        model.Position = position;
        model.Scale    = new Vector3(size * width, size, size * width * side);
        parent.AddChild(model);
        return model;
    }

    private const float FIN_REACH = 0.45f;          // the fin models reach about this far from their root

    /// <summary>The webbing of a fin model (Core/PartModels.cs), the edge takes the paint color of the design.</summary>
    /// <param name="design">(Required) The design, for its paint pattern and color.</param>
    /// <param name="bodyColor">(Required) Color at the root of the fin.</param>
    /// <returns>The membrane: body color at the root, the paint color (or a light shade) at the edge.</returns>
    public static ShaderMaterial FinMaterial(CreatureDesign design, Color bodyColor)
    {
        Color edge = CreaturePattern.None != design.Pattern && null != design.PatternColor
            ? design.PatternColor.Value.Lightened(0.25f)
            : bodyColor.Lightened(0.55f);
        // A saddle paints the whole back: fins grow out of that color, not out of the pale belly
        Color root = CreaturePattern.Saddle == design.Pattern ? design.PatternColor ?? bodyColor.Darkened(0.45f) : bodyColor;
        return PartModels.Membrane(new Color(root, bodyColor.A), new Color(edge, bodyColor.A), PartModels.MembranePlane.Free, FIN_REACH, 0f);
    }
}
