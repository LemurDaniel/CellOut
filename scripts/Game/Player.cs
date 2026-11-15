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
//   On land (Main sets IsOnLand at the coast) it walks on legs, without legs it only crawls. The first time on land makes it a land
//   animal, whatever it has. The pack (a nest with conspecifics) and the tribe (the pack settles
//   around a fire, the player becomes one commanded unit of many) are founded by the
//   PackKeeper (Tribe/Pack/) through FoundSociety.
//   Created by Main, which listens to its signals. Steered with WASD, arrows or the held
//   mouse, divides or evolves with Space or the HUD buttons. In the third person view of
//   the land animal Main turns the keys with the camera (InputTurn): W walks away from it.

//
//   Chapters:
//     STATE              Stages, evolution points, signals, health and the traits from the design (PlayerTraits).
//     MOVE               Input, movement, regeneration, photosynthesis and diet fading.
//     DIVISION           When the cells may divide and the division itself.
//     ORGANISM           Evolving the full cluster into a creature and committing its body plan.
//     LAND               Crawling and walking on land, the step to the land animal.
//     SOCIETY            Pack and tribe: the stages the PackKeeper founds, orders and the takeover of a kin.
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
    //   Stages      Translation keys and indices of the stages, the numbers of the player.
    //   Evolution   Evolution points and the diet.
    //   Signals     Stats, evolution, stage changes and deaths for Main and the HUD.
    //   Traits      Health and the values the design sets (PlayerTraits).
    //

    // ---------------------------------------------------------------------------
    // Stages
    //   Translation keys and indices of the stages, the numbers of the player.
    // ---------------------------------------------------------------------------

    // The multicellular stage has no threshold, the first division reaches it (DIVISION),
    // the organism comes from evolving the full cluster (ORGANISM), the land animal from
    // the first step onto land (LAND)
    /// <summary>Every tunable number of the player from data/balance.json (player), the formulas are in Growth.</summary>
    private static PlayerBalance Tune => Balance.Current.Player;

    public static readonly string[] STAGE_KEYS = { "STAGE_PROTOCELL", "STAGE_SINGLE", "STAGE_MULTI", "STAGE_ORGANISM", "STAGE_LAND", "STAGE_PACK", "STAGE_TRIBE" };
    public const int SINGLE_STAGE = 1;
    public const int MULTICELLULAR_STAGE = 2;
    public const int ORGANISM_STAGE = 3;
    public const int LAND_STAGE = 4;
    public const int PACK_STAGE = 5;
    public const int TRIBE_STAGE = 6;

    public int Stage { get; private set; }
    public int Level { get; private set; }
    public float Biomass { get; private set; }
    public float NextLevelAt { get; private set; } = float.PositiveInfinity;
    private float _levelStart;

    // ---------------------------------------------------------------------------
    // Evolution
    //   Evolution points and the diet.
    // ---------------------------------------------------------------------------

    public int EvolutionPoints { get; private set; }
    public float Diet { get; private set; }          // -1 herbivore .. 1 carnivore
    private readonly DietTracker _diet = new();

    // ---------------------------------------------------------------------------
    // Signals
    //   Stats, evolution, stage changes and deaths for Main and the HUD.
    // ---------------------------------------------------------------------------

    [Signal] public delegate void StatsChangedEventHandler();
    [Signal] public delegate void EvolutionReadyEventHandler();      // stage advance: an open editor shows the new points
    [Signal] public delegate void StageChangedEventHandler(int stage);
    [Signal] public delegate void DividedEventHandler(int cellCount);
    [Signal] public delegate void EvolveRequestedEventHandler();     // Main plays the cutscene and opens the creature editor
    [Signal] public delegate void DiedEventHandler();
    [Signal] public delegate void CellsLostEventHandler(int lost);

    // ---------------------------------------------------------------------------
    // Traits
    //   Health and the values the design sets (PlayerTraits).
    // ---------------------------------------------------------------------------

    public float MaxHp { get; private set; } = Tune.ProtocellHp;
    public float Hp { get; private set; } = Tune.ProtocellHp;
    public float Invulnerable { get; set; }

    /// <summary>Every trait from the parts of the cell or the body plan of the creature, renewed by ApplyDesign (GROWTH).</summary>
    private PlayerTraits _traits = PlayerTraits.Base();

    public float MagnetRange => _traits.MagnetRange;

    /// <summary>Traits only a cell has (regeneration, slime, pili, pigment, stings, suckers), neutral for an organism.</summary>
    public CellStats CellTraits => _traits.Cell;

    // ===========================================================================
    // ================================   MOVE   =================================
    // ===========================================================================
    //
    //   InputTurn         Turn of the key directions, set by Main from the camera yaw.
    //   _Ready            Protocell look and a short grace period.
    //   _PhysicsProcess   Steers by keys or mouse, regenerates, photosynthesizes, fades the diet.
    //

    /// <summary>Turn of the key directions in radians, 0 in the top view (W = screen up).</summary>
    public float InputTurn { get; set; }

    public override void _Ready()
    {
        base._Ready();
        IsProtocell  = true;
        _stageColor  = PROTOCELL_COLOR;
        BodyColor    = _stageColor;
        Invulnerable = Tune.StartInvulnerability;
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

        // Keys and the held mouse button, or the order of the tribe (SOCIETY)
        Vector2 input = (IsCommanded ? OrderDirection() : SteeringInput()).LimitLength(1f);

        Velocity  = Velocity.Lerp(input * CurrentSpeed(), Mathf.Clamp(_traits.Acceleration * dt, 0f, 1f));
        Position += Velocity * dt;

        // Regeneration, photosynthesis and fading meals
        Invulnerable = Mathf.Max(0f, Invulnerable - dt);
        Hp           = Mathf.Min(MaxHp, Hp + MaxHp * Tune.RegenPerSecond * CellTraits.Regen * dt);
        if (_traits.Photosynthesis > 0f)
        {
            // By body size, not cell radius: a division shrinks the cells and must not cut the light
            AddBiomass(_traits.Photosynthesis * BodySize() / REFERENCE_SIZE * dt, BiomassSource.Light);
        }
        _diet.Decay(dt);
        Diet = _diet.Value(Design.Lean(), Design.Parts.Count > 0);
        UpdateLand(dt);
    }

    /// <summary>WASD, arrows or the held mouse button, the keys turned with the camera.</summary>
    private Vector2 SteeringInput()
    {
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
        return input;
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

    /// <summary>Divisions so far, an organism counts the divisions of the cutscene up to ORGANISM_CELLS.</summary>
    public int Divisions => System.Numerics.BitOperations.Log2((uint)(IsOrganism ? ORGANISM_CELLS : CellCount));

    public float NextDivisionAt => Growth.DivisionAt(Divisions);

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
    //   EvolvedAt        Biomass at the moment of becoming an organism.
    //   RequestEvolve    Asks Main to play the cutscene and open the creature editor.
    //   BecomeOrganism   Takes over the shaped body plan: one body, stage 4, every trait from the creature.
    //   CommitCreature   A later creature editor session: new body plan, points left.
    //
    //   Bonus points (organismBonusEp) come on top of the saved ones for the first creature editor.
    //

    public bool IsOrganism => Stage >= ORGANISM_STAGE;

    public float EvolveAt => Growth.DivisionAt(System.Numerics.BitOperations.Log2(MAX_CELLS));

    public bool CanEvolve => MULTICELLULAR_STAGE == Stage && MAX_CELLS == CellCount && Biomass >= EvolveAt;

    /// <summary>Biomass when the cluster became an organism (at least EvolveAt), 0 before. Later steps like the coast grow from it.</summary>
    public float EvolvedAt { get; private set; }

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
        EvolvedAt       = Biomass;
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
    //   IsLandAnimal   From stage 5 on: the creature went onto land, whatever it walks or breathes with.
    //   UpdateLand     The first time on land advances the stage.
    //
    //   Main sets CellBody.IsOnLand every physics frame from the Coast.
    //

    public bool IsLandAnimal => Stage >= LAND_STAGE;

    /*
      > Not an organism yet: nothing to do
      > First time on land: the creature becomes a land animal, with or without legs or lung
        (decision of the user 2026-11-15); nobody needs air on land any more, without legs it crawls
    */
    /// <summary>The step onto land, once per physics frame.</summary>
    private void UpdateLand(float dt)
    {
        if (!IsOrganism)
        {
            return;
        }

        if (ORGANISM_STAGE == Stage && IsOnLand)
        {
            Stage            = LAND_STAGE;
            EvolutionPoints += Tune.EpPerStage;
            Log.Verbose("Player", $"First steps on land -> stage {Stage} ({STAGE_KEYS[Stage]}).");
            EmitSignal(SignalName.StageChanged, Stage);
            EmitSignal(SignalName.StatsChanged);
        }
    }

    // ===========================================================================
    // ==============================   SOCIETY   ================================
    // ===========================================================================
    //
    //   IsPackLeader   From stage 6 on: a nest with conspecifics that follow.
    //   IsTribal       Stage 7: the pack settled around a fire, the player is one commanded unit.
    //   IsCommanded    The tribe steers the body by orders (OrderTarget), keys and mouse are ignored.
    //   FoundSociety   The PackKeeper advances to the pack or the tribe, one stage at a time.
    //   Revive         A dead leader lives on in the body of a pack member (PackKeeper.TryTakeOver).
    //

    public bool IsPackLeader => Stage >= PACK_STAGE;

    public bool IsTribal => Stage >= TRIBE_STAGE;

    /// <summary>(Optional) True from the tribe on: the body follows OrderTarget instead of the keys. Defaults to false.</summary>
    public bool IsCommanded { get; set; }

    /// <summary>Advances to the pack (from the land animal) or the tribe (from the pack), with the stage bonus.</summary>
    /// <param name="stage">(Required) PACK_STAGE or TRIBE_STAGE, the one right after the current stage.</param>
    /// <example><code>player.FoundSociety(Player.PACK_STAGE);   // after the nest is built</code></example>
    public void FoundSociety(int stage)
    {
        bool isSocial = PACK_STAGE == stage || TRIBE_STAGE == stage;
        if (!isSocial || stage != Stage + 1)
        {
            throw new System.InvalidOperationException($"stage must be the pack or the tribe right after stage {Stage}, got '{stage}'.");
        }
        Stage            = stage;
        EvolutionPoints += Tune.EpPerStage;
        IsCommanded      = TRIBE_STAGE == stage;
        Log.Verbose("Player", $"Society founded -> stage {Stage} ({STAGE_KEYS[Stage]}).");
        EmitSignal(SignalName.StageChanged, Stage);
        EmitSignal(SignalName.StatsChanged);
    }

    /// <summary>Heals a share of the maximum health, e.g. resting at the nest.</summary>
    /// <param name="share">(Required) Share of MaxHp, clamped at full health.</param>
    public void Heal(float share)
    {
        if (IsDead || Hp >= MaxHp)
        {
            return;
        }
        Hp = Mathf.Min(MaxHp, Hp + MaxHp * share);
        EmitSignal(SignalName.StatsChanged);
    }

    /// <summary>The leader lives on: full health, a grace period, at the place of the pack member taken over.</summary>
    /// <param name="at">(Required) Plane position of the body taken over.</param>
    public void Revive(Vector2 at)
    {
        if (!IsDead)
        {
            throw new System.InvalidOperationException("Revive is only for a dead player.");
        }
        IsDead       = false;
        Hp           = MaxHp;
        Invulnerable = Tune.RespawnInvulnerability;
        Position     = at;
        Velocity     = Vector2.Zero;
        Log.Verbose("Player", $"Revived at {at} in the body of a pack member.");
        EmitSignal(SignalName.StatsChanged);
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

    /// <summary>Swim speed in the water, on land the walking (or crawling) speed of LAND.</summary>
    public float CurrentSpeed()
    {
        return SwimSpeed(BodySize()) * (IsOnLand ? _traits.LandSpeed : _traits.Speed) * StunFactor;
    }

    public float GoalProgress()
    {
        if (0 == Stage)
        {
            return Biomass / Tune.SingleCellAt;
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
        return _traits.ContactDamage(worldAngle - Facing);
    }

    /// <summary>Whether a jaw points towards the given world direction.</summary>
    public bool CanBiteToward(float worldAngle)
    {
        return _traits.CanBite(worldAngle - Facing);
    }

    /// <summary>
    /// The player swallows a cell when its body size is larger than the cell radius times this
    /// ratio (Growth.EatRatio): lower with a jaw towards it and with every division (bigger prey).
    /// </summary>
    /// <param name="worldAngle">(Required) Direction from the player to the prey.</param>
    /// <example><code>player.EatRatioToward(0f)  // 1.15 as single cell, 0.9 with 32 cells</code></example>
    public float EatRatioToward(float worldAngle)
    {
        return Growth.EatRatio(CanBiteToward(worldAngle), Divisions);
    }

    /// <summary>Share of the damage taken from the given world direction, plates halve it, slime lowers it everywhere.</summary>
    public float DamageFactorFrom(float worldAngle)
    {
        return _traits.DamageFactor(worldAngle - Facing);
    }

    public bool HasThorns()
    {
        return _traits.HasThorns;
    }

    // ===========================================================================
    // ==============================   GAMEPLAY   ===============================
    // ===========================================================================
    //
    //   AddBiomass     Grows, counts the diet, advances stages and pays EP per level (the
    //                  player opens the editor when it wants, nothing opens it by itself).
    //   TakeDamage     Hurts and pushes back, zero health is game over (Die),
    //                  a cluster loses a quarter of its cells instead (LoseCells).
    //   Digest         Steady damage inside a bigger cell: no push back, no grace period.
    //   CheckDeath     Dies or loses cells at zero health, shared by both.
    //   Die            Game over: the body stops, Main shows the game over screen.
    //   LoseCells      The cluster survives a deadly hit by bursting a quarter of its cells.
    //   CommitDesign   Takes over the design and points left from the editor.
    //

    /// <summary>Adds biomass and handles diet, stage advances and level ups.</summary>
    /// <param name="amount">(Required) Biomass before digestion.</param>
    /// <param name="source">(Optional) Plant and meat are digested and counted for the diet. Defaults to Plant.</param>
    public void AddBiomass(float amount, BiomassSource source = BiomassSource.Plant)
    {
        float gained = source switch
        {
            BiomassSource.Plant => amount * _traits.DigestAll * _traits.DigestPlant,
            BiomassSource.Meat  => amount * _traits.DigestAll * _traits.DigestMeat,
            _                   => amount,
        };
        Biomass += gained;
        if (Stage >= SINGLE_STAGE)
        {
            _diet.Add(source, gained);
        }
        UpdateRadius();

        if (0 == Stage && Biomass >= Tune.SingleCellAt)
        {
            AdvanceStage();
        }
        while (Biomass >= NextLevelAt)
        {
            Level++;
            EvolutionPoints += _traits.EpPerLevel;
            _levelStart      = NextLevelAt;
            NextLevelAt      = Growth.NextLevelAt(NextLevelAt);
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
        Invulnerable  = Tune.HitInvulnerability;
        Velocity     -= toAttacker.Normalized() * CurrentSpeed() * Tune.Knockback * CellTraits.Knockback;
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
        int lost = Growth.CellsLost(CellCount);
        Biomass  *= 1f - (float)lost / CellCount;
        CellCount -= lost;
        UpdateRadius();
        ApplyDesign();
        Hp           = MaxHp;
        Invulnerable = Tune.RespawnInvulnerability;
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
    //   ApplyMutations Traits, EP and protocell hue from the mutations of the RNA prelude.
    //   AdvanceStage   Nucleus at stage 1, multicellular at stage 2 (from Divide), EP bonus on both.
    //   ApplyDesign    Renews every trait from the placed parts (or the creature) and the divisions,
    //                  takes the custom color of the design.
    //   UpdateRadius   Cell radius: the cluster size follows the biomass, the cells share it.
    //

    private static readonly Color PROTOCELL_COLOR = new(0.55f, 0.85f, 0.95f);
    private static readonly Color SINGLE_CELL_COLOR = new(0.45f, 0.85f, 0.65f);

    /// <summary>Default coral of the clay, the creature keeps it until the editor picks another color.</summary>
    public static readonly Color ORGANISM_COLOR = new(0.98f, 0.5f, 0.45f);
    private Color _stageColor;

    /// <summary>Color of the current stage, used when the design has no custom color.</summary>
    public Color StageColor => _stageColor;

    private const float MUTATION_HUE_SHIFT = 0.07f;  // per mutation of the RNA prelude

    private readonly List<MutationKind> _mutations = new();

    /// <summary>Mutations from the RNA prelude, their traits last through the cell phase (ApplyDesign).</summary>
    public IReadOnlyList<MutationKind> Mutations => _mutations;

    /// <summary>
    /// Takes over the mutations the RNA strand caught in the prelude (Prelude/RnaWorld.cs):
    /// their traits (Prelude/Mutation.cs), evolution points for the good ones (capped) and a hue shift of the protocell.
    /// </summary>
    /// <param name="mutations">(Required) Mutations of the strand, empty changes nothing.</param>
    /// <example><code>player.ApplyMutations(new[] { MutationKind.Tough, MutationKind.Sluggish });</code></example>
    public void ApplyMutations(IReadOnlyList<MutationKind> mutations)
    {
        if (0 == mutations.Count || !IsProtocell)
        {
            return;
        }
        _mutations.AddRange(mutations);
        EvolutionPoints += Mutation.EvolutionPoints(mutations);

        Color tinted = PROTOCELL_COLOR;
        tinted.H    = Mathf.Wrap(PROTOCELL_COLOR.H + MUTATION_HUE_SHIFT * _mutations.Count, 0f, 1f);
        _stageColor = tinted;
        ApplyDesign();
        Log.Verbose("Player", $"Mutations from the prelude: {string.Join(", ", mutations)} -> {EvolutionPoints} EP, hue {tinted.H:F2}.");
        EmitSignal(SignalName.StatsChanged);
    }

    private void AdvanceStage()
    {
        Stage++;
        EvolutionPoints += Tune.EpPerStage;
        Log.Verbose("Player", $"Biomass {Biomass:F0} reached -> advancing to stage {Stage} ({STAGE_KEYS[Stage]}).");
        if (SINGLE_STAGE == Stage)
        {
            IsProtocell = false;
            _stageColor = SINGLE_CELL_COLOR;
            _levelStart = Biomass;
            NextLevelAt = Growth.FirstLevelAt(Biomass);
            _diet.Reset();
            RegenOrganelles();
        }
        ApplyDesign();
        Hp = MaxHp;
        EmitSignal(SignalName.StageChanged, Stage);
        EmitSignal(SignalName.EvolutionReady);
    }

    /// <summary>Renews every trait: an organism takes them from its body plan, a cell from its parts and the mutations of the prelude.</summary>
    private void ApplyDesign()
    {
        float hpRatio = MaxHp > 0f ? Hp / MaxHp : 1f;
        _traits = IsOrganism ? PlayerTraits.OfCreature(Creature) : PlayerTraits.OfCell(Design, _mutations);

        float hpMultiplier = _traits.Health * (1f + Tune.HpPerDivision * Divisions);
        BodyColor = IsOrganism ? Creature.Color ?? ORGANISM_COLOR : Design.Color ?? _stageColor;
        MaxHp = (0 == Stage ? Tune.ProtocellHp : Tune.CellHp) * hpMultiplier;
        Hp    = hpRatio * MaxHp;
        Diet  = _diet.Value(Design.Lean(), Design.Parts.Count > 0);
    }

    /// <summary>
    /// The whole body grows with the biomass (Growth.BodySize), a cluster shares
    /// that size: each cell is the body size divided by the cluster extent. A division
    /// therefore keeps the body size and shrinks the cells.
    /// </summary>
    private void UpdateRadius()
    {
        Radius = Growth.BodySize(Biomass) / ClusterExtent(CellCount);
    }
}
