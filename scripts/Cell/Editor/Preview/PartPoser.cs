// NOTE:
//   Poses the picked part of the draft with the gizmo (PartGizmo): arrows shift it,
//   rings turn it, free of charge and looks only. It owns the gizmo node, keeps it on
//   the picked part and turns a gizmo drag into a new pose in the unit space of the
//   cell. Lives in the world of the CellPreview, CellEditor routes the mouse input here
//   first (Handle).
//
//   var poser = new PartPoser();
//   poser.Setup(session, preview);
//   preview.AddToWorld(poser);
//   if (poser.Handle(@event)) return;

//
//   Chapters:
//     SETUP        Session, preview and the gizmo node.
//     POSE PART    Shifting and turning the picked part.
//     DRAG GIZMO   Mouse input on the gizmo and its place on the picked part.
//

using Godot;

namespace CellOut;

public partial class PartPoser : Node3D
{
    // ===========================================================================
    // ================================   SETUP   ================================
    // ===========================================================================
    //
    //   Setup    Hands in the session and the preview the gizmo lives in.
    //   _Ready   Builds the gizmo on the camera of the preview.
    //

    private CellEditSession _session;
    private CellPreview _preview;
    private PartGizmo _gizmo;

    /// <summary>Hands in what the poser works on, call before it enters the tree.</summary>
    /// <param name="session">(Required) The session with the draft and the picked part.</param>
    /// <param name="preview">(Required) The preview, already built: its camera drives the gizmo.</param>
    public void Setup(CellEditSession session, CellPreview preview)
    {
        _session = session;
        _preview = preview;
    }

    public override void _Ready()
    {
        _gizmo = new PartGizmo { Camera = _preview.Camera };
        AddChild(_gizmo);
    }

    // ===========================================================================
    // ==============================   POSE PART   ==============================
    // ===========================================================================
    //
    //   Move   Shifts the picked part (gizmo arrows).
    //   Turn   Turns the picked part (gizmo rings).
    //

    /// <summary>Shifts the picked part, clamped to CellDesign.MAX_PART_OFFSET.</summary>
    /// <param name="delta">(Required) Shift in the preview world, turned and scaled into the unit space of the cell.</param>
    /// <returns>False when no part is picked.</returns>
    public bool Move(Vector3 delta)
    {
        int picked = _session.PickedPart;
        if (picked < 0)
        {
            return false;
        }
        PlacedPart part = _session.Draft.Parts[picked];
        _session.Draft.SetPartPose(picked, part.Offset + _preview.CellBasis().Inverse() * delta, part.Rotation);
        return true;
    }

    /// <summary>Turns the picked part around a world axis of the preview.</summary>
    /// <param name="axis">(Required) Normalized world axis.</param>
    /// <param name="angle">(Required) Radians, right hand rule.</param>
    /// <returns>False when no part is picked.</returns>
    public bool Turn(Vector3 axis, float angle)
    {
        int picked = _session.PickedPart;
        if (picked < 0)
        {
            return false;
        }
        PlacedPart part      = _session.Draft.Parts[picked];
        Vector3    localAxis = (_preview.CellBasis().Orthonormalized().Inverse() * axis).Normalized();
        Basis      turned    = new Basis(localAxis, angle) * Basis.FromEuler(part.Rotation);
        _session.Draft.SetPartPose(picked, part.Offset, turned.Orthonormalized().GetEuler());
        return true;
    }

    // ===========================================================================
    // =============================   DRAG GIZMO   ==============================
    // ===========================================================================
    //
    //   Reset         Forgets a running gizmo drag (the editor opens).
    //   Handle        Left drag on the gizmo shifts or turns the picked part.
    //   UpdateGizmo   Puts the gizmo on the picked part, hidden without one.
    //

    private GizmoHandle _gizmoDrag = GizmoHandle.None;

    public void Reset()
    {
        _gizmoDrag = GizmoHandle.None;
    }

    /// <summary>Offers a mouse event of the preview to the gizmo.</summary>
    /// <param name="event">(Required) The input event, positions in pixels of the preview.</param>
    /// <returns>True when the gizmo took the event (grab, drag or release of a handle).</returns>
    public bool Handle(InputEvent @event)
    {
        // The gizmo lights up under the cursor while nothing is dragged
        if (@event is InputEventMouseMotion moved && GizmoHandle.None == _gizmoDrag)
        {
            _gizmo.Hovered = _gizmo.HandleAt(moved.Position);
        }

        switch (@event)
        {
            // Left press on the gizmo: shift or turn the picked part
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } grab when GizmoHandle.None != _gizmo.HandleAt(grab.Position):
                _gizmoDrag    = _gizmo.HandleAt(grab.Position);
                _gizmo.Active = _gizmoDrag;
                return true;

            // Left release after a gizmo drag
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } when GizmoHandle.None != _gizmoDrag:
                _gizmoDrag    = GizmoHandle.None;
                _gizmo.Active = GizmoHandle.None;
                return true;

            // Gizmo drag: arrows shift along their axis, rings turn around it
            case InputEventMouseMotion pose when GizmoHandle.None != _gizmoDrag:
                if (PartGizmo.IsTurn(_gizmoDrag))
                {
                    Turn(PartGizmo.AxisOf(_gizmoDrag), _gizmo.TurnDelta(_gizmoDrag, pose.Position - pose.Relative, pose.Position));
                }
                else
                {
                    Move(_gizmo.MoveDelta(_gizmoDrag, pose.Relative));
                }
                return true;
        }
        return false;
    }

    /// <summary>Puts the gizmo on the anchor of the picked part, hides it without one.</summary>
    public void UpdateGizmo()
    {
        Vector3 anchor   = Vector3.Zero;
        int     picked   = _session.PickedPart;
        // A palette part in hand hides the gizmo, so it never catches a click meant for placing
        bool    isPicked = picked >= 0 && picked < _session.Draft.Parts.Count && _preview.IsShown && null == _session.Selected;
        _gizmo.Visible = isPicked && _preview.TryGetPartAnchor(picked, out anchor);
        if (_gizmo.Visible)
        {
            _gizmo.GlobalPosition = anchor;
        }
    }
}
