// NOTE:
//   Temporary render script for the coast, removed after the visual check.

using Godot;

namespace CellOut;

public partial class RenderLand : Node
{
    private Main _main;
    private int _frame;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _main = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Main>();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, _main);
    }

    public override void _Process(double delta)
    {
        _frame++;
        Player player = _main.Player;
        if (null == player) return;
        if (2 == _frame) _main.StartGame(isInstant: true);
        if (10 == _frame)
        {
            _main.CheatTo(CheatTarget.FullCluster);
            _main.StartEvolve(isInstant: true);
            _main.CreatureEditor.ChooseType(CreatureType.Fish);
            _main.CreatureEditor.TryPlace(CreaturePartKind.CaudalFin, 0f);
            _main.CreatureEditor.TryPlace(CreaturePartKind.Eye, 0.85f);
            _main.CreatureEditor.TryPlace(CreaturePartKind.DorsalFin, 0.5f);
            _main.CreatureEditor.Finish();
            player.Invulnerable = 0f;
        }
        if (20 == _frame)
        {
            _main.CheatTo(CheatTarget.Coast);
            // Back out to sea a bit, the shore ahead in view
            player.Position -= _main.Coast.Normal * player.BodySize() * 9f;
        }
        if (_frame > 20 && _frame < 150)
        {
            player.Position += _main.Coast.Normal * player.BodySize() * 0.08f;
        }
        if (150 == _frame) _main.CheatTo(CheatTarget.LandAnimal);
        if (_frame > 150)
        {
            player.Position += _main.Coast.Normal.Rotated(0.5f) * player.BodySize() * 0.06f;
        }
    }
}
