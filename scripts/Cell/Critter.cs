// NOTE:
//   NPC cells: flee from bigger cells, hunt smaller ones (the player and other critters),
//   keep their distance to equal ones and drift otherwise. Spawned by Main.
//
//   var critter = new Critter();
//   critter.Setup(radius, CritterKind.Hunter, player.BodySize());

//
//   Chapters:
//     STATE        Kinds, eating rules and the hunting state.
//     SETUP        Size, look, parts and speed per kind.
//     EAT AND DIE  Eating rules, growing after a meal and taking damage.
//     ENGULF       Being slowly wrapped, pulled in and digested by a bigger cell.
//     STEER        Flee, hunt, separate or wander, once per physics frame.
//

using Godot;

namespace CellOut;

public enum CritterKind { Bacterium, Amoeba, Hunter }

public partial class Critter : CellBody
{
    // ===========================================================================
    // ================================   STATE   ================================
    // ===========================================================================
    //
    //   Rules   How much bigger a predator must be and how long a meal lasts.
    //   Hunt    Health, speed, senses and the hunting state.
    //

    // ---------------------------------------------------------------------------
    // Rules
    //   How much bigger a predator must be and how long a meal lasts.
    // ---------------------------------------------------------------------------

    public const float EAT_RATIO = 1.15f;
    private const float SATED_TIME = 4f;           // seconds without hunting after a meal
    private const float SENSE_RADII = 9f;          // sense range in own radii
    private const float WANDER_SPEED = 0.45f;      // share of full speed while drifting
    private const float SEPARATION_WEIGHT = 0.6f;
    private const float ENGULF_SLOWDOWN = 0.3f;    // share of the speed while swallowing, busy flowing around the prey

    [Signal] public delegate void PoppedEventHandler(Vector2 position, float radius, Color color);

    // ---------------------------------------------------------------------------
    // Hunt
    //   Health, speed, senses and the hunting state.
    // ---------------------------------------------------------------------------

    public CritterKind Kind { get; private set; } = CritterKind.Bacterium;
    public float Hp { get; private set; } = 10f;
    public Player Target { get; set; }
    public float BumpCooldown { get; set; }
    private float _speed = 80f;
    private float _sense = 300f;
    private float _sated;
    private Vector2 _wanderDirection = Vector2.Right;
    private float _wanderTimer;

    // ===========================================================================
    // ================================   SETUP   ================================
    // ===========================================================================
    //
    //   Setup   Size, look, editor parts (CellDesign.ForCritter) and speed per kind, called before AddChild.
    //

    private CellStats _stats = new();

    /// <summary>Sets size, look, parts and speed of the critter.</summary>
    /// <param name="radius">(Required) Radius on the plane.</param>
    /// <param name="kind">(Required) Bacteria only flee, amoebas and hunters also hunt.</param>
    /// <param name="playerSize">(Required) Current Player.BodySize(), the speed scales with it.</param>
    /// <param name="stage">(Optional) Stage of the player, every stage adds a part. Defaults to 0.</param>
    /// <example><code>critter.Setup(20f, CritterKind.Hunter, player.BodySize(), player.Stage);</code></example>
    public void Setup(float radius, CritterKind kind, float playerSize, int stage = 0)
    {
        Radius = radius;
        Kind   = kind;
        Design = CellDesign.ForCritter(kind, stage);
        _stats = CellStats.From(Design);
        Hp     = radius * 1.5f * _stats.Health;
        _sense = radius * SENSE_RADII * (1f + _stats.SightBonus);

        float speedFactor;
        switch (kind)
        {
            case CritterKind.Bacterium:
                BodyColor   = Color.FromHsv(Rand.Range(0.06f, 0.16f), 0.55f, 0.95f);
                speedFactor = 0.6f;
                break;
            case CritterKind.Amoeba:
                BodyColor   = Color.FromHsv(Rand.Range(0.72f, 0.88f), 0.42f, 0.9f);
                speedFactor = 0.5f;
                break;
            case CritterKind.Hunter:
                BodyColor   = Color.FromHsv(Rand.Range(0f, 0.04f), 0.6f, 0.92f);
                speedFactor = 0.7f;
                break;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(kind), $"kind must be a known CritterKind, got '{kind}'.");
        }
        _speed = SwimSpeed(playerSize) * speedFactor * _stats.Speed;
    }

    // ===========================================================================
    // =============================   EAT AND DIE   =============================
    // ===========================================================================
    //
    //   CanEat                Whether this critter can eat something of the given radius, a jaw allows bigger prey.
    //   Devour                Grows a little after a meal (capped, digestion parts help) and stays sated for a while.
    //   Hurt                  Takes damage (armor plates towards the attacker halve it), pops at zero health.
    //   ContactDamageToward   Spike and jaw damage dealt towards a world direction (CellStats).
    //   DamageFactorFrom      Share of the damage taken from a world direction, plates halve it.
    //

    private const float JAW_EAT_RATIO = 1f;        // with a jaw a critter swallows cells as big as itself

    /// <summary>Whether this critter can eat something of the given radius. Bacteria never eat.</summary>
    /// <param name="otherRadius">(Required) Radius of the prey.</param>
    public bool CanEat(float otherRadius)
    {
        float ratio = _stats.JawAngles.Count > 0 ? JAW_EAT_RATIO : EAT_RATIO;
        return CritterKind.Bacterium != Kind && Radius > otherRadius * ratio;
    }

    /// <summary>Spike and jaw damage this critter deals towards a world direction, toxin included.</summary>
    /// <param name="worldAngle">(Required) Direction from the critter to the other cell.</param>
    public float ContactDamageToward(float worldAngle)
    {
        return _stats.ContactDamageToward(worldAngle - Facing);
    }

    /// <summary>Share of the damage taken from a world direction, armor plates halve it.</summary>
    /// <param name="worldAngle">(Required) Direction from the critter to the attacker.</param>
    public float DamageFactorFrom(float worldAngle)
    {
        return _stats.DamageFactorFrom(worldAngle - Facing);
    }

    /// <summary>Grows after a meal and pauses hunting for SATED_TIME seconds.</summary>
    /// <param name="eatenRadius">(Required) Radius of the eaten cell.</param>
    /// <param name="maxRadius">(Required) Upper limit, keeps critters relative to the player.</param>
    public void Devour(float eatenRadius, float maxRadius)
    {
        float digested = eatenRadius * eatenRadius * 0.35f * _stats.DigestAll * _stats.DigestMeat;
        Radius  = Mathf.Min(Mathf.Sqrt(Radius * Radius + digested), maxRadius);
        Hp     += eatenRadius * 0.75f;
        _sated  = SATED_TIME;
    }

    /// <summary>Takes damage and pops at zero health, Main turns the pop into food.</summary>
    /// <param name="amount">(Required) Damage in hit points before armor.</param>
    /// <param name="from">(Optional) Plane point of the attacker, armor plates towards it halve the damage. Defaults to no armor.</param>
    public void Hurt(float amount, Vector2? from = null)
    {
        float factor = from is Vector2 attacker ? DamageFactorFrom((attacker - GlobalPosition).Angle()) : 1f;
        Hp        -= amount * factor;
        HurtFlash  = 1f;
        if (Hp > 0f || IsQueuedForDeletion())
        {
            return;
        }
        EmitSignal(SignalName.Popped, GlobalPosition, Radius, BodyColor);
        QueueFree();
    }

    // ===========================================================================
    // ===============================   ENGULF   ================================
    // ===========================================================================
    //
    //   IsEngulfed      Whether a bigger cell is swallowing this one right now.
    //   StartEngulf     Caught: remembers the eater, the spot and the size at the catch.
    //   AdvanceEngulf   Pulls the cell into the eater and shrinks it, true once digested.
    //   ReleaseEngulf   The eater is gone: the cell swims on with its size at the catch.
    //

    /*
      Phagocytosis instead of a snap: the eater's membrane flows around the prey
      (CellVisual3D, pseudopods), the prey is pulled to the eater's center and shrinks
      while it is digested. Its own steering stops meanwhile.

        progress  0 ........ 0.5 ........ 1
        prey      at contact   half inside  at the center, ENGULF_SHRINK of its size
        eater     arms reach   arms close   membrane smooth again
    */
    private const float ENGULF_SHRINK = 0.3f;       // share of the radius left when digested
    private const float ENGULF_WIGGLE = 0.08f;      // the prey struggles a little, share of its radius

    /// <summary>The cell swallowing this one, null when free.</summary>
    public CellBody Engulfer { get; private set; }

    /// <summary>0 at the catch .. 1 digested.</summary>
    public float EngulfProgress { get; private set; }

    /// <summary>Radius at the catch, the reward and the eater's growth use it.</summary>
    public float EngulfRadius { get; private set; }

    public bool IsEngulfed => null != Engulfer;

    private Vector2 _engulfOffset;

    /// <summary>Starts being swallowed by a bigger cell.</summary>
    /// <param name="eater">(Required) The player or a critter.</param>
    public void StartEngulf(CellBody eater)
    {
        Engulfer       = eater ?? throw new System.ArgumentNullException(nameof(eater), "eater must be the cell that swallows this one.");
        EngulfProgress = 0f;
        EngulfRadius   = Radius;
        _engulfOffset  = GlobalPosition - eater.GlobalPosition;
        Velocity       = Vector2.Zero;
    }

    /// <summary>Pulls the cell towards the center of its eater and shrinks it.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    /// <param name="seconds">(Required) Duration of the whole swallow.</param>
    /// <returns>True once digested, Main then frees the cell and hands out the reward.</returns>
    /// <example><code>if (prey.AdvanceEngulf(dt, 1.4f)) prey.QueueFree();</code></example>
    public bool AdvanceEngulf(float dt, float seconds)
    {
        EngulfProgress = Mathf.Min(1f, EngulfProgress + dt / Mathf.Max(seconds, 0.01f));
        float pull     = Mathf.SmoothStep(0f, 1f, EngulfProgress);
        float wiggle   = Mathf.Sin(EngulfProgress * 40f) * ENGULF_WIGGLE * EngulfRadius * (1f - pull);
        Vector2 side   = _engulfOffset.Orthogonal().Normalized();
        GlobalPosition = Engulfer.GlobalPosition + _engulfOffset * (1f - pull) + side * wiggle;
        Radius         = EngulfRadius * Mathf.Lerp(1f, ENGULF_SHRINK, pull);
        return EngulfProgress >= 1f;
    }

    /// <summary>The eater vanished (eaten itself, freed): the cell is free again with its size at the catch.</summary>
    public void ReleaseEngulf()
    {
        Radius         = EngulfRadius;
        Engulfer       = null;
        EngulfProgress = 0f;
    }

    // ===========================================================================
    // ================================   STEER   ================================
    // ===========================================================================
    //
    //   1. Senses   Collects threats, the closest prey and too close neighbors.
    //   2. Decide   Fleeing beats hunting, hunting beats wandering, separation always adds.
    //   3. Move     Eases the velocity towards the desired direction.
    //

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        BumpCooldown = Mathf.Max(0f, BumpCooldown - dt);
        _sated       = Mathf.Max(0f, _sated - dt);

        // Being swallowed: Main moves the cell (ENGULF), it does not steer any more
        if (IsEngulfed)
        {
            return;
        }

        // ---------------------------------------------------------------------------
        // Senses
        //   Collects threats, the closest prey and too close neighbors.
        // ---------------------------------------------------------------------------

        Vector2 flee      = Vector2.Zero;
        Vector2 separate  = Vector2.Zero;
        Vector2 preyDir   = Vector2.Zero;
        float   preyDist  = float.PositiveInfinity;

        // The player
        if (IsInstanceValid(Target))
        {
            Vector2 toPlayer   = Target.GlobalPosition - GlobalPosition;
            float   playerSize = Target.BodySize();
            float   distance   = toPlayer.Length();
            bool    isInSense  = distance < _sense + playerSize;
            if (isInSense && Target.Stage >= 1 && playerSize > Radius * EAT_RATIO)
            {
                flee -= toPlayer / Mathf.Max(distance, 1f);
            }
            else if (isInSense && CanEat(playerSize) && Target.Invulnerable <= 0f)
            {
                preyDir  = toPlayer;
                preyDist = distance;
            }
        }

        // Other critters, the siblings in the same layer
        foreach (Node node in GetParent().GetChildren())
        {
            if (node is not Critter other || other == this || other.IsQueuedForDeletion() || other.IsEngulfed)
            {
                continue;
            }
            Vector2 toOther  = other.Position - Position;
            float   distance = toOther.Length();
            if (distance > _sense + other.Radius)
            {
                continue;
            }

            if (other.CanEat(Radius))
            {
                flee -= toOther / Mathf.Max(distance, 1f);
            }
            else if (CanEat(other.Radius) && distance < preyDist)
            {
                preyDir  = toOther;
                preyDist = distance;
            }
            else if (!CanEat(other.Radius) && distance < (Radius + other.Radius) * 1.5f)
            {
                separate -= toOther / Mathf.Max(distance, 1f);
            }
        }

        // ---------------------------------------------------------------------------
        // Decide
        //   Fleeing beats hunting, hunting beats wandering, separation always adds.
        // ---------------------------------------------------------------------------

        Vector2 desired;
        if (Vector2.Zero != flee)
        {
            desired = flee.Normalized();
        }
        else if (!float.IsInfinity(preyDist) && _sated <= 0f)
        {
            desired = preyDir.Normalized();
        }
        else
        {
            _wanderTimer -= dt;
            if (_wanderTimer <= 0f)
            {
                _wanderDirection = Vector2.FromAngle(Rand.Angle());
                _wanderTimer     = Rand.Range(1.5f, 4f);
            }
            desired = _wanderDirection * WANDER_SPEED;
        }
        desired = (desired + separate * SEPARATION_WEIGHT).LimitLength(1f);

        // ---------------------------------------------------------------------------
        // Move
        //   Eases the velocity towards the desired direction.
        // ---------------------------------------------------------------------------

        // While it swallows something (EngulfReach set by Main) it nearly stops, prey can still wriggle free
        float speed = EngulfReach > 0f ? _speed * ENGULF_SLOWDOWN : _speed;
        Velocity  = Velocity.Lerp(desired * speed, Mathf.Clamp(dt * 2f * _stats.Acceleration, 0f, 1f));
        Position += Velocity * dt;
    }
}
