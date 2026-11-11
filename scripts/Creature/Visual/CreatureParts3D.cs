// NOTE:
//   The parts of a 3D organism: fins, eyes, mouths (jaw, filter mouth, beak), spines, shell,
//   poison gland, blowhole, sculpted limbs with feet and hands, a brain, the nostrils of a
//   lung and wings, each on a pivot along the
//   spine that rides the swim wave. Legs, hands, wings, brain, stalk eyes and fins are
//   models from tools/blender (Visual/CreatureModels.cs), the rest are primitive meshes.
//   A node component of CreatureVisual3D: a child at the origin of the visual, built once
//   and replaced by a new one on every rebuild. The visual tells it the wave, the blink
//   and the spout of the frame; picking answers come back as return values.
//   A new creature part gets its case in BuildPart (see CLAUDE.md, "Neues Kreatur-Teil").
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
    //   BuildMouth   A filter mouth with baleen or a beak at the nose.
    //   BuildShell   Overlapping armor bands over the back.
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

    /// <summary>Kinds that leave the size of their tab: the shell hugs the body, so it is built in body scale.</summary>
    private static readonly IReadOnlyDictionary<CreaturePartKind, float> BASE_KIND_SIZES = new Dictionary<CreaturePartKind, float>
    {
        [CreaturePartKind.Shell]       = 1f,
        [CreaturePartKind.PoisonGland] = 1.4f,
    };

    private sealed record Attached(Node3D Pivot, float X, float Y);   // Y: height of the bent spine there

    private readonly List<Attached> _attached = new();
    private readonly List<Node3D> _eyes = new();
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
            float         size    = BASE_KIND_SIZES.TryGetValue(part.Kind, out float kindSize) ? kindSize : BASE_PART_SIZES[part.Info.Category];
            List<Node3D>  dropped = _poser.Pose(index, part, pivots, size);
            foreach (Node3D piece in dropped)
            {
                _eyes.RemoveAll(eye => eye == piece || piece.IsAncestorOf(eye));
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
    }

    private void BuildPart(CreatureDesign design, CreaturePart part, Color color)
    {
        var                finSkin = new Dictionary<string, Material> { ["Fin"] = CreatureModels.FinMaterial(design, color) };
        Color              outline = color.Darkened(0.5f);
        float              x       = design.XAt(part.T);
        float              radius  = design.RadiusAt(x);
        float              top     = design.TopAt(x);
        float              flank   = design.FlankAt(x);
        float              finSize = 0.6f + design.MeanGirth * 0.5f;

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
            case CreaturePartKind.Spines:
                for (int i = -2; i <= 2; i++)
                {
                    float   spineX = x + i * 0.09f;
                    Node3D  pivot  = AddPivot(spineX);
                    MeshInstance3D spine = CellVisual3D.AddMesh(pivot, CellVisual3D.ConeMesh(), CellVisual3D.Material(color.Lightened(0.4f), 0f, 0.35f),
                        new Vector3(0f, design.TopAt(spineX) + 0.08f, 0f), new Vector3(0.6f, 0.55f, 0.6f));
                    spine.Rotation = new Vector3(0f, 0f, 0.35f);   // swept back a little
                    foreach (float side in new[] { -1f, 1f })
                    {
                        MeshInstance3D flankSpine = CellVisual3D.AddMesh(pivot, CellVisual3D.ConeMesh(), CellVisual3D.Material(color.Lightened(0.4f), 0f, 0.35f),
                            new Vector3(0f, design.TopAt(spineX) * 0.3f, side * (design.FlankAt(spineX) + 0.06f)), new Vector3(0.5f, 0.45f, 0.5f));
                        flankSpine.Rotation = new Vector3(side * Mathf.Pi * 0.4f, 0f, 0f);
                    }
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

            // Sculpted limb: a clay tube through its joints, on both flanks
            case CreaturePartKind.StubLimb:
            case CreaturePartKind.BentLimb:
            case CreaturePartKind.Limb:
            case CreaturePartKind.Tentacle:
            // Feet and hands: a sculpted limb with the foot or hand model on its tip
            case CreaturePartKind.Legs:
            case CreaturePartKind.StubbyLegs:
            case CreaturePartKind.ClawLegs:
            case CreaturePartKind.CrabLegs:
            case CreaturePartKind.Hands:
            case CreaturePartKind.Claws:
                _limbs.Build(AddPivot(_limbs.RootOn(design, part, 1).X), design, part, color);
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

            // Nose: a dark mouth slit with a row of teeth
            case CreaturePartKind.Jaw:
                float  noseX = design.NoseTipX;
                float  mouthX = noseX - Mathf.Max(0.1f, (noseX - design.SpineEndX) * 0.45f);
                Node3D jaw   = AddPivot(mouthX);
                float  mouthTop   = design.TopAt(mouthX);
                float  mouthFlank = design.FlankAt(mouthX);
                CellVisual3D.AddMesh(jaw, CellVisual3D.SphereMesh(), CellVisual3D.Material(new Color(0.25f, 0.06f, 0.08f)),
                    new Vector3(0.02f, -mouthTop * 0.25f, 0f), new Vector3(0.16f, 0.035f, mouthFlank * 0.95f));
                for (int i = -2; i <= 2; i++)
                {
                    float          along = i / 2f;
                    MeshInstance3D tooth = CellVisual3D.AddMesh(jaw, CellVisual3D.ConeMesh(), CellVisual3D.Material(new Color(0.97f, 0.95f, 0.88f), 0.1f, 0.3f),
                        new Vector3(0.08f - Mathf.Abs(along) * 0.08f, -mouthTop * 0.25f + 0.02f, along * mouthFlank * 0.8f), new Vector3(0.35f, 0.15f, 0.35f));
                    tooth.Rotation = new Vector3(Mathf.Pi, 0f, 0f);   // tip down
                }
                break;

            // Nose: a wide gape with a comb of baleen, or a horny beak over the nose tip
            case CreaturePartKind.FilterMouth:
            case CreaturePartKind.Beak:
                BuildMouth(design, part.Kind);
                break;

            // Back: armor bands from flank to flank
            case CreaturePartKind.Shell:
                BuildShell(design, x, color);
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

    private const int BALEEN_COUNT = 9;
    private const float BEAK_OVERHANG = 0.18f;       // the beak reaches this far past the nose tip
    private const float CONE_HEIGHT = 0.6f;          // CellVisual3D.ConeMesh: height and bottom radius
    private const float CONE_RADIUS = 0.13f;
    private const int SHELL_BANDS = 2;               // bands to each side of the middle one
    private static readonly Color BALEEN_COLOR = new(0.93f, 0.9f, 0.78f);
    private static readonly Color BEAK_COLOR = new(0.96f, 0.68f, 0.22f);
    private static readonly Color POISON_COLOR = new(0.62f, 0.25f, 0.78f);

    /// <summary>The plant mouths at the nose: a wide gape with hanging baleen (filter mouth) or two horny cones (beak).</summary>
    private void BuildMouth(CreatureDesign design, CreaturePartKind kind)
    {
        float  noseX      = design.NoseTipX;
        float  mouthX     = noseX - Mathf.Max(0.1f, (noseX - design.SpineEndX) * 0.45f);
        float  mouthTop   = design.TopAt(mouthX);
        float  mouthFlank = design.FlankAt(mouthX);
        Node3D mouth      = AddPivot(mouthX);

        if (CreaturePartKind.FilterMouth == kind)
        {
            // A gape wider than the jaw, the baleen hang from the upper lip like a comb
            float lipY = -mouthTop * 0.22f;
            CellVisual3D.AddMesh(mouth, CellVisual3D.SphereMesh(), CellVisual3D.Material(new Color(0.2f, 0.07f, 0.1f)),
                new Vector3(0.02f, lipY, 0f), new Vector3(0.13f, 0.035f, mouthFlank * 0.8f));
            for (int i = 0; i < BALEEN_COUNT; i++)
            {
                float along  = i / (BALEEN_COUNT - 1f) * 2f - 1f;                 // -1 left .. 1 right
                float bristle = 0.14f - Mathf.Abs(along) * 0.05f;
                CellVisual3D.AddMesh(mouth, CellVisual3D.RodMesh(), CellVisual3D.Material(BALEEN_COLOR, 0.05f, 0.5f),
                    new Vector3(0.15f - Mathf.Abs(along) * 0.1f, lipY - bristle * 0.5f - 0.02f, along * mouthFlank * 0.76f), new Vector3(0.016f, bristle, 0.016f));
            }
            return;
        }

        // Beak: both cones start at the mouth and meet past the nose tip, the upper one hooks over the lower
        float length    = noseX - mouthX + BEAK_OVERHANG;
        float width     = mouthFlank * 0.5f / CONE_RADIUS;                     // across the nose
        float height    = mouthTop * 0.3f / CONE_RADIUS;                       // each half is flat
        foreach (float half in new[] { 1f, -1f })
        {
            float          size = half > 0f ? 1f : 0.8f;
            MeshInstance3D horn = CellVisual3D.AddMesh(mouth, CellVisual3D.ConeMesh(), CellVisual3D.Material(half > 0f ? BEAK_COLOR : BEAK_COLOR.Darkened(0.15f), 0f, 0.3f),
                new Vector3(length * size * 0.5f, half * mouthTop * 0.16f - mouthTop * 0.15f, 0f),
                new Vector3(height, length * size / CONE_HEIGHT, width));
            horn.Rotation = new Vector3(0f, 0f, -Mathf.Pi * 0.5f - half * 0.14f);   // tip forward (local X is then up), the halves lean together
        }
    }

    /// <summary>Armor bands over the back, each a dome from flank to flank that pokes out of the body, the middle ones highest.</summary>
    private void BuildShell(CreatureDesign design, float x, Color color)
    {
        for (int i = -SHELL_BANDS; i <= SHELL_BANDS; i++)
        {
            float bandX = x + i * 0.11f;
            float rise  = 1.18f - Mathf.Abs(i) * 0.05f;
            Color tone  = 0 == i % 2 ? color.Darkened(0.4f) : color.Darkened(0.28f);
            CellVisual3D.AddMesh(AddPivot(bandX), CellVisual3D.SphereMesh(), CellVisual3D.Material(tone, 0f, 0.3f),
                new Vector3(0f, design.TopAt(bandX) * 0.2f, 0f), new Vector3(0.2f, design.TopAt(bandX) * rise * 0.82f, design.FlankAt(bandX) * rise * 0.95f));
        }
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
        float y     = _design.LiftAt(x);
        var   pivot = new Node3D { Position = new Vector3(x, y, 0f) };
        AddChild(pivot);
        _attached.Add(new Attached(pivot, x, y));
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
            eye.Scale = new Vector3(1f, isBlinking ? 0.15f : 1f, 1f);
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
