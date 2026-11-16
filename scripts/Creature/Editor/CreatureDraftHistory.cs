// NOTE:
//   Undo and redo of a creature draft: snapshots of the whole design with the points spent
//   at that moment. The owner (CreatureEditorSession) calls Checkpoint whenever the draft is
//   at rest (no mouse button held): everything that changed since the last checkpoint is one
//   step, so a whole drag of a gizmo or a slider is undone at once. Pure logic, no nodes.
//
//   var history = new CreatureDraftHistory();
//   history.Reset(draft, spent: 0);
//   draft.SetGirth(2, 0.9f);
//   history.Checkpoint(draft, spent: 0);
//   history.Undo(draft, out int spent);   // the girth is back

//
//   Chapters:
//     HISTORY   Snapshots, checkpoint, undo and redo.
//

using System.Collections.Generic;

namespace CellOut;

public sealed class CreatureDraftHistory
{
    // ===========================================================================
    // ===============================   HISTORY   ===============================
    // ===========================================================================
    //
    //   CanUndo, CanRedo   Whether a step lies in that direction.
    //   Reset              A new sitting: the draft as it is becomes the only state.
    //   Checkpoint         Stores the draft as a new step when it changed since the last one.
    //   Undo               Puts the draft back one step.
    //   Redo               Puts the draft forward one step again.
    //

    public const int MAX_STEPS = 60;

    private sealed record State(CreatureDesign Design, int Spent);

    private readonly List<State> _undo = new();      // older states, the last one is the step before the current
    private readonly List<State> _redo = new();
    private State _current;
    private (int Shape, int Paint) _stamp;           // versions of the draft the current state was taken at

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Forgets every step: the draft as it is becomes the only state.</summary>
    /// <param name="draft">(Required) The draft of the sitting.</param>
    /// <param name="spent">(Required) Points spent so far.</param>
    public void Reset(CreatureDesign draft, int spent)
    {
        _undo.Clear();
        _redo.Clear();
        Take(draft, spent);
    }

    /// <summary>Stores the draft as a new step when it changed since the last checkpoint. Redo steps are dropped then.</summary>
    /// <param name="draft">(Required) The draft of the sitting.</param>
    /// <param name="spent">(Required) Points spent right now.</param>
    /// <returns>True when a step was stored.</returns>
    public bool Checkpoint(CreatureDesign draft, int spent)
    {
        if (null == _current || (draft.Version, draft.PaintVersion) == _stamp)
        {
            return false;
        }
        _undo.Add(_current);
        if (_undo.Count > MAX_STEPS)
        {
            _undo.RemoveAt(0);
        }
        _redo.Clear();
        Take(draft, spent);
        return true;
    }

    /// <summary>Puts the draft back one step. Changes since the last checkpoint count as a step of their own.</summary>
    /// <param name="draft">(Required) The draft, replaced in place.</param>
    /// <param name="spent">The points spent at that step, unchanged when nothing was undone.</param>
    /// <returns>False when there is no step to take back.</returns>
    public bool Undo(CreatureDesign draft, ref int spent)
    {
        Checkpoint(draft, spent);
        if (0 == _undo.Count)
        {
            return false;
        }
        _redo.Add(_current);
        Apply(_undo[^1], draft, out spent);
        _undo.RemoveAt(_undo.Count - 1);
        return true;
    }

    /// <summary>Puts the draft forward one step again, as long as nothing changed after the undo.</summary>
    /// <param name="draft">(Required) The draft, replaced in place.</param>
    /// <param name="spent">The points spent at that step, unchanged when nothing was redone.</param>
    /// <returns>False when there is no step to repeat.</returns>
    public bool Redo(CreatureDesign draft, ref int spent)
    {
        if (Checkpoint(draft, spent) || 0 == _redo.Count)
        {
            return false;
        }
        _undo.Add(_current);
        Apply(_redo[^1], draft, out spent);
        _redo.RemoveAt(_redo.Count - 1);
        return true;
    }

    private void Take(CreatureDesign draft, int spent)
    {
        var copy = new CreatureDesign();
        copy.CopyFrom(draft);
        _current = new State(copy, spent);
        _stamp   = (draft.Version, draft.PaintVersion);
    }

    private void Apply(State state, CreatureDesign draft, out int spent)
    {
        draft.CopyFrom(state.Design);
        spent    = state.Spent;
        _current = state;
        _stamp   = (draft.Version, draft.PaintVersion);
    }
}
