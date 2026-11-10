// NOTE:
//   Footer of the creature editor: the info card (name and description of the part in
//   focus, else of the body type), the size slider of the picked part, the points badge
//   and the done button. A view: slider and button raise events, the Refresh calls show
//   the session. The badge pops when the points change, the done button once all are spent.
//
//   var footer = new CreatureFooter();
//   footer.Setup(session);
//   footer.DonePressed += Finish;
//   frame.AddRow(footer);
//   footer.Refresh();
//   footer.RefreshInfo(palette.Hovered);

//
//   Chapters:
//     BUILD FOOTER     Info card, size slider, points badge and the done button.
//     REFRESH FOOTER   Points and done button, size slider, info card.
//

using Godot;

namespace CellOut;

public partial class CreatureFooter : HBoxContainer
{
    // ===========================================================================
    // ============================   BUILD FOOTER   =============================
    // ===========================================================================
    //
    //   PartSizeChanged  Raised when the size slider is moved (new size).
    //   DonePressed      Raised when the done button is clicked.
    //   Setup            Info card, size slider of the picked part, points badge and the done button.
    //

    public event System.Action<float> PartSizeChanged;
    public event System.Action DonePressed;

    private CreatureEditorSession _session;
    private Label _infoName;
    private Label _infoText;
    private HBoxContainer _sizeRow;
    private Label _sizeLabel;
    private HSlider _sizeSlider;
    private Label _sizeValue;
    private PanelContainer _pointsBadge;
    private Label _pointsLabel;
    private Button _doneButton;

    /// <summary>Builds the footer, call once before it enters the tree.</summary>
    /// <param name="session">(Required) The sitting: points, phase, selection and the picked part.</param>
    public void Setup(CreatureEditorSession session)
    {
        _session = session;
        AddThemeConstantOverride("separation", 14);

        // Info card: name in the type color, description below, fixed height so nothing jumps
        var card = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize   = new Vector2(0f, 62f),
            MouseFilter         = MouseFilterEnum.Ignore,
        };
        card.AddThemeConstantOverride("separation", 0);
        AddChild(card);
        _infoName = CreatureEditorUi.NewLabel(20);
        _infoText = CreatureEditorUi.NewLabel(14);
        _infoText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        card.AddChild(_infoName);
        card.AddChild(_infoText);

        // Size slider of the picked part, only shown while one is picked
        _sizeRow = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter, Visible = false };
        _sizeRow.AddThemeConstantOverride("separation", 10);
        AddChild(_sizeRow);
        _sizeLabel  = CreatureEditorUi.NewLabel(17);
        _sizeValue  = CreatureEditorUi.NewLabel(17);
        _sizeValue.CustomMinimumSize = new Vector2(64f, 0f);
        _sizeSlider = new HSlider
        {
            MinValue          = CreatureDesign.MIN_PART_SIZE,
            MaxValue          = CreatureDesign.MAX_PART_SIZE,
            Step              = 0.05,
            Value             = 1.0,
            CustomMinimumSize = new Vector2(180f, 28f),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            FocusMode         = FocusModeEnum.None,
        };
        _sizeSlider.ValueChanged += value => PartSizeChanged?.Invoke((float)value);
        _sizeRow.AddChild(_sizeLabel);
        _sizeRow.AddChild(_sizeSlider);
        _sizeRow.AddChild(_sizeValue);

        // Points badge and done button
        _pointsBadge = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        _pointsBadge.AddThemeStyleboxOverride("panel", EditorStyle.Pill(new Color(0.08f, 0.2f, 0.2f), EditorStyle.DIET_COLORS[DietClass.Omnivore], 0.4f));
        _pointsLabel = CreatureEditorUi.NewLabel(17);
        _pointsLabel.AddThemeColorOverride("font_color", EditorStyle.DIET_COLORS[DietClass.Omnivore].Lightened(0.4f));
        _pointsBadge.AddChild(_pointsLabel);
        AddChild(_pointsBadge);

        _doneButton = CreatureEditorUi.NewPillButton(new Vector2(150, 48), 20);
        _doneButton.Pressed      += () => DonePressed?.Invoke();
        _doneButton.MouseEntered += () => EditorStyle.ScaleTo(_doneButton, 1.06f);
        _doneButton.MouseExited  += () => EditorStyle.ScaleTo(_doneButton, 1f);
        AddChild(_doneButton);
    }

    // ===========================================================================
    // ===========================   REFRESH FOOTER   ============================
    // ===========================================================================
    //
    //   ForgetPoints   A new sitting: the first points shown do not pop the badge.
    //   Refresh        Points badge and the done button.
    //   RefreshSize    Size slider of the picked part.
    //   RefreshInfo    Name and description of the part in focus, else of the body type.
    //

    private int _lastPoints = -1;

    /// <summary>Forgets the points shown last, so the badge does not pop when a sitting begins.</summary>
    public void ForgetPoints()
    {
        _lastPoints = -1;
    }

    /// <summary>Updates the points and the done button, hidden during the type choice.</summary>
    public void Refresh()
    {
        int   points = _session.Points;
        Color accent = CreatureEditorUi.TypeAccent(_session.Draft.Type);
        _pointsLabel.Text   = string.Format(Tr("EDITOR_POINTS"), points);
        _doneButton.Text    = Tr("EDITOR_DONE");
        _doneButton.Visible = !_session.IsChoosingType;

        // Points changed: the badge pops, all spent: the done button invites
        if (_lastPoints >= 0 && points != _lastPoints)
        {
            EditorStyle.Punch(_pointsBadge, 1.15f);
            if (0 == points)
            {
                EditorStyle.Punch(_doneButton, 1.12f);
            }
        }
        _lastPoints = points;
        EditorStyle.ApplyButton(_doneButton, accent);
    }

    /// <summary>Slider and percent of the picked part, hidden without one.</summary>
    public void RefreshSize()
    {
        CreatureDesign draft   = _session.Draft;
        int            picked  = _session.PickedPart;
        bool           isShown = !_session.IsChoosingType && picked >= 0 && picked < draft.Parts.Count;
        _sizeRow.Visible = isShown;
        if (!isShown)
        {
            return;
        }
        float size = draft.Parts[picked].Size;
        _sizeLabel.Text = Tr("CREATURE_PART_SIZE");
        _sizeValue.Text = string.Format(Tr("CREATURE_UNIT_PERCENT"), Mathf.RoundToInt(size * 100f));
        _sizeSlider.SetValueNoSignal(size);
    }

    /// <summary>Fills the info card.</summary>
    /// <param name="focused">(Required) The dragged or hovered part, null when there is none: the selection of the session counts then.</param>
    /// <example><code>footer.RefreshInfo(ghost.Dragged ?? palette.Hovered);</code></example>
    public void RefreshInfo(CreaturePartInfo focused)
    {
        // Dragged tile first, then the hovered one, then the palette selection, then the picked part on the body
        CreatureDesign   draft  = _session.Draft;
        int              picked = _session.PickedPart;
        CreaturePartInfo info   = focused
            ?? (null != _session.Selected ? CreatureCatalog.Get(_session.Selected.Value) : null)
            ?? (picked >= 0 && picked < draft.Parts.Count ? draft.Parts[picked].Info : null);
        if (_session.IsChoosingType)
        {
            _infoName.Text = "";
            _infoText.Text = "";
            return;
        }
        if (_session.IsSpraying)
        {
            _infoName.Text = Tr("CREATURE_SPRAY");
            _infoText.Text = Tr("CREATURE_SPRAY_HINT");
            _infoName.AddThemeColorOverride("font_color", _session.SprayColor.Lightened(0.3f));
            return;
        }
        if (null == info)
        {
            _infoName.Text = Tr(CreatureEditorUi.TypeKey(draft.Type, "NAME"));
            _infoText.Text = Tr(CreatureEditorUi.TypeKey(draft.Type, "DESC"));
            _infoName.AddThemeColorOverride("font_color", CreatureEditorUi.TypeAccent(draft.Type));
            return;
        }
        _infoName.Text = Tr(info.NameKey);
        _infoText.Text = Tr(info.DescriptionKey);
        _infoName.AddThemeColorOverride("font_color", CreatureEditorUi.TypeAccent(draft.Type).Lightened(0.2f));
    }
}
