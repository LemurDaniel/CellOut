// NOTE:
//   The way from the RNA world to the protocell (Prelude/RnaWorld.cs), played on the
//   player strand itself once the player evolves: the strand coils up into a short helix,
//   the ribozyme head melts into it, lipids drift in and line up into a shell around it,
//   the membrane closes, then a partner strand grows base by base along the RNA (A-T, G-C,
//   the U turns into T): the RNA becomes the DNA double helix of the protocell. Two
//   heartbeats in a warm light, then RnaWorld hands over to the real protocell.
//   Takes over the drawing of the player strand: RnaWorld hides the strand and builds this
//   component with its head and links at the same spots.
//
//   var genesis = new RnaGenesis();
//   genesis.Setup(palette, size, cellColor, center, strand.HeadPosition, strand.Chain.Links);
//   AddChild(genesis);
//   genesis.Animate(time);             // every frame, seconds since the start
//   if (genesis.IsDone) { ... }

//
//   Chapters:
//     SETTINGS   The timeline and the shape of the helix.
//     BUILD      Head, links, partner strand, lipids, membrane and the warm light.
//     ANIMATE    Coil, lipid shell, membrane, the partner strand, heartbeats.
//     HELPERS    Spots on the helix.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class RnaGenesis : Node3D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   DURATION   Seconds from the start to the handover.
    //   IsDone     Whether the animation reached DURATION.
    //   DnaPose    Pose of the helix at the handover, the protocell blends its DNA from there.
    //
    //   Timeline in seconds:
    //     0.0 - 1.6   the strand coils into a helix around the center, the head melts into it
    //     0.3 - 2.4   lipids drift in and line up into a shell
    //     2.0 - 2.8   the membrane closes over the shell
    //     2.5 - 4.1   the partner strand grows base by base: RNA becomes DNA
    //     4.4, 4.95   two heartbeats, the warm light swells with them
    //     5.6         done, RnaWorld hands over
    //
    //   Lengths in body sizes, close to the DNA of the protocell (CellVisual3D.BuildDna).
    //

    public const float DURATION = 5.6f;
    private const float COIL_END = 1.6f;
    private const float HEAD_FADE_START = 0.6f;
    private const float LIPID_START = 0.3f;
    private const float LIPID_END = 2.4f;
    private const float MEMBRANE_START = 2f;
    private const float MEMBRANE_END = 2.8f;
    private const float PARTNER_START = 2.5f;
    private const float PARTNER_SWEEP = 1f;           // seconds from the first to the last pair
    private const float PARTNER_GROW = 0.6f;          // seconds one partner base needs
    private static readonly float[] BEAT_TIMES = { 4.4f, 4.95f };
    private const float BEAT_SECONDS = 0.5f;

    private const float HELIX_LENGTH = 1.05f;
    private const float HELIX_RADIUS = 0.15f;
    private const float HELIX_TURNS = 1.75f;
    private const float HELIX_LIFT = 0.04f;           // the helix floats a bit above the middle of the cell
    private const float HELIX_SPIN = 0.9f;            // radians per second around its own axis
    private const float BEAD_RADIUS = 0.05f;          // a link once it sits in the helix
    private const float BOND_RADIUS = 0.022f;
    private const int LIPIDS = 80;
    private const float WARM_ENERGY = 1.6f;
    private static readonly Shader MEMBRANE_SHADER = GD.Load<Shader>("res://shaders/membrane.gdshader");
    private static readonly Color LIPID_COLOR = new(1f, 0.9f, 0.55f);
    private static readonly Color WARM_COLOR = new(1f, 0.85f, 0.6f);

    public bool IsDone => _time >= DURATION;

    /// <summary>
    /// Pose of the helix at DURATION in body sizes around the cell center, turned like the
    /// world (not like the cell). The helix has the shape of CellVisual3D.BuildDna along
    /// its local X: the spin turns it around that axis, the phase grows with it.
    /// </summary>
    public Transform3D DnaPose => new(_axis * new Basis(Vector3.Right, -DURATION * HELIX_SPIN), new Vector3(0f, HELIX_LIFT, 0f));

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   Setup           (Required) palette, size, cellColor, center, head, links.
    //                   Builds every part at the spot of the strand it replaces.
    //   BuildStrands    Head, beads and bonds of the RNA, the hidden partner strand and its rungs.
    //   BuildShell      Lipids scattered wide, the hidden membrane and the warm light.
    //

    private sealed record Pair(MeshInstance3D Bead, MeshInstance3D Bond, Vector3 Start,
        MeshInstance3D Partner, MeshInstance3D PartnerBond, MeshInstance3D Rung, MeshInstance3D PartnerRung);
    private sealed record Lipid(MeshInstance3D Mesh, Vector3 Start, Vector3 Target, Basis Facing, float Delay);

    private readonly List<Pair> _pairs = new();
    private readonly List<Lipid> _lipids = new();
    private float _size;
    private Basis _axis;                // turns the helix (local +X) to the direction the strand swam
    private MeshInstance3D _head;
    private Vector3 _headStart;
    private MeshInstance3D _membrane;
    private ShaderMaterial _membraneMaterial;
    private Color _cellColor;
    private OmniLight3D _warmLight;
    private float _time;

    /// <summary>Builds every part at the spot of the strand it replaces, called once before AddChild.</summary>
    /// <param name="palette">(Required) The shared materials of the prelude, the beads keep their colors.</param>
    /// <param name="size">(Required) Body size of the player, the cell gets exactly this size.</param>
    /// <param name="cellColor">(Required) Membrane color of the protocell to be.</param>
    /// <param name="center">(Required) Plane point the cell forms around.</param>
    /// <param name="head">(Required) Plane position of the ribozyme head.</param>
    /// <param name="links">(Required) The links of the player chain, the first one hangs on the head.</param>
    public void Setup(RnaPalette palette, float size, Color cellColor, Vector2 center, Vector2 head, IReadOnlyList<RnaChain.Link> links)
    {
        _size      = size;
        _cellColor = cellColor;
        Position   = Plane3D.To3D(center);

        // The helix lies along the strand: the head end points where the strand swam
        Vector2 tail = 0 == links.Count ? center : links[^1].Position;
        Vector2 line = head - tail;
        _axis = new Basis(Vector3.Up, Plane3D.FacingToYaw(line.IsZeroApprox() ? 0f : line.Angle()));

        BuildStrands(palette, head - center, links, center);
        BuildShell();
    }

    private void BuildStrands(RnaPalette palette, Vector2 head, IReadOnlyList<RnaChain.Link> links, Vector2 center)
    {
        StandardMaterial3D backbone = RnaPalette.Glow(RnaPalette.BACKBONE_COLOR, 0.4f);
        _headStart = Plane3D.To3D(head);
        _head      = NewMesh(CellVisual3D.SphereMesh(), RnaPalette.Glow(RnaPalette.BACKBONE_COLOR, 0.35f));

        // One pair per link: the RNA bead as it is, the partner pairs with its base (U pairs with A, in DNA that is T)
        foreach (RnaChain.Link link in links)
        {
            StandardMaterial3D linkMaterial    = palette.LinkMaterial(link.Kind, link.Mutation);
            StandardMaterial3D partnerMaterial = palette.BaseMaterial(RnaTemplate.Pair(link.Kind));
            _pairs.Add(new Pair(
                NewMesh(CellVisual3D.SphereMesh(true), linkMaterial),
                NewMesh(CellVisual3D.RodMesh(), backbone),
                Plane3D.To3D(link.Position - center),
                NewMesh(CellVisual3D.SphereMesh(true), partnerMaterial),
                NewMesh(CellVisual3D.RodMesh(), backbone),
                NewMesh(CellVisual3D.RodMesh(), palette.BaseMaterial(link.Kind)),
                NewMesh(CellVisual3D.RodMesh(), partnerMaterial)));
        }
    }

    private void BuildShell()
    {
        Material material = CellVisual3D.Material(LIPID_COLOR, 0.2f, 0.3f);
        for (int i = 0; i < LIPIDS; i++)
        {
            (Vector3 target, Basis facing) = Intro.ShellSpot(i, LIPIDS);
            float angle = Rand.Angle();
            var   start = new Vector3(Mathf.Cos(angle), Rand.Range(-0.2f, 0.6f), Mathf.Sin(angle)) * Rand.Range(2.5f, 5f);
            _lipids.Add(new Lipid(NewMesh(CellVisual3D.RodMesh(), material), start, target, facing, Rand.Range(0f, 0.5f)));
        }

        _membraneMaterial = new ShaderMaterial { Shader = MEMBRANE_SHADER };
        _membraneMaterial.SetShaderParameter("body_color", new Color(_cellColor, 0f));
        _membraneMaterial.SetShaderParameter("seed", Rand.Range(0f, 100f));
        _membraneMaterial.SetShaderParameter("core_alpha", 0.2f);
        _membrane = NewMesh(CellVisual3D.SphereMesh(), _membraneMaterial);

        _warmLight = new OmniLight3D
        {
            LightColor  = WARM_COLOR,
            OmniRange   = _size * 6f,
            LightEnergy = 0f,
            Position    = Vector3.Up * _size * 1.5f,
        };
        AddChild(_warmLight);
    }

    private MeshInstance3D NewMesh(Mesh mesh, Material material)
    {
        MeshInstance3D instance = CellVisual3D.AddMesh(this, mesh, material, Vector3.Zero, Vector3.One * 0.001f);
        instance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return instance;
    }

    // ===========================================================================
    // ===============================   ANIMATE   ===============================
    // ===========================================================================
    //
    //   1. Coil        The links glide onto the helix, the head melts into its front end.
    //   2. Partner     A partner base grows out of the axis next to every link, front to back.
    //   3. Lipids      Drift onto the shell, melt into the membrane once it closes.
    //   4. Membrane    Closes over the shell and beats twice, the warm light swells with it.
    //

    /// <summary>Shows the frame at a time of the animation.</summary>
    /// <param name="time">(Required) Seconds since the player evolved.</param>
    public void Animate(float time)
    {
        _time = time;

        // ---------------------------------------------------------------------------
        // Coil
        //   The links glide onto the helix, the head melts into its front end.
        // ---------------------------------------------------------------------------

        float   coil      = Ease.InOut(Ease.Window(time, 0f, COIL_END));
        float   spin      = time * HELIX_SPIN;
        int     count     = _pairs.Count;
        Vector3 headSpot  = HelixSpot(-1, count, 0f, spin);
        float   headScale = RnaStrand.HEAD_RADIUS * (1f - Ease.Smooth(Ease.Window(time, HEAD_FADE_START, COIL_END)));
        _head.Position = _headStart.Lerp(headSpot, coil);
        _head.Scale    = Vector3.One * Mathf.Max(headScale, 0.0005f) * _size;

        float   beadScale = Mathf.Lerp(RnaStrand.LINK_RADIUS, BEAD_RADIUS, coil);
        Vector3 front     = _head.Position;
        var     beads     = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            // Waves a little on its way, like the swimming strand
            Pair    pair = _pairs[i];
            Vector3 wave = Vector3.Up * Mathf.Sin(time * 4f - i * 0.7f) * 0.05f * _size * (1f - coil);
            beads[i] = pair.Start.Lerp(HelixSpot(i, count, 0f, spin), coil) + wave;
            pair.Bead.Position = beads[i];
            pair.Bead.Scale    = Vector3.One * beadScale * _size;
            pair.Bond.Transform = Intro.Rod(i == 0 ? front : beads[i - 1], beads[i], Mathf.Lerp(0.035f, BOND_RADIUS, coil) * _size);
        }

        // ---------------------------------------------------------------------------
        // Partner
        //   A partner base grows out of the axis next to every link, front to back.
        // ---------------------------------------------------------------------------

        Vector3 previous     = Vector3.Zero;
        float   previousGrow = 0f;
        for (int i = 0; i < count; i++)
        {
            Pair    pair    = _pairs[i];
            float   start   = PARTNER_START + (count > 1 ? (float)i / (count - 1) : 0f) * PARTNER_SWEEP;
            float   grow    = Ease.Window(time, start, start + PARTNER_GROW);
            Vector3 axis    = AxisSpot(i, count);
            Vector3 partner = axis.Lerp(HelixSpot(i, count, Mathf.Pi, spin), Ease.Smooth(grow));
            pair.Partner.Position = partner;
            pair.Partner.Scale    = Vector3.One * Mathf.Max(BEAD_RADIUS * Ease.BackOut(grow), 0.0005f) * _size;

            // The two halves of the rung: the base of the link and the base of the partner
            float rung = Mathf.Max(BOND_RADIUS * grow, 0.0005f) * _size;
            pair.Rung.Transform        = Intro.Rod(beads[i], axis, rung);
            pair.PartnerRung.Transform = Intro.Rod(partner, axis, rung);

            // The partner backbone links up behind the growing front
            float bond = i > 0 ? Mathf.Min(grow, previousGrow) : 0f;
            pair.PartnerBond.Transform = Intro.Rod(i > 0 ? previous : partner, partner, Mathf.Max(BOND_RADIUS * bond, 0.0005f) * _size);
            previous     = partner;
            previousGrow = grow;
        }

        // ---------------------------------------------------------------------------
        // Lipids
        //   Drift onto the shell, melt into the membrane once it closes.
        // ---------------------------------------------------------------------------

        float closing = Ease.Window(time, MEMBRANE_START, MEMBRANE_END);
        float melt    = 1f - Ease.Smooth(closing);
        foreach (Lipid lipid in _lipids)
        {
            float   gather = Ease.InOut(Ease.Window(time, LIPID_START + lipid.Delay, LIPID_END - 0.5f + lipid.Delay));
            Vector3 drift  = lipid.Start.Rotated(Vector3.Up, time * 0.6f);
            Vector3 spot   = lipid.Target.Rotated(Vector3.Up, (1f - gather) * 1.8f);
            float   appear = Ease.Window(time, LIPID_START + lipid.Delay * 0.5f, LIPID_START + 0.5f + lipid.Delay * 0.5f);
            Basis   facing = Basis.Identity.Slerp(lipid.Facing, gather);
            Vector3 scale  = new Vector3(0.035f, 0.11f, 0.035f) * Mathf.Max(appear * melt, 0.005f) * _size;
            lipid.Mesh.Transform = new Transform3D(facing * Basis.FromScale(scale), drift.Lerp(spot, gather) * _size);
        }

        // ---------------------------------------------------------------------------
        // Membrane
        //   Closes over the shell and beats twice, the warm light swells with it.
        // ---------------------------------------------------------------------------

        float beat = 0f;
        foreach (float beatTime in BEAT_TIMES)
        {
            beat = Mathf.Max(beat, Ease.Pulse(time, beatTime, BEAT_SECONDS));
        }
        float bubble = closing > 0f ? Mathf.Lerp(0.92f, 1f, Ease.BackOut(closing)) * (1f + 0.08f * beat) * _size : 0.001f;
        _membrane.Scale = new Vector3(bubble, bubble * CellVisual3D.FLATTEN, bubble);
        _membraneMaterial.SetShaderParameter("body_color", new Color(_cellColor, 0.8f * Ease.Smooth(closing)));

        float warmth = Ease.Smooth(Ease.Window(time, 0.5f, MEMBRANE_END)) * (1f - Ease.Smooth(Ease.Window(time, DURATION - 0.5f, DURATION)));
        _warmLight.LightEnergy = WARM_ENERGY * warmth * (1f + 0.6f * beat);
    }

    // ===========================================================================
    // ===============================   HELPERS   ===============================
    // ===========================================================================
    //
    //   HelixSpot   Spot of link i on one side of the helix, index -1 is the head in front of it.
    //   AxisSpot    Point on the axis of the helix at link i, where the rung halves meet.
    //

    /// <summary>
    /// Link 0 hangs on the head, so it sits at the front end (+X) and the last link at the
    /// back. The side turns the spot half around the axis for the partner strand.
    /// </summary>
    private Vector3 HelixSpot(int index, int count, float side, float spin)
    {
        float   t      = Along(index, count);
        float   phase  = t * HELIX_TURNS * Mathf.Tau + side + spin;
        var     around = new Vector3(0f, Mathf.Sin(phase), Mathf.Cos(phase)) * HELIX_RADIUS;
        return _axis * (AxisLocal(t) + around) * _size;
    }

    private Vector3 AxisSpot(int index, int count)
    {
        return _axis * AxisLocal(Along(index, count)) * _size;
    }

    // 1 at the front end (link 0), 0 at the back end, the head one step in front of link 0
    private static float Along(int index, int count)
    {
        return count > 1 ? 1f - (float)index / (count - 1) : 0.5f;
    }

    private static Vector3 AxisLocal(float t)
    {
        return new Vector3((t - 0.5f) * HELIX_LENGTH, HELIX_LIFT, 0f);
    }
}
