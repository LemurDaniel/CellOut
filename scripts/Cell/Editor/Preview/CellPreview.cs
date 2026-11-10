// NOTE:
//   The right column of the cell editor: color swatches above the rotatable 3D cell in
//   its petri dish (own SubViewport with orbit camera, glowing floor, dotted rim,
//   bubbles and a forward arrow). It owns the camera and the cell of the draft, answers
//   what lies under a pixel and plays the punch and ring burst. It decides nothing:
//   mouse input, a dragged part over the cell and a clicked swatch go out as events,
//   CellEditor routes them. The other preview components (PartGhost, PartPoser,
//   MembraneSculptor) get this node in their Setup and live in its world (AddToWorld).
//
//   var preview = new CellPreview();
//   preview.Setup(session);
//   preview.PreviewInput += OnPreviewInput;
//   body.AddChild(preview);
//   preview.ShowCell();

//
//   Chapters:
//     EVENTS AND SETUP   What the preview reports and the session it shows.
//     ORBIT CAMERA       Yaw, pitch and zoom, a press that is a click or a rotation.
//     PICK ON CELL       What lies under a pixel: unit point, membrane, placed part.
//     SHOW CELL          The draft cell, its color and the swatches.
//     FEEDBACK           Cell punch, ring burst, the turning dish and the diet accent.
//     BUILD PREVIEW      Swatches, viewport, camera, petri dish, bubbles, arrow and cell.
//

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class CellPreview : VBoxContainer
{
    // ===========================================================================
    // ==========================   EVENTS AND SETUP   ===========================
    // ===========================================================================
    //
    //   PreviewInput   Mouse input on the 3D cell, in pixels of the preview.
    //   PartHovered    A dragged palette part moves over the cell.
    //   PartDropped    A dragged palette part is released over the cell.
    //   ColorPicked    A swatch was clicked, null is the color of the stage.
    //   Setup          Hands in the session whose draft is shown.
    //

    /// <summary>Mouse input on the 3D cell, positions in pixels of the preview.</summary>
    public event Action<InputEvent> PreviewInput;

    /// <summary>A dragged palette part moves over the cell: pixel and part.</summary>
    public event Action<Vector2, PartKind> PartHovered;

    /// <summary>A dragged palette part is released over the cell: pixel and part.</summary>
    public event Action<Vector2, PartKind> PartDropped;

    /// <summary>A swatch was clicked: its color, null for the color of the stage.</summary>
    public event Action<Color?> ColorPicked;

    private CellEditSession _session;

    /// <summary>Hands in the shared session, call before the preview enters the tree.</summary>
    /// <param name="session">(Required) The session whose draft is shown and hit.</param>
    public void Setup(CellEditSession session)
    {
        _session = session;
    }

    // ===========================================================================
    // ============================   ORBIT CAMERA   =============================
    // ===========================================================================
    //
    //   ResetCamera    Front view from above, forward to the right.
    //   Zoom           One wheel step closer or further away.
    //   BeginPress     Left press: maybe a click, maybe the start of a rotation.
    //   TrackOrbit     Whether the press has become a rotation (past the threshold).
    //   Orbit          Turns the camera by a mouse motion.
    //   EndPress       Left release.
    //   UpdateCamera   Orbit position from yaw, pitch and zoom.
    //

    private const float CAMERA_DISTANCE = 5.5f;          // fits a cell with flagella, the editor always shows one cell
    private const float CAMERA_FOV = 40f;
    private const float START_PITCH = 0.9f;              // radians above the plane
    private const float MIN_PITCH = -1.2f;
    private const float MAX_PITCH = 1.5f;
    private const float ORBIT_SPEED = 0.008f;            // radians per pixel
    private const float ZOOM_STEP = 1.1f;
    private const float MIN_ZOOM = 0.55f;
    private const float MAX_ZOOM = 1.8f;

    private float _yaw;
    private float _pitch;
    private float _zoom = 1f;
    private bool _isLeftDown;
    private bool _isOrbiting;
    private Vector2 _leftDownAt;

    /// <summary>The camera of the preview world, for hit tests of the other preview components.</summary>
    public Camera3D Camera { get; private set; }

    /// <summary>Whether the left button is held on the cell.</summary>
    public bool IsPressed => _isLeftDown;

    /// <summary>Whether the held press has become a rotation.</summary>
    public bool IsOrbiting => _isOrbiting;

    public void ResetCamera()
    {
        _yaw   = 0f;
        _pitch = START_PITCH;
        _zoom  = 1f;
        UpdateCamera();
    }

    /// <summary>One wheel step.</summary>
    /// <param name="isCloser">(Required) True zooms in, false out.</param>
    public void Zoom(bool isCloser)
    {
        _zoom = Mathf.Clamp(_zoom * (isCloser ? 1f / ZOOM_STEP : ZOOM_STEP), MIN_ZOOM, MAX_ZOOM);
        UpdateCamera();
    }

    /// <summary>Left press on the cell: a click until TrackOrbit says otherwise.</summary>
    /// <param name="pixel">(Required) Where the button went down.</param>
    public void BeginPress(Vector2 pixel)
    {
        _isLeftDown = true;
        _isOrbiting = false;
        _leftDownAt = pixel;
    }

    /// <summary>Checks whether the held press has moved far enough to count as a rotation.</summary>
    /// <param name="pixel">(Required) The mouse position now.</param>
    /// <param name="threshold">(Required) Pixels from the press before a click becomes a rotation.</param>
    /// <returns>True once the press is a rotation, it stays one until EndPress.</returns>
    public bool TrackOrbit(Vector2 pixel, float threshold)
    {
        _isOrbiting = _isOrbiting || pixel.DistanceTo(_leftDownAt) > threshold;
        return _isOrbiting;
    }

    /// <summary>Turns the camera around the cell.</summary>
    /// <param name="relative">(Required) Mouse motion in pixels.</param>
    public void Orbit(Vector2 relative)
    {
        _yaw  -= relative.X * ORBIT_SPEED;
        _pitch = Mathf.Clamp(_pitch + relative.Y * ORBIT_SPEED, MIN_PITCH, MAX_PITCH);
        UpdateCamera();
    }

    public void EndPress()
    {
        _isLeftDown = false;
        _isOrbiting = false;
    }

    /// <summary>Yaw 0 looks from +Z towards the cell, so screen right is +X (forward).</summary>
    private void UpdateCamera()
    {
        float distance = CAMERA_DISTANCE * _zoom;
        Vector3 offset = new Vector3(
            Mathf.Sin(_yaw) * Mathf.Cos(_pitch),
            Mathf.Sin(_pitch),
            Mathf.Cos(_yaw) * Mathf.Cos(_pitch)) * distance;
        // Straight from above or below the default up vector degenerates, so up follows the yaw there
        Vector3 up = Mathf.Abs(Mathf.Cos(_pitch)) < 0.01f ? new Vector3(-Mathf.Sin(_yaw), 0f, -Mathf.Cos(_yaw)) : Vector3.Up;
        Camera.Transform = new Transform3D(Basis.LookingAt(-offset, up), offset);
    }

    // ===========================================================================
    // ============================   PICK ON CELL   =============================
    // ===========================================================================
    //
    //   IsShown            Whether the editor panel around the preview is visible.
    //   IsMouseOver        Whether the mouse is over the 3D cell.
    //   ToUnit             Preview pixel to unit space (MembraneGeometry.ToUnit).
    //   TryHitSurface      The point of the draft membrane under a pixel (MembraneGeometry.TryHitSurface).
    //   PartAtPixel        The placed part under a pixel, -1 for none.
    //   TryGetPartAnchor   Where a placed part sits in the preview world, for the gizmo.
    //   CellBasis          Basis from the unit space of the cell to the preview world.
    //   SetCursor          The mouse cursor over the cell.
    //

    /// <summary>Whether the editor panel around the preview is visible (false once it slid out).</summary>
    public bool IsShown => IsVisibleInTree();

    /// <summary>Whether the mouse is over the 3D cell.</summary>
    public bool IsMouseOver => _container.GetGlobalRect().HasPoint(_container.GetGlobalMousePosition());

    /// <summary>Preview pixel to unit space, see MembraneGeometry.ToUnit.</summary>
    public Vector2 ToUnit(Vector2 pixel)
    {
        return MembraneGeometry.ToUnit(Camera.ProjectRayOrigin(pixel), Camera.ProjectRayNormal(pixel));
    }

    /// <summary>The point of the draft membrane under a pixel, see MembraneGeometry.TryHitSurface.</summary>
    /// <param name="pixel">(Required) Pixel in the preview.</param>
    /// <param name="point">The hit seen from above, in unit space.</param>
    /// <param name="lift">Height of the hit, the sine of its latitude.</param>
    /// <param name="surface">The hit in 3D, for the hover dot.</param>
    /// <returns>False when the ray misses the membrane.</returns>
    public bool TryHitSurface(Vector2 pixel, out Vector2 point, out float lift, out Vector3 surface)
    {
        return MembraneGeometry.TryHitSurface(_session.Draft, Camera.ProjectRayOrigin(pixel), Camera.ProjectRayNormal(pixel),
            out point, out lift, out surface);
    }

    /// <summary>The placed part under a pixel of the preview, -1 for none.</summary>
    public int PartAtPixel(Vector2 pixel)
    {
        CellVisual3D visual = _body.Visual;
        return null == visual ? -1 : visual.PartAt(Camera.ProjectRayOrigin(pixel), Camera.ProjectRayNormal(pixel));
    }

    /// <summary>Where a placed part sits in the preview world.</summary>
    /// <param name="index">(Required) Index in Draft.Parts.</param>
    /// <param name="anchor">The anchor of the part, zero when there is none.</param>
    /// <returns>False while the cell is not built or the part has no anchor.</returns>
    public bool TryGetPartAnchor(int index, out Vector3 anchor)
    {
        anchor = Vector3.Zero;
        return null != _body.Visual && _body.Visual.TryGetPartAnchor(index, out anchor);
    }

    /// <summary>Basis from the unit space of the preview cell to the preview world (turn and size of the visual).</summary>
    public Basis CellBasis()
    {
        CellVisual3D visual = _body.Visual;
        return null != visual && visual.IsInsideTree() ? visual.GlobalTransform.Basis : Basis.Identity;
    }

    /// <summary>Sets the mouse cursor shown over the cell.</summary>
    public void SetCursor(CursorShape shape)
    {
        _container.MouseDefaultCursorShape = shape;
    }

    // ===========================================================================
    // ==============================   SHOW CELL   ==============================
    // ===========================================================================
    //
    //   ShowCell     Puts the draft of the session into the dish.
    //   ClearCell    Drops the draft from the dish (the panel slid out).
    //   ShowColor    Paints the cell and marks the picked swatch.
    //   AddToWorld   Adds a node of another preview component to the 3D world.
    //

    private readonly List<(Color? Color, Button Swatch)> _swatches = new();
    private SubViewportContainer _container;
    private SubViewport _viewport;
    private CellBody _body;

    public void ShowCell()
    {
        _body.Design = _session.Draft;
    }

    public void ClearCell()
    {
        _body.Design = new CellDesign();
    }

    /// <summary>Paints the cell and the swatches.</summary>
    /// <param name="color">(Required) The color of the cell.</param>
    /// <param name="stageColor">(Required) The color of the stage, shown by the first swatch.</param>
    /// <param name="picked">(Required) The color of the draft, null for the stage color: its swatch is marked.</param>
    public void ShowColor(Color color, Color stageColor, Color? picked)
    {
        _body.BodyColor = color;
        foreach ((Color? swatchColor, Button swatch) in _swatches)
        {
            EditorStyle.ApplySwatch(swatch, swatchColor ?? stageColor, swatchColor == picked);
        }
    }

    /// <summary>Adds a node to the 3D world of the preview, above everything added before.</summary>
    /// <param name="node">(Required) A preview component (ghost, gizmo, height handles).</param>
    public void AddToWorld(Node node)
    {
        _viewport.AddChild(node);
    }

    // ===========================================================================
    // ==============================   FEEDBACK   ===============================
    // ===========================================================================
    //
    //   Celebrate     Punches the cell to a scale and sends a ring out from a point.
    //   Punch         A short bounce of the whole preview.
    //   TurnDish      Turns the dotted rim of the petri dish.
    //   ApplyAccent   Dish, rim dots and arrow take on the accent of the diet.
    //

    private const float BURST_SECONDS = 0.45f;
    private const float DISH_TURN_SPEED = 0.12f;     // radians per second

    private readonly StandardMaterial3D _floorMaterial = new()
    {
        ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
    };
    private readonly StandardMaterial3D _dotMaterial = new()
    {
        Transparency    = BaseMaterial3D.TransparencyEnum.Alpha,
        EmissionEnabled = true,
    };
    private readonly StandardMaterial3D _arrowMaterial = new()
    {
        Transparency             = BaseMaterial3D.TransparencyEnum.Alpha,
        EmissionEnabled          = true,
        EmissionEnergyMultiplier = 0.4f,
    };
    private Node3D _dish;
    private Node3D _dishRim;
    private Node3D _effects;

    /// <summary>Punches the cell and sends a ring out, on place, remove and a released height handle.</summary>
    /// <param name="position">(Required) Where the ring starts, in the preview world.</param>
    /// <param name="color">(Required) Color of the ring.</param>
    /// <param name="punch">(Required) Scale the cell jumps to before it springs back to 1.</param>
    public void Celebrate(Vector3 position, Color color, float punch)
    {
        // Cell punch: out and elastic back
        Tween bounce = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        bounce.TweenMethod(Callable.From<float>(radius => _body.Radius = radius), 1f, punch, 0.08f);
        bounce.TweenMethod(Callable.From<float>(radius => _body.Radius = radius), punch, 1f, 0.35f)
            .SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);

        // Ring burst at the part, its own material so the fade does not touch other rings
        var material = new StandardMaterial3D
        {
            ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor  = color,
        };
        MeshInstance3D ring = CellVisual3D.AddMesh(_effects, CellVisual3D.TorusMesh(), material, position, Vector3.One * 0.1f);
        ring.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        Tween burst = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        burst.TweenMethod(Callable.From<float>(t =>
        {
            ring.Scale           = new Vector3(1f, 0.4f, 1f) * Mathf.Lerp(0.1f, 0.7f, t);
            material.AlbedoColor = new Color(color, 1f - t);
        }), 0f, 1f, BURST_SECONDS).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        burst.TweenCallback(Callable.From(ring.QueueFree));
    }

    /// <summary>A short bounce of the 3D cell view, e.g. on a new color.</summary>
    /// <param name="scale">(Required) Scale it jumps to, see EditorStyle.Punch.</param>
    public void Punch(float scale)
    {
        EditorStyle.Punch(_container, scale);
    }

    /// <summary>Turns the dotted rim of the petri dish while the editor shows.</summary>
    /// <param name="delta">(Required) Seconds since the last frame.</param>
    public void TurnDish(float delta)
    {
        if (null != _dishRim && IsShown)
        {
            _dishRim.RotateY(delta * DISH_TURN_SPEED);
        }
    }

    /// <summary>Tints floor, rim dots and arrow, see CellEditorPanel (DIET TINT).</summary>
    /// <param name="accent">(Required) The accent color of the diet.</param>
    public void ApplyAccent(Color accent)
    {
        _floorMaterial.AlbedoColor = accent;
        _dotMaterial.AlbedoColor   = new Color(accent, 0.75f);
        _dotMaterial.Emission      = accent;
        _arrowMaterial.AlbedoColor = new Color(accent, 0.6f);
        _arrowMaterial.Emission    = accent;
    }

    // ===========================================================================
    // ============================   BUILD PREVIEW   ============================
    // ===========================================================================
    //
    //   _Ready       Color swatches over a SubViewport with its own 3D world, orbit
    //                camera, petri dish, bubbles, forward arrow and the cell.
    //   OnCanDrop    Reports a dragged part over the cell (PartHovered).
    //   OnDrop       Reports a part released over the cell (PartDropped).
    //

    private const int RIM_DOTS = 48;                 // dots on the rim of the petri dish
    private const float DISH_RADIUS = 1.8f;

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical   = SizeFlags.ExpandFill;

        _container = new SubViewportContainer
        {
            Stretch             = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical   = SizeFlags.ExpandFill,
            MouseFilter         = MouseFilterEnum.Stop,
        };
        _container.GuiInput += @event => PreviewInput?.Invoke(@event);
        _container.SetDragForwarding(new Callable(),
            Callable.From<Vector2, Variant, bool>(OnCanDrop),
            Callable.From<Vector2, Variant>(OnDrop));

        // Column: color swatches above the cell, the first one is the color of the stage
        var swatchRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        swatchRow.AddThemeConstantOverride("separation", 8);
        AddChild(swatchRow);
        foreach (Color? swatchColor in new Color?[] { null }.Concat(EditorStyle.BODY_SWATCHES.Select(color => (Color?)color)))
        {
            var swatch = new Button { CustomMinimumSize = new Vector2(30, 30), FocusMode = FocusModeEnum.None };
            Color? picked = swatchColor;
            swatch.Pressed      += () => ColorPicked?.Invoke(picked);
            swatch.MouseEntered += () => EditorStyle.ScaleTo(swatch, 1.15f);
            swatch.MouseExited  += () => EditorStyle.ScaleTo(swatch, 1f);
            swatchRow.AddChild(swatch);
            _swatches.Add((swatchColor, swatch));
        }
        AddChild(_container);

        _viewport = PartIcon.NewStudio(new Vector2I(440, 440));
        _container.AddChild(_viewport);

        Camera = new Camera3D { Fov = CAMERA_FOV, Current = true };
        _viewport.AddChild(Camera);

        // Petri dish: a soft glowing floor and a slowly turning dotted rim
        _dish = new Node3D { Position = new Vector3(0f, -0.65f, 0f) };
        _viewport.AddChild(_dish);
        var glow = new GradientTexture2D
        {
            Gradient = new Gradient
            {
                Colors  = new[] { new Color(1f, 1f, 1f, 0.45f), new Color(1f, 1f, 1f, 0.18f), new Color(1f, 1f, 1f, 0f) },
                Offsets = new[] { 0f, 0.6f, 1f },
            },
            Fill     = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo   = new Vector2(1f, 0.5f),
            Width    = 128,
            Height   = 128,
        };
        var floor = new MeshInstance3D
        {
            Mesh             = new PlaneMesh { Size = Vector2.One * DISH_RADIUS * 2.2f },
            MaterialOverride = _floorMaterial,
            CastShadow       = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _floorMaterial.AlbedoTexture = glow;
        _dish.AddChild(floor);
        _dishRim = new Node3D();
        _dish.AddChild(_dishRim);
        for (int i = 0; i < RIM_DOTS; i++)
        {
            float angle = Mathf.Tau * i / RIM_DOTS;
            float size  = 0 == i % 4 ? 0.04f : 0.022f;
            MeshInstance3D dot = CellVisual3D.AddMesh(_dishRim, CellVisual3D.SphereMesh(true), _dotMaterial,
                new Vector3(Mathf.Cos(angle), 0.03f, Mathf.Sin(angle)) * (DISH_RADIUS + 0.12f), Vector3.One * size);
            dot.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }

        // Bubbles rising through the dish
        var bubbleRamp = new Gradient
        {
            Colors  = new[] { new Color(0.85f, 0.9f, 1f, 0f), new Color(0.85f, 0.9f, 1f, 0.5f), new Color(0.85f, 0.9f, 1f, 0f) },
            Offsets = new[] { 0f, 0.3f, 1f },
        };
        var bubbles = new CpuParticles3D
        {
            Amount             = 36,
            Lifetime           = 6f,
            Preprocess         = 6f,
            Mesh               = CellVisual3D.SphereMesh(true),
            EmissionShape      = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(DISH_RADIUS, 0.1f, DISH_RADIUS),
            Direction          = Vector3.Up,
            Spread             = 12f,
            Gravity            = new Vector3(0f, 0.1f, 0f),
            InitialVelocityMin = 0.05f,
            InitialVelocityMax = 0.2f,
            ScaleAmountMin     = 0.015f,
            ScaleAmountMax     = 0.05f,
            ColorRamp          = bubbleRamp,
            MaterialOverride   = new StandardMaterial3D
            {
                ShadingMode            = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency           = BaseMaterial3D.TransparencyEnum.Alpha,
                VertexColorUseAsAlbedo = true,
            },
            CastShadow         = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _dish.AddChild(bubbles);

        // Forward arrow on the plane in front of the cell, it stays readable from every angle
        var arrow = new Node3D { Position = new Vector3(1.55f, 0f, 0f) };
        _viewport.AddChild(arrow);
        MeshInstance3D cone = CellVisual3D.AddMesh(arrow, CellVisual3D.ConeMesh(),
            _arrowMaterial, Vector3.Zero, Vector3.One);
        cone.Basis      = new Basis(Vector3.Back, -Mathf.Pi * 0.5f) * Basis.FromScale(new Vector3(1.6f, 0.6f, 1.6f));
        cone.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        _body = new CellBody { Radius = 1f };
        _viewport.AddChild(_body);
        _effects = new Node3D();                     // ring bursts of FEEDBACK
        _viewport.AddChild(_effects);
    }

    /// <summary>Any dragged palette part may drop on the cell, where it fits decides the ghost.</summary>
    private bool OnCanDrop(Vector2 position, Variant data)
    {
        if (Variant.Type.Int != data.VariantType)
        {
            return false;
        }
        PartHovered?.Invoke(position, (PartKind)data.AsInt32());
        return true;
    }

    private void OnDrop(Vector2 position, Variant data)
    {
        PartDropped?.Invoke(position, (PartKind)data.AsInt32());
    }
}
