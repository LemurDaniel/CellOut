// NOTE:
//   The ship of the city: once the city knows Sailing (Civ/TechTree.cs), the chieftain who
//   walks into the sea sails instead of swimming. The ship (Civ/ShipModel.cs, sail and
//   hull band, funnel band and flag in the color of the chieftain) floats on the waterline under it, turns
//   with it and rocks on the waves; the speed comes from the techs (Player.SailFactor). The
//   retinue goes on board when the ship casts off (hidden, carried along) and steps ashore
//   around the chieftain when it lands, so the islands of the rivals are in reach of the
//   whole war party. It does not process on its own: Society calls Update after the city.
//
//   var ships = new ShipKeeper();
//   ships.Setup(session, pack, civ);
//   AddChild(ships);
//   ships.Update(dt);

//
//   Chapters:
//     SETTINGS   The session, the pack, the city and the ship model.
//     SAIL       Casting off, sailing, landing, per physics frame.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class ShipKeeper : Node3D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   CastOff / Landed   The ship left the shore or landed, with the retinue on board.
    //   Setup              The session, the pack and the city, before AddChild.
    //   IsSailing          The chieftain sails right now.
    //   Aboard             Members of the retinue on board.
    //   _Ready             Top level, the ship model (ShipModel) comes with the first cast off.
    //

    public event System.Action CastOff;
    public event System.Action Landed;

    private WorldSession _session;
    private PackKeeper _pack;
    private CivKeeper _civ;
    private Node3D _ship;
    private readonly List<Kin> _aboard = new();

    private Player Player => _session.Player;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player and coast.</param>
    /// <param name="pack">(Required) The pack: the retinue that goes on board.</param>
    /// <param name="civ">(Required) The city: whether and how fast it sails.</param>
    public void Setup(WorldSession session, PackKeeper pack, CivKeeper civ)
    {
        _session = session;
        _pack    = pack;
        _civ     = civ;
    }

    public bool IsSailing { get; private set; }

    public IReadOnlyList<Kin> Aboard => _aboard;

    public override void _Ready()
    {
        TopLevel = true;
    }

    // ===========================================================================
    // =================================   SAIL   ================================
    // ===========================================================================
    //
    //   Update     Sails while the city can and the chieftain is in the sea: the ship under it,
    //              the retinue on board; on land the ship is gone and the retinue steps ashore.
    //   Board      The retinue goes on board when the ship casts off.
    //   GoAshore   Every member on board steps ashore in a ring around the chieftain.
    //

    private const float ASHORE_RING = 1.6f;          // body sizes from the chieftain the retinue lands

    private float _time;

    /// <summary>One physics frame: sails while the city can and the chieftain is in the sea.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Update(float dt)
    {
        _time += dt;
        bool isSailing = _civ.Bonuses.CanSail && Player.IsLandAnimal && !Player.IsOnLand && !Player.IsDead;
        Player.SailFactor = isSailing ? _civ.Bonuses.ShipSpeed : 0f;
        if (isSailing != IsSailing)
        {
            IsSailing       = isSailing;
            Player.IsHidden = isSailing;     // the ship is the chieftain on the sea, the body would poke through its hull
            if (isSailing)
            {
                Board();
            }
            else
            {
                GoAshore();
            }
        }
        if (!IsSailing)
        {
            return;
        }
        ShipModel.Place(_ship, Player.GlobalPosition, Player.Facing, Player.BodySize(), _time);
        foreach (Kin kin in _aboard)
        {
            if (IsInstanceValid(kin))
            {
                kin.Position = Player.GlobalPosition;     // carried along under the deck
            }
        }
    }

    private void Board()
    {
        if (null == _ship)
        {
            _ship = ShipModel.New(Player.BodyColor);
            AddChild(_ship);
        }
        _ship.Visible = true;
        _aboard.Clear();
        foreach (Kin kin in _pack.Kin)
        {
            if (KinMode.Following == kin.Mode && !kin.IsQueuedForDeletion())
            {
                kin.IsHidden = true;
                _aboard.Add(kin);
            }
        }
        Log.Verbose("ShipKeeper", $"Cast off with {_aboard.Count} on board.");
        CastOff?.Invoke();
    }

    private void GoAshore()
    {
        float size = Player.BodySize();
        for (int i = 0; i < _aboard.Count; i++)
        {
            Kin kin = _aboard[i];
            if (!IsInstanceValid(kin))
            {
                continue;
            }
            Vector2 spot = Player.GlobalPosition + Vector2.FromAngle(Player.Facing + Mathf.Pi + (i - (_aboard.Count - 1) * 0.5f) * 0.6f) * size * ASHORE_RING;
            kin.Position = null != _session.Coast && _session.Coast.IsOnLand(spot) ? spot : Player.GlobalPosition;
            kin.IsHidden = false;
        }
        if (null != _ship)
        {
            _ship.Visible = false;
        }
        Log.Verbose("ShipKeeper", $"Landed, {_aboard.Count} stepped ashore.");
        _aboard.Clear();
        Landed?.Invoke();
    }
}
