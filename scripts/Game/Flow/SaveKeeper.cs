// NOTE:
//   Saves the running game into the slot of its planet (Core/Save/SaveSlots.cs, one JSON per
//   planet) and brings a saved game back. Saves on its own a few seconds after every new
//   stage, every AUTOSAVE_SECONDS of play and when the window closes; F5 saves at once. Never
//   while the game is busy (menu, evolution, cutscene, an editor) or the player is dead.
//   A short note bottom center confirms a save. Restoring runs right after the birth of the
//   protocell (Main.OnStartFinished): player, coast, pack, tribe, neighbors in this order.
//
//   var saves = new SaveKeeper { Main = this };
//   AddChild(saves);
//   saves.Begin(startFlow.PendingSave);      // when the protocell is born, null for a new game
//   SaveGame game = saves.Capture();          // e.g. the smoke test

//
//   Chapters:
//     SAVE      When to save, capturing the game, the note on screen.
//     RESTORE   A saved game after the start.
//

using System.Linq;
using Godot;

namespace CellOut;

public partial class SaveKeeper : Node
{
    // ===========================================================================
    // =================================   SAVE   ================================
    // ===========================================================================
    //
    //   Main                 (Required) The game that is saved and restored.
    //   _Ready               Listens to the stages, builds the note.
    //   _Process             Play time, the autosave after a stage and on the clock.
    //   _UnhandledKeyInput   F5 saves at once.
    //   _Notification        Saves when the window closes.
    //   CanSave              The game runs calmly: no menu, evolution, cutscene, editor, death.
    //   SaveNow              Captures and writes, shows the note.
    //   Capture              The whole game as plain save data.
    //

    private const float AUTOSAVE_SECONDS = 180f;
    private const float AFTER_STAGE_SECONDS = 4f;    // a new stage saves once its cutscene is over
    private const float NOTE_SECONDS = 1.6f;

    public Main Main { get; set; }

    private float _playSeconds;
    private float _sinceSave;
    private float _stageSaveIn = -1f;
    private Label _note;

    private Player Player => Main.Player;

    public override void _Ready()
    {
        Main.Player.StageChanged += _ => _stageSaveIn = AFTER_STAGE_SECONDS;

        var layer = new CanvasLayer { Layer = 40 };
        AddChild(layer);
        _note = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, Visible = false, HorizontalAlignment = HorizontalAlignment.Center };
        _note.AddThemeFontSizeOverride("font_size", 18);
        _note.AddThemeColorOverride("font_outline_color", EditorStyle.OUTLINE);
        _note.AddThemeConstantOverride("outline_size", 5);
        _note.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        _note.Position += new Vector2(-120f, -130f);
        _note.Size      = new Vector2(240f, 30f);
        layer.AddChild(_note);
    }

    public override void _Process(double delta)
    {
        if (!CanSave)
        {
            return;
        }
        float dt = (float)delta;
        _playSeconds += dt;
        _sinceSave   += dt;
        if (_stageSaveIn >= 0f)
        {
            _stageSaveIn -= dt;
            if (_stageSaveIn < 0f)
            {
                SaveNow();
            }
        }
        if (_sinceSave > AUTOSAVE_SECONDS)
        {
            SaveNow();
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F5 })
        {
            SaveNow();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Notification(int what)
    {
        if (NotificationWMCloseRequest == what)
        {
            SaveNow();
        }
    }

    private bool CanSave => null != Main && !Main.IsBusy && !Player.IsDead && !Player.IsHidden;

    /// <summary>Captures the game and writes it into the slot of its planet, when the game runs calmly.</summary>
    /// <returns>True when it was written.</returns>
    public bool SaveNow()
    {
        if (!CanSave)
        {
            return false;
        }
        _sinceSave   = 0f;
        _stageSaveIn = -1f;
        bool isWritten = SaveSlots.Write(Capture());
        if (isWritten)
        {
            ShowNote();
        }
        return isWritten;
    }

    /// <summary>The whole game as plain save data: meta, coast (once it exists), player, society (from the land animal on).</summary>
    public SaveGame Capture()
    {
        (float levelStart, float plant, float meat) = Player.SaveTotals;
        return new SaveGame
        {
            Meta = new SaveMeta
            {
                Planet      = Planets.Current.Key,
                Stage       = Player.Stage,
                SavedAt     = System.DateTime.Now.ToString("s"),
                PlaySeconds = _playSeconds,
            },
            World  = Main.Coast?.Save(),
            Player = new SavePlayer
            {
                Stage           = Player.Stage,
                Level           = Player.Level,
                Biomass         = Player.Biomass,
                NextLevelAt     = Player.NextLevelAt,
                LevelStart      = levelStart,
                EvolutionPoints = Player.EvolutionPoints,
                DietPlant       = plant,
                DietMeat        = meat,
                Hp              = Player.Hp,
                Mutations       = Player.Mutations.Select(mutation => mutation.ToString()).ToList(),
                CellCount       = Player.CellCount,
                EvolvedAt       = Player.EvolvedAt,
                Position        = SaveData.Of(Player.Position),
                Facing          = Player.Facing,
                Cell            = Player.Design.Save(),
                Creature        = Player.IsOrganism ? Player.Creature.Save() : null,
            },
            Society = Player.IsLandAnimal ? new SaveSociety
            {
                Pack   = Main.Pack.Save(),
                Tribe  = Player.IsTribal ? Main.Tribe.Save() : null,
                Rivals = Main.Rivals.Save(),
            } : null,
        };
    }

    private void ShowNote()
    {
        _note.Text     = Tr("SAVE_DONE");
        _note.Visible  = true;
        _note.Modulate = Colors.White;
        Tween fade = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        fade.TweenInterval(NOTE_SECONDS);
        fade.TweenProperty(_note, "modulate:a", 0f, 0.4f);
        fade.TweenCallback(Callable.From(() => _note.Visible = false));
    }

    // ===========================================================================
    // ================================   RESTORE   ==============================
    // ===========================================================================
    //
    //   Begin     The protocell is born: a saved game takes over, none starts fresh.
    //   Restore   Player, coast, pack, tribe, neighbors in this order, then the play time.
    //

    /// <summary>The protocell is born: restores a saved game, if any.</summary>
    /// <param name="save">(Optional) The game to continue, null for a new game.</param>
    public void Begin(SaveGame save)
    {
        _playSeconds = 0f;
        _sinceSave   = 0f;
        _stageSaveIn = -1f;
        if (null != save)
        {
            Restore(save);
        }
    }

    /// <summary>Takes over a saved game at once, without cutscenes or banners.</summary>
    /// <param name="save">(Required) The saved game, its planet is current already.</param>
    public void Restore(SaveGame save)
    {
        Player.Restore(save.Player);
        if (null != save.World)
        {
            Main.CoastKeeper.RestoreCoast(save.World);
        }
        if (null != save.Society && null != Main.Coast)
        {
            Main.Pack.Restore(save.Society.Pack);
            if (null != save.Society.Tribe)
            {
                Main.Tribe.Restore(save.Society.Tribe);
            }
            Main.Rivals.Restore(save.Society.Rivals);
        }
        _playSeconds = (float)save.Meta.PlaySeconds;
        Log.Verbose("SaveKeeper", $"Game of {save.Meta.Planet} restored at stage {save.Player.Stage}.");
    }
}
