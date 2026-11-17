// NOTE:
//   Keeps the lone beast of the island: one big animal of a predator species with its own
//   lair, the danger of the pack stage. It is a RivalTribe of one animal that never
//   settles (RivalTribe.IsLoner), so the three ways work on it like on a foreign pack:
//     Hunt      a long fight for the whole pack, its body is a feast.
//     Display   more body than its presence humbles it: it stays in its lair (Humbled).
//     Court     every bow calms it for a moment, with enough trust it joins the pack.
//   Hostile and not humbled it prowls to the player's nest every LonerProwlInterval and
//   attacks whom it finds (the attack itself is PackManners.cs). The lair appears with the
//   foreign packs once the nest stands. Society owns it, Update runs after the rivals.
//
//   var loners = new LonerKeeper();
//   loners.Setup(session, pack, rivals);
//   loners.Humbled += OnHumbled;
//   AddChild(loners);
//   rivals.Loners = loners.Loners;
//   loners.Settle();                 // when the nest is built
//   loners.Update(dt);

//
//   Chapters:
//     SETTINGS   The session, the pack, the rivals and the signal.
//     LAIR       Where the beast lives and what it is.
//     PROWL      Its pace, the prowl to the nest and the respect it forgets.
//     SAVE       The beast as save data and back.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class LonerKeeper : Node2D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Humbled   The beast respects the player for the first time (a won display).
    //   Loners    The lone beasts, none before the nest, for the RivalKeeper and the encounters.
    //   Setup     The session, the pack and the rivals, before AddChild.
    //

    public event System.Action<RivalTribe> Humbled;

    private static WaysBalance Tune => Balance.Current.Tribe.Ways;

    private WorldSession _session;
    private PackKeeper _pack;
    private RivalKeeper _rivals;
    private readonly List<RivalTribe> _loners = new();

    private Player Player => _session.Player;
    private Coast Coast => _session.Coast;

    public IReadOnlyList<RivalTribe> Loners => _loners;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player and coast.</param>
    /// <param name="pack">(Required) The pack: the nest the beast prowls to.</param>
    /// <param name="rivals">(Required) The foreign packs: the lair keeps away from their dens, their species are taken.</param>
    public void Setup(WorldSession session, PackKeeper pack, RivalKeeper rivals)
    {
        _session = session;
        _pack    = pack;
        _rivals  = rivals;
    }

    // ===========================================================================
    // ================================   LAIR   =================================
    // ===========================================================================
    //
    //   Settle   The lair on grass around the nest, away from the dens, once the nest stands.
    //   LairOf   A spot for the lair, null when the island has none.
    //

    private const int FACTION = 100;                 // far above the factions of the neighbors
    private const int LAIR_TRIES = 32;
    private const float DEN_GAP = 0.45f;             // share of SameIslandDistance the lair keeps from every den

    /// <summary>Species of the lone beast, creature templates of hunters that walk on land.</summary>
    private static readonly string[] SPECIES = { "TREX", "CROCODILE", "SCORPION", "SPIDER", "SNAKE" };

    /// <summary>The lone beast moves in: its lair on grass around the nest, once, when the nest stands.</summary>
    /// <param name="relation">(Optional) Its relation to the player, e.g. from a save. Defaults to LonerRelation.</param>
    /// <example><code>loners.Settle();   // Society, when the nest is built</code></example>
    public void Settle(float? relation = null)
    {
        if (_loners.Count > 0 || null == Coast || null == _pack.Nest)
        {
            return;
        }
        Vector2 nest = _pack.Nest.Position;
        if (LairOf(nest) is not Vector2 lair)
        {
            Log.Verbose("LonerKeeper", "No room for a lair on this island, no lone beast.");
            return;
        }
        var kinds = new List<string>(SPECIES);
        foreach (RivalTribe pack in _rivals.Rivals)
        {
            kinds.Remove(pack.Species);     // never the species of a pack next door
        }
        string         species  = kinds[Rand.Index(kinds.Count)];
        CreatureDesign bodyPlan = CreatureTemplates.Get(species).Build();
        var loner = new RivalTribe { Species = species, IsLoner = true };
        loner.AttachCoast(Coast);
        loner.Setup(FACTION, "RIVAL_LONER", lair, lair, bodyPlan, bodyPlan.Color ?? new Color(0.45f, 0.4f, 0.38f), relation ?? Tune.LonerRelation, Player.BodySize() * Tune.LonerSize, isSettled: false);
        _loners.Add(loner);
        AddChild(loner);
        _prowlTimer = Tune.LonerProwlInterval * FIRST_PROWL;
        Log.Verbose("LonerKeeper", $"A lone {species} lives in its lair at {lair}.");
    }

    /// <summary>A spot on grass at LonerDistance from the nest that keeps DEN_GAP from every den, null when the tries all miss.</summary>
    private Vector2? LairOf(Vector2 nest)
    {
        float grass    = Balance.Current.Tribe.Pack.NestDepth * Coast.Unit;
        float distance = Balance.Current.Tribe.Rivals.SameIslandDistance * Coast.Unit;
        for (int attempt = 0; attempt < LAIR_TRIES; attempt++)
        {
            Vector2 spot    = nest + Vector2.FromAngle(Rand.Angle()) * distance * Tune.LonerDistance * Rand.Range(0.85f, 1.1f);
            bool    isClose = false;
            foreach (RivalTribe pack in _rivals.Rivals)
            {
                isClose |= pack.Fire.DistanceTo(spot) < distance * DEN_GAP;
            }
            if (Coast.LandDepth(spot) >= grass && !isClose)
            {
                return spot;
            }
        }
        return null;
    }

    // ===========================================================================
    // ================================   PROWL   ================================
    // ===========================================================================
    //
    //   ForgetGone   Drops a freed beast, first in a frame.
    //   Update       Steers the beast, sets its pace, lets its respect fade, starts and ends the prowl.
    //

    private const float PACE = 0.8f;                 // its pace against the walking pace of a body of the player's size: the pack can outrun it
    private const float PROWL_RING = 3f;             // body sizes from the nest it prowls to
    private const float FIRST_PROWL = 0.5f;          // share of LonerProwlInterval until its first prowl: the pack meets it early

    private float _prowlTimer;
    private float _prowlLeft;
    private bool _wasRespectful;

    /// <summary>Every lone beast that was freed since the last frame is forgotten; Society calls it first in a frame.</summary>
    public void ForgetGone()
    {
        foreach (RivalTribe loner in _loners)
        {
            loner.ForgetGone();
        }
    }

    /*
      One physics frame of the lone beast:
      > Dead or adopted: its lair is abandoned (Defeat), nothing more happens.
      > Its pace is a share of the player's walking pace, so a pack can run from it.
      > Its respect fades like that of the packs; the first time it respects the player: Humbled.
      > Pack stage, hostile, not respectful, not calmed: every LonerProwlInterval it walks to
        the player's nest and stays LonerProwlSeconds (PackManners lets it attack whom it
        finds), then it walks home.
    */
    /// <summary>One physics frame of the lone beast, nothing before it settled.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Update(float dt)
    {
        float size = Player.BodySize();
        foreach (RivalTribe loner in _loners)
        {
            loner.Update(dt, size);
            if (loner.IsDefeated)
            {
                continue;
            }
            if (0 == loner.Kin.Count)
            {
                loner.Defeat();
                continue;
            }
            Kin beast  = loner.Kin[0];
            beast.Pace = CellBody.SwimSpeed(size) * Balance.Current.Creature.Land.WalkSpeed * PACE;

            loner.ShiftRespect(-Tune.RespectDrift * dt);
            if (loner.IsRespectful && !_wasRespectful)
            {
                Humbled?.Invoke(loner);
            }
            _wasRespectful = loner.IsRespectful;

            // The prowl to the player's nest
            bool isProwler = !Player.IsTribal && null != _pack.Nest && loner.IsHostile && !loner.IsRespectful && !beast.IsCalm;
            if (_prowlLeft > 0f)
            {
                _prowlLeft -= dt;
                if (_prowlLeft <= 0f || !isProwler)
                {
                    _prowlLeft = 0f;
                    beast.OrderMove(loner.Fire);
                    Log.Verbose("LonerKeeper", "The lone beast walks home.");
                }
                continue;
            }
            _prowlTimer -= isProwler ? dt : 0f;
            if (isProwler && _prowlTimer <= 0f && beast.IsIdle)
            {
                _prowlTimer = Tune.LonerProwlInterval;
                _prowlLeft  = Tune.LonerProwlSeconds;
                beast.OrderMove(_pack.Nest.Position + Vector2.FromAngle(Rand.Angle()) * size * PROWL_RING);
                Log.Verbose("LonerKeeper", "The lone beast prowls to the nest.");
            }
        }
    }

    // ===========================================================================
    // =================================   SAVE   ================================
    // ===========================================================================
    //
    //   Save      Relation, respect and whether the beast is gone.
    //   Restore   The beast of a save: a new lair, its standing, or gone for good.
    //

    /// <summary>The lone beast as plain save data (Core/Save/SaveGame.cs), null before it settled.</summary>
    public SaveRival Save()
    {
        return 0 == _loners.Count ? null : new SaveRival(_loners[0].Relation, _loners[0].Respect, _loners[0].IsDefeated);
    }

    /// <summary>Takes over the lone beast of a save, the pack (nest) and the neighbors are restored already. Without one in the save a new beast moves in.</summary>
    /// <param name="saved">(Required) The saved beast, null for a save from before the beast.</param>
    public void Restore(SaveRival saved)
    {
        Settle(saved?.Relation);
        if (0 == _loners.Count || null == saved)
        {
            return;
        }
        RivalTribe loner = _loners[0];
        loner.ShiftRespect(saved.Respect - loner.Respect);
        _wasRespectful = loner.IsRespectful;
        if (saved.IsDefeated)
        {
            foreach (Kin beast in new List<Kin>(loner.Kin))
            {
                beast.QueueFree();
            }
            loner.Defeat();
        }
        Log.Verbose("LonerKeeper", $"Lone beast restored (gone: {saved.IsDefeated}).");
    }
}
