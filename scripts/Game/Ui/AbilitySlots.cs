// NOTE:
//   The slots of the abilities in an arc around the upper left of the dial bottom right
//   (VitalsDial), clickable like a key (wish of the user 2026-11-17): in the cell stages
//   the key abilities of the parts (3D icon of the part), from the pack on the three ways
//   court, display and hunt (a drawn glyph), then the ability parts of the creature (its
//   3D icon). Every slot sits on a disc in the color of its
//   diet: herbivore green, omnivore yellow, carnivore red (EditorStyle.DIET_COLORS). A dark
//   sweep runs while it recharges, a golden ring while its effect runs, a dim slot has no
//   target. From the pack on a way slot holds a fan (wish of the user 2026-11-17): a click or
//   its key opens it outwards with the ability parts of the matching tab (not the way itself);
//   a click fires one, the key again fires the way. A view: the Hud calls
//   Refresh every frame and hears SlotPressed and FanPressed.
//
//   var slots = new AbilitySlots();
//   slots.SlotPressed += index => ...;
//   slots.FanPressed  += (slot, item) => ...;
//   AddChild(slots);
//   slots.Refresh(views, player.BodyColor, fans);
//   slots.PressKey(0);                       // key 1: opens the fan of the first way, again fires it

//
//   Chapters:
//     TYPES     One slot as the Hud describes it.
//     LAYOUT    Size, place around the dial and the look of a slot.
//     REFRESH   Rebuilds the slots on a change, redraws cooldowns and running effects.
//     FANS      The fan of a way slot: open, close, its items and their clicks.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class AbilitySlots : Control
{
    // ===========================================================================
    // ================================   TYPES   ================================
    // ===========================================================================
    //
    //   Slot   What one slot shows: a cell part, a way or a creature part, its diet, cooldown, running, dim.
    //

    /// <summary>One slot as the Hud describes it.</summary>
    /// <param name="Part">The part of a cell ability, null for a way.</param>
    /// <param name="Way">The way of the pack or tribe, null for a part.</param>
    /// <param name="Creature">The ability part of a creature, null otherwise.</param>
    /// <param name="Diet">Diet class: the color of the disc.</param>
    /// <param name="Cooldown">1 right after use .. 0 ready.</param>
    /// <param name="IsRunning">Its effect runs (cloak, slime): a golden ring.</param>
    /// <param name="IsDim">Nothing to use it on right now.</param>
    public readonly record struct Slot(PartKind? Part, PackWay? Way, CreaturePartKind? Creature, DietClass Diet, float Cooldown, bool IsRunning, bool IsDim);

    // ===========================================================================
    // ================================   LAYOUT   ===============================
    // ===========================================================================
    //
    //   SlotPressed   A slot was clicked: its index, the Hud uses it like the key.
    //   _Ready        Anchors to the corner of the dial, lets the mouse through between the slots.
    //   SlotCenter    Center of a slot on the arc around the dial.
    //

    [Signal] public delegate void SlotPressedEventHandler(int index);
    [Signal] public delegate void FanPressedEventHandler(int slot, int item);

    private const float SIZE = 340f;                 // square in the corner, the dial sits in its lower right
    private const float CORNER_MARGIN = 12f;         // like VitalsDial
    private const float DIAL_HALF = 98f;             // half the dial (VitalsDial.SIZE / 2)
    private const float ARC_RADIUS = 138f;           // slots this far from the center of the dial
    private const float SLOT_RADIUS = 27f;
    private const int ICON_PIXELS = 46;
    private const float FIRST_ANGLE = Mathf.Pi;      // left of the dial, the slots climb to its top
    private const float ANGLE_STEP = 0.42f;
    private const float DISC_SHADE = 0.55f;          // the diet color darkened under the icon
    private const float DIM_ALPHA = 0.4f;

    private static readonly Color RIM_COLOR = new(0.92f, 0.95f, 1f, 0.9f);
    private static readonly Color SWEEP_COLOR = new(0f, 0f, 0f, 0.62f);
    private static readonly Color RUNNING_COLOR = new(0.98f, 0.85f, 0.35f);
    private static readonly Color KEY_TEXT = new(0.04f, 0.1f, 0.12f);
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
    //   Refresh       Shows or hides, rebuilds the slots when they changed, redraws.
    //   Rebuild       One button per slot: the disc, the icon (cell or creature part) or glyph (way), the overlay.
    //   DrawDisc      The disc in the color of the diet with its rim, dim without a target.
    //   DrawOverlay   The recharge sweep, the running ring and the key number of one slot.
    //   DrawWay       The glyph of a way: a heart (court), a crown (display), fangs (hunt).
    //

    private readonly List<Slot> _slots = new();
    private readonly List<Control> _overlays = new();
    private readonly List<Button> _buttons = new();
    private string _signature = "";
    private Color _bodyColor = Colors.White;

    /// <summary>Takes the slots of this frame, called every frame by the Hud.</summary>
    /// <param name="slots">(Required) The slots in key order, empty hides the arc.</param>
    /// <param name="bodyColor">(Required) Color of the mini cells in the part icons.</param>
    /// <param name="fans">(Optional) Per slot the items of its fan, empty or null for a slot that fires at once. Defaults to none.</param>
    public void Refresh(IReadOnlyList<Slot> slots, Color bodyColor, IReadOnlyList<IReadOnlyList<Slot>> fans = null)
    {
        Visible = slots.Count > 0;
        if (!Visible)
        {
            CloseFan();
            return;
        }
        RefreshFans(fans, bodyColor);

        // Rebuild only when the parts or ways or the color changed, cooldowns just redraw
        string signature = string.Join(",", slots.Select(slot => $"{slot.Part}{slot.Way}{slot.Creature}"));
        if (signature != _signature || _bodyColor != bodyColor)
        {
            Rebuild(slots, bodyColor);
            _signature = signature;
        }
        _slots.Clear();
        _slots.AddRange(slots);
        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Modulate = new Color(1f, 1f, 1f, _slots[i].IsDim ? DIM_ALPHA : 1f);
            _buttons[i].QueueRedraw();
            _overlays[i].QueueRedraw();
        }
    }

    /// <summary>One round button per slot with its disc, its icon or glyph and an overlay on top.</summary>
    private void Rebuild(IReadOnlyList<Slot> slots, Color bodyColor)
    {
        CloseFan();
        foreach (Node child in GetChildren())
        {
            child.QueueFree();
        }
        _overlays.Clear();
        _buttons.Clear();
        _slots.Clear();
        _slots.AddRange(slots);
        _bodyColor = bodyColor;
        for (int i = 0; i < slots.Count; i++)
        {
            int index  = i;
            var button = new Button
            {
                Position          = SlotCenter(i) - Vector2.One * SLOT_RADIUS,
                Size              = Vector2.One * SLOT_RADIUS * 2f,
                CustomMinimumSize = Vector2.One * SLOT_RADIUS * 2f,
                FocusMode         = FocusModeEnum.None,
                Flat              = true,
                MouseDefaultCursorShape = CursorShape.PointingHand,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
            };
            foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
            {
                button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            }
            button.Draw    += () => DrawDisc(button, index < _slots.Count ? (Slot?)_slots[index] : null, SLOT_RADIUS);
            button.Pressed += () => PressSlot(index);
            AddChild(button);
            _buttons.Add(button);

            Vector2 middle = Vector2.One * SLOT_RADIUS;
            if (slots[i].Part is PartKind part)
            {
                button.AddChild(new PartIcon(part, bodyColor, ICON_PIXELS, isPartOnly: true) { Position = middle - Vector2.One * ICON_PIXELS * 0.5f });
            }
            else if (slots[i].Creature is CreaturePartKind creature)
            {
                Control icon = CreatureIcon.New(CreatureIcon.DesignFor(CreatureCatalog.Get(creature), CreatureType.Fish), bodyColor, ICON_PIXELS, isTurning: false, isPartsOnly: true);
                icon.Position    = middle - Vector2.One * ICON_PIXELS * 0.5f;
                icon.MouseFilter = MouseFilterEnum.Ignore;
                button.AddChild(icon);
            }
            else if (slots[i].Way is PackWay way)
            {
                var glyph = new Control { Size = Vector2.One * SLOT_RADIUS * 2f, MouseFilter = MouseFilterEnum.Ignore };
                glyph.Draw += () => DrawWay(glyph, way);
                button.AddChild(glyph);
            }

            var overlay = new Control { Size = Vector2.One * SLOT_RADIUS * 2f, MouseFilter = MouseFilterEnum.Ignore };
            overlay.Draw += () => DrawOverlay(overlay, index < _slots.Count ? (Slot?)_slots[index] : null, SLOT_RADIUS, (index + 1).ToString());
            button.AddChild(overlay);
            _overlays.Add(overlay);
        }
    }

    /*
      The disc, built like a glass button (the look holds on every graphics tier, no shader):
      - a soft shadow below it
      - the diet color from a dark edge to a light middle in rings
      - a gloss on the upper half, a light rim above and a dark rim below (embossed)
      Hovered it lights up a little.
    */
    /// <summary>The disc in the color of the diet: shaded, glossy, embossed, brighter while hovered.</summary>
    private static void DrawDisc(Button button, Slot? slot, float radius)
    {
        if (null == slot)
        {
            return;
        }
        Color   diet   = EditorStyle.DIET_COLORS[slot.Value.Diet];
        Vector2 center = Vector2.One * radius;
        float   lift   = button.IsHovered() ? 0.18f : 0f;

        button.DrawCircle(center + new Vector2(0f, 2.5f), radius + 1.5f, new Color(0f, 0f, 0f, 0.35f));
        const int RINGS = 7;
        for (int ring = 0; ring < RINGS; ring++)
        {
            float share = (float)ring / RINGS;
            Color fill  = diet.Darkened(Mathf.Lerp(0.72f, 0.28f, share) - lift);
            button.DrawCircle(center + new Vector2(0f, -share * radius * 0.12f), radius * (1f - share * 0.62f), fill);
        }

        // Gloss: a flat bright ellipse over the upper half
        button.DrawSetTransform(center + new Vector2(0f, -radius * 0.42f), 0f, new Vector2(1f, 0.5f));
        button.DrawCircle(Vector2.Zero, radius * 0.66f, new Color(1f, 1f, 1f, 0.16f + lift * 0.3f));
        button.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

        // Embossed rim: light above, dark below
        button.DrawArc(center, radius - 1f, Mathf.Pi, Mathf.Tau, 24, diet.Lerp(Colors.White, 0.6f), 2.2f, true);
        button.DrawArc(center, radius - 1f, 0f, Mathf.Pi, 24, diet.Darkened(0.6f), 2.2f, true);
    }
    /// <summary>The dark sweep runs clockwise from 12 o'clock while it recharges, a golden ring while it runs, the key below.</summary>
    private void DrawOverlay(Control overlay, Slot? slotOrNone, float radius, string key)
    {
        if (null == slotOrNone)
        {
            return;
        }
        Slot    slot   = slotOrNone.Value;
        Vector2 center = Vector2.One * radius;

        // The sweep as one thick arc from the center to the rim: no polygon to triangulate at a sliver or a full turn
        if (slot.Cooldown > 0f)
        {
            float start  = -Mathf.Pi * 0.5f;
            float half = (radius - 1f) * 0.5f;
            overlay.DrawArc(center, half, start, start + Mathf.Tau * slot.Cooldown, 36, SWEEP_COLOR, half * 2f);
        }
        if (slot.IsRunning)
        {
            overlay.DrawArc(center, radius + 1f, 0f, Mathf.Tau, 40, RUNNING_COLOR, 3f, true);
        }

        // The key in a small badge at the lower left, none on the parts of a fan
        if (string.IsNullOrEmpty(key))
        {
            return;
        }
        Vector2 badge = center + new Vector2(-radius * 0.72f, radius * 0.72f);
        overlay.DrawCircle(badge, 9f, RIM_COLOR);
        overlay.DrawString(GetThemeDefaultFont(), badge + new Vector2(-4f, 5f), key, HorizontalAlignment.Left, -1f, 14, KEY_TEXT);
    }

    private static readonly Color GLYPH_COLOR = new(1f, 0.98f, 0.94f);
    private static readonly Color GLYPH_SHADOW = new(0f, 0f, 0f, 0.38f);
    private static readonly Color JEWEL_COLOR = new(0.95f, 0.3f, 0.35f);

    /// <summary>A glyph per way, cream white with a soft shadow: a heart to court, a crown to display, two curved fangs to hunt.</summary>
    private static void DrawWay(Control glyph, PackWay way, float radius = SLOT_RADIUS)
    {
        Vector2 c    = Vector2.One * radius;
        float   size = radius / SLOT_RADIUS;
        switch (way)
        {
            case PackWay.Court:
            {
                // The classic heart curve, 48 points
                var heart = new Vector2[48];
                for (int i = 0; i < heart.Length; i++)
                {
                    float a = i / (float)heart.Length * Mathf.Tau;
                    float x = 16f * Mathf.Pow(Mathf.Sin(a), 3f);
                    float y = 13f * Mathf.Cos(a) - 5f * Mathf.Cos(2f * a) - 2f * Mathf.Cos(3f * a) - Mathf.Cos(4f * a);
                    heart[i] = c + new Vector2(x, -y + 1.5f) * 0.82f * size;
                }
                DrawShaded(glyph, heart);
                glyph.DrawCircle(c + new Vector2(-6f, -5f) * size, 2.6f * size, new Color(1f, 1f, 1f, 0.9f));
                break;
            }
            case PackWay.Display:
            {
                // Crown: a band and three spikes with balls on their tips, jewels on the band
                Vector2[] crown =
                {
                    c + new Vector2(-13f, 10f) * size, c + new Vector2(-14f, -6f) * size, c + new Vector2(-6.5f, 1f) * size,
                    c + new Vector2(0f, -11f) * size, c + new Vector2(6.5f, 1f) * size, c + new Vector2(14f, -6f) * size,
                    c + new Vector2(13f, 10f) * size,
                };
                DrawShaded(glyph, crown);
                foreach (Vector2 tip in new[] { new Vector2(-14f, -7.5f), new Vector2(0f, -12.5f), new Vector2(14f, -7.5f) })
                {
                    glyph.DrawCircle(c + tip * size, 2.6f * size, GLYPH_COLOR);
                }
                glyph.DrawRect(new Rect2(c + new Vector2(-13f, 5f) * size, new Vector2(26f, 2.2f) * size), new Color(0f, 0f, 0f, 0.18f));
                foreach (float x in new[] { -7f, 0f, 7f })
                {
                    glyph.DrawCircle(c + new Vector2(x, 2.5f) * size, 1.9f * size, JEWEL_COLOR);
                }
                break;
            }
            default:
            {
                // An upper jaw as a crescent, two long fangs at its ends curving in, small teeth between
                var jaw = new List<Vector2>();
                for (int i = 0; i <= 12; i++)
                {
                    float a = Mathf.Lerp(0.35f, Mathf.Pi - 0.35f, i / 12f);
                    jaw.Add(c + new Vector2(Mathf.Cos(a) * 14f, -Mathf.Sin(a) * 6f - 4f) * size);
                }
                for (int i = 12; i >= 0; i--)
                {
                    float a = Mathf.Lerp(0.35f, Mathf.Pi - 0.35f, i / 12f);
                    jaw.Add(c + new Vector2(Mathf.Cos(a) * 14f, -Mathf.Sin(a) * 2f - 1f) * size);
                }
                DrawShaded(glyph, jaw.ToArray());
                DrawShaded(glyph, Fang(c + new Vector2(-9f, -2f) * size, 1f, size));
                DrawShaded(glyph, Fang(c + new Vector2(9f, -2f) * size, -1f, size));
                foreach (float x in new[] { -3f, 3f })
                {
                    glyph.DrawColoredPolygon(new[] { c + new Vector2(x - 2f, -1.5f) * size, c + new Vector2(x + 2f, -1.5f) * size, c + new Vector2(x, 3.5f) * size }, GLYPH_COLOR);
                }
                break;
            }
        }
    }

    /// <summary>One fang: wide at the jaw, a sharp tip curving towards the middle (inward 1 curls right, -1 left).</summary>
    private static Vector2[] Fang(Vector2 root, float inward, float size)
    {
        const float WIDTH = 3.6f;
        const float LENGTH = 14f;
        const float CURL = 4f;
        var points = new List<Vector2>();
        for (int i = 0; i <= 8; i++)
        {
            float t = i / 8f;
            points.Add(root + new Vector2(-WIDTH * (1f - t) + inward * CURL * t * t, LENGTH * t) * size);
        }
        for (int i = 8; i >= 0; i--)
        {
            float t = i / 8f;
            points.Add(root + new Vector2(WIDTH * (1f - t) + inward * CURL * t * t, LENGTH * t) * size);
        }
        return points.ToArray();
    }

    /// <summary>A filled shape in cream white over its own soft shadow.</summary>
    private static void DrawShaded(Control glyph, Vector2[] shape)
    {
        glyph.DrawColoredPolygon(shape.Select(point => point + new Vector2(1.2f, 2f)).ToArray(), GLYPH_SHADOW);
        glyph.DrawColoredPolygon(shape, GLYPH_COLOR);
    }
    // ===========================================================================
    // =================================   FANS   ================================
    // ===========================================================================
    //
    //   PressSlot     A click on a slot: opens or closes its fan, a slot without one fires at once.
    //   PressKey      The key of a slot: opens its fan, the same key again fires the slot itself (the way).
    //   CloseFan      Closes the open fan.
    //   RefreshFans   Takes the fans of this frame, rebuilds the open one on a change, closes it after a while.
    //   BuildFan      One small button per item, spread around the outward direction of its slot.
    //

    private const float FAN_DISTANCE = 84f;          // from the center of the slot to its items
    private const float FAN_SPREAD = 0.68f;          // radians between two items
    private const float ITEM_RADIUS = 23f;
    private const int ITEM_ICON_PIXELS = 40;
    private const float FAN_SECONDS = 5f;            // an untouched fan closes again

    private readonly List<IReadOnlyList<Slot>> _fans = new();
    private readonly List<Button> _fanButtons = new();
    private readonly List<Control> _fanOverlays = new();
    private int _openFan = -1;
    private string _fanSignature = "";
    private float _fanAge;

    private bool HasFan(int index) => index < _fans.Count && null != _fans[index] && _fans[index].Count > 0;

    private void PressSlot(int index)
    {
        if (!HasFan(index))
        {
            EmitSignal(SignalName.SlotPressed, index);
            return;
        }
        if (_openFan == index)
        {
            CloseFan();
            return;
        }
        _openFan = index;
        _fanAge  = 0f;
        BuildFan(_bodyColor);
    }

    /// <summary>The key of a slot: opens its fan, the same key again fires the first item (the way itself); a slot without a fan fires.</summary>
    /// <param name="index">(Required) The slot, 0 for key 1.</param>
    /// <returns>False when there is no such slot.</returns>
    public bool PressKey(int index)
    {
        if (index < 0 || index >= _slots.Count)
        {
            return false;
        }
        if (HasFan(index) && _openFan == index)
        {
            CloseFan();
            EmitSignal(SignalName.SlotPressed, index);     // the way itself
            return true;
        }
        PressSlot(index);
        return true;
    }

    /// <summary>Closes the open fan, if any.</summary>
    public void CloseFan()
    {
        _openFan = -1;
        foreach (Button button in _fanButtons)
        {
            button.QueueFree();
        }
        _fanButtons.Clear();
        _fanOverlays.Clear();
        _fanSignature = "";
    }

    private void RefreshFans(IReadOnlyList<IReadOnlyList<Slot>> fans, Color bodyColor)
    {
        _fans.Clear();
        if (null != fans)
        {
            _fans.AddRange(fans);
        }
        if (_openFan < 0)
        {
            return;
        }
        _fanAge += (float)GetProcessDeltaTime();
        if (!HasFan(_openFan) || _fanAge > FAN_SECONDS)
        {
            CloseFan();
            return;
        }
        string signature = string.Join(",", _fans[_openFan].Select(slot => $"{slot.Way}{slot.Creature}"));
        if (signature != _fanSignature)
        {
            BuildFan(bodyColor);
        }
        for (int i = 0; i < _fanButtons.Count; i++)
        {
            _fanButtons[i].Modulate = new Color(1f, 1f, 1f, _fans[_openFan][i].IsDim ? DIM_ALPHA : 1f);
            _fanOverlays[i].QueueRedraw();
        }
    }

    private void BuildFan(Color bodyColor)
    {
        int open = _openFan;
        CloseFan();
        _openFan = open;
        IReadOnlyList<Slot> items = _fans[open];
        _fanSignature = string.Join(",", items.Select(slot => $"{slot.Way}{slot.Creature}"));

        // Items around the direction from the dial out through the slot
        Vector2 slot    = SlotCenter(open);
        float   outward = (slot - DIAL_CENTER).Angle();
        for (int k = 0; k < items.Count; k++)
        {
            int     item   = k;
            float   angle  = outward + (k - (items.Count - 1) * 0.5f) * FAN_SPREAD;
            Vector2 center = slot + Vector2.FromAngle(angle) * FAN_DISTANCE;
            var button = new Button
            {
                Position                = center - Vector2.One * ITEM_RADIUS,
                Size                    = Vector2.One * ITEM_RADIUS * 2f,
                FocusMode               = FocusModeEnum.None,
                Flat                    = true,
                MouseDefaultCursorShape = CursorShape.PointingHand,
                AutoTranslateMode       = AutoTranslateModeEnum.Disabled,
                PivotOffset             = Vector2.One * ITEM_RADIUS,
            };
            foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
            {
                button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            }
            button.Draw += () => DrawDisc(button, item < _fans[open].Count ? (Slot?)_fans[open][item] : null, ITEM_RADIUS);
            button.Pressed += () =>
            {
                CloseFan();
                EmitSignal(SignalName.FanPressed, open, item);
            };
            if (items[k].Creature is CreaturePartKind creature)
            {
                Control icon = CreatureIcon.New(CreatureIcon.DesignFor(CreatureCatalog.Get(creature), CreatureType.Fish), bodyColor, ITEM_ICON_PIXELS, isTurning: false, isPartsOnly: true);
                icon.Position    = Vector2.One * (ITEM_RADIUS - ITEM_ICON_PIXELS * 0.5f);
                icon.MouseFilter = MouseFilterEnum.Ignore;
                button.AddChild(icon);
                button.TooltipText = Tr(CreatureCatalog.Get(creature).NameKey);
            }
            else if (items[k].Way is PackWay way)
            {
                var glyph = new Control { Size = Vector2.One * ITEM_RADIUS * 2f, MouseFilter = MouseFilterEnum.Ignore };
                glyph.Draw += () => DrawWay(glyph, way, ITEM_RADIUS);
                button.AddChild(glyph);
            }
            var overlay = new Control { Size = Vector2.One * ITEM_RADIUS * 2f, MouseFilter = MouseFilterEnum.Ignore };
            overlay.Draw += () => DrawOverlay(overlay, item < _fans[open].Count ? (Slot?)_fans[open][item] : null, ITEM_RADIUS, "");
            button.AddChild(overlay);
            AddChild(button);
            _fanButtons.Add(button);
            _fanOverlays.Add(overlay);
            EditorStyle.PopIn(button, k * 0.04f);
        }
    }
}