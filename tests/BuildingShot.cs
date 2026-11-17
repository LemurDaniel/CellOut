// NOTE:
//   Shows the buildings of the village builder for a movie render, so their look can be
//   checked by eye without the game around them: every template of every function side by
//   side, or every single block. No checks, the frame is for the eye. Quits by itself, the
//   last frame is the still.
//
//   $env:CELLOUT_SHOT_BUILDINGS = 'templates'   # optional: 'templates' (default), 'blocks', a BuildingKind ('Dwelling')
//                                               # or a BlockGroup with the prefix 'group:' ('group:Roof')
//   $env:CELLOUT_SHOT_PAGE = '1'                # optional, with 'blocks': the page of PER_PAGE blocks, from 0
//   godot --path . res://tests/BuildingShot.tscn --write-movie <dir>/f.png --fixed-fps 30 --resolution 1920x1080

//
//   Chapters:
//     SHOW BUILDINGS   One studio per building or block, then quit.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class BuildingShot : Node
{
    // ===========================================================================
    // ===========================   SHOW BUILDINGS   ============================
    // ===========================================================================
    //
    //   _Ready     Collects what CELLOUT_SHOT_BUILDINGS names and builds one view each.
    //   AddView    One studio with a building or block, a label and a camera three quarters from the front.
    //   _Process   Fits the cameras once the models are built, then quits.
    //

    private const int QUIT_FRAME = 24;
    private const int FIT_FRAME = 4;                 // the visuals are built by then
    private const int PER_PAGE = 12;                 // blocks per picture
    private const float FOV = 35f;
    private const float FIT_MARGIN = 1.15f;
    private static readonly Vector3 FROM = new Vector3(1f, 0.55f, 0.8f).Normalized();     // the front of a block is +X
    private static readonly Color BACKDROP = new(0.13f, 0.17f, 0.23f);

    private readonly List<(Camera3D Camera, Node3D Model)> _views = new();
    private int _frame;

    public override void _Ready()
    {
        TestFiles.KeepAwayFromPlayer();
        string what = OS.GetEnvironment("CELLOUT_SHOT_BUILDINGS");
        int    page = int.TryParse(OS.GetEnvironment("CELLOUT_SHOT_PAGE"), out int parsed) ? parsed : 0;

        // What to show: name and a model built on demand
        var shown = new List<(string Name, System.Func<Node3D> Build)>();
        if (what.StartsWith("group:") && System.Enum.TryParse(what[6..], true, out BlockGroup group))
        {
            shown.AddRange(BlockCatalog.In(group).Select(info => (info.Kind.ToString(), (System.Func<Node3D>)(() => BuildingVisual3D.NewBlock(info.Kind, BlockCatalog.DEFAULT_PAINT)))));
        }
        else if (what.StartsWith("era:") && System.Enum.TryParse(what[4..], true, out BlockEra era))
        {
            shown.AddRange(BlockCatalog.In(era).Select(info => (info.Kind.ToString(), (System.Func<Node3D>)(() => BuildingVisual3D.NewBlock(info.Kind, BlockCatalog.DEFAULT_PAINT)))));
        }
        else if ("blocks" == what)
        {
            shown.AddRange(BlockCatalog.BLOCKS.Skip(page * PER_PAGE).Take(PER_PAGE)
                .Select(info => (info.Kind.ToString(), (System.Func<Node3D>)(() => BuildingVisual3D.NewBlock(info.Kind, BlockCatalog.DEFAULT_PAINT)))));
        }
        else
        {
            foreach (BuildingKind kind in System.Enum.GetValues<BuildingKind>())
            {
                if (System.Enum.TryParse(what, true, out BuildingKind only) && only != kind)
                {
                    continue;
                }
                for (int i = 0; i < BuildingTemplates.Count(kind); i++)
                {
                    int index = i;
                    shown.Add(($"{kind} {index}", () =>
                    {
                        var visual = new BuildingVisual3D();
                        visual.Build(BuildingTemplates.Build(kind, index));
                        return visual;
                    }));
                }
            }
        }

        var layer = new CanvasLayer();
        AddChild(layer);
        var backdrop = new ColorRect { Color = BACKDROP };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(backdrop);
        int columns = Mathf.CeilToInt(Mathf.Sqrt(shown.Count * 16f / 9f));
        int rows    = Mathf.CeilToInt(shown.Count / (float)Mathf.Max(columns, 1));
        var grid = new GridContainer { Columns = Mathf.Max(columns, 1) };
        grid.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        grid.AddThemeConstantOverride("h_separation", 0);
        grid.AddThemeConstantOverride("v_separation", 0);
        layer.AddChild(grid);
        Vector2  screen = grid.GetViewportRect().Size;
        Vector2I size   = new((int)(screen.X / Mathf.Max(columns, 1)), (int)(screen.Y / Mathf.Max(rows, 1)));
        foreach ((string name, System.Func<Node3D> build) in shown)
        {
            AddView(grid, name, build(), size);
        }
    }

    private void AddView(GridContainer grid, string name, Node3D model, Vector2I size)
    {
        var container = new SubViewportContainer { Stretch = true, CustomMinimumSize = size };
        grid.AddChild(container);
        SubViewport viewport = PartIcon.NewStudio(size);
        container.AddChild(viewport);
        viewport.AddChild(model);
        var camera = new Camera3D { Fov = FOV, Current = true };
        camera.Transform = new Transform3D(Basis.LookingAt(-FROM, Vector3.Up), FROM * 8f);
        viewport.AddChild(camera);
        var label = new Label { Text = name, Position = new Vector2(8f, 4f) };
        label.AddThemeFontSizeOverride("font_size", 18);
        container.AddChild(label);
        _views.Add((camera, model));
    }

    /// <summary>Every camera backs off until the bounding sphere of all meshes of its model fits the picture.</summary>
    private void FitViews()
    {
        foreach ((Camera3D camera, Node3D model) in _views)
        {
            Aabb? bounds = null;
            foreach (MeshInstance3D mesh in model.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            {
                Transform3D toModel = Transform3D.Identity;
                for (Node node = mesh; node != model && node is Node3D node3D; node = node.GetParent())
                {
                    toModel = node3D.Transform * toModel;
                }
                Aabb box = toModel * mesh.GetAabb();
                bounds = null == bounds ? box : bounds.Value.Merge(box);
            }
            if (null == bounds)
            {
                continue;
            }
            Vector3 center   = bounds.Value.GetCenter();
            float   radius   = Mathf.Max(bounds.Value.Size.Length() * 0.5f, 0.05f);
            Vector3 offset   = FROM * (radius / Mathf.Sin(Mathf.DegToRad(FOV * 0.5f)) * FIT_MARGIN);
            camera.Transform = new Transform3D(Basis.LookingAt(-offset, Vector3.Up), center + offset);
        }
    }

    public override void _Process(double delta)
    {
        _frame++;
        if (FIT_FRAME == _frame)
        {
            FitViews();
        }
        if (_frame >= QUIT_FRAME)
        {
            GetTree().Quit();
        }
    }
}
