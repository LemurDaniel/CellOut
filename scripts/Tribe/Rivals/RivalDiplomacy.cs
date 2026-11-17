// NOTE:
//   The goal of the tribe along the path locked at the founding (TribeWays): every tribe
//   an ally (pacifist), every tribe respecting the player (territorial) or every tribe
//   defeated (warlike). The chieftain and its retinue get there themselves by courting,
//   displaying and hunting (PackEncounter); this only counts the progress for the panel
//   and shows the banner once the path is won. Society owns it, Update runs after the
//   RivalKeeper.
//
//   var diplomacy = new RivalDiplomacy();
//   diplomacy.Setup(rivals, tribe);
//   diplomacy.BannerRequested += Banner;
//   AddChild(diplomacy);
//   diplomacy.Update();

//
//   Chapters:
//     VICTORY    Progress of the path and the banner once it is won.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class RivalDiplomacy : Node
{
    // ===========================================================================
    // ===============================   VICTORY   ===============================
    // ===========================================================================
    //
    //   BannerRequested   A banner is due: title key, text key and its arguments.
    //   Setup             The rivals and the tribe, before AddChild.
    //   IsWon             The path reached its goal, the banner showed.
    //   Progress          Rivals done and in total for the path, for the panel.
    //   Share             0..1: how far the goal of the path is, for the progress ring.
    //   Update            Checks the goal once per physics frame from the tribe on.
    //   Standings         Relation, respect and defeat of every rival.
    //

    public event System.Action<string, string, object[]> BannerRequested;

    private RivalKeeper _rivals;
    private TribeCommander _tribe;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="rivals">(Required) The neighbor tribes.</param>
    /// <param name="tribe">(Required) The tribe with its path.</param>
    public void Setup(RivalKeeper rivals, TribeCommander tribe)
    {
        _rivals = rivals;
        _tribe  = tribe;
    }

    public bool IsWon { get; private set; }

    /// <summary>Rivals done and in total for the path of the tribe.</summary>
    public (int Done, int Total) Progress => TribeWays.Progress(_tribe.Path, Standings());

    /// <summary>0..1: how far the goal of the path is (TribeWays.GoalShare), for the progress ring.</summary>
    public float Share => TribeWays.GoalShare(_tribe.Path, Standings());

    /// <summary>Shows the banner of the path once its goal is reached.</summary>
    public void Update()
    {
        if (IsWon || !_tribe.IsActive || !TribeWays.IsWon(_tribe.Path, Standings()))
        {
            return;
        }
        IsWon = true;
        Log.Verbose("RivalDiplomacy", $"The {_tribe.Path} path is won.");
        string path = _tribe.Path.ToString().ToUpperInvariant();
        BannerRequested?.Invoke($"BANNER_WON_{path}_TITLE", $"BANNER_WON_{path}_TEXT", System.Array.Empty<object>());
    }

    private IEnumerable<RivalStanding> Standings()
    {
        return _rivals.Rivals.Select(rival => new RivalStanding(rival.Relation, rival.Respect, rival.IsDefeated));
    }
}
