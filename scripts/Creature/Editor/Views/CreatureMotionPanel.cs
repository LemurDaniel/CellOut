// NOTE:
//   The animation mode of the creature editor, two panels beside the turntable:
//   - BodyPanel (left): the preview (pace, in the water or on land) and the motion of the
//     whole animal (swim wave, its pace, breathing, every part on or off at once).
//   - PartPanel (right): the own motion of the picked part: on or off, pace, swing (limbs),
//     offset in the cycle, blinking (eyes), and one strength slider per channel of its model
//     animation (the rattle: tail lift and shaking bells). Without a picked part it says how to pick one.
//   A view: it only reports clicks and sliders (events) and shows the session (Refresh).
//   A plain node that owns no layout itself, the editor places its two panels.
//
//   var motion = new CreatureMotionPanel();
//   motion.Setup(session, paletteWidth, CreatureToolbar.SIDE_WIDTH, stage.Clay.ChannelsOf);
//   motion.PartMotionChanged += value => session.SetPickedMotion(value);
//   AddChild(motion);

//
//   Chapters:
//     BUILD PANELS     Preview, body motion and the motion of the picked part.
//     REFRESH PANELS   Texts, switches and sliders from the session.
//

using Godot;

namespace CellOut;

public partial class CreatureMotionPanel : Node
{
    // ===========================================================================
    // ============================   BUILD PANELS   =============================
    // ===========================================================================
    //
    //   PreviewChanged      Pace or place of the preview changed (pace 0..1, isOnLand).
    //   BodyMotionChanged   Swim wave, its pace or the breathing changed.
    //   AllPartsToggled     Every part on or off at once.
    //   PartMotionChanged   The motion of the picked part changed.
    //   BodyPanel, PartPanel   The two panels, placed by the editor.
    //   Setup               Builds both panels.
    //   NewToggle           A pill that switches between two states.
    //

    public event System.Action<float, bool> PreviewChanged;
    public event System.Action<float, float, bool> BodyMotionChanged;
    public event System.Action<bool> AllPartsToggled;
    public event System.Action<PartMotion> PartMotionChanged;
    public event System.Action<string, float> ChannelChanged;

    /// <summary>Left of the turntable: preview and the motion of the whole animal.</summary>
    public CreaturePanel BodyPanel { get; private set; }

    /// <summary>Right of the turntable: the motion of the picked part.</summary>
    public CreaturePanel PartPanel { get; private set; }

    private CreatureEditorSession _session;
    private Label _paceLabel;
    private HSlider _paceSlider;
    private Button _placeButton;
    private Label _waveLabel;
    private HSlider _waveSlider;
    private Label _waveSpeedLabel;
    private HSlider _waveSpeedSlider;
    private Button _breathButton;
    private Button _allOnButton;
    private Button _allOffButton;
    private Label _partName;
    private Label _partHint;
    private Button _onButton;
    private Label _speedLabel;
    private HSlider _speedSlider;
    private Control _speedRow;
    private Label _amountLabel;
    private HSlider _amountSlider;
    private Control _amountRow;
    private Label _phaseLabel;
    private HSlider _phaseSlider;
    private Control _phaseRow;
    private Button _blinkButton;
    private System.Func<int, System.Collections.Generic.IReadOnlyList<string>> _channelsOf;
    private VBoxContainer _channelRows;
    private string _channelKey = "";                 // part and channel names the rows were built for
    private readonly System.Collections.Generic.List<(string Name, Label Label, HSlider Slider)> _channelSliders = new();

    /// <summary>Builds both panels, call once before the editor places them.</summary>
    /// <param name="session">(Required) The sitting: draft, picked part and the preview.</param>
    /// <param name="bodyWidth">(Required) Width of the body panel: as wide as the part palette it replaces.</param>
    /// <param name="partWidth">(Required) Width of the part panel.</param>
    /// <param name="channelsOf">(Required) The channels of the model animation of a part by its index (CreatureVisual3D.ChannelsOf).</param>
    public void Setup(CreatureEditorSession session, float bodyWidth, float partWidth, System.Func<int, System.Collections.Generic.IReadOnlyList<string>> channelsOf)
    {
        _session    = session;
        _channelsOf = channelsOf;

        // Preview: how fast the animal moves on the turntable, swimming or walking
        BodyPanel = new CreaturePanel();
        BodyPanel.Setup(bodyWidth);
        BodyPanel.AddHeadline("CREATURE_MOTION_PREVIEW");
        _paceLabel  = new Label();
        _paceSlider = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05 };
        _paceSlider.ValueChanged += value => PreviewChanged?.Invoke((float)value, _session.PreviewsLand);
        BodyPanel.AddSliderRow(_paceLabel, _paceSlider);
        _placeButton = NewToggle(BodyPanel, isOn => PreviewChanged?.Invoke(_session.PreviewSpeed, isOn));

        // The whole animal: swim wave, its pace, the breathing mouth, every part at once
        BodyPanel.AddHeadline("CREATURE_MOTION_BODY");
        _waveLabel  = new Label();
        _waveSlider = new HSlider { MinValue = 0, MaxValue = CreatureLook.MAX_WAVE_AMOUNT, Step = 0.1 };
        _waveSlider.ValueChanged += _ => RaiseBody();
        BodyPanel.AddSliderRow(_waveLabel, _waveSlider);
        _waveSpeedLabel  = new Label();
        _waveSpeedSlider = new HSlider { MinValue = CreatureLook.MIN_WAVE_SPEED, MaxValue = CreatureLook.MAX_WAVE_SPEED, Step = 0.05 };
        _waveSpeedSlider.ValueChanged += _ => RaiseBody();
        BodyPanel.AddSliderRow(_waveSpeedLabel, _waveSpeedSlider);
        _breathButton = NewToggle(BodyPanel, _ => RaiseBody());
        var all = new HBoxContainer();
        all.AddThemeConstantOverride("separation", 8);
        BodyPanel.Sections.AddChild(all);
        _allOnButton  = CreatureEditorUi.NewPillButton(new Vector2(0, 34), 14);
        _allOffButton = CreatureEditorUi.NewPillButton(new Vector2(0, 34), 14);
        _allOnButton.Pressed  += () => AllPartsToggled?.Invoke(true);
        _allOffButton.Pressed += () => AllPartsToggled?.Invoke(false);
        foreach (Button button in new[] { _allOnButton, _allOffButton })
        {
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            EditorStyle.ApplyButton(button, EditorStyle.RIM_COLOR);
            all.AddChild(button);
        }

        // The picked part: what it can do shows, the rest stays hidden (Refresh)
        PartPanel = new CreaturePanel();
        PartPanel.Setup(partWidth);
        PartPanel.AddHeadline("CREATURE_MOTION_PART");
        _partName = CreatureEditorUi.NewLabel(15);
        _partHint = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _partHint.AddThemeFontSizeOverride("font_size", 13);
        PartPanel.Sections.AddChild(_partName);
        PartPanel.Sections.AddChild(_partHint);
        _onButton = NewToggle(PartPanel, _ => RaisePart());
        _speedLabel  = new Label();
        _speedSlider = new HSlider { MinValue = PartMotion.MIN_SPEED, MaxValue = PartMotion.MAX_SPEED, Step = 0.05 };
        _speedSlider.ValueChanged += _ => RaisePart();
        _speedRow = PartPanel.AddSliderRow(_speedLabel, _speedSlider);
        _amountLabel  = new Label();
        _amountSlider = new HSlider { MinValue = 0, MaxValue = PartMotion.MAX_AMOUNT, Step = 0.05 };
        _amountSlider.ValueChanged += _ => RaisePart();
        _amountRow = PartPanel.AddSliderRow(_amountLabel, _amountSlider);
        _phaseLabel  = new Label();
        _phaseSlider = new HSlider { MinValue = 0, MaxValue = 0.95, Step = 0.05 };
        _phaseSlider.ValueChanged += _ => RaisePart();
        _phaseRow = PartPanel.AddSliderRow(_phaseLabel, _phaseSlider);
        _blinkButton = NewToggle(PartPanel, _ => RaisePart());

        // One slider per channel of the model animation (a bone or node it moves), built for the picked part (RefreshChannels)
        _channelRows = new VBoxContainer();
        _channelRows.AddThemeConstantOverride("separation", 6);
        PartPanel.Sections.AddChild(_channelRows);
    }

    /// <summary>A pill with two states in a panel, the text follows the state in Refresh.</summary>
    private static Button NewToggle(CreaturePanel panel, System.Action<bool> toggled)
    {
        Button button = CreatureEditorUi.NewPillButton(new Vector2(0, 34), 14);
        button.ToggleMode = true;
        button.Toggled   += isOn => toggled(isOn);
        panel.Sections.AddChild(button);
        return button;
    }

    private void RaiseBody()
    {
        BodyMotionChanged?.Invoke((float)_waveSlider.Value, (float)_waveSpeedSlider.Value, _breathButton.ButtonPressed);
    }

    private void RaisePart()
    {
        PartMotionChanged?.Invoke(new PartMotion(_onButton.ButtonPressed, (float)_speedSlider.Value, (float)_amountSlider.Value, (float)_phaseSlider.Value, _blinkButton.ButtonPressed));
    }

    // ===========================================================================
    // ===========================   REFRESH PANELS   ============================
    // ===========================================================================
    //
    //   Refresh       Both panels only in the animation mode, with the state of the session.
    //   Poll          Follows the channels of the picked part once the turntable has rebuilt it.
    //   RefreshPart   What the picked part can do: its switches and sliders, else a hint.
    //   RefreshChannels   One slider per channel of its model animation.
    //   Toggle        Text, state and color of a pill with two states.
    //

    private static readonly System.Globalization.CultureInfo NUMBER = System.Globalization.CultureInfo.InvariantCulture;

    /// <summary>Shows the panels in the animation mode with the state of the session.</summary>
    public void Refresh()
    {
        bool isShown = CreatureEditorMode.Animate == _session.Mode && !_session.IsChoosingType;
        BodyPanel.Visible = isShown;
        PartPanel.Visible = isShown;
        if (!isShown)
        {
            return;
        }
        BodyPanel.Translate();
        PartPanel.Translate();
        CreatureDesign draft  = _session.Draft;
        Color          accent = CreatureEditorUi.TypeAccent(draft.Type);

        _paceLabel.Text = string.Format(Tr("CREATURE_MOTION_PACE"), Mathf.RoundToInt(_session.PreviewSpeed * 100f));
        _paceSlider.SetValueNoSignal(_session.PreviewSpeed);
        Toggle(_placeButton, _session.PreviewsLand, "CREATURE_MOTION_LAND", "CREATURE_MOTION_WATER", accent);

        _waveLabel.Text      = string.Format(Tr("CREATURE_MOTION_WAVE"), Mathf.RoundToInt(draft.WaveAmount * 100f));
        _waveSpeedLabel.Text = string.Format(Tr("CREATURE_MOTION_WAVE_SPEED"), Mathf.RoundToInt(draft.WaveSpeed * 100f));
        _waveSlider.SetValueNoSignal(draft.WaveAmount);
        _waveSpeedSlider.SetValueNoSignal(draft.WaveSpeed);
        Toggle(_breathButton, draft.IsBreathing, "CREATURE_MOTION_BREATH_ON", "CREATURE_MOTION_BREATH_OFF", accent);
        _allOnButton.Text  = Tr("CREATURE_MOTION_ALL_ON");
        _allOffButton.Text = Tr("CREATURE_MOTION_ALL_OFF");

        RefreshPart(draft, accent);
    }

    /// <summary>
    /// Once per frame in the animation mode: the channels of the picked part are only known once the turntable has
    /// rebuilt the animal (a frame after the change), the panel follows when they differ from the rows it shows.
    /// </summary>
    public void Poll()
    {
        int picked = _session.PickedPart;
        if (!PartPanel.Visible || picked < 0)
        {
            return;
        }
        if (!_channelKey.EndsWith($":{string.Join(",", _channelsOf(picked))}"))
        {
            Refresh();
        }
    }

    private void RefreshPart(CreatureDesign draft, Color accent)
    {
        int  picked  = _session.PickedPart;
        bool hasPart = picked >= 0 && picked < draft.Parts.Count;
        CreaturePart part = hasPart ? draft.Parts[picked] : default;

        // What this part can do: a model with a loop, a limb that swings, an eye that blinks
        bool hasLoop = hasPart && CreatureModels.MODELS.TryGetValue(part.Kind, out var model) && null != model.Animation;
        bool isLimb  = hasPart && null != part.Joints;
        bool isEye   = hasPart && CreatureCategory.Eyes == part.Info.Category;
        bool isMoved = hasLoop || isLimb;

        _partName.Visible = hasPart;
        _partName.Text    = hasPart ? Tr(part.Info.NameKey) : "";
        _partHint.Visible = !hasPart || (!isMoved && !isEye);
        _partHint.Text    = Tr(hasPart ? "CREATURE_MOTION_NONE" : "CREATURE_MOTION_PICK");

        PartMotion motion = hasPart ? part.Movement : PartMotion.REST;
        _onButton.Visible    = isMoved;
        _speedRow.Visible    = isMoved && motion.IsOn;
        _amountRow.Visible   = isLimb && motion.IsOn;
        _phaseRow.Visible    = isMoved && motion.IsOn;
        _blinkButton.Visible = isEye;
        Toggle(_onButton, motion.IsOn, "CREATURE_MOTION_ON", "CREATURE_MOTION_OFF", accent);
        Toggle(_blinkButton, motion.IsBlinking, "CREATURE_MOTION_BLINK_ON", "CREATURE_MOTION_BLINK_OFF", accent);
        _speedLabel.Text  = string.Format(Tr("CREATURE_MOTION_PACE"), Mathf.RoundToInt(motion.Speed * 100f));
        _amountLabel.Text = string.Format(Tr("CREATURE_MOTION_AMOUNT"), Mathf.RoundToInt(motion.Amount * 100f));
        _phaseLabel.Text  = string.Format(Tr("CREATURE_MOTION_PHASE"), motion.Phase.ToString("0.00", NUMBER));
        _speedSlider.SetValueNoSignal(motion.Speed);
        _amountSlider.SetValueNoSignal(motion.Amount);
        _phaseSlider.SetValueNoSignal(motion.Phase);
        RefreshChannels(hasPart && hasLoop && motion.IsOn ? picked : -1, part);
    }

    /*
      The channels of the model animation of the picked part, one slider each (0 off .. 250 %).
      The rows are only rebuilt when the part or its channels change: a refresh while a
      slider is dragged must not take it away. A channel is named by its bone or node in
      the model; a translation CREATURE_CHANNEL_<NAME> replaces the raw name where one exists.
    */
    private void RefreshChannels(int picked, CreaturePart part)
    {
        System.Collections.Generic.IReadOnlyList<string> names = picked >= 0 ? _channelsOf(picked) : System.Array.Empty<string>();
        string key = $"{picked}:{string.Join(",", names)}";
        if (key != _channelKey)
        {
            _channelKey = key;
            foreach (Node row in _channelRows.GetChildren())
            {
                _channelRows.RemoveChild(row);
                row.QueueFree();
            }
            _channelSliders.Clear();
            foreach (string name in names)
            {
                var label  = new Label { AutowrapMode = TextServer.AutowrapMode.Off, ClipText = true };
                label.AddThemeFontSizeOverride("font_size", 13);
                var slider = new HSlider { MinValue = 0, MaxValue = PartMotion.MAX_AMOUNT, Step = 0.05, FocusMode = Control.FocusModeEnum.None };
                slider.ValueChanged += value => ChannelChanged?.Invoke(name, (float)value);
                _channelRows.AddChild(label);
                _channelRows.AddChild(slider);
                _channelSliders.Add((name, label, slider));
            }
        }
        _channelRows.Visible = _channelSliders.Count > 1;      // a single channel is the whole motion, the switch above covers it
        foreach ((string name, Label label, HSlider slider) in _channelSliders)
        {
            string translationKey = $"CREATURE_CHANNEL_{name}".ToUpperInvariant();
            string shown          = Tr(translationKey);
            float  amount         = part.ChannelAmount(name);
            label.Text = $"{(shown == translationKey ? name : shown)}: {string.Format(Tr("CREATURE_UNIT_PERCENT"), Mathf.RoundToInt(amount * 100f))}";
            slider.SetValueNoSignal(amount);
        }
    }

    private void Toggle(Button button, bool isOn, string onKey, string offKey, Color accent)
    {
        button.Text = Tr(isOn ? onKey : offKey);
        button.SetPressedNoSignal(isOn);
        EditorStyle.ApplyButton(button, isOn ? accent : EditorStyle.RIM_COLOR);
    }
}
