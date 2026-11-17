// NOTE:
//   Coordinator of the social stages: owns the pack (PackKeeper), the encounters with
//   foreign packs (PackEncounter), the tribe (TribeCommander), its city (Civ/CivKeeper) with its ship (ShipKeeper) and scenery (CityScenery), the neighbor tribes
//   (RivalKeeper) and the diplomacy of its path (RivalDiplomacy), the building placer,
//   the village builder, the HUD layers of the tribe (TribePanel, TribeHub) and the tech tree of the city. It
//   wires them and advances them in the right order. Main creates it, routes input and
//   frames to it and shows its banners; it never reaches up.
//
//   var society = new Society();
//   society.Setup(session, followCamera);
//   society.BannerRequested  += (title, text, args) => Hud.ShowBanner(title, text, args);
//   society.BuilderRequested += () => OpenBuildingEditor();
//   AddChild(society);
//   society.Update(dt);                         // in _PhysicsProcess, after the coast
//   if (society.HandleInput(@event)) ...        // in _UnhandledInput

//
//   Chapters:
//     SETTINGS   The session, the camera, the banner and builder events and the parts.
//     BUILD      Creates and wires every part of the social stages.
//     RUN        Input, the order of a physics frame and the founding of the tribe.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class Society : Node
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   BannerRequested    A banner is due: title key, text key and its arguments.
    //   BuilderRequested   The hub asks for the village builder, Main pauses and opens it.
    //   Setup              The session and the camera, before AddChild.
    //   Pack ... Hub       The parts, for Main, the cheats and the tests.
    //   HudLayers          Both HUD layers of the tribe, hidden with the HUD.
    //

    public event System.Action<string, string, object[]> BannerRequested;
    public event System.Action BuilderRequested;

    private WorldSession _session;
    private FollowCamera _camera;
    private readonly List<CanvasLayer> _hudLayers = new();

    public PackKeeper Pack { get; private set; }
    public PackEncounter Encounter { get; private set; }
    public TribeCommander Tribe { get; private set; }
    public RivalKeeper Rivals { get; private set; }
    public RivalDiplomacy Diplomacy { get; private set; }
    public BuildingPlacer Placer { get; private set; }
    public BuildingEditor BuildingEditor { get; private set; }
    public TribePanel Panel { get; private set; }
    public TribeHub Hub { get; private set; }
    public CivKeeper Civ { get; private set; }
    public TechTreeView TechTree { get; private set; }
    public ShipKeeper Ships { get; private set; }
    public CityScenery Scenery { get; private set; }
    public IReadOnlyList<CanvasLayer> HudLayers => _hudLayers;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player, layers and coast.</param>
    /// <param name="camera">(Required) The camera: view radius, strategy view.</param>
    public void Setup(WorldSession session, FollowCamera camera)
    {
        _session = session;
        _camera  = camera;
    }

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   _Ready   1. Pack     The pack and the encounters with foreign packs.
    //            2. Tribe    The tribe, the neighbors, the city, placer and village builder.
    //            3. HUD      The tribe panel, the hub bar and the tech tree.
    //

    public override void _Ready()
    {
        // ---------------------------------------------------------------------------
        // Pack
        //   The pack and the encounters with foreign packs.
        // ---------------------------------------------------------------------------

        Pack = new PackKeeper();
        Pack.Setup(_session, _camera);
        AddChild(Pack);

        // ---------------------------------------------------------------------------
        // Tribe
        //   The tribe, the neighbors, placer and village builder.
        // ---------------------------------------------------------------------------

        Tribe = new TribeCommander();
        Tribe.Setup(_session, Pack);
        AddChild(Tribe);

        Rivals = new RivalKeeper();
        Rivals.Setup(_session, Pack, Tribe);
        Rivals.RaidStarted   += nameKey => Banner("BANNER_RAID_TITLE", "BANNER_RAID_TEXT", Tr(nameKey));
        Rivals.TradeArrived  += (nameKey, food, wood) => Banner("BANNER_TRADE_TITLE", "BANNER_TRADE_TEXT", Tr(nameKey), food, wood);
        Rivals.TribeDefeated += (nameKey, food, wood) => Banner("BANNER_DEFEAT_TITLE", "BANNER_DEFEAT_TEXT", Tr(nameKey), food, wood);
        Rivals.TributePaid   += (nameKey, food, wood) => Banner("BANNER_TRIBUTE_TITLE", "BANNER_TRIBUTE_TEXT", Tr(nameKey), food, wood);
        Rivals.KinKilled     += OnKinKilled;
        AddChild(Rivals);

        // The foreign packs move in around the nest as soon as it stands
        Pack.NestBuilt += Rivals.SettlePacks;

        Diplomacy = new RivalDiplomacy();
        Diplomacy.Setup(Rivals, Tribe);
        Diplomacy.BannerRequested += Banner;
        AddChild(Diplomacy);

        Encounter = new PackEncounter();
        Encounter.Setup(_session, Pack, Rivals);
        Encounter.BannerRequested += Banner;
        AddChild(Encounter);

        Placer = new BuildingPlacer();
        Placer.Setup(_session, Tribe);
        AddChild(Placer);

        // The city grows from the tribe: founded at the fire, then knowledge and techs
        Civ = new CivKeeper();
        Civ.Setup(_session, Tribe);
        Civ.TechResearched += kind => Banner("BANNER_TECH_TITLE", "BANNER_TECH_TEXT", Tr($"TECH_{kind}".ToUpperInvariant()), Tr($"TECH_{kind}_EFFECT".ToUpperInvariant()));
        AddChild(Civ);
        Pack.CanFoundCity   = () => Civ.CanFound;
        Pack.CityRequested += () => Civ.Found();
        Civ.CityFounded    += Rivals.BuildHalls;
        Civ.Changed        += () => Tribe.Bonuses = Civ.Bonuses;

        // With Sailing the chieftain becomes a ship on the water, the retinue on board
        Ships = new ShipKeeper();
        Ships.Setup(_session, Pack, Civ);
        Ships.CastOff += () =>
        {
            if (!_hasSailed)
            {
                _hasSailed = true;
                Banner("BANNER_SAIL_TITLE", "BANNER_SAIL_TEXT", Ships.Aboard.Count);
            }
        };
        AddChild(Ships);

        // Fields and the pier: the techs change the world around the city
        Scenery = new CityScenery();
        Scenery.Setup(_session, Tribe, Civ);
        AddChild(Scenery);

        BuildingEditor = new BuildingEditor();
        BuildingEditor.Applied += session => Tribe.TakeDesigns(session.Result);
        AddChild(BuildingEditor);

        // ---------------------------------------------------------------------------
        // HUD
        //   The tribe panel and the hub bar.
        // ---------------------------------------------------------------------------

        Panel = new TribePanel();
        _hudLayers.Add(Panel);
        Panel.Watch(Pack, Tribe, Encounter);
        Panel.Civ = Civ;
        Panel.ActionPressed   += () => Pack.Act();
        Panel.BuildPressed    += kind => Placer.Begin(kind);
        Panel.DesignerPressed += () => BuilderRequested?.Invoke();
        AddChild(Panel);

        Hub = new TribeHub();
        _hudLayers.Add(Hub);
        Hub.Watch(_session, Pack, Tribe, Rivals, Civ);
        Hub.BuilderPressed += () => Panel.ToggleBuildMenu();     // the build menu, its first tile opens the builder
        Hub.GiftPressed    += rival => Rivals.Gift(rival);
        AddChild(Hub);

        TechTree = new TechTreeView();
        _hudLayers.Add(TechTree);
        TechTree.Watch(Civ);
        TechTree.ResearchPressed += kind => Civ.Research(kind);
        Hub.ResearchPressed      += TechTree.Toggle;
        AddChild(TechTree);
    }

    private bool _hasSailed;                         // the first cast off of a game gets its banner

    private void Banner(string titleKey, string textKey, params object[] args)
    {
        BannerRequested?.Invoke(titleKey, textKey, args);
    }

    // ===========================================================================
    // =================================   RUN   =================================
    // ===========================================================================
    //
    //   HandleInput   Placer, encounters and pack take the input in this order.
    //   Update        Pack, encounters, tribe, city, ship, scenery, placer, neighbors and their diplomacy, in this order.
    //   SettleTribe   The tribe was founded: the path locks, the packs become tribes.
    //   OnKinKilled   Meat in the pack stage, food for a warlike tribe.
    //

    /// <summary>Routes one input event, the first part that uses it wins.</summary>
    /// <returns>True when a part used the event.</returns>
    public bool HandleInput(InputEvent @event)
    {
        return Placer.HandleInput(@event) || Encounter.HandleInput(@event) || Pack.HandleInput(@event);
    }

    /// <summary>One physics frame of the social stages, after the coast.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Update(float dt)
    {
        Pack.Update(dt);
        Encounter.Update(dt);
        Tribe.Update(dt);
        Civ.Update(dt);
        Ships.Update(dt);
        Scenery.Update();
        Placer.Update();
        Panel.Placing = Placer.Pending;
        Rivals.Update(dt);
        Diplomacy.Update();
    }

    /// <summary>The tribe was founded: its path locks from the diet, the packs become tribes.</summary>
    /// <returns>The path of the tribe.</returns>
    public TribePath SettleTribe()
    {
        Tribe.Path = TribeWays.PathOf(_session.Player.Diet);
        Rivals.Settle(Tribe.Path);
        Log.Verbose("Society", $"Tribe founded on the {Tribe.Path} path (diet {_session.Player.Diet:F2}).");
        return Tribe.Path;
    }

    /// <summary>An enemy died by the player's side: meat where it fell in the pack stage, food for the stock of a warlike tribe.</summary>
    private void OnKinKilled(Vector2 position, float size)
    {
        if (Tribe.IsActive)
        {
            if (TribePath.Warlike == Tribe.Path)
            {
                Tribe.Economy.AddFood(Balance.Current.Tribe.Ways.KillFood);
            }
            return;
        }
        Encounter.DropMeat(position, size);
    }
}
