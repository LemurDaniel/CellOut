// NOTE:
//   Headless smoke test: starts the main scene, boosts biomass through all stages,
//   places parts in every cell editor, kills and respawns the player, lets a hunter
//   eat a bacterium, grows the full cluster and evolves it into an organism in the
//   creature editor, walks it onto the coast with the F10 cheats, and checks swim speed, wheel zoom, the German/English texts and
//   the editor rules (direction, thrust, diet, unlocks, refunds).
//   Exit code 0 = all checks passed, 1 = a check failed. A frame cap prevents endless runs.
//   Run by tools/Test-CellOut.ps1 after `dotnet build`.
//
//   godot --headless --path . res://tests/SmokeTest.tscn

//
//   Chapters:
//     PLAY THROUGH ALL STAGES   Frame script: boost, choose, die, feed, then check.
//     CHECK THE RESULT          One entry per check, printed as PASS/FAIL.
//     PROBES                    Zoom and language probes used by the checks.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class SmokeTest : Node
{
    // ===========================================================================
    // =======================   PLAY THROUGH ALL STAGES   =======================
    // ===========================================================================
    //
    //   1. Start      Instantiates the main scene and keeps running while it is paused.
    //   2. Script     Frame 2 connects, frames 20-400 boost, 450 kills, 460 feeds,
    //                 470 grows to 64 cells, 560 evolves and shapes a creature, from 700 checks.
    //

    private const int MAX_FRAMES = 1500;
    private const int BOOST_EVERY = 20;
    private const int BOOST_UNTIL = 400;
    private const float BOOST_BIOMASS = 60f;        // 20 boosts x 60 > 900 for the multicellular stage
    private const int KILL_FRAME = 450;
    private const int FEED_FRAME = 460;
    private const int GROW_FRAME = 470;             // enough biomass for 64 cells and the organism
    private const int EVOLVE_FRAME = 560;
    private const int LAND_FRAME = 580;             // cheats: points, then legs, lung and the coast
    private const int CHECK_FRAME = 700;
    private const int CHECK_DEADLINE = 1400;
    private const float GROW_BIOMASS = 100_000f;
    private const int MANUAL_EDITOR_FRAME = 300;   // after several level ups, between the stage advances

    private Main _main;
    private int _frame;
    private bool _hasDied;
    private readonly List<int> _stagesSeen = new();
    private Critter _prey;
    private bool _isPreySpawned;
    private int _editorSessions;
    private bool _isDropPreviewOk;
    private bool _isMenuShown;
    private int _divisions;
    private bool _isDivisionSizeKept;
    private bool _isCellSacrificeOk;
    private bool _isManualEditorOk;
    private bool _isEvolveOk;
    private bool _isCreatureEditorOk;
    private bool _isChecked;
    private bool _isCheatPointsOk;

    // ---------------------------------------------------------------------------
    // Start
    //   Instantiates the main scene and keeps running while it is paused.
    // ---------------------------------------------------------------------------

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _main = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Main>();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, _main);
    }

    // ---------------------------------------------------------------------------
    // Script
    //   Frame 2 connects, frames 20-400 boost, 450 kills, 460 feeds,
    //   470 grows to 64 cells, 560 evolves and shapes a creature, from 700 checks.
    // ---------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        _frame++;
        if (_frame > MAX_FRAMES)
        {
            Finish(false, "Frame-Cap erreicht");
            return;
        }
        Player player = _main.Player;
        if (null == player)
        {
            return;
        }

        if (2 == _frame)
        {
            // Title menu first, then straight into the game
            _isMenuShown = _main.IsInMenu
                && GetTree().Paused
                && 0 == _main.FoodLayer.GetChildCount()
                && 0 == _main.CritterLayer.GetChildCount();
            _main.StartGame(isInstant: true);
            player.StageChanged += stage => _stagesSeen.Add(stage);
            player.Died         += () => _hasDied = true;
        }

        // Points wait instead of opening the editor, E / the button opens it
        if (MANUAL_EDITOR_FRAME == _frame)
        {
            bool isClosedBefore = !_main.Editor.IsOpen && player.EvolutionPoints > 0;
            _isManualEditorOk   = isClosedBefore && _main.OpenEditor() && _main.Editor.IsOpen;
        }

        // Every open editor: place one part, then close it
        if (_main.Editor.IsOpen)
        {
            if (0 == _editorSessions)
            {
                _isDropPreviewOk = ProbeDropPreview(_main.Editor);
            }
            PlaceAnyPart(_main.Editor);
            _main.Editor.Finish();
            _editorSessions++;
        }

        if (0 == _frame % BOOST_EVERY && _frame <= BOOST_UNTIL)
        {
            player.AddBiomass(BOOST_BIOMASS, BiomassSource.Raw);
        }

        // Divide whenever allowed, the body size must stay and the cells must shrink
        if (player.CanDivide)
        {
            float sizeBefore   = player.BodySize();
            float radiusBefore = player.Radius;
            bool  isDivided    = player.Divide();
            _isDivisionSizeKept = (0 == _divisions || _isDivisionSizeKept)
                && isDivided
                && Mathf.IsEqualApprox(player.BodySize(), sizeBefore)
                && player.Radius < radiusBefore;
            _divisions++;
        }
        // A deadly hit on the cluster bursts a quarter of the cells, the player lives on
        if (KILL_FRAME == _frame)
        {
            int cellsBefore = player.CellCount;
            player.Invulnerable = 0f;
            player.TakeDamage(99999f, player.Position + Vector2.Right);
            _isCellSacrificeOk = player.CellCount == cellsBefore - Mathf.Max(1, cellsBefore / 4)
                && player.Hp > 0f
                && !_hasDied;
        }
        if (FEED_FRAME == _frame)
        {
            SpawnFeedingPair(player);
        }
        // Enough biomass: the division above runs once per frame up to 64 cells
        if (GROW_FRAME == _frame)
        {
            player.AddBiomass(GROW_BIOMASS, BiomassSource.Raw);
        }
        // Full cluster: evolve, choose a marine mammal, shape it, done
        if (EVOLVE_FRAME == _frame)
        {
            _isEvolveOk         = player.CanEvolve && _main.StartEvolve(isInstant: true) && _main.IsEvolving && GetTree().Paused;
            _isCreatureEditorOk = ShapeCreature(_main.CreatureEditor);
            _isEvolveOk         = _isEvolveOk && !_main.IsEvolving && !GetTree().Paused && player.IsOrganism;
        }
        // Cheat menu: ten points, then onto the land with legs and lung
        if (LAND_FRAME == _frame)
        {
            int pointsBefore = player.EvolutionPoints;
            _main.CheatTo(CheatTarget.Points);
            _isCheatPointsOk = pointsBefore + 10 == player.EvolutionPoints;
            _main.CheatTo(CheatTarget.LandAnimal);
        }
        // The creatures pop up in real time after the evolution, the check waits for them (at most CHECK_DEADLINE)
        bool hasCreatures = _main.CritterLayer.GetChildCount() > 0;
        if (!_isChecked && _frame >= CHECK_FRAME && (hasCreatures || _frame >= CHECK_DEADLINE))
        {
            _isChecked = true;
            Check(player);
        }
    }

    /// <summary>Places the first unlocked, affordable part that fits, starting at a different part per session.</summary>
    private void PlaceAnyPart(CellEditor editor)
    {
        IReadOnlyList<PartInfo> parts = PartCatalog.All();
        for (int i = 0; i < parts.Count; i++)
        {
            PartKind kind = parts[(i + _editorSessions) % parts.Count].Kind;
            foreach (Vector2 point in CandidatePoints(PartCatalog.Get(kind).Placement))
            {
                if (editor.TryPlace(kind, point))
                {
                    return;
                }
            }
        }
    }

    private static IEnumerable<Vector2> CandidatePoints(Placement placement)
    {
        float radius = Placement.Rim == placement ? 1f : 0.35f;
        var   points = new List<Vector2>();
        for (int k = 0; k < 12; k++)
        {
            points.Add(Vector2.FromAngle(k * Mathf.Tau / 12f) * radius);
        }
        return points;
    }

    /// <summary>
    /// The first shaping: type cards first, then a marine mammal with fluke and jaw
    /// (a tail fin of fish is refused), one more vertebra and a thicker middle.
    /// </summary>
    private static bool ShapeCreature(CreatureEditor editor)
    {
        bool isChoosing  = editor.IsOpen && editor.IsChoosingType;
        bool isBlocked   = !editor.TryPlace(CreaturePartKind.Fluke, 0f);
        editor.ChooseType(CreatureType.MarineMammal);
        bool isFishOnly  = !editor.TryPlace(CreaturePartKind.CaudalFin, 0f);
        int  pointsStart = editor.Points;
        bool isPlaced    = editor.TryPlace(CreaturePartKind.Fluke, 0f) && editor.TryPlace(CreaturePartKind.Jaw, 1f);
        bool isTailFull  = !editor.TryPlace(CreaturePartKind.Fluke, 0f);
        bool isPaid      = pointsStart - 4 == editor.Points;
        bool isLonger    = editor.AddVertebra() && 6 == editor.Draft.Girths.Count;
        editor.SetGirth(3, 5f);
        bool isClamped   = Mathf.IsEqualApprox(editor.Draft.Girths[3], CreatureDesign.MAX_GIRTH);
        editor.Finish();
        return isChoosing && isBlocked && isFishOnly && isPlaced && isTailFull && isPaid && isLonger && isClamped && !editor.IsOpen;
    }

    /// <summary>Hunter and prey on top of each other away from the player: the prey must be eaten.</summary>
    private void SpawnFeedingPair(Player player)
    {
        float   size     = player.BodySize();
        Vector2 position = player.Position + new Vector2(size * 6f, 0f);

        var hunter = new Critter { Position = position };
        hunter.Setup(size * 2f, CritterKind.Hunter, size);
        _main.CritterLayer.AddChild(hunter);

        _prey = new Critter { Position = position };
        _prey.Setup(size * 0.5f, CritterKind.Bacterium, size);
        _main.CritterLayer.AddChild(_prey);
        _isPreySpawned = true;
    }

    // ===========================================================================
    // ==========================   CHECK THE RESULT   ===========================
    // ===========================================================================
    //
    //   Stufe 1 erreicht        The protocell became a single cell.
    //   Stufe 2 erreicht        The single cell became multicellular.
    //   Editor genutzt          Editors opened and at least one part placed.
    //   Editor per Taste        Level ups only collect points, OpenEditor opens the editor.
    //   Zellteilung             The cells divided at least twice (4 cells or more).
    //   Teilung hält Größe      Every division kept the body size and shrank the cells.
    //   Zellen opfern           A deadly hit on the cluster costs a quarter of the cells, no death.
    //   Tod + Respawn           A deadly hit on a single cell costs 40% biomass and refills health.
    //   Größere Beute           Each division lowers the eat ratio by 0.05 (1.15 -> 0.85 at 64 cells).
    //   Spiel nicht pausiert    No choice panel is left open.
    //   Futter gespawnt         Food exists.
    //   Zellen gespawnt         Critters exist.
    //   Zellen fressen Zellen   The hunter ate the prey.
    //   3D-Kamera aktiv         The viewport renders through a Camera3D.
    //   3D-Zellhaufen           The player visual holds at least one node per cell.
    //   Haufen-Layout           1 cell in the middle, 2 side by side, 64 cells within an extent of 10.
    //   Form & Farbe            The outline passes through its radii and clamps, the color reaches the player.
    //   Beute < 20% Biomasse    A large prey is worth less than 20% of the own biomass.
    //   Schwimmtempo            SwimSpeed at the reference size is exactly 112.5.
    //   Zoom per Mausrad        Wheel down caps at MAX_ZOOM, wheel up zooms back in.
    //   Texte EN                English stage name and every mutation translated.
    //   Texte DE                German stage name and every mutation translated.
    //   Sprach-Button           ToggleLanguage switches from German to English.
    //   Stachel gerichtet       A forward spike hurts forward only.
    //   Geißel hinten           A flagellum at the back is faster than one at the front.
    //   Ernährung links         Only molecules -> clearly herbivore.
    //   Ernährung rechts        Mostly meat afterwards -> carnivore side.
    //   Kiefer-Freischaltung    The jaw is locked at diet 0, open at diet 0.5.
    //   EP-Erstattung           Removing a part returns its paid cost.
    //   Drop-Vorschau           The ghost shows on a free rim spot, hides on a taken one.
    //   Titelmenü               The game starts paused in the menu over an empty soup, StartGame leaves it.
    //   Intro endet             The intro ends in the gameplay camera pose and fires Finished.
    //   Evolve-Cutscene         The cutscene divides on to 512 cells and fires Finished.
    //   Lebewesen               64 cells evolve: editor opens, the player becomes an organism, game resumes.
    //   Kreatur-Editor          Type first, fish parts refused, costs paid, one part per tail, girth clamped.
    //   Kreatur im Spiel        One body of the same size, creature visual, cell visual hidden, no division.
    //   Kreatur-Werte           Fluke faster than none, jaw bites forward, spines guard flanks and back.
    //   Meer voller Kreaturen   NPCs of the organism stage are creatures.
    //   Kreatur-Geometrie       Blob profile and inside test, vertebra limits, NPC body plans.
    //   Texte Kreatur           Every creature part and type translated in both languages.
    //   Küste                   The coast exists, land and sea are on the right sides, things get pushed to sea.
    //   Landtier per Cheat      F10 points add 10 EP, the land jump walks the creature onto the coast: stage 5.
    //   Luft an Land            A fish without lung runs out of air on land and suffocates slowly, a mammal does not.
    //   Laufen an Land          Without legs it crawls, with legs it walks faster, lung and legs make it a land animal.
    //

    private void Check(Player player)
    {
        var checks = new List<(string Name, bool IsPassed)>
        {
            ("Stufe 1 erreicht", _stagesSeen.Contains(1)),
            ("Stufe 2 erreicht", _stagesSeen.Contains(2)),
            ("Editor genutzt", _editorSessions > 0 && player.Design.Parts.Count > 0),
            ("Editor per Taste", _isManualEditorOk),
            ("Zellteilung", _divisions >= 2),
            ("Teilung hält Größe", _isDivisionSizeKept),
            ("Zellen opfern", _isCellSacrificeOk),
            ("Tod + Respawn", ProbeSingleDeath()),
            ("Größere Beute", ProbeEatRatio()),
            ("Spiel nicht pausiert", !GetTree().Paused),
            ("Futter gespawnt", _main.FoodLayer.GetChildCount() > 0),
            ("Zellen gespawnt", _main.CritterLayer.GetChildCount() > 0),
            ("Zellen fressen Zellen", _isPreySpawned && !IsInstanceValid(_prey)),
            ("3D-Kamera aktiv", null != GetViewport().GetCamera3D()),
            ("3D-Zellhaufen", null != player.Visual && player.Visual.GetChildCount() >= player.CellCount + 1),
            ("Haufen-Layout", ProbeClusterLayout()),
            ("Form & Farbe", ProbeShapeAndColor()),
            ("Beute < 20% Biomasse", _main.KillReward(player.BodySize() * 0.85f) < player.Biomass * 0.2f),
            ("Schwimmtempo", Mathf.IsEqualApprox(CellBody.SwimSpeed(CellBody.REFERENCE_SIZE), 112.5f)),
            ("Zoom per Mausrad", ProbeZoom()),
            ("Texte EN", ProbeTexts("en", "Protocell")),
            ("Texte DE", ProbeTexts("de", "Protozelle")),
            ("Sprach-Button", ProbeLanguageButton()),
            ("Stachel gerichtet", ProbeDirectionalSpike()),
            ("Geißel hinten", ProbeFlagellumThrust()),
            ("Ernährung links", ProbeDiet().IsHerbivore),
            ("Ernährung rechts", ProbeDiet().IsCarnivore),
            ("Kiefer-Freischaltung", !PartCatalog.IsUnlocked(PartCatalog.Get(PartKind.Jaw), 1, 0f) && PartCatalog.IsUnlocked(PartCatalog.Get(PartKind.Jaw), 1, 0.5f)),
            ("EP-Erstattung", ProbeRefund()),
            ("Drop-Vorschau", _isDropPreviewOk),
            ("Titelmenü", _isMenuShown && !_main.IsInMenu),
            ("Intro endet", ProbeIntro()),
            ("Evolve-Cutscene", ProbeEvolveCutscene()),
            ("Lebewesen", _isEvolveOk),
            ("Kreatur-Editor", _isCreatureEditorOk),
            ("Kreatur im Spiel", ProbeOrganismPlayer(player)),
            ("Kreatur-Werte", ProbeCreatureStats()),
            ("Meer voller Kreaturen", ProbeCreatureSea()),
            ("Kreatur-Geometrie", ProbeCreatureGeometry()),
            ("Texte Kreatur", ProbeCreatureTexts("en") && ProbeCreatureTexts("de")),
            ("Küste", ProbeCoast()),
            ("Landtier per Cheat", _isCheatPointsOk && player.IsLandAnimal && player.IsOnLand),
            ("Luft an Land", ProbeAir()),
            ("Laufen an Land", ProbeWalking()),
        };

        bool isOk = true;
        foreach ((string name, bool isPassed) in checks)
        {
            GD.Print($"{(isPassed ? "PASS" : "FAIL")} {name}");
            isOk = isOk && isPassed;
        }
        Finish(isOk, $"stage={player.Stage} level={player.Level} cells={player.CellCount} size={player.BodySize():F1}");
    }

    // ===========================================================================
    // ===============================   PROBES   ================================
    // ===========================================================================
    //
    //   ProbeZoom             Twenty wheel steps out hit MAX_ZOOM, one step in leaves it.
    //   ProbeTexts            Stage name as expected and no part key left untranslated.
    //   ProbeLanguageButton   Toggling from German lands on English, German is restored.
    //   ProbeDirectionalSpike Spike at angle 0: 8 damage forward, none backward.
    //   ProbeFlagellumThrust  Back flagellum vs. front flagellum on two loose players.
    //   ProbeDiet             100 plant, then 500 meat on a fresh tracker.
    //   ProbeRefund           Place a spike, remove it, get its cost back.
    //   ProbeDropPreview      Ghost on a free rim spot, gone after placing, none on the taken spot.
    //   ProbeIntro            One long frame past the end: Finished fired, camera at CameraEnd.
    //   ProbeClusterLayout    Concrete cell positions of 1 and 2 cells, size of the 64 cell cluster.
    //   ProbeEvolveCutscene   One long frame past the end: Finished fired, 512 cells shown.
    //   ProbeOrganismPlayer   Stage 4, one body, creature visual shown, cells hidden, no more division.
    //   ProbeCreatureStats    Loose organisms: fluke speed, jaw and spine directions.
    //   OrganismProbe         A loose player grown to 64 cells and turned into an organism.
    //   ProbeCreatureSea      Every NPC in the sea carries a creature body plan.
    //   ProbeCreatureGeometry Blob radius, inside test, vertebra limits, NPC plans.
    //   ProbeCreatureTexts    No creature key left untranslated.
    //   ProbeCoast            Land depth of concrete points, ToSea, the coast of the run.
    //   ProbeAir              Twelve seconds on land: fish without lung 0 air and hurt, mammal full air.
    //   ProbeWalking          Crawl vs. walk speed on land, the step onto land advances the stage.
    //   ProbeShapeAndColor    Stretched front 1.4, squashed back 0.8, clamp at 1.5, copied, color reaches the player.
    //   ProbeSingleDeath      A loose single cell dies: Died fires, 60% biomass left, full health.
    //   ProbeEatRatio         1.15 as single cell, 1.05 after two divisions, 0.85 at 64 cells.
    //

    private bool ProbeZoom()
    {
        var wheelDown = new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = true };
        var wheelUp   = new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true };
        for (int i = 0; i < 20; i++)
        {
            _main._UnhandledInput(wheelDown);
        }
        bool isCapped = Mathf.IsEqualApprox(_main.ZoomFactor, Main.MAX_ZOOM);
        _main._UnhandledInput(wheelUp);
        return isCapped && _main.ZoomFactor < Main.MAX_ZOOM;
    }

    private bool ProbeTexts(string locale, string expectedStage)
    {
        TranslationServer.SetLocale(locale);
        bool isStageOk = expectedStage == Tr("STAGE_PROTOCELL");
        foreach (PartInfo info in PartCatalog.All())
        {
            if (info.NameKey == Tr(info.NameKey) || info.DescriptionKey == Tr(info.DescriptionKey))
            {
                GD.Print($"  missing '{locale}' text for {info.Kind}");
                isStageOk = false;
            }
        }
        return isStageOk;
    }

    private bool ProbeLanguageButton()
    {
        TranslationServer.SetLocale("de");
        _main.Hud.ToggleLanguage();
        bool isEnglish = TranslationServer.GetLocale().StartsWith("en");
        TranslationServer.SetLocale("de");
        return isEnglish;
    }

    private static bool ProbeDirectionalSpike()
    {
        var design = new CellDesign();
        design.TryPlace(PartKind.Spike, new Vector2(1f, 0f), paidCost: 1);
        var probe = new Player();
        probe.CommitDesign(design, 0);
        bool isDirectional = Mathf.IsEqualApprox(probe.ContactDamageToward(0f), 8f)
            && Mathf.IsZeroApprox(probe.ContactDamageToward(Mathf.Pi));
        probe.Free();
        return isDirectional;
    }

    private static bool ProbeFlagellumThrust()
    {
        float SpeedWith(Vector2 position)
        {
            var design = new CellDesign();
            design.TryPlace(PartKind.Flagellum, position, paidCost: 2);
            var probe = new Player();
            probe.CommitDesign(design, 0);
            float speed = probe.CurrentSpeed();
            probe.Free();
            return speed;
        }
        return SpeedWith(new Vector2(-1f, 0f)) > SpeedWith(new Vector2(1f, 0f));
    }

    private static (bool IsHerbivore, bool IsCarnivore) ProbeDiet()
    {
        var tracker = new DietTracker();
        tracker.Add(BiomassSource.Plant, 100f);
        bool isHerbivore = tracker.Value(0f, hasParts: false) < -0.5f;
        tracker.Add(BiomassSource.Meat, 500f);
        bool isCarnivore = tracker.Value(0f, hasParts: false) > 0.33f;
        return (isHerbivore, isCarnivore);
    }

    private static bool ProbeRefund()
    {
        var  design   = new CellDesign();
        bool isPlaced = design.TryPlace(PartKind.Spike, new Vector2(0f, 1f), paidCost: 1);
        PlacedPart? removed = design.RemoveNear(new Vector2(0f, 0.95f));
        return isPlaced && null != removed && 1 == removed.Value.PaidCost && 0 == design.Parts.Count;
    }

    /// <summary>Runs in the first editor session, removes its spike again so the session stays untouched.</summary>
    private static bool ProbeDropPreview(CellEditor editor)
    {
        Vector2 spot      = new(-0.6f, -0.8f);
        bool    isShown   = editor.PreviewPart(PartKind.Spike, spot) && PartKind.Spike == editor.GhostKind;
        bool    isPlaced  = editor.TryPlace(PartKind.Spike, spot);
        bool    isCleared = null == editor.GhostKind;
        bool    isBlocked = !editor.PreviewPart(PartKind.Spike, spot) && null == editor.GhostKind;
        editor.RemoveAt(Vector2.FromAngle(spot.Angle()));
        return isShown && isPlaced && isCleared && isBlocked;
    }

    private bool ProbeIntro()
    {
        var camera = new Camera3D();
        AddChild(camera);
        var end   = new Transform3D(Basis.Identity, new Vector3(0f, 5f, 0f));
        var intro = new Intro { Camera = camera, CameraEnd = end, Center = Vector3.Zero, Size = 1f };
        bool isFinished = false;
        intro.Finished += () => isFinished = true;
        AddChild(intro);
        intro._Process(10.0);
        bool isAtEnd = camera.GlobalTransform.Origin.IsEqualApprox(end.Origin);
        intro.QueueFree();
        camera.QueueFree();
        return isFinished && isAtEnd;
    }

    private static bool ProbeSingleDeath()
    {
        var  probe   = new Player();
        bool hasDied = false;
        probe.Died += () => hasDied = true;
        probe.AddBiomass(100f, BiomassSource.Raw);
        float biomass = probe.Biomass;
        probe.Invulnerable = 0f;
        probe.TakeDamage(99999f, Vector2.Right);
        bool isOk = hasDied && Mathf.IsEqualApprox(probe.Biomass, biomass * 0.6f) && Mathf.IsEqualApprox(probe.Hp, probe.MaxHp);
        probe.Free();
        return isOk;
    }

    private static bool ProbeEatRatio()
    {
        var probe = new Player();
        probe.AddBiomass(400f, BiomassSource.Raw);
        float single = probe.EatRatioToward(0f);
        probe.Divide();
        probe.Divide();
        float afterTwo = probe.EatRatioToward(0f);
        probe.AddBiomass(1_000_000f, BiomassSource.Raw);
        while (probe.Divide()) { }
        float atMax     = probe.EatRatioToward(0f);
        int   cellCount = probe.CellCount;
        probe.Free();
        return Mathf.IsEqualApprox(single, 1.15f)
            && Mathf.IsEqualApprox(afterTwo, 1.05f)
            && Mathf.IsEqualApprox(atMax, 0.85f)
            && Player.MAX_CELLS == cellCount;
    }

    private static bool ProbeShapeAndColor()
    {
        var radii = new float[CellDesign.SHAPE_POINTS];
        System.Array.Fill(radii, 1f);
        radii[0] = 1.4f;
        radii[2] = 9f;
        radii[4] = 0.8f;
        var design = new CellDesign();
        design.SetShape(radii);
        design.SetColor(new Color(1f, 0f, 0f));
        var copy = new CellDesign();
        copy.CopyFrom(design);

        var probe = new Player();
        probe.CommitDesign(copy, 0);
        bool isColored = probe.BodyColor.IsEqualApprox(new Color(1f, 0f, 0f));
        probe.Free();
        return Mathf.IsEqualApprox(copy.ShapeRadius(0f), 1.4f)
            && Mathf.IsEqualApprox(copy.ShapeRadius(Mathf.Pi), 0.8f)
            && Mathf.IsEqualApprox(copy.Shape[2], CellDesign.MAX_SHAPE)
            && isColored;
    }

    private bool ProbeEvolveCutscene()
    {
        var camera = new Camera3D();
        AddChild(camera);
        var  cutscene   = new EvolveCutscene { Camera = camera, Center = Vector3.Zero, Size = 1f };
        bool isFinished = false;
        cutscene.Finished += () => isFinished = true;
        AddChild(cutscene);
        bool isFullAtStart = Player.MAX_CELLS == cutscene.VisibleCells;
        cutscene._Process(10.0);
        bool isAtEnd = Player.ORGANISM_CELLS == cutscene.VisibleCells;
        cutscene.QueueFree();
        camera.QueueFree();
        return isFinished && isFullAtStart && isAtEnd;
    }

    private static bool ProbeOrganismPlayer(Player player)
    {
        return player.IsOrganism
            && 1 == player.CellCount
            && CreatureType.MarineMammal == player.Creature.Type
            && 1 == player.Creature.Count(CreaturePartKind.Fluke)
            && !player.IsHidden
            && null != player.CreatureVisual && player.CreatureVisual.GetChildCount() > 0
            && !player.Visual.Visible
            && !player.CanDivide && !player.CanEvolve
            && Mathf.IsEqualApprox(player.BodySize(), player.Radius);
    }

    private static bool ProbeCreatureStats()
    {
        float SpeedWith(params CreaturePartKind[] kinds)
        {
            Player probe = OrganismProbe(CreatureType.MarineMammal, kinds);
            float  speed = probe.CurrentSpeed();
            probe.Free();
            return speed;
        }
        bool isFaster = SpeedWith(CreaturePartKind.Fluke) > SpeedWith();

        Player hunter  = OrganismProbe(CreatureType.Fish, CreaturePartKind.Jaw, CreaturePartKind.Spines);
        bool   isArmed = hunter.CanBiteToward(0f) && !hunter.CanBiteToward(Mathf.Pi)
            && hunter.ContactDamageToward(Mathf.Pi * 0.5f) > 0f && hunter.ContactDamageToward(Mathf.Pi) > 0f;
        hunter.Free();
        return isFaster && isArmed;
    }

    private static Player OrganismProbe(CreatureType type, params CreaturePartKind[] kinds)
    {
        var probe = new Player();
        probe.AddBiomass(GROW_BIOMASS, BiomassSource.Raw);
        while (probe.Divide()) { }
        CreatureDesign design = CreatureDesign.Blob();
        design.Type = type;
        foreach (CreaturePartKind kind in kinds)
        {
            design.TryPlace(kind, 0.5f, 0);
        }
        probe.BecomeOrganism(design, 0);
        return probe;
    }

    private bool ProbeCreatureSea()
    {
        List<Critter> critters = _main.CritterLayer.GetChildren().OfType<Critter>().Where(critter => !critter.IsQueuedForDeletion()).ToList();
        return critters.Count > 0 && critters.All(critter => null != critter.Creature);
    }

    private static bool ProbeCreatureGeometry()
    {
        CreatureDesign blob = CreatureDesign.Blob();
        bool isProfileOk = Mathf.IsEqualApprox(blob.RadiusAt(0f), CreatureDesign.MAX_RADIUS)
            && Mathf.IsZeroApprox(blob.RadiusAt(blob.NoseTipX + 0.01f))
            && blob.Contains(Vector3.Zero)
            && !blob.Contains(new Vector3(0f, CreatureDesign.MAX_RADIUS * 1.1f, 0f));
        while (blob.AddVertebra()) { }
        bool isMaxOk = CreatureDesign.MAX_VERTEBRAE == blob.Girths.Count;
        while (blob.RemoveVertebra()) { }
        bool isMinOk = CreatureDesign.MIN_VERTEBRAE == blob.Girths.Count;
        bool isNpcOk = 1 == CreatureDesign.ForCritter(CritterKind.Hunter).Count(CreaturePartKind.Jaw)
            && 0 == CreatureDesign.ForCritter(CritterKind.Bacterium).Count(CreaturePartKind.Jaw);
        return isProfileOk && isMaxOk && isMinOk && isNpcOk;
    }

    private bool ProbeCreatureTexts(string locale)
    {
        TranslationServer.SetLocale(locale);
        bool isOk = "STAGE_ORGANISM" != Tr("STAGE_ORGANISM");
        foreach (CreaturePartInfo info in CreatureCatalog.All())
        {
            if (info.NameKey == Tr(info.NameKey) || info.DescriptionKey == Tr(info.DescriptionKey))
            {
                GD.Print($"  missing '{locale}' text for creature part {info.Kind}");
                isOk = false;
            }
        }
        foreach (string key in new[] { "CREATURE_TYPE_FISH_NAME", "CREATURE_TYPE_MARINEMAMMAL_DESC", "ORGANISM_BUTTON", "GOAL_ORGANISM_AT" })
        {
            isOk = isOk && key != Tr(key);
        }
        TranslationServer.SetLocale("de");
        return isOk;
    }

    private bool ProbeCoast()
    {
        var  coast       = new Coast { Origin = Vector2.Zero, Normal = Vector2.Right, Unit = 1f };
        bool isSidesOk   = Mathf.IsEqualApprox(coast.LandDepth(new Vector2(5f, 3f)), 5f)
            && coast.IsOnLand(new Vector2(1f, 0f)) && !coast.IsOnLand(new Vector2(-1f, 0f));
        bool isToSeaOk   = coast.ToSea(new Vector2(4f, 2f), 1f).IsEqualApprox(new Vector2(-1f, 2f))
            && coast.ToSea(new Vector2(-9f, 2f), 1f).IsEqualApprox(new Vector2(-9f, 2f));
        coast.Free();
        return isSidesOk && isToSeaOk && null != _main.Coast;
    }

    private static bool ProbeAir()
    {
        const float STEP = 0.5f;
        const int STEPS = 24;                        // 12 seconds: 8 to run out of air, 4 suffocating
        Player fish   = OrganismProbe(CreatureType.Fish);
        Player mammal = OrganismProbe(CreatureType.MarineMammal);
        fish.IsOnLand   = true;
        mammal.IsOnLand = true;
        fish.Invulnerable = 0f;
        for (int i = 0; i < STEPS; i++)
        {
            fish._PhysicsProcess(STEP);
            mammal._PhysicsProcess(STEP);
        }
        bool isOk = Mathf.IsZeroApprox(fish.Air) && fish.Hp < fish.MaxHp * 0.9f && fish.Hp > 0f
            && Mathf.IsEqualApprox(mammal.Air, 1f)
            && Player.ORGANISM_STAGE == fish.Stage;
        fish.Free();
        mammal.Free();
        return isOk;
    }

    private static bool ProbeWalking()
    {
        Player crawler = OrganismProbe(CreatureType.Fish);
        Player walker  = OrganismProbe(CreatureType.Fish, CreaturePartKind.Legs, CreaturePartKind.Lung);
        crawler.IsOnLand = true;
        walker.IsOnLand  = true;
        bool isCrawling  = Mathf.IsEqualApprox(crawler.CurrentSpeed(), CellBody.SwimSpeed(crawler.BodySize()) * Player.CRAWL_SPEED);
        bool isWalking   = walker.CurrentSpeed() > crawler.CurrentSpeed();
        walker._PhysicsProcess(0.1);
        bool isLandStage = walker.IsLandAnimal && !crawler.IsLandAnimal;
        crawler.Free();
        walker.Free();
        return isCrawling && isWalking && isLandStage;
    }

    private static bool ProbeClusterLayout()
    {
        Vector3[] single = CellBody.ClusterLayout(1);
        Vector3[] pair   = CellBody.ClusterLayout(2);
        Vector3[] full   = CellBody.ClusterLayout(Player.MAX_CELLS);
        return 1 == single.Length && single[0].IsEqualApprox(Vector3.Zero)
            && pair[1].IsEqualApprox(new Vector3(0f, 0f, 0.85f))
            && 64 == full.Length
            && CellBody.ClusterExtent(Player.MAX_CELLS) < 10f
            && Mathf.IsEqualApprox(CellBody.ClusterExtent(1), 1f);
    }

    private void Finish(bool isOk, string info)
    {
        GD.Print($"SMOKE {(isOk ? "OK" : "FAILED")} · {info}");
        GetTree().Quit(isOk ? 0 : 1);
    }
}
