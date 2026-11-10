// NOTE:
//   Shows where a part in hand would go on the preview cell: the ghost (a parts-only
//   cell with just this part at its snapped spot) and a glowing dot on the membrane
//   under the cursor, red where the part finds no spot. While the ghost shows, the drag
//   icon at the cursor should hide, so the part seems to snap onto the cell: the ghost
//   asks for that through DragIconToggled. Lives in the world of the CellPreview.
//
//   var ghost = new PartGhost();
//   ghost.Setup(session, preview);
//   ghost.DragIconToggled += palette.SetDragIconVisible;
//   preview.AddToWorld(ghost);
//   bool isFitting = ghost.Preview(kind, new Vector2(-1f, 0f));

//
//   Chapters:
//     SETUP        Session, preview and the nodes of ghost and dot.
//     GHOST        The part at its snapped spot.
//     HOVER DOT    The dot on the membrane under the cursor.
//

using System;
using Godot;

namespace CellOut;

public partial class PartGhost : Node3D
{
    // ===========================================================================
    // ================================   SETUP   ================================
    // ===========================================================================
    //
    //   DragIconToggled   Asks to hide (false) or show (true) the drag icon at the cursor.
    //   Setup             Hands in the session and the preview the ghost lives in.
    //   _Ready            Builds ghost body and hover dot, both hidden until a part hovers.
    //

    /// <summary>Asks to show (true) or hide (false) the drag icon at the cursor.</summary>
    public event Action<bool> DragIconToggled;

    private CellEditSession _session;
    private CellPreview _preview;

    /// <summary>Hands in what the ghost works on, call before it enters the tree.</summary>
    /// <param name="session">(Required) The session whose draft the part would go on.</param>
    /// <param name="preview">(Required) The preview whose membrane the cursor hits.</param>
    public void Setup(CellEditSession session, CellPreview preview)
    {
        _session = session;
        _preview = preview;
    }

    public override void _Ready()
    {
        _ghostBody = new CellBody { Radius = 1f, IsPartsOnly = true };
        AddChild(_ghostBody);
        _hoverDot = CellVisual3D.AddMesh(this, CellVisual3D.SphereMesh(true), _hoverDotMaterial, Vector3.Zero, Vector3.One * 0.07f);
        _hoverDot.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _hoverDot.Visible    = false;
    }

    // ===========================================================================
    // ================================   GHOST   ================================
    // ===========================================================================
    //
    //   Kind             The part whose ghost shows, null when none.
    //   Preview          The part at its snapped spot, hidden when it does not fit there.
    //   PreviewAtPixel   Dot and ghost for a dragged part over a pixel of the preview.
    //   HideGhost        Removes the ghost, the drag icon shows again.
    //   SetColor         Paints the ghost in the color of the draft.
    //

    private CellBody _ghostBody;

    /// <summary>The part whose ghost shows on the cell, null when none does.</summary>
    public PartKind? Kind { get; private set; }

    /// <summary>The snapped unit position of the ghost, valid while Kind is set.</summary>
    public Vector2 Point { get; private set; }

    /// <summary>The snapped lift of the ghost, valid while Kind is set.</summary>
    public float Lift { get; private set; }

    /// <summary>Shows the ghost of a part where it would snap.</summary>
    /// <param name="kind">(Required) The part.</param>
    /// <param name="point">(Required) Position in unit space (core radius 1, +X forward).</param>
    /// <param name="lift">(Optional) Height on the membrane for rim parts, -1 bottom .. 1 top. Defaults to 0 (equator).</param>
    /// <returns>True when the part fits there and its ghost shows, false when the ghost is hidden.</returns>
    public bool Preview(PartKind kind, Vector2 point, float lift = 0f)
    {
        CellDesign draft = _session.Draft;
        if (!draft.TrySnap(kind, point, out Vector2 snapped, out float snappedLift, lift))
        {
            HideGhost();
            return false;
        }
        if (kind == Kind && snapped.IsEqualApprox(Point) && Mathf.IsEqualApprox(snappedLift, Lift))
        {
            return true;
        }

        // Ghost: a parts-only body with just this part, rebuilt only when kind or spot change
        var design = new CellDesign();
        design.SetShape(draft.Shape);
        design.SetHeights(draft.Heights);
        design.TryPlace(kind, snapped, 0, snappedLift);
        _ghostBody.Design = design;
        Kind              = kind;
        Point             = snapped;
        Lift              = snappedLift;
        DragIconToggled?.Invoke(false);
        return true;
    }

    /// <summary>A dragged part over the cell: the dot under the cursor and the ghost where the part would snap.</summary>
    /// <param name="pixel">(Required) Pixel in the preview.</param>
    /// <param name="kind">(Required) The dragged part.</param>
    public void PreviewAtPixel(Vector2 pixel, PartKind kind)
    {
        ShowDot(kind, pixel);
        if (_preview.TryHitSurface(pixel, out Vector2 spot, out float lift, out _))
        {
            Preview(kind, spot, lift);
        }
        else
        {
            HideGhost();
        }
    }

    public void HideGhost()
    {
        DragIconToggled?.Invoke(true);
        if (null == Kind)
        {
            return;
        }
        Kind              = null;
        _ghostBody.Design = new CellDesign();
    }

    /// <summary>Paints the ghost like the cell it would go on.</summary>
    public void SetColor(Color color)
    {
        _ghostBody.BodyColor = color;
    }

    // ===========================================================================
    // ==============================   HOVER DOT   ==============================
    // ===========================================================================
    //
    //   ShowDot       A glowing dot on the membrane under the cursor, red where the part finds no spot.
    //   HideDot       Hides the dot.
    //   UpdateHover   Hides ghost and dot once the mouse leaves the cell, pulses the dot.
    //

    private const float DOT_PULSE_SPEED = 8f;
    private static readonly Color DOT_FREE = new(1f, 0.85f, 0.55f);
    private static readonly Color DOT_TAKEN = new(1f, 0.3f, 0.25f);

    private MeshInstance3D _hoverDot;
    private float _dotTime;
    private readonly StandardMaterial3D _hoverDotMaterial = new()
    {
        AlbedoColor              = DOT_FREE,
        EmissionEnabled          = true,
        Emission                 = DOT_FREE,
        EmissionEnergyMultiplier = 1.4f,
        NoDepthTest              = true,     // stays visible inside the translucent membrane
    };

    /// <summary>Shows the dot where a part would touch the membrane.</summary>
    /// <param name="kind">(Required) The part in hand, it decides free or taken.</param>
    /// <param name="pixel">(Required) Pixel in the preview, a miss of the membrane hides the dot.</param>
    public void ShowDot(PartKind kind, Vector2 pixel)
    {
        if (!_preview.TryHitSurface(pixel, out Vector2 point, out float lift, out Vector3 surface))
        {
            HideDot();
            return;
        }
        Color color = _session.Draft.TrySnap(kind, point, out _, out _, lift) ? DOT_FREE : DOT_TAKEN;
        _hoverDotMaterial.AlbedoColor = color;
        _hoverDotMaterial.Emission    = color;
        _hoverDot.Position = surface;
        _hoverDot.Visible  = true;
    }

    public void HideDot()
    {
        _hoverDot.Visible = false;
    }

    /// <summary>Per frame: nothing in hand or the mouse off the cell hides ghost and dot, else the dot pulses.</summary>
    /// <param name="delta">(Required) Seconds since the last frame.</param>
    /// <param name="isInHand">(Required) Whether a palette part is selected or being dragged.</param>
    public void UpdateHover(float delta, bool isInHand)
    {
        if (!_preview.IsMouseOver || !isInHand || !_preview.IsShown)
        {
            HideGhost();
            HideDot();
            return;
        }
        _dotTime += delta;
        _hoverDot.Scale = Vector3.One * (0.07f + 0.015f * Mathf.Sin(_dotTime * DOT_PULSE_SPEED));
    }
}
