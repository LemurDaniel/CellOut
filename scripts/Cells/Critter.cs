// NOTE:
//   NPC cells: flee from bigger cells, hunt smaller ones (the player and other critters),
//   keep their distance to equal ones and drift otherwise. Spawned by Main.
//
//   var critter = new Critter();
//   critter.Setup(radius, CritterKind.Hunter, player.BodySize());

//
//   Chapters:
//     STATE        Kinds, eating rules and the hunting state.
//     SETUP        Size, look and speed per kind.
//     EAT AND DIE  Eating rules, growing after a meal and taking damage.
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
    //   Setup   Size, look and speed per kind, called before AddChild.
    //

    /// <summary>Sets size, look and speed of the critter.</summary>
    /// <param name="radius">(Required) Radius on the plane.</param>
    /// <param name="kind">(Required) Bacteria only flee, amoebas and hunters also hunt.</param>
    /// <param name="playerSize">(Required) Current Player.BodySize(), the speed scales with it.</param>
    public void Setup(float radius, CritterKind kind, float playerSize)
    {
        Radius = radius;
        Kind   = kind;
        Hp     = radius * 1.5f;
        _sense = radius * SENSE_RADII;

        float speedFactor;
        switch (kind)
        {
            case CritterKind.Bacterium:
                BodyColor   = Color.FromHsv(Rand.Range(0.06f, 0.16f), 0.55f, 0.95f);
                Flagella    = 1;
                speedFactor = 0.7f;
                break;
            case CritterKind.Amoeba:
                BodyColor   = Color.FromHsv(Rand.Range(0.72f, 0.88f), 0.42f, 0.9f);
                HasCilia    = true;
                EyeCount    = 2;
                speedFactor = 0.5f;
                break;
            case CritterKind.Hunter:
                BodyColor   = Color.FromHsv(Rand.Range(0f, 0.04f), 0.6f, 0.92f);
                HasSpikes   = true;
                Flagella    = 2;
                EyeCount    = 2;
                speedFactor = 0.8f;
                break;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(kind), $"kind must be a known CritterKind, got '{kind}'.");
        }
        _speed = SwimSpeed(playerSize) * speedFactor;
    }

    // ===========================================================================
    // =============================   EAT AND DIE   =============================
    // ===========================================================================
    //
    //   CanEat   Whether this critter can eat something of the given radius.
    //   Devour   Grows a little after a meal (capped) and stays sated for a while.
    //   Hurt     Takes damage, pops into food at zero health.
    //

    /// <summary>Whether this critter can eat something of the given radius. Bacteria never eat.</summary>
    /// <param name="otherRadius">(Required) Radius of the prey.</param>
    public bool CanEat(float otherRadius)
    {
        return CritterKind.Bacterium != Kind && Radius > otherRadius * EAT_RATIO;
    }

    /// <summary>Grows after a meal and pauses hunting for SATED_TIME seconds.</summary>
    /// <param name="eatenRadius">(Required) Radius of the eaten cell.</param>
    /// <param name="maxRadius">(Required) Upper limit, keeps critters relative to the player.</param>
    public void Devour(float eatenRadius, float maxRadius)
    {
        Radius  = Mathf.Min(Mathf.Sqrt(Radius * Radius + eatenRadius * eatenRadius * 0.35f), maxRadius);
        Hp     += eatenRadius * 0.75f;
        _sated  = SATED_TIME;
    }

    /// <summary>Takes damage and pops at zero health, Main turns the pop into food.</summary>
    /// <param name="amount">(Required) Damage in hit points.</param>
    public void Hurt(float amount)
    {
        Hp        -= amount;
        HurtFlash  = 1f;
        if (Hp > 0f || IsQueuedForDeletion())
        {
            return;
        }
        EmitSignal(SignalName.Popped, GlobalPosition, Radius, BodyColor);
        QueueFree();
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
            if (node is not Critter other || other == this || other.IsQueuedForDeletion())
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

        Velocity  = Velocity.Lerp(desired * _speed, Mathf.Clamp(dt * 2f, 0f, 1f));
        Position += Velocity * dt;
    }
}
