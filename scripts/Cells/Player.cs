// NOTE:
//   The player cell: grows by eating, earns evolution points (EP) per level and
//   rebuilds itself in the CellEditor. Every trait comes from the placed parts of its
//   Design, where they sit matters (spikes, jaws and plates only work towards their side,
//   a flagellum pushes best from the back). Tracks the diet between herbivore and carnivore.
//   From the single cell on it can divide once it is big enough: every cell splits, 1, 2, 4
//   up to 64 cells stick together, the cluster keeps its size and the cells get smaller.
//   The first division makes it multicellular. Multicellular perks: a deadly hit only
//   bursts a quarter of the cells, and every division lets it swallow bigger prey.
//   At 64 cells and enough biomass it can evolve into an organism (Space or the HUD
//   button): Main plays the evolve cutscene, the CreatureEditor shapes the clay, then
//   BecomeOrganism takes over the body plan and every trait comes from the creature.
//   On land (Main sets IsOnLand at the coast) it needs air and legs: without a lung the
//   air runs out, without legs it only crawls. The first step onto land with both makes
//   it a land animal.
//   Created by Main, which listens to its signals. Steered with WASD, arrows or the held
//   mouse, divides or evolves with Space or the HUD buttons. In the third person view of
//   the land animal Main turns the keys with the camera (InputTurn): W walks away from it.

//
//   Chapters:
//     STATE              Stages, evolution points, signals and the traits from the design.
//     MOVE               Input, movement, regeneration, photosynthesis and diet fading.
//     DIVISION           When the cells may divide and the division itself.
//     ORGANISM           Evolving the full cluster into a creature and committing its body plan.
//     LAND               Air, crawling and walking on land, the step to the land animal.
//     STATS              Speed, goal progress and the directional combat values.
//     GAMEPLAY           Biomass, damage and committing a new design.
//     GROWTH             Stage advance, traits from the design and the cell radius.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class Player : CellBody
{
    // ===========================================================================
    // ================================   STATE   ================================
    // ===========================================================================
    //
    //   Stages      Translation keys and biomass thresholds of the stages.
    //   Evolution   Evolution points and the diet.
    //   Signals     Stats, evolution, stage changes and deaths for Main and the HUD.
    //   Traits      Health, speed and the values the design sets.
    //

    // ---------------------------------------------------------------------------
    // Stages
    //   Translation keys and biomass thresholds of the stages.
    // ---------------------------------------------------------------------------

    // The multicellular stage has no threshold, the first division reaches it (DIVISION),
    // the organism comes from evolving the full cluster (ORGANISM), the land animal from
    // the first step onto land (LAND)
    public static readonly string[] STAGE_KEYS = { "STAGE_PROTOCELL", "STAGE_SINGLE", "STAGE_MULTI", "STAGE_ORGANISM", "STAGE_LAND" };
    public static readonly float[] STAGE_THRESHOLDS = { 0f, 40f };
    public const int SINGLE_STAGE = 1;
    public const int MULTICELLULAR_STAGE = 2;
    public const int ORGANISM_STAGE = 3;
    public const int LAND_STAGE = 4;

    public int Stage { get; private set; }
    public int Level { get; private set; }
    public float Biomass { get; private set; }
    public float NextLevelAt { get; private set; } = float.PositiveInfinity;
    private float _levelStart;

    // ---------------------------------------------------------------------------
    // Evolution
    //   Evolution points and the diet.
    // ---------------------------------------------------------------------------

    public const int EP_PER_LEVEL = 2;               // parts cost 2-6 EP, a level buys about half a part
    private const int EP_PER_STAGE = 2;

    public int EvolutionPoints { get; private set; }
    private int _epPerLevel = EP_PER_LEVEL;          // a brain adds to it (CreatureStats)
    public float Diet { get; private set; }          // -1 herbivore .. 1 carnivore
    private readonly DietTracker _diet = new();

    // ---------------------------------------------------------------------------
    // Signals
    //   Stats, evolution, stage changes and deaths for Main and the HUD.
    // ---------------------------------------------------------------------------

    [Signal] public delegate void StatsChangedEventHandler();
    [Signal] public delegate void EvolutionReadyEventHandler();      // stage advance: the editor opens by itself
    [Signal] public delegate void StageChangedEventHandler(int stage);
    [Signal] public delegate void DividedEventHandler(int cellCount);
    [Signal] public delegate void EvolveRequestedEventHandler();     // Main plays the cutscene and opens the creature editor
    [Signal] public delegate void DiedEventHandler();
    [Signal] public delegate void CellsLostEventHandler(int lost);

    // ---------------------------------------------------------------------------
    // Traits
    //   Health, speed and the values the design sets.
    // ---------------------------------------------------------------------------

    private const float START_INVULNERABILITY = 3f;
    private const float HIT_INVULNERABILITY = 0.5f;
    private const float RESPAWN_INVULNERABILITY = 2.5f;
    private const float CELLS_LOST_ON_DEATH = 0.25f;  // a cluster loses a quarter of its cells instead
    private const float REGEN_PER_SECOND = 0.02f;     // share of MaxHp
    private const float BASE_ACCELERATION = 2.5f;
    public const float BASE_MAGNET_RANGE = 1.6f;

    public float MaxHp { get; private set; } = 50f;
    public float Hp { get; private set; } = 50f;
    public float Invulnerable { get; set; }
    public float MagnetRange { get; private set; } = BASE_MAGNET_RANGE;
    private float _speedMultiplier = 1f;
    private float _acceleration = BASE_ACCELERATION;
    private float _digestAll = 1f;
    private float _digestPlant = 1f;
    private float _digestMeat = 1f;
    private float _photosynthesis;
    private float _toxin = 1f;
    private readonly List<float> _spikeAngles = new();
    private readonly List<float> _jawAngles = new();
    private readonly List<float> _plateAngles = new();

    // ===========================================================================
    // ================================   MOVE   =================================
    // ===========================================================================
    //
    //   InputTurn         Turn of the key directions, set by Main from the camera yaw.
    //   _Ready            Protocell look and a short grace period.
    //   _PhysicsProcess   Steers by keys or mouse, regenerates, photosynthesizes, fades the diet.
    //   _Process          Blinks while invulnerable.
    //

    /// <summary>Turn of the key directions in radians, 0 in the top view (W = screen up).</summary>
    public float InputTurn { get; set; }

    public override void _Ready()
    {
        base._Ready();
        IsProtocell  = true;
        _stageColor  = PROTOCELL_COLOR;
        BodyColor    = _stageColor;
        Invulnerable = START_INVULNERABILITY;
        UpdateRadius();
        ApplyDesign();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (IsDead)
        {
            return;
        }

        // Keys, the held mouse button overrides them
        var input = Vector2.Zero;
        if (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up))    input.Y -= 1f;
        if (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down))  input.Y += 1f;
        if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left))  input.X -= 1f;
        if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right)) input.X += 1f;
        input = input.Rotated(InputTurn);
        if (Input.IsMouseButtonPressed(MouseButton.Left))
        {
            Vector2 toMouse = Plane3D.MouseOnPlane(GetViewport(), GlobalPosition) - GlobalPosition;
            if (toMouse.Length() > BodySize() * 0.3f)
            {
                input = toMouse.Normalized();
            }
        }
        input = input.LimitLength(1f);

        Velocity  = Velocity.Lerp(input * CurrentSpeed(), Mathf.Clamp(_acceleration * dt, 0f, 1f));
        Position += Velocity * dt;

        // Regeneration, photosynthesis and fading meals
        Invulnerable = Mathf.Max(0f, Invulnerable - dt);
        Hp           = Mathf.Min(MaxHp, Hp + MaxHp * REGEN_PER_SECOND * dt);
        if (_photosynthesis > 0f)
        {
            AddBiomass(_photosynthesis * Radius / REFERENCE_SIZE * dt, BiomassSource.Light);
        }
        _diet.Decay(dt);
        Diet = _diet.Value(Design.Lean(), Design.Parts.Count > 0);
        UpdateLand(dt);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        IsGhost = Invulnerable > 0f;
    }

    // ===========================================================================
    // ==============================   DIVISION   ===============================
    // ===========================================================================
    //
    //   MAX_CELLS          The cluster stops dividing at 64 cells.
    //   Divisions          How often the cells divided so far.
    //   NextDivisionAt     Biomass from which the next division is allowed.
    //   CanDivide          Single cell or multicellular, below MAX_CELLS and big enough.
    //   Divide             Every cell splits in two (capped at MAX_CELLS), the first division makes it multicellular.
    //   _UnhandledInput    Space divides, at 64 cells it evolves (ORGANISM).
    //

    public const int MAX_CELLS = 64;
    public const int ORGANISM_CELLS = 512;           // the evolve cutscene divides on up to here
    private const float DIVISION_BIOMASS = 100f;     // first division, every next one needs DIVISION_GROWTH times more
    private const float DIVISION_GROWTH = 2.5f;
    private const float HP_PER_DIVISION = 0.15f;     // share of the base health each division adds

    /// <summary>Divisions so far, an organism counts the divisions of the cutscene up to ORGANISM_CELLS.</summary>
    public int Divisions => System.Numerics.BitOperations.Log2((uint)(IsOrganism ? ORGANISM_CELLS : CellCount));

    public float NextDivisionAt => DIVISION_BIOMASS * Mathf.Pow(DIVISION_GROWTH, Divisions);

    public bool CanDivide => Stage >= SINGLE_STAGE && !IsOrganism && CellCount < MAX_CELLS && Biomass >= NextDivisionAt;

    /// <summary>
    /// Splits every cell in two when CanDivide allows it. The cluster keeps its overall size
    /// (UpdateRadius), so each cell gets smaller. The first division advances to the
    /// multicellular stage.
    /// </summary>
    /// <returns>True when the cells divided.</returns>
    /// <example><code>if (player.CanDivide) player.Divide();</code></example>
    public bool Divide()
    {
        if (!CanDivide)
        {
            return false;
        }
        CellCount = Mathf.Min(CellCount * 2, MAX_CELLS);
        UpdateRadius();
        Log.Verbose("Player", $"Divided into {CellCount} cells at biomass {Biomass:F0}, next division at {NextDivisionAt:F0}.");
        EmitSignal(SignalName.Divided, CellCount);
        if (SINGLE_STAGE == Stage)
        {
            AdvanceStage();
        }
        else
        {
            ApplyDesign();
        }
        EmitSignal(SignalName.StatsChanged);
        return true;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space } && (Divide() || RequestEvolve()))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    // ===========================================================================
    // ==============================   ORGANISM   ===============================
    // ===========================================================================
    //
    //   IsOrganism       From stage 4 on: the body is a creature (also the land animal).
    //   EvolveAt         Biomass from which the full cluster may evolve, the next division step.
    //   CanEvolve        Full cluster (MAX_CELLS) and EvolveAt reached.
    //   RequestEvolve    Asks Main to play the cutscene and open the creature editor.
    //   BecomeOrganism   Takes over the shaped body plan: one body, stage 4, every trait from the creature.
    //   CommitCreature   A later creature editor session: new body plan, points left.
    //
    //   ORGANISM_BONUS_EP points come on top of the saved ones for the first creature editor.
    //

    public const int ORGANISM_BONUS_EP = 6;

    public bool IsOrganism => Stage >= ORGANISM_STAGE;

    public float EvolveAt => DIVISION_BIOMASS * Mathf.Pow(DIVISION_GROWTH, System.Numerics.BitOperations.Log2(MAX_CELLS));

    public bool CanEvolve => MULTICELLULAR_STAGE == Stage && MAX_CELLS == CellCount && Biomass >= EvolveAt;

    /// <summary>Asks Main to start the evolution into an organism, when CanEvolve allows it.</summary>
    /// <returns>True when the request went out.</returns>
    /// <example><code>if (player.CanEvolve) player.RequestEvolve();</code></example>
    public bool RequestEvolve()
    {
        if (!CanEvolve)
        {
            return false;
        }
        Log.Verbose("Player", $"Evolve requested at biomass {Biomass:F0} with {CellCount} cells.");
        EmitSignal(SignalName.EvolveRequested);
        return true;
    }

    /// <summary>
    /// The cluster becomes one organism: the body keeps its size (one body instead of
    /// CellCount cells), the creature visual replaces the cells, health refills.
    /// </summary>
    /// <param name="design">(Required) The body plan shaped in the creature editor, copied.</param>
    /// <param name="pointsLeft">(Required) Unspent evolution points, kept for the next editor.</param>
    public void BecomeOrganism(CreatureDesign design, int pointsLeft)
    {
        if (MULTICELLULAR_STAGE != Stage)
        {
            throw new System.InvalidOperationException($"Only a multicellular player can become an organism, stage is '{Stage}'.");
        }
        Creature = new CreatureDesign();
        Creature.CopyFrom(design);
        EvolutionPoints = pointsLeft;
        CellCount       = 1;
        Stage           = ORGANISM_STAGE;
        UpdateRadius();
        ApplyDesign();
        Hp = MaxHp;
        ShowCreature();
        Log.Verbose("Player", $"Became a {design.Type} with {design.Girths.Count} vertebrae, {design.Parts.Count} part(s), {pointsLeft} EP left.");
        EmitSignal(SignalName.StageChanged, Stage);
        EmitSignal(SignalName.StatsChanged);
    }

    /// <summary>Takes over a reshaped body plan from a later creature editor session.</summary>
    /// <param name="design">(Required) The finished draft, copied.</param>
    /// <param name="pointsLeft">(Required) Unspent evolution points.</param>
    public void CommitCreature(CreatureDesign design, int pointsLeft)
    {
        Creature.CopyFrom(design);
        EvolutionPoints = pointsLeft;
        ApplyDesign();
        Log.Verbose("Player", $"Creature committed with {design.Parts.Count} part(s), {pointsLeft} EP left.");
        EmitSignal(SignalName.StatsChanged);
    }

    /// <summary>Cheat (F10): free evolution points.</summary>
    /// <param name="points">(Required) Points added.</param>
    public void GrantPoints(int points)
    {
        EvolutionPoints += points;
        EmitSignal(SignalName.StatsChanged);
    }

    /// <summary>Cheat (F10): a pair of legs and (for a fish) a lung for free, wherever they still fit.</summary>
    public void GiveLegsAndLung()
    {
        var design = new CreatureDesign();
        design.CopyFrom(Creature);
        float[] spots = { 0.3f, 0.45f, 0.75f, 0.15f, 0.6f, 0.9f };
        foreach (CreaturePartKind kind in new[] { CreaturePartKind.Legs, CreaturePartKind.Lung })
        {
            bool isNeeded = CreaturePartKind.Legs == kind ? 0 == design.LegPairs : !design.BreathesAir;
            for (int i = 0; isNeeded && i < spots.Length && !design.TryPlace(kind, spots[i], 0); i++) { }
        }
        CommitCreature(design, EvolutionPoints);
    }

    // ===========================================================================
    // ================================   LAND   =================================
    // ===========================================================================
    //
    //   IsLandAnimal   Stage 5: the creature stepped onto land with legs and lung.
    //   CanWalk        Legs placed and air breathed, the conditions of the step onto land.
    //   Air            Breath left, 0..1, drains on land without a lung.
    //   UpdateLand     Air drains or refills, suffocation hurts, the first walk on land advances.
    //
    //   Main sets CellBody.IsOnLand every physics frame from the Coast.
    //

    private const float AIR_SECONDS = 8f;            // a full breath lasts this long on land
    private const float AIR_REFILL = 0.5f;           // per second in the water
    private const float SUFFOCATE_DAMAGE = 0.1f;     // share of MaxHp per second without air

    private float _landSpeed = CreatureStats.CRAWL_SPEED;    // from CreatureStats, see ApplyCreature

    public bool IsLandAnimal => LAND_STAGE == Stage;

    public bool CanWalk => IsOrganism && Creature.LegPairs > 0 && Creature.BreathesAir;

    public float Air { get; private set; } = 1f;

    /*
      > Not an organism yet: nothing to do
      > On land without breathing air: the air drains, at 0 the body suffocates
      > Otherwise: the air refills
      > First time on land with legs and lung: the creature becomes a land animal
    */
    /// <summary>Air, suffocation and the step onto land, once per physics frame.</summary>
    private void UpdateLand(float dt)
    {
        if (!IsOrganism)
        {
            return;
        }

        if (IsOnLand && !Creature.BreathesAir)
        {
            Air = Mathf.Max(0f, Air - dt / AIR_SECONDS);
            if (Air <= 0f && Invulnerable <= 0f)
            {
                Hp       -= MaxHp * SUFFOCATE_DAMAGE * dt;
                HurtFlash = Mathf.Max(HurtFlash, 0.4f);
                if (Hp <= 0f)
                {
                    Log.Verbose("Player", "Suffocated on land.");
                    Die();
                }
                EmitSignal(SignalName.StatsChanged);
            }
        }
        else
        {
            Air = Mathf.Min(1f, Air + dt * AIR_REFILL);
        }

        if (ORGANISM_STAGE == Stage && IsOnLand && CanWalk)
        {
            Stage            = LAND_STAGE;
            EvolutionPoints += EP_PER_STAGE;
            Log.Verbose("Player", $"First steps on land -> stage {Stage} ({STAGE_KEYS[Stage]}).");
            EmitSignal(SignalName.StageChanged, Stage);
            EmitSignal(SignalName.StatsChanged);
        }
    }

    // ===========================================================================
    // ================================   STATS   ================================
    // ===========================================================================
    //
    //   CurrentSpeed          Swim speed of the body size times the design bonus.
    //   GoalProgress          Progress towards the next stage or level, 0..1.
    //   PreviewDiet           Diet value a design would give, for the editor.
    //   ContactDamageToward   Spike and jaw damage dealt to a cell touching from a direction.
    //   CanBiteToward         Whether a jaw points towards a direction.
    //   EatRatioToward        How much bigger than prey of a direction the player must be, divisions lower it.
    //   DamageFactorFrom      Damage taken from a direction, armor plates halve it.
    //   HasThorns             Whether any spike or jaw exists.
    //

    private const float JAW_EAT_RATIO = 0.95f;           // with a jaw towards it, cells up to 95% of the own size
    private const float EAT_RATIO_PER_DIVISION = 0.05f;  // every division swallows 5% bigger prey
    private const float MIN_EAT_RATIO = 0.75f;           // at most prey a third bigger than the own body

    /// <summary>Swim speed in the water, on land the walking (or crawling) speed of LAND.</summary>
    public float CurrentSpeed()
    {
        return SwimSpeed(BodySize()) * (IsOnLand ? _landSpeed : _speedMultiplier);
    }

    public float GoalProgress()
    {
        if (0 == Stage)
        {
            return Biomass / STAGE_THRESHOLDS[SINGLE_STAGE];
        }
        return Mathf.Clamp((Biomass - _levelStart) / Mathf.Max(NextLevelAt - _levelStart, 1f), 0f, 1f);
    }

    /// <summary>Diet value the given design would give with the current meals.</summary>
    /// <param name="design">(Required) The draft design of the editor.</param>
    public float PreviewDiet(CellDesign design)
    {
        return _diet.Value(design.Lean(), design.Parts.Count > 0);
    }

    /// <summary>Damage dealt to a cell that touches from the given world direction, scaled to REFERENCE_SIZE.</summary>
    /// <param name="worldAngle">(Required) Direction from the player to the other cell.</param>
    public float ContactDamageToward(float worldAngle)
    {
        return CellStats.ContactDamage(_spikeAngles, _jawAngles, _toxin, worldAngle - Facing);
    }

    /// <summary>Whether a jaw points towards the given world direction.</summary>
    public bool CanBiteToward(float worldAngle)
    {
        return _jawAngles.Exists(angle => IsWithin(worldAngle, angle, CellStats.JAW_ARC));
    }

    /// <summary>
    /// The player swallows a cell when its body size is larger than the cell radius times this
    /// ratio: 1.15 normally, 0.95 with a jaw towards it, each division 0.05 less (bigger prey),
    /// never below MIN_EAT_RATIO.
    /// </summary>
    /// <param name="worldAngle">(Required) Direction from the player to the prey.</param>
    /// <example><code>player.EatRatioToward(0f)  // 1.15 as single cell, 0.9 with 32 cells</code></example>
    public float EatRatioToward(float worldAngle)
    {
        float baseRatio = CanBiteToward(worldAngle) ? JAW_EAT_RATIO : Critter.EAT_RATIO;
        return Mathf.Max(MIN_EAT_RATIO, baseRatio - EAT_RATIO_PER_DIVISION * Divisions);
    }

    /// <summary>Share of the damage taken from the given world direction, plates halve it.</summary>
    public float DamageFactorFrom(float worldAngle)
    {
        bool isShielded = _plateAngles.Exists(angle => IsWithin(worldAngle, angle, CellStats.PLATE_ARC));
        return isShielded ? CellStats.PLATE_DAMAGE_FACTOR : 1f;
    }

    public bool HasThorns()
    {
        return _spikeAngles.Count > 0 || _jawAngles.Count > 0;
    }

    /// <summary>Whether a world direction lies within arc of a part angle given relative to the facing.</summary>
    private bool IsWithin(float worldAngle, float partAngle, float arc)
    {
        return CellStats.IsWithin(worldAngle - Facing, partAngle, arc);
    }

    // ===========================================================================
    // ==============================   GAMEPLAY   ===============================
    // ===========================================================================
    //
    //   AddBiomass     Grows, counts the diet, advances stages and pays EP per level (the
    //                  player opens the editor when it wants, only a stage opens it).
    //   TakeDamage     Hurts and pushes back, zero health is game over (Die),
    //                  a cluster loses a quarter of its cells instead (LoseCells).
    //   Digest         Steady damage inside a bigger cell: no push back, no grace period.
    //   CheckDeath     Dies or loses cells at zero health, shared by both.
    //   Die            Game over: the body stops, Main shows the game over screen.
    //   LoseCells      The cluster survives a deadly hit by bursting a quarter of its cells.
    //   CommitDesign   Takes over the design and points left from the editor.
    //

    // Levels get rarer the bigger you are: every next one needs LEVEL_GROWTH times the biomass
    private const float LEVEL_GROWTH = 2.1f;
    private const float LEVEL_STEP = 30f;
    private const float FIRST_LEVEL_STEP = 45f;

    /// <summary>Adds biomass and handles diet, stage advances and level ups.</summary>
    /// <param name="amount">(Required) Biomass before digestion.</param>
    /// <param name="source">(Optional) Plant and meat are digested and counted for the diet. Defaults to Plant.</param>
    public void AddBiomass(float amount, BiomassSource source = BiomassSource.Plant)
    {
        float gained = source switch
        {
            BiomassSource.Plant => amount * _digestAll * _digestPlant,
            BiomassSource.Meat  => amount * _digestAll * _digestMeat,
            _                   => amount,
        };
        Biomass += gained;
        if (Stage >= SINGLE_STAGE)
        {
            _diet.Add(source, gained);
        }
        UpdateRadius();

        if (0 == Stage && Biomass >= STAGE_THRESHOLDS[SINGLE_STAGE])
        {
            AdvanceStage();
        }
        while (Biomass >= NextLevelAt)
        {
            Level++;
            EvolutionPoints += _epPerLevel;
            _levelStart      = NextLevelAt;
            NextLevelAt      = NextLevelAt * LEVEL_GROWTH + LEVEL_STEP;
            Log.Verbose("Player", $"Biomass {Biomass:F0} reached level {Level} -> {EvolutionPoints} EP, next at {NextLevelAt:F0}.");
        }
        EmitSignal(SignalName.StatsChanged);
    }

    /// <summary>Hurts the player unless invulnerable, armor plates towards the attacker halve the damage.</summary>
    /// <param name="amount">(Required) Damage in hit points before armor.</param>
    /// <param name="from">(Required) Plane point of the attacker, the player is pushed away from it.</param>
    /// <returns>True when the hit landed, false while invulnerable.</returns>
    public bool TakeDamage(float amount, Vector2 from)
    {
        if (Invulnerable > 0f || IsDead)
        {
            return false;
        }
        Vector2 toAttacker = from - GlobalPosition;
        Hp           -= amount * DamageFactorFrom(toAttacker.Angle());
        HurtFlash     = 1f;
        Invulnerable  = HIT_INVULNERABILITY;
        Velocity     -= toAttacker.Normalized() * CurrentSpeed() * 2.5f;
        CheckDeath();
        return true;
    }

    /// <summary>Steady damage while a bigger cell digests the player: armor towards it halves it, no push back.</summary>
    /// <param name="amount">(Required) Damage of this frame before armor.</param>
    /// <param name="from">(Required) Plane point of the eater.</param>
    /// <example><code>player.Digest(12f * dt, critter.Position);</code></example>
    public void Digest(float amount, Vector2 from)
    {
        if (Invulnerable > 0f || IsDead)
        {
            return;
        }
        Hp        -= amount * DamageFactorFrom((from - GlobalPosition).Angle());
        HurtFlash  = Mathf.Max(HurtFlash, 0.6f);
        CheckDeath();
    }

    private void CheckDeath()
    {
        if (Hp <= 0f && CellCount > 1)
        {
            LoseCells();
        }
        else if (Hp <= 0f)
        {
            Die();
        }
        EmitSignal(SignalName.StatsChanged);
    }

    /// <summary>Game over: true once health hit zero as a single cell or organism.</summary>
    public bool IsDead { get; private set; }

    /// <summary>Game over: the body stops for good, Main pauses and shows the game over screen.</summary>
    private void Die()
    {
        if (IsDead)
        {
            return;
        }
        IsDead   = true;
        Hp       = 0f;
        Velocity = Vector2.Zero;
        Log.Verbose("Player", $"Died at stage {Stage} with biomass {Biomass:F0} -> game over.");
        EmitSignal(SignalName.Died);
    }

    /// <summary>
    /// Bursts a quarter of the cells (at least one) instead of dying. The biomass shrinks by
    /// the share of the lost cells, health refills and a short grace period starts.
    /// </summary>
    private void LoseCells()
    {
        int lost = Mathf.Max(1, Mathf.FloorToInt(CellCount * CELLS_LOST_ON_DEATH));
        Biomass  *= 1f - (float)lost / CellCount;
        CellCount -= lost;
        UpdateRadius();
        ApplyDesign();
        Hp           = MaxHp;
        Invulnerable = RESPAWN_INVULNERABILITY;
        Log.Verbose("Player", $"Deadly hit -> {lost} cell(s) burst, {CellCount} left, biomass {Biomass:F0}.");
        EmitSignal(SignalName.CellsLost, lost);
    }

    /// <summary>Takes over the design built in the editor.</summary>
    /// <param name="design">(Required) The finished draft, copied.</param>
    /// <param name="pointsLeft">(Required) Unspent evolution points, kept for the next editor.</param>
    public void CommitDesign(CellDesign design, int pointsLeft)
    {
        Design.CopyFrom(design);
        EvolutionPoints = pointsLeft;
        ApplyDesign();
        Log.Verbose("Player", $"Design committed with {Design.Parts.Count} part(s), {pointsLeft} EP left.");
        EmitSignal(SignalName.StatsChanged);
    }

    // ===========================================================================
    // ===============================   GROWTH   ================================
    // ===========================================================================
    //
    //   AdvanceStage   Nucleus at stage 1, multicellular at stage 2 (from Divide), EP bonus on both.
    //   ApplyDesign    Recomputes every trait from the placed parts (or the creature) and the divisions,
    //                  takes the custom color of the design.
    //   ApplyCreature  Speed, turning, senses and weapons of an organism from its body plan.
    //   UpdateRadius   Cell radius: the cluster size follows the biomass, the cells share it.
    //

    private static readonly Color PROTOCELL_COLOR = new(0.55f, 0.85f, 0.95f);
    private static readonly Color SINGLE_CELL_COLOR = new(0.45f, 0.85f, 0.65f);

    /// <summary>Default coral of the clay, the creature keeps it until the editor picks another color.</summary>
    public static readonly Color ORGANISM_COLOR = new(0.98f, 0.5f, 0.45f);
    private Color _stageColor;

    /// <summary>Color of the current stage, used when the design has no custom color.</summary>
    public Color StageColor => _stageColor;

    private const float PROTOCELL_HP = 50f;
    private const float CELL_HP = 60f;

    private void AdvanceStage()
    {
        Stage++;
        EvolutionPoints += EP_PER_STAGE;
        Log.Verbose("Player", $"Biomass {Biomass:F0} reached -> advancing to stage {Stage} ({STAGE_KEYS[Stage]}).");
        if (SINGLE_STAGE == Stage)
        {
            IsProtocell = false;
            _stageColor = SINGLE_CELL_COLOR;
            _levelStart = Biomass;
            NextLevelAt = Biomass + FIRST_LEVEL_STEP;
            _diet.Reset();
            RegenOrganelles();
        }
        ApplyDesign();
        Hp = MaxHp;
        EmitSignal(SignalName.StageChanged, Stage);
        EmitSignal(SignalName.EvolutionReady);
    }

    /// <summary>Recomputes every trait from the base values and the placed parts.</summary>
    private void ApplyDesign()
    {
        float hpRatio      = MaxHp > 0f ? Hp / MaxHp : 1f;
        float hpMultiplier = 1f;
        _speedMultiplier = 1f;
        _landSpeed       = CreatureStats.CRAWL_SPEED;
        _acceleration    = BASE_ACCELERATION;
        _digestAll       = 1f;
        _digestPlant     = 1f;
        _digestMeat      = 1f;
        _photosynthesis  = 0f;
        _toxin           = 1f;
        MagnetRange      = BASE_MAGNET_RANGE;
        _epPerLevel      = EP_PER_LEVEL;
        _spikeAngles.Clear();
        _jawAngles.Clear();
        _plateAngles.Clear();

        // An organism takes every trait from its body plan, the cell parts are history
        if (IsOrganism)
        {
            hpMultiplier = ApplyCreature();
        }
        else
        {
            // A cell takes its traits from the placed parts, the same formula as the NPC cells
            CellStats cell = CellStats.From(Design);
            hpMultiplier     *= cell.Health;
            _speedMultiplier *= cell.Speed;
            _acceleration    *= cell.Acceleration;
            _digestAll       *= cell.DigestAll;
            _digestPlant     *= cell.DigestPlant;
            _digestMeat      *= cell.DigestMeat;
            _photosynthesis  += cell.Photosynthesis;
            _toxin           *= cell.Toxin;
            MagnetRange      += cell.MagnetBonus;
            _spikeAngles.AddRange(cell.SpikeAngles);
            _jawAngles.AddRange(cell.JawAngles);
            _plateAngles.AddRange(cell.PlateAngles);
        }

        hpMultiplier *= 1f + HP_PER_DIVISION * Divisions;
        BodyColor = IsOrganism ? Creature.Color ?? ORGANISM_COLOR : Design.Color ?? _stageColor;
        MaxHp = (0 == Stage ? PROTOCELL_HP : CELL_HP) * hpMultiplier;
        Hp    = hpRatio * MaxHp;
        Diet  = _diet.Value(Design.Lean(), Design.Parts.Count > 0);
    }

    /// <summary>Sets speed, turning, senses and weapons from the creature (CreatureStats).</summary>
    /// <returns>The health multiplier of the creature.</returns>
    private float ApplyCreature()
    {
        CreatureStats stats = CreatureStats.From(Creature);
        _speedMultiplier = stats.SwimSpeed;
        _acceleration   *= stats.Acceleration;
        _digestMeat     *= stats.DigestMeat;
        _landSpeed       = stats.LandSpeed;
        _epPerLevel      = EP_PER_LEVEL + stats.EpPerLevelBonus;
        MagnetRange     += stats.MagnetBonus;
        _jawAngles.AddRange(stats.JawAngles);
        _spikeAngles.AddRange(stats.SpikeAngles);
        return stats.Health;
    }

    /// <summary>
    /// The whole body grows with the biomass (10 + 1.6 * sqrt(biomass)), a cluster shares
    /// that size: each cell is the body size divided by the cluster extent. A division
    /// therefore keeps the body size and shrinks the cells.
    /// </summary>
    private void UpdateRadius()
    {
        float bodySize = 10f + Mathf.Sqrt(Mathf.Max(Biomass, 0f)) * 1.6f;
        Radius = bodySize / ClusterExtent(CellCount);
    }
}
