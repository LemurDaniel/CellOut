// NOTE:
//   Keeps the pack of the land animal: wild conspecifics (Kin) roam the island, a call
//   makes one follow, with a follower a nest can be built (the pack stage: pups are born,
//   the nest is the spawn point) and with enough members at the nest the tribe is founded
//   (the fire is lit, the kin become villagers). Steers every kin once per physics frame,
//   lets them eat the fruit they touch and knows which action is offered right now
//   (Offer). It does not process on its own: Main calls Update after the coast and
//   routes Space and F to HandleInput. Kin and nest are its children.
//
//   var pack = new PackKeeper();
//   pack.Setup(session, followCamera);
//   pack.KinAte     += OnKinAte;
//   pack.NestBuilt  += OnNestBuilt;
//   AddChild(pack);
//   pack.Update(dt);                       // in _PhysicsProcess
//   if (pack.HandleInput(@event)) GetViewport().SetInputAsHandled();

//
//   Chapters:
//     TYPES        What the pack offers the player to do next.
//     SETTINGS     The session, the camera and the signals.
//     KEEP PACK    Wild kin, steering, eating, the offer and the frame update.
//     ACT          Call, nest, tribe, follow or stay, takeover and the cheats.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   PackOffer   The action Space (or the HUD button) performs right now.
//

public enum PackOffer { None, Call, Nest, FoundTribe }

public partial class PackKeeper : Node2D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   KinAte       A kin ate a piece of land food, the tribe counts it as gathered.
    //   NestBuilt    The nest stands, Main shows the stage.
    //   Setup        The session and the camera (view radius), before AddChild.
    //

    [Signal] public delegate void KinAteEventHandler(Vector2 position, float size, Color color);
    [Signal] public delegate void NestBuiltEventHandler();

    /// <summary>Every tunable number of the pack from data/balance.json (tribe.pack).</summary>
    private static PackBalance Tune => Balance.Current.Tribe.Pack;

    private WorldSession _session;
    private FollowCamera _camera;

    private Player Player => _session.Player;
    private Coast Coast => _session.Coast;
    private Node2D FoodLayer => _session.FoodLayer;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player, food layer and coast.</param>
    /// <param name="camera">(Required) The camera, wild kin spawn just outside its view.</param>
    public void Setup(WorldSession session, FollowCamera camera)
    {
        _session = session;
        _camera  = camera;
    }

    // ===========================================================================
    // ==============================   KEEP PACK   ==============================
    // ===========================================================================
    //
    //   Kin          Every conspecific on the island, wild or in the pack.
    //   Members      Kin of the pack: following, staying or villagers.
    //   Nest         The nest, null until built.
    //   Offer        What Space does right now, recomputed every frame.
    //   OfferHint    Translation key of what is missing for the next step, null when nothing is.
    //   IsStaying    Whether the pack waits at the nest (F toggles).
    //   Update       Spawns wild kin, steers all, lets them eat, keeps the nest on the ground, computes the offer.
    //   SpawnWild    A wild kin on grass just outside the view.
    //   SpawnKin     One kin of the player's body plan at a spot.
    //

    private const float HUE_SHIFT = 0.06f;           // a kin's color sits this far around the hue wheel from the player's
    private const float WILD_SIZE_MIN = 0.85f;        // wild kin against the player size
    private const float WILD_SIZE_MAX = 1.1f;
    private const float SPAWN_NEAR = 1.05f;           // view radii
    private const float SPAWN_FAR = 1.6f;
    private const int SPAWN_TRIES = 10;
    private const float NEST_AHEAD = 1.5f;            // the nest lands this many sizes in front of the player

    private readonly List<Kin> _kin = new();
    private float _spawnCooldown;

    public IReadOnlyList<Kin> Kin => _kin;

    /// <summary>Kin of the pack: everyone who is not wild any more.</summary>
    public int Members
    {
        get
        {
            int count = 0;
            foreach (Kin kin in _kin)
            {
                if (KinMode.Wild != kin.Mode)
                {
                    count++;
                }
            }
            return count;
        }
    }

    public Nest Nest { get; private set; }

    public PackOffer Offer { get; private set; }

    /// <summary>Key in translations.csv naming what is missing for the next step (find kin, go inland, more kin, back to the nest), null with an offer or before the land.</summary>
    public string OfferHint { get; private set; }

    public bool IsStaying { get; private set; }

    /*
      One physics frame of the pack, the coast must exist:
      1. Wild kin      Up to KinOnIsland roam the island of the land animal, one every KinSpawnInterval.
      2. Steer         Every kin walks: orders, following, roaming (Kin.Steer).
      3. Eat           A kin that touches land food eats it (KinAte), pups grow faster.
      4. Nest          The nest stays on the ground that scales with the player, the leader heals there (NestRegen).
      5. Offer         Call a kin nearby, build the nest, found the tribe, or nothing.
    */
    /// <summary>One physics frame of the pack.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Update(float dt)
    {
        if (null == Coast)
        {
            return;
        }
        float size = Player.BodySize();

        // Wild kin
        _kin.RemoveAll(kin => !IsInstanceValid(kin) || kin.IsQueuedForDeletion());
        bool isWildWanted = Player.IsLandAnimal && !Player.IsTribal && _kin.Count - Members < Tune.KinOnIsland;
        _spawnCooldown -= dt;
        if (isWildWanted && _spawnCooldown <= 0f)
        {
            _spawnCooldown = Tune.KinSpawnInterval;
            SpawnWild(size);
        }

        // Steer
        foreach (Kin kin in _kin)
        {
            kin.Steer(dt, Coast, _kin, Player, size, isPackFollowing: !IsStaying);
        }

        // Eat
        foreach (Node node in FoodLayer.GetChildren())
        {
            if (node is not Food { Habitat: FoodHabitat.Land } food || food.IsEngulfed || food.IsQueuedForDeletion())
            {
                continue;
            }
            foreach (Kin kin in _kin)
            {
                if (kin.Position.DistanceTo(food.Position) < kin.Radius + food.Size * 0.5f)
                {
                    food.QueueFree();
                    EmitSignal(SignalName.KinAte, food.Position, food.Size, food.ColorOf());
                    break;
                }
            }
        }

        // Nest
        if (null != Nest)
        {
            Nest.Size         = size;
            Nest.GroundHeight = Coast.GroundHeight(Nest.Position) * size;
            if (Player.GlobalPosition.DistanceTo(Nest.Position) <= size * Tune.NestRange)
            {
                Player.Heal(Tune.NestRegen * dt);
            }
        }

        // Offer
        Offer = ComputeOffer(size);
    }

    /*
      > Land animal without a nest
          - A wild kin within CallRange: Call.
          - A member follows and the player stands on grass (NestDepth inland): Nest,
            on the sand the hint says to go inland, without a member to find kin.
      > Pack with a nest, not yet a tribe
          - At the nest (NestRange) with FoundTribeAt members: FoundTribe,
            else the hint says to call more kin or to go back to the nest.
      > Otherwise nothing
    */
    private PackOffer ComputeOffer(float size)
    {
        OfferHint = null;
        if (!Player.IsLandAnimal || Player.IsTribal || !Player.IsOnLand)
        {
            return PackOffer.None;
        }
        if (null == Nest)
        {
            if (null != NearestWild(size * Tune.CallRange))
            {
                return PackOffer.Call;
            }
            bool isOnGrass = Coast.LandDepth(Player.GlobalPosition) >= Tune.NestDepth * Coast.Unit;
            if (Members > 0 && isOnGrass)
            {
                return PackOffer.Nest;
            }
            OfferHint = Members > 0 ? "PACK_HINT_INLAND" : "PACK_HINT_FIND";
            return PackOffer.None;
        }
        bool isAtNest = Player.GlobalPosition.DistanceTo(Nest.Position) <= size * Tune.NestRange;
        if (isAtNest && Members >= Tune.FoundTribeAt)
        {
            return PackOffer.FoundTribe;
        }
        OfferHint = Members < Tune.FoundTribeAt ? "PACK_HINT_MORE" : "PACK_HINT_RETURN";
        return PackOffer.None;
    }

    /// <summary>The closest wild kin within a range, null when none.</summary>
    private Kin NearestWild(float range)
    {
        Kin   nearest  = null;
        float distance = range;
        foreach (Kin kin in _kin)
        {
            float d = kin.Position.DistanceTo(Player.GlobalPosition);
            if (KinMode.Wild == kin.Mode && d <= distance)
            {
                nearest  = kin;
                distance = d;
            }
        }
        return nearest;
    }

    /// <summary>A wild kin on grass in a ring just outside the view, a few tries, none when the ring misses the land.</summary>
    private void SpawnWild(float size)
    {
        float viewRadius = _camera.ViewRadius();
        for (int i = 0; i < SPAWN_TRIES; i++)
        {
            Vector2 spot = Player.GlobalPosition + Vector2.FromAngle(Rand.Angle()) * viewRadius * Rand.Range(SPAWN_NEAR, SPAWN_FAR);
            if (Coast.LandDepth(spot) >= Tune.NestDepth * Coast.Unit)
            {
                SpawnKin(spot, size * Rand.Range(WILD_SIZE_MIN, WILD_SIZE_MAX), KinMode.Wild);
                return;
            }
        }
    }

    /// <summary>One kin of the player's body plan in a shade of its color.</summary>
    /// <param name="spot">(Required) Plane point, moved out of trunks.</param>
    /// <param name="size">(Required) Adult body size.</param>
    /// <param name="mode">(Required) Wild, following, staying or villager.</param>
    /// <param name="isPup">(Optional) Born small. Defaults to false.</param>
    private Kin SpawnKin(Vector2 spot, float size, KinMode mode, bool isPup = false)
    {
        Color tint = Player.BodyColor;
        tint.H     = Mathf.Wrap(tint.H + Rand.Range(-HUE_SHIFT, HUE_SHIFT), 0f, 1f);
        tint.V     = Mathf.Clamp(tint.V * Rand.Range(0.85f, 1.05f), 0f, 1f);
        var kin    = new Kin { Position = Coast.PushOutOfTrees(spot, size) };
        kin.Setup(Player.Creature, size, tint, home: null == Nest ? spot : Nest.Position);
        if (isPup)
        {
            kin.BornSmall();
        }
        switch (mode)
        {
            case KinMode.Following:
                kin.Follow();
                break;
            case KinMode.Staying:
                kin.Stay(Nest.Position);
                break;
            case KinMode.Villager:
                kin.JoinTribe(Nest.Position);
                break;
        }
        _kin.Add(kin);
        AddChild(kin);
        Log.Verbose("PackKeeper", $"Kin spawned at {spot} ({mode}, pup: {isPup}), {_kin.Count} on the island.");
        return kin;
    }

    // ===========================================================================
    // =================================   ACT   =================================
    // ===========================================================================
    //
    //   HandleInput     Space performs the offer, F lets the pack stay or follow.
    //   Act             Performs the offered action (Space, HUD button).
    //   Call            The nearest wild kin follows the leader.
    //   BuildNest       The nest in front of the player, pups are born, the pack stage.
    //   FoundTribe      The fire is lit, every member becomes a villager, the tribe stage.
    //   ToggleStay      F: the pack waits at the nest or follows again.
    //   SpawnVillager   A grown villager at a hut (TribeCommander).
    //   TryTakeOver     A dead leader lives on in the biggest member, false without members.
    //   CheatFoundPack  F10: three followers and the nest right here.
    //   CheatFoundTribe F10: enough members, then the tribe.
    //

    /// <summary>Space performs the offer, F toggles following and staying from the pack on.</summary>
    /// <param name="event">(Required) The event Main received in _UnhandledInput.</param>
    /// <returns>True when the pack used the event.</returns>
    public bool HandleInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return false;
        }
        if (Key.Space == key.Keycode && Act())
        {
            return true;
        }
        if (Key.F == key.Keycode && Player.IsPackLeader && !Player.IsTribal)
        {
            ToggleStay();
            return true;
        }
        return false;
    }

    /// <summary>Performs the offered action.</summary>
    /// <returns>True when something happened, false with no offer.</returns>
    /// <example><code>if (pack.Act()) ...   // what Space and the HUD button do</code></example>
    public bool Act()
    {
        switch (Offer)
        {
            case PackOffer.Call:
                Call();
                return true;
            case PackOffer.Nest:
                BuildNest();
                return true;
            case PackOffer.FoundTribe:
                FoundTribe();
                return true;
            default:
                return false;
        }
    }

    private void Call()
    {
        Kin kin = NearestWild(Player.BodySize() * Tune.CallRange);
        if (null == kin)
        {
            return;
        }
        kin.Follow();
        Offer = ComputeOffer(Player.BodySize());
        Log.Verbose("PackKeeper", $"Called a kin, {Members} member(s).");
    }

    /// <summary>The nest in front of the player, PupsAtNest pups are born next to it and stay there until grown, the pack stage.</summary>
    private void BuildNest()
    {
        if (null != Nest || !Player.IsLandAnimal || Player.IsPackLeader)
        {
            return;
        }
        float   size = Player.BodySize();
        Vector2 spot = Coast.PushOutOfTrees(Player.GlobalPosition + Vector2.FromAngle(Player.Facing) * size * NEST_AHEAD, size * 1.6f);
        Nest = new Nest { Position = spot, Size = size, GroundHeight = Coast.GroundHeight(spot) * size };
        AddChild(Nest);
        for (int i = 0; i < Tune.PupsAtNest; i++)
        {
            SpawnKin(spot + Vector2.FromAngle(Rand.Angle()) * size * 2.5f, size, KinMode.Staying, isPup: true);
        }
        Player.FoundSociety(Player.PACK_STAGE);
        EmitSignal(SignalName.NestBuilt);
        Offer = ComputeOffer(size);
    }

    /// <summary>The fire is lit, every member becomes a villager around it, the tribe stage.</summary>
    private void FoundTribe()
    {
        if (null == Nest || Player.IsTribal)
        {
            return;
        }
        Nest.LightFire();
        foreach (Kin kin in _kin)
        {
            if (KinMode.Wild != kin.Mode)
            {
                kin.JoinTribe(Nest.Position);
            }
        }
        Player.FoundSociety(Player.TRIBE_STAGE);
        Offer = PackOffer.None;
        Log.Verbose("PackKeeper", $"Tribe founded with {Members} villager(s).");
    }

    /// <summary>F: the pack waits at the nest (or where it stands without one) or follows again.</summary>
    public void ToggleStay()
    {
        IsStaying = !IsStaying;
        foreach (Kin kin in _kin)
        {
            if (KinMode.Wild == kin.Mode || KinMode.Villager == kin.Mode || kin.IsPup)
            {
                continue;     // pups stay at the nest until they are grown
            }
            if (IsStaying)
            {
                kin.Stay(null == Nest ? kin.Position : Nest.Position);
            }
            else
            {
                kin.Follow();
            }
        }
        Log.Verbose("PackKeeper", IsStaying ? "The pack stays." : "The pack follows.");
    }

    /// <summary>A grown villager next to a hut, the tribe raised it.</summary>
    /// <param name="spot">(Required) Plane point of the hut.</param>
    public void SpawnVillager(Vector2 spot)
    {
        if (null == Nest)
        {
            throw new System.InvalidOperationException("A villager needs the nest with the fire, build the nest first.");
        }
        float size = Player.BodySize();
        SpawnKin(spot + Vector2.FromAngle(Rand.Angle()) * size * 2f, size, KinMode.Villager);
    }

    /// <summary>The dead leader lives on in the body of the biggest grown member, which leaves the pack.</summary>
    /// <returns>True when a member was taken over, false without one (game over).</returns>
    /// <example><code>if (!pack.TryTakeOver()) ShowGameOver();</code></example>
    public bool TryTakeOver()
    {
        Kin biggest = null;
        foreach (Kin kin in _kin)
        {
            if (KinMode.Wild != kin.Mode && !kin.IsPup && (null == biggest || kin.Radius > biggest.Radius))
            {
                biggest = kin;
            }
        }
        if (null == biggest)
        {
            return false;
        }
        Player.Revive(biggest.Position);
        _kin.Remove(biggest);
        biggest.QueueFree();
        Log.Verbose("PackKeeper", $"The leader lives on in a pack member, {Members} member(s) left.");
        return true;
    }

    /// <summary>Cheat (F10): three kin follow and the nest stands right here, whatever the ground.</summary>
    public void CheatFoundPack()
    {
        if (null == Coast || !Player.IsLandAnimal || Player.IsPackLeader)
        {
            return;
        }
        float size = Player.BodySize();
        for (int i = 0; i < 3; i++)
        {
            SpawnKin(Player.GlobalPosition + Vector2.FromAngle(Rand.Angle()) * size * 3f, size, KinMode.Following);
        }
        BuildNest();
    }

    /// <summary>Cheat (F10): the pack if needed, members up to FoundTribeAt, then the tribe.</summary>
    public void CheatFoundTribe()
    {
        CheatFoundPack();
        if (null == Nest || Player.IsTribal)
        {
            return;
        }
        float size = Player.BodySize();
        while (Members < Tune.FoundTribeAt)
        {
            SpawnKin(Nest.Position + Vector2.FromAngle(Rand.Angle()) * size * 3f, size, KinMode.Following);
        }
        FoundTribe();
    }
}
