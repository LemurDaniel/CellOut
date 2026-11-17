// NOTE:
//   Placing a building by hand: after a build tile of the HUD is pressed, the building of
//   that function stands as a see-through preview on a plot of the settlement (wishes of the
//   user 2026-11-17: fixed plots around the fire instead of free placing, Settlement.cs, and
//   a build view): the camera rises over the settlement (Society sets FollowCamera.Overview
//   while IsPlacing), the chieftain stands still, and the preview jumps to the free plot
//   closest to the mouse, turned to the fire. R turns it by an eighth (Shift + R back).
//   Nearly solid when the stock pays for it, faded when not. A left click or Space builds
//   it there, Escape lets go. It does not process on its own: Society calls Update per
//   physics frame and routes the input to HandleInput first.
//
//   var placer = new BuildingPlacer();
//   placer.Setup(session, tribe);
//   AddChild(placer);
//   placer.Begin(BuildingKind.Market);       // a build tile was pressed
//   if (placer.HandleInput(@event)) GetViewport().SetInputAsHandled();
//   placer.Update();

//
//   Chapters:
//     PLACE   Begin, the preview on the plot under the mouse, turning, building and letting go.
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
    //   IsPlacing     A building stands as a preview in front of the chieftain.
    //   Pending       Its function, null when none.
    //   IsAllowed     Whether a plot is free and the stock pays for it.
    //   Plot          The free plot of the settlement it stands on: the one closest to the mouse.
    //   Turn / Rotate How it is turned on top of the turn of its plot, in steps of TURN_STEP.
    //   Aim           The mouse on the plane.
    //   Setup         The session and the tribe, before AddChild.
    //   Begin         Takes a function to place, the same one again lets go.
    //   Cancel        Lets go.
    //   PlaceAt       Builds the pending building at a spot.
    //   HandleInput   R turns, a left click or Space builds, Escape lets go.
    //   Update        The preview jumps to the free plot under the mouse and shows whether the stock pays for it.
    //

    private const float ALLOWED_FADE = 0.2f;         // transparency of the preview where it may stand
    private const float REFUSED_FADE = 0.75f;        // and where it may not

    private WorldSession _session;
    private TribeCommander _tribe;
    private BuildingVisual3D _preview;
    private bool? _shownAllowed;

    public BuildingKind? Pending { get; private set; }

    public bool IsPlacing => null != Pending;

    public bool IsAllowed { get; private set; }

    private const float TURN_STEP = Mathf.Tau / 8f;  // one press of R

    /// <summary>The free plot of the settlement the preview stands on: the one closest to the mouse, null when every plot is taken.</summary>
    public SettlementSlot? Plot => IsPlacing ? _tribe.FreeSlotNear(Aim()) : null;

    /// <summary>Turn of the building on top of the turn of its plot (front to the fire), kept for the next one.</summary>
    public float Turn { get; private set; }

    /// <summary>Turns the building around the vertical axis.</summary>
    /// <param name="steps">(Required) Steps of an eighth turn, negative turns back.</param>
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

    /// <summary>Stands the building of a function in front of the chieftain, the function already shown lets go.</summary>
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
        _session.Player.Autopilot = Vector2.Zero;       // the chieftain stands still in the build view, a click picks a plot
        _preview.Build(_tribe.DesignOf(kind));
        Log.Verbose("BuildingPlacer", $"Placing a {kind}.");
    }

    public void Cancel()
    {
        if (IsPlacing)
        {
            _session.Player.Autopilot = null;
        }
        Pending          = null;
        _preview.Visible = false;
    }

    /// <summary>Builds the pending building on the plot at a spot and lets go, the stock pays.</summary>
    /// <param name="spot">(Required) Plane point of a free plot of the settlement.</param>
    /// <returns>False when nothing is pending, the spot is no free plot or the stock is short (it keeps hanging then).</returns>
    public bool PlaceAt(Vector2 spot)
    {
        if (Pending is not BuildingKind kind || _tribe.FreeSlotNear(spot) is not SettlementSlot slot || !_tribe.TryBuildAt(kind, spot, slot.Turn + Turn))
        {
            return false;
        }
        Cancel();
        return true;
    }

    /// <summary>Takes R, Space, the left click and Escape while a building is shown.</summary>
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
        bool isBuild = @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space or Key.Enter }
            || @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left };
        if (isBuild)
        {
            if (Plot is SettlementSlot plot)
            {
                PlaceAt(plot.Spot);
            }
            return true;
        }
        return false;
    }

    /// <summary>Where the player points in the build view: the mouse on the foundation of the settlement (its height, not the plane below it), the free plot closest to it takes the building.</summary>
    private Vector2 Aim()
    {
        Camera3D camera = GetViewport().GetCamera3D();
        if (null == camera)
        {
            return _tribe.Fire;
        }
        Vector2 mouse     = GetViewport().GetMousePosition();
        Vector3 origin    = camera.ProjectRayOrigin(mouse);
        Vector3 direction = camera.ProjectRayNormal(mouse);
        float   ground    = _session.Coast.GroundHeight(_tribe.Fire) * _session.Player.BodySize();
        if (direction.Y > -0.0001f)
        {
            return _tribe.Fire;     // the mouse points at the sky
        }
        Vector3 hit = origin + direction * ((ground - origin.Y) / direction.Y);
        return new Vector2(hit.X, hit.Z);
    }

    /// <summary>Moves the preview with the chieftain, once per physics frame.</summary>
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
        // The preview stands on the free plot closest to where the chieftain points, turned to the fire; hidden when every plot is taken
        Coast coast = _session.Coast;
        if (Plot is not SettlementSlot plot)
        {
            _preview.Visible = false;
            IsAllowed        = false;
            return;
        }
        Vector2 spot   = plot.Spot;
        float   ground = coast.GroundHeight(spot) * _session.Player.BodySize();
        _preview.Visible        = true;
        _preview.GlobalPosition = Plane3D.To3D(spot, ground - Plane3D.Drop(spot));
        _preview.Rotation       = new Vector3(0f, plot.Turn + Turn, 0f);
        _preview.Scale          = Vector3.One * coast.Unit * Hut.SCALE;

        // Nearly solid when the stock pays for it, faded when not; the meshes are only touched when that changes
        IsAllowed = _tribe.Economy.CanAfford(_tribe.DesignOf(Pending.Value));
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
