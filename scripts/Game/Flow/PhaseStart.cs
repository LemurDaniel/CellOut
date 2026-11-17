// NOTE:
//   A game started in a later phase from the galaxy menu: after the birth of the protocell
//   it jumps there with the stage jumps of the F10 menu (Main.CheatTo). From the organism
//   on the creature is shaped first: the jump to the organism opens the creature editor,
//   once it closes the jump to land, pack or tribe follows. Also remembers the furthest
//   phase of every game per planet (Core/PlanetProgress.cs), which opens the phases in the menu.
//
//   var phaseStart = new PhaseStart { Main = this };
//   AddChild(phaseStart);
//   phaseStart.Begin(startFlow.Phase);          // when the protocell is born

//
//   Chapters:
//     JUMP       Waits for the game, then jumps into the chosen phase.
//     PROGRESS   Every new stage is remembered for the planet.
//

using Godot;

namespace CellOut;

public partial class PhaseStart : Node
{
    // ===========================================================================
    // ================================   JUMP   =================================
    // ===========================================================================
    //
    //   Main       (Required) The game the jumps act on.
    //   Begin      The protocell is born: remembers it, a later phase starts its jump.
    //   _Process   Jumps once the game runs, a land phase after the creature editor closed.
    //

    public Main Main { get; set; }

    private CheatTarget? _target;

    /// <summary>The protocell is born: remembers the phase, a later chosen phase jumps on the next frames.</summary>
    /// <param name="phase">(Required) The phase chosen in the menu.</param>
    public void Begin(StartPhase phase)
    {
        Reach(StartPhase.Protocell);
        _target = StartPhases.JumpOf(phase);
        if (null != _target)
        {
            Log.Verbose("PhaseStart", $"Jumping into phase {phase} on {Planets.Current.Key}.");
        }
    }

    /*
      > Up to the organism: one jump
      > Land, pack, tribe: first the organism (the creature editor opens and pauses the game),
        the jump follows once the creature swims
    */
    public override void _Process(double delta)
    {
        if (null == _target || Main.IsInMenu || Main.IsEvolving)
        {
            return;
        }
        CheatTarget target = _target.Value;
        bool needsCreature = target is CheatTarget.LandAnimal or CheatTarget.Pack or CheatTarget.Tribe or CheatTarget.Civilization;
        if (needsCreature && !Main.Player.IsOrganism)
        {
            Main.CheatTo(CheatTarget.Organism);
            return;
        }
        _target = null;
        Main.CheatTo(target);
    }

    // ===========================================================================
    // ===============================   PROGRESS   ==============================
    // ===========================================================================
    //
    //   _Ready   Listens to the stages of the player.
    //   Reach    Remembers a phase for the current planet.
    //

    public override void _Ready()
    {
        Main.Player.StageChanged += stage => Reach(StartPhases.OfStage(stage));
    }

    private static void Reach(StartPhase phase)
    {
        PlanetProgress.Reach(Planets.Current.Key, phase);
    }
}
