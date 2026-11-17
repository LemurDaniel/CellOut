// NOTE:
//   One sitting in the village builder as plain state and rules: a draft per building
//   function, the function shown, the block group shown in the palette and the block
//   picked from it with its turn and paint. Dropping centers the block on the pointed cell, an emptied draft falls back to
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
//     POSE      The placed block under the gizmo: shift, turn, scale.
//

using System;
using System.Collections.Generic;
using Godot;

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
    //   DraftOf         The draft of any function (the icons of the tabs).
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

    public BuildingDesign DraftOf(BuildingKind kind)
    {
        return _drafts[kind];
    }

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
        Picked   = null;
        Era      = BlockEra.Village;
        Group    = BlockGroup.Wall;
        Turns    = 0;
        Paint    = BlockCatalog.DEFAULT_PAINT;
        Pattern  = 0;
        Changed?.Invoke();
        SelectionChanged?.Invoke();
    }

    public void ShowKind(BuildingKind kind)
    {
        if (kind == Kind)
        {
            return;
        }
        Kind   = kind;
        Picked = null;
        Changed?.Invoke();
    }

    /// <summary>Replaces the draft of the function shown with one of its templates.</summary>
    /// <param name="index">(Required) 0..BuildingTemplates.Count(Kind).</param>
    public void ApplyTemplate(int index)
    {
        Draft.CopyFrom(BuildingTemplates.Build(Kind, index));
        Picked = null;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Draft.Clear();
        Picked = null;
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
    //   SelectionChanged   The group shown, the picked block, its turn or the paint changed.
    //   Era                The era the palette shows (village, modern).
    //   ShowEra            Switches the era shown and opens its first group.
    //   Group              The block group of the era the palette shows.
    //   ShowGroup          Switches the group shown.
    //   Selected           The block picked from the palette, null for none.
    //   Turns              Quarter turns of the next block.
    //   Paint              Paint of the next block, BlockCatalog.DEFAULT_PAINT for its own.
    //   Select             Picks a block, the same block again lets go.
    //   Rotate             A quarter turn more.
    //   Pattern            Pattern of the next block, 0 for plain.
    //   PickPaint          Sets the paint.
    //   PickPattern        Sets the pattern.
    //   Repaint            Gives a placed block the current paint and pattern.
    //   AnchorFor          The lowest corner that centers the picked block on a cell, kept inside the grid.
    //   PlaceAt            Drops the picked block centered on a cell.
    //   Remove             Removes a placed block.
    //

    public event Action SelectionChanged;

    public BlockEra Era { get; private set; } = BlockEra.Village;

    public BlockGroup Group { get; private set; } = BlockGroup.Wall;

    public BlockKind? Selected { get; private set; }

    /// <summary>Switches the era shown, the palette opens its first group that holds blocks. An era without blocks stays closed.</summary>
    /// <param name="era">(Required) The era.</param>
    public void ShowEra(BlockEra era)
    {
        foreach (BlockInfo info in BlockCatalog.In(era))
        {
            Era   = era;
            Group = info.Group;
            SelectionChanged?.Invoke();
            return;
        }
    }

    public void ShowGroup(BlockGroup group)
    {
        Group = group;
        SelectionChanged?.Invoke();
    }

    public int Turns { get; private set; }

    public int Paint { get; private set; } = BlockCatalog.DEFAULT_PAINT;

    public int Pattern { get; private set; }

    /// <summary>Picks a block from the palette, the palette shows its group.</summary>
    /// <param name="kind">(Required) The block, null or the picked one again for none.</param>
    public void Select(BlockKind? kind)
    {
        Selected = kind == Selected ? null : kind;
        if (Selected is BlockKind picked)
        {
            Era   = BlockCatalog.Get(picked).Era;
            Group = BlockCatalog.Get(picked).Group;
        }
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
        RepaintPicked();
        SelectionChanged?.Invoke();
    }

    /// <summary>Sets the pattern of the next blocks.</summary>
    /// <param name="pattern">(Required) 0..BlockCatalog.PATTERN_COUNT, 0 is plain.</param>
    public void PickPattern(int pattern)
    {
        Pattern = Math.Clamp(pattern, 0, BlockCatalog.PATTERN_COUNT - 1);
        RepaintPicked();
        SelectionChanged?.Invoke();
    }

    /// <summary>A paint or pattern picked while a placed block carries the gizmo repaints that block.</summary>
    private void RepaintPicked()
    {
        if (Picked is int index && Draft.Repaint(index, Paint, Pattern))
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Gives a placed block of the draft the current paint and pattern.</summary>
    /// <param name="index">(Required) Index in Draft.Blocks.</param>
    /// <returns>False for an index outside Draft.Blocks.</returns>
    public bool Repaint(int index)
    {
        if (!Draft.Repaint(index, Paint, Pattern))
        {
            return false;
        }
        Changed?.Invoke();
        return true;
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
        if (!Draft.TryPlace(kind, x, z, Turns, Paint, Pattern))
        {
            return false;
        }
        Picked = Draft.Blocks.Count - 1;      // the gizmo sits on the block just dropped
        Changed?.Invoke();
        SelectionChanged?.Invoke();
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
        Picked = null;      // the indices behind the removed block moved
        Changed?.Invoke();
        return true;
    }

    // ===========================================================================
    // =================================   POSE   ================================
    // ===========================================================================
    //
    //   Posed        The picked block moved, turned or grew: only its transform changed.
    //   Picked       The placed block the gizmo sits on, null for none.
    //   Pick         Puts the gizmo on a placed block or takes it away.
    //   MovePicked   Shifts the picked block.
    //   TurnPicked   Turns the picked block around a world axis.
    //   GrowPicked   Scales the picked block, along the axis of its own frame closest to a world axis or as a whole.
    //   ResetPicked  Back to its plain place on the grid.
    //   CommitPose   A drag of the gizmo ended: icons and everything else follow (Changed).
    //

    public event Action Posed;

    public int? Picked { get; private set; }

    /// <summary>Puts the gizmo on a placed block of the draft.</summary>
    /// <param name="index">(Required) Index in Draft.Blocks, null or an index outside it for none.</param>
    public void Pick(int? index)
    {
        Picked = index is int picked && picked >= 0 && picked < Draft.Blocks.Count ? picked : null;
        SelectionChanged?.Invoke();
    }

    /// <summary>Shifts the picked block.</summary>
    /// <param name="shift">(Required) World shift in model units.</param>
    public void MovePicked(Vector3 shift)
    {
        if (Picked is int index)
        {
            PlacedBlock block = Draft.Blocks[index];
            Draft.SetPose(index, block.Offset + shift, block.Rotation, block.Scale);
            Posed?.Invoke();
        }
    }

    /// <summary>Turns the picked block around a world axis through its place, on top of the turn it has.</summary>
    /// <param name="axis">(Required) Normalized world axis.</param>
    /// <param name="angle">(Required) Radians, right handed.</param>
    public void TurnPicked(Vector3 axis, float angle)
    {
        if (Picked is int index)
        {
            PlacedBlock block  = Draft.Blocks[index];
            Basis       turned = new Basis(axis, angle) * Basis.FromEuler(block.Rotation);
            Draft.SetPose(index, block.Offset, turned.Orthonormalized().GetEuler(), block.Scale);
            Posed?.Invoke();
        }
    }

    /*
      The gizmo pulls along world axes, a block scales in its own frame:
      > No axis (the scale ring): the whole block grows.
      > An axis (a stretch cube): the axis of the block that lies closest to it grows, so a
        block turned by a quarter stretches where the cube was pulled.
    */
    /// <summary>Scales the picked block.</summary>
    /// <param name="factor">(Required) Growth, above 1 bigger.</param>
    /// <param name="axis">(Optional) World axis of a stretch, null scales the whole block. Defaults to null.</param>
    public void GrowPicked(float factor, Vector3? axis = null)
    {
        if (Picked is not int index)
        {
            return;
        }
        PlacedBlock block = Draft.Blocks[index];
        Vector3     scale = block.Scale;
        if (axis is Vector3 world)
        {
            Basis   turned = Basis.FromEuler(block.Rotation) * new Basis(Vector3.Up, block.Turns * Mathf.Pi * 0.5f);
            Vector3 own    = (turned.Inverse() * world).Abs();
            int     along  = (int)own.MaxAxisIndex();
            scale[along]  *= factor;
        }
        else
        {
            scale *= factor;
        }
        Draft.SetPose(index, block.Offset, block.Rotation, scale);
        Posed?.Invoke();
    }

    /// <summary>The picked block goes back to its plain place: no shift, no turn, its own size.</summary>
    public void ResetPicked()
    {
        if (Picked is int index)
        {
            Draft.SetPose(index, Vector3.Zero, Vector3.Zero, Vector3.One);
            Changed?.Invoke();
        }
    }

    /// <summary>A drag of the gizmo ended, everything that shows the draft follows.</summary>
    public void CommitPose()
    {
        Changed?.Invoke();
    }
}
