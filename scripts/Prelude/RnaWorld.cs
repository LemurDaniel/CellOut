// NOTE:
//   Playable prelude before the protocell: the player is a free RNA strand in the
//   primordial soup. The ribozyme head swims like a snake, nucleotides (A, U, G, C)
//   drift around. The strand copies a random template: swallowing the base that pairs
//   with the next template base (A-U, G-C) adds a link, a wrong base still sticks but
//   as a mutation that does not count for the goal: it rolls a trait (Prelude/Mutation.cs,
//   good ones white, bad ones red) that the cell keeps, plus evolution points. A bitten
//   off mutation link takes its trait along. The ring around the head glows in the wanted color.
//   Rival strands swim around and eat nucleotides too. A head biting into a chain breaks
//   it there, the broken off links float away as nucleotides: rivals bite the player,
//   the player bites rivals. GOAL_LINKS paired links call a lipid vesicle, swimming into
//   it ends the prelude, Main then plays the Intro there.
//   Runs while the game is paused (like the cutscenes), Tab skips it.
//   This class is the coordinator: the rules are in Prelude/Chain/ (template, chain), the
//   strands, nucleotides, the vesicle and the HUD are components in Prelude/View/.
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
//     HELPERS         Spawning rivals and nucleotides, breaking a strand.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class RnaWorld : Node3D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Camera       (Required) The world camera, flown down from the menu pose and then following the head.
    //   Center       (Required) Spawn point of the player on the plane, the strand starts here.
    //   Size         (Required) Body size of the player (Player.BodySize()), everything scales with it.
    //   CameraMoved  Every frame with the camera focus and distance, Main moves the sea floor with it.
    //   Finished     The strand swam into the vesicle (or Tab skipped): its point and the mutation count.
    //
    //   The bases (RnaBase) are in Prelude/Chain/RnaTemplate.cs.
    //

    [Signal] public delegate void CameraMovedEventHandler(Vector2 focus, float distance);
    [Signal] public delegate void FinishedEventHandler(Vector2 vesicle, int[] mutations);

    public Camera3D Camera { get; set; }
    public Vector2 Center { get; set; }
    public float Size { get; set; } = 14f;

    // Goal and chain, lengths in body sizes
    public const int GOAL_LINKS = 8;
    private const int START_LINKS = 2;
    private const float HEAD_RADIUS = RnaStrand.HEAD_RADIUS;
    public const float LINK_SPACING = RnaStrand.LINK_SPACING;

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
    private const float SPAWN_MIN = 3f;
    private const float SPAWN_MAX = 9f;
    private const float DESPAWN_DISTANCE = 12f;
    private const float WANTED_CHANCE = 0.5f;       // share of new nucleotides that pair with the next template base

    // Vesicle and camera
    private const float VESICLE_DISTANCE = 5f;
    private const float FLY_SECONDS = 1.6f;
    private const float CAM_TILT = 0.5f;            // tilt from vertical, like Main
    private const float CAM_DISTANCE = 7f;
    private const float CAM_DISTANCE_PER_LINK = 0.18f;

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   _Ready           Keeps running while paused, builds the player strand, rivals, nucleotides and the HUD.
    //   NewStrand        A strand component at a point, the player or a rival.
    //   _Notification    A language change writes the HUD texts new.
    //

    private readonly List<RnaStrand> _rivals = new();
    private readonly RnaTemplate _template = new(() => (RnaBase)Rand.Index(RnaTemplate.BASE_COUNT));
    private RnaPalette _palette;
    private RnaStrand _player;
    private RnaSoup _soup;
    private RnaVesicle _vesicle;
    private RnaHud _hud;
    private Transform3D _cameraStart;
    private Vector3 _camFocus;
    private float _time;
    private bool _isFinished;

    public override void _Ready()
    {
        ProcessMode  = ProcessModeEnum.Always;
        _cameraStart = Camera.GlobalTransform;
        _camFocus    = Plane3D.To3D(Center);
        _palette     = new RnaPalette();

        _soup = new RnaSoup();
        _soup.Setup(_palette, Size);
        AddChild(_soup);
        _vesicle = new RnaVesicle();
        _vesicle.Setup(Size);
        AddChild(_vesicle);

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
        _hud = new RnaHud();
        AddChild(_hud);
        UpdateHud();
        UpdateFrame(0f);
        Log.Verbose("RnaWorld", $"Prelude started at {Center}, goal {GOAL_LINKS} paired links, {RIVALS} rivals.");
    }

    private RnaStrand NewStrand(Vector2 position, float heading, bool isRival)
    {
        var strand = new RnaStrand();
        strand.Setup(_palette, Size, position, heading, isRival);
        AddChild(strand);
        return strand;
    }

    public override void _Notification(int what)
    {
        if (NotificationTranslationChanged == what && null != _hud)
        {
            UpdateHud();
        }
    }

    // ===========================================================================
    // ================================   CHAIN   ================================
    // ===========================================================================
    //
    //   Pair             The base that pairs with another one: A-U, G-C.
    //   WantedAt         The base that pairs with the template at a position.
    //   Wanted           The base the next link needs.
    //   PairedCount      Links that paired correctly, they count for the goal.
    //   MutationCount    Links that stuck without pairing.
    //   Mutations        The traits of those links (Prelude/Mutation.cs).
    //   LinkCount        All links behind the head.
    //   IsGoalReached    Enough paired links, the vesicle is called.
    //   Attach           Adds a link at the end, a paired one or a mutation.
    //   BreakAt          A bite: breaks the chain at the first link inside a circle.
    //   Skip             Ends the prelude at once (Tab, smoke test).
    //   UpdateHud        The ring in the wanted color and the state of the chain for the HUD.
    //
    //   The rules themselves are in Prelude/Chain/RnaTemplate.cs and RnaChain.cs.
    //

    /// <summary>The base that pairs with another one: A-U, G-C.</summary>
    /// <example><code>RnaWorld.Pair(RnaBase.G)  // RnaBase.C</code></example>
    public static RnaBase Pair(RnaBase kind)
    {
        return RnaTemplate.Pair(kind);
    }

    /// <summary>The base that pairs with the template at a position, a broken chain asks again from there.</summary>
    public RnaBase WantedAt(int index) => _template.WantedAt(index);

    /// <summary>The base the next link needs, every link (mutations too) takes one template position.</summary>
    public RnaBase Wanted => WantedAt(_player.Chain.Count);

    public int PairedCount { get; private set; }
    public int MutationCount { get; private set; }
    public int LinkCount => _player.Chain.Count;
    public bool IsGoalReached => PairedCount >= GOAL_LINKS;

    /// <summary>The traits of the mutation links on the player chain, a bitten off link takes its trait along.</summary>
    public IReadOnlyList<MutationKind> Mutations => _player.Chain.Mutations;

    /// <summary>Adds a link at the end of the player chain, paired if it fits Wanted, else as a mutation with a rolled trait.</summary>
    /// <param name="kind">(Required) The swallowed base.</param>
    /// <returns>True when it paired.</returns>
    /// <example><code>prelude.Attach(prelude.Wanted)  // true</code></example>
    public bool Attach(RnaBase kind)
    {
        bool          isPaired = kind == Wanted;
        MutationKind? mutation = isPaired ? null : Mutation.Roll();
        _player.AddLink(kind, mutation);
        RecountLinks();
        Log.Verbose("RnaWorld", $"{kind} {(isPaired ? "paired" : $"stuck as mutation {mutation}")}, {PairedCount}/{GOAL_LINKS} paired, {MutationCount} mutations.");
        if (IsGoalReached && !_vesicle.HasAppeared)
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
        int cut = _player.FirstLinkAt(point, radius);
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
        if (!_vesicle.HasAppeared)
        {
            CallVesicle();
        }
        Finish();
    }

    private void RecountLinks()
    {
        PairedCount   = _player.Chain.PairedCount;
        MutationCount = _player.Chain.MutationCount;
        UpdateHud();
    }

    private void UpdateHud()
    {
        RnaBase wanted = Wanted;
        _palette.ShowWanted(wanted);
        if (null == _hud)
        {
            return;
        }
        var upcoming = new RnaBase[RnaHud.UPCOMING_SHOWN];
        for (int i = 0; i < upcoming.Length; i++)
        {
            upcoming[i] = WantedAt(_player.Chain.Count + 1 + i);
        }
        _hud.Refresh(new RnaHud.State(PairedCount, GOAL_LINKS, IsGoalReached, wanted, upcoming, Mutations));
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

        float   distance = Size * (CAM_DISTANCE + CAM_DISTANCE_PER_LINK * _player.Chain.Count);
        Vector3 focus    = Plane3D.To3D(_player.HeadPosition);
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
                input = Plane3D.MouseOnPlane(GetViewport(), _player.HeadPosition) - _player.HeadPosition;
            }
        }
        bool isSteered = input.LengthSquared() > 0.0001f;
        _player.Move(isSteered ? input.Angle() : _player.Heading, isSteered ? SWIM_SPEED : IDLE_SPEED, TURN_SPEED, dt, _time);
        _player.SpinRing(_time);

        // ---------------------------------------------------------------------------
        // Rivals
        //   Flee the player head, go for a nucleotide nearby or wander, far ones respawn.
        // ---------------------------------------------------------------------------

        for (int i = _rivals.Count - 1; i >= 0; i--)
        {
            RnaStrand rival = _rivals[i];
            if (rival.HeadPosition.DistanceTo(_player.HeadPosition) > RIVAL_DESPAWN * Size)
            {
                rival.QueueFree();
                _rivals.RemoveAt(i);
                continue;
            }

            // > The player head within RIVAL_FLEE_RANGE: swim away from it
            // > A nucleotide within RIVAL_SEEK_RANGE: swim to the nearest one
            // > Else: wander, the target heading drifts slowly
            Vector2  fromPlayer = rival.HeadPosition - _player.HeadPosition;
            Vector2? prey       = _soup.Nearest(rival.HeadPosition, RIVAL_SEEK_RANGE * Size);
            rival.DriftWander(Rand.Range(-1f, 1f) * dt);
            float target = rival.Heading + rival.Wander;
            if (fromPlayer.Length() < RIVAL_FLEE_RANGE * Size)
            {
                target = fromPlayer.Angle();
            }
            else if (null != prey)
            {
                target = (prey.Value - rival.HeadPosition).Angle();
            }
            rival.Move(target, RIVAL_SPEED, RIVAL_TURN, dt, _time);
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
            foreach (RnaStrand rival in _rivals)
            {
                rival.CoolBite(dt);

                // The rival bites the player chain
                if (rival.IsReadyToBite && BreakAt(rival.HeadPosition, bite) > 0)
                {
                    rival.RestAfterBite(BITE_COOLDOWN);
                }

                // The player bites the rival chain
                int cut = rival.FirstLinkAt(_player.HeadPosition, bite);
                if (cut >= 0)
                {
                    int lost = BreakStrand(rival, cut, _player.HeadPosition);
                    Log.Verbose("RnaWorld", $"Player bit a rival at link {cut}, {lost} links float free.");
                }
            }
        }

        // ---------------------------------------------------------------------------
        // Nucleotides
        //   Drift and bob, heads swallow them, far ones respawn around the view.
        // ---------------------------------------------------------------------------

        float reach = (HEAD_RADIUS + RnaSoup.NUCLEOTIDE_RADIUS) * Size;
        _soup.Drift(_time, dt);
        for (int i = _soup.Count - 1; i >= 0; i--)
        {
            Vector2 position = _soup.PositionAt(i);
            if (_time >= START_GUARD && position.DistanceTo(_player.HeadPosition) <= reach)
            {
                Attach(_soup.KindAt(i));
                _soup.RemoveAt(i);
                continue;
            }
            RnaStrand eater = _rivals.Find(rival => rival.Chain.Count < RIVAL_MAX_LINKS && position.DistanceTo(rival.HeadPosition) <= reach);
            if (null != eater)
            {
                eater.AddLink(_soup.KindAt(i), null);
                _soup.RemoveAt(i);
                continue;
            }
            if (position.DistanceTo(_player.HeadPosition) > DESPAWN_DISTANCE * Size)
            {
                _soup.RemoveAt(i);
            }
        }
        while (_soup.Count < NUCLEOTIDES)
        {
            SpawnNucleotide(Rand.Range(SPAWN_MAX * 0.8f, SPAWN_MAX));
        }

        // ---------------------------------------------------------------------------
        // Vesicle
        //   Wobbles, swimming into it ends the prelude.
        // ---------------------------------------------------------------------------

        _vesicle.Animate(_time);
        if (_vesicle.Contains(_player.HeadPosition))
        {
            Finish();
        }
    }

    private void CallVesicle()
    {
        Vector2 spot = _player.HeadPosition + Vector2.FromAngle(_player.Heading + Rand.Range(-1f, 1f)) * VESICLE_DISTANCE * Size;
        _vesicle.Appear(spot, _time);
        UpdateHud();
        Log.Verbose("RnaWorld", $"Goal reached -> vesicle at {spot}.");
    }

    private void Finish()
    {
        if (_isFinished)
        {
            return;
        }
        _isFinished = true;
        Log.Verbose("RnaWorld", $"Strand entered the vesicle with {PairedCount} paired links and {MutationCount} mutations.");
        EmitSignal(SignalName.Finished, _vesicle.Spot, Mutations.Select(kind => (int)kind).ToArray());
    }

    // ===========================================================================
    // ===============================   HELPERS   ===============================
    // ===========================================================================
    //
    //   SpawnRival        A rival with a few links in a ring around the player.
    //   SpawnNucleotide   A new nucleotide in a ring around the player, a share pairs with the next template base.
    //   BreakStrand       Breaks a strand at a link, the lost links float away as nucleotides.
    //

    private void SpawnRival(float distance)
    {
        Vector2   position = _player.HeadPosition + Vector2.FromAngle(Rand.Angle()) * distance * Size;
        RnaStrand rival    = NewStrand(position, Rand.Angle(), isRival: true);
        int       links    = RIVAL_MIN_LINKS + Rand.Index(RIVAL_MAX_LINKS - RIVAL_MIN_LINKS + 1);
        for (int i = 0; i < links; i++)
        {
            rival.AddLink((RnaBase)Rand.Index(RnaTemplate.BASE_COUNT), null);
        }
        _rivals.Add(rival);
    }

    private void SpawnNucleotide(float distance)
    {
        RnaBase kind     = Rand.Chance(WANTED_CHANCE) ? Wanted : (RnaBase)Rand.Index(RnaTemplate.BASE_COUNT);
        Vector2 position = _player.HeadPosition + Vector2.FromAngle(Rand.Angle()) * distance * Size;
        _soup.Add(kind, position, Vector2.Zero);
    }

    private int BreakStrand(RnaStrand strand, int cut, Vector2 from)
    {
        IReadOnlyList<RnaChain.Link> lost = strand.Break(cut);
        for (int i = lost.Count - 1; i >= 0; i--)
        {
            Vector2 away = (lost[i].Position - from).Normalized();
            _soup.Add(lost[i].Kind, lost[i].Position, (away.IsZeroApprox() ? Vector2.FromAngle(Rand.Angle()) : away) * Size * Rand.Range(0.8f, 1.6f));
        }
        return lost.Count;
    }
}
