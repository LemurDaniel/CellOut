// NOTE:
//   The handles the clay is sculpted with: a glowing handle above every vertebra, the
//   joint handles of the picked limb and the gizmo (Cell/Editor/PartGizmo.cs) on the
//   picked vertebra or part. It knows which handle lies under a pixel and applies a drag
//   to the draft of the session. It reads no input on its own: the CreatureEditor asks
//   it in a fixed order (ScrollGirth, Grab, Drag, Release, Hover) and calls Update every
//   frame. Stands next to the clay (CreatureStage.AddToScene), in the same unit space.
//
//   var handles = new CreatureSculptHandles();
//   handles.Setup(session, stage);
//   stage.AddToScene(handles);
//   bool isTaken = handles.Grab(mousePosition);

//
//   Chapters:
//     BUILD HANDLES   Gizmo, materials and where a vertebra handle floats.
//     DRAG HANDLES    Wheel, press, drag, release and hover on gizmo, joints and vertebrae.
//     SHOW HANDLES    Moves vertebra handles, joint handles and the gizmo every frame.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class CreatureSculptHandles : Node3D
{
    // ===========================================================================
    // ============================   BUILD HANDLES   ============================
    // ===========================================================================
    //
    //   Setup            Takes session and stage, builds the gizmo.
    //   HandlePosition   Where the handle of a vertebra floats above the back.
    //   TryGetLimbFrame  Frame of the picked limb on the flank of the gizmo.
    //

    private const float HANDLE_LIFT = 0.16f;          // above the back

    private CreatureEditorSession _session;
    private CreatureStage _stage;
    private PartGizmo _gizmo;
    private readonly List<MeshInstance3D> _handles = new();
    private readonly List<MeshInstance3D> _jointHandles = new();
    private readonly StandardMaterial3D _jointMaterial = new()
    {
        AlbedoColor              = new Color(0.75f, 1f, 0.95f),
        EmissionEnabled          = true,
        Emission                 = new Color(0.35f, 0.95f, 0.85f),
        EmissionEnergyMultiplier = 1.3f,
        NoDepthTest              = true,             // joints inside the clay stay grabbable
        RenderPriority           = 5,
    };

    /// <summary>Call once before the handles enter the scene of the stage.</summary>
    /// <param name="session">(Required) The sitting: draft, picked part and vertebra.</param>
    /// <param name="stage">(Required) The stage, for its camera and the clay.</param>
    public void Setup(CreatureEditorSession session, CreatureStage stage)
    {
        _session = session;
        _stage   = stage;

        // Gizmo on the picked part
        _gizmo = new PartGizmo { Camera = stage.Camera };
        AddChild(_gizmo);
    }

    /// <summary>Where the handle of a vertebra floats above the back, unit space.</summary>
    /// <param name="index">(Required) Vertebra, 0 is the tail end.</param>
    public Vector3 HandlePosition(int index)
    {
        CreatureDesign draft = _session.Draft;
        float x = draft.VertebraX(index);
        return new Vector3(x, draft.LiftAt(x) + draft.TopAt(x) + HANDLE_LIFT, 0f);
    }

    /// <summary>Frame of the picked limb on the flank of the gizmo, false when no limb is picked.</summary>
    private bool TryGetLimbFrame(out Transform3D frame)
    {
        frame = Transform3D.Identity;
        int  picked = _session.PickedPart;
        bool isLimb = !_session.IsChoosingType && picked >= 0 && picked < _session.Draft.Parts.Count && null != _session.Draft.Parts[picked].Joints;
        return isLimb && _stage.Clay.TryGetLimbFrame(picked, _session.GizmoFlank, out frame);
    }

    // ===========================================================================
    // ============================   DRAG HANDLES   =============================
    // ===========================================================================
    //
    //   IsDragging        A gizmo handle or a joint is being dragged.
    //   ScrollGirth       The wheel over a vertebra handle sets its girth.
    //   Grab              Left press: the gizmo, a joint or a vertebra handle takes it.
    //   Drag              The gizmo shifts, turns or scales, a joint sculpts the limb.
    //   Release           Left release: every drag ends.
    //   CancelGizmoDrag   A new sitting starts without a gizmo drag.
    //   Hover             Lights up the gizmo handle, joint or vertebra handle under the cursor.
    //   HandleAt          The vertebra handle under a pixel, -1 for none.
    //   JointAt           The joint handle of the picked limb under a pixel, -1 for none.
    //   DragJoint         Moves a joint of the picked limb in the plane facing the camera.
    //

    private const float GIRTH_WHEEL_STEP = 0.06f;      // per wheel notch over a vertebra handle
    private const float HANDLE_PICK_PIXELS = 16f;

    private int _hoverHandle = -1;
    private int _dragJoint = -1;
    private int _hoverJoint = -1;
    private GizmoHandle _gizmoDrag = GizmoHandle.None;

    public bool IsDragging => GizmoHandle.None != _gizmoDrag || _dragJoint >= 0;

    /// <summary>Wheel over a vertebra handle: sets its girth.</summary>
    /// <param name="pixel">(Required) Position in the stage.</param>
    /// <param name="notch">(Required) 1 wheel up (thicker), -1 wheel down (thinner).</param>
    /// <returns>False when no vertebra handle is under the cursor.</returns>
    public bool ScrollGirth(Vector2 pixel, float notch)
    {
        int girthAt = HandleAt(pixel);
        if (girthAt < 0)
        {
            return false;
        }
        _session.Draft.SetGirth(girthAt, _session.Draft.Girths[girthAt] + notch * GIRTH_WHEEL_STEP);
        return true;
    }

    /// <summary>
    /// Left press: the gizmo shifts or turns, a joint sculpts the limb, a vertebra handle
    /// picks its vertebra (and bends the spine through the gizmo it gets).
    /// </summary>
    /// <param name="pixel">(Required) Position in the stage.</param>
    /// <returns>True when a handle took the press, false for free space.</returns>
    public bool Grab(Vector2 pixel)
    {
        _gizmoDrag    = _gizmo.HandleAt(pixel);
        _gizmo.Active = _gizmoDrag;
        bool isGizmo  = GizmoHandle.None != _gizmoDrag;
        _dragJoint    = isGizmo ? -1 : JointAt(pixel);
        int handle    = isGizmo || _dragJoint >= 0 ? -1 : HandleAt(pixel);
        if (handle >= 0)
        {
            _session.PickVertebra(handle);
        }
        _session.ForgetPull();
        return isGizmo || _dragJoint >= 0 || handle >= 0;
    }

    /// <summary>Applies a mouse motion to what is being dragged (IsDragging).</summary>
    public void Drag(InputEventMouseMotion motion)
    {
        // Gizmo drag: arrows shift along their axis, rings turn around it
        // Gizmo on a vertebra: arrows push it, the golden ring scales it
        if (GizmoHandle.None != _gizmoDrag && _session.PickedVertebra >= 0)
        {
            if (GizmoHandle.Scale == _gizmoDrag)
            {
                _session.ScalePickedVertebra(_gizmo.ScaleFactor(motion.Position - motion.Relative, motion.Position));
            }
            else
            {
                _session.MovePickedVertebra(_gizmo.MoveDelta(_gizmoDrag, motion.Relative));
            }
            return;
        }
        if (GizmoHandle.None != _gizmoDrag)
        {
            if (PartGizmo.IsTurn(_gizmoDrag))
            {
                _session.TurnPickedPart(PartGizmo.AxisOf(_gizmoDrag), _gizmo.TurnDelta(_gizmoDrag, motion.Position - motion.Relative, motion.Position));
            }
            else
            {
                _session.MovePickedPart(_gizmo.MoveDelta(_gizmoDrag, motion.Relative));
            }
            return;
        }

        // Joint drag: the picked limb follows the cursor
        if (_dragJoint >= 0)
        {
            DragJoint(motion.Position);
        }
    }

    /// <summary>Left release: gizmo and joint let go.</summary>
    public void Release()
    {
        _dragJoint    = -1;
        _gizmoDrag    = GizmoHandle.None;
        _gizmo.Active = GizmoHandle.None;
    }

    /// <summary>Forgets a gizmo drag of the last sitting.</summary>
    public void CancelGizmoDrag()
    {
        _gizmoDrag = GizmoHandle.None;
    }

    /// <summary>Hover: gizmo and handles light up.</summary>
    /// <param name="pixel">(Required) Position in the stage.</param>
    /// <returns>True when the cursor is over a gizmo handle, a joint or a vertebra handle.</returns>
    public bool Hover(Vector2 pixel)
    {
        _gizmo.Hovered = _gizmo.HandleAt(pixel);
        _hoverJoint    = GizmoHandle.None == _gizmo.Hovered ? JointAt(pixel) : -1;
        _hoverHandle   = GizmoHandle.None == _gizmo.Hovered && _hoverJoint < 0 ? HandleAt(pixel) : -1;
        return _hoverHandle >= 0 || _hoverJoint >= 0 || GizmoHandle.None != _gizmo.Hovered;
    }

    private int HandleAt(Vector2 pixel)
    {
        Camera3D camera   = _stage.Camera;
        int      closest  = -1;
        float    bestDist = HANDLE_PICK_PIXELS;
        for (int i = 0; i < _session.Draft.Girths.Count; i++)
        {
            Vector3 position = HandlePosition(i);
            if (camera.IsPositionBehind(position))
            {
                continue;
            }
            float distance = camera.UnprojectPosition(position).DistanceTo(pixel);
            if (distance < bestDist)
            {
                bestDist = distance;
                closest  = i;
            }
        }
        return closest;
    }

    private int JointAt(Vector2 pixel)
    {
        if (!TryGetLimbFrame(out Transform3D frame))
        {
            return -1;
        }
        Camera3D               camera   = _stage.Camera;
        IReadOnlyList<Vector3> joints   = _session.Draft.Parts[_session.PickedPart].Joints;
        int                    closest  = -1;
        float                  bestDist = HANDLE_PICK_PIXELS;
        for (int i = 0; i < joints.Count; i++)
        {
            Vector3 position = frame * joints[i];
            if (camera.IsPositionBehind(position))
            {
                continue;
            }
            float distance = camera.UnprojectPosition(position).DistanceTo(pixel);
            if (distance < bestDist)
            {
                bestDist = distance;
                closest  = i;
            }
        }
        return closest;
    }

    /// <summary>
    /// The joint stays at its depth seen from the camera and follows the cursor in that
    /// plane; the world point goes back into limb space through the frame of the limb.
    /// </summary>
    private void DragJoint(Vector2 pixel)
    {
        if (!TryGetLimbFrame(out Transform3D frame))
        {
            return;
        }
        Camera3D camera = _stage.Camera;
        int      picked = _session.PickedPart;
        Vector3  world  = frame * _session.Draft.Parts[picked].Joints[_dragJoint];
        float    depth  = (world - camera.GlobalPosition).Dot(-camera.GlobalBasis.Z);
        Vector3  moved  = camera.ProjectPosition(pixel, depth);
        _session.Draft.SetLimbJoint(picked, _dragJoint, frame.AffineInverse() * moved);
    }

    // ===========================================================================
    // ============================   SHOW HANDLES   =============================
    // ===========================================================================
    //
    //   Update   Vertebra handles above the back, joint handles of the picked limb and
    //            the gizmo on the picked vertebra or part.
    //

    private bool _isGizmoOnVertebra;

    /// <summary>Moves every handle to its spot, called every frame while the editor shows.</summary>
    /// <param name="time">(Required) Clock of the stage, drives the breathing of the handles.</param>
    public void Update(float time)
    {
        CreatureDesign draft      = _session.Draft;
        bool           isChoosing = _session.IsChoosingType;

        // Handles: one per vertebra above the back, hidden while the type is open
        while (_handles.Count < draft.Girths.Count)
        {
            MeshInstance3D handle = CellVisual3D.AddMesh(this, CellVisual3D.SphereMesh(true), _stage.GlowMaterial, Vector3.Zero, Vector3.One * 0.06f);
            handle.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _handles.Add(handle);
        }
        for (int i = 0; i < _handles.Count; i++)
        {
            bool isShown = !isChoosing && i < draft.Girths.Count;
            _handles[i].Visible = isShown;
            if (!isShown)
            {
                continue;
            }
            bool  isActive = i == _session.PickedVertebra || i == _hoverHandle;
            bool  isEnd    = 0 == i || draft.Girths.Count - 1 == i;   // the ends can be pulled out, they stand out
            float size     = (isActive ? 0.09f : 0.06f + 0.008f * Mathf.Sin(time * 3f + i)) * (isEnd ? 1.3f : 1f);
            _handles[i].Position = HandlePosition(i);
            _handles[i].Scale    = Vector3.One * size;
        }

        // Joint handles of the picked limb, on the flank of the gizmo
        int  pickedPart = _session.PickedPart;
        bool hasLimb    = TryGetLimbFrame(out Transform3D limbFrame);
        int  jointCount = hasLimb ? draft.Parts[pickedPart].Joints.Count : 0;
        while (_jointHandles.Count < jointCount)
        {
            MeshInstance3D joint = CellVisual3D.AddMesh(this, CellVisual3D.SphereMesh(true), _jointMaterial, Vector3.Zero, Vector3.One * 0.06f);
            joint.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _jointHandles.Add(joint);
        }
        for (int i = 0; i < _jointHandles.Count; i++)
        {
            _jointHandles[i].Visible = i < jointCount;
            if (i < jointCount)
            {
                bool isActive = i == _dragJoint || i == _hoverJoint;
                _jointHandles[i].GlobalPosition = limbFrame * draft.Parts[pickedPart].Joints[i];
                _jointHandles[i].Scale          = Vector3.One * (isActive ? 0.085f : 0.06f + 0.006f * Mathf.Sin(time * 4f + i));
            }
        }

        // Gizmo on the picked vertebra (in the spine, push and scale) or on the anchor of the picked part
        int     pickedVertebra = _session.PickedVertebra;
        bool    isVertebra     = !isChoosing && pickedVertebra >= 0 && pickedVertebra < draft.Girths.Count;
        bool    isPicked       = !isChoosing && pickedPart >= 0 && pickedPart < draft.Parts.Count;
        Vector3 anchor         = Vector3.Zero;
        if (isVertebra)
        {
            float x = draft.Positions[pickedVertebra];
            anchor = _stage.Clay.ToGlobal(new Vector3(x, draft.LiftAt(x), 0f));
        }
        _gizmo.Visible = isVertebra || (isPicked && _stage.Clay.TryGetPartAnchor(pickedPart, _session.GizmoFlank, out anchor));
        if (_gizmo.Visible)
        {
            _gizmo.GlobalPosition = anchor;
            if (isVertebra != _isGizmoOnVertebra)
            {
                _isGizmoOnVertebra = isVertebra;
                if (isVertebra)
                {
                    _gizmo.ShowHandles(GizmoHandle.MoveX, GizmoHandle.MoveY, GizmoHandle.Scale);
                }
                else
                {
                    _gizmo.ShowHandles(PartGizmo.PART_HANDLES);
                }
            }
        }
    }
}
