// NOTE:
//   Spray can of the creature editor: while it is on (CreatureEditorSession.IsSpraying),
//   a left drag over the body sprays dots of the second color (mirrored with symmetry)
//   and the wheel sets the nozzle. It owns the nozzle size and the stroke a drag belongs
//   to. It reads no input on its own: the CreatureEditor hands it the events of the
//   stage first while the can is on (Handle) and only keeps what the can does not take.
//
//   var spray = new CreatureSprayCan();
//   spray.Setup(session, stage);
//   spray.StrokeEnded += Refresh;
//   AddChild(spray);
//   if (session.IsSpraying && spray.Handle(@event)) return;

//
//   Chapters:
//     SPRAY PAINT   Nozzle, strokes, the dots and the input of the spray can.
//

using Godot;

namespace CellOut;

public partial class CreatureSprayCan : Node
{
    // ===========================================================================
    // =============================   SPRAY PAINT   =============================
    // ===========================================================================
    //
    //   StrokeEnded  Raised when a stroke ends: undo and wipe light up.
    //   Radius       Size of the nozzle, unit space.
    //   Hover        Where the nozzle points on the body, null beside it.
    //   Setup        Takes session and stage.
    //   Reset        Ends the stroke and forgets the hover point.
    //   SprayAt      One spray dot (and its mirror with symmetry) where the body was hit.
    //   Handle       Wheel, press, drag and release while the spray can is on.
    //

    // Spray can: nozzle size and the stroke a drag belongs to
    private const float SPRAY_RADIUS = 0.12f;
    private const float SPRAY_STEP = 0.02f;          // nozzle change per wheel notch
    private const float SPRAY_SPACING = 0.35f;       // share of the nozzle a drag moves before the next dot
    private const float MIRROR_EPSILON = 0.03f;      // dots this close to the midline get no mirror twin

    public event System.Action StrokeEnded;

    private CreatureEditorSession _session;
    private CreatureStage _stage;
    private int _sprayStroke;
    private Vector3? _lastSpray;

    public float Radius { get; private set; } = SPRAY_RADIUS;
    public Vector3? Hover { get; private set; }

    /// <summary>Call once before the first input.</summary>
    /// <param name="session">(Required) The sitting: draft, spray color and symmetry.</param>
    /// <param name="stage">(Required) The stage, it knows the point of the body under a pixel.</param>
    public void Setup(CreatureEditorSession session, CreatureStage stage)
    {
        _session = session;
        _stage   = stage;
    }

    /// <summary>Ends the stroke and forgets the hover point, called when a sitting begins and when the can is switched.</summary>
    public void Reset()
    {
        _lastSpray = null;
        Hover      = null;
    }

    /// <summary>Sprays one dot of the spray color, mirrored on the other flank while symmetry is on. Free of charge.</summary>
    /// <param name="hit">(Required) Point on the body, unit space.</param>
    /// <param name="stroke">(Optional) Stroke id, 0 starts a new stroke. Defaults to 0.</param>
    /// <returns>False when the body holds CreatureDesign.MAX_SPLATS dots already.</returns>
    /// <example><code>spray.SprayAt(new Vector3(0f, 0f, 0.4f));</code></example>
    public bool SprayAt(Vector3 hit, int stroke = 0)
    {
        CreatureDesign draft = _session.Draft;
        int  id        = 0 == stroke ? draft.NewStroke() : stroke;
        bool isSprayed = draft.AddSplat(hit, Radius, _session.SprayColor, id);
        if (isSprayed && _session.IsSymmetric && Mathf.Abs(hit.Z) > MIRROR_EPSILON)
        {
            draft.AddSplat(new Vector3(hit.X, hit.Y, -hit.Z), Radius, _session.SprayColor, id);
        }
        return isSprayed;
    }

    /*
      Spray can input, the rest of the stage input only sees what it does not take:
      > Wheel over the body: nozzle size.
      > Left press on the body: a new stroke, the first dot.
      > Motion with the stroke on: a dot every SPRAY_SPACING of the nozzle; hover moves the marker.
      > Left release: the stroke ends.
      A press beside the body falls through and turns the view as usual.
    */
    /// <param name="event">(Required) An input event of the stage.</param>
    /// <returns>True when the spray can took the event.</returns>
    public bool Handle(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel when _stage.RayToBody(wheel.Position) is not null:
                float notch = MouseButton.WheelUp == wheel.ButtonIndex ? 1f : -1f;
                Radius = Mathf.Clamp(Radius + notch * SPRAY_STEP, CreatureDesign.MIN_SPLAT_RADIUS, CreatureDesign.MAX_SPLAT_RADIUS);
                return true;

            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } press when _stage.RayToBody(press.Position) is Vector3 start:
                _sprayStroke = _session.Draft.NewStroke();
                _lastSpray   = start;
                SprayAt(start, _sprayStroke);
                return true;

            case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } when null != _lastSpray:
                _lastSpray = null;
                StrokeEnded?.Invoke();               // undo and wipe light up
                return true;

            case InputEventMouseMotion motion:
                Hover = _stage.RayToBody(motion.Position);
                if (null == _lastSpray || Hover is not Vector3 at)
                {
                    return null != _lastSpray;
                }
                if (at.DistanceTo(_lastSpray.Value) >= Radius * SPRAY_SPACING)
                {
                    SprayAt(at, _sprayStroke);
                    _lastSpray = at;
                }
                return true;
        }
        return false;
    }
}
