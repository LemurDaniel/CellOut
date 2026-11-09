// NOTE:
//   Temporary photo studio: shapes four funny creatures with the design API and saves
//   one PNG each to docs/creatures/ for the README (deleted after the shoot).
//
//   godot --path . --resolution 1920x1440 res://tests/TmpShowcase.tscn
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class TmpShowcase : Node3D
{
    private const int SETTLE_FRAMES = 25;
    private const float FILL = 1.0f;

    private readonly List<(string File, CreatureDesign Design, Color Color, Vector3 View)> _models = new();
    private CreatureVisual3D _visual;
    private Camera3D _camera;
    private MeshInstance3D _floor;
    private int _index = -1;
    private int _frame;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
        BuildStudio();
        _models.Add(("squid", Squid(), new Color(0.88f, 0.42f, 0.72f), new Vector3(0.9f, 0.45f, 0.75f)));
        _models.Add(("crab", Crab(), new Color(0.95f, 0.38f, 0.28f), new Vector3(0.75f, 0.55f, 1f)));
        _models.Add(("giraffe-sausage", GiraffeSausage(), new Color(0.98f, 0.78f, 0.35f), new Vector3(0.45f, 0.3f, 1f)));
        _models.Add(("muscle-whale", MuscleWhale(), new Color(0.45f, 0.62f, 0.92f), new Vector3(0.75f, 0.35f, 1f)));
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://docs/creatures"));
    }

    public override void _Process(double delta)
    {
        _frame++;
        if (_index < 0 || SETTLE_FRAMES * 2 == _frame)
        {
            if (_index >= 0)
            {
                string path  = ProjectSettings.GlobalizePath($"res://docs/creatures/{_models[_index].File}.png");
                Image  image = GetViewport().GetTexture().GetImage();
                image.Resize(image.GetWidth() / 2, image.GetHeight() / 2, Image.Interpolation.Lanczos);
                image.SavePng(path);
                GD.Print($"SHOT saved {path}");
            }
            _index++;
            _frame = 0;
            if (_index >= _models.Count)
            {
                GetTree().Quit();
                return;
            }
            _visual.Design    = _models[_index].Design;
            _visual.BodyColor = _models[_index].Color;
        }
        if (SETTLE_FRAMES == _frame)
        {
            FitCamera(_models[_index].View.Normalized());
        }
    }

    private void BuildStudio()
    {
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor        = new Color(0.05f, 0.16f, 0.26f),
            SkyHorizonColor    = new Color(0.12f, 0.36f, 0.45f),
            GroundBottomColor  = new Color(0.03f, 0.08f, 0.12f),
            GroundHorizonColor = new Color(0.12f, 0.36f, 0.45f),
        };
        AddChild(new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode     = Godot.Environment.BGMode.Sky,
            Sky                = new Sky { SkyMaterial = sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor  = new Color(0.68f, 0.78f, 0.9f),
            AmbientLightEnergy = 0.55f,
            SsaoEnabled        = true,
            SsaoRadius         = 0.35f,
            SsaoIntensity      = 1.6f,
        } });
        AddChild(new DirectionalLight3D { Basis = Basis.LookingAt(new Vector3(0.4f, -1f, -0.5f), Vector3.Up), LightEnergy = 1f, ShadowEnabled = true });
        AddChild(new DirectionalLight3D { Basis = Basis.LookingAt(new Vector3(-0.6f, -0.2f, 0.8f), Vector3.Up), LightEnergy = 0.35f, LightColor = new Color(0.55f, 0.85f, 1f) });
        _floor = CellVisual3D.AddMesh(this, new CylinderMesh { TopRadius = 3.6f, BottomRadius = 3.7f, Height = 0.06f, RadialSegments = 96 },
            CellVisual3D.Material(new Color(0.36f, 0.33f, 0.27f), 0f, 0.95f), new Vector3(0f, -0.95f, 0f), Vector3.One);
        _visual = new CreatureVisual3D { Knead = 0.3f, SwimScale = 0.2f, Seed = 3f };
        AddChild(_visual);
        _camera = new Camera3D { Fov = 34f, Current = true };
        AddChild(_camera);
    }

    // The sand under the lowest point, the camera moved in until the widest projected corner fills FILL of the screen
    private void FitCamera(Vector3 view)
    {
        Aabb? bounds = null;
        foreach (MeshInstance3D mesh in _visual.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (!mesh.IsVisibleInTree()) continue;
            Aabb box = mesh.GlobalTransform * mesh.GetAabb();
            bounds = null == bounds ? box : bounds.Value.Merge(box);
        }
        if (null == bounds) return;
        _floor.Position = new Vector3(bounds.Value.GetCenter().X, bounds.Value.Position.Y - 0.03f, 0f);
        Vector3 center   = bounds.Value.GetCenter();
        float   distance = bounds.Value.Size.Length() * 2f;
        Vector2 screen   = GetViewport().GetVisibleRect().Size;
        for (int pass = 0; pass < 4; pass++)
        {
            Vector3 eye = center + view * distance;
            _camera.Transform = new Transform3D(Basis.LookingAt(center - eye, Vector3.Up), eye);
            float extent = 0f;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector2 share = (_camera.UnprojectPosition(bounds.Value.GetEndpoint(corner)) - screen * 0.5f) / (screen * 0.5f);
                extent = Mathf.Max(extent, Mathf.Max(Mathf.Abs(share.X), Mathf.Abs(share.Y)));
            }
            distance *= extent / FILL;
        }
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static void Spine(CreatureDesign d, params float[] girths)
    {
        while (d.Girths.Count < girths.Length) d.GrowEnd(isNose: false);
        while (d.Girths.Count > girths.Length) d.ShrinkEnd(isNose: false);
        for (int i = 0; i < girths.Length; i++) d.SetGirth(i, girths[i]);
    }

    private static int Place(CreatureDesign d, CreaturePartKind kind, float t, float size = 1f, int side = 0)
    {
        if (!d.TryPlace(kind, t, 0, side)) { GD.Print($"SHOT could not place {kind} at {t}"); return -1; }
        d.SetPartSize(d.Parts.Count - 1, size);
        return d.Parts.Count - 1;
    }

    private static int Branch(CreatureDesign d, CreaturePartKind kind, int parent, int joint, float size = 1f)
    {
        if (parent < 0 || !d.TryPlaceOnLimb(kind, parent, joint, 0)) { GD.Print($"SHOT could not branch {kind}"); return -1; }
        d.SetPartSize(d.Parts.Count - 1, size);
        return d.Parts.Count - 1;
    }

    private static void Joints(CreatureDesign d, int index, params Vector3[] joints)
    {
        if (index < 0) return;
        for (int i = 0; i < joints.Length; i++) d.SetLimbJoint(index, i, joints[i]);
    }

    private static void Remove(CreatureDesign d, CreaturePartKind kind)
    {
        for (int i = d.Parts.Count - 1; i >= 0; i--)
        {
            if (kind == d.Parts[i].Kind) d.RemovePart(i);
        }
    }

    private static void Spray(CreatureDesign d, Vector3 point, float radius, Color color)
    {
        int stroke = d.NewStroke();
        d.AddSplat(point, radius, color, stroke);
        d.AddSplat(new Vector3(point.X, point.Y, -point.Z), radius, color, stroke);
    }

    // ---------------------------------------------------------------------------
    // The models
    // ---------------------------------------------------------------------------

    // Squid: a pointed mantle with fins at the back, huge eyes, four tentacles that fork into eight
    private static CreatureDesign Squid()
    {
        CreatureDesign d = CreatureDesign.Starter(CreatureType.Fish);
        Spine(d, 0.25f, 0.55f, 0.78f, 0.9f, 0.92f, 0.88f, 0.8f);
        Remove(d, CreaturePartKind.CaudalFin);
        Remove(d, CreaturePartKind.Eye);
        Place(d, CreaturePartKind.Eye, 0.8f, 2.6f);
        Place(d, CreaturePartKind.PectoralFin, 0.15f, 1.4f);
        float[] spots = { 0.72f, 0.9f };
        for (int i = 0; i < spots.Length; i++)
        {
            int   arm  = Place(d, CreaturePartKind.Tentacle, spots[i], 1.3f);
            float down = 0 == i ? 1f : 0.6f;
            Joints(d, arm,
                new Vector3(0.18f, -0.22f * down, 0.22f),
                new Vector3(0.45f, -0.5f * down, 0.3f),
                new Vector3(0.75f, -0.62f * down, 0.22f),
                new Vector3(0.95f, -0.45f * down, 0.08f));
            int fork = Branch(d, CreaturePartKind.Tentacle, arm, 1, 1f);
            Joints(d, fork,
                new Vector3(0.1f, -0.25f, 0.12f),
                new Vector3(0.22f, -0.45f, 0.22f),
                new Vector3(0.42f, -0.55f, 0.3f),
                new Vector3(0.58f, -0.42f, 0.4f));
        }
        d.SetPattern(CreaturePattern.Spots, new Color(0.45f, 0.12f, 0.4f));
        return d;
    }

    // Crab: short and wide, stalk eyes, two pairs of talon feet, claw paws with a second claw on a branch
    private static CreatureDesign Crab()
    {
        CreatureDesign d = CreatureDesign.Starter(CreatureType.Fish);
        Spine(d, 0.55f, 0.9f, 1f, 0.95f, 0.8f);
        Remove(d, CreaturePartKind.CaudalFin);
        Remove(d, CreaturePartKind.Eye);
        Place(d, CreaturePartKind.StalkEye, 0.85f, 1.4f);
        Place(d, CreaturePartKind.ClawLegs, 0.2f);
        Place(d, CreaturePartKind.ClawLegs, 0.42f);
        int arm = Place(d, CreaturePartKind.Claws, 0.7f, 1.2f);
        Joints(d, arm, new Vector3(0.15f, 0.05f, 0.3f), new Vector3(0.38f, 0.18f, 0.45f), new Vector3(0.62f, 0.12f, 0.45f));
        int second = Branch(d, CreaturePartKind.Claws, arm, 1, 0.8f);
        Joints(d, second, new Vector3(0.12f, 0.15f, 0.08f), new Vector3(0.28f, 0.28f, 0.12f), new Vector3(0.45f, 0.3f, 0.14f));
        d.SetPattern(CreaturePattern.Saddle, new Color(0.55f, 0.12f, 0.1f));
        return d;
    }

    // Giraffe sausage: a long body, a neck pulled up high, four paws, patches and stalk eyes
    private static CreatureDesign GiraffeSausage()
    {
        CreatureDesign d = CreatureDesign.Starter(CreatureType.Fish);
        Spine(d, 0.35f, 0.6f, 0.7f, 0.7f, 0.7f, 0.65f, 0.4f, 0.32f, 0.3f, 0.42f);
        for (int i = 6; i < 10; i++) d.SetLift(i, (i - 5) * 0.3f);
        Remove(d, CreaturePartKind.Eye);
        Place(d, CreaturePartKind.StalkEye, 0.92f, 1.2f);
        Place(d, CreaturePartKind.Legs, 0.12f);
        Place(d, CreaturePartKind.Legs, 0.5f);
        Place(d, CreaturePartKind.DorsalFin, 0.3f, 0.8f);
        d.SetPattern(CreaturePattern.Patches, new Color(0.55f, 0.3f, 0.12f));
        return d;
    }

    // Muscle whale: hands stretched up like a body builder, a big brain, a grin and sprayed racing stripes
    private static CreatureDesign MuscleWhale()
    {
        CreatureDesign d = CreatureDesign.Starter(CreatureType.MarineMammal);
        Spine(d, 0.28f, 0.45f, 0.7f, 0.95f, 1f, 0.95f, 0.75f);
        int arms = Place(d, CreaturePartKind.Hands, 0.62f, 1.3f);
        Joints(d, arms, new Vector3(0.05f, 0.1f, 0.3f), new Vector3(0.08f, 0.42f, 0.5f), new Vector3(0.05f, 0.72f, 0.4f));
        Place(d, CreaturePartKind.BigBrain, 1f, 1.2f);
        Place(d, CreaturePartKind.Jaw, 1f, 1.6f);
        Place(d, CreaturePartKind.DorsalFin, 0.4f);
        d.SetPattern(CreaturePattern.Saddle, new Color(0.15f, 0.2f, 0.4f));
        for (int i = 0; i < 9; i++)
        {
            float x = Mathf.Lerp(-0.9f, 0.7f, i / 8f);
            Spray(d, new Vector3(x, d.LiftAt(x), d.FlankAt(x) * 0.98f), 0.09f, new Color(1f, 0.85f, 0.25f));
        }
        return d;
    }
}
