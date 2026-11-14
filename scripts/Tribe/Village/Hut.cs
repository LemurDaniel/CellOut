// NOTE:
//   A hut of a tribe on the plane, built from a blueprint (HutDesign): its look is
//   HutVisual3D, its hit points come from the wall, the villagers it raises from the
//   roof (Raise, every VillagerSeconds, smoke rises meanwhile). Raiders hurt it (Hurt),
//   at zero health it collapses (Destroyed). The TribeCommander or the RivalTribe keeps
//   GroundHeight up to date (the ground scales with the player).
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
    //   SCALE           Units of the coast per unit of the hut model: a hut is a few bodies across.
    //   Size            (Required) World units per model unit (coast unit times SCALE).
    //   GroundHeight    (Required) Ground below in world units, kept up to date by its keeper.
    //   Design          The blueprint it was built from.
    //   Faction         0 the player's, else the rival tribe it belongs to.
    //   Hp / MaxHp      Hit points, the wall of the design.
    //   VillagersLeft   Villagers this hut still raises.
    //   Progress        0..1 of the next villager, 0 once the hut raised them all.
    //   Destroyed       Zero health: the hut is gone, its keeper forgets it.
    //   Setup           Blueprint and faction, before AddChild.
    //   Raise           Counts down to the next villager, true when one is born.
    //   Hurt            Raid damage, collapses at zero.
    //   _Process        Sits on the ground, smoke rises from the roof while a villager ripens.
    //

    /// <summary>Villager time from data/balance.json (tribe.village).</summary>
    private static VillageBalance Tune => Balance.Current.Tribe.Village;

    public const float SCALE = 2.4f;

    [Signal] public delegate void DestroyedEventHandler();

    public float Size { get; set; } = 14f;
    public float GroundHeight { get; set; }
    public HutDesign Design { get; } = new();
    public int Faction { get; private set; }
    public float Hp { get; private set; }
    public float MaxHp { get; private set; }
    public int VillagersLeft { get; private set; }

    public float Progress => VillagersLeft > 0 ? 1f - _raiseTimer / Tune.VillagerSeconds : 0f;

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
    public void Setup(HutDesign design, int faction)
    {
        Design.CopyFrom(design);
        Faction       = faction;
        MaxHp         = Design.Health;
        Hp            = MaxHp;
        VillagersLeft = Design.Villagers;
    }

    public override void _Ready()
    {
        _visual = new Node3D { Basis = new Basis(Vector3.Up, Rand.Angle()) };
        AddChild(_visual);
        var look = new HutVisual3D();
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
        Log.Verbose("Hut", $"Built at {Position}: {Design.Wall} wall, {Design.Roof} roof, {Design.Addons.Count} add-on(s), faction {Faction}.");
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
        float roofTop  = HutVisual3D.WALL_HEIGHT + HutVisual3D.ROOF_HEIGHT;
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
