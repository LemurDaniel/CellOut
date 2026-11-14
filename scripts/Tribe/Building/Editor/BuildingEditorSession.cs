// NOTE:
//   One sitting in the village builder as plain state and rules: a draft per building
//   function, the function shown, the block picked from the palette with its turn and
//   paint. Dropping centers the block on the pointed cell, an emptied draft falls back to
//   the first template when the sitting ends. The BuildingEditor creates it and hands it
//   to stage and views, they follow Changed (drafts, function) and SelectionChanged
//   (picked block, turn, paint). Pure logic without the engine, tested in tests/unit.
//
//   var session = new BuildingEditorSession();
//   session.Begin(kind => tribe.DesignOf(kind));
//   session.Select(BlockKind.ConeRoof);
//   session.PlaceAt(cellX: 4, cellZ: 4);
//   BuildingDesign result = session.Result(BuildingKind.Dwelling);

//
//   Chapters:
//     SITTING   Drafts, the function shown and how a sitting begins and ends.
//     BLOCKS    The picked block, its turn and paint, dropping and removing.
//

using System;
using System.Collections.Generic;

namespace CellOut;

public sealed class BuildingEditorSession
{
    // ===========================================================================
    // ===============================   SITTING   ===============================
    // ===========================================================================
    //
    //   Changed         A draft or the function shown changed.
    //   Kind            The function shown.
    //   Draft           The draft of the function shown.
    //   Begin           Copies the designs of the tribe into fresh drafts.
    //   ShowKind        Switches the function shown.
    //   ApplyTemplate   Replaces the draft with a template of its function.
    //   Clear           Empties the draft.
    //   Result          The draft of a function, the first template when it is empty.
    //

    public event Action Changed;

    private readonly Dictionary<BuildingKind, BuildingDesign> _drafts = new();

    public BuildingKind Kind { get; private set; } = BuildingKind.Dwelling;

    public BuildingDesign Draft => _drafts[Kind];

    public BuildingEditorSession()
    {
        foreach (BuildingKind kind in Enum.GetValues<BuildingKind>())
        {
            _drafts[kind] = BuildingTemplates.Default(kind);
        }
    }

    /// <summary>Starts a sitting: every draft becomes a copy of the current design of its function.</summary>
    /// <param name="designOf">(Required) The current design of a function, copied.</param>
    public void Begin(Func<BuildingKind, BuildingDesign> designOf)
    {
        foreach (BuildingKind kind in Enum.GetValues<BuildingKind>())
        {
            _drafts[kind].CopyFrom(designOf(kind));
        }
        Selected = null;
        Turns    = 0;
        Paint    = BlockCatalog.DEFAULT_PAINT;
        Changed?.Invoke();
        SelectionChanged?.Invoke();
    }

    public void ShowKind(BuildingKind kind)
    {
        if (kind == Kind)
        {
            return;
        }
        Kind = kind;
        Changed?.Invoke();
    }

    /// <summary>Replaces the draft of the function shown with one of its templates.</summary>
    /// <param name="index">(Required) 0..BuildingTemplates.Count(Kind).</param>
    public void ApplyTemplate(int index)
    {
        Draft.CopyFrom(BuildingTemplates.Build(Kind, index));
        Changed?.Invoke();
    }

    public void Clear()
    {
        Draft.Clear();
        Changed?.Invoke();
    }

    /// <summary>What the tribe builds for a function after the sitting.</summary>
    /// <returns>The draft, the first template of the function when the draft holds no block (nothing to see, nothing to hit).</returns>
    public BuildingDesign Result(BuildingKind kind)
    {
        return _drafts[kind].Blocks.Count > 0 ? _drafts[kind] : BuildingTemplates.Default(kind);
    }

    // ===========================================================================
    // ===============================   BLOCKS   ================================
    // ===========================================================================
    //
    //   SelectionChanged   The picked block, its turn or the paint changed.
    //   Selected           The block picked from the palette, null for none.
    //   Turns              Quarter turns of the next block.
    //   Paint              Paint of the next block, BlockCatalog.DEFAULT_PAINT for its own.
    //   Select             Picks a block, the same block again lets go.
    //   Rotate             A quarter turn more.
    //   PickPaint          Sets the paint.
    //   AnchorFor          The lowest corner that centers the picked block on a cell, kept inside the grid.
    //   PlaceAt            Drops the picked block centered on a cell.
    //   Remove             Removes a placed block.
    //

    public event Action SelectionChanged;

    public BlockKind? Selected { get; private set; }

    public int Turns { get; private set; }

    public int Paint { get; private set; } = BlockCatalog.DEFAULT_PAINT;

    /// <summary>Picks a block from the palette.</summary>
    /// <param name="kind">(Required) The block, null or the picked one again for none.</param>
    public void Select(BlockKind? kind)
    {
        Selected = kind == Selected ? null : kind;
        SelectionChanged?.Invoke();
    }

    public void Rotate()
    {
        Turns = (Turns + 1) & 3;
        SelectionChanged?.Invoke();
    }

    /// <summary>Sets the paint of the next blocks.</summary>
    /// <param name="paint">(Required) Index in BlockCatalog.PAINTS, BlockCatalog.DEFAULT_PAINT for the paint of each block.</param>
    public void PickPaint(int paint)
    {
        Paint = paint >= 0 && paint < BlockCatalog.PAINTS.Count ? paint : BlockCatalog.DEFAULT_PAINT;
        SelectionChanged?.Invoke();
    }

    /// <summary>The lowest corner that puts the picked block centered on a cell, pushed back into the grid at its edges.</summary>
    /// <param name="cellX">(Required) The pointed cell along X.</param>
    /// <param name="cellZ">(Required) The pointed cell along Z.</param>
    /// <returns>The corner, null when no block is picked.</returns>
    public (int X, int Z)? AnchorFor(int cellX, int cellZ)
    {
        if (Selected is not BlockKind kind)
        {
            return null;
        }
        (int width, int depth) = BlockCatalog.Footprint(kind, Turns);
        int x = Math.Clamp(cellX - (width - 1) / 2, 0, BuildingDesign.GRID_SIZE - width);
        int z = Math.Clamp(cellZ - (depth - 1) / 2, 0, BuildingDesign.GRID_SIZE - depth);
        return (x, z);
    }

    /// <summary>Drops the picked block centered on a cell, with the current turn and paint.</summary>
    /// <returns>False when no block is picked or it does not fit (too high, design full).</returns>
    public bool PlaceAt(int cellX, int cellZ)
    {
        if (Selected is not BlockKind kind || AnchorFor(cellX, cellZ) is not (int x, int z))
        {
            return false;
        }
        if (!Draft.TryPlace(kind, x, z, Turns, Paint))
        {
            return false;
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>Removes a placed block of the draft, the ones above fall.</summary>
    /// <param name="index">(Required) Index in Draft.Blocks.</param>
    /// <returns>False for an index outside Draft.Blocks.</returns>
    public bool Remove(int index)
    {
        if (!Draft.RemoveAt(index))
        {
            return false;
        }
        Changed?.Invoke();
        return true;
    }
}
