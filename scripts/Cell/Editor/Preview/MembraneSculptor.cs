// NOTE:
//   Reshapes the membrane of the draft with empty hands: dragging the edge stretches or
//   squashes the outline right there (free form, no mirroring), dragging one of the
//   glowing height handles above the cell raises or flattens it. Free of charge. The
//   math is MembraneGeometry, this node keeps the drag state and the handle meshes.
//   Lives in the world of the CellPreview, CellEditor routes the mouse input here.
//
//   var sculptor = new MembraneSculptor();
//   sculptor.Setup(session, preview);
//   preview.AddToWorld(sculptor);
//   if (sculptor.HandleHeight(@event)) return;

//
//   Chapters:
//     SETUP            Session and preview.
//     STRETCH EDGE     Dragging the membrane edge.
//     RAISE HEIGHTS    The height handles: drag, hover and their meshes.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class MembraneSculptor : Node3D
{
    // ===========================================================================
    // ================================   SETUP   ================================
    // ===========================================================================
    //
    //   Setup   Hands in the session and the preview the handles live in.
    //

    private CellEditSession _session;
    private CellPreview _preview;

    /// <summary>Hands in what the sculptor works on, call before it enters the tree.</summary>
    /// <param name="session">(Required) The session whose draft is reshaped.</param>
    /// <param name="preview">(Required) The preview whose camera and membrane the cursor hits.</param>
    public void Setup(CellEditSession session, CellPreview preview)
    {
        _session = session;
        _preview = preview;
    }

    // ===========================================================================
    // ============================   STRETCH EDGE   =============================
    // ===========================================================================
    //
    //   IsShaping          Whether an edge drag runs.
    //   TryBeginShaping    Starts an edge drag when the press lies on the free membrane edge.
    //   ShapeTowards       Moves the grabbed edge to the cursor, neighbors follow softly.
    //   EndShaping         Ends the edge drag.
    //

    private bool _isShaping;
    private float _grabAngle;
    private float _grabRadius;
    private readonly float[] _shapeBefore = new float[CellDesign.SHAPE_POINTS];

    /// <summary>Whether the held press stretches the edge instead of rotating the cell.</summary>
    public bool IsShaping => _isShaping;

    /// <summary>
    /// Pressed on the membrane edge without a part in hand and not on a placed part:
    /// stretch instead of rotate. Remembers the grabbed angle and the outline before the drag.
    /// </summary>
    /// <param name="pixel">(Required) Where the button went down, in pixels of the preview.</param>
    /// <returns>True when an edge drag started.</returns>
    public bool TryBeginShaping(Vector2 pixel)
    {
        CellDesign draft   = _session.Draft;
        Vector2    pressed = _preview.ToUnit(pixel);
        _isShaping = null == _session.Selected && MembraneGeometry.IsOnEdge(draft, pressed) && _preview.PartAtPixel(pixel) < 0;
        if (!_isShaping)
        {
            return false;
        }
        _grabAngle  = pressed.Angle();
        _grabRadius = draft.ShapeRadius(_grabAngle);
        for (int i = 0; i < CellDesign.SHAPE_POINTS; i++)
        {
            _shapeBefore[i] = draft.Shape[i];
        }
        return true;
    }

    /// <summary>Stretches or squashes the grabbed edge towards the cursor, see MembraneGeometry.StretchedShape.</summary>
    /// <param name="pixel">(Required) The mouse position in pixels of the preview.</param>
    public void ShapeTowards(Vector2 pixel)
    {
        _session.Draft.SetShape(MembraneGeometry.StretchedShape(_shapeBefore, _grabAngle, _grabRadius, _preview.ToUnit(pixel)));
    }

    public void EndShaping()
    {
        _isShaping = false;
    }

    // ===========================================================================
    // ============================   RAISE HEIGHTS   ============================
    // ===========================================================================
    //
    //   RaiseHeight      Raises or flattens the membrane at one height handle, neighbors follow softly.
    //   HandleHeight     Left drag on a height handle raises or flattens the membrane there.
    //   HoverAt          Remembers the handle under the cursor and says which cursor offers what.
    //   HeightHandleAt   The height handle under a pixel, -1 for none.
    //   UpdateHandles    Shows the handles with empty hands, the active one bigger.
    //

    private const float HEIGHT_PER_PIXEL = 0.006f;   // height factor per pixel of vertical mouse drag
    private const float HANDLE_PICK_PIXELS = 16f;
    private int _heightDrag = -1;
    private int _heightHover = -1;
    private float _handleTime;
    private readonly List<MeshInstance3D> _heightHandles = new();
    private readonly StandardMaterial3D _handleMaterial = new()
    {
        AlbedoColor              = new Color(1f, 0.95f, 0.8f),
        EmissionEnabled          = true,
        Emission                 = new Color(1f, 0.85f, 0.55f),
        EmissionEnergyMultiplier = 1.2f,
    };

    /// <summary>Raises (or with a negative change flattens) the membrane at one height handle, free of charge.</summary>
    /// <param name="index">(Required) Handle, 0 forward, counting like CellDesign.Heights.</param>
    /// <param name="change">(Required) Change of the height factor at the handle, neighbors get EdgeWeight of it.</param>
    public void RaiseHeight(int index, float change)
    {
        _session.Draft.SetHeights(MembraneGeometry.RaisedHeights(_session.Draft, index, change));
    }

    /// <summary>Offers a mouse event of the preview to the height handles.</summary>
    /// <param name="event">(Required) The input event, positions in pixels of the preview.</param>
    /// <returns>True when a handle took the event (grab, drag or release).</returns>
    public bool HandleHeight(InputEvent @event)
    {
        switch (@event)
        {
            // Left press on a height handle with empty hands: raise or flatten the membrane there
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } raise when null == _session.Selected && HeightHandleAt(raise.Position) >= 0:
                _heightDrag = HeightHandleAt(raise.Position);
                return true;

            // Left release after a height drag
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } when _heightDrag >= 0:
                _preview.Celebrate(MembraneGeometry.HeightHandlePosition(_session.Draft, _heightDrag), EditorStyle.DIET_COLORS[DietClass.Omnivore], 1.03f);
                _heightDrag = -1;
                return true;

            // Height drag: up raises, down flattens
            case InputEventMouseMotion lifting when _heightDrag >= 0:
                RaiseHeight(_heightDrag, -lifting.Relative.Y * HEIGHT_PER_PIXEL);
                return true;
        }
        return false;
    }

    /// <summary>Hover with empty hands: remembers the height handle under the cursor.</summary>
    /// <param name="pixel">(Required) The mouse position in pixels of the preview.</param>
    /// <returns>The cursor that offers to raise (handle), to stretch (edge) or nothing.</returns>
    public Control.CursorShape HoverAt(Vector2 pixel)
    {
        _heightHover = HeightHandleAt(pixel);
        return _heightHover >= 0
            ? Control.CursorShape.Vsize
            : MembraneGeometry.IsOnEdge(_session.Draft, _preview.ToUnit(pixel)) ? Control.CursorShape.Drag : Control.CursorShape.Arrow;
    }

    private int HeightHandleAt(Vector2 pixel)
    {
        if (null != _session.Selected || !_preview.IsShown)
        {
            return -1;
        }
        Camera3D camera   = _preview.Camera;
        int      closest  = -1;
        float    bestDist = HANDLE_PICK_PIXELS;
        for (int i = 0; i < CellDesign.SHAPE_POINTS; i++)
        {
            Vector3 position = MembraneGeometry.HeightHandlePosition(_session.Draft, i);
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

    /// <summary>Per frame: places and pulses the handles, hidden with a part in hand or a ghost on the cell.</summary>
    /// <param name="delta">(Required) Seconds since the last frame.</param>
    /// <param name="hasGhost">(Required) Whether the ghost of a part shows on the cell.</param>
    public void UpdateHandles(float delta, bool hasGhost)
    {
        _handleTime += delta;
        while (_heightHandles.Count < CellDesign.SHAPE_POINTS)
        {
            MeshInstance3D handle = CellVisual3D.AddMesh(this, CellVisual3D.SphereMesh(true), _handleMaterial, Vector3.Zero, Vector3.One * 0.06f);
            handle.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _heightHandles.Add(handle);
        }
        bool isShown = null == _session.Selected && !hasGhost && _preview.IsShown;
        for (int i = 0; i < _heightHandles.Count; i++)
        {
            _heightHandles[i].Visible = isShown;
            if (!isShown)
            {
                continue;
            }
            bool  isActive = i == _heightDrag || i == _heightHover;
            float size     = isActive ? 0.09f : 0.06f + 0.008f * Mathf.Sin(_handleTime * 3f + i);
            _heightHandles[i].Position = MembraneGeometry.HeightHandlePosition(_session.Draft, i);
            _heightHandles[i].Scale    = Vector3.One * size;
        }
    }
}
