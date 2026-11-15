// NOTE:
//   Shows one creature template (data/creatures/<key in lower case>.json) from four sides for a movie render, so
//   the look of an animal can be checked by eye without the editor around it: side,
//   three quarters from the front, top and front. No checks, the frame is for the eye.
//   Quits by itself, the last frame is the still.
//
//   $env:CELLOUT_SHOT_TEMPLATE = 'SHARK'
//   $env:CELLOUT_SHOT_VIEW = 'hero'          # optional: one big view three quarters from the front (README gallery)
//   godot --path . res://tests/CreatureShot.tscn --write-movie <dir>/f.png --fixed-fps 30
//   Usually run through tools/Show-Creature.ps1 -Template SHARK.

//
//   Chapters:
//     SHOW CREATURE   Four studios around the same body plan, then quit.
//

using Godot;

namespace CellOut;

public partial class CreatureShot : Node
{
    // ===========================================================================
    // ============================   SHOW CREATURE   ============================
    // ===========================================================================
    //
    //   _Ready     Builds the four views of the template named by CELLOUT_SHOT_TEMPLATE.
    //   AddView    One studio with the creature and a camera looking from a direction, perRow views share the width.
    //   _Process   Quits after the creature has settled.
    //

    private const int QUIT_FRAME = 24;
    private const float FOV = 40f;
    private const float ROOM = 2.4f;                 // fins, limbs and stalks reach past the body, in unit space
    private static readonly Color BACKDROP = new(0.13f, 0.17f, 0.23f);

    private int _frame;

    public override void _Ready()
    {
        string key = OS.GetEnvironment("CELLOUT_SHOT_TEMPLATE");
        // Only the file of this template is read: a broken file of another animal must not stop the shot
        string file = $"{CreatureTemplates.FOLDER_PATH}/{key.ToLowerInvariant()}.json";
        CreatureDesign design;
        try
        {
            design = CreatureTemplates.Parse(FileAccess.GetFileAsString(file)).Build();
        }
        catch (System.Exception error)
        {
            // An empty picture would hide the mistake: say it and leave with an error code
            GD.PrintErr($"CREATURE SHOT FAILED: {file}: {error.Message}");
            GetTree().Quit(1);
            return;
        }
        Log.Verbose("CreatureShot", $"Showing {key}: {design.Girths.Count} vertebrae, {design.Parts.Count} parts.");

        var layer = new CanvasLayer();
        AddChild(layer);
        var backdrop = new ColorRect { Color = BACKDROP };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(backdrop);
        bool isHero = "hero" == OS.GetEnvironment("CELLOUT_SHOT_VIEW");
        var grid = new GridContainer { Columns = isHero ? 1 : 2 };
        grid.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        grid.AddThemeConstantOverride("h_separation", 0);
        grid.AddThemeConstantOverride("v_separation", 0);
        layer.AddChild(grid);

        // Side (the nose points right), three quarters from the front, top (the nose points right), front
        // A bent spine (lifts) takes room above and below, the camera aims at its middle
        float low    = System.Linq.Enumerable.Min(design.Lifts);
        float high   = System.Linq.Enumerable.Max(design.Lifts);
        float span   = Mathf.Max(design.NoseTipX - design.TailTipX, (high - low) * 1.6f) + ROOM;
        var   middle = new Vector3(0f, (low + high) * 0.5f - 0.15f, 0f);
        // Hero: one big view three quarters from the front, for the gallery of the README
        if (isHero)
        {
            AddView(grid, design, middle + Vector3.Up * 0.15f, new Vector3(0.6f, 0.3f, 1f), Vector3.Up, span * 1.15f, 1);
            return;
        }
        AddView(grid, design, middle, new Vector3(0f, 0.12f, 1f), Vector3.Up, span);
        AddView(grid, design, middle, new Vector3(0.75f, 0.45f, 0.8f), Vector3.Up, span);
        // Top and front show what sticks out sideways (flippers, legs, arms): more room
        AddView(grid, design, middle, new Vector3(0f, 1f, 0.001f), Vector3.Left, span * 1.3f);
        AddView(grid, design, middle, new Vector3(1f, 0.1f, 0f), Vector3.Up, span * 1.1f);
    }

    private static void AddView(GridContainer grid, CreatureDesign design, Vector3 middle, Vector3 from, Vector3 up, float span, int perRow = 2)
    {
        Vector2I size = (Vector2I)(grid.GetViewportRect().Size / perRow);
        var container = new SubViewportContainer { Stretch = true, CustomMinimumSize = size };
        grid.AddChild(container);
        SubViewport viewport = PartIcon.NewStudio(size);
        container.AddChild(viewport);
        viewport.AddChild(new CreatureVisual3D { Design = design, BodyColor = design.Color ?? Player.ORGANISM_COLOR, Knead = CreatureVisual3D.LIVING_KNEAD });

        // The camera backs off until the whole span fits the width of the view
        float   distance = span * 0.5f / Mathf.Tan(Mathf.DegToRad(FOV * 0.5f)) / ((float)size.X / size.Y) * 1.0f;
        Vector3 offset   = from.Normalized() * distance;
        var camera = new Camera3D { Fov = FOV, Current = true };
        camera.Transform = new Transform3D(Basis.LookingAt(-offset, up), middle + offset);
        viewport.AddChild(camera);
    }

    public override void _Process(double delta)
    {
        _frame++;
        if (_frame >= QUIT_FRAME)
        {
            GetTree().Quit();
        }
    }
}
