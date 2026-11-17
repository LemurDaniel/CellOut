// NOTE:
//   Runs the abilities of the creature's ability parts (wish of the user 2026-11-17, tabs
//   Gentle, Clever and Wild of the creature editor), in the sea and on land alike. The
//   strangers are the creatures of the sea and the animals and villagers of the foreign
//   packs and tribes. In the sea every ability is a key slot (keys 1 to 4), from the pack on
//   it sits in the fan of its way (Game/Ui/AbilitySlots.cs: court gentle, display clever,
//   hunt wild; keys 1 to 3 open them, PackEncounter). It also carries
//   the player along its shoves (a charge) in every creature stage. Feedback comes from
//   bodies and particles, no rings on the ground.
//
//     Part          Tab      What happens (numbers in data/balance.json, creature.abilities)
//     ScentGland    Gentle   Calm: strangers within Reach leave the player alone for Duration,
//                            foreign packs and tribes like it Power more.
//     HealingMoss   Gentle   Heal: Power of the health back, for the player and its pack within Reach.
//     GlowCrest     Clever   Flash: strangers within Reach are stunned for Duration.
//     InkSac        Clever   Ink: hidden for Duration, a shove of Power walking speeds away.
//     RoarSac       Wild     Roar: strangers within Reach flee for Duration.
//     RamBrow       Wild     Charge: a shove of Power speeds forward, strangers it runs into
//                            take Damage and fly aside, for Duration.
//
//   var abilities = new CreatureAbilityResolver();
//   abilities.Setup(session, pack, rivals, encounter);
//   AddChild(abilities);
//   abilities.Resolve(dt);   // in _PhysicsProcess, after the society

//
//   Chapters:
//     SETTINGS   The session, the society parts, the particles and the keys.
//     RESOLVE    One physics frame: cooldowns, keys, the charge, the shove.
//     USE        The six abilities.
//     HELPERS    Strangers, hits and the key offset.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class CreatureAbilityResolver : Node
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Setup                The session, the pack, the rivals and the encounters, before AddChild.
    //   _Ready               The particles of the abilities.
    //   _UnhandledKeyInput   Keys 1 to 4 fire the slots in the sea; from the pack on the fans of the ways hold them.
    //   UseSlot              Fires the ability of a slot (key or click).
    //   UsePart              Fires the ability of a part (the fans of the ways).
    //

    private static readonly Key[] KEYS = { Key.Key1, Key.Key2, Key.Key3, Key.Key4, Key.Key5, Key.Key6, Key.Key7 };

    private WorldSession _session;
    private PackKeeper _pack;
    private RivalKeeper _rivals;
    private PackEncounter _encounter;
    private AbilityParticles _particles;

    private Player Player => _session.Player;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    public void Setup(WorldSession session, PackKeeper pack, RivalKeeper rivals, PackEncounter encounter)
    {
        _session   = session;
        _pack      = pack;
        _rivals    = rivals;
        _encounter = encounter;
    }

    public override void _Ready()
    {
        _particles = new AbilityParticles();
        AddChild(_particles);
    }

    /// <summary>Slots of the ways in front of the abilities: 3 from the pack on, else 0.</summary>
    public int SlotOffset => _encounter.IsActive ? System.Enum.GetValues<PackWay>().Length : 0;

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        // From the pack on the ability parts sit in the fans of the ways (Game/Ui/AbilitySlots.cs), keys 1-3 open those
        if (!Player.IsOrganism || SlotOffset > 0 || @event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }
        int index = System.Array.IndexOf(KEYS, key.PhysicalKeycode) - SlotOffset;
        if (index >= 0 && index < Player.CreatureAbilities.Slots.Count)
        {
            UseSlot(index);
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Fires the ability of one ability part, e.g. from the fan of a way.</summary>
    /// <returns>True when it fired.</returns>
    public bool UsePart(CreaturePartKind kind)
    {
        int slot = -1;
        for (int i = 0; i < Player.CreatureAbilities.Slots.Count; i++)
        {
            slot = kind == Player.CreatureAbilities.Slots[i] ? i : slot;
        }
        return slot >= 0 && UseSlot(slot);
    }

    /// <summary>Fires the ability on a slot of the creature (0 = its first ability, the ways not counted).</summary>
    /// <returns>True when it fired.</returns>
    public bool UseSlot(int slot)
    {
        IReadOnlyList<CreaturePartKind> slots = Player.CreatureAbilities.Slots;
        if (!Player.IsOrganism || Player.IsDead || slot < 0 || slot >= slots.Count || !Player.CreatureAbilities.TryUse(slots[slot]))
        {
            return false;
        }
        Use(slots[slot]);
        Log.Verbose("CreatureAbilityResolver", $"The creature uses {slots[slot]}.");
        return true;
    }

    // ===========================================================================
    // ================================   RESOLVE   ==============================
    // ===========================================================================
    //
    //   Resolve   1. Clock    Cooldowns and running effects tick.
    //             2. Ink      Hidden while the ink runs.
    //             3. Charge   Strangers in the way of a running charge are hit once.
    //             4. Shove    The player rides its shove, never into the sea from land.
    //

    private const float PUSH_DECAY = 5f;
    private readonly HashSet<CellBody> _rammed = new();

    /// <summary>One physics frame of the creature abilities.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Resolve(float dt)
    {
        if (!Player.IsOrganism)
        {
            return;
        }
        CreatureAbilitySet abilities = Player.CreatureAbilities;
        abilities.Tick(dt);

        // Ink: hidden while it runs (the senses of every stranger read SenseFactor)
        bool isInked = abilities.IsRunning(CreaturePartKind.InkSac);
        Player.IsCloaked = isInked;

        // Charge: whoever stands in the way while it runs is hit once and flies aside
        if (abilities.IsRunning(CreaturePartKind.RamBrow))
        {
            Ram();
        }
        else
        {
            _rammed.Clear();
        }

        // Shove: lunges of the hunt and charges carry the player
        if (Player.Push.LengthSquared() > 1f)
        {
            Vector2 next = Player.Position + Player.Push * dt;
            if (!Player.IsOnLand || null == _session.Coast || _session.Coast.IsOnLand(next))
            {
                Player.Position = next;
            }
            Player.Push *= Mathf.Exp(-PUSH_DECAY * dt);
        }
        _particles.Advance(dt);
    }

    // ===========================================================================
    // =================================   USE   =================================
    // ===========================================================================
    //
    //   Use     Starts the ability of a part: the instant ones act at once, ink and charge run.
    //   Calm    Scent gland: strangers leave the player alone, their people like it more.
    //   Heal    Healing moss: health back for the player and the pack nearby.
    //   Flash   Glow crest: strangers nearby are stunned.
    //   Ink     Ink sac: a dark cloud, a shove away from the closest stranger.
    //   Roar    Roar sac: strangers nearby flee.
    //   Charge  Ram brow: a shove forward, Ram hits whatever is in the way.
    //   Ram     One frame of a running charge.
    //

    private const float REAR_UP = 0.55f;

    private void Use(CreaturePartKind kind)
    {
        AbilityBalance tune = Player.CreatureAbilities.TuneOf(kind);
        float          size = Player.BodySize();
        switch (kind)
        {
            case CreaturePartKind.ScentGland:
                Calm(tune, size);
                break;
            case CreaturePartKind.HealingMoss:
                Heal(tune, size);
                break;
            case CreaturePartKind.GlowCrest:
                Flash(tune, size);
                break;
            case CreaturePartKind.InkSac:
                Ink(tune, size);
                break;
            case CreaturePartKind.RoarSac:
                Roar(tune, size);
                break;
            case CreaturePartKind.RamBrow:
                Charge(tune, size);
                break;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(kind), $"kind must be an ability part, got '{kind}'.");
        }
    }

    private void Calm(AbilityBalance tune, float size)
    {
        var liked = new HashSet<RivalTribe>();
        foreach (CellBody stranger in Strangers(size * tune.Reach))
        {
            if (stranger is Critter critter)
            {
                critter.Calm(tune.Duration);
            }
            else if (stranger is Kin kin)
            {
                if (kin.AttackTarget == Player)
                {
                    kin.OrderMove(kin.Position);     // drops the attack
                }
                RivalTribe owner = _rivals.Rivals.FirstOrDefault(rival => rival.Faction == kin.Faction);
                if (null != owner && liked.Add(owner))
                {
                    owner.Shift(tune.Power);
                }
            }
        }
        Burst(Player.GlobalPosition, size, new Color(0.95f, 0.65f, 0.9f, 0.4f), 24, size * tune.Reach * 0.5f, rise: true);
    }

    private void Heal(AbilityBalance tune, float size)
    {
        Player.Heal(tune.Power);
        foreach (Kin kin in _pack.Kin)
        {
            if (!GodotObject.IsInstanceValid(kin))
            {
                continue;       // freed since the last physics frame, read here outside of it
            }
            if (KinMode.Wild != kin.Mode && kin.GlobalPosition.DistanceTo(Player.GlobalPosition) <= size * tune.Reach)
            {
                kin.Heal(kin.MaxHp * tune.Power);
                Burst(kin.GlobalPosition, kin.Radius, new Color(0.55f, 1f, 0.5f, 0.5f), 6, kin.Radius, rise: true);
            }
        }
        Burst(Player.GlobalPosition, size, new Color(0.55f, 1f, 0.5f, 0.5f), 16, size, rise: true);
    }

    private void Flash(AbilityBalance tune, float size)
    {
        foreach (CellBody stranger in Strangers(size * tune.Reach))
        {
            stranger.Stun(tune.Duration);
        }
        Burst(Player.GlobalPosition, size * 1.5f, new Color(0.6f, 1f, 0.95f, 0.8f), 30, size * tune.Reach, rise: false);
    }

    private void Ink(AbilityBalance tune, float size)
    {
        CellBody closest = null;
        foreach (CellBody stranger in Strangers(size * 10f))
        {
            if (null == closest || stranger.GlobalPosition.DistanceSquaredTo(Player.GlobalPosition) < closest.GlobalPosition.DistanceSquaredTo(Player.GlobalPosition))
            {
                closest = stranger;
            }
            if (stranger is Kin kin && kin.AttackTarget == Player)
            {
                kin.OrderMove(kin.Position);     // it lost sight of the player
            }
        }
        Vector2 away = null != closest ? (Player.GlobalPosition - closest.GlobalPosition).Normalized() : Vector2.FromAngle(Player.Facing);
        Player.Push += away * Player.CurrentSpeed() * tune.Power;
        Burst(Player.GlobalPosition, size * 1.2f, new Color(0.45f, 0.25f, 0.6f, 0.55f), 26, size * 2f, rise: false, isLarge: true);     // the particles glow additively: a violet cloud reads as ink
    }

    private void Roar(AbilityBalance tune, float size)
    {
        Player.Strike(REAR_UP, 1f);
        foreach (CellBody stranger in Strangers(size * tune.Reach))
        {
            if (stranger is Critter critter)
            {
                critter.Scare(Player.GlobalPosition, tune.Duration);
            }
            else if (stranger is Kin kin)
            {
                Vector2 away = (kin.GlobalPosition - Player.GlobalPosition).Normalized();
                kin.OrderMove(kin.Position + away * size * tune.Reach);
            }
        }
        // The roar: air streaks rushing outwards, no ring
        for (int i = 0; i < 28; i++)
        {
            Vector2 way = Vector2.FromAngle(Rand.Angle());
            _particles.Emit(Plane3D.To3D(Player.GlobalPosition + way * size, size * 0.6f), Plane3D.To3D(way * size * tune.Reach * 2.5f), size * 0.18f, new Color(1f, 0.9f, 0.8f, 0.35f), 0.6f);
        }
    }

    private void Charge(AbilityBalance tune, float size)
    {
        Player.Push += Vector2.FromAngle(Player.Facing) * Player.CurrentSpeed() * tune.Power;
        Player.Strike(-0.2f, tune.Duration);     // head down into the charge
        _rammed.Clear();
    }

    /// <summary>A running charge: every stranger the player touches is hit once, takes Damage and flies aside.</summary>
    private void Ram()
    {
        AbilityBalance tune = Player.CreatureAbilities.TuneOf(CreaturePartKind.RamBrow);
        float          size = Player.BodySize();
        foreach (CellBody stranger in Strangers(size * 3f))
        {
            float contact = (size + stranger.BodySize()) * 1.1f;
            if (_rammed.Contains(stranger) || stranger.GlobalPosition.DistanceTo(Player.GlobalPosition) > contact)
            {
                continue;
            }
            _rammed.Add(stranger);
            Vector2 aside = (stranger.GlobalPosition - Player.GlobalPosition).Normalized();
            stranger.Position += aside * size * 1.5f;
            Hit(stranger, tune.Damage * size / CellBody.REFERENCE_SIZE);
            Burst(stranger.GlobalPosition, stranger.BodySize(), new Color(1f, 0.85f, 0.6f, 0.5f), 8, size, rise: false);
        }
    }

    // ===========================================================================
    // ===============================   HELPERS   ===============================
    // ===========================================================================
    //
    //   Strangers   The sea creatures and the animals of foreign packs and tribes within a range.
    //   Hit         Damage to a stranger, a dead kin is reported (meat, relation).
    //   Burst       A puff of particles around a point.
    //

    private List<CellBody> Strangers(float range)
    {
        var strangers = new List<CellBody>();
        Vector2 center = Player.GlobalPosition;
        foreach (Node node in _session.CritterLayer.GetChildren())
        {
            if (node is Critter critter && !critter.IsQueuedForDeletion() && critter.GlobalPosition.DistanceTo(center) - critter.BodySize() <= range)
            {
                strangers.Add(critter);
            }
        }
        foreach (RivalTribe rival in _rivals.Rivals)
        {
            foreach (Kin kin in rival.Kin)
            {
                if (!GodotObject.IsInstanceValid(kin))
                {
                    continue;       // freed since the last physics frame, read here outside of it
                }
                if (!kin.IsQueuedForDeletion() && kin.GlobalPosition.DistanceTo(center) - kin.Radius <= range)
                {
                    strangers.Add(kin);
                }
            }
        }
        return strangers;
    }

    private void Hit(CellBody stranger, float damage)
    {
        if (stranger is Critter critter)
        {
            critter.Hurt(damage, Player.GlobalPosition);
        }
        else if (stranger is Kin kin && kin.Hurt(damage))
        {
            _rivals.ReportKill(kin);
        }
    }

    private void Burst(Vector2 point, float size, Color color, int count, float reach, bool rise, bool isLarge = false)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 way    = Vector2.FromAngle(Rand.Angle());
            Vector3 drift  = Plane3D.To3D(way * reach * Rand.Range(0.6f, 1.4f)) + (rise ? Vector3.Up * size * Rand.Range(0.5f, 1.5f) : Vector3.Zero);
            float   bubble = size * Rand.Range(isLarge ? 0.4f : 0.12f, isLarge ? 0.8f : 0.3f);
            _particles.Emit(Plane3D.To3D(point + way * size * 0.6f, size * 0.4f), drift, bubble, color, Rand.Range(0.7f, 1.3f));
        }
    }
}
