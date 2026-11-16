// NOTE:
//   Tests of undo and redo of a creature draft (scripts/Creature/Editor/CreatureDraftHistory.cs).
//
//   dotnet test tests/unit/CellOut.Tests.csproj --filter CreatureDraftHistoryTests

using Xunit;

namespace CellOut.Tests;

public class CreatureDraftHistoryTests
{
    [Fact]
    public void StepsGoBackAndForthWithThePointsSpent()
    {
        CreatureDesign draft = CreatureDesign.FromGirths(CreatureType.Fish, new[] { 0.4f, 0.6f, 0.8f, 0.6f }, null);
        var history = new CreatureDraftHistory();
        int spent   = 0;
        history.Reset(draft, spent);
        Assert.False(history.CanUndo);
        Assert.False(history.Checkpoint(draft, spent));                        // nothing changed

        // Two steps: a girth, then a part that costs points
        draft.SetGirth(1, 0.9f);
        Assert.True(history.Checkpoint(draft, spent));
        Assert.True(draft.TryPlace(CreaturePartKind.Eye, 0.8f, 1));
        spent = 1;
        draft.SetPartSize(0, 2f);                                              // same step: no checkpoint in between
        Assert.True(history.Checkpoint(draft, spent));

        Assert.True(history.Undo(draft, ref spent));
        Assert.Empty(draft.Parts);
        Assert.Equal(0, spent);
        Assert.Equal(0.9f, draft.Girths[1], 4);
        Assert.True(history.Undo(draft, ref spent));
        Assert.Equal(0.6f, draft.Girths[1], 4);
        Assert.False(history.Undo(draft, ref spent));

        Assert.True(history.Redo(draft, ref spent));
        Assert.True(history.Redo(draft, ref spent));
        Assert.Single(draft.Parts);
        Assert.Equal(2f, draft.Parts[0].Size, 4);
        Assert.Equal(1, spent);
        Assert.False(history.Redo(draft, ref spent));

        // A change after an undo drops the redo steps; an undo without a checkpoint takes the open change back
        Assert.True(history.Undo(draft, ref spent));
        draft.SetGirth(2, 0.5f);
        Assert.False(history.Redo(draft, ref spent));
        Assert.True(history.Undo(draft, ref spent));
        Assert.Equal(0.8f, draft.Girths[2], 4);

        // Spray paint counts as a change too
        int paint = draft.PaintVersion;
        draft.ClearPaint();
        Assert.Equal(paint != draft.PaintVersion, history.Checkpoint(draft, spent));
    }
}
