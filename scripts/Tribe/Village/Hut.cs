// NOTE:
//   A building of a tribe on the plane, built from a blueprint (BuildingDesign): its
//   look is BuildingVisual3D, its hit points and the villagers it raises come from its
//   function (Raise, every VillagerSeconds, smoke rises meanwhile). Raiders hurt it
//   (Hurt), at zero health it collapses (Destroyed). The TribeCommander or the RivalTribe
//   keeps GroundHeight up to date (the ground scales with the player).
//
//   var hut = new Hut { Position = spot, Size = coast.Unit * Hut.SCALE, GroundHeight = ground };
//   hut.Setup(design, faction: 0);
//   AddChild(hut);
//   if (hut.Raise(dt)) pack.SpawnVillager(hut.Position);   // in the commander

//
//   Chapters:
//     HUT   Blueprint, hit points, the villagers it raises, smoke and the placement on the ground.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class Hut : Node2D
{
    // ===========================================================================
    // =================================   HUT   =================================
    // ===========================================================================
    //
    //   SCALE           Units of the coast per unit of the building model: a building is a few bodies across.
    //   RADIUS          Model units from the center that count as "at the building": half a room block.
    //   Size            (Required) World units per model unit (coast unit times SCALE).
    //   GroundHeight    (Required) Ground below in world units, kept up to date by its keeper.
    //   Turn            (Optional) Turn around the vertical axis in radians, a random one when not set.
    //   Design          The blueprint it was built from.
    //   Faction         0 the player's, else the rival tribe it belongs to.
    //   Hp / MaxHp      Hit points, from the function of the design.
    //   VillagersLeft   Villagers (or warriors, in a warrior hut) this building still raises.
    //   Progress        0..1 of the next villager, 0 once the hut raised them all.
    //   FoundationRadius   World units of flat ground it needs (Coast.AddPad).
    //   BlockRadius / ContactRadius   Nobody walks through it; how close counts as at it.
    //   BlockOn         Registers it as an obstacle on the coast.
    //   FitShare        How much of its size a new building may have between the others.
    //   Destroyed       Zero health: the hut is gone, its keeper forgets it.
    //   Setup           Blueprint and faction, before AddChild.
    //   Raise           Counts down to the next villager, true when one is born.
    //   Hurt            Raid damage, collapses at zero.
    //   _Process        Sits on the ground, smoke rises from the roof while a villager ripens.
    //

    /// <summary>Villager time from data/balance.json (tribe.village).</summary>
    private static VillageBalance Tune => Balance.Current.Tribe.Village;

    public const float SCALE = 2.4f;
    public const float RADIUS = 1f;

    [Signal] public delegate void DestroyedEventHandler();

    public float Size { get; set; } = 14f;
    public float GroundHeight { get; set; }
    public float? Turn { get; set; }
    public BuildingDesign Design { get; } = new(BuildingKind.Dwelling);
    public int Faction { get; private set; }
    public float Hp { get; private set; }
    public float MaxHp { get; private set; }
    public int VillagersLeft { get; private set; }

    public float Progress => VillagersLeft > 0 ? 1f - _raiseTimer / Tune.VillagerSeconds : 0f;

    /// <summary>World units around the middle the ground is leveled for it (Coast.AddPad): every block plus a little yard.</summary>
    public float FoundationRadius => (Mathf.Max(Design.FootprintRadius, MIN_FOOTPRINT) * BuildingVisual3D.CELL + FOUNDATION_YARD) * Size;

    private const float MIN_FOOTPRINT = 2f;          // cells, an empty blueprint still gets a small yard
    private const float FOUNDATION_YARD = 0.3f;      // model units of flat ground around the outermost block
    private const float BLOCK_SHARE = 0.8f;          // the round blocker covers this much of the footprint circle: the square walls, not the corners
    private const float CONTACT_REACH = 1.3f;        // a body this far out (in blocker radii, at least a room block) is at the building

    /// <summary>World units of the round footprint nobody walks through (Coast.AddBlocker).</summary>
    public float BlockRadius => Mathf.Max(Design.FootprintRadius, MIN_FOOTPRINT) * BuildingVisual3D.CELL * BLOCK_SHARE * Size;

    /// <summary>World units from the middle a body counts as at the building: raiders hurt it, razers tear it down, from there.</summary>
    public float ContactRadius => Mathf.Max(BlockRadius, Size * RADIUS) * CONTACT_REACH;

    private const float FIT_MIN = 0.55f;             // a building squeezed between others shrinks to at most this share
    private const float FIT_GAP = 0.5f;              // world units it keeps to the walls of its neighbors, in coast units

    /// <summary>The share of its full size a new building may have so its walls stay clear of the buildings around (the headquarters on the old fire).</summary>
    /// <param name="design">(Required) The blueprint of the new building.</param>
    /// <param name="size">(Required) Its full Size (coast unit times SCALE).</param>
    /// <param name="spot">(Required) Plane point of its middle.</param>
    /// <param name="others">(Required) The buildings standing around.</param>
    /// <param name="unit">(Required) World units of one coast unit.</param>
    /// <returns>1 when it fits, down to FIT_MIN.</returns>
    public static float FitShare(BuildingDesign design, float size, Vector2 spot, IEnumerable<Hut> others, float unit)
    {
        float full = Mathf.Max(design.FootprintRadius, MIN_FOOTPRINT) * BuildingVisual3D.CELL * BLOCK_SHARE * size;
        float room = float.MaxValue;
        foreach (Hut other in others)
        {
            if (IsInstanceValid(other) && !other.IsQueuedForDeletion())
            {
                room = Mathf.Min(room, other.Position.DistanceTo(spot) - other.BlockRadius - FIT_GAP * unit);
            }
        }
        return Mathf.Clamp(room / Mathf.Max(full, 0.001f), FIT_MIN, 1f);
    }

    /// <summary>Blocks the way on the coast from now on, until the building is gone.</summary>
    /// <param name="coast">(Required) The coast its keeper builds on.</param>
    public void BlockOn(Coast coast)
    {
        Coast.Blocker blocker = coast.AddBlocker(Position, BlockRadius);
        TreeExiting += () =>
        {
            // When the game shuts down the coast may be gone before its buildings
            if (IsInstanceValid(coast))
            {
                coast.RemoveBlocker(blocker);
            }
        };
    }

    private const int PUFFS = 3;
    private const float PUFF_RISE = 0.9f;            // in model units, how high a puff climbs over the roof
    private const float PUFF_SPEED = 0.35f;          // climbs per second
    private static readonly Color SMOKE_COLOR = new(0.85f, 0.85f, 0.88f, 0.55f);

    private readonly List<MeshInstance3D> _puffs = new();
    private Node3D _visual;
    private float _raiseTimer = Tune.VillagerSeconds;
    private float _time;

    /// <summary>Takes the blueprint and the owner, call before AddChild.</summary>
    /// <param name="design">(Required) The blueprint, copied.</param>
    /// <param name="faction">(Required) 0 for the player's tribe, else the rival tribe.</param>
    public void Setup(BuildingDesign design, int faction)
    {
        Design.CopyFrom(design);
        Faction       = faction;
        MaxHp         = Design.Health;
        Hp            = MaxHp;
        VillagersLeft = Design.Villagers + Design.Warriors;
    }

    public override void _Ready()
    {
        Turn  ??= Rand.Angle();     // kept, a save brings the building back as it stood
        _visual = new Node3D { Basis = new Basis(Vector3.Up, Turn.Value) };
        AddChild(_visual);
        var look = new BuildingVisual3D();
        look.Build(Design);
        _visual.AddChild(look);

        // Smoke puffs over the roof tip, sized in _Process while a villager ripens
        StandardMaterial3D smoke = CellVisual3D.Material(SMOKE_COLOR, 0f, 1f);
        for (int i = 0; i < PUFFS; i++)
        {
            MeshInstance3D puff = CellVisual3D.AddMesh(_visual, CellVisual3D.SphereMesh(true), smoke, Vector3.Zero, Vector3.One * 0.001f);
            puff.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _puffs.Add(puff);
        }
        Place();
        Log.Verbose("Hut", $"Built at {Position}: {Design.Kind} of {Design.Blocks.Count} block(s), faction {Faction}.");
    }

    /// <summary>Counts down to the next villager.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    /// <returns>True once a villager is born, at most Design.Villagers times.</returns>
    public bool Raise(float dt)
    {
        if (VillagersLeft <= 0)
        {
            return false;
        }
        _raiseTimer -= dt;
        if (_raiseTimer > 0f)
        {
            return false;
        }
        _raiseTimer = Tune.VillagerSeconds;
        VillagersLeft--;
        return true;
    }

    /// <summary>Raid damage, the hut collapses at zero health.</summary>
    /// <param name="amount">(Required) Damage in hit points, after the defense of the village.</param>
    /// <returns>True when it collapsed.</returns>
    public bool Hurt(float amount)
    {
        if (Hp <= 0f || IsQueuedForDeletion())
        {
            return false;
        }
        Hp -= amount;
        if (Hp > 0f)
        {
            return false;
        }
        Log.Verbose("Hut", $"Hut at {Position} collapsed.");
        EmitSignal(SignalName.Destroyed);
        QueueFree();
        return true;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        Place();

        // Each puff climbs on its own phase and fades out at the top, bigger the riper the villager
        float progress = Progress;
        float roofTop  = Design.Height * BuildingVisual3D.CELL;
        for (int i = 0; i < _puffs.Count; i++)
        {
            float phase = Mathf.PosMod(_time * PUFF_SPEED + (float)i / PUFFS, 1f);
            float size  = progress > 0f ? (0.1f + 0.18f * progress) * (1f - phase * phase) : 0.001f;
            _puffs[i].Position = new Vector3(Mathf.Sin(phase * 6f + i) * 0.1f, roofTop + phase * PUFF_RISE, Mathf.Cos(phase * 5f + i) * 0.1f);
            _puffs[i].Scale    = Vector3.One * size;
        }
    }

    /// <summary>On the ground at its plane point, scaled, dropped with the planet.</summary>
    private void Place()
    {
        _visual.GlobalPosition = Plane3D.To3D(GlobalPosition, GroundHeight - Plane3D.Drop(GlobalPosition));
        _visual.Scale          = Vector3.One * Size;
    }
}
