// NOTE:
//   Headless smoke test: starts the main scene, boosts biomass through all stages,
//   places parts in every cell editor, kills and respawns the player, lets a hunter
//   eat a bacterium, grows the full cluster and evolves it into an organism in the
//   creature editor, walks it onto the island coast with the F10 cheats (third person), and checks the RNA prelude, swim speed, wheel zoom, the German/English texts and
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
    private Food _grazedFood;
    private int _editorSessions;
    private bool _isDropPreviewOk;
    private bool _isCellGizmoOk;
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
            _main.IsAnimationSkipped = true;
            _main.StartGame(isInstant: true);
            player.StageChanged += stage => _stagesSeen.Add(stage);
            player.Died         += () => _hasDied = true;
        }

        // Points wait instead of opening the editor, E / the button opens it
        if (MANUAL_EDITOR_FRAME == _frame)
        {
            // Neither a level up nor the stage advance before opened it
            bool isClosedBefore = !_main.Editor.IsOpen && 0 == _editorSessions && player.EvolutionPoints > 0;
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
            if (0 == _editorSessions)
            {
                _isCellGizmoOk = ProbeCellGizmo(_main.Editor);
            }
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
            editor.Select(kind);                     // like a tile click, placing deselects it again
            foreach (Vector2 point in CandidatePoints(PartCatalog.Get(kind).Placement))
            {
                if (editor.TryPlace(kind, point))
                {
                    return;
                }
            }
        }
    }

    /// <summary>The part just placed carries the gizmo and its tile is deselected: shift, turn and size are free and stored, shift and size clamped.</summary>
    private static bool ProbeCellGizmo(CellEditor editor)
    {
        int index  = editor.Draft.Parts.Count - 1;
        int points = editor.Points;
        bool isPicked = index >= 0 && index == editor.PickedPart && null == editor.Selected;
        bool isPosed  = editor.MovePickedPart(new Vector3(0f, 0.1f, 0f)) && editor.TurnPickedPart(Vector3.Up, 0.5f)
            && editor.Draft.Parts[index].Offset.Y > 0.05f && Mathf.Abs(editor.Draft.Parts[index].Rotation.Y) > 0.25f
            && editor.MovePickedPart(Vector3.Right * 9f) && editor.Draft.Parts[index].Offset.Length() <= CellDesign.MAX_PART_OFFSET + 0.001f;
        bool isSized  = editor.SetPartSize(5f) && Mathf.IsEqualApprox(editor.Draft.Parts[index].Size, CellDesign.MAX_PART_SIZE);
        editor.PickPart(-1);
        bool isCleared = !editor.MovePickedPart(Vector3.Up) && !editor.SetPartSize(1f);
        return isPicked && isPosed && isSized && isCleared && points == editor.Points;
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
    /// The first shaping: type cards first, then a marine mammal (streamlined starter with free
    /// fluke and eyes, the fluke removed again) with fluke and jaw
    /// (a tail fin of fish is refused), the fluke shifted and turned with the gizmo, single
    /// hands with symmetry off, one more vertebra, a thicker middle, a neck lifted and pulled
    /// out, the tail pushed in, and a sculpted limb as leg and as arm.
    /// </summary>
    private static bool ShapeCreature(CreatureEditor editor)
    {
        bool isChoosing  = editor.IsOpen && editor.IsChoosingType;
        bool isBlocked   = !editor.TryPlace(CreaturePartKind.Fluke, 0f);
        int  pointsType  = editor.Points;
        editor.ChooseType(CreatureType.MarineMammal);
        bool isFishOnly  = !editor.TryPlace(CreaturePartKind.CaudalFin, 0f);

        // Starter body: streamlined, free fluke and eyes; removing the free fluke refunds nothing
        bool isStarter   = 7 == editor.Draft.Girths.Count && 2 == editor.Draft.Parts.Count
            && 1 == editor.Draft.Count(CreaturePartKind.Fluke) && 1 == editor.Draft.Count(CreaturePartKind.Eye)
            && pointsType == editor.Points
            && editor.RemovePart(0) && pointsType == editor.Points;
        int  pointsStart = editor.Points;
        editor.Select(CreaturePartKind.Fluke);
        bool isPlaced    = editor.TryPlace(CreaturePartKind.Fluke, 0f) && null == editor.Selected   // placing deselects the tile
            && editor.TryPlace(CreaturePartKind.Jaw, 1f);
        bool isTailFull  = !editor.TryPlace(CreaturePartKind.Fluke, 0f);
        bool isPaid      = pointsStart - 4 == editor.Points;
        editor.PickPart(0);
        bool isSized     = editor.SetPartSize(5f) && Mathf.IsEqualApprox(editor.Draft.Parts[0].Size, CreatureDesign.MAX_PART_SIZE) && pointsStart - 4 == editor.Points;

        // Gizmo: shift and turn are free and stored on the part, the shift is clamped
        bool isPosed     = editor.MovePickedPart(new Vector3(0f, 0.1f, 0f)) && editor.TurnPickedPart(Vector3.Up, 0.5f)
            && Mathf.IsEqualApprox(editor.Draft.Parts[0].Offset.Y, 0.1f) && Mathf.IsEqualApprox(editor.Draft.Parts[0].Rotation.Y, 0.5f)
            && editor.MovePickedPart(Vector3.Right * 9f) && editor.Draft.Parts[0].Offset.Length() <= CreatureDesign.MAX_PART_OFFSET + 0.001f
            && pointsStart - 4 == editor.Points;

        // Symmetry off: one hand per flank at the same spot, each at half price and half weight
        float weightBefore = CreatureStats.From(editor.Draft).Weight;
        editor.SetSymmetric(false);
        bool isSingle    = editor.TryPlace(CreaturePartKind.Hands, 0.6f, side: 1) && editor.TryPlace(CreaturePartKind.Hands, 0.6f, side: -1)
            && !editor.TryPlace(CreaturePartKind.Hands, 0.6f, side: 1)
            && pointsStart - 6 == editor.Points
            && Mathf.IsEqualApprox(CreatureStats.From(editor.Draft).Weight - weightBefore, CreatureCatalog.Get(CreaturePartKind.Hands).Weight);
        editor.SetSymmetric(true);
        bool isSymmetric = editor.IsSymmetric && editor.RemovePart(editor.Draft.Parts.Count - 1) && pointsStart - 5 == editor.Points;
        bool isLonger    = editor.AddVertebra() && 8 == editor.Draft.Girths.Count;
        editor.ShowCategory(CreatureCategory.Feet);
        bool isTabbed    = CreatureCategory.Feet == editor.Category;
        editor.SetGirth(3, 5f);
        bool isClamped   = Mathf.IsEqualApprox(editor.Draft.Girths[3], CreatureDesign.MAX_GIRTH);

        // Spine: the nose lifted to a neck, the neighbor follows; the nose pulled out one vertebra, the eyes ride with the head
        int   nose       = editor.Draft.Girths.Count - 1;
        editor.SetLift(nose, 0.9f);
        bool  isBent     = Mathf.IsEqualApprox(editor.Draft.Lifts[nose], 0.9f) && editor.Draft.Lifts[nose - 1] >= 0.9f - 0.33f
            && editor.Draft.LiftAt(editor.Draft.NoseTipX) > 0.8f;
        int   eye        = IndexOf(editor.Draft, CreaturePartKind.Eye);
        float eyeX       = editor.Draft.XAt(editor.Draft.Parts[eye].T);
        int   pointsPull = editor.Points;
        bool  isPulled   = editor.PullEnd(isNose: true, isGrowing: true) && nose + 2 == editor.Draft.Girths.Count
            && Mathf.Abs(editor.Draft.XAt(editor.Draft.Parts[eye].T) - (eyeX + CreatureDesign.SPACING * 0.5f)) < 0.02f
            && editor.PullEnd(isNose: false, isGrowing: false) && nose + 1 == editor.Draft.Girths.Count
            && pointsPull == editor.Points;

        // Vertebra gizmo: push along and up, scale the girth, pull the nose far out for a new vertebra
        editor.PickVertebra(2);
        float xBefore     = editor.Draft.Positions[2];
        float girthBefore = editor.Draft.Girths[2];
        bool  isPushed    = editor.MovePickedVertebra(new Vector3(0.05f, 0.1f, 0f))
            && Mathf.IsEqualApprox(editor.Draft.Positions[2], xBefore + 0.05f)
            && editor.ScalePickedVertebra(0.8f)
            && Mathf.IsEqualApprox(editor.Draft.Girths[2], Mathf.Max(girthBefore * 0.8f, CreatureDesign.MIN_GIRTH));
        int   countBefore = editor.Draft.Girths.Count;
        editor.PickVertebra(countBefore - 1);
        editor.MovePickedVertebra(new Vector3(2f, 0f, 0f));
        isPushed = isPushed && countBefore + 1 == editor.Draft.Girths.Count && countBefore == editor.PickedVertebra
            && pointsPull == editor.Points;
        editor.PickVertebra(-1);

        // Sculpted limb: hangs to the ground by default and carries, pulled up it is an arm
        editor.ShowCategory(CreatureCategory.Limbs);
        bool isLimbPlaced = editor.TryPlace(CreaturePartKind.Limb, 0.15f);
        int  limb         = editor.Draft.Parts.Count - 1;
        bool isLeg        = isLimbPlaced && editor.Draft.IsWalkingLimb(editor.Draft.Parts[limb]) && CreatureStats.From(editor.Draft).Carry > 0f;
        if (isLimbPlaced)
        {
            editor.Draft.SetLimbJoint(limb, 2, new Vector3(0.4f, 0.4f, 0.4f));
        }
        Vector3 tip       = isLimbPlaced ? editor.Draft.Parts[limb].Joints[2] : Vector3.Zero;
        bool isArm        = isLimbPlaced && !editor.Draft.IsWalkingLimb(editor.Draft.Parts[limb]) && Mathf.IsZeroApprox(CreatureStats.From(editor.Draft).Carry)
            && (tip - editor.Draft.Parts[limb].Joints[1]).Length() <= CreatureDesign.MAX_LIMB_SEGMENT + 0.001f;

        // Ends: a claw paw sticks on the bare limb, it costs its price minus the limb (1 EP), the joints stay;
        // a limb with an end takes no second one
        int  pointsEnd  = editor.Points;
        int  limbPaid   = isLimbPlaced ? editor.Draft.Parts[limb].PaidCost : 0;
        int  endCost    = Mathf.Max(1, CreatureCatalog.Get(CreaturePartKind.Claws).Cost - limbPaid);
        bool isAttached = isLimbPlaced && editor.AttachEnd(limb, CreaturePartKind.Claws)
            && CreaturePartKind.Claws == editor.Draft.Parts[limb].Kind && 3 == editor.Draft.Parts[limb].Joints.Count
            && pointsEnd - endCost == editor.Points
            && !editor.AttachEnd(limb, CreaturePartKind.Hands);

        // Branch: a tentacle grows from the elbow of the claw arm; removing the arm takes it along, both refunded
        int  pointsBranch = editor.Points;
        int  armId        = isLimbPlaced ? editor.Draft.Parts[limb].Id : -1;
        int  branchCost   = isLimbPlaced ? CreatureCatalog.Get(CreaturePartKind.Tentacle).CostFor(editor.Draft.Parts[limb].Side) : 0;
        bool isBranched   = isLimbPlaced && editor.PlaceOnLimb(CreaturePartKind.Tentacle, limb, 1)
            && armId == editor.Draft.Parts[^1].ParentId && 1 == editor.Draft.Parts[^1].ParentJoint
            && pointsBranch - branchCost == editor.Points;
        int  partsBefore  = editor.Draft.Parts.Count;
        int  pointsBefore = editor.Points;
        int  refund       = isLimbPlaced ? editor.Draft.BranchCost(limb) : 0;
        isBranched = isBranched && editor.RemovePart(limb) && partsBefore - 2 == editor.Draft.Parts.Count
            && pointsBefore + refund == editor.Points && -1 == editor.Draft.IndexOfId(armId);

        // Every sculpted limb kind places with its own joint count: stub 1, bent 2, limb 3, tentacle 4
        var  limbProbe = CreatureDesign.Starter(CreatureType.Fish);
        bool isLimbSet = true;
        CreaturePartKind[] limbKinds = { CreaturePartKind.StubLimb, CreaturePartKind.BentLimb, CreaturePartKind.Limb, CreaturePartKind.Tentacle };
        for (int k = 0; k < limbKinds.Length; k++)
        {
            isLimbSet = isLimbSet && limbProbe.TryPlace(limbKinds[k], 0.15f + k * 0.2f, 0)
                && k + 1 == limbProbe.Parts[^1].Joints.Count && CreatureCatalog.Get(limbKinds[k]).IsSculpted;
        }
        // Paint: a pattern in a second color, free of charge
        int  pointsPaint = editor.Points;
        editor.PickPattern(CreaturePattern.Stripes);
        editor.PickPatternColor(new Color(0.2f, 0.15f, 0.3f));
        bool isPainted   = CreaturePattern.Stripes == editor.Draft.Pattern && new Color(0.2f, 0.15f, 0.3f) == editor.Draft.PatternColor
            && pointsPaint == editor.Points;

        // Spray can: a dot on the right flank plus its mirror, free; undo takes the stroke, wipe clears
        editor.SetSpraying(true);
        Vector3 flankHit  = new(0f, editor.Draft.LiftAt(0f), editor.Draft.FlankAt(0f) * 0.98f);
        bool    isSprayed = editor.IsSpraying && editor.SprayAt(flankHit) && 2 == editor.Draft.Splats.Count
            && Mathf.Abs(editor.Draft.SplatPoint(editor.Draft.Splats[0]).Z - flankHit.Z) < 0.05f
            && pointsPaint == editor.Points
            && editor.UndoSpray() && 0 == editor.Draft.Splats.Count;
        editor.SprayAt(flankHit);
        editor.ClearSpray();
        isSprayed = isSprayed && 0 == editor.Draft.Splats.Count;
        editor.SetSpraying(false);
        bool isCoral     = Player.ORGANISM_COLOR == editor.DraftColor;
        int  pointsTranslucent = editor.Points;
        editor.PickColor(EditorStyle.TRANSLUCENT_COLOR);
        bool isTranslucent     = EditorStyle.TRANSLUCENT_COLOR == editor.DraftColor && pointsTranslucent == editor.Points;
        editor.Finish();
        return isChoosing && isBlocked && isFishOnly && isPlaced && isTailFull && isStarter && isPaid && isSized && isPosed && isSingle && isSymmetric
            && isLonger && isTabbed && isClamped && isBent && isPulled && isPushed && isLeg && isArm && isLimbSet && isAttached && isBranched && isPainted && isSprayed && isCoral && isTranslucent && !editor.IsOpen;
    }

    private static int IndexOf(CreatureDesign design, CreaturePartKind kind)
    {
        for (int i = 0; i < design.Parts.Count; i++)
        {
            if (kind == design.Parts[i].Kind) return i;
        }
        return -1;
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

        // A piece of food right on the hunter, the cell eats it without the player
        _grazedFood = new Food { Position = position };
        _grazedFood.Setup(size * 0.2f, FoodKind.Sugar);
        _main.FoodLayer.AddChild(_grazedFood);
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
    //   Game Over               A deadly hit on a single cell ends the game: Died fires, the body stops, biomass stays.
    //   Größere Beute           Each division lowers the eat ratio by 0.05 (1.15 -> 0.85 at 64 cells).
    //   Spiel nicht pausiert    No choice panel is left open.
    //   Futter gespawnt         Food exists.
    //   Zellen gespawnt         Critters exist.
    //   Zellen fressen Futter   A cell eats the food it touches.
    //   Zellen fressen Zellen   The hunter ate the prey.
    //   Umfließen               A swallowed cell is halfway in and shrunk at half time, at the center and digested at the end.
    //   Verdaut werden          Digestion inside a bigger cell costs health, but no push back and no grace period.
    //   Gegner-Teile            NPC cells carry editor parts by kind, more with the stage, none as organisms; a hunter's spike hurts.
    //   3D-Kamera aktiv         The viewport renders through a Camera3D.
    //   3D-Zellhaufen           The player visual holds at least one node per cell.
    //   Haufen-Layout           1 cell in the middle, 2 side by side, 64 cells within an extent of 10.
    //   Form & Farbe            The outline passes through its radii and clamps, the color reaches the player.
    //   Beute < 20% Biomasse    A large prey is worth less than 20% of the own biomass.
    //   Schwimmtempo            SwimSpeed at the reference size is exactly 56.25.
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
    //   Stufen-Cutscene         The stage cutscene fires Finished and puts the camera back where it was.
    //   Editor-Übergang         The editor transition fires Finished once, puts the camera back and frees itself.
    //   Lebewesen               64 cells evolve: editor opens, the player becomes an organism, game resumes.
    //   Kreatur-Editor          Type first, fish parts refused, costs paid, one part per tail, part size clamped and free, tab switch, girth clamped,
    //                           coral by default, translucent picked for free.
    //   Kreatur im Spiel        One body of the same size, creature visual, cell visual hidden, no division, translucent color kept.
    //   Kreatur-Werte           Fluke faster than none, jaw bites forward, spines guard flanks and back.
    //   Meer voller Kreaturen   NPCs of the organism stage are creatures.
    //   Kreatur-Geometrie       Blob profile and inside test, vertebra limits, NPC body plans.
    //   Texte Kreatur           Every creature part and type translated in both languages.
    //   Küste                   The coast exists, land and sea are on the right sides, things get pushed to sea.
    //   Landtier per Cheat      F10 points add 10 EP, the land jump walks the creature onto the coast: stage 5.
    //   Luft an Land            A fish without lung runs out of air on land and suffocates slowly, a mammal does not.
    //   Laufen an Land          Without legs it crawls, with legs it walks faster, lung and legs make it a land animal.
    //   Gewicht                 Blob about 10 kg, parts add weight, a heavy body walks slower, more legs carry more.
    //   Fliegen                 A blob with one feather pair is too heavy, a slim body with two pairs could fly.
    //   Hirn                    A big brain adds 2 EP per level (4 instead of 2).
    //   Teil-Modelle            Every Blender part model (creature and cell) loads with meshes and its looping animation.
    //   Grafikstufen            The tier cycles Low, Medium, High, owners hear the change, depth blur only above Low.
    //   Wasserfarben            The water follows the stage: primordial, pond, sea; the lens and the depth fog are set.
    //

    /// <summary>
    /// Graphics tiers: Low, Medium and High cycle (the OpenGL fallback only has Low), every
    /// switch raises Changed, depth of field exists above Low only. The tier of the run is restored.
    /// </summary>
    private static bool ProbeGraphicsTiers()
    {
        GraphicsTier before  = GraphicsQuality.Tier;
        int          changes = 0;
        void Count() => changes++;
        GraphicsQuality.Changed += Count;

        GraphicsQuality.Use(GraphicsTier.Low);
        bool isLowPlain = GraphicsTier.Low == GraphicsQuality.Tier && !GraphicsQuality.HasDepthOfField;
        bool isCycling  = GraphicsQuality.IsFallback
            ? GraphicsTier.Low == GraphicsQuality.Next()
            : GraphicsTier.Medium == GraphicsQuality.Next();
        GraphicsQuality.Use(GraphicsQuality.Next());
        bool isBlurred = GraphicsQuality.IsFallback || GraphicsQuality.HasDepthOfField;
        GraphicsQuality.Use(GraphicsTier.High);
        bool isAround = GraphicsTier.Low == GraphicsQuality.Next();

        GraphicsQuality.Changed -= Count;
        GraphicsQuality.Use(before);
        return isLowPlain && isCycling && isBlurred && isAround && 3 == changes;
    }

    /// <summary>
    /// Water colors: every early stage has its own palette, the sea from the cluster on, a
    /// blend lies between two palettes, and the running game has its depth fog switched on.
    /// </summary>
    private bool ProbeWater()
    {
        bool isStaged = SoupPalette.PRIMORDIAL == SoupPalette.For(0)
            && SoupPalette.POND == SoupPalette.For(Player.SINGLE_STAGE)
            && SoupPalette.SEA == SoupPalette.For(Player.MULTICELLULAR_STAGE)
            && SoupPalette.SEA == SoupPalette.For(Player.LAND_STAGE);
        SoupPalette half = SoupPalette.PRIMORDIAL.Lerp(SoupPalette.SEA, 0.5f);
        bool isBlended = half.Background.IsEqualApprox(SoupPalette.PRIMORDIAL.Background.Lerp(SoupPalette.SEA.Background, 0.5f))
            && SoupPalette.SEA.Light.IsEqualApprox(SoupPalette.PRIMORDIAL.Lerp(SoupPalette.SEA, 2f).Light);
        Godot.Collections.Array<Node> worlds = _main.FindChildren("*", nameof(WorldEnvironment), recursive: true, owned: false);
        bool isFogged = worlds.Any(world => world.GetParent() is FollowCamera && ((WorldEnvironment)world).Environment.FogEnabled);
        return isStaged && isBlended && isFogged;
    }
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
            ("Game Over", ProbeSingleDeath()),
            ("Größere Beute", ProbeEatRatio()),
            ("Spiel nicht pausiert", !GetTree().Paused),
            ("Futter gespawnt", _main.FoodLayer.GetChildCount() > 0),
            ("Zellen gespawnt", _main.CritterLayer.GetChildCount() > 0),
            ("Zellen fressen Zellen", _isPreySpawned && !IsInstanceValid(_prey)),
            ("Zellen fressen Futter", _isPreySpawned && !IsInstanceValid(_grazedFood)),
            ("Umfließen", ProbeEngulf()),
            ("Verdaut werden", ProbeDigest()),
            ("Gegner-Teile", ProbeCritterParts()),
            ("3D-Kamera aktiv", null != GetViewport().GetCamera3D()),
            ("3D-Zellhaufen", null != player.Visual && player.Visual.GetChildCount() >= player.CellCount + 1),
            ("Haufen-Layout", ProbeClusterLayout()),
            ("Form & Farbe", ProbeShapeAndColor()),
            ("Beute < 20% Biomasse", _main.KillReward(player.BodySize() * 0.85f) < player.Biomass * 0.2f),
            ("Schwimmtempo", Mathf.IsEqualApprox(CellBody.SwimSpeed(CellBody.REFERENCE_SIZE), 56.25f)),
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
            ("Rim-Höhe", ProbeRimLift()),
            ("Drop-Vorschau", _isDropPreviewOk),
            ("Zell-Gizmo", _isCellGizmoOk),
            ("Titelmenü", _isMenuShown && !_main.IsInMenu),
            ("Intro endet", ProbeIntro()),
            ("RNA-Welt", ProbePrelude()),
            ("Mutationen", ProbeMutations()),
            ("Evolve-Cutscene", ProbeEvolveCutscene()),
            ("Stufen-Cutscene", ProbeStageCutscene()),
            ("Editor-Übergang", ProbeEditorTransition()),
            ("Lebewesen", _isEvolveOk),
            ("Kreatur-Editor", _isCreatureEditorOk),
            ("Kreatur im Spiel", ProbeOrganismPlayer(player)),
            ("Kreatur-Werte", ProbeCreatureStats()),
            ("Meer voller Kreaturen", ProbeCreatureSea()),
            ("Kreatur-Geometrie", ProbeCreatureGeometry()),
            ("Texte Kreatur", ProbeCreatureTexts("en") && ProbeCreatureTexts("de")),
            ("Küste", ProbeCoast()),
            ("Küste nach Evolve-Biomasse", player.EvolvedAt >= GROW_BIOMASS && _main.CoastAt > player.EvolvedAt),
            ("Landtier per Cheat", _isCheatPointsOk && player.IsLandAnimal && player.IsOnLand),
            ("Third Person", ProbeThirdPerson()),
            ("Futter Meer/Land", ProbeFoodHabitats()),
            ("Bäume", ProbeTrees()),
            ("Luft an Land", ProbeAir()),
            ("Laufen an Land", ProbeWalking()),
            ("Gewicht", ProbeWeight()),
            ("Fliegen", ProbeFlight()),
            ("Hirn", ProbeBrain()),
            ("Teil-Modelle", ProbeModels()),
            ("Grafikstufen", ProbeGraphicsTiers()),
            ("Wasserfarben", ProbeWater()),
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
    //   ProbeRimLift          Spikes above each other at one angle fit, the same spot twice slides aside, lift clamps at the bottom pole.
    //   ProbeDropPreview      Ghost on a free rim spot, gone after placing, slid on the taken spot; a height handle raises the back.
    //   ProbeIntro            One long frame past the end: Finished fired, camera at CameraEnd.
    //   ProbePrelude          RNA pairing, a mutation, the goal, evolve, bites (far miss, chain hit) and the genesis.
    //   ProbeMutations        Tough and Sluggish on a fresh protocell: HP, EP and speed.
    //   ProbeClusterLayout    Concrete cell positions of 1 and 2 cells, size of the 64 cell cluster.
    //   ProbeEvolveCutscene   One long frame past the end: Finished fired, 512 cells shown.
    //   ProbeStageCutscene    One long frame past the end: Finished fired, camera back at its start pose.
    //   ProbeEditorTransition One long frame past the end: Finished fired, camera back, queued for freeing.
    //   ProbeOrganismPlayer   Stage 4, one body, creature visual shown, cells hidden, no more division.
    //   ProbeCreatureStats    Loose organisms: fluke speed, jaw and spine directions.
    //   OrganismProbe         A loose player grown to 64 cells and turned into an organism.
    //   ProbeCreatureSea      Every NPC in the sea carries a creature body plan.
    //   ProbeCreatureGeometry Blob radius, inside test, vertebra limits, NPC plans.
    //   ProbeCreatureTexts    No creature key left untranslated.
    //   ProbeCoast            Land depth of concrete points, ToSea, the coast of the run.
    //   ProbeAir              Twelve seconds on land: fish without lung 0 air and hurt, mammal full air.
    //   ProbeWalking          Crawl vs. walk speed on land, the step onto land advances the stage.
    //   ProbeWeight           Blob weight, part weight, load slows walking, a second pair speeds it up.
    //   ProbeFlight           Flight ratio of a blob with one feather pair and a slim body with two.
    //   ProbeBrain            EP of one level up with and without a big brain.
    //   ProbeModels           Every entry of CreatureVisual3D.MODELS and CellVisual3D.CELL_MODELS: meshes and
    //                         the named animation, every editor part has a cell model.
    //   ProbeShapeAndColor    Stretched front 1.4, squashed back 0.8, clamp at 1.5, heights 1.6 / clamp 0.5, copied, color reaches the player.
    //   ProbeSingleDeath      A loose single cell dies: Died fires once, IsDead, biomass kept, no more damage.
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

    private static bool ProbeRimLift()
    {
        var  design     = new CellDesign();
        bool isEquator  = design.TryPlace(PartKind.Spike, new Vector2(1f, 0f), paidCost: 1);
        bool isTop      = design.TryPlace(PartKind.Spike, new Vector2(1f, 0f), paidCost: 1, lift: 0.8f);
        // A click on a taken spot slides to the closest free one, clear of both spikes
        bool isSlid     = design.TryPlace(PartKind.Spike, new Vector2(1f, 0.05f), paidCost: 1, lift: 0.8f)
            && design.Parts[^1].Direction.AngleTo(design.Parts[0].Direction) > 0.3f
            && design.Parts[^1].Direction.AngleTo(design.Parts[1].Direction) > 0.3f;
        bool isClamped  = design.TryPlace(PartKind.Spike, new Vector2(-1f, 0f), paidCost: 1, lift: -1.5f)
            && Mathf.IsEqualApprox(design.Parts[^1].Lift, -CellDesign.MAX_RIM_LIFT);
        return isEquator && isTop && isSlid && isClamped && 4 == design.Parts.Count;
    }

    /// <summary>Runs in the first editor session, removes its spike again so the session stays untouched.</summary>
    private static bool ProbeDropPreview(CellEditor editor)
    {
        Vector2 spot      = new(-0.6f, -0.8f);
        bool    isShown   = editor.PreviewPart(PartKind.Spike, spot) && PartKind.Spike == editor.GhostKind;
        bool    isPlaced  = editor.TryPlace(PartKind.Spike, spot);
        bool    isCleared = null == editor.GhostKind;
        bool    isSlid    = editor.PreviewPart(PartKind.Spike, spot) && PartKind.Spike == editor.GhostKind;
        editor.RemoveAt(Vector2.FromAngle(spot.Angle()));

        // Height handle at the back: raises the back, the front stays, back down again
        editor.RaiseHeight(4, 0.5f);
        bool isRaised = Mathf.IsEqualApprox(editor.Draft.Heights[4], 1.5f) && Mathf.IsEqualApprox(editor.Draft.Heights[0], 1f);
        editor.RaiseHeight(4, -0.5f);
        return isShown && isPlaced && isCleared && isSlid && isRaised;
    }

    private bool ProbeEngulf()
    {
        var eater = new CellBody { Radius = 2f, IsHidden = true };
        AddChild(eater);
        var prey = new Critter { Position = new Vector2(3f, 0f), IsHidden = true };
        prey.Setup(1f, CritterKind.Bacterium, 2f);
        AddChild(prey);

        prey.StartEngulf(eater);
        bool isHalfway = !prey.AdvanceEngulf(0.7f, 1.4f)
            && prey.Position.X > 1.3f && prey.Position.X < 1.7f
            && Mathf.IsEqualApprox(prey.Radius, 0.65f);
        bool isDigested = prey.AdvanceEngulf(0.7f, 1.4f)
            && prey.Position.Length() < 0.01f
            && Mathf.IsEqualApprox(prey.Radius, 0.3f)
            && Mathf.IsEqualApprox(prey.EngulfRadius, 1f);
        prey.QueueFree();
        eater.QueueFree();
        return isHalfway && isDigested;
    }

    private static bool ProbeDigest()
    {
        var probe = new Player();
        probe.AddBiomass(100f, BiomassSource.Raw);
        probe.Invulnerable = 0f;
        float hp = probe.Hp;
        probe.Digest(5f, probe.Position + Vector2.Right);
        bool isDigested = Mathf.IsEqualApprox(probe.Hp, hp - 5f)
            && probe.Velocity.IsZeroApprox()
            && Mathf.IsZeroApprox(probe.Invulnerable);
        probe.Free();
        return isDigested;
    }

    private static bool ProbeCritterParts()
    {
        // Random designs: ten rolls, every one must follow the rules
        bool isByKind = true;
        for (int i = 0; i < 10 && isByKind; i++)
        {
            CellDesign hunterEarly = CellDesign.ForCritter(CritterKind.Hunter, 0);
            CellDesign hunterLate  = CellDesign.ForCritter(CritterKind.Hunter, 2);
            isByKind = CellDesign.ForCritter(CritterKind.Bacterium, 1).Count(PartKind.Flagellum) >= 1
                && CellDesign.ForCritter(CritterKind.Amoeba, 1).Count(PartKind.Cilia) >= 1
                && hunterLate.Count(PartKind.Spike) >= 1
                && hunterLate.Parts.Count > hunterEarly.Parts.Count
                && 0 == CellDesign.ForCritter(CritterKind.Hunter, Player.ORGANISM_STAGE).Parts.Count;
        }

        // A hunter's spike hurts towards its direction (facing 0: local = world)
        var hunter = new Critter();
        hunter.Setup(20f, CritterKind.Hunter, 20f, 1);
        float spikeAngle = hunter.Design.Parts.First(part => PartKind.Spike == part.Kind).Angle;
        bool  isArmed    = hunter.ContactDamageToward(spikeAngle) >= Balance.Current.Cell.Combat.SpikeDamage;
        hunter.Free();
        return isByKind && isArmed;
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

    private bool ProbePrelude()
    {
        var camera = new Camera3D();
        AddChild(camera);
        var  prelude    = new RnaWorld { Camera = camera, Center = Vector2.Zero, Size = 1f };
        bool isFinished = false;
        int  mutations  = -1;
        prelude.Evolving += (_, kinds) => mutations = kinds.Length;
        prelude.Finished += () => isFinished = true;
        AddChild(prelude);

        // Two paired start links, A-U and G-C pair, a wrong base sticks as a mutation
        bool isStartOk    = 2 == prelude.PairedCount && RnaBase.U == RnaWorld.Pair(RnaBase.A) && RnaBase.G == RnaWorld.Pair(RnaBase.C);
        bool isMutationOk = !prelude.Attach(RnaWorld.Pair(prelude.Wanted)) && 1 == prelude.MutationCount && 2 == prelude.PairedCount && 1 == prelude.Mutations.Count;

        // No evolving before the goal
        bool isEarlyEvolveBlocked = !prelude.Evolve() && !prelude.IsEvolving;
        while (!prelude.IsGoalReached)
        {
            prelude.Attach(prelude.Wanted);
        }
        bool isGoalOk = RnaWorld.GOAL_LINKS == prelude.PairedCount && RnaWorld.GOAL_LINKS + 1 == prelude.LinkCount;

        // A far bite misses, a bite into the second-to-last paired link (straight behind the head) breaks off three links, the mutation stays
        bool isBreakOk = 0 == prelude.BreakAt(new Vector2(100f, 100f), 0.5f)
            && 3 == prelude.BreakAt(new Vector2(0f, RnaWorld.LINK_SPACING * (RnaWorld.GOAL_LINKS - 1)), 0.01f)
            && RnaWorld.GOAL_LINKS - 2 == prelude.LinkCount
            && 1 == prelude.MutationCount;

        // Back at the goal: evolve once (a second press does nothing), the cell forms, a long frame ends it
        while (!prelude.IsGoalReached)
        {
            prelude.Attach(prelude.Wanted);
        }
        var  end           = new Transform3D(Basis.Identity, new Vector3(0f, 30f, 10f));
        prelude.Evolving  += (_, _) => prelude.CameraEnd = end;
        bool isEvolveOk    = prelude.Evolve() && prelude.IsEvolving && !prelude.Evolve() && 1 == mutations && !isFinished;
        prelude._Process(RnaGenesis.DURATION + 0.1);
        bool isGenesisOk   = isFinished && camera.GlobalTransform.Origin.IsEqualApprox(end.Origin);
        prelude.Skip();
        prelude.QueueFree();
        camera.QueueFree();
        return isStartOk && isMutationOk && isEarlyEvolveBlocked && isGoalOk && isBreakOk && isEvolveOk && isGenesisOk;
    }

    private bool ProbeMutations()
    {
        // Tough (+5 % HP) and Sluggish (-3 % speed) on a fresh protocell: 50 * 1.05 HP, two EP more, slower
        var probe = new Player();
        AddChild(probe);
        int   pointsBefore = probe.EvolutionPoints;
        float speedBefore  = probe.CurrentSpeed();
        probe.ApplyMutations(new[] { MutationKind.Tough, MutationKind.Sluggish });
        bool isOk = Mathf.IsEqualApprox(probe.MaxHp, 52.5f)
            && pointsBefore + 2 * Balance.Current.Prelude.EpPerMutation == probe.EvolutionPoints
            && Mathf.IsEqualApprox(probe.CurrentSpeed(), speedBefore * 0.97f)
            && 2 == probe.Mutations.Count;
        probe.QueueFree();
        return isOk;
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
        bool isOk = hasDied && probe.IsDead && Mathf.IsEqualApprox(probe.Biomass, biomass) && !probe.TakeDamage(1f, Vector2.Right);
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
        var heights = new float[CellDesign.SHAPE_POINTS];
        System.Array.Fill(heights, 1f);
        heights[0] = 0.1f;
        heights[4] = 1.6f;
        var design = new CellDesign();
        design.SetShape(radii);
        design.SetHeights(heights);
        design.SetColor(new Color(1f, 0f, 0f));
        var copy = new CellDesign();
        copy.CopyFrom(design);
        bool isShaped = Mathf.IsEqualApprox(copy.HeightScale(Mathf.Pi), 1.6f)
            && Mathf.IsEqualApprox(copy.Heights[0], CellDesign.MIN_HEIGHT)
            && Mathf.IsEqualApprox(copy.TopHeight(Vector2.Zero), CellVisual3D.FLATTEN * copy.Heights.Average());

        var probe = new Player();
        probe.CommitDesign(copy, 0);
        bool isColored = probe.BodyColor.IsEqualApprox(new Color(1f, 0f, 0f));
        probe.Free();
        return Mathf.IsEqualApprox(copy.ShapeRadius(0f), 1.4f)
            && Mathf.IsEqualApprox(copy.ShapeRadius(Mathf.Pi), 0.8f)
            && Mathf.IsEqualApprox(copy.Shape[2], CellDesign.MAX_SHAPE)
            && isShaped
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

    private bool ProbeStageCutscene()
    {
        var camera = new Camera3D();
        AddChild(camera);
        var start = new Vector3(0f, 9f, 4f);
        camera.Position = start;
        var  cutscene   = new StageCutscene { Camera = camera, Center = Vector3.Zero, Size = 1f, Stage = Player.MULTICELLULAR_STAGE };
        bool isFinished = false;
        cutscene.Finished += () => isFinished = true;
        AddChild(cutscene);

        cutscene._Process(0.6);
        bool isDiving = camera.Position.DistanceTo(Vector3.Zero) < start.Length() * 0.8f;
        cutscene._Process(10.0);
        bool isBack = camera.Position.IsEqualApprox(start);
        cutscene.QueueFree();
        camera.QueueFree();
        return isFinished && isDiving && isBack;
    }

    private bool ProbeEditorTransition()
    {
        var camera = new Camera3D();
        AddChild(camera);
        var start = new Vector3(0f, 9f, 4f);
        camera.Position = start;
        var  transition = new EditorTransition { Camera = camera, Center = Vector3.Zero, Size = 1f };
        int  finished   = 0;
        transition.Finished += () => finished++;
        AddChild(transition);
        transition._Process(10.0);
        transition._Process(0.1);
        bool isBack  = camera.Position.IsEqualApprox(start);
        bool isFreed = transition.IsQueuedForDeletion();
        camera.QueueFree();
        return 1 == finished && isBack && isFreed;
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
            && EditorStyle.TRANSLUCENT_COLOR == player.BodyColor
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

        // A shell damps hits at the back, a beak gets more out of plants than a bare mouth
        Player grazer  = OrganismProbe(CreatureType.Fish, CreaturePartKind.Beak, CreaturePartKind.Shell);
        Player bare    = OrganismProbe(CreatureType.Fish);
        float  before  = grazer.Biomass;
        float  beforeBare = bare.Biomass;
        grazer.AddBiomass(10f, BiomassSource.Plant);
        bare.AddBiomass(10f, BiomassSource.Plant);
        bool isGrazer = grazer.DamageFactorFrom(Mathf.Pi) < 1f && Mathf.IsEqualApprox(grazer.DamageFactorFrom(0f), 1f)
            && Mathf.IsEqualApprox(bare.DamageFactorFrom(Mathf.Pi), 1f)
            && grazer.Biomass - before > bare.Biomass - beforeBare;
        grazer.Free();
        bare.Free();
        return isFaster && isArmed && isGrazer;
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
        foreach (string key in new[] { "CREATURE_TYPE_FISH_NAME", "CREATURE_TYPE_MARINEMAMMAL_DESC", "ORGANISM_BUTTON", "GOAL_ORGANISM_AT", "CREATURE_STAT_WEIGHT", "CREATURE_FLIGHT_HEAVY" })
        {
            isOk = isOk && key != Tr(key);
        }
        foreach (CreatureCategory category in System.Enum.GetValues<CreatureCategory>())
        {
            string key = $"CREATURE_CATEGORY_{category}".ToUpperInvariant();
            isOk = isOk && key != Tr(key);
        }
        TranslationServer.SetLocale("de");
        return isOk;
    }

    /*
      One island at the origin, radius 10, seed 0: towards angle 0 every bulge is sin(0) = 0,
      so the shore lies exactly at (10, 0). The archipelago of the game: several islands,
      shores at least MIN_GAP apart, the landing point on the shore of the first one.
    */
    private bool ProbeCoast()
    {
        var coast = new Coast { Unit = 1f };
        coast.AddIsland(new Coast.Island(Vector2.Zero, 10f, 0f), new Vector2(-20f, 0f));
        bool isSidesOk = Mathf.IsEqualApprox(coast.LandDepth(new Vector2(2f, 0f)), 8f)
            && coast.IsOnLand(new Vector2(9f, 0f)) && !coast.IsOnLand(new Vector2(11f, 0f));
        bool isToSeaOk = coast.ToSea(new Vector2(4f, 0f), 1f).IsEqualApprox(new Vector2(11f, 0f))
            && coast.ToSea(new Vector2(30f, 2f), 1f).IsEqualApprox(new Vector2(30f, 2f));
        bool isLandingOk = Mathf.IsZeroApprox(coast.LandDepth(coast.Origin)) && coast.Normal.IsEqualApprox(Vector2.Right);
        coast.Free();

        Coast game        = _main.Coast;
        bool  isScattered = null != game && game.Islands.Count >= 3;
        for (int i = 0; isScattered && i < game.Islands.Count; i++)
        {
            for (int j = i + 1; j < game.Islands.Count; j++)
            {
                Coast.Island a = game.Islands[i];
                Coast.Island b = game.Islands[j];
                isScattered = isScattered && a.Center.DistanceTo(b.Center) > (a.Radius + b.Radius) * 1.25f;
            }
        }
        if (!isSidesOk || !isToSeaOk || !isLandingOk || !isScattered)
        {
            GD.Print($"  coast: sides {isSidesOk}, to sea {isToSeaOk}, landing {isLandingOk}, archipelago {isScattered}");
        }
        return isSidesOk && isToSeaOk && isLandingOk && isScattered;
    }

    /// <summary>After the evolution no soup molecules are left: food is sea food, on the islands land food on the ground.</summary>
    private bool ProbeFoodHabitats()
    {
        List<Food> foods  = _main.FoodLayer.GetChildren().OfType<Food>().Where(food => !food.IsQueuedForDeletion()).ToList();
        bool       isOk   = foods.Count > 0 && foods.All(food => FoodHabitat.Soup != food.Habitat);
        bool       isLand = foods.Where(food => FoodHabitat.Land == food.Habitat).All(food => _main.Coast.IsOnLand(food.Position) || food.IsMeat);
        GD.Print($"  food: {foods.Count(food => FoodHabitat.Sea == food.Habitat)} sea, {foods.Count(food => FoodHabitat.Land == food.Habitat)} land");
        return isOk && isLand;
    }

    /*
      The landing island holds trees: seeded (the same cell twice gives the same trees),
      on land, sizes from young to giant, several species, and a trunk pushes a body out.
    */
    private bool ProbeTrees()
    {
        Coast coast = _main.Coast;
        var   trees = new List<Coast.Tree>();
        for (int x = -15; x <= 15; x++)
        {
            for (int y = -15; y <= 15; y++)
            {
                var cell = new Vector2I(Mathf.FloorToInt(coast.Islands[0].Center.X / (16f * coast.Unit)) + x, Mathf.FloorToInt(coast.Islands[0].Center.Y / (16f * coast.Unit)) + y);
                trees.AddRange(coast.TreesIn(cell));
            }
        }
        var  probe       = new Vector2I(3, 5);
        bool isSeeded    = coast.TreesIn(probe).SequenceEqual(coast.TreesIn(probe));
        bool isOnLand    = trees.All(tree => coast.IsOnLand(tree.Position));
        int  species     = trees.Select(tree => tree.Species).Distinct().Count();
        bool isSized     = trees.All(tree => tree.Height >= 1.5f * Coast.NATURE_SCALE * coast.Unit && tree.Height <= 25f * Coast.NATURE_SCALE * coast.Unit);
        // Trees standing close push a body back and forth, so most (not all) trunks must clear it
        int  blocked     = trees.Count(tree => coast.PushOutOfTrees(tree.Position + new Vector2(0.1f, 0f), coast.Unit).DistanceTo(tree.Position) >= tree.TrunkRadius + coast.Unit * 0.99f);
        bool isBlocking  = trees.Count > 0 && blocked >= trees.Count * 0.95f;
        GD.Print($"  trees: {trees.Count} on the landing island, {species} species, seeded {isSeeded}, on land {isOnLand}, sized {isSized}, blocking {blocked}/{trees.Count}");
        return trees.Count > 40 && isSeeded && isOnLand && species >= 4 && isSized && isBlocking;
    }

    /// <summary>The land animal sees the world from behind: camera blending, the planet bends, keys turn with the camera.</summary>
    private bool ProbeThirdPerson()
    {
        bool isBlending = _main.ThirdPerson > 0f && Plane3D.Curve > 0f && Plane3D.Drop(Plane3D.CurveOrigin + new Vector2(1000f, 0f)) > 0f;
        // Camera east of the player (yaw pi/2, InputTurn -pi/2): W walks west
        bool isTurned = new Vector2(0f, -1f).Rotated(-Mathf.Pi * 0.5f).IsEqualApprox(new Vector2(-1f, 0f));

        // Walking a curve: after a few seconds the camera sits behind the new facing (keys turn with it)
        Player player = _main.Player;
        _main._Process(5.0);
        float facing = player.Facing + 0.8f;
        for (int i = 0; i < 60; i++)
        {
            player.Facing   = facing;
            player.Velocity = Vector2.FromAngle(facing) * player.CurrentSpeed();
            _main._Process(0.1);
        }
        Vector2 behind      = -Vector2.FromAngle(facing);
        float   yawBehind   = Mathf.Atan2(behind.X, behind.Y);
        bool    isFollowing = Mathf.Abs(Mathf.Wrap(player.InputTurn + yawBehind, -Mathf.Pi, Mathf.Pi)) < 0.05f;
        if (!isFollowing)
        {
            GD.Print($"  camera follow: input turn {player.InputTurn:F2}, expected {-yawBehind:F2}");
        }
        return isBlending && isTurned && isFollowing;
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
        bool isCrawling  = Mathf.IsEqualApprox(crawler.CurrentSpeed(), CellBody.SwimSpeed(crawler.BodySize()) * Balance.Current.Creature.Land.CrawlSpeed);
        bool isWalking   = walker.CurrentSpeed() > crawler.CurrentSpeed();
        walker._PhysicsProcess(0.1);
        bool isLandStage = walker.IsLandAnimal && !crawler.IsLandAnimal;
        crawler.Free();
        walker.Free();
        return isCrawling && isWalking && isLandStage;
    }

    private static bool ProbeWeight()
    {
        CreatureStats blob      = CreatureStats.From(CreatureDesign.Blob());
        CreatureStats withLegs  = StatsWith(CreatureDesign.Blob(), CreaturePartKind.Legs);
        CreatureStats twoPairs  = StatsWith(CreatureDesign.Blob(), CreaturePartKind.Legs, CreaturePartKind.Legs);
        CreatureStats stubby    = StatsWith(CreatureDesign.Blob(), CreaturePartKind.StubbyLegs);
        CreatureDesign heavy    = CreatureDesign.Blob();
        while (heavy.AddVertebra()) { }
        for (int i = 0; i < heavy.Girths.Count; i++)
        {
            heavy.SetGirth(i, CreatureDesign.MAX_GIRTH);
        }
        CreatureStats heavyLegs = StatsWith(heavy, CreaturePartKind.Legs);
        GD.Print($"  weight blob {blob.Weight:F1} kg, legs {withLegs.Weight:F1} kg at {withLegs.LandSpeed:F2}, heavy {heavyLegs.Weight:F1} kg at {heavyLegs.LandSpeed:F2}, two pairs {twoPairs.LandSpeed:F2}");
        return blob.Weight is > 8f and < 12f
            && Mathf.IsEqualApprox(withLegs.Weight, blob.Weight + CreatureCatalog.Get(CreaturePartKind.Legs).Weight)
            && Mathf.IsEqualApprox(blob.LandSpeed, Balance.Current.Creature.Land.CrawlSpeed)
            && heavyLegs.LandSpeed < withLegs.LandSpeed
            && twoPairs.LandSpeed > withLegs.LandSpeed
            && stubby.Carry > withLegs.Carry;
    }

    private static bool ProbeFlight()
    {
        CreatureStats blob = StatsWith(CreatureDesign.Blob(), CreaturePartKind.FeatherWings);
        CreatureDesign slim = CreatureDesign.Blob();
        while (slim.RemoveVertebra()) { }
        for (int i = 0; i < slim.Girths.Count; i++)
        {
            slim.SetGirth(i, CreatureDesign.MIN_GIRTH);
        }
        CreatureStats flyer = StatsWith(slim, CreaturePartKind.FeatherWings, CreaturePartKind.FeatherWings);
        GD.Print($"  flight blob {blob.FlightRatio:F2}, slim {flyer.FlightRatio:F2}");
        return !blob.CanFly && flyer.CanFly && Mathf.IsZeroApprox(CreatureStats.From(CreatureDesign.Blob()).Lift);
    }

    private static bool ProbeBrain()
    {
        int EpOfOneLevel(params CreaturePartKind[] kinds)
        {
            Player probe  = OrganismProbe(CreatureType.Fish, kinds);
            int    before = probe.EvolutionPoints;
            probe.AddBiomass(probe.NextLevelAt - probe.Biomass + 1f, BiomassSource.Raw);
            int    gained = probe.EvolutionPoints - before;
            probe.Free();
            return gained;
        }
        return Balance.Current.Player.EpPerLevel == EpOfOneLevel() && Balance.Current.Player.EpPerLevel + 2 == EpOfOneLevel(CreaturePartKind.BigBrain);
    }

    private static bool ProbeModels()
    {
        bool isOk = true;
        foreach ((CreaturePartKind kind, (string file, string animation)) in CreatureVisual3D.MODELS)
        {
            Node3D model     = CreatureVisual3D.NewModel(kind, Colors.Tan);
            bool   hasMeshes = model.FindChildren("*", nameof(MeshInstance3D), true, false).Count > 0;
            var    player    = model.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().FirstOrDefault();
            bool   hasLoop   = null == animation || (null != player && player.HasAnimation(animation));
            if (!hasMeshes || !hasLoop)
            {
                GD.Print($"  model {file} of {kind}: meshes {hasMeshes}, animation '{animation}' {hasLoop}");
                isOk = false;
            }
            model.Free();
        }
        foreach ((PartKind kind, (string file, string animation)) in CellVisual3D.CELL_MODELS)
        {
            Node3D model     = CellVisual3D.NewPartModel(kind, Colors.Tan);
            bool   hasMeshes = model.FindChildren("*", nameof(MeshInstance3D), true, false).Count > 0;
            var    player    = model.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().FirstOrDefault();
            bool   hasLoop   = null == animation || (null != player && player.HasAnimation(animation));
            if (!hasMeshes || !hasLoop)
            {
                GD.Print($"  model {file} of {kind}: meshes {hasMeshes}, animation '{animation}' {hasLoop}");
                isOk = false;
            }
            model.Free();
        }
        bool isEveryPartModeled = System.Enum.GetValues<PartKind>().All(kind => CellVisual3D.CELL_MODELS.ContainsKey(kind));
        return isOk && isEveryPartModeled;
    }

    /// <summary>Stats of a design with parts placed at free spots along the spine.</summary>
    private static CreatureStats StatsWith(CreatureDesign design, params CreaturePartKind[] kinds)
    {
        float[] spots = { 0.3f, 0.6f, 0.45f, 0.75f, 0.15f, 0.9f };
        foreach (CreaturePartKind kind in kinds)
        {
            for (int i = 0; i < spots.Length && !design.TryPlace(kind, spots[i], 0); i++) { }
        }
        return CreatureStats.From(design);
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
