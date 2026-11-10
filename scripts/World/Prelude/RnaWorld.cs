// NOTE:
//   Playable prelude before the protocell: the player is a free RNA strand in the
//   primordial soup. The ribozyme head swims like a snake, nucleotides (A, U, G, C)
//   drift around. The strand copies a random template: swallowing the base that pairs
//   with the next template base (A-U, G-C) adds a link, a wrong base still sticks but
//   as a mutation that does not count for the goal: it rolls a trait (Evolution/Mutation.cs,
//   good ones white, bad ones red) that the cell keeps, plus evolution points. A bitten
//   off mutation link takes its trait along. The ring around the head glows in the wanted color.
//   Rival strands swim around and eat nucleotides too. A head biting into a chain breaks
//   it there, the broken off links float away as nucleotides: rivals bite the player,
//   the player bites rivals. GOAL_LINKS paired links call a lipid vesicle, swimming into
//   it ends the prelude, Main then plays the Intro there.
//   Runs while the game is paused (like the cutscenes), Tab skips it.
//
//   var prelude = new RnaWorld { Camera = camera, Center = spawn, Size = player.BodySize() };
//   AddChild(prelude);
//   prelude.CameraMoved += (focus, distance) => backdrop.Follow(focus, size, ViewRadiusAt(distance));
//   prelude.Finished    += (vesicle, mutations) => StartIntro(vesicle, mutations);

//
//   Chapters:
//     SETTINGS        Inputs from Main, signals and the tuning values.
//     BUILD           Player strand, rivals, the first nucleotides and the HUD.
//     CHAIN           Pairing rules, attaching and breaking links, used by the game loop and the smoke test.
//     PLAY            Camera, steering, rivals, bites, nucleotides, the vesicle.
//     STRANDS         Building, moving, growing and breaking a strand (player and rivals alike).
//     HELPERS         Nucleotides, materials and labels.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

// ===========================================================================
// ==============================   SETTINGS   ===============================
// ===========================================================================
//
//   RnaBase      The four bases of RNA, U instead of the T of DNA.
//   Camera       (Required) The world camera, flown down from the menu pose and then following the head.
//   Center       (Required) Spawn point of the player on the plane, the strand starts here.
//   Size         (Required) Body size of the player (Player.BodySize()), everything scales with it.
//   CameraMoved  Every frame with the camera focus and distance, Main moves the sea floor with it.
//   Finished     The strand swam into the vesicle (or Tab skipped): its point and the mutation count.
//

public enum RnaBase { A, U, G, C }

public partial class RnaWorld : Node3D
{
    [Signal] public delegate void CameraMovedEventHandler(Vector2 focus, float distance);
    [Signal] public delegate void FinishedEventHandler(Vector2 vesicle, int[] mutations);

    public Camera3D Camera { get; set; }
    public Vector2 Center { get; set; }
    public float Size { get; set; } = 14f;

    // Goal and chain, lengths in body sizes
    public const int GOAL_LINKS = 12;
    private const int START_LINKS = 2;
    private const float HEAD_RADIUS = 0.3f;
    private const float LINK_RADIUS = 0.13f;
    public const float LINK_SPACING = 0.34f;

    // Steering, speeds in body sizes per second
    private const float SWIM_SPEED = 2.4f;
    private const float IDLE_SPEED = 0.6f;          // without input the strand keeps drifting forward
    private const float TURN_SPEED = 3.6f;          // radians per second
    private const float START_GUARD = 1.4f;         // seconds of camera flight before the steering starts

    // Rivals: slower than the player, they wander, go for nucleotides nearby and flee the player head
    private const int RIVALS = 4;
    private const int RIVAL_MIN_LINKS = 3;
    private const int RIVAL_MAX_LINKS = 10;
    private const float RIVAL_SPEED = 1.5f;
    private const float RIVAL_TURN = 2.2f;
    private const float RIVAL_SEEK_RANGE = 3f;
    private const float RIVAL_FLEE_RANGE = 2.5f;    // rivals turn away from the player head within this range
    private const float RIVAL_SPAWN_MIN = 6f;
    private const float RIVAL_SPAWN_MAX = 10f;
    private const float RIVAL_DESPAWN = 14f;
    private const float BITE_COOLDOWN = 1f;         // seconds a head rests after a bite

    // Nucleotides
    private const int NUCLEOTIDES = 34;
    private const float NUCLEOTIDE_RADIUS = 0.15f;
    private const float SPAWN_MIN = 3f;
    private const float SPAWN_MAX = 9f;
    private const float DESPAWN_DISTANCE = 12f;
    private const float WANTED_CHANCE = 0.35f;      // share of new nucleotides that pair with the next template base
    private const float DRIFT_SPEED = 0.25f;

    // Vesicle, camera and HUD
    private const float VESICLE_RADIUS = 1.3f;
    private const float VESICLE_DISTANCE = 5f;
    private const float FLY_SECONDS = 1.6f;
    private const float CAM_TILT = 0.5f;            // tilt from vertical, like Main
    private const float CAM_DISTANCE = 7f;
    private const float CAM_DISTANCE_PER_LINK = 0.18f;
    private const int UPCOMING_SHOWN = 2;           // template bases shown after the wanted one
    private const float BANNER_HOLD = 3.5f;
    private const float BANNER_FADE = 1.2f;

    // Same hues as CellVisual3D.DNA_BASES, U takes the blue of T
    private static readonly Color[] BASE_COLORS =
    {
        new(0.98f, 0.85f, 0.35f),   // A
        new(0.45f, 0.8f, 1f),       // U
        new(0.55f, 0.95f, 0.55f),   // G
        new(1f, 0.6f, 0.35f),       // C
    };
    private static readonly Color BACKBONE_COLOR = new(0.95f, 0.55f, 0.75f);
    private static readonly Color RIVAL_COLOR = new(0.6f, 0.5f, 0.95f);
    private static readonly Color GOOD_MUTATION_COLOR = new(0.95f, 1f, 1f);
    private static readonly Color BAD_MUTATION_COLOR = new(1f, 0.25f, 0.3f);
    private static readonly Color VESICLE_COLOR = new(1f, 0.9f, 0.55f);
    private static readonly Shader MEMBRANE_SHADER = GD.Load<Shader>("res://shaders/membrane.gdshader");

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   _Ready      Keeps running while paused, builds the player strand, rivals, nucleotides and the HUD.
    //   BuildHud    Banner, goal, the wanted base and mutations, all via Tr().
    //

    private sealed class Link
    {
        public RnaBase Kind;
        public MutationKind? Mutation;     // null: paired, else the trait the wrong base rolled
        public bool IsMutation => null != Mutation;
        public Vector2 Position;
        public MeshInstance3D Bead;
        public MeshInstance3D Bond;
    }

    private sealed class Strand
    {
        public bool IsRival;
        public Vector2 Position;
        public float Heading;
        public float Wander;            // rivals: drifting target heading
        public float BiteCooldown;
        public Node3D Head;
        public MeshInstance3D Ring;     // player only, glows in the wanted color
        public StandardMaterial3D Backbone;
        public readonly List<Link> Links = new();
    }

    private sealed class Nucleotide
    {
        public RnaBase Kind;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Phase;
        public Node3D Node;
    }

    private readonly List<Nucleotide> _nucleotides = new();
    private readonly List<Strand> _rivals = new();
    private readonly List<RnaBase> _template = new();
    private readonly StandardMaterial3D[] _baseMaterials = new StandardMaterial3D[4];
    private StandardMaterial3D _goodMutationMaterial;
    private StandardMaterial3D _badMutationMaterial;
    private StandardMaterial3D _ringMaterial;
    private Strand _player;
    private Transform3D _cameraStart;
    private Vector3 _camFocus;
    private float _time;
    private MeshInstance3D _vesicle;
    private ShaderMaterial _vesicleMaterial;
    private Vector2 _vesiclePosition;
    private float _vesicleTime;
    private bool _isFinished;

    public override void _Ready()
    {
        ProcessMode  = ProcessModeEnum.Always;
        _cameraStart = Camera.GlobalTransform;
        _camFocus    = Plane3D.To3D(Center);

        for (int kind = 0; kind < BASE_COLORS.Length; kind++)
        {
            _baseMaterials[kind] = Glow(BASE_COLORS[kind], 0.5f);
        }
        _goodMutationMaterial = Glow(GOOD_MUTATION_COLOR, 1.6f);
        _badMutationMaterial  = Glow(BAD_MUTATION_COLOR, 1.2f);
        _ringMaterial     = Glow(BASE_COLORS[(int)RnaBase.A], 1.4f);

        // The player swims up the screen
        _player = NewStrand(Center, -Mathf.Pi * 0.5f, isRival: false);
        for (int i = 0; i < START_LINKS; i++)
        {
            Attach(Wanted);
        }
        for (int i = 0; i < RIVALS; i++)
        {
            SpawnRival(Rand.Range(RIVAL_SPAWN_MIN, RIVAL_SPAWN_MAX));
        }
        for (int i = 0; i < NUCLEOTIDES; i++)
        {
            SpawnNucleotide(Rand.Range(SPAWN_MIN, SPAWN_MAX));
        }
        BuildHud();
        UpdateFrame(0f);
        Log.Verbose("RnaWorld", $"Prelude started at {Center}, goal {GOAL_LINKS} paired links, {RIVALS} rivals.");
    }

    private Label _banner;
    private Label _bannerSub;
    private Label _goalLabel;
    private Label _wantedLabel;
    private Label _wantedBase;
    private readonly Label[] _upcoming = new Label[UPCOMING_SHOWN];
    private Label _mutationLabel;
    private VBoxContainer _mutationList;

    private void BuildHud()
    {
        var hud = new CanvasLayer { Layer = 10 };
        AddChild(hud);

        // Banner in the upper middle, fades out after BANNER_HOLD
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        box.OffsetTop = 110;
        hud.AddChild(box);
        _banner    = NewLabel(64);
        GameFonts.UseDisplay(_banner);
        _bannerSub = NewLabel(20);
        foreach (Label label in new[] { _banner, _bannerSub })
        {
            label.HorizontalAlignment = HorizontalAlignment.Center;
            box.AddChild(label);
        }
        Tween fade = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
        fade.TweenProperty(_banner, "modulate:a", 0f, BANNER_FADE).SetDelay(BANNER_HOLD);
        fade.TweenProperty(_bannerSub, "modulate:a", 0f, BANNER_FADE).SetDelay(BANNER_HOLD);

        // Status top left: goal, wanted base with the next ones, mutations
        var corner = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        corner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        corner.AddThemeConstantOverride("margin_left", 24);
        corner.AddThemeConstantOverride("margin_top", 20);
        hud.AddChild(corner);
        var status = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        corner.AddChild(status);
        _goalLabel = NewLabel(22);
        status.AddChild(_goalLabel);
        var wantedRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        status.AddChild(wantedRow);
        _wantedLabel = NewLabel(20);
        _wantedBase  = NewLabel(30);
        wantedRow.AddChild(_wantedLabel);
        wantedRow.AddChild(_wantedBase);
        for (int i = 0; i < _upcoming.Length; i++)
        {
            _upcoming[i] = NewLabel(20);
            wantedRow.AddChild(_upcoming[i]);
        }
        _mutationLabel = NewLabel(18);
        status.AddChild(_mutationLabel);
        _mutationList = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _mutationList.AddThemeConstantOverride("separation", 0);
        status.AddChild(_mutationList);

        ApplyTexts();
    }

    public override void _Notification(int what)
    {
        if (NotificationTranslationChanged == what && null != _goalLabel)
        {
            ApplyTexts();
        }
    }

    private void ApplyTexts()
    {
        _banner.Text    = Tr("PRELUDE_TITLE");
        _bannerSub.Text = Tr("PRELUDE_TEXT");
        UpdateHud();
    }

    // ===========================================================================
    // ================================   CHAIN   ================================
    // ===========================================================================
    //
    //   Pair             The base that pairs with another one: A-U, G-C.
    //   TemplateAt       Base of the random template at a position, grows on demand.
    //   WantedAt         The base that pairs with the template at a position.
    //   Wanted           The base the next link needs.
    //   PairedCount      Links that paired correctly, they count for the goal.
    //   MutationCount    Links that stuck without pairing.
    //   Mutations        The traits of those links (Evolution/Mutation.cs).
    //   LinkCount        All links behind the head.
    //   IsGoalReached    Enough paired links, the vesicle is called.
    //   Attach           Adds a link at the end, a paired one or a mutation.
    //   BreakAt          A bite: breaks the chain at the first link inside a circle.
    //   Skip             Ends the prelude at once (Tab, smoke test).
    //

    /// <summary>The base that pairs with another one: A-U, G-C.</summary>
    /// <example><code>RnaWorld.Pair(RnaBase.G)  // RnaBase.C</code></example>
    public static RnaBase Pair(RnaBase kind)
    {
        return kind switch
        {
            RnaBase.A => RnaBase.U,
            RnaBase.U => RnaBase.A,
            RnaBase.G => RnaBase.C,
            _         => RnaBase.G,
        };
    }

    /// <summary>Base of the random template at a position, the template grows as far as it is asked.</summary>
    private RnaBase TemplateAt(int index)
    {
        while (_template.Count <= index)
        {
            _template.Add((RnaBase)Rand.Index(BASE_COLORS.Length));
        }
        return _template[index];
    }

    /// <summary>The base that pairs with the template at a position, a broken chain asks again from there.</summary>
    public RnaBase WantedAt(int index) => Pair(TemplateAt(index));

    /// <summary>The base the next link needs, every link (mutations too) takes one template position.</summary>
    public RnaBase Wanted => WantedAt(_player.Links.Count);

    public int PairedCount { get; private set; }
    public int MutationCount { get; private set; }
    public int LinkCount => _player.Links.Count;
    public bool IsGoalReached => PairedCount >= GOAL_LINKS;

    /// <summary>The traits of the mutation links on the player chain, a bitten off link takes its trait along.</summary>
    public IReadOnlyList<MutationKind> Mutations => _player.Links.Where(link => link.IsMutation).Select(link => link.Mutation.Value).ToList();

    /// <summary>Adds a link at the end of the player chain, paired if it fits Wanted, else as a mutation with a rolled trait.</summary>
    /// <param name="kind">(Required) The swallowed base.</param>
    /// <returns>True when it paired.</returns>
    /// <example><code>prelude.Attach(prelude.Wanted)  // true</code></example>
    public bool Attach(RnaBase kind)
    {
        bool          isPaired = kind == Wanted;
        MutationKind? mutation = isPaired ? null : Mutation.Roll();
        AddLink(_player, kind, mutation);
        RecountLinks();
        Log.Verbose("RnaWorld", $"{kind} {(isPaired ? "paired" : $"stuck as mutation {mutation}")}, {PairedCount}/{GOAL_LINKS} paired, {MutationCount} mutations.");
        if (IsGoalReached && null == _vesicle)
        {
            CallVesicle();
        }
        return isPaired;
    }

    /// <summary>A bite into the player chain: breaks it at the first link inside the circle, the rest floats away.</summary>
    /// <param name="point">(Required) Center of the bite on the plane.</param>
    /// <param name="radius">(Required) Reach of the bite on the plane.</param>
    /// <returns>The number of lost links.</returns>
    public int BreakAt(Vector2 point, float radius)
    {
        int cut = FirstLinkAt(_player, point, radius);
        if (cut < 0)
        {
            return 0;
        }
        int lost = BreakStrand(_player, cut, point);
        RecountLinks();
        Log.Verbose("RnaWorld", $"Bitten at link {cut}, {lost} lost, {PairedCount}/{GOAL_LINKS} paired.");
        return lost;
    }

    /// <summary>Ends the prelude at once with a vesicle in front of the head.</summary>
    public void Skip()
    {
        if (null == _vesicle)
        {
            CallVesicle();
        }
        Finish();
    }

    private void RecountLinks()
    {
        PairedCount   = 0;
        MutationCount = 0;
        foreach (Link link in _player.Links)
        {
            if (link.IsMutation)
            {
                MutationCount++;
            }
            else
            {
                PairedCount++;
            }
        }
        UpdateHud();
    }

    // ===========================================================================
    // ================================   PLAY   =================================
    // ===========================================================================
    //
    //   1. Camera        Flies down from the menu pose, then follows the head and zooms out with the chain.
    //   2. Steering      Keys or the held mouse turn the player head, it always drifts forward.
    //   3. Rivals        Flee the player head, go for a nucleotide nearby or wander, far ones respawn.
    //   4. Bites         A head biting into another chain breaks it there.
    //   5. Nucleotides   Drift and bob, heads swallow them, far ones respawn around the view.
    //   6. Vesicle       Wobbles, swimming into it ends the prelude.
    //
    //   Tab skips the prelude (_UnhandledInput).
    //

    public override void _Process(double delta)
    {
        if (_isFinished)
        {
            return;
        }
        _time += (float)delta;
        UpdateFrame((float)delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_isFinished || @event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Tab })
        {
            return;
        }
        GetViewport().SetInputAsHandled();
        Log.Verbose("RnaWorld", $"Skipped at {_time:F1} s.");
        Skip();
    }

    private void UpdateFrame(float dt)
    {
        // ---------------------------------------------------------------------------
        // Camera
        //   Flies down from the menu pose, then follows the head and zooms out with the chain.
        // ---------------------------------------------------------------------------

        float   distance = Size * (CAM_DISTANCE + CAM_DISTANCE_PER_LINK * _player.Links.Count);
        Vector3 focus    = Plane3D.To3D(_player.Position);
        _camFocus = dt <= 0f ? focus : _camFocus.Lerp(focus, Mathf.Clamp(dt * 3f, 0f, 1f));
        var follow = new Transform3D(Basis.Identity, _camFocus + new Vector3(0f, Mathf.Cos(CAM_TILT), Mathf.Sin(CAM_TILT)) * distance);
        follow = follow.LookingAt(_camFocus, Vector3.Up);
        float flight = Ease.InOut(Ease.Window(_time, 0f, FLY_SECONDS));
        Camera.GlobalTransform = _cameraStart.InterpolateWith(follow, flight);
        Camera.Near = distance * 0.05f;
        Camera.Far  = Mathf.Max(Camera.Far, distance * 8f);
        EmitSignal(SignalName.CameraMoved, new Vector2(_camFocus.X, _camFocus.Z), Camera.GlobalPosition.DistanceTo(_camFocus));

        // ---------------------------------------------------------------------------
        // Steering
        //   Keys or the held mouse turn the player head, it always drifts forward.
        // ---------------------------------------------------------------------------

        Vector2 input = Vector2.Zero;
        if (_time >= START_GUARD)
        {
            if (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up))    input.Y -= 1f;
            if (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down))  input.Y += 1f;
            if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left))  input.X -= 1f;
            if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right)) input.X += 1f;
            if (Input.IsMouseButtonPressed(MouseButton.Left))
            {
                input = Plane3D.MouseOnPlane(GetViewport(), _player.Position) - _player.Position;
            }
        }
        bool isSteered = input.LengthSquared() > 0.0001f;
        MoveStrand(_player, isSteered ? input.Angle() : _player.Heading, isSteered ? SWIM_SPEED : IDLE_SPEED, TURN_SPEED, dt);
        _player.Ring.Rotation = new Vector3(0.6f * Mathf.Sin(_time * 1.3f), _time * 3f, 0.6f);

        // ---------------------------------------------------------------------------
        // Rivals
        //   Flee the player head, go for a nucleotide nearby or wander, far ones respawn.
        // ---------------------------------------------------------------------------

        for (int i = _rivals.Count - 1; i >= 0; i--)
        {
            Strand rival = _rivals[i];
            if (rival.Position.DistanceTo(_player.Position) > RIVAL_DESPAWN * Size)
            {
                FreeStrand(rival);
                _rivals.RemoveAt(i);
                continue;
            }

            // > The player head within RIVAL_FLEE_RANGE: swim away from it
            // > A nucleotide within RIVAL_SEEK_RANGE: swim to the nearest one
            // > Else: wander, the target heading drifts slowly
            Vector2    fromPlayer = rival.Position - _player.Position;
            Nucleotide prey       = NearestNucleotide(rival.Position, RIVAL_SEEK_RANGE * Size);
            rival.Wander = Mathf.Clamp(rival.Wander + Rand.Range(-1f, 1f) * dt, -0.6f, 0.6f);
            float target = rival.Heading + rival.Wander;
            if (fromPlayer.Length() < RIVAL_FLEE_RANGE * Size)
            {
                target = fromPlayer.Angle();
            }
            else if (null != prey)
            {
                target = (prey.Position - rival.Position).Angle();
            }
            MoveStrand(rival, target, RIVAL_SPEED, RIVAL_TURN, dt);
        }
        while (_rivals.Count < RIVALS)
        {
            SpawnRival(Rand.Range(RIVAL_SPAWN_MAX * 0.9f, RIVAL_SPAWN_MAX));
        }

        // ---------------------------------------------------------------------------
        // Bites
        //   A head biting into another chain breaks it there.
        // ---------------------------------------------------------------------------

        if (_time >= START_GUARD)
        {
            float bite = HEAD_RADIUS * Size;
            foreach (Strand rival in _rivals)
            {
                rival.BiteCooldown = Mathf.Max(0f, rival.BiteCooldown - dt);

                // The rival bites the player chain
                if (0f == rival.BiteCooldown && BreakAt(rival.Position, bite) > 0)
                {
                    rival.BiteCooldown = BITE_COOLDOWN;
                }

                // The player bites the rival chain
                int cut = FirstLinkAt(rival, _player.Position, bite);
                if (cut >= 0)
                {
                    int lost = BreakStrand(rival, cut, _player.Position);
                    Log.Verbose("RnaWorld", $"Player bit a rival at link {cut}, {lost} links float free.");
                }
            }
        }

        // ---------------------------------------------------------------------------
        // Nucleotides
        //   Drift and bob, heads swallow them, far ones respawn around the view.
        // ---------------------------------------------------------------------------

        float reach = (HEAD_RADIUS + NUCLEOTIDE_RADIUS) * Size;
        for (int i = _nucleotides.Count - 1; i >= 0; i--)
        {
            Nucleotide nucleotide = _nucleotides[i];
            nucleotide.Velocity  = nucleotide.Velocity.Lerp(Vector2.FromAngle(nucleotide.Phase + _time * 0.3f) * DRIFT_SPEED * Size, Mathf.Clamp(dt * 0.8f, 0f, 1f));
            nucleotide.Position += nucleotide.Velocity * dt;
            nucleotide.Node.Position = Plane3D.To3D(nucleotide.Position, Mathf.Sin(_time * 1.5f + nucleotide.Phase) * 0.06f * Size);
            nucleotide.Node.Rotation = new Vector3(0f, _time + nucleotide.Phase, 0f);

            if (_time >= START_GUARD && nucleotide.Position.DistanceTo(_player.Position) <= reach)
            {
                Attach(nucleotide.Kind);
                RemoveNucleotide(i);
                continue;
            }
            Strand eater = _rivals.Find(rival => rival.Links.Count < RIVAL_MAX_LINKS && nucleotide.Position.DistanceTo(rival.Position) <= reach);
            if (null != eater)
            {
                AddLink(eater, nucleotide.Kind, null);
                RemoveNucleotide(i);
                continue;
            }
            if (nucleotide.Position.DistanceTo(_player.Position) > DESPAWN_DISTANCE * Size)
            {
                RemoveNucleotide(i);
            }
        }
        while (_nucleotides.Count < NUCLEOTIDES)
        {
            SpawnNucleotide(Rand.Range(SPAWN_MAX * 0.8f, SPAWN_MAX));
        }

        // ---------------------------------------------------------------------------
        // Vesicle
        //   Wobbles, swimming into it ends the prelude.
        // ---------------------------------------------------------------------------

        if (null == _vesicle)
        {
            return;
        }
        float grow = Ease.BackOut(Ease.Window(_time, _vesicleTime, _vesicleTime + 0.8f));
        float size = VESICLE_RADIUS * Size * Mathf.Max(grow, 0.001f) * (1f + 0.05f * Mathf.Sin(_time * 3f));
        _vesicle.Scale = new Vector3(size, size * CellVisual3D.FLATTEN, size);
        _vesicleMaterial.SetShaderParameter("flash", 0.25f + 0.25f * Mathf.Sin(_time * 4f));
        if (_player.Position.DistanceTo(_vesiclePosition) <= VESICLE_RADIUS * Size)
        {
            Finish();
        }
    }

    private void CallVesicle()
    {
        _vesiclePosition = _player.Position + Vector2.FromAngle(_player.Heading + Rand.Range(-1f, 1f)) * VESICLE_DISTANCE * Size;
        _vesicleTime     = _time;
        _vesicleMaterial = new ShaderMaterial { Shader = MEMBRANE_SHADER };
        _vesicleMaterial.SetShaderParameter("body_color", new Color(VESICLE_COLOR, 0.6f));
        _vesicleMaterial.SetShaderParameter("seed", Rand.Range(0f, 100f));
        _vesicleMaterial.SetShaderParameter("core_alpha", 0.15f);
        _vesicle = CellVisual3D.AddMesh(this, CellVisual3D.SphereMesh(), _vesicleMaterial, Plane3D.To3D(_vesiclePosition), Vector3.One * 0.001f);
        _vesicle.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        UpdateHud();
        Log.Verbose("RnaWorld", $"Goal reached -> vesicle at {_vesiclePosition}.");
    }

    private void Finish()
    {
        if (_isFinished)
        {
            return;
        }
        _isFinished = true;
        Log.Verbose("RnaWorld", $"Strand entered the vesicle with {PairedCount} paired links and {MutationCount} mutations.");
        EmitSignal(SignalName.Finished, _vesiclePosition, Mutations.Select(kind => (int)kind).ToArray());
    }

    private void UpdateHud()
    {
        RnaBase wanted = Wanted;
        _ringMaterial.AlbedoColor = BASE_COLORS[(int)wanted];
        _ringMaterial.Emission    = BASE_COLORS[(int)wanted];
        if (null == _goalLabel)
        {
            return;
        }

        // The wanted base big in its color, the next ones small and dim behind it
        _goalLabel.Text     = IsGoalReached ? Tr("PRELUDE_GOAL_VESICLE") : string.Format(Tr("PRELUDE_GOAL"), PairedCount, GOAL_LINKS);
        _wantedLabel.Text   = Tr("PRELUDE_WANTED");
        _wantedBase.Text    = wanted.ToString();
        _wantedBase.AddThemeColorOverride("font_color", BASE_COLORS[(int)wanted]);
        for (int i = 0; i < _upcoming.Length; i++)
        {
            RnaBase next = WantedAt(_player.Links.Count + 1 + i);
            _upcoming[i].Text = next.ToString();
            _upcoming[i].AddThemeColorOverride("font_color", new Color(BASE_COLORS[(int)next], 0.55f - 0.15f * i));
        }
        _mutationLabel.Text    = string.Format(Tr("PRELUDE_MUTATIONS"), MutationCount, MutationCount * Mutation.EP_PER_MUTATION);
        _mutationLabel.Visible = MutationCount > 0;

        // One line per mutation on the chain: name and effect, green helps, red hurts
        foreach (Node line in _mutationList.GetChildren())
        {
            line.QueueFree();
        }
        foreach (MutationKind kind in Mutations)
        {
            Mutation mutation = Mutation.Get(kind);
            Label    line     = NewLabel(16);
            line.Text = $"{Tr(mutation.NameKey)}  {Tr(mutation.DescKey)}";
            line.AddThemeColorOverride("font_color", mutation.IsGood ? new Color(0.6f, 1f, 0.65f) : new Color(1f, 0.5f, 0.5f));
            _mutationList.AddChild(line);
        }
    }

    // ===========================================================================
    // ===============================   STRANDS   ===============================
    // ===========================================================================
    //
    //   NewStrand     A head at a point: pink with the wanted-color ring for the player, violet for rivals.
    //   SpawnRival    A rival with a few links in a ring around the player.
    //   MaterialOf    Bead material: base color, white for a good mutation, red for a bad one.
    //   AddLink       A bead and its bond at the end of a chain, a mutation link carries its trait.
    //   MoveStrand    Turns the head towards a heading, swims, every link follows the one in front.
    //   FirstLinkAt   Index of the first link inside a circle, -1 for none.
    //   BreakStrand   Frees the links from an index on, they float away as nucleotides.
    //   FreeStrand    Frees head and links of a strand.
    //

    private Strand NewStrand(Vector2 position, float heading, bool isRival)
    {
        Color color  = isRival ? RIVAL_COLOR : BACKBONE_COLOR;
        var   strand = new Strand
        {
            IsRival  = isRival,
            Position = position,
            Heading  = heading,
            Head     = new Node3D(),
            Backbone = Glow(color, 0.4f),
        };
        AddChild(strand.Head);
        CellVisual3D.AddMesh(strand.Head, CellVisual3D.SphereMesh(), Glow(color, 0.35f), Vector3.Zero, Vector3.One * HEAD_RADIUS * Size);
        if (!isRival)
        {
            strand.Ring = CellVisual3D.AddMesh(strand.Head, CellVisual3D.TorusMesh(), _ringMaterial, Vector3.Zero, Vector3.One * HEAD_RADIUS * 1.35f * Size);
        }

        // Two dots in front: the strand looks where it swims
        foreach (float side in new[] { -1f, 1f })
        {
            var eye = new Vector3(HEAD_RADIUS * 0.75f, HEAD_RADIUS * 0.5f, side * HEAD_RADIUS * 0.3f) * Size;
            CellVisual3D.AddMesh(strand.Head, CellVisual3D.SphereMesh(true), CellVisual3D.Material(new Color(0.08f, 0.05f, 0.1f)), eye, Vector3.One * HEAD_RADIUS * 0.2f * Size);
        }
        strand.Head.Position = Plane3D.To3D(position);
        return strand;
    }

    private void SpawnRival(float distance)
    {
        Vector2 position = _player.Position + Vector2.FromAngle(Rand.Angle()) * distance * Size;
        Strand  rival    = NewStrand(position, Rand.Angle(), isRival: true);
        int     links    = RIVAL_MIN_LINKS + Rand.Index(RIVAL_MAX_LINKS - RIVAL_MIN_LINKS + 1);
        for (int i = 0; i < links; i++)
        {
            AddLink(rival, (RnaBase)Rand.Index(BASE_COLORS.Length), null);
        }
        _rivals.Add(rival);
    }

    /// <summary>Bead material of a link: its base color, white for a good mutation, red for a bad one.</summary>
    private StandardMaterial3D MaterialOf(RnaBase kind, MutationKind? mutation)
    {
        if (null == mutation)
        {
            return _baseMaterials[(int)kind];
        }
        return Mutation.Get(mutation.Value).IsGood ? _goodMutationMaterial : _badMutationMaterial;
    }

    private void AddLink(Strand strand, RnaBase kind, MutationKind? mutation)
    {
        Vector2 previous = 0 == strand.Links.Count ? strand.Position : strand.Links[^1].Position;
        var link = new Link
        {
            Kind       = kind,
            Mutation   = mutation,
            Position   = previous - Vector2.FromAngle(strand.Heading) * LINK_SPACING * Size,
            Bead       = CellVisual3D.AddMesh(this, CellVisual3D.SphereMesh(true), MaterialOf(kind, mutation), Vector3.Zero, Vector3.One * LINK_RADIUS * Size),
            Bond       = CellVisual3D.AddMesh(this, CellVisual3D.RodMesh(), strand.Backbone, Vector3.Zero, Vector3.One),
        };
        link.Bead.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        link.Bond.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        link.Bead.Position   = Plane3D.To3D(link.Position);
        strand.Links.Add(link);
    }

    private void MoveStrand(Strand strand, float targetHeading, float speed, float turn, float dt)
    {
        // Head: turns towards the target heading, then swims forward
        strand.Heading   = Mathf.RotateToward(strand.Heading, targetHeading, turn * dt);
        strand.Position += Vector2.FromAngle(strand.Heading) * speed * Size * dt;
        strand.Head.Position = Plane3D.To3D(strand.Position, Mathf.Sin(_time * 2f + strand.Wander) * 0.04f * Size);
        strand.Head.Rotation = new Vector3(0f, Plane3D.FacingToYaw(strand.Heading), 0f);

        // Chain: every link keeps LINK_SPACING to the one in front, with a soft wave
        Vector3 front3D = strand.Head.Position;
        Vector2 front   = strand.Position;
        for (int i = 0; i < strand.Links.Count; i++)
        {
            Link    link = strand.Links[i];
            Vector2 back = link.Position - front;
            link.Position = front + (back.IsZeroApprox() ? -Vector2.FromAngle(strand.Heading) : back.Normalized()) * LINK_SPACING * Size;

            Vector3 at = Plane3D.To3D(link.Position, Mathf.Sin(_time * 4f - i * 0.7f) * 0.05f * Size);
            link.Bead.Position  = at;
            link.Bond.Transform = Intro.Rod(front3D, at, 0.035f * Size);
            front   = link.Position;
            front3D = at;
        }
    }

    private int FirstLinkAt(Strand strand, Vector2 point, float radius)
    {
        for (int i = 0; i < strand.Links.Count; i++)
        {
            if (strand.Links[i].Position.DistanceTo(point) <= radius + LINK_RADIUS * Size)
            {
                return i;
            }
        }
        return -1;
    }

    private int BreakStrand(Strand strand, int cut, Vector2 from)
    {
        int lost = strand.Links.Count - cut;
        for (int i = strand.Links.Count - 1; i >= cut; i--)
        {
            Link    link = strand.Links[i];
            Vector2 away = (link.Position - from).Normalized();
            AddNucleotide(link.Kind, link.Position, (away.IsZeroApprox() ? Vector2.FromAngle(Rand.Angle()) : away) * Size * Rand.Range(0.8f, 1.6f));
            link.Bead.QueueFree();
            link.Bond.QueueFree();
            strand.Links.RemoveAt(i);
        }
        return lost;
    }

    private static void FreeStrand(Strand strand)
    {
        strand.Head.QueueFree();
        foreach (Link link in strand.Links)
        {
            link.Bead.QueueFree();
            link.Bond.QueueFree();
        }
        strand.Links.Clear();
    }

    // ===========================================================================
    // ===============================   HELPERS   ===============================
    // ===========================================================================
    //
    //   SpawnNucleotide    A new nucleotide in a ring around the player, a share pairs with the next template base.
    //   AddNucleotide      A nucleotide bead with its letter at a point.
    //   RemoveNucleotide   Frees one nucleotide.
    //   NearestNucleotide  The nearest nucleotide within a range, null for none.
    //   Glow               Glowing material.
    //   NewLabel           HUD label with outline, translated by hand.
    //

    private void SpawnNucleotide(float distance)
    {
        RnaBase kind     = Rand.Chance(WANTED_CHANCE) ? Wanted : (RnaBase)Rand.Index(BASE_COLORS.Length);
        Vector2 position = _player.Position + Vector2.FromAngle(Rand.Angle()) * distance * Size;
        AddNucleotide(kind, position, Vector2.Zero);
    }

    private void AddNucleotide(RnaBase kind, Vector2 position, Vector2 velocity)
    {
        var node = new Node3D { Position = Plane3D.To3D(position) };
        AddChild(node);
        MeshInstance3D bead = CellVisual3D.AddMesh(node, CellVisual3D.SphereMesh(true), _baseMaterials[(int)kind], Vector3.Zero, Vector3.One * NUCLEOTIDE_RADIUS * Size);
        bead.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        // The letter floats above the bead and always faces the camera
        node.AddChild(new Label3D
        {
            Text            = kind.ToString(),
            Billboard       = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize        = 64,
            OutlineSize     = 14,
            PixelSize       = NUCLEOTIDE_RADIUS * Size * 0.025f,
            Modulate        = BASE_COLORS[(int)kind].Lightened(0.3f),
            OutlineModulate = new Color(0.05f, 0.05f, 0.1f),
            Position        = Vector3.Up * NUCLEOTIDE_RADIUS * 1.3f * Size,
            NoDepthTest     = true,
        });
        _nucleotides.Add(new Nucleotide { Kind = kind, Position = position, Velocity = velocity, Phase = Rand.Angle(), Node = node });
    }

    private void RemoveNucleotide(int index)
    {
        _nucleotides[index].Node.QueueFree();
        _nucleotides.RemoveAt(index);
    }

    private Nucleotide NearestNucleotide(Vector2 point, float range)
    {
        Nucleotide nearest = null;
        float      best    = range;
        foreach (Nucleotide nucleotide in _nucleotides)
        {
            float distance = nucleotide.Position.DistanceTo(point);
            if (distance < best)
            {
                best    = distance;
                nearest = nucleotide;
            }
        }
        return nearest;
    }

    private static StandardMaterial3D Glow(Color color, float glow)
    {
        return new StandardMaterial3D
        {
            AlbedoColor              = color,
            Roughness                = 0.35f,
            EmissionEnabled          = true,
            Emission                 = color,
            EmissionEnergyMultiplier = glow,
        };
    }

    private static Label NewLabel(int fontSize)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        label.AddThemeConstantOverride("outline_size", Mathf.Max(4, fontSize / 5));
        return label;
    }
}
