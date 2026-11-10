// NOTE:
//   Drag and drop of the creature editor: a tile dragged from the palette hangs at the
//   cursor as a floating part; over the clay it shows as a ghost where it would land.
//   While the ghost shows, the cursor icon hides, so the part seems to snap onto the
//   clay. The ghost only reports the drop (Dropped), the CreaturePartPlacer places the part,
//   the CreatureEditor ends the drag (EndDrag) when Godot sends NotificationDragEnd.
//   Stands next to the clay (CreatureStage.AddToScene), on the same swim wave.
//
//   var ghost = new CreatureDropGhost();
//   ghost.Setup(session, stage);
//   ghost.Dropped += placer.DropAt;
//   stage.AddToScene(ghost);
//   palette.Setup(session, ghost.StartDrag);

//
//   Chapters:
//     DRAG AND GHOST   A tile dragged over the clay shows its part where it would land.
//

using Godot;

namespace CellOut;

public partial class CreatureDropGhost : Node3D
{
    // ===========================================================================
    // ===========================   DRAG AND GHOST   ============================
    // ===========================================================================
    //
    //   DragStarted   Raised when a tile is picked up: the info card shows it.
    //   Dropped       Raised when the part is let go over the stage (kind, pixel, point on the clay).
    //   Dragged       The part hanging at the cursor, null without a drag.
    //   GhostKind     The part whose ghost shows on the clay, null when none does.
    //   GhostHit      Where the ghost was last shown.
    //   Setup         Builds the ghost visual and takes the drops of the stage.
    //   StartDrag     Starts dragging a tile: its part hangs at the cursor.
    //   CanDropPart   Over the clay: shows the ghost where the part would land.
    //   DropPart      Reports the drop where the ghost shows.
    //   PreviewPart   The part alone on a copy of the body (parts only) at its snapped spot,
    //                 hidden when it does not fit there.
    //   HideGhost     Removes the ghost, the cursor icon shows again.
    //   HideOutside   The ghost goes once the dragged part leaves the clay.
    //   EndDrag       The drag is over (dropped or cancelled): ghost and drag icon go.
    //

    private const int DRAG_ICON_PIXELS = 120;
    private const float GHOST_ALPHA = 0.6f;

    public event System.Action DragStarted;
    public event System.Action<CreaturePartKind, Vector2, Vector3?> Dropped;

    private CreatureEditorSession _session;
    private CreatureStage _stage;
    private Control _dragIcon;
    private CreaturePartInfo _dragged;
    private CreaturePartKind? _ghostKind;
    private Vector3 _ghostHit;
    private CreatureVisual3D _ghost;

    public CreaturePartInfo Dragged => _dragged;

    /// <summary>The part whose ghost shows on the clay, null when none does.</summary>
    public CreaturePartKind? GhostKind => _ghostKind;

    /// <summary>The point of the clay the ghost was last shown at, unit space.</summary>
    public Vector3 GhostHit => _ghostHit;

    /// <summary>Call once before the ghost enters the scene of the stage.</summary>
    /// <param name="session">(Required) The sitting: draft, points and symmetry.</param>
    /// <param name="stage">(Required) The stage the parts are dropped on.</param>
    public void Setup(CreatureEditorSession session, CreatureStage stage)
    {
        _session = session;
        _stage   = stage;

        // Ghost of a dragged part: parts only, on the same swim wave as the clay
        _ghost = new CreatureVisual3D { Knead = CreatureVisual3D.EDITOR_KNEAD, SwimScale = 0.3f, Seed = 7f, IsPartsOnly = true, Visible = false };
        AddChild(_ghost);

        _stage.SetDragForwarding(new Callable(),
            Callable.From<Vector2, Variant, bool>(CanDropPart),
            Callable.From<Vector2, Variant>(DropPart));
    }

    /// <summary>Starts dragging a palette tile. Unaffordable parts cannot be dragged.</summary>
    /// <param name="info">(Required) The part of the tile.</param>
    /// <param name="source">(Required) A control of the palette, it shows the drag preview.</param>
    /// <returns>The kind as drag data, nothing when the part cannot be dragged.</returns>
    public Variant StartDrag(CreaturePartInfo info, Control source)
    {
        if (_session.IsChoosingType || _session.CostOf(info) > _session.Points)
        {
            return default;
        }

        // The cursor sits in the middle of the floating part
        _dragIcon = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        Control icon = CreatureIcon.New(CreatureIcon.DesignFor(info, _session.Draft.Type), _session.DraftColor, DRAG_ICON_PIXELS, isTurning: false, isPartsOnly: true);
        icon.Position = -Vector2.One * DRAG_ICON_PIXELS * 0.5f;
        _dragIcon.AddChild(icon);
        source.SetDragPreview(_dragIcon);
        _dragged = info;
        DragStarted?.Invoke();
        return (int)info.Kind;
    }

    private bool CanDropPart(Vector2 position, Variant data)
    {
        if (Variant.Type.Int != data.VariantType || _session.IsChoosingType)
        {
            return false;
        }
        var kind = (CreaturePartKind)data.AsInt32();
        if (_stage.RayToBody(position) is Vector3 hit)
        {
            PreviewPart(kind, hit);
        }
        else
        {
            HideGhost();
        }
        return true;
    }

    private void DropPart(Vector2 position, Variant data)
    {
        var      kind = (CreaturePartKind)data.AsInt32();
        Vector3? hit  = _stage.RayToBody(position) ?? (null != _ghostKind ? _ghostHit : null);
        HideGhost();
        Dropped?.Invoke(kind, position, hit);
    }

    /// <summary>Shows the ghost of a part where a drop at a point of the clay would place it.</summary>
    /// <param name="kind">(Required) The dragged part.</param>
    /// <param name="hit">(Required) Point on the clay, unit space.</param>
    /// <returns>True when the part fits there and its ghost shows.</returns>
    public bool PreviewPart(CreaturePartKind kind, Vector3 hit)
    {
        CreatureDesign draft = _session.Draft;
        int            side  = _session.SideAt(hit);
        if (!draft.TrySnap(kind, draft.TAt(hit.X), out float snapped, side))
        {
            HideGhost();
            return false;
        }

        // Ghost: the body without its parts, drawn parts only, with just this part
        var design = new CreatureDesign();
        design.CopyFrom(draft, isPartsKept: false);
        design.TryPlace(kind, snapped, 0, side);
        _ghost.Design    = design;
        _ghost.BodyColor = new Color(_session.DraftColor, GHOST_ALPHA);
        _ghostKind       = kind;
        _ghostHit        = hit;
        _stage.ShowLanding(hit);
        if (IsInstanceValid(_dragIcon))
        {
            _dragIcon.Visible = false;
        }
        return true;
    }

    /// <summary>Removes the ghost, the cursor icon shows again.</summary>
    public void HideGhost()
    {
        if (IsInstanceValid(_dragIcon))
        {
            _dragIcon.Visible = true;
        }
        if (null == _ghostKind)
        {
            return;
        }
        _ghostKind    = null;
        _ghost.Design = null;                        // a visual without design hides itself
        _stage.ShowLanding(null);
    }

    /// <summary>Hides the ghost once the dragged part leaves the clay, called every frame.</summary>
    public void HideOutside()
    {
        if (null != _ghostKind && !_stage.GetGlobalRect().HasPoint(_stage.GetGlobalMousePosition()))
        {
            HideGhost();
        }
    }

    /// <summary>Ends the drag: the dragged part, its icon and the ghost go.</summary>
    public void EndDrag()
    {
        _dragged  = null;
        _dragIcon = null;
        HideGhost();
    }
}
