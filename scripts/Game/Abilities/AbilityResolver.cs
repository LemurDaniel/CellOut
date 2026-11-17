// NOTE:
//   Runs the abilities of the cell parts (decision of the user 2026-11-17: parts are
//   abilities, not small stat bonuses) for the player and the NPC cells with the same
//   rules: abilities on the keys 1 to 4, abilities that fire by themselves, the choices
//   of the NPC cells, the zones they leave and the shoves they give. The two sides are
//   the player and the NPC cells, nobody hurts its own side. Damage of the player is
//   scaled to its size like its spikes, damage of a cell is not (like its stings).
//   It does not process on its own: Main calls Resolve once per physics frame, after
//   the contacts, and Clear when the cells leave.
//
//     Part            Fires      What happens (numbers in data/balance.json, cell.abilities)
//     Flagellum       key        Dash: shove forward, Power swim speeds.
//     Cilia           key        Dodge: shove sideways, away from the closest threat.
//     JetNozzle       key        Blast out of every nozzle: cells within Reach in its cone are
//                                shoved Power swim speeds away and take Damage, the recoil
//                                shoves the owner back.
//     SlimeGland      key        Lays slime behind it for Duration: patches of Reach sizes stay
//                                Linger seconds, the other side keeps Power of its speed.
//     Chromatophore   key        Cloak for Duration: others barely notice it, it fades.
//     Electrocyte     auto       A bolt into up to Count cells within Reach: Damage, stun Duration.
//     ToxinGland      auto       Hit: a cloud of Reach sizes for Linger seconds, Damage per
//                                second and Power of the speed for the other side.
//     Jaw             auto       Snap: prey within Reach in front of a jaw, shove Power swim speeds at it.
//     Pseudopod       auto       Grab: food or prey within Reach is pulled in at Power sizes per second.
//     ArmorPlate      auto       Shield: blocks a whole hit from its side (Player, Critter).
//     LightSail, FilterMouth, Sucker, Spike, Nematocyst: all the time, in Player and ContactResolver.
//
//   var abilities = new AbilityResolver();
//   abilities.Setup(session);
//   AddChild(abilities);
//   abilities.Resolve(dt, viewRadius);   // in _PhysicsProcess, after the contacts

//
//   Chapters:
//     SETTINGS            The session, the effects and the keys.
//     RESOLVE ABILITIES   One physics frame for both sides.
//     KEY ABILITIES       Dash, dodge, blast, slime and cloak.
//     AUTO ABILITIES      Bolt, cloud, snap and grab.
//     NPC CHOICES         When a cell fires its key abilities.
//     HELPERS             Sides, speeds, hits and points on the rim.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class AbilityResolver : Node
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Setup                The session with player and layers, before AddChild.
    //   _Ready               Zones and particles, both in 3D next to the cells.
    //   _UnhandledKeyInput   Keys 1 to 4 queue the abilities of the slots.
    //   Clear                Removes zones, grabs and particles (organism, fresh start).
    //

    private static readonly Key[] SLOT_KEYS = { Key.Key1, Key.Key2, Key.Key3, Key.Key4 };

    private WorldSession _session;
    private AbilityZones _zones;
    private AbilityParticles _particles;
    private readonly Queue<int> _pressedSlots = new();

    private Player Player => _session.Player;
    private Node2D CritterLayer => _session.CritterLayer;
    private Node2D FoodLayer => _session.FoodLayer;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player and layers.</param>
    public void Setup(WorldSession session)
    {
        _session = session;
    }

    public override void _Ready()
    {
        _zones     = new AbilityZones();
        _particles = new AbilityParticles();
        AddChild(_zones);
        AddChild(_particles);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Player.IsOrganism || @event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;     // a creature fires its own abilities (CreatureAbilityResolver)
        }
        int slot = System.Array.IndexOf(SLOT_KEYS, key.PhysicalKeycode);
        if (slot >= 0)
        {
            _pressedSlots.Enqueue(slot);
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Removes every zone and grab, the cells left the water (organism) or the game starts anew.</summary>
    public void Clear()
    {
        _zones.Clear();
        _grabs.Clear();
        _pressedSlots.Clear();
    }

    /// <summary>Fires the ability on a key slot of the player, for the smoke test and the HUD.</summary>
    /// <param name="slot">(Required) 0 to 3, the keys 1 to 4.</param>
    /// <returns>True when it fired.</returns>
    public bool UseSlot(int slot)
    {
        IReadOnlyList<PartKind> slots = Player.Abilities.Slots;
        return slot >= 0 && slot < slots.Count && UseKey(Player, slots[slot]);
    }

    // ===========================================================================
    // ==========================   RESOLVE ABILITIES   ==========================
    // ===========================================================================
    //
    //   1. Bodies    The player and the NPC cells in view, cooldowns tick.
    //   2. Zones     Clouds and slime age, hurt and slow the other side.
    //   3. Player    Queued keys fire, then its auto abilities.
    //   4. Cells     Auto abilities and the choices of every NPC cell in view.
    //   5. Motion    Slime laying, cloak, grabs and shoves.
    //   6. Effects   Particles and bolts move and fade.
    //

    private const float VIEW_MARGIN = 1.2f;       // NPC cells this many view radii away still use abilities

    /// <summary>One physics frame of abilities, after the contacts.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    /// <param name="viewRadius">(Required) Visible radius on the plane, farther cells rest.</param>
    /// <example><code>abilities.Resolve(dt, followCamera.ViewRadius());</code></example>
    public void Resolve(float dt, float viewRadius)
    {
        // ---------------------------------------------------------------------------
        // Bodies
        //   The player and the NPC cells in view, cooldowns tick.
        // ---------------------------------------------------------------------------

        var critters = new List<Critter>();
        foreach (Node node in CritterLayer.GetChildren())
        {
            if (node is Critter critter && !critter.IsQueuedForDeletion() && !critter.IsEngulfed
                && critter.Position.DistanceTo(Player.GlobalPosition) < viewRadius * VIEW_MARGIN)
            {
                critters.Add(critter);
            }
        }
        bool isPlayerCell = !Player.IsOrganism && !Player.IsHidden && !Player.IsDead;
        var  bodies       = new List<CellBody>(critters);
        if (isPlayerCell)
        {
            bodies.Add(Player);
        }
        foreach (CellBody body in bodies)
        {
            body.Abilities.Tick(dt);
        }

        // ---------------------------------------------------------------------------
        // Zones
        //   Clouds and slime age, hurt and slow the other side.
        // ---------------------------------------------------------------------------

        _zones.Advance(dt);
        foreach (CellBody body in bodies)
        {
            bool isPlayerSide = body == Player;
            _zones.Affect(body.GlobalPosition, body.BodySize(), isPlayerSide, out float poison, out float slow);
            body.SlowFactor = slow;
            if (poison > 0f)
            {
                Vector2 source = body.GlobalPosition + Vector2.Right;     // a cloud has no side, no plate catches it
                HurtSteady(body, poison * dt, source);
            }
        }

        // ---------------------------------------------------------------------------
        // Player
        //   Queued keys fire, then its auto abilities.
        // ---------------------------------------------------------------------------

        while (_pressedSlots.Count > 0)
        {
            int slot = _pressedSlots.Dequeue();
            if (isPlayerCell)
            {
                UseSlot(slot);
            }
        }
        if (isPlayerCell)
        {
            RunAutos(Player);
        }

        // ---------------------------------------------------------------------------
        // Cells
        //   Auto abilities and the choices of every NPC cell in view.
        // ---------------------------------------------------------------------------

        foreach (Critter critter in critters)
        {
            if (critter.IsQueuedForDeletion())
            {
                continue;
            }
            if (isPlayerCell)
            {
                RunAutos(critter);
                ChooseKeys(critter, dt);
            }
            critter.WasHit = false;
        }
        Player.WasHit = false;

        // ---------------------------------------------------------------------------
        // Motion
        //   Slime laying, cloak, grabs and shoves.
        // ---------------------------------------------------------------------------

        foreach (CellBody body in bodies)
        {
            if (!IsInstanceValid(body) || body.IsQueuedForDeletion())
            {
                continue;
            }
            LaySlime(body, dt);
            UpdateCloak(body);
            body.Position += body.Push * dt;
            body.Push     *= Mathf.Exp(-PUSH_DECAY * dt);
        }
        UpdateGrabs(dt);

        // ---------------------------------------------------------------------------
        // Effects
        //   Particles and bolts move and fade.
        // ---------------------------------------------------------------------------

        if (isPlayerCell)
        {
            ShowPassives(Player, dt);
        }
        _particles.Advance(dt);
    }

    // ===========================================================================
    // ============================   KEY ABILITIES   ============================
    // ===========================================================================
    //
    //   UseKey       Fires one key ability of a body, false while it recharges.
    //   Dash         Flagellum: shove along the way it swims.
    //   Dodge        Cilia: shove sideways, away from the closest threat.
    //   Blast        Jet nozzle: a pressure wave out of every nozzle, the recoil shoves back.
    //   LaySlime     Slime gland: patches behind the body while it runs.
    //   UpdateCloak  Chromatophores: hidden and faded while it runs.
    //

    private const float PUSH_DECAY = 5f;           // a shove loses this share per second
    private const float BLAST_ARC = 0.6f;          // half angle of the pressure wave, radians
    private const float RECOIL = 0.5f;             // share of the blast the nozzle shoves back
    private const float SLIME_STEP = 0.12f;        // seconds between two slime patches
    private const float PLAYER_CLOAK_FADE = 0.6f;  // the player still sees itself
    private const float CELL_CLOAK_FADE = 0.82f;

    /// <summary>Fires one key ability, its effect at once (shoves) or while it runs (slime, cloak).</summary>
    private bool UseKey(CellBody body, PartKind kind)
    {
        if (!body.Abilities.TryUse(kind))
        {
            return false;
        }
        switch (kind)
        {
            case PartKind.Flagellum:
                Dash(body);
                break;
            case PartKind.Cilia:
                Dodge(body);
                break;
            case PartKind.JetNozzle:
                Blast(body);
                break;
            case PartKind.SlimeGland:
            case PartKind.Chromatophore:
                break;      // run in Motion while the ability runs
            default:
                throw new System.ArgumentOutOfRangeException(nameof(kind), $"kind must be a key ability, got '{kind}'.");
        }
        Log.Verbose("AbilityResolver", $"{Who(body)} fires {kind}.");
        return true;
    }

    /// <summary>Flagellum: a shove of Power swim speeds along the way it swims (or faces).</summary>
    private void Dash(CellBody body)
    {
        Vector2 way = body.Velocity.LengthSquared() > 1f ? body.Velocity.Normalized() : Vector2.FromAngle(body.Facing);
        body.Push += way * SpeedOf(body) * AbilitySet.Tune(PartKind.Flagellum).Power;
        for (int i = 0; i < 10; i++)
        {
            Vector2 spot = body.GlobalPosition - way * body.BodySize() * Rand.Range(0.6f, 1.2f) + way.Orthogonal() * body.BodySize() * Rand.Range(-0.4f, 0.4f);
            _particles.Emit(Plane3D.To3D(spot), Plane3D.To3D(-way * SpeedOf(body) * Rand.Range(0.3f, 0.8f)), body.BodySize() * 0.25f, new Color(0.85f, 0.95f, 1f, 0.35f), 0.7f);
        }
    }

    /// <summary>Cilia: a shove of Power swim speeds sideways, to the side away from the closest threat.</summary>
    private void Dodge(CellBody body)
    {
        Vector2 forward = Vector2.FromAngle(body.Facing);
        Vector2 side    = forward.Orthogonal();
        CellBody threat = Enemies(body).OrderBy(enemy => enemy.GlobalPosition.DistanceSquaredTo(body.GlobalPosition)).FirstOrDefault();
        if (null != threat && (threat.GlobalPosition - body.GlobalPosition).Dot(side) > 0f)
        {
            side = -side;
        }
        body.Push += side * SpeedOf(body) * AbilitySet.Tune(PartKind.Cilia).Power;
    }

    /// <summary>Jet nozzle: out of every nozzle a pressure wave shoves the cells in its cone away and hurts them, the recoil shoves the owner back.</summary>
    private void Blast(CellBody body)
    {
        AbilityBalance tune  = AbilitySet.Tune(PartKind.JetNozzle);
        float          size  = body.BodySize();
        float          speed = SpeedOf(body);
        foreach (PlacedPart nozzle in body.Design.Parts.Where(part => PartKind.JetNozzle == part.Kind))
        {
            float   angle = body.Facing + nozzle.Angle;
            Vector2 aim   = Vector2.FromAngle(angle);
            foreach (CellBody target in Enemies(body))
            {
                Vector2 toTarget = target.GlobalPosition - body.GlobalPosition;
                float   gap      = toTarget.Length() - size - target.BodySize();
                if (gap > size * tune.Reach || Mathf.Abs(Mathf.Wrap(toTarget.Angle() - angle, -Mathf.Pi, Mathf.Pi)) > BLAST_ARC)
                {
                    continue;
                }
                target.Push += toTarget.Normalized() * speed * tune.Power;
                Hit(body, target, tune.Damage);
            }
            body.Push -= aim * speed * tune.Power * RECOIL;

            // The jet: bubbles shooting out of the nozzle in its cone
            Vector2 mouth = RimPoint(body, nozzle.Angle);
            for (int i = 0; i < 16; i++)
            {
                Vector2 way = Vector2.FromAngle(angle + Rand.Range(-BLAST_ARC, BLAST_ARC));
                _particles.Emit(Plane3D.To3D(mouth), Plane3D.To3D(way * size * tune.Reach * Rand.Range(2f, 4f)), size * Rand.Range(0.12f, 0.3f), new Color(0.8f, 0.92f, 1f, 0.45f), Rand.Range(0.4f, 0.8f));
            }
        }
    }

    /// <summary>Slime gland: while it runs, a patch every SLIME_STEP seconds behind the body.</summary>
    private void LaySlime(CellBody body, float dt)
    {
        if (!body.Abilities.IsRunning(PartKind.SlimeGland))
        {
            return;
        }
        _slimeTimers[body.GetInstanceId()] = _slimeTimers.GetValueOrDefault(body.GetInstanceId()) - dt;
        if (_slimeTimers[body.GetInstanceId()] > 0f)
        {
            return;
        }
        _slimeTimers[body.GetInstanceId()] = SLIME_STEP;
        AbilityBalance tune   = AbilitySet.Tune(PartKind.SlimeGland);
        Vector2        behind = body.Velocity.LengthSquared() > 1f ? -body.Velocity.Normalized() : -Vector2.FromAngle(body.Facing);
        Vector2        spot   = body.GlobalPosition + behind * body.BodySize() * 0.8f;
        _zones.Add(new AbilityZones.Zone(ZoneKind.Slime, spot, body.BodySize() * tune.Reach, tune.Linger, 0f, tune.Power, body == Player));
    }

    private readonly Dictionary<ulong, float> _slimeTimers = new();

    /// <summary>Chromatophores: hidden and faded while the cloak runs, back to normal after.</summary>
    private void UpdateCloak(CellBody body)
    {
        bool isCloaked = body.Abilities.IsRunning(PartKind.Chromatophore);
        if (isCloaked)
        {
            body.Fade = body == Player ? PLAYER_CLOAK_FADE : CELL_CLOAK_FADE;
        }
        else if (body.IsCloaked)
        {
            body.Fade = 0f;
        }
        body.IsCloaked = isCloaked;
    }

    // ===========================================================================
    // ============================   AUTO ABILITIES   ===========================
    // ===========================================================================
    //
    //   RunAutos      Every auto ability of a body that has its moment.
    //   Bolt          Electrocyte: a bolt into the closest cells of the other side.
    //   Cloud         Toxin gland: a poison cloud right after a hit.
    //   Snap          Jaw: a lunge at prey in front of a jaw.
    //   Snatch        Pseudopod: pulls food or prey within reach.
    //   UpdateGrabs   Moves grabbed things to their owner, the arm reaches out meanwhile.
    //

    private const float GRAB_SECONDS = 0.9f;

    /// <summary>A grab in progress: the thing (food or a body) slides towards its owner.</summary>
    private sealed record Grab(Node2D Target, CellBody Owner)
    {
        public float Left { get; set; } = GRAB_SECONDS;
    }

    private readonly List<Grab> _grabs = new();

    /// <summary>Every auto ability of a body whose moment came, each only when charged.</summary>
    private void RunAutos(CellBody body)
    {
        AbilitySet abilities = body.Abilities;
        if (abilities.IsReady(PartKind.Electrocyte))
        {
            Bolt(body);
        }
        if (body.WasHit && abilities.IsReady(PartKind.ToxinGland))
        {
            Cloud(body);
        }
        if (abilities.IsReady(PartKind.Jaw))
        {
            Snap(body);
        }
        if (abilities.IsReady(PartKind.Pseudopod))
        {
            Snatch(body);
        }
    }

    /// <summary>Electrocyte: up to Count closest cells of the other side within Reach, a jagged bolt jumps from one to the next.</summary>
    private void Bolt(CellBody body)
    {
        AbilityBalance tune    = AbilitySet.Tune(PartKind.Electrocyte);
        float          size    = body.BodySize();
        List<CellBody> targets = Enemies(body)
            .Where(target => Gap(body, target) <= size * tune.Reach)
            .OrderBy(target => target.GlobalPosition.DistanceSquaredTo(body.GlobalPosition))
            .Take(tune.Count)
            .ToList();
        if (0 == targets.Count || !body.Abilities.TryUse(PartKind.Electrocyte))
        {
            return;
        }
        var chain = new List<Vector3> { Plane3D.To3D(body.GlobalPosition, size * 0.3f) };
        foreach (CellBody target in targets)
        {
            chain.Add(Plane3D.To3D(target.GlobalPosition, target.BodySize() * 0.3f));
            Hit(body, target, tune.Damage);
            target.Stun(tune.Duration);
        }
        _particles.Bolt(chain, size * 0.06f);
        Log.Verbose("AbilityResolver", $"{Who(body)} discharges into {targets.Count} cell(s).");
    }

    /// <summary>Toxin gland: right after a hit a poison cloud wells up around the body.</summary>
    private void Cloud(CellBody body)
    {
        if (!body.Abilities.TryUse(PartKind.ToxinGland))
        {
            return;
        }
        AbilityBalance tune   = AbilitySet.Tune(PartKind.ToxinGland);
        float          size   = body.BodySize();
        float          damage = tune.Damage * DamageScale(body);
        _zones.Add(new AbilityZones.Zone(ZoneKind.Poison, body.GlobalPosition, size * tune.Reach, tune.Linger, damage, tune.Power, body == Player));
        for (int i = 0; i < 18; i++)
        {
            Vector2 way = Vector2.FromAngle(Rand.Angle());
            _particles.Emit(Plane3D.To3D(body.GlobalPosition + way * size * 0.8f), Plane3D.To3D(way * size * tune.Reach * Rand.Range(1.5f, 3f)), size * Rand.Range(0.2f, 0.45f), new Color(0.65f, 1f, 0.3f, 0.3f), Rand.Range(0.6f, 1.1f));
        }
        Log.Verbose("AbilityResolver", $"{Who(body)} sprays a poison cloud.");
    }

    /// <summary>Jaw: prey within Reach in front of a jaw, the body lunges at it, the bite itself is the contact.</summary>
    private void Snap(CellBody body)
    {
        AbilityBalance tune  = AbilitySet.Tune(PartKind.Jaw);
        float          size  = body.BodySize();
        CellBody       prey  = Enemies(body)
            .Where(target => IsPrey(body, target) && Gap(body, target) <= size * tune.Reach && Gap(body, target) > 0f && JawsToward(body, target))
            .OrderBy(target => target.GlobalPosition.DistanceSquaredTo(body.GlobalPosition))
            .FirstOrDefault();
        if (null == prey || !body.Abilities.TryUse(PartKind.Jaw))
        {
            return;
        }
        body.Push += (prey.GlobalPosition - body.GlobalPosition).Normalized() * SpeedOf(body) * tune.Power;
    }

    /// <summary>Pseudopod: the closest food or prey within Reach (not yet touching) is pulled in.</summary>
    private void Snatch(CellBody body)
    {
        AbilityBalance tune  = AbilitySet.Tune(PartKind.Pseudopod);
        float          size  = body.BodySize();
        float          reach = size * tune.Reach;
        Node2D         thing = Enemies(body)
            .Where(target => IsPrey(body, target) && Gap(body, target) > 0f && Gap(body, target) <= reach)
            .OrderBy(target => target.GlobalPosition.DistanceSquaredTo(body.GlobalPosition))
            .FirstOrDefault();

        // The player also snatches food, the NPC cells graze on their own
        if (null == thing && body == Player)
        {
            thing = FoodLayer.GetChildren().OfType<Food>()
                .Where(food => !food.IsEngulfed && !food.IsQueuedForDeletion())
                .Where(food => food.Position.DistanceTo(body.GlobalPosition) - size > 0f && food.Position.DistanceTo(body.GlobalPosition) - size <= reach)
                .OrderBy(food => food.Position.DistanceSquaredTo(body.GlobalPosition))
                .FirstOrDefault();
        }
        if (null == thing || _grabs.Any(grab => grab.Target == thing) || !body.Abilities.TryUse(PartKind.Pseudopod))
        {
            return;
        }
        _grabs.Add(new Grab(thing, body));
    }

    /// <summary>Grabbed things slide to their owner at Power sizes per second until they touch, the arm reaches out meanwhile.</summary>
    private void UpdateGrabs(float dt)
    {
        float pull = AbilitySet.Tune(PartKind.Pseudopod)?.Power ?? 0f;
        for (int i = _grabs.Count - 1; i >= 0; i--)
        {
            Grab grab = _grabs[i];
            grab.Left -= dt;
            bool isGone = !IsInstanceValid(grab.Target) || grab.Target.IsQueuedForDeletion() || !IsInstanceValid(grab.Owner) || grab.Owner.IsQueuedForDeletion();
            if (isGone || grab.Left <= 0f || (grab.Target is Food food && food.IsEngulfed) || (grab.Target is Critter caught && caught.IsEngulfed))
            {
                _grabs.RemoveAt(i);
                continue;
            }
            float   size     = grab.Owner.BodySize();
            Vector2 toOwner  = grab.Owner.GlobalPosition - grab.Target.GlobalPosition;
            float   touching = size + (grab.Target is CellBody target ? target.BodySize() : 0f) * 0.9f;
            if (toOwner.Length() <= touching)
            {
                _grabs.RemoveAt(i);
                continue;
            }
            grab.Target.Position += toOwner.LimitLength(size * pull * dt);
            grab.Owner.EngulfReach = Mathf.Max(grab.Owner.EngulfReach, 0.8f);
            grab.Owner.EngulfPoint = grab.Target.GlobalPosition;
        }
    }

    // ===========================================================================
    // =============================   NPC CHOICES   =============================
    // ===========================================================================
    //
    //   ChooseKeys   A cell fires its key abilities when the moment fits, with a short reaction time.
    //

    private const float REACTION = 2f;            // chance per second a cell reacts once the moment fits
    private const float THREAT_RANGE = 6f;        // a bigger player this many player sizes away is a threat
    private const float HUNT_RANGE = 5f;          // a cell hunts the player from this many own sizes
    private const float DODGE_RANGE = 1.5f;       // dodges once the threat is this many player sizes off its membrane

    /*
      The moment of each key ability of a cell:
      - Flagellum:       fleeing from the player or hunting it
      - Cilia:           the threat is right at its membrane
      - Jet nozzle:      the player is inside the cone of a nozzle
      - Slime gland:     fleeing
      - Chromatophores:  fleeing
      Every moment fires only with a chance of REACTION per second, so the cells do not
      answer within the same frame.
    */
    private void ChooseKeys(Critter critter, float dt)
    {
        if (0 == critter.Abilities.Slots.Count || !Rand.Chance(dt * REACTION))
        {
            return;
        }
        float playerSize  = Player.BodySize();
        float distance    = critter.GlobalPosition.DistanceTo(Player.GlobalPosition);
        float gap         = Gap(critter, Player);
        bool  isThreat    = Player.Stage >= Player.SINGLE_STAGE && playerSize > critter.BodySize() * Balance.Current.Critter.EatRatio && distance < playerSize * THREAT_RANGE;
        bool  isHunting   = critter.CanEat(playerSize) && !Player.IsCloaked && distance < critter.BodySize() * HUNT_RANGE;
        foreach (PartKind kind in critter.Abilities.Slots)
        {
            bool isMoment = kind switch
            {
                PartKind.Flagellum     => isThreat || isHunting,
                PartKind.Cilia         => isThreat && gap < playerSize * DODGE_RANGE,
                PartKind.JetNozzle     => IsInNozzleCone(critter, Player),
                PartKind.SlimeGland    => isThreat,
                PartKind.Chromatophore => isThreat,
                _                      => false,
            };
            if (isMoment && UseKey(critter, kind))
            {
                return;     // one ability per reaction
            }
        }
    }

    /// <summary>Whether the target sits inside the reach and cone of one of the nozzles of a body.</summary>
    private static bool IsInNozzleCone(CellBody body, CellBody target)
    {
        AbilityBalance tune     = AbilitySet.Tune(PartKind.JetNozzle);
        Vector2        toTarget = target.GlobalPosition - body.GlobalPosition;
        if (Gap(body, target) > body.BodySize() * tune.Reach)
        {
            return false;
        }
        return body.Design.Parts.Any(part => PartKind.JetNozzle == part.Kind
            && Mathf.Abs(Mathf.Wrap(toTarget.Angle() - body.Facing - part.Angle, -Mathf.Pi, Mathf.Pi)) <= BLAST_ARC);
    }

    // ===========================================================================
    // ===============================   HELPERS   ===============================
    // ===========================================================================
    //
    //   Enemies       The other side: the NPC cells for the player, the player for a cell.
    //   IsPrey        Whether a body could swallow or bite the target.
    //   JawsToward    Whether a jaw of the body points at the target.
    //   Gap           Distance between two membranes.
    //   SpeedOf       Swim speed of a body, the unit of every shove.
    //   DamageScale   The player hits harder the bigger it is, a cell like at the reference size.
    //   Hit           One hit of an ability, a plate may block it.
    //   HurtSteady    Damage per frame (clouds), no plate catches it.
    //   RimPoint      A point on the membrane at a part angle.
    //   ShowPassives  The vortex swirl of filter mouths and the glint of the light sails.
    //   Who           A short name for the log.
    //

    /// <summary>The other side of a body: every NPC cell in view for the player, the player for a cell (while it is a cell).</summary>
    private IEnumerable<CellBody> Enemies(CellBody body)
    {
        if (body != Player)
        {
            return Player.IsOrganism || Player.IsDead || Player.IsHidden ? Enumerable.Empty<CellBody>() : new CellBody[] { Player };
        }
        return CritterLayer.GetChildren().OfType<Critter>().Where(critter => !critter.IsQueuedForDeletion() && !critter.IsEngulfed);
    }

    /// <summary>Whether a body could swallow the target or bite it with a jaw towards it.</summary>
    private bool IsPrey(CellBody body, CellBody target)
    {
        if (body == Player)
        {
            float direction = (target.GlobalPosition - body.GlobalPosition).Angle();
            return Player.Stage >= Player.SINGLE_STAGE && Player.BodySize() > target.BodySize() * Player.EatRatioToward(direction);
        }
        return body is Critter critter && critter.CanEat(target.BodySize()) && !target.IsCloaked;
    }

    private static bool JawsToward(CellBody body, CellBody target)
    {
        float     local = (target.GlobalPosition - body.GlobalPosition).Angle() - body.Facing;
        CellStats stats = body is Player player ? player.CellTraits : ((Critter)body).Stats;
        return stats.CanBiteToward(local);
    }

    private static float Gap(CellBody body, CellBody target)
    {
        return body.GlobalPosition.DistanceTo(target.GlobalPosition) - body.BodySize() - target.BodySize();
    }

    private static float SpeedOf(CellBody body)
    {
        return body is Player player ? player.CurrentSpeed() : CellBody.SwimSpeed(body.BodySize());
    }

    private float DamageScale(CellBody body)
    {
        return body == Player ? Player.BodySize() / CellBody.REFERENCE_SIZE : 1f;
    }

    /// <summary>One hit of an ability on the other side, a charged plate towards the owner blocks it.</summary>
    private void Hit(CellBody owner, CellBody target, float damage)
    {
        float scaled = damage * DamageScale(owner);
        if (target is Critter critter)
        {
            critter.Hurt(scaled, owner.GlobalPosition);
        }
        else if (target is Player player)
        {
            player.TakeDamage(scaled, owner.GlobalPosition);
        }
    }

    /// <summary>Damage of this frame from a cloud, no plate catches it.</summary>
    private static void HurtSteady(CellBody body, float amount, Vector2 source)
    {
        if (body is Critter critter)
        {
            critter.Hurt(amount, source, isSteady: true);
        }
        else if (body is Player player)
        {
            player.Digest(amount, source);
        }
    }

    /// <summary>A point on the membrane of a body at a part angle (local, 0 = front).</summary>
    private static Vector2 RimPoint(CellBody body, float partAngle)
    {
        return body.GlobalPosition + Vector2.FromAngle(body.Facing + partAngle) * body.BodySize();
    }

    private const float SWIRL_RATE = 14f;          // vortex particles per second around a filter mouth
    private const float GLINT_RATE = 4f;           // glints per second over a light sail

    /// <summary>Filter mouths swirl water in, light sails glint all the time.</summary>
    private void ShowPassives(Player player, float dt)
    {
        float size = player.BodySize();
        foreach (PlacedPart part in player.Design.Parts)
        {
            Vector2 rim = RimPoint(player, part.Angle);
            if (PartKind.FilterMouth == part.Kind && Rand.Chance(dt * SWIRL_RATE))
            {
                // A particle out at the edge of the pull range spirals into the mouth
                Vector2 from   = rim + Vector2.FromAngle(Rand.Angle()) * size * player.CellTraits.MagnetBonus * Rand.Range(0.4f, 1f);
                Vector2 inward = (rim - from);
                Vector2 swirl  = inward.Orthogonal() * 0.6f;
                _particles.Emit(Plane3D.To3D(from), Plane3D.To3D((inward + swirl) * 1.4f), size * 0.12f, new Color(0.75f, 0.95f, 1f, 0.35f), 0.8f);
            }
            if (PartKind.LightSail == part.Kind && Rand.Chance(dt * GLINT_RATE))
            {
                Vector2 spot = rim + Vector2.FromAngle(Rand.Angle()) * size * 0.3f;
                _particles.Emit(Plane3D.To3D(spot, size * 0.3f), Vector3.Up * size * 0.6f, size * 0.15f, new Color(1f, 0.95f, 0.55f, 0.5f), 1f);
            }
        }
    }

    private string Who(CellBody body)
    {
        return body == Player ? "Player" : body is Critter critter ? critter.Kind.ToString() : body.GetType().Name;
    }
}
