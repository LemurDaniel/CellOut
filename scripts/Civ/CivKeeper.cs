// NOTE:
//   The city of the civilization stage. A tribe with enough standing buildings founds its
//   city at the fire (PackKeeper offers it, Space): the headquarters goes up where nest and
//   fire stood (BuildingKind.Hall, its blueprint is edited in the BuildingEditor like every
//   other function) and the player reaches the civilization. From then on the city gathers
//   knowledge (the hall, every villager, with Philosophy every building) and researches the
//   techs of Civ/TechTree.cs with it; what they do together is Bonuses, Society hands it to
//   the tribe and the rivals. It does not process on its own: Society calls Update after the
//   tribe.
//
//   var civ = new CivKeeper();
//   civ.Setup(session, tribe);
//   civ.TechResearched += kind => ShowBanner(kind);
//   AddChild(civ);
//   civ.Update(dt);
//   civ.Research(TechKind.Engine);

//
//   Chapters:
//     SETTINGS   The session, the tribe, the events and the state of the city.
//     FOUND      Whether and how the city is founded at the fire.
//     RESEARCH   Knowledge per frame and the research of a tech.
//     SAVE       The city as save data and back.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class CivKeeper : Node
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Changed          Knowledge or techs changed, the views refresh.
    //   TechResearched   A tech was researched, Society shows the banner.
    //   CityFounded      The city was founded, the neighbors follow (Society).
    //   Setup            The session and the tribe, before AddChild.
    //   IsCity           From the civilization stage on.
    //   Knowledge        Knowledge of the city, spent on techs.
    //   Researched       The techs researched so far.
    //   Bonuses          What they do together (TechTree.BonusesOf).
    //

    public event System.Action Changed;
    public event System.Action<TechKind> TechResearched;
    public event System.Action CityFounded;

    private static CivBalance Tune => Balance.Current.Civ;

    private WorldSession _session;
    private TribeCommander _tribe;
    private readonly HashSet<TechKind> _researched = new();

    private Player Player => _session.Player;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player, layers and coast.</param>
    /// <param name="tribe">(Required) The tribe the city grows from.</param>
    public void Setup(WorldSession session, TribeCommander tribe)
    {
        _session = session;
        _tribe   = tribe;
    }

    public bool IsCity => Player.Stage >= Player.CIVILIZATION_STAGE;

    public float Knowledge { get; private set; }

    public IReadOnlySet<TechKind> Researched => _researched;

    public CivBonuses Bonuses { get; private set; } = CivBonuses.NONE;

    // ===========================================================================
    // ================================   FOUND   ================================
    // ===========================================================================
    //
    //   Buildings   Standing buildings of the tribe, the headquarters not counted.
    //   CanFound    A tribe (not yet a city) with FoundBuildings standing and the stock for the hall.
    //   FoundHint   What is still missing, shown at the fire.
    //   Found       The headquarters on the fire, the civilization stage.
    //   CheatFound  The cheat and the start phase: enough stock for the hall, founded at once.
    //   StepOutOfHall   The chieftain steps out of the new headquarters.
    //

    private const float STEP_OUT = 2f;               // body sizes the chieftain stands in front of the new headquarters

    public int Buildings => _tribe.Huts.Count(hut => IsInstanceValid(hut) && BuildingKind.Hall != hut.Design.Kind);

    /// <summary>Whether the tribe may found its city now: enough standing buildings and the stock for the hall.</summary>
    public bool CanFound => Player.TRIBE_STAGE == Player.Stage && _tribe.IsActive && !_tribe.HasHall
        && Buildings >= Tune.FoundBuildings && _tribe.Economy.CanAfford(_tribe.DesignOf(BuildingKind.Hall));

    /// <summary>Founds the city: the headquarters goes up on the fire, the player reaches the civilization.</summary>
    /// <returns>True when it was founded.</returns>
    public bool Found()
    {
        if (!CanFound || !_tribe.TryBuildHall())
        {
            return false;
        }
        StepOutOfHall();
        Player.FoundSociety(Player.CIVILIZATION_STAGE);
        Log.Verbose("CivKeeper", $"City founded with {Buildings} building(s) around the headquarters.");
        Changed?.Invoke();
        CityFounded?.Invoke();
        return true;
    }

    /// <summary>The chieftain stood at the fire, where the headquarters is now: it steps out in front of it.</summary>
    private void StepOutOfHall()
    {
        Hut     hall = _tribe.Hall;
        Vector2 away = (Player.GlobalPosition - hall.Position).Normalized();
        away = away.IsZeroApprox() ? Vector2.Right : away;
        Player.GlobalPosition = hall.Position + away * (hall.FoundationRadius + Player.BodySize() * STEP_OUT);
    }

    /*
      Hint at the fire while the city cannot be founded yet:
      > Not a tribe any more, or away from the fire: none
      > Too few buildings: how many more
      > Too little stock for the hall: how much food and wood more
      > Otherwise none, the offer itself shows (Space)
    */
    /// <summary>What the tribe still needs for its city, as a text for the panel while the chieftain stands at the fire.</summary>
    /// <returns>Null away from the fire, once everything is there, or in a city.</returns>
    public string FoundHint()
    {
        if (Player.TRIBE_STAGE != Player.Stage || !_tribe.IsActive)
        {
            return null;
        }
        float reach = Player.BodySize() * Balance.Current.Tribe.Pack.NestRange;
        if (Player.GlobalPosition.DistanceTo(_tribe.Fire) > reach)
        {
            return null;
        }
        int missing = Tune.FoundBuildings - Buildings;
        if (missing > 0)
        {
            return string.Format(Tr("CIV_HINT_BUILDINGS"), missing);
        }
        BuildingDesign hall = _tribe.DesignOf(BuildingKind.Hall);
        int food = Mathf.Max(0, hall.FoodCost - _tribe.Economy.Food);
        int wood = Mathf.Max(0, hall.WoodCost - _tribe.Economy.Wood);
        return food > 0 || wood > 0 ? string.Format(Tr("CIV_HINT_STOCK"), food, wood) : null;
    }

    /// <summary>Cheat and start phase: a tribe gets the stock for the hall and founds its city at once, whatever stands.</summary>
    public void CheatFound()
    {
        if (Player.TRIBE_STAGE != Player.Stage || !_tribe.IsActive)
        {
            return;
        }
        BuildingDesign hall = _tribe.DesignOf(BuildingKind.Hall);
        _tribe.Economy.AddFood(hall.FoodCost);
        _tribe.Economy.AddWood(hall.WoodCost);
        if (_tribe.TryBuildHall())
        {
            StepOutOfHall();
            Player.FoundSociety(Player.CIVILIZATION_STAGE);
            Changed?.Invoke();
            CityFounded?.Invoke();
        }
    }

    // ===========================================================================
    // ===============================   RESEARCH   ==============================
    // ===========================================================================
    //
    //   KnowledgeRate   Knowledge per minute: the hall, every villager, with Philosophy every
    //                   building, times the factor of Writing.
    //   Update          Knowledge grows while the city stands.
    //   Research        Spends knowledge on a tech, the bonuses follow.
    //   CheatKnowledge  Cheat: knowledge to research with.
    //

    private const float SECONDS_PER_MINUTE = 60f;

    /// <summary>Knowledge the city gathers per minute right now.</summary>
    public float KnowledgeRate
    {
        get
        {
            if (!IsCity)
            {
                return 0f;
            }
            IEnumerable<Hut> standing = _tribe.Huts.Where(IsInstanceValid);
            float fromHalls     = standing.Sum(hut => hut.Design.Knowledge);
            float fromBuildings = Buildings * Bonuses.KnowledgePerBuilding;
            float fromVillagers = _tribe.Villagers().Count * Tune.KnowledgePerVillager;
            return (fromHalls + fromBuildings + fromVillagers) * Bonuses.Knowledge;
        }
    }

    /// <summary>One physics frame: the city gathers knowledge.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Update(float dt)
    {
        if (!IsCity)
        {
            return;
        }
        float before = Knowledge;
        Knowledge += KnowledgeRate * dt / SECONDS_PER_MINUTE;
        // The views only need a whole point more
        if ((int)Knowledge != (int)before)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Researches a tech: its knowledge is spent, the bonuses of the city follow.</summary>
    /// <param name="kind">(Required) The tech.</param>
    /// <returns>True when it was researched (open, new and paid).</returns>
    /// <example><code>if (civ.Research(TechKind.Engine)) ...   // the chieftain sails from now on</code></example>
    public bool Research(TechKind kind)
    {
        if (!IsCity || !TechTree.CanResearch(kind, _researched, Knowledge))
        {
            return false;
        }
        Knowledge -= TechTree.CostOf(kind);
        _researched.Add(kind);
        Bonuses = TechTree.BonusesOf(_researched);
        Log.Verbose("CivKeeper", $"{kind} researched, {Knowledge:F0} knowledge left.");
        Changed?.Invoke();
        TechResearched?.Invoke(kind);
        return true;
    }

    /// <summary>Cheat: knowledge to research with.</summary>
    /// <param name="amount">(Required) Knowledge to add.</param>
    public void CheatKnowledge(float amount)
    {
        Knowledge += amount;
        Changed?.Invoke();
    }

    // ===========================================================================
    // =================================   SAVE   ================================
    // ===========================================================================
    //
    //   Save      Knowledge and the researched techs.
    //   Restore   The city of a save, the headquarters comes back with the tribe.
    //

    /// <summary>The city as plain save data (Core/Save/SaveGame.cs).</summary>
    public SaveCiv Save()
    {
        return new SaveCiv { Knowledge = Knowledge, Techs = _researched.Select(kind => kind.ToString()).ToList() };
    }

    /// <summary>Takes over the city of a save.</summary>
    /// <param name="save">(Required) The saved city.</param>
    public void Restore(SaveCiv save)
    {
        Knowledge = save.Knowledge;
        _researched.Clear();
        foreach (string tech in save.Techs)
        {
            if (!System.Enum.TryParse(tech, out TechKind kind))
            {
                Log.Verbose("CivKeeper", $"Saved tech '{tech}' is no TechKind any more -> skipped.");
                continue;
            }
            _researched.Add(kind);
        }
        Bonuses = TechTree.BonusesOf(_researched);
        Changed?.Invoke();
        Log.Verbose("CivKeeper", $"City restored: {Knowledge:F0} knowledge, {_researched.Count} tech(s).");
    }
}
