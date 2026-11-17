// NOTE:
//   The four key slots of the cell abilities, in an arc around the upper left of the dial
//   bottom right (VitalsDial): the 3D icon of the part, its key 1 to 4, a dark sweep
//   while it recharges and a golden ring while its effect runs (cloak, slime). Only the
//   key abilities show here, the automatic ones need no key. A view: the Hud calls
//   Refresh every frame, it only rebuilds the icons when the slots change.
//
//   var slots = new AbilitySlots();
//   AddChild(slots);
//   slots.Refresh(player.Abilities, player.BodyColor, isShown: !player.IsOrganism);

//
//   Chapters:
//     LAYOUT    Size, place around the dial and the look of a slot.
//     REFRESH   Rebuilds the icons on a change, redraws cooldowns and running effects.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class AbilitySlots : Control
{
    // ===========================================================================
    // ================================   LAYOUT   ===============================
    // ===========================================================================
    //
    //   _Ready     Anchors to the corner of the dial, lets the mouse through.
    //   SlotCenter Center of a slot on the arc around the dial.
    //

    private const float SIZE = 340f;                 // square in the corner, the dial sits in its lower right
    private const float CORNER_MARGIN = 12f;         // like VitalsDial
    private const float DIAL_HALF = 98f;             // half the dial (VitalsDial.SIZE / 2)
    private const float ARC_RADIUS = 138f;           // slots this far from the center of the dial
    private const float SLOT_RADIUS = 27f;
    private const int ICON_PIXELS = 46;
    private const float FIRST_ANGLE = Mathf.Pi;      // left of the dial, the slots climb to its top
    private const float ANGLE_STEP = 0.42f;

    private static readonly Color DISC_COLOR = new(0.04f, 0.1f, 0.12f, 0.78f);
    private static readonly Color RIM_COLOR = new(0.72f, 0.82f, 0.96f, 0.85f);
    private static readonly Color SWEEP_COLOR = new(0f, 0f, 0f, 0.62f);
    private static readonly Color RUNNING_COLOR = new(0.98f, 0.85f, 0.35f);

    private static readonly Vector2 DIAL_CENTER = Vector2.One * (SIZE - DIAL_HALF);

    public override void _Ready()
    {
        CustomMinimumSize = Vector2.One * SIZE;
        Size              = Vector2.One * SIZE;
        MouseFilter       = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
        GrowHorizontal = GrowDirection.Begin;
        GrowVertical   = GrowDirection.Begin;
        Position      -= Vector2.One * CORNER_MARGIN;
    }

    /// <summary>Center of slot i on the arc, slot 0 left of the dial.</summary>
    private static Vector2 SlotCenter(int index)
    {
        return DIAL_CENTER + Vector2.FromAngle(FIRST_ANGLE + index * ANGLE_STEP) * ARC_RADIUS;
    }

    // ===========================================================================
    // ===============================   REFRESH   ===============================
    // ===========================================================================
    //
    //   Refresh   Shows or hides, rebuilds the icons when the slots changed, redraws.
    //   _Draw     Disc and rim under every icon (the sweep and ring sit on top, DrawOverlay).
    //   DrawOverlay   The recharge sweep, the running ring and the key number of one slot.
    //

    private readonly List<PartKind> _kinds = new();
    private readonly List<Control> _overlays = new();
    private AbilitySet _abilities = new();
    private Color _bodyColor = Colors.White;

    /// <summary>Takes the current abilities, called every frame by the Hud.</summary>
    /// <param name="abilities">(Required) The abilities of the player.</param>
    /// <param name="bodyColor">(Required) Color of the mini cells in the icons.</param>
    /// <param name="isShown">(Required) False hides the slots (organism, menus).</param>
    public void Refresh(AbilitySet abilities, Color bodyColor, bool isShown)
    {
        _abilities = abilities;
        Visible    = isShown && abilities.Slots.Count > 0;
        if (!Visible)
        {
            return;
        }

        // Rebuild the icons only when the slots or the color changed
        if (!_kinds.SequenceEqual(abilities.Slots) || _bodyColor != bodyColor)
        {
            Rebuild(abilities.Slots, bodyColor);
        }
        QueueRedraw();
        foreach (Control overlay in _overlays)
        {
            overlay.QueueRedraw();
        }
    }

    /// <summary>One 3D icon and one overlay per slot.</summary>
    private void Rebuild(IReadOnlyList<PartKind> slots, Color bodyColor)
    {
        foreach (Node child in GetChildren())
        {
            child.QueueFree();
        }
        _kinds.Clear();
        _kinds.AddRange(slots);
        _overlays.Clear();
        _bodyColor = bodyColor;
        for (int i = 0; i < slots.Count; i++)
        {
            Vector2 center = SlotCenter(i);
            var icon = new PartIcon(slots[i], bodyColor, ICON_PIXELS, isPartOnly: true)
            {
                Position = center - Vector2.One * ICON_PIXELS * 0.5f,
            };
            AddChild(icon);

            int index   = i;
            var overlay = new Control { Position = center - Vector2.One * SLOT_RADIUS, Size = Vector2.One * SLOT_RADIUS * 2f, MouseFilter = MouseFilterEnum.Ignore };
            overlay.Draw += () => DrawOverlay(overlay, index);
            AddChild(overlay);
            _overlays.Add(overlay);
        }
    }

    public override void _Draw()
    {
        for (int i = 0; i < _kinds.Count; i++)
        {
            Vector2 center = SlotCenter(i);
            DrawCircle(center, SLOT_RADIUS, DISC_COLOR);
            DrawArc(center, SLOT_RADIUS, 0f, Mathf.Tau, 40, RIM_COLOR, 2f, true);
        }
    }

    /// <summary>The dark sweep runs clockwise from 12 o'clock while it recharges, a golden ring while it runs, the key below.</summary>
    private void DrawOverlay(Control overlay, int index)
    {
        if (index >= _kinds.Count)
        {
            return;
        }
        PartKind kind   = _kinds[index];
        Vector2  center = Vector2.One * SLOT_RADIUS;
        float    share  = _abilities.CooldownShare(kind);
        // The sweep as one thick arc from the center to the rim: no polygon to triangulate at a sliver or a full turn
        if (share > 0f)
        {
            float start  = -Mathf.Pi * 0.5f;
            float radius = (SLOT_RADIUS - 1f) * 0.5f;
            overlay.DrawArc(center, radius, start, start + Mathf.Tau * share, 36, SWEEP_COLOR, radius * 2f);
        }
        if (_abilities.IsRunning(kind))
        {
            overlay.DrawArc(center, SLOT_RADIUS + 1f, 0f, Mathf.Tau, 40, RUNNING_COLOR, 3f, true);
        }

        // The key in a small badge at the lower left
        Vector2 badge = center + new Vector2(-SLOT_RADIUS * 0.72f, SLOT_RADIUS * 0.72f);
        overlay.DrawCircle(badge, 9f, RIM_COLOR);
        Font font = GetThemeDefaultFont();
        string key = (index + 1).ToString();
        overlay.DrawString(font, badge + new Vector2(-4f, 5f), key, HorizontalAlignment.Left, -1f, 14, DISC_COLOR with { A = 1f });
    }
}
