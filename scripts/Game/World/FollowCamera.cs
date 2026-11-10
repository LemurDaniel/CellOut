// NOTE:
//   The chase camera of the game with everything that grows along with it: environment,
//   sun, sky and backdrop. Top view over the soup, third person over the islands, where
//   the world bends to a planet (Core/Plane3D.cs). It does not process on its own: Main
//   advances it once per frame and routes the input to it.
//
//   var followCamera = new FollowCamera();
//   followCamera.Setup(session);
//   AddChild(followCamera);            // builds environment, sun, camera and backdrop
//   followCamera.StartClose();         // once the player is in the tree
//   followCamera.Advance(dt);          // in _Process
//   if (followCamera.HandleInput(@event)) GetViewport().SetInputAsHandled();

//
//   Chapters:
//     BUILD SCENE      Environment, sun, camera and backdrop.
//     FOLLOW PLAYER    Wheel zoom, orbit, third person blend, planet curve and sky.
//     POSE FOR FLOWS   Camera poses for the title menu, the intro and after a cutscene.
//     VIEW RADIUS      Visible radius on the plane.
//

using Godot;

namespace CellOut;

public partial class FollowCamera : Node
{
    private const float CAM_FOV = 50f;
    private const float CAM_TILT = 0.5f;            // tilt from vertical in radians
    private const float CAM_DISTANCE = 14f;         // camera distance in player sizes
    private static readonly Color BACKGROUND_COLOR = new(0.04f, 0.13f, 0.17f);
    private static readonly Color SOUP_AMBIENT = new(0.5f, 0.8f, 0.85f);
    private static readonly Color SKY_AMBIENT = new(0.78f, 0.84f, 0.92f);
    private const float SOUP_AMBIENT_ENERGY = 0.6f;
    private const float SKY_AMBIENT_ENERGY = 0.8f;

    // ===========================================================================
    // =============================   BUILD SCENE   =============================
    // ===========================================================================
    //
    //   Setup    The session with player and coast, before AddChild.
    //   _Ready   Environment, sun, camera and backdrop.
    //   Camera   The world camera, cutscenes borrow it.
    //

    /// <summary>The world camera, cutscenes move it while the game is paused.</summary>
    public Camera3D Camera { get; private set; }

    private WorldSession _session;
    private DirectionalLight3D _sun;
    private Godot.Environment _environment;
    private Backdrop _backdrop;

    private Player Player => _session.Player;
    private Coast Coast => _session.Coast;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player and coast the camera follows.</param>
    public void Setup(WorldSession session)
    {
        _session = session;
    }

    public override void _Ready()
    {
        var environment = new Godot.Environment
        {
            BackgroundMode     = Godot.Environment.BGMode.Color,
            BackgroundColor    = BACKGROUND_COLOR,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor  = SOUP_AMBIENT,
            AmbientLightEnergy = SOUP_AMBIENT_ENERGY,
            GlowEnabled        = true,
            GlowIntensity      = 0.7f,
            GlowBloom          = 0.05f,
            SsaoEnabled        = true,                   // contact shadows where parts meet the body, radius in Advance
            SsaoIntensity      = 1.6f,
        };
        _environment = environment;
        AddChild(new WorldEnvironment { Environment = environment });

        _sun = new DirectionalLight3D
        {
            Basis         = Basis.LookingAt(new Vector3(0.35f, -1f, 0.45f), Vector3.Up),
            LightColor    = new Color(1f, 0.97f, 0.9f),
            LightEnergy   = 1.1f,
            ShadowEnabled = true,
            ShadowOpacity = 0.6f,
        };
        AddChild(_sun);

        Camera = new Camera3D { Fov = CAM_FOV };
        AddChild(Camera);
        Camera.MakeCurrent();

        _backdrop = new Backdrop();
        AddChild(_backdrop);
    }

    // ===========================================================================
    // ============================   FOLLOW PLAYER   ============================
    // ===========================================================================
    //
    //   StartClose     Snaps to the player, slightly zoomed in.
    //   HandleInput    Mouse wheel zooms, a right drag orbits the third person camera.
    //   ZoomBy         Changes the zoom factor by steps, clamped.
    //   ThirdPerson    0 top view .. 1 third person, the land animal blends over TP_BLEND_SECONDS.
    //   YawBehind      Camera yaw behind a facing.
    //   FollowFacing   Swings the third person camera behind the walking creature.
    //   Advance        Eases view, distance and focus, then moves camera, planet, sky and backdrop.
    //   UpdateSky      From the dark soup to sky, sunlight and haze over the open sea.
    //
    //   Top view: tilted CAM_TILT from vertical, screen up = -y on the plane.
    //   Third person: behind and above the land animal, swinging behind it while it walks,
    //   orbited by a right drag (yaw, tilt), the keys turn with it (Player.InputTurn), the
    //   world bends to a planet (Plane3D.SetCurve).
    //

    private const float ZOOM_STEP = 1.15f;
    public const float MIN_ZOOM = 0.5f;
    public const float MAX_ZOOM = 2.5f;
    private const float TP_BLEND_SECONDS = 2.2f;
    private const float TP_TILT = 1.2f;              // third person: tilt from vertical, low over the ground
    private const float TP_MIN_TILT = 0.6f;
    private const float TP_MAX_TILT = 1.45f;
    private const float TP_DISTANCE = 6f;            // in player sizes
    private const float TP_LOOK_UP = 0.9f;           // the camera looks this far above the body, in player sizes
    private const float TP_CLEARANCE = 0.35f;        // the camera stays this far above the ground, in player sizes
    private const float TP_FAR = 700f;               // draw distance in player sizes, the horizon is nearer
    private const float TP_VIEW_RADIUS = 28f;        // spawning and props around the player, in player sizes
    private const float ORBIT_SPEED = 0.006f;        // radians per pixel of a right drag
    private const float FOLLOW_SPEED = 2.2f;         // third person: how fast the camera swings behind the walking creature
    private const float FOLLOW_BACKWARD_LIMIT = -0.3f;   // facing this much towards the camera: no swing
    private const float TP_SEA_SINK = 0.95f;         // third person: swimmers sink this many sizes, just below the low sea (Coast.OPEN_SEA_HEIGHT)
    private const float TP_SHADOW_DISTANCE = 90f;   // third person: tall trees throw shadows this far, in player sizes
    private const float PLANET_RADIUS = 8000f;       // in Unit of the coast, the islands are ~260 Unit across

    /// <summary>Wheel zoom on top of the automatic distance, 1 = default, bigger = farther away.</summary>
    public float ZoomFactor { get; private set; } = 1f;

    /// <summary>0 top view .. 1 third person, eased with SmoothStep when used.</summary>
    public float ThirdPerson { get; private set; }

    /// <summary>SmoothStep of ThirdPerson, the blend everything uses.</summary>
    public float View => _view;

    private float _camDistance;
    private const float SSAO_RADIUS = 0.35f;         // contact shadow reach in body sizes
    private Vector3 _camFocus = Vector3.Zero;
    private float _camYaw;                           // third person: 0 = camera south of the player (+z)
    private float _camTilt = TP_TILT;
    private float _view;                             // SmoothStep of ThirdPerson, the blend everything uses
    private bool _isOrbiting;

    /// <summary>Snaps the camera to the player, slightly zoomed in. Call once the player is in the tree.</summary>
    public void StartClose()
    {
        _camDistance = Player.BodySize() * CAM_DISTANCE * 0.6f;   // start slightly zoomed in
        Advance(0f);
    }

    /// <summary>Takes the camera part of an input event: orbit and wheel zoom.</summary>
    /// <param name="event">(Required) The event Main received in _UnhandledInput.</param>
    /// <returns>True when the camera used the event, Main marks it as handled.</returns>
    /// <example><code>if (followCamera.HandleInput(@event)) GetViewport().SetInputAsHandled();</code></example>
    public bool HandleInput(InputEvent @event)
    {
        // Third person: a right drag orbits the camera, the cursor hides meanwhile
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right } orbit && (_view > 0.5f || !orbit.Pressed))
        {
            _isOrbiting     = orbit.Pressed;
            Input.MouseMode = _isOrbiting ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
            return true;
        }
        if (@event is InputEventMouseMotion motion && _isOrbiting)
        {
            _camYaw  = Mathf.Wrap(_camYaw - motion.Relative.X * ORBIT_SPEED, -Mathf.Pi, Mathf.Pi);
            _camTilt = Mathf.Clamp(_camTilt - motion.Relative.Y * ORBIT_SPEED, TP_MIN_TILT, TP_MAX_TILT);
            return true;
        }

        if (@event is not InputEventMouseButton button || !button.Pressed)
        {
            return false;
        }
        switch (button.ButtonIndex)
        {
            case MouseButton.WheelUp:
                ZoomBy(-1);
                break;
            case MouseButton.WheelDown:
                ZoomBy(1);
                break;
            default:
                return false;
        }
        return true;
    }

    /// <summary>Zooms out (positive steps) or in (negative steps), clamped to MIN_ZOOM..MAX_ZOOM.</summary>
    /// <param name="steps">(Required) Number of wheel steps, each one ZOOM_STEP.</param>
    /// <example><code>ZoomBy(-2)  // two steps closer</code></example>
    public void ZoomBy(int steps)
    {
        ZoomFactor = Mathf.Clamp(ZoomFactor * Mathf.Pow(ZOOM_STEP, steps), MIN_ZOOM, MAX_ZOOM);
        Log.Verbose("FollowCamera", $"Zoom factor {ZoomFactor:F2}.");
    }

    /// <summary>The camera yaw of a creature looking in a direction: the camera behind it.</summary>
    private static float YawBehind(float facing)
    {
        Vector2 behind = -Vector2.FromAngle(facing);
        return Mathf.Atan2(behind.X, behind.Y);
    }

    /*
      Third person, the camera swings behind the walking creature, faster the faster it walks:
      > Orbiting with the right mouse button: the hand of the player wins, no swing
      > Standing still: the camera stays where it is, the player may look around
      > Walking towards the camera (S): no swing, else it would whip around 180 degrees
      > Otherwise: the yaw eases towards behind the facing, A/D turn into a curve
    */
    /// <summary>Swings the third person camera behind the facing of the walking creature.</summary>
    private void FollowFacing(float dt)
    {
        if (_view < 0.5f || _isOrbiting || dt <= 0f)
        {
            return;
        }
        float speedShare = Mathf.Clamp(Player.Velocity.Length() / Mathf.Max(Player.CurrentSpeed(), 0.001f), 0f, 1f);
        if (speedShare < 0.1f)
        {
            return;
        }
        Vector2 cameraForward = -new Vector2(Mathf.Sin(_camYaw), Mathf.Cos(_camYaw));
        if (Vector2.FromAngle(Player.Facing).Dot(cameraForward) < FOLLOW_BACKWARD_LIMIT)
        {
            return;
        }
        _camYaw = Mathf.LerpAngle(_camYaw, YawBehind(Player.Facing), Mathf.Clamp(dt * FOLLOW_SPEED * speedShare, 0f, 1f));
    }

    /*
      1. View      Blends towards third person once the player is a land animal; the
                   blend starts with the camera yaw behind the creature, then swings
                   behind it while it walks (FollowFacing).
      2. Pose      Yaw and tilt from the top view pose to the orbit pose, distance and the
                   look-up point eased, the camera never dips into a hill.
      3. Planet    Curvature with the blend, flat around the camera.
      4. Scenery   Shadows, contact shadows, sky and backdrop grow along.
    */
    /// <summary>Chase camera, top view or third person, its distance relative to the player size.</summary>
    /// <param name="dt">(Required) Frame time, 0 snaps focus and view to the player.</param>
    /// <example><code>followCamera.Advance((float)delta);</code></example>
    public void Advance(float dt)
    {
        float playerSize = Player.BodySize();

        // View
        float target = Player.IsLandAnimal ? 1f : 0f;
        if (target > 0f && ThirdPerson <= 0f)
        {
            _camYaw  = YawBehind(Player.Facing);
            _camTilt = TP_TILT;
            Log.Verbose("FollowCamera", "Camera turns into the third person view.");
        }
        ThirdPerson = dt <= 0f ? target : Mathf.MoveToward(ThirdPerson, target, dt / TP_BLEND_SECONDS);
        _view       = Mathf.SmoothStep(0f, 1f, ThirdPerson);
        FollowFacing(dt);

        // Pose
        float   yaw   = Mathf.LerpAngle(0f, _camYaw, _view);
        float   tilt  = Mathf.Lerp(CAM_TILT, _camTilt, _view);
        float   sink  = Player.IsOnLand ? 0f : playerSize * TP_SEA_SINK;
        Vector3 focus = Plane3D.To3D(Player.GlobalPosition, (TP_LOOK_UP * playerSize + Player.GroundLift - sink) * _view);
        if (dt <= 0f)
        {
            _camFocus = focus;
        }
        float distance = playerSize * Mathf.Lerp(CAM_DISTANCE, TP_DISTANCE, _view) * ZoomFactor;
        _camDistance = Mathf.Lerp(_camDistance, distance, Mathf.Clamp(dt * 1.5f, 0f, 1f));
        _camFocus    = _camFocus.Lerp(focus, Mathf.Clamp(dt * 5f, 0f, 1f));

        Vector3 offset   = new Vector3(Mathf.Sin(yaw) * Mathf.Sin(tilt), Mathf.Cos(tilt), Mathf.Cos(yaw) * Mathf.Sin(tilt)) * _camDistance;
        Vector3 position = _camFocus + offset;
        if (null != Coast && _view > 0f)
        {
            var   ground = new Vector2(position.X, position.Z);
            float floor  = (Coast.GroundHeight(ground) + TP_CLEARANCE) * playerSize;
            position.Y   = Mathf.Max(position.Y, floor);
        }
        Camera.Position = position;
        Camera.LookAt(_camFocus, Vector3.Up);
        Camera.Near     = _camDistance * 0.05f;
        Camera.Far      = Mathf.Lerp(_camDistance * 8f, playerSize * TP_FAR, _view);
        Player.InputTurn = -yaw;

        // Planet
        float unit = null == Coast ? playerSize : Coast.Unit;
        Plane3D.SetCurve(_view / (2f * PLANET_RADIUS * unit), position);
        Plane3D.SeaDrop = _view * playerSize * TP_SEA_SINK;

        // Scenery: light, contact shadows and backdrop grow along, so shadows and motes fit at every size
        _sun.DirectionalShadowMaxDistance = Mathf.Lerp(_camDistance * 3f, playerSize * TP_SHADOW_DISTANCE, _view);
        _environment.SsaoRadius           = playerSize * SSAO_RADIUS;
        UpdateSky(playerSize);
        _backdrop.IsMotesShown = _view < 0.5f;
        _backdrop.Follow(new Vector2(_camFocus.X, _camFocus.Z), playerSize, ViewRadius());
    }

    private static readonly Color SKY_TOP = new(0.24f, 0.5f, 0.86f);
    private static readonly Color SKY_HORIZON = new(0.7f, 0.84f, 0.95f);
    private static readonly Color SEA_BELOW = new(0.07f, 0.3f, 0.42f);
    private const float FOG_DENSITY = 0.0025f;       // per player size, half the light at ~280 sizes
    private ProceduralSkyMaterial _sky;

    /// <summary>Background, ambient light and haze between the soup (view 0) and the open sky (view 1).</summary>
    private void UpdateSky(float playerSize)
    {
        if (_view <= 0f)
        {
            _environment.BackgroundMode     = Godot.Environment.BGMode.Color;
            _environment.FogEnabled         = false;
            _environment.AmbientLightColor  = SOUP_AMBIENT;
            _environment.AmbientLightEnergy = SOUP_AMBIENT_ENERGY;
            return;
        }

        if (null == _sky)
        {
            _sky = new ProceduralSkyMaterial { SunAngleMax = 20f };
            _environment.Sky = new Sky { SkyMaterial = _sky };
        }
        _sky.SkyTopColor                = BACKGROUND_COLOR.Lerp(SKY_TOP, _view);
        _sky.SkyHorizonColor            = BACKGROUND_COLOR.Lerp(SKY_HORIZON, _view);
        _sky.GroundHorizonColor         = BACKGROUND_COLOR.Lerp(SKY_HORIZON, _view);
        _sky.GroundBottomColor          = BACKGROUND_COLOR.Lerp(SEA_BELOW, _view);
        _environment.BackgroundMode     = Godot.Environment.BGMode.Sky;
        _environment.FogEnabled         = true;
        _environment.FogLightColor      = SKY_HORIZON;
        _environment.FogDensity         = FOG_DENSITY * _view / playerSize;
        _environment.FogSkyAffect       = 0f;
        _environment.AmbientLightColor  = SOUP_AMBIENT.Lerp(SKY_AMBIENT, _view);
        _environment.AmbientLightEnergy = Mathf.Lerp(SOUP_AMBIENT_ENERGY, SKY_AMBIENT_ENERGY, _view);
    }

    // ===========================================================================
    // ===========================   POSE FOR FLOWS   ============================
    // ===========================================================================
    //
    //   ShowMenuPose       High above the spawn, for the title menu.
    //   FollowBackdrop     Keeps the sea floor below a camera a cutscene moves.
    //   PeekGamePose       The gameplay pose for a camera flight, the camera stays where it is.
    //   ResumeFromPose     Eases back out from the pose a cutscene left.
    //

    private const float MENU_CAMERA_DISTANCE = 26f;  // in player sizes
    private const float MENU_CAMERA_TILT = 0.25f;    // tilt from vertical in radians

    /// <summary>Puts the camera high above the player, the pose behind the title menu.</summary>
    public void ShowMenuPose()
    {
        float   size  = Player.BodySize();
        Vector3 focus = Plane3D.To3D(Player.GlobalPosition);
        Camera.Position = focus + new Vector3(0f, Mathf.Cos(MENU_CAMERA_TILT), Mathf.Sin(MENU_CAMERA_TILT)) * size * MENU_CAMERA_DISTANCE;
        Camera.LookAt(focus, Vector3.Up);
        Camera.Far = size * MENU_CAMERA_DISTANCE * 3f;
        _backdrop.Follow(new Vector2(focus.X, focus.Z), size, ViewRadiusAt(size * MENU_CAMERA_DISTANCE));
    }

    /// <summary>Moves the sea floor below a camera that a cutscene flies, else its edges show.</summary>
    /// <param name="focus">(Required) Point on the plane the camera looks at.</param>
    /// <param name="playerSize">(Required) Body size of the player.</param>
    /// <param name="distance">(Required) Distance of the camera to its focus.</param>
    public void FollowBackdrop(Vector2 focus, float playerSize, float distance)
    {
        _backdrop.Follow(focus, playerSize, ViewRadiusAt(distance));
    }

    /// <summary>
    /// The gameplay pose as the end of a camera flight. The camera returns to its current
    /// pose, its far plane covers both poses.
    /// </summary>
    /// <returns>The pose Advance(0) would snap to.</returns>
    public Transform3D PeekGamePose()
    {
        Transform3D startPose = Camera.GlobalTransform;
        float       far       = Camera.Far;
        Advance(0f);
        Transform3D gamePose = Camera.GlobalTransform;
        Camera.GlobalTransform = startPose;
        Camera.Far             = Mathf.Max(far, Camera.Far);
        return gamePose;
    }

    /// <summary>Takes the distance of the current pose, so the camera eases back out from where a cutscene left it.</summary>
    public void ResumeFromPose()
    {
        _camDistance = Camera.GlobalPosition.DistanceTo(_camFocus);
    }

    // ===========================================================================
    // =============================   VIEW RADIUS   =============================
    // ===========================================================================
    //
    //   ViewRadius     Visible radius on the plane at the current camera distance.
    //   ViewRadiusAt   Visible radius on the plane at any camera distance (title menu).
    //

    /// <summary>Radius of the visible area on the plane at the current camera distance, in third person a fixed reach.</summary>
    public float ViewRadius()
    {
        return Mathf.Lerp(ViewRadiusAt(_camDistance), Player.BodySize() * TP_VIEW_RADIUS, _view);
    }

    /// <summary>Radius of the visible area on the plane at a camera distance, with a margin for the camera tilt.</summary>
    private float ViewRadiusAt(float distance)
    {
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        float   aspect   = viewport.X / Mathf.Max(viewport.Y, 1f);
        return distance * Mathf.Tan(Mathf.DegToRad(CAM_FOV * 0.5f)) * Mathf.Sqrt(1f + aspect * aspect) * 1.25f;
    }
}
