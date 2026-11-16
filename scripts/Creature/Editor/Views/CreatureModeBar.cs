// NOTE:
//   The bar in the header of the creature editor: the four modes on the left (Build shows
//   parts and shape, Paint shows colors, skin, pattern and the spray can, Animate shows how
//   body and parts move, Sculpt shapes the clay of own parts) and undo / redo on the right. A view: the buttons raise events, Refresh shows the session.
//
//   var bar = new CreatureModeBar();
//   bar.Setup(session);
//   bar.ModePressed += mode => session.SetMode(mode);
//   frame.SetHeader(bar);

//
//   Chapters:
//     MODE BAR   Builds the mode switch and the undo buttons and keeps them in step with the session.
//

using Godot;

namespace CellOut;

public partial class CreatureModeBar : HBoxContainer
{
    // ===========================================================================
    // ==============================   MODE BAR   ===============================
    // ===========================================================================
    //
    //   ModePressed   Raised when a mode is clicked.
    //   UndoPressed   Raised by the undo button.
    //   RedoPressed   Raised by the redo button.
    //   Setup         The three mode pills on the left, undo and redo on the right.
    //   Refresh       Texts, the pressed mode, undo and redo only while a step lies there.
    //

    private static readonly Vector2 MODE_SIZE = new(132, 40);

    public event System.Action<CreatureEditorMode> ModePressed;
    public event System.Action UndoPressed;
    public event System.Action RedoPressed;

    private CreatureEditorSession _session;
    private Button _buildButton;
    private Button _paintButton;
    private Button _animateButton;
    private Button _sculptButton;
    private Button _undoButton;
    private Button _redoButton;

    /// <summary>Builds the bar, call once before it enters the tree.</summary>
    /// <param name="session">(Required) The sitting: mode, phase and the steps of its history.</param>
    public void Setup(CreatureEditorSession session)
    {
        _session            = session;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 8);

        _buildButton = CreatureEditorUi.NewPillButton(MODE_SIZE, 19);
        _paintButton = CreatureEditorUi.NewPillButton(MODE_SIZE, 19);
        _animateButton = CreatureEditorUi.NewPillButton(MODE_SIZE, 19);
        _buildButton.Pressed   += () => ModePressed?.Invoke(CreatureEditorMode.Build);
        _paintButton.Pressed   += () => ModePressed?.Invoke(CreatureEditorMode.Paint);
        _animateButton.Pressed += () => ModePressed?.Invoke(CreatureEditorMode.Animate);
        _sculptButton = CreatureEditorUi.NewPillButton(MODE_SIZE, 19);
        _sculptButton.Pressed += () => ModePressed?.Invoke(CreatureEditorMode.Sculpt);
        _undoButton = CreatureEditorUi.NewPillButton(new Vector2(0, 34), 14);
        _redoButton = CreatureEditorUi.NewPillButton(new Vector2(0, 34), 14);
        _undoButton.Pressed += () => UndoPressed?.Invoke();
        _redoButton.Pressed += () => RedoPressed?.Invoke();

        // The modes on the left where the title of the type choice stood, a spacer pushes undo and redo to the right
        AddChild(_buildButton);
        AddChild(_paintButton);
        AddChild(_animateButton);
        AddChild(_sculptButton);
        AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        AddChild(_undoButton);
        AddChild(_redoButton);
    }

    /// <summary>Shows mode and history of the session, hidden during the type choice.</summary>
    public void Refresh()
    {
        Visible = !_session.IsChoosingType;
        Color accent = CreatureEditorUi.TypeAccent(_session.Draft.Type);
        _buildButton.Text = Tr("CREATURE_MODE_BUILD");
        _paintButton.Text = Tr("CREATURE_MODE_PAINT");
        _undoButton.Text  = Tr("CREATURE_UNDO");
        _redoButton.Text  = Tr("CREATURE_REDO");
        _animateButton.Text = Tr("CREATURE_MODE_ANIMATE");
        _sculptButton.Text  = Tr("CREATURE_MODE_SCULPT");
        EditorStyle.ApplyButton(_sculptButton, CreatureEditorMode.Sculpt == _session.Mode ? accent : EditorStyle.RIM_COLOR);
        EditorStyle.ApplyButton(_buildButton, CreatureEditorMode.Build == _session.Mode ? accent : EditorStyle.RIM_COLOR);
        EditorStyle.ApplyButton(_paintButton, CreatureEditorMode.Paint == _session.Mode ? accent : EditorStyle.RIM_COLOR);
        EditorStyle.ApplyButton(_animateButton, CreatureEditorMode.Animate == _session.Mode ? accent : EditorStyle.RIM_COLOR);
        EditorStyle.ApplyButton(_undoButton, EditorStyle.RIM_COLOR);
        EditorStyle.ApplyButton(_redoButton, EditorStyle.RIM_COLOR);
        _undoButton.Disabled    = !_session.CanUndo;
        _redoButton.Disabled    = !_session.CanRedo;
        _undoButton.TooltipText = "Ctrl+Z";
        _redoButton.TooltipText = "Ctrl+Y";
    }
}
