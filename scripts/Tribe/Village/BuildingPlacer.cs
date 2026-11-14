// NOTE:
//   Placing a building by hand: after a build tile of the HUD is pressed, the building of
//   that function hangs at the mouse as a see-through preview on the ground. Where it may
//   stand (TribeCommander.CanBuildAt: on land, within the build radius of the fire, clear
//   of the fire and of other buildings) it is nearly solid, elsewhere it fades. R turns
//   it a step (Shift + R back), a left click builds it there as it stands, a right click
//   or Escape lets go. It does not process on its
//   own: Main calls Update per physics frame and routes the input to HandleInput first.
//
//   var placer = new BuildingPlacer();
//   placer.Setup(session, tribe);
//   AddChild(placer);
//   placer.Begin(BuildingKind.Market);       // a build tile was pressed
//   if (placer.HandleInput(@event)) GetViewport().SetInputAsHandled();
//   placer.Update();

//
//   Chapters:
//     PLACE   Begin, the preview at the mouse, building and letting go.
//

using System.Linq;
using Godot;

namespace CellOut;

public partial class BuildingPlacer : Node3D
{
    // ===========================================================================
    // ================================   PLACE   ================================
    // ===========================================================================
    //
    //   IsPlacing     A building hangs at the mouse.
    //   Pending       Its function, null when none.
    //   IsAllowed     Whether the spot under the mouse takes it.
    //   Turn          How the building is turned, kept for the next one.
    //   Rotate        Turns it by steps of TURN_STEP.
    //   Setup         The session and the tribe, before AddChild.
    //   Begin         Takes a function to place, the same one again lets go.
    //   Cancel        Lets go.
    //   PlaceAt       Builds the pending building at a spot.
    //   HandleInput   R turns, left click builds, right click and Escape let go.
    //   Update        The preview follows the mouse on the ground and shows whether the spot is allowed.
    //

    private const float ALLOWED_FADE = 0.2f;         // transparency of the preview where it may stand
    private const float REFUSED_FADE = 0.75f;        // and where it may not
    private const float TURN_STEP = Mathf.Tau / 16f; // one press of R

    private WorldSession _session;
    private TribeCommander _tribe;
    private BuildingVisual3D _preview;
    private bool? _shownAllowed;

    public BuildingKind? Pending { get; private set; }

    public bool IsPlacing => null != Pending;

    public bool IsAllowed { get; private set; }

    public float Turn { get; private set; }

    /// <summary>Turns the building around the vertical axis.</summary>
    /// <param name="steps">(Required) Steps of a sixteenth turn, negative turns back.</param>
    public void Rotate(int steps)
    {
        Turn = Mathf.Wrap(Turn + steps * TURN_STEP, 0f, Mathf.Tau);
    }

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player and coast.</param>
    /// <param name="tribe">(Required) The tribe that checks the spot and builds.</param>
    public void Setup(WorldSession session, TribeCommander tribe)
    {
        _session = session;
        _tribe   = tribe;
    }

    public override void _Ready()
    {
        TopLevel = true;
        _preview = new BuildingVisual3D { Visible = false };
        AddChild(_preview);
    }

    /// <summary>Hangs the building of a function at the mouse, the function that already hangs there lets go.</summary>
    /// <param name="kind">(Required) The function, built from the blueprint of the tribe.</param>
    public void Begin(BuildingKind kind)
    {
        if (kind == Pending || !_tribe.IsActive)
        {
            Cancel();
            return;
        }
        Pending       = kind;
        _shownAllowed = null;
        _preview.Build(_tribe.DesignOf(kind));
        Log.Verbose("BuildingPlacer", $"Placing a {kind}.");
    }

    public void Cancel()
    {
        Pending          = null;
        _preview.Visible = false;
    }

    /// <summary>Builds the pending building at a spot and lets go, the stock pays.</summary>
    /// <param name="spot">(Required) Plane point of its center.</param>
    /// <returns>False when nothing is pending, the spot is not allowed or the stock is short (it keeps hanging then).</returns>
    public bool PlaceAt(Vector2 spot)
    {
        if (Pending is not BuildingKind kind || !_tribe.TryBuildAt(kind, spot, Turn))
        {
            return false;
        }
        Cancel();
        return true;
    }

    /// <summary>Takes clicks and Escape while a building hangs at the mouse.</summary>
    /// <param name="event">(Required) The event Main received in _UnhandledInput.</param>
    /// <returns>True when the placer used the event.</returns>
    public bool HandleInput(InputEvent @event)
    {
        if (!IsPlacing)
        {
            return false;
        }
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Cancel();
            return true;
        }
        if (@event is InputEventKey { Pressed: true, Keycode: Key.R } turn)     // held down it keeps turning
        {
            Rotate(turn.ShiftPressed ? -1 : 1);
            return true;
        }
        if (@event is not InputEventMouseButton button)
        {
            return false;
        }
        if (MouseButton.Right == button.ButtonIndex && button.Pressed)
        {
            Cancel();
            return true;
        }
        if (MouseButton.Left == button.ButtonIndex)
        {
            // The press builds, its release is swallowed too: it must not select units
            if (button.Pressed)
            {
                PlaceAt(Plane3D.MouseOnPlane(GetViewport(), _session.Player.GlobalPosition));
            }
            return true;
        }
        return false;
    }

    /// <summary>Moves the preview to the mouse on the ground, once per physics frame.</summary>
    public void Update()
    {
        if (!IsPlacing)
        {
            return;
        }
        if (!_tribe.IsActive)
        {
            Cancel();
            return;
        }
        Coast   coast = _session.Coast;
        Vector2 spot  = Plane3D.MouseOnPlane(GetViewport(), _session.Player.GlobalPosition);
        float   ground = coast.GroundHeight(spot) * _session.Player.BodySize();
        _preview.Visible        = true;
        _preview.GlobalPosition = Plane3D.To3D(spot, ground - Plane3D.Drop(spot));
        _preview.Rotation       = new Vector3(0f, Turn, 0f);
        _preview.Scale          = Vector3.One * coast.Unit * Hut.SCALE;

        // Nearly solid where it may stand, faded where not; the meshes are only touched when that changes
        IsAllowed = _tribe.CanBuildAt(spot) && _tribe.Economy.CanAfford(_tribe.DesignOf(Pending.Value));
        if (IsAllowed == _shownAllowed)
        {
            return;
        }
        _shownAllowed = IsAllowed;
        foreach (MeshInstance3D mesh in _preview.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            mesh.Transparency = IsAllowed ? ALLOWED_FADE : REFUSED_FADE;
            mesh.CastShadow   = GeometryInstance3D.ShadowCastingSetting.Off;
        }
    }
}
