// NOTE:
//   Fills the world around the player: the starting cells (and the new sea after the
//   evolution) bit by bit inside the view, later cells in a ring just outside it. Food
//   never appears in sight: it comes as whole patches, all pieces at once, at a spot
//   outside the view, the soup between them stays empty and the player has to go
//   looking (rule in CLAUDE.md). A popped cell leaves meat right where it popped.
//   Sizes are relative to the player, kinds depend on the stage. It does not process on
//   its own: Main calls Refill once per physics frame, after the contacts.
//
//   var spawner = new Spawner();
//   spawner.Setup(session, followCamera);
//   spawner.SplashRequested += SpawnSplash;
//   AddChild(spawner);
//   spawner.StartPopulating();   // after the intro and after the evolution
//   spawner.Refill(dt);          // in _PhysicsProcess

//
//   Chapters:
//     SETTINGS   The session, the camera and the splash signal.
//     SPAWN      The starting queue, the refill, food, cells and their spawn points.
//     PATCHES    Whole food patches outside the view.
//

using Godot;

namespace CellOut;

public partial class Spawner : Node
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   SplashRequested   A plop is due on the plane, Main spawns it.
    //   Setup             The session and the camera (view radius), before AddChild.
    //

    [Signal] public delegate void SplashRequestedEventHandler(Vector2 position, float radius, Color color);

    private const int MAX_CRITTERS = 24;
    public const int START_CRITTERS = 8;

    private WorldSession _session;
    private FollowCamera _camera;

    private Player Player => _session.Player;
    private Coast Coast => _session.Coast;
    private Node2D FoodLayer => _session.FoodLayer;
    private Node2D CritterLayer => _session.CritterLayer;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player, layers and coast.</param>
    /// <param name="camera">(Required) The camera, its view radius decides where things spawn.</param>
    public void Setup(WorldSession session, FollowCamera camera)
    {
        _session = session;
        _camera  = camera;
    }

    // ===========================================================================
    // ================================   SPAWN   ================================
    // ===========================================================================
    //
    //   StartPopulating      The first food patches outside the view, queues the starting cells.
    //   Refill               Spawns the queue first, then keeps the world topped up outside the view.
    //   UpdatePopulating     Spawns the cell queue bit by bit inside the view, with a plop.
    //   SpawnFood            One piece of food at a point (molecule, sea or land food), size and value relative to the player.
    //   SpawnCritter         One cell or colony, kind and size depend on the stage, never right on top of the player.
    //   SpawnPosition        A point inside the view or in a ring just outside it, in the sea if asked.
    //   OnCritterPopped      A popped cell leaves four rich meat chunks.
    //   OnColonyCellsBurst   Burst cells of a colony leave one meat chunk each (capped).
    //

    public const float POPULATE_SECONDS = 5f;      // the starting cells arrive over this time
    private const float SEA_FOOD_LOOK = 2.2f;      // sea and land food are drawn bigger than their value size,
    private const float LAND_FOOD_LOOK = 2.6f;     // next to a creature a molecule sized apple looked lost
    private const float COLONY_SHARE = 0.2f;       // share of colonies among the cells of the multicellular stage
    private const float COLONY_MIN_SIZE = 0.6f;    // body size of a colony in player sizes
    private const float COLONY_MAX_SIZE = 2f;

    private int _pendingCritters;
    private float _populateTime;

    /// <summary>The first food patches (PatchRule.StartPatches) outside the view at once, START_CRITTERS cells pop up over POPULATE_SECONDS.</summary>
    /// <example><code>spawner.StartPopulating();</code></example>
    public void StartPopulating()
    {
        for (int i = 0; i < Patches.StartPatches; i++)
        {
            SpawnPatch();
        }
        _patchCooldown   = Patches.Interval;
        _pendingCritters = START_CRITTERS;
        _populateTime    = 0f;
    }

    /// <summary>Fills the soup after the start, then keeps it topped up outside the view.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Refill(float dt)
    {
        if (UpdatePopulating(dt))
        {
            return;
        }

        // A whole new patch once the eaten or left behind ones made room for it
        _patchCooldown -= dt;
        if (_patchCooldown <= 0f && FoodLayer.GetChildCount() + Patches.MaxPieces <= Patches.MaxFood)
        {
            SpawnPatch();
            _patchCooldown = Patches.Interval;
        }
        if (CritterLayer.GetChildCount() < MAX_CRITTERS && Rand.Chance(0.05f))
        {
            SpawnCritter(isInView: false);
        }
    }

    /// <summary>Spawns as many cells of the queue as the elapsed share of POPULATE_SECONDS allows, starting a second in.</summary>
    /// <returns>True while the queue is not empty, the normal refill waits meanwhile.</returns>
    private bool UpdatePopulating(float dt)
    {
        if (0 == _pendingCritters)
        {
            return false;
        }
        _populateTime += dt;
        float cellShare   = Mathf.Clamp((_populateTime - 1f) / (POPULATE_SECONDS - 1f), 0f, 1f);
        int   crittersDue = Mathf.CeilToInt(START_CRITTERS * cellShare) - (START_CRITTERS - _pendingCritters);
        for (int i = 0; i < crittersDue && _pendingCritters > 0; i++, _pendingCritters--)
        {
            Critter critter = SpawnCritter(isInView: true);
            EmitSignal(SignalName.SplashRequested, critter.Position, critter.Radius * 1.5f, critter.BodyColor);
        }
        return true;
    }

    // Off for now (decision of the user): land food popping out of nothing looked wrong,
    // it comes back with a source (e.g. fruit growing on trees and falling down)
    private static readonly bool IS_LAND_FOOD_SPAWNED = false;    // readonly, not const: no unreachable code warning

    /*
      Habitat by stage and spot:
      - Up to the cluster: molecules of the soup
      - Organism: sea food in the water, land food (fruit, mushrooms, berries) on the islands
        only with IS_LAND_FOOD_SPAWNED, else that spot stays empty
    */
    private void SpawnFood(Vector2 position)
    {
        float size = Player.BodySize();
        var   food = new Food { Habitat = Player.IsOrganism ? FoodHabitat.Sea : FoodHabitat.Soup };
        if (Player.IsOrganism && null != Coast && Coast.IsOnLand(position))
        {
            if (!IS_LAND_FOOD_SPAWNED)
            {
                return;
            }
            food.Habitat      = FoodHabitat.Land;
            food.GroundHeight = Coast.GroundHeight(position) * size;
        }
        food.Setup(size * Rand.Range(0.15f, 0.28f), (FoodKind)Rand.Index(3));
        food.Value *= Balance.Current.Diet.PlantFoodValue;
        food.LookScale = FoodHabitat.Land == food.Habitat ? LAND_FOOD_LOOK : FoodHabitat.Sea == food.Habitat ? SEA_FOOD_LOOK : 1f;
        food.Position = position;
        FoodLayer.AddChild(food);
    }

    /// <returns>The new cell, already in the critter layer.</returns>
    private Critter SpawnCritter(bool isInView)
    {
        float playerSize = Player.BodySize();
        float roll       = GD.Randf();

        /*
          Kind by stage and roll:
          - Protocell:      60% bacteria (0.4-0.8x), 40% amoebas (1.3-1.8x, dangerous)
          - Multicellular:  COLONY_SHARE colonies (ColonyRule.CellCountFor, cells about as big as
                            the player's), the rest rolls like the later stages
          - Later:          55% bacteria (0.35-0.8x), 30% amoebas (0.8-1.4x), 15% hunters (1.4-2.2x)
        */
        CritterKind kind;
        float radius;
        int   cellCount     = 1;
        bool  isColonyStage = Player.MULTICELLULAR_STAGE == Player.Stage;
        bool  isColony      = isColonyStage && roll < COLONY_SHARE;

        // The rest of the multicellular roll is stretched back to 0..1 for the single cells
        if (isColonyStage && !isColony)
        {
            roll = (roll - COLONY_SHARE) / (1f - COLONY_SHARE);
        }

        if (isColony)
        {
            kind      = CritterKind.Colony;
            cellCount = ColonyRule.CellCountFor(Player.CellCount, Rand.Index(3) - 1);
            radius    = playerSize * Mathf.Clamp(Rand.Range(0.85f, 1.25f) * Mathf.Sqrt((float)cellCount / Player.CellCount), COLONY_MIN_SIZE, COLONY_MAX_SIZE);
        }
        else if (0 == Player.Stage)
        {
            kind   = roll < 0.6f ? CritterKind.Bacterium : CritterKind.Amoeba;
            radius = playerSize * (CritterKind.Bacterium == kind ? Rand.Range(0.4f, 0.8f) : Rand.Range(1.3f, 1.8f));
        }
        else if (roll < 0.55f)
        {
            kind   = CritterKind.Bacterium;
            radius = playerSize * Rand.Range(0.35f, 0.8f);
        }
        else if (roll < 0.85f)
        {
            kind   = CritterKind.Amoeba;
            radius = playerSize * Rand.Range(0.8f, 1.4f);
        }
        else
        {
            kind   = CritterKind.Hunter;
            radius = playerSize * Rand.Range(1.4f, 2.2f);
        }

        // In the organism stage the sea is full of creatures instead of cells
        var critter = new Critter { Target = Player, FoodLayer = FoodLayer };
        critter.Setup(radius, kind, playerSize, Player.Stage, cellCount);
        if (Player.IsOrganism)
        {
            critter.Creature = CreatureDesign.ForCritter(kind);
        }

        // Keep a safety distance of ten player sizes when spawning in view
        Vector2 position     = SpawnPosition(isInView);
        Vector2 fromPlayer   = position - Player.GlobalPosition;
        float   safeDistance = playerSize * 10f;
        if (isInView && fromPlayer.Length() < safeDistance)
        {
            position = Player.GlobalPosition + fromPlayer.Normalized() * safeDistance;
        }
        critter.Position = position;
        critter.Popped     += OnCritterPopped;
        critter.CellsBurst += OnColonyCellsBurst;
        CritterLayer.AddChild(critter);
        return critter;
    }

    private const int SEA_SPOT_TRIES = 8;

    /// <summary>A random spawn point around the player.</summary>
    /// <param name="isInView">(Required) Inside the view instead of the ring just outside it.</param>
    /// <param name="isSeaOnly">(Optional) Tries a few points for one in the sea (deep on an island
    /// the coast would pile them up at the shore), else pushes the last one out. Defaults to true.</param>
    private Vector2 SpawnPosition(bool isInView, bool isSeaOnly = true)
    {
        float   viewRadius = _camera.ViewRadius();
        Vector2 position   = Player.GlobalPosition;
        for (int i = 0; i < SEA_SPOT_TRIES; i++)
        {
            float distance = isInView ? Rand.Range(0.1f, 1f) * viewRadius : Rand.Range(1.05f, 1.6f) * viewRadius;
            position = Player.GlobalPosition + Vector2.FromAngle(Rand.Angle()) * distance;
            if (null == Coast || !isSeaOnly || !Coast.IsOnLand(position))
            {
                break;
            }
        }
        return null == Coast || !isSeaOnly ? position : Coast.ToSea(position, Player.BodySize() * 2f);
    }

    private const int POP_FOOD = 4;
    private const float POP_FOOD_BONUS = 3f;

    private const int MAX_BURST_FOOD = 6;          // a big colony bursts many cells, the soup must not flood

    private void OnCritterPopped(Vector2 position, float radius, Color color)
    {
        EmitSignal(SignalName.SplashRequested, position, radius * 1.8f, color);
        SpawnMeat(position, radius, radius * 0.6f, POP_FOOD);
    }

    /// <param name="cellRadius">(Required) Radius of one burst cell, the chunks are sized by it.</param>
    /// <param name="bodySize">(Required) Body size before the burst, the chunks land just outside it
    /// (inside, the colony would swallow them right back).</param>
    private void OnColonyCellsBurst(Vector2 position, float cellRadius, float bodySize, Color color, int count)
    {
        EmitSignal(SignalName.SplashRequested, position, bodySize * 1.2f, color);
        SpawnMeat(position, cellRadius, bodySize * 1.15f, Mathf.Min(count, MAX_BURST_FOOD));
    }

    /// <summary>Rich meat chunks on a ring around a point, sized by the radius of what burst.</summary>
    /// <param name="spread">(Required) Distance of the chunks from the point.</param>
    private void SpawnMeat(Vector2 position, float radius, float spread, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var food = new Food { IsMeat = true, Habitat = Player.IsOrganism ? FoodHabitat.Sea : FoodHabitat.Soup };
            food.Setup(radius * Rand.Range(0.2f, 0.3f), (FoodKind)Rand.Index(3));
            food.LookScale = FoodHabitat.Sea == food.Habitat ? SEA_FOOD_LOOK : 1f;
            food.Position = position + Vector2.FromAngle(Rand.Angle()) * spread;
            food.Value   *= POP_FOOD_BONUS;
            FoodLayer.CallDeferred(Node.MethodName.AddChild, food);
        }
    }

    // ===========================================================================
    // ===============================   PATCHES   ===============================
    // ===========================================================================
    //
    //   PatchRule     How many patches a habitat gets and how rich they are.
    //   SpawnPatch    A whole food patch at once at a spot outside the view.
    //   PatchCenter   A point so far out that the whole patch disc lies outside the view.
    //

    /// <summary>How much food a habitat holds.</summary>
    /// <param name="MaxFood">A new patch only comes while it still fits under this many pieces.</param>
    /// <param name="StartPatches">Patches placed at once by StartPopulating.</param>
    /// <param name="Interval">Seconds between two new patches at most.</param>
    /// <param name="MinPieces">Smallest patch.</param>
    /// <param name="MaxPieces">Biggest patch.</param>
    private readonly record struct PatchRule(int MaxFood, int StartPatches, float Interval, int MinPieces, int MaxPieces);

    /*
      The soup of the cell stages is richer than the sea: a herbivore cell lives on the
      patches alone and shares them with the grazing cells, a creature finds bigger pieces
      (SEA_FOOD_LOOK) and reaches them faster.
    */
    private static readonly PatchRule SOUP_PATCHES = new(MaxFood: 130, StartPatches: 5, Interval: 1.8f, MinPieces: 14, MaxPieces: 24);
    private static readonly PatchRule SEA_PATCHES  = new(MaxFood: 80, StartPatches: 3, Interval: 2.5f, MinPieces: 10, MaxPieces: 18);

    private PatchRule Patches => Player.IsOrganism ? SEA_PATCHES : SOUP_PATCHES;

    private const float PATCH_MIN_RADIUS = 2.5f;   // in player sizes
    private const float PATCH_MAX_RADIUS = 4.5f;
    private const float PATCH_NEAR = 0.15f;        // view radii between the view edge and the patch disc
    private const float PATCH_FAR = 0.8f;

    private float _patchCooldown;

    /// <summary>All pieces of a patch at once, denser in the middle, out of sight of the player.</summary>
    private void SpawnPatch()
    {
        float   radius = Player.BodySize() * Rand.Range(PATCH_MIN_RADIUS, PATCH_MAX_RADIUS);
        Vector2 center = PatchCenter(radius);
        int     count  = Patches.MinPieces + Rand.Index(Patches.MaxPieces - Patches.MinPieces + 1);

        // The plain roll packs the middle, the square root would spread the pieces evenly
        for (int i = 0; i < count; i++)
        {
            SpawnFood(center + Vector2.FromAngle(Rand.Angle()) * radius * Rand.Range(0f, 1f));
        }
        Log.Verbose("Spawner", $"Food patch of {count} at {center}, radius {radius:F0}.");
    }

    private Vector2 PatchCenter(float radius)
    {
        float viewRadius = _camera.ViewRadius();
        float distance   = viewRadius * (1f + Rand.Range(PATCH_NEAR, PATCH_FAR)) + radius;
        return Player.GlobalPosition + Vector2.FromAngle(Rand.Angle()) * distance;
    }
}
