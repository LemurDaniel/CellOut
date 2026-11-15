// NOTE:
//   The parts of a 3D organism: fins, eyes, mouths (jaw, filter mouth, beak), spines, shell,
//   poison gland, blowhole, gills, sculpted limbs with feet and hands, a brain, the nostrils
//   of a lung, a lantern and wings, each on a pivot along the spine that rides the swim
//   wave. Legs, hands, pincers, sucker arms, wings, brain, stalk eyes, fins, mouths, spines,
//   shell, gills and the lantern are models from tools/blender (Visual/CreatureModels.cs),
//   the rest (eyes, pits, blowhole, nostrils, poison gland) are primitive meshes.
//   A node component of CreatureVisual3D: a child at the origin of the visual, built once
//   and replaced by a new one on every rebuild. The visual tells it the wave, the blink
//   and the spout of the frame; picking answers come back as return values.
//   A new modelled part gets a row in PLACEMENTS, only a part that is drawn from primitives or
//   needs its own rule gets a case in BuildPart (see CLAUDE.md, "Neues Kreatur-Teil").
//
//   var parts = new CreatureParts3D();
//   visual.AddChild(parts);                              // in the tree first: Build reads global transforms
//   parts.Build(design, color, limbClay, knuckleClay);
//   parts.Swim(design.HalfLength, amplitude, phase, isVertical: false);

//
//   Chapters:
//     BUILD PARTS     Every placed part on its pivots, posed by side, size, shift and turn.
//     ANIMATE PARTS   Swim wave on the pivots, eye blink and the blowhole spout.
//     PICK PARTS      The part under a ray, the anchor of its gizmo and the frame of a limb.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class CreatureParts3D : Node3D
{
    // ===========================================================================
    // =============================   BUILD PARTS   =============================
    // ===========================================================================
    //
    //   Build        Builds and poses every part of a design.
    //   BuildPart    One placed part on a pivot that rides the swim wave.
    //   PLACEMENTS / BuildPlaced   New modelled parts: a table row says how the model sits on the body.
    //   BuildMouth   The mouth model around the snout: shark jaw, whale mouth or beak.
    //   AttachHugging   A model that hugs the body (spines, shell), stretched to the body there.
    //   BuildWings   A pair of membrane or feather wings on the back.
    //   BuildEye     One eye on the flank, pupil forward and outwards.
    //   AddPivot     A pivot at a spine X, moved by the swim wave.
    //

    private const float FLAP_RATE = 0.5f;            // wing beats per second
    private const float SWAY_RATE = 0.8f;            // stalk eyes look around
    private const float FLUTTER_RATE = 1f;           // fin flutter cycles per second

    /*
      Base size of every part before the size slider of the editor (CreaturePart.Size):
      bold and monstrous by design, the slider then goes 60% .. 300% of it. Legs stay at
      1, they get their bulk from LEG_WIDTH so the soles keep touching the ground.
    */
    private static readonly IReadOnlyDictionary<CreatureCategory, float> BASE_PART_SIZES = new Dictionary<CreatureCategory, float>
    {
        [CreatureCategory.Fins]    = 1.6f,
        [CreatureCategory.Mouths]  = 1.9f,
        [CreatureCategory.Defense] = 1.9f,
        [CreatureCategory.Eyes]    = 2f,
        [CreatureCategory.Limbs]   = 1f,             // sculpted, the joints already set the length
        [CreatureCategory.Feet]    = 1f,
        [CreatureCategory.Hands]   = 1f,             // on a sculpted limb: the joints set the length, END_SIZE the hand
        [CreatureCategory.Brain]   = 1.4f,
        [CreatureCategory.Lungs]   = 1.3f,
        [CreatureCategory.Wings]   = 1.8f,
    };

    /// <summary>Kinds that leave the size of their tab: what hugs the body (shell, spines, mouths, gills) is built in body scale.</summary>
    private static readonly IReadOnlyDictionary<CreaturePartKind, float> BASE_KIND_SIZES = new Dictionary<CreaturePartKind, float>
    {
        [CreaturePartKind.Shell]       = 1f,
        [CreaturePartKind.Eye]         = 1f,             // the tab size never showed on eyes (the blink reset it), 1 keeps their known look
        [CreaturePartKind.Spines]      = 1f,
        [CreaturePartKind.Jaw]         = 1f,
        [CreaturePartKind.FilterMouth] = 1f,
        [CreaturePartKind.Beak]        = 1f,
        [CreaturePartKind.Gills]       = 1f,
        [CreaturePartKind.Lure]        = 1.2f,
        [CreaturePartKind.PoisonGland] = 1.4f,
    };

    private sealed record Attached(Node3D Pivot, float X, float Y, bool IsOnLimb = false);   // Y: height of the bent spine there, IsOnLimb: rides its limb, not the swim wave

    private readonly List<Attached> _attached = new();
    private readonly List<Node3D> _eyes = new();
    private readonly Dictionary<Node3D, Vector3> _eyeScales = new();     // the posed size of every eye, the blink squeezes it
    private readonly List<MeshInstance3D> _spouts = new();
    private readonly CreaturePartPoser _poser = new();
    private CreatureDesign _design;
    private ShaderMaterial _knuckleClay;
    private CreatureLimbs3D _limbs;

    /// <summary>Builds every part of a design, each posed by side, size, shift and turn. Call once, inside the tree.</summary>
    /// <param name="design">(Required) The design whose parts are built.</param>
    /// <param name="color">(Required) The body color.</param>
    /// <param name="limbClay">(Required) Clay of the limb tubes: the body look without a swim wave of its own.</param>
    /// <param name="knuckleClay">(Required) Plain clay of knuckles and fillets, without the paint pattern.</param>
    public void Build(CreatureDesign design, Color color, ShaderMaterial limbClay, ShaderMaterial knuckleClay)
    {
        _design      = design;
        _knuckleClay = knuckleClay;
        _limbs       = new CreatureLimbs3D(limbClay, knuckleClay);

        for (int index = 0; index < design.Parts.Count; index++)
        {
            int firstPivot = _attached.Count;
            BuildPart(design, design.Parts[index], color);

            // Posed by side, size, shift and turn; an eye of a dropped half blinks no more
            CreaturePart  part    = design.Parts[index];
            List<Node3D>  pivots  = _attached.Skip(firstPivot).Select(attached => attached.Pivot).ToList();
            // A kind of PLACEMENTS brings its size in its row, it is built in body scale
            float         size    = PLACEMENTS.ContainsKey(part.Kind) ? 1f
                : BASE_KIND_SIZES.TryGetValue(part.Kind, out float kindSize) ? kindSize : BASE_PART_SIZES[part.Info.Category];
            // Pivots on the body are posed together, the pivots on a limb joint per bud (one per flank of the limb)
            foreach (IGrouping<Node, Node3D> group in pivots.GroupBy(pivot => pivot.GetParent()))
            {
                Transform3D? toVisual = this == group.Key ? null : GlobalTransform.AffineInverse() * ((Node3D)group.Key).GlobalTransform;
                List<Node3D> dropped  = _poser.Pose(index, part, group.ToList(), size, toVisual);
                foreach (Node3D piece in dropped)
                {
                    _eyes.RemoveAll(eye => eye == piece || piece.IsAncestorOf(eye));
                }
            }

            if (part.Radial >= 2 && !CreatureDesign.IsOnBud(part))
            {
                AddRing(index, part, pivots);
            }

            // Sculpted limbs remember their posed shoulder per flank (joint handles of the editor)
            // and where their joints ended up (branches grow from them, the parent is built first)
            foreach ((Node3D piece, int flank) in _limbs.TakeBuilt())
            {
                if (null == piece.GetParent())
                {
                    continue;
                }
                var points = new List<Vector3> { ToLocal(piece.GlobalTransform * Vector3.Zero) };
                points.AddRange(part.Joints.Select(joint => ToLocal(piece.GlobalTransform * joint)));
                _limbs.Remember(index, flank, piece, points);
            }
        }
        foreach (Node3D eye in _eyes)
        {
            _eyeScales[eye] = eye.Scale;
        }
    }

    /*
      Point symmetry: the posed pieces of the part (its right half) are copied Radial - 1
      times, each copy turned a step further around the spine at its pivot. The copies
      play the animation of the original, blink with it and pick the same part. Branches
      on a limb only grow on the original, not on its copies.
    */
    private void AddRing(int index, CreaturePart part, IReadOnlyList<Node3D> pivots)
    {
        foreach (Node3D pivot in pivots)
        {
            List<Node3D> pieces = pivot.GetChildren().OfType<Node3D>().ToList();
            for (int k = 1; k < part.Radial; k++)
            {
                var turned = new Node3D { Basis = new Basis(Vector3.Right, Mathf.Tau * k / part.Radial) };
                pivot.AddChild(turned);
                foreach (Node3D piece in pieces)
                {
                    var copy = (Node3D)piece.Duplicate();
                    turned.AddChild(copy);
                    _poser.AddCopy(index, copy);
                    if (_eyes.Contains(piece))
                    {
                        _eyes.Add(copy);
                    }
                    // A duplicated player does not play: start what the original plays, a little later per copy
                    var players = piece.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().ToList();
                    var copies  = copy.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().ToList();
                    for (int i = 0; i < players.Count && i < copies.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(players[i].CurrentAnimation))
                        {
                            copies[i].SpeedScale = players[i].SpeedScale;
                            copies[i].Play(players[i].CurrentAnimation);
                            copies[i].Seek(k * 0.15, true);
                        }
                    }
                }
            }
        }
    }

    private const float BUD_SCALE = 0.42f;           // a part on a limb joint is built on a little body of this size
    private static readonly float[] BUD_GIRTHS = { 0.4f, 0.62f, 0.7f, 0.52f };

    private Node3D _pivotParent;                     // set while a part is built on a bud: its pivots hang there
    private CreatureDesign _pivotDesign;             // the body the pivots of the part being built sit on

    /*
      A part on a joint of a limb that is no limb itself: built with the same code as on the
      body, but on a bud, a little plain body that points along the limb (nose outwards) and
      sits with its middle on the joint. One bud per flank of the limb, a child of the posed
      limb piece: it follows the pose of the limb and the swim wave on its own.
      > A mouth gets the clay of its bud with the carved opening: a little head on the limb.
      > Everything else shows only the part, the bud stays invisible.
    */
    private void BuildOnLimb(CreatureDesign design, CreaturePart part, Color color)
    {
        int parentIndex = design.IndexOfId(part.ParentId);
        if (parentIndex < 0 || null == design.Parts[parentIndex].Joints)
        {
            return;
        }
        IReadOnlyList<Vector3> joints = design.Parts[parentIndex].Joints;
        int     joint  = Mathf.Clamp(part.ParentJoint, 0, joints.Count - 1);
        Vector3 before = 0 == joint ? Vector3.Zero : joints[joint - 1];
        Vector3 along  = (joints[joint] - before).Normalized();
        Vector3 aside  = along.Cross(Vector3.Up);
        aside = aside.LengthSquared() < 1e-4f ? Vector3.Back : aside.Normalized();
        var     turn   = new Basis(along, aside.Cross(along), aside);

        CreatureDesign bud = CreatureDesign.FromGirths(design.Type, BUD_GIRTHS, null);
        bool isMouth = CreatureCategory.Mouths == part.Info.Category;
        if (isMouth)
        {
            bud.TryPlace(part.Kind, 1f, 0);          // the bud carves the opening of this mouth
        }
        Vector3 middle = new((bud.TailTipX + bud.NoseTipX) * 0.5f, 0f, 0f);
        foreach ((int _, Node3D piece) in _limbs.PiecesOf(parentIndex).ToList())
        {
            var holder = new Node3D { Transform = new Transform3D(turn * Basis.FromScale(Vector3.One * BUD_SCALE), joints[joint]) };
            piece.AddChild(holder);
            var budSpace = new Node3D { Position = -middle };
            holder.AddChild(budSpace);
            if (isMouth)
            {
                var head = (ShaderMaterial)_knuckleClay.Duplicate();
                head.SetShaderParameter("mouth_gape", MouthShapes.For(bud)?.Gape ?? -1f);
                head.SetShaderParameter("half_length", Mathf.Max(bud.HalfLength, 0.1f));
                CellVisual3D.AddMesh(budSpace, CreatureClay.BuildBodyMesh(bud), head, Vector3.Zero, Vector3.One);
            }
            _pivotParent = budSpace;
            _pivotDesign = bud;
            BuildPart(bud, part with { ParentId = 0, T = CreatureDesign.BudShare(part.Kind) }, color);
            _pivotParent = null;
            _pivotDesign = null;
        }
    }

    private void BuildPart(CreatureDesign design, CreaturePart part, Color color)
    {
        if (CreatureDesign.IsOnBud(part))
        {
            BuildOnLimb(design, part, color);
            return;
        }

        // Materials that follow the design: the fin webbing, BackSkin (the color of the painted back)
        // and Skin, the plain clay of the body, so a snout or a tail is one piece with it
        Color              back    = CreaturePattern.None == design.Pattern ? color : design.PatternColor ?? color.Darkened(0.45f);
        var                finSkin = new Dictionary<string, Material>
        {
            ["Fin"]      = CreatureModels.FinMaterial(design, color),
            ["BackSkin"] = CellVisual3D.Material(back, 0f, 0.6f),
            ["Skin"]     = _knuckleClay,
        };
        Color              outline = color.Darkened(0.5f);
        float              x       = design.XAt(part.T);
        float              radius  = design.RadiusAt(x);
        float              top     = design.TopAt(x);
        float              flank   = design.FlankAt(x);
        float              finSize = 0.6f + design.MeanGirth * 0.5f;

        // Every sculpted limb, with or without a foot or a hand on its tip: a clay tube through its joints, on both flanks
        if (part.Info.IsSculpted || part.Info.IsEnd)
        {
            _limbs.Build(AddPivot(_limbs.RootOn(design, part, 1).X), design, part, color);
            return;
        }

        // A model placed by its row in PLACEMENTS
        if (PLACEMENTS.TryGetValue(part.Kind, out Placement placement))
        {
            BuildPlaced(design, part, placement, color, finSkin);
            return;
        }

        switch (part.Kind)
        {
            // Tail: a forked upright fin (fish) or flat flukes (marine mammal) behind the tail tip, fluttering
            case CreaturePartKind.CaudalFin:
            case CreaturePartKind.Fluke:
                Node3D tail = CreatureModels.Attach(AddPivot(design.TailTipX + 0.06f), part.Kind, color, Vector3.Zero, finSize, 1f, overrides: finSkin);
                PartModels.PlayLoop(tail, CreatureModels.MODELS[part.Kind].Animation, 0f, FLUTTER_RATE);
                break;

            // Back: a swept sail, a row of spines or the blowhole with its spout
            case CreaturePartKind.DorsalFin:
                Node3D sail = CreatureModels.Attach(AddPivot(x), part.Kind, color, new Vector3(0f, top - 0.03f, 0f), 0.5f + radius * 0.6f, 1f, overrides: finSkin);
                PartModels.PlayLoop(sail, CreatureModels.MODELS[part.Kind].Animation, 0.3f, FLUTTER_RATE);
                break;
            // Spines of a puffer fish: they stand out from back and flanks all around the body there
            case CreaturePartKind.Spines:
                AttachHugging(AddPivot(x), part.Kind, color, new Vector3(Mathf.Min(design.Length * SPINES_SPREAD, SPINES_MAX_SPREAD), top, flank));
                break;

            // Top of the head: the lantern of an angler fish arches forward and sways
            case CreaturePartKind.Lure:
                Node3D lantern    = AddPivot(x);
                float  lureLength = 0.4f + radius * 0.5f;
                Node3D lure       = CreatureModels.Attach(lantern, part.Kind, color, new Vector3(0f, top * 0.92f, 0f), lureLength, 1f);
                CreatureClay.AddFillet(lantern, new Vector3(0f, top * 0.92f, 0f), lureLength * CreatureClay.STALK_FILLET, _knuckleClay);
                PartModels.PlayLoop(lure, CreatureModels.MODELS[part.Kind].Animation, 0f, SWAY_RATE);
                break;

            // Gills: three slits with red frills on both flanks behind the head, wherever they were placed inside
            case CreaturePartKind.Gills:
                float  gillX = design.XAt(GILLS_SHARE);
                Node3D gills = AddPivot(gillX);
                foreach (float side in new[] { -1f, 1f })
                {
                    CreatureModels.Attach(gills, part.Kind, color, new Vector3(0f, design.TopAt(gillX) * 0.05f, side * design.FlankAt(gillX) * 0.96f), design.TopAt(gillX) * 0.95f, side);
                }
                break;
            case CreaturePartKind.Blowhole:
                Node3D hole = AddPivot(x);
                CellVisual3D.AddMesh(hole, CellVisual3D.SphereMesh(), CellVisual3D.Material(outline), new Vector3(0f, top - 0.005f, 0f), new Vector3(0.05f, 0.02f, 0.035f));
                var spout = new Node3D { Position = new Vector3(0f, top, 0f) };
                hole.AddChild(spout);
                for (int i = 0; i < 3; i++)
                {
                    MeshInstance3D drop = CellVisual3D.AddMesh(spout, CellVisual3D.SphereMesh(), CellVisual3D.Material(new Color(0.85f, 0.95f, 1f, 0.75f), 0.4f, 0.1f), Vector3.Zero, Vector3.One * 0.05f);
                    drop.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                    _spouts.Add(drop);
                }
                break;

            // Flanks: a pair of paddles tilted down, or a pair of eyes
            case CreaturePartKind.PectoralFin:
                Node3D fins = AddPivot(x);
                foreach (float side in new[] { -1f, 1f })
                {
                    // Tilted down a little, mirrored for the left flank, both flutter in step
                    var paddle = new Node3D { Position = new Vector3(0f, -top * 0.3f, side * flank * 0.85f), Rotation = new Vector3(side * 0.45f, 0f, 0f) };
                    fins.AddChild(paddle);
                    Node3D model = CreatureModels.Attach(paddle, part.Kind, color, Vector3.Zero, 0.55f + radius * 0.6f, side, overrides: finSkin);
                    PartModels.PlayLoop(model, CreatureModels.MODELS[part.Kind].Animation, 0.15f, FLUTTER_RATE);
                }
                break;
            case CreaturePartKind.Eye:
                Node3D eyes = AddPivot(x);
                foreach (float side in new[] { -1f, 1f })
                {
                    BuildEye(eyes, new Vector3(0f, top * 0.3f, side * flank * 0.88f), side, 0.08f + radius * 0.12f);
                }
                break;

            // Flanks: two dark glossy pits per side
            case CreaturePartKind.PitEye:
                Node3D pits = AddPivot(x);
                foreach (float side in new[] { -1f, 1f })
                {
                    for (int i = 0; i < 2; i++)
                    {
                        CellVisual3D.AddMesh(pits, CellVisual3D.SphereMesh(), CellVisual3D.Material(new Color(0.05f, 0.05f, 0.08f), 0f, 0.1f),
                            new Vector3(i * 0.07f - 0.035f, top * (0.35f + i * 0.1f), side * flank * 0.93f), Vector3.One * 0.035f);
                    }
                }
                break;

            // Top of the head: two swaying stalks leaning outwards with a big eye on top
            case CreaturePartKind.StalkEye:
                Node3D stalks      = AddPivot(x);
                float  stalkLength = 0.3f + radius * 0.2f;
                foreach (float side in new[] { -1f, 1f })
                {
                    var stalk = new Node3D { Position = new Vector3(0f, top * 0.8f, side * flank * 0.35f), Rotation = new Vector3(side * 0.45f, 0f, -0.2f) };
                    stalks.AddChild(stalk);
                    Node3D model = CreatureModels.Attach(stalk, part.Kind, color, Vector3.Zero, stalkLength, side);
                    CreatureClay.AddFillet(stalk, Vector3.Zero, stalkLength * CreatureClay.STALK_FILLET, _knuckleClay);
                    PartModels.PlayLoop(model, CreatureModels.MODELS[part.Kind].Animation, side * 0.3f, SWAY_RATE);
                }
                break;

            // Skull: two folded pink lobes bulging from the top of the head
            case CreaturePartKind.SmallBrain:
            case CreaturePartKind.BigBrain:
                float  brainX    = design.XAt(0.78f);
                float  brainY    = design.TopAt(brainX) * 0.9f;
                float  brainSize = (CreaturePartKind.BigBrain == part.Kind ? 1f : 0.7f) * (0.1f + design.RadiusAt(brainX) * 0.22f) * 1.8f;
                CreatureModels.Attach(AddPivot(brainX), part.Kind, color, new Vector3(0f, brainY, 0f), brainSize, 1f);
                break;

            // Back: a pair of wings
            case CreaturePartKind.MembraneWings:
            case CreaturePartKind.FeatherWings:
                BuildWings(part.Kind, x, top, flank, radius, color);
                break;

            // Lung: two nostrils on top of the nose, wherever it was placed inside.
            // The big lung has wider nostrils and a puffed throat sac under the chin.
            case CreaturePartKind.Lung:
            case CreaturePartKind.BigLung:
                bool   isBigLung = CreaturePartKind.BigLung == part.Kind;
                float  nostrilX  = design.SpineEndX + (design.NoseTipX - design.SpineEndX) * 0.45f;
                Node3D nose      = AddPivot(nostrilX);
                foreach (float side in new[] { -1f, 1f })
                {
                    CellVisual3D.AddMesh(nose, CellVisual3D.SphereMesh(), CellVisual3D.Material(outline),
                        new Vector3(0f, design.TopAt(nostrilX) * 0.92f, side * (isBigLung ? 0.07f : 0.05f)), new Vector3(0.03f, 0.015f, 0.022f) * (isBigLung ? 1.6f : 1f));
                }
                if (isBigLung)
                {
                    float throatX = design.SpineEndX - 0.1f;
                    CellVisual3D.AddMesh(AddPivot(throatX), CellVisual3D.SphereMesh(), CellVisual3D.Material(color.Lightened(0.3f), 0f, 0.5f),
                        new Vector3(0f, -design.TopAt(throatX) * 0.75f, 0f), new Vector3(0.18f, 0.12f, design.FlankAt(throatX) * 0.6f));
                }
                break;

            // Nose: the chomping jaw of a shark, the baleen mouth of a whale or a hooked beak
            case CreaturePartKind.Jaw:
            case CreaturePartKind.FilterMouth:
            case CreaturePartKind.Beak:
                BuildMouth(design, part.Kind, color, finSkin);
                break;

            // Back: the shell of a turtle around the body, head and tail poke out of it
            case CreaturePartKind.Shell:
                AttachHugging(AddPivot(x), part.Kind, color, new Vector3(design.Length * SHELL_LENGTH, top * 1.12f, flank * 1.16f));
                break;

            // Poison gland: glowing pustules high on both flanks, wherever it was placed inside
            case CreaturePartKind.PoisonGland:
                Node3D glands = AddPivot(x);
                foreach (float side in new[] { -1f, 1f })
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        CellVisual3D.AddMesh(glands, CellVisual3D.SphereMesh(), CellVisual3D.Material(POISON_COLOR, 0.6f, 0.15f),
                            new Vector3(i * 0.075f, top * (0.62f - Mathf.Abs(i) * 0.12f), side * flank * 0.8f), Vector3.One * (0.05f - Mathf.Abs(i) * 0.012f));
                    }
                }
                break;

            default:
                throw new System.ArgumentOutOfRangeException(nameof(part), $"part must be a known CreaturePartKind, got '{part.Kind}'.");
        }
    }

    private const float BEAK_OVERHANG = 0.18f;       // the beak reaches this far past the nose tip
    private const float CHOMP_RATE = 0.5f;           // bites of the jaw per second
    private const float SPINES_SPREAD = 0.3f;        // the spines cover this share of the spine to each side of their spot
    private const float SPINES_MAX_SPREAD = 0.2f;    // on a long body the spines stay a patch around their spot instead of covering it all
    private const float SHELL_LENGTH = 0.36f;        // the shell covers this share of the spine to each side of its spot
    private const float GILLS_SHARE = 0.72f;         // gills sit at this share of the spine, behind the head
    private static readonly Color POISON_COLOR = new(0.62f, 0.25f, 0.78f);

    /*
      The mouth models are a horseshoe around the snout (Visual/CreatureModels.cs): the
      pivot sits at the mouth corners of the opening the body mesh cut for the kind
      (Design/MouthShapes.cs, data/mouths.json), at the level of its mouth line. The model
      is stretched forward to the nose tip, sideways to the flanks there and to three
      quarters of the height of the head.
      > Jaw:           two gums full of teeth, the lower one chomps
      > Filter mouth:  the same horseshoe a little wider, baleen hanging from the lip
      > Beak:          a hooked horn over the whole nose, reaching past its tip
    */
    /// <summary>The mouth at the nose: shark jaw, whale mouth or beak.</summary>
    private void BuildMouth(CreatureDesign design, CreaturePartKind kind, Color color, IReadOnlyDictionary<string, Material> skins)
    {
        // The model sits in the opening the body mesh cut for this kind (Design/MouthShapes.cs)
        MouthShape shape  = MouthShapes.Current.TryGetValue(kind, out MouthShape cut) ? cut : new MouthShape();
        float  noseX      = design.NoseTipX;
        float  mouthX     = shape.CornerX(design);
        float  mouthTop   = design.TopAt(mouthX);
        float  mouthFlank = design.FlankAt(mouthX);
        float  reach      = noseX - mouthX;
        Node3D mouth      = CreatureModels.NewModel(kind, color, skins);       // BackSkin: lips in the color of the painted back
        AddPivot(mouthX).AddChild(mouth);

        // The beak is modelled half as wide as it is long: twice the body there covers the whole face
        if (CreaturePartKind.Beak == kind)
        {
            mouth.Position = new Vector3(reach * 0.35f, -mouthTop * 0.12f, 0f);
            mouth.Scale    = new Vector3(reach * 0.65f + BEAK_OVERHANG, mouthTop * 1.2f, mouthFlank * 1.3f);
            return;
        }
        float width = mouthFlank * (CreaturePartKind.FilterMouth == kind ? 1.05f : 1.02f);
        mouth.Position = new Vector3(0f, shape.Level * mouthTop, 0f);
        mouth.Scale    = new Vector3(reach * 1.03f, mouthTop * 0.75f, width);
        if (null != CreatureModels.MODELS[kind].Animation)
        {
            PartModels.PlayLoop(mouth, CreatureModels.MODELS[kind].Animation, 0f, CHOMP_RATE);
        }
    }

    private const float MODEL_RATE = 0.8f;           // loops per second of the animation of a placed model

    /// <summary>The model spaces a part model can be built in (tools/blender, see Visual/CreatureModels.cs).</summary>
    private enum ModelSpace { Snout, Hug, FlankPair, Top, Belly, Tail, Eye }

    /// <summary>How the model of a kind sits on the body.</summary>
    /// <param name="Space">The space the model is built in, it decides the pivot and the stretch.</param>
    /// <param name="Size">(Optional) Size on top of the body there, 1 = as modelled. Defaults to 1.</param>
    /// <param name="Reach">(Optional) Hug: share of the spine the model covers to each side of its spot. Defaults to 0.3.</param>
    /// <param name="Share">(Optional) Fixed share of the spine it sits at (inner parts shown outside), negative for the spot of the part. Defaults to -1.</param>
    private sealed record Placement(ModelSpace Space, float Size = 1f, float Reach = 0.3f, float Share = -1f);

    /*
      New modelled parts get a row here instead of a case in BuildPart:
      - Snout:      a horseshoe around the snout, stretched from behind the head cap to the nose tip
      - Hug:        around the body, stretched to its height and width at the spot
      - FlankPair:  on the skin of both flanks, as tall as the body there
      - Top:        on the back, standing up
      - Belly:      under the belly, hanging down
      - Tail:       behind the tail tip, reaching back
      - Eye:        an eyeball of radius 1 on both flanks where the plain eyes sit, +Z out of the head, it blinks
    */
    private static readonly IReadOnlyDictionary<CreaturePartKind, Placement> PLACEMENTS = new Dictionary<CreaturePartKind, Placement>
    {
        [CreaturePartKind.SharkTail]    = new(ModelSpace.Tail),
        [CreaturePartKind.NewtTail]     = new(ModelSpace.Tail),
        [CreaturePartKind.MantleFins]   = new(ModelSpace.Tail),
        [CreaturePartKind.FanTail]      = new(ModelSpace.Tail),
        [CreaturePartKind.CurlTail]     = new(ModelSpace.Tail),
        [CreaturePartKind.SharkFin]     = new(ModelSpace.Top),
        [CreaturePartKind.TallFin]      = new(ModelSpace.Top),
        [CreaturePartKind.Flipper]      = new(ModelSpace.FlankPair),
        [CreaturePartKind.Paddle]       = new(ModelSpace.FlankPair),
        [CreaturePartKind.NeedleMaw]    = new(ModelSpace.Snout),
        [CreaturePartKind.PufferMouth]  = new(ModelSpace.Snout),
        [CreaturePartKind.TubeSnout]    = new(ModelSpace.Snout),
        [CreaturePartKind.Carapace]     = new(ModelSpace.Hug, Size: 1.12f, Reach: 0.72f),
        [CreaturePartKind.FeatherGills] = new(ModelSpace.FlankPair, Size: 0.8f),
        [CreaturePartKind.Siphon]       = new(ModelSpace.Belly),
        [CreaturePartKind.SickleFin]     = new(ModelSpace.FlankPair),
        [CreaturePartKind.RoundFlipper]  = new(ModelSpace.FlankPair),
        [CreaturePartKind.HindPaddle]    = new(ModelSpace.FlankPair),
        [CreaturePartKind.FinRibbon]     = new(ModelSpace.Hug, Reach: 0.5f),
        [CreaturePartKind.SharkMaw]      = new(ModelSpace.Snout),
        [CreaturePartKind.OrcaMaw]       = new(ModelSpace.Snout),
        [CreaturePartKind.GapeMaw]       = new(ModelSpace.Snout),
        [CreaturePartKind.SquidBeak]     = new(ModelSpace.Snout),
        [CreaturePartKind.CrabMouth]     = new(ModelSpace.Snout),
        [CreaturePartKind.Smile]         = new(ModelSpace.Snout),
        [CreaturePartKind.BeadEye]       = new(ModelSpace.Eye),
        [CreaturePartKind.GhostEye]      = new(ModelSpace.Eye),
        [CreaturePartKind.BulgeEye]      = new(ModelSpace.Eye),
        [CreaturePartKind.PatchEye]      = new(ModelSpace.Eye),
        [CreaturePartKind.SquidEye]      = new(ModelSpace.Eye),
        [CreaturePartKind.PegEye]        = new(ModelSpace.Eye),
        [CreaturePartKind.LidEye]        = new(ModelSpace.Eye),
        [CreaturePartKind.TurretEye]     = new(ModelSpace.Eye),
        [CreaturePartKind.FrogEye]       = new(ModelSpace.Eye),
        [CreaturePartKind.CrocEye]       = new(ModelSpace.Eye),
        [CreaturePartKind.CrocTail]      = new(ModelSpace.Tail),
        [CreaturePartKind.SpiralShell]   = new(ModelSpace.Hug, Size: 1.1f, Reach: 0.6f),
        [CreaturePartKind.NautilusShell] = new(ModelSpace.Hug, Size: 1.1f, Reach: 0.7f),
        [CreaturePartKind.BackPlates]    = new(ModelSpace.Hug, Reach: 0.8f),
        [CreaturePartKind.CrocMaw]       = new(ModelSpace.Snout),
        [CreaturePartKind.JellyBell]     = new(ModelSpace.Hug, Size: 1.15f, Reach: 0.9f),
        [CreaturePartKind.OralArms]      = new(ModelSpace.Belly),
    };

    /// <summary>Builds the model of a kind listed in PLACEMENTS on its pivot and starts its animation.</summary>
    private void BuildPlaced(CreatureDesign design, CreaturePart part, Placement placement, Color color, IReadOnlyDictionary<string, Material> finSkin)
    {
        float x      = placement.Share < 0f ? design.XAt(part.T) : design.XAt(placement.Share);
        float top    = design.TopAt(x);
        float flank  = design.FlankAt(x);
        float size   = (0.4f + design.RadiusAt(x) * 0.5f) * placement.Size;
        var   models = new List<Node3D>();
        switch (placement.Space)
        {
            case ModelSpace.Snout:
                // In the opening the body mesh cut for this kind (Design/MouthShapes.cs)
                MouthShape shape = MouthShapes.Current.TryGetValue(part.Kind, out MouthShape cut) ? cut : new MouthShape();
                float  mouthX = shape.CornerX(design);
                float  width  = design.FlankAt(mouthX) * 1.03f * placement.Size;
                Node3D snout  = CreatureModels.NewModel(part.Kind, color, finSkin);
                snout.Position = new Vector3(0f, shape.Level * design.TopAt(mouthX), 0f);
                snout.Scale    = new Vector3((design.NoseTipX - mouthX) * 1.03f, design.TopAt(mouthX) * 0.75f * placement.Size, width);
                Node3D jaws = AddPivot(mouthX);
                jaws.AddChild(snout);
                models.Add(snout);
                AddTeeth(design, part, shape, jaws, snout.Position);
                break;
            case ModelSpace.Hug:
                Node3D hug = CreatureModels.NewModel(part.Kind, color, finSkin);
                hug.Scale = new Vector3(design.Length * placement.Reach, top * placement.Size, flank * placement.Size);
                AddPivot(x).AddChild(hug);
                models.Add(hug);
                break;
            case ModelSpace.FlankPair:
                Node3D flanks = AddPivot(x);
                foreach (float side in new[] { -1f, 1f })
                {
                    models.Add(CreatureModels.Attach(flanks, part.Kind, color, new Vector3(0f, top * 0.05f, side * flank * 0.96f), top * placement.Size, side, overrides: finSkin));
                }
                break;
            case ModelSpace.Top:
                models.Add(CreatureModels.Attach(AddPivot(x), part.Kind, color, new Vector3(0f, top * 0.92f, 0f), size, 1f, overrides: finSkin));
                break;
            case ModelSpace.Belly:
                models.Add(CreatureModels.Attach(AddPivot(x), part.Kind, color, new Vector3(0f, -top * 0.92f, 0f), size, 1f, overrides: finSkin));
                break;
            case ModelSpace.Tail:
                models.Add(CreatureModels.Attach(AddPivot(design.TailTipX + 0.06f), part.Kind, color, Vector3.Zero,
                    (0.6f + design.MeanGirth * 0.5f) * placement.Size, 1f, overrides: finSkin));
                break;
            case ModelSpace.Eye:
                Node3D sockets = AddPivot(x);
                foreach (float side in new[] { -1f, 1f })
                {
                    Node3D eye = CreatureModels.Attach(sockets, part.Kind, color, new Vector3(0f, top * 0.3f, side * flank * 0.88f),
                        (0.08f + design.RadiusAt(x) * 0.12f) * placement.Size, side, overrides: finSkin);
                    models.Add(eye);
                    _eyes.Add(eye);
                }
                break;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(placement), $"placement must use a known ModelSpace, got '{placement.Space}'.");
        }

        // Without a name in MODELS a rigged model plays its only loop, a static one stays still
        string animation = CreatureModels.MODELS[part.Kind].Animation ?? "Loop";
        for (int i = 0; i < models.Count; i++)
        {
            PartModels.PlayLoop(models[i], animation, i * 0.15f, MODEL_RATE);
        }
    }

    private const float TOOTH_CONE_HEIGHT = 0.6f;    // CellVisual3D.ConeMesh
    private const float TOOTH_CONE_WIDTH = 0.26f;
    private static readonly Color TOOTH_COLOR = new(0.97f, 0.95f, 0.88f);

    /*
      The teeth stand on the lips of the carved opening (Design/MouthTeeth.cs), one cone each,
      in a holder next to the mouth model on the same pivot. The poser grows every piece by
      the size and stretch of the part: the holder undoes that for the roots, so they stay on
      the lips and only the teeth themselves grow with the part.
    */
    /// <summary>Adds the teeth of a mouth shape to the pivot of its mouth model.</summary>
    /// <param name="jaws">(Required) The pivot of the mouth at the mouth corners.</param>
    /// <param name="at">(Required) Position of the mouth model on that pivot, the holder sits on the same spot (one anchor for the poser).</param>
    private static void AddTeeth(CreatureDesign design, CreaturePart part, MouthShape shape, Node3D jaws, Vector3 at)
    {
        List<Tooth> teeth = MouthTeeth.Place(design, shape);
        if (0 == teeth.Count)
        {
            return;
        }
        var holder = new Node3D { Position = at };
        jaws.AddChild(holder);
        Vector3  grown  = part.Proportion * part.Size;
        Vector3  origin = jaws.Position + at;
        Material ivory  = CellVisual3D.Material(TOOTH_COLOR, 0.22f, 0.3f);     // a little glow of their own: teeth stand in the shade of the head
        foreach (Tooth tooth in teeth)
        {
            // Cone along Y: wide along the lip, thin across it, the base on the root
            Vector3 across = tooth.Along.Cross(tooth.Direction).Normalized();
            var     basis  = new Basis(
                tooth.Along * (tooth.Width / TOOTH_CONE_WIDTH),
                tooth.Direction * (tooth.Length / TOOTH_CONE_HEIGHT),
                across * (tooth.Width * shape.ToothFlat / TOOTH_CONE_WIDTH));
            Vector3 center = tooth.Root + tooth.Direction * tooth.Length * 0.5f - origin;
            var     mesh   = new MeshInstance3D
            {
                Mesh             = CellVisual3D.ConeMesh(),
                MaterialOverride = ivory,
                Transform        = new Transform3D(basis, center / grown),
                CastShadow       = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            holder.AddChild(mesh);
        }
    }

    /// <summary>A model that hugs the body (spines, shell): built around a unit sphere, stretched to the body at its pivot.</summary>
    private static void AttachHugging(Node3D pivot, CreaturePartKind kind, Color color, Vector3 scale)
    {
        Node3D model = CreatureModels.NewModel(kind, color);
        model.Scale = scale;
        pivot.AddChild(model);
    }

    /// <summary>A pair of wing models on the back, flapping together.</summary>
    private void BuildWings(CreaturePartKind kind, float x, float top, float flank, float radius, Color color)
    {
        Node3D shoulders = AddPivot(x);
        float  span      = (0.7f + radius * 0.8f) * 1.15f;
        foreach (float side in new[] { -1f, 1f })
        {
            Node3D model = CreatureModels.Attach(shoulders, kind, color, new Vector3(0f, top * 0.8f, side * flank * 0.4f), span, side);
            CreatureClay.AddFillet(shoulders, new Vector3(0f, top * 0.8f, side * flank * 0.4f), span * CreatureClay.WING_FILLET, _knuckleClay);
            PartModels.PlayLoop(model, CreatureModels.MODELS[kind].Animation, 0f, FLAP_RATE);
        }
    }

    /// <param name="side">(Required) -1 left flank, 1 right flank, the pupil looks outwards there.</param>
    private void BuildEye(Node3D pivot, Vector3 position, float side, float size)
    {
        var eye = new Node3D { Position = position, Scale = Vector3.One };
        pivot.AddChild(eye);
        CellVisual3D.AddMesh(eye, CellVisual3D.SphereMesh(), CellVisual3D.Material(new Color(0.97f, 0.97f, 0.97f), 0.25f, 0.2f), Vector3.Zero, Vector3.One * size);
        CellVisual3D.AddMesh(eye, CellVisual3D.SphereMesh(), CellVisual3D.Material(new Color(0.06f, 0.06f, 0.1f), 0f, 0.1f),
            new Vector3(size * 0.35f, size * 0.15f, side * size * 0.6f), Vector3.One * size * 0.55f);
        CellVisual3D.AddMesh(eye, CellVisual3D.SphereMesh(), CellVisual3D.Material(Colors.White, 1.5f),
            new Vector3(size * 0.5f, size * 0.55f, side * size * 0.75f), Vector3.One * size * 0.18f);
        _eyes.Add(eye);
    }

    /// <summary>A pivot on the bent spine at an X: parts built on it sit at the height of the spine there.</summary>
    private Node3D AddPivot(float x)
    {
        float y     = (_pivotDesign ?? _design).LiftAt(x);
        var   pivot = new Node3D { Position = new Vector3(x, y, 0f) };
        (_pivotParent ?? this).AddChild(pivot);
        _attached.Add(new Attached(pivot, x, y, null != _pivotParent));
        return pivot;
    }

    // ===========================================================================
    // ============================   ANIMATE PARTS   ============================
    // ===========================================================================
    //
    //   Swim    Moves and turns every pivot with the swim wave.
    //   Blink   Closes or opens the eyes.
    //   Spout   Lets the droplets of the blowhole rise and fade.
    //

    /// <summary>Moves every pivot with the swim wave of the body (Visual/SwimWave.cs) and turns it along the wave.</summary>
    /// <param name="halfLength">(Required) CreatureDesign.HalfLength.</param>
    /// <param name="amplitude">(Required) Offset of the wave at the tail.</param>
    /// <param name="phase">(Required) Wave phase.</param>
    /// <param name="isVertical">(Required) True for a marine mammal (up and down), false for a fish (side to side).</param>
    public void Swim(float halfLength, float amplitude, float phase, bool isVertical)
    {
        foreach (Attached part in _attached)
        {
            if (part.IsOnLimb)
            {
                continue;                            // rides its limb
            }
            float bend  = SwimWave.Bend(part.X, halfLength, amplitude, phase);
            float slope = (SwimWave.Bend(part.X + 0.01f, halfLength, amplitude, phase) - SwimWave.Bend(part.X - 0.01f, halfLength, amplitude, phase)) / 0.02f;
            part.Pivot.Position = isVertical ? new Vector3(part.X, part.Y + bend, 0f) : new Vector3(part.X, part.Y, bend);
            part.Pivot.Rotation = isVertical ? new Vector3(0f, 0f, Mathf.Atan(slope)) : new Vector3(0f, -Mathf.Atan(slope), 0f);
        }
    }

    /// <summary>Closes or opens the eyes.</summary>
    /// <param name="isBlinking">(Required) True while the eyes are shut.</param>
    public void Blink(bool isBlinking)
    {
        foreach (Node3D eye in _eyes)
        {
            Vector3 open = _eyeScales.TryGetValue(eye, out Vector3 posed) ? posed : Vector3.One;
            eye.Scale = new Vector3(open.X, open.Y * (isBlinking ? 0.15f : 1f), open.Z);
        }
    }

    /// <summary>Spout: three droplets rise from the blowhole and fade.</summary>
    /// <param name="spout">(Required) Progress of the breath, the droplets show between 0 and 1.</param>
    public void Spout(float spout)
    {
        for (int i = 0; i < _spouts.Count; i++)
        {
            float t = spout - i * 0.12f;
            _spouts[i].Visible  = t is > 0f and < 1f;
            _spouts[i].Position = new Vector3((i - 1) * 0.03f * t, 0.05f + t * 0.45f, (i - 1) * 0.04f * t);
            _spouts[i].Scale    = Vector3.One * 0.05f * (1f - t * 0.6f);
        }
    }

    // ===========================================================================
    // =============================   PICK PARTS   ==============================
    // ===========================================================================
    //
    //   PartAt             The placed part under a ray.
    //   TryGetPartAnchor   Where the gizmo of a part sits, in global space.
    //   TryGetLimbFrame    Where a sculpted limb sits, for the joint handles of the editor.
    //

    /// <summary>The placed part whose mesh bounds a ray hits first (CreaturePartPoser.PartAt).</summary>
    /// <param name="from">(Required) Ray origin in global space.</param>
    /// <param name="direction">(Required) Ray direction in global space.</param>
    /// <param name="flank">The flank of the hit piece, 0 for a midline part.</param>
    /// <returns>Index in Design.Parts, -1 when the ray hits no part.</returns>
    public int PartAt(Vector3 from, Vector3 direction, out int flank)
    {
        return _poser.PartAt(from, direction, out flank);
    }

    /// <summary>Where the gizmo of a part sits: its shifted anchor (rest pose, without the swim wave).</summary>
    /// <param name="index">(Required) Index in Design.Parts.</param>
    /// <param name="flank">(Required) -1 or 1 for one half of a pair, 0 for a midline part.</param>
    /// <param name="anchor">The anchor in global space.</param>
    /// <returns>False when the part has no group on that flank (not built yet).</returns>
    public bool TryGetPartAnchor(int index, int flank, out Vector3 anchor)
    {
        if (!_poser.TryGetPartAnchor(index, flank, out Vector3 local))
        {
            anchor = Vector3.Zero;
            return false;
        }
        anchor = ToGlobal(local);
        return true;
    }

    /// <summary>The posed shoulder of a sculpted limb on one flank (CreatureLimbs3D.TryGetFrame).</summary>
    /// <param name="index">(Required) Index in Design.Parts, a Limb.</param>
    /// <param name="flank">(Required) -1 or 1.</param>
    /// <param name="frame">The global transform of the limb piece (pose, size and mirror included).</param>
    /// <returns>False when the limb has no piece on that flank.</returns>
    public bool TryGetLimbFrame(int index, int flank, out Transform3D frame)
    {
        frame = Transform3D.Identity;
        return null != _limbs && _limbs.TryGetFrame(index, flank, out frame);
    }
}
