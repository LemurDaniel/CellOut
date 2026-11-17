// NOTE:
//   What the techs of the city change in the world (models of tools/blender/models/city.py):
//   with Farming four fields lie in a ring around the headquarters, with Sailing a harbor
//   pier runs out from the shore nearest to it. Fields get a flat foundation (no trees grow
//   on them) but do not block the way; the pier stands in the water. The places follow from
//   the headquarters and the coast, so a saved city gets the same scenery back. It does not
//   process on its own: Society calls Update after the city.
//
//   var scenery = new CityScenery();
//   scenery.Setup(session, tribe, civ);
//   AddChild(scenery);
//   scenery.Update();

//
//   Chapters:
//     SETTINGS   The session, the tribe, the city and the models.
//     PLACE      Fields and pier once their tech is there, kept on the ground.
//

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CellOut;

public partial class CityScenery : Node3D
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   Setup      The session, the tribe and the city, before AddChild.
    //   Fields     The fields standing, for the tests.
    //   HasPier    Whether the pier stands.
    //

    private const string FIELD_MODEL = "res://assets/models/civ/civ.field.glb";
    private const string PIER_MODEL = "res://assets/models/civ/civ.pier.glb";
    private const float SCALE = 3f;                  // world units per model unit, in coast units (a field is about 7 x 5 units)
    private static readonly Color CLOTH = new(0.75f, 0.35f, 0.3f);    // shirt of the scarecrow, amphoras

    private WorldSession _session;
    private TribeCommander _tribe;
    private CivKeeper _civ;
    private readonly List<(Node3D Model, Vector2 Spot)> _fields = new();
    private Node3D _pier;
    private Vector2 _pierSpot;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player and coast.</param>
    /// <param name="tribe">(Required) The tribe with its headquarters.</param>
    /// <param name="civ">(Required) The city with its techs.</param>
    public void Setup(WorldSession session, TribeCommander tribe, CivKeeper civ)
    {
        _session = session;
        _tribe   = tribe;
        _civ     = civ;
    }

    public int Fields => _fields.Count;

    public bool HasPier => null != _pier;

    public override void _Ready()
    {
        TopLevel = true;
    }

    // ===========================================================================
    // ================================   PLACE   ================================
    // ===========================================================================
    //
    //   Update       Fields with Farming, the pier with Sailing, both kept on the ground.
    //   PlaceFields  FIELD_COUNT spots on a ring around the headquarters, on land, clear of buildings.
    //   PlacePier    The shortest way from the headquarters to the sea, the pier runs out along it.
    //   Keep         A model on the ground (or the sea) at its spot, scaled with the player.
    //

    private const int FIELD_COUNT = 4;
    private const float FIELD_RING = 0.62f;          // share of the build radius the fields lie on
    private const float FIELD_RADIUS = 1.45f;        // model units of a field from its middle to a corner
    private const int SHORE_RAYS = 32;
    private const float SHORE_STEP = 1f;             // coast units per step towards the sea

    /// <summary>One physics frame: the scenery of the researched techs, kept on the ground.</summary>
    public void Update()
    {
        Coast coast = _session.Coast;
        Hut   hall  = _tribe.Hall;
        if (null == coast || null == hall)
        {
            return;
        }
        if (0 == _fields.Count && _civ.Bonuses.FarmFoodPerMinute > 0f)
        {
            PlaceFields(coast, hall);
        }
        if (null == _pier && _civ.Bonuses.CanSail)
        {
            PlacePier(coast, hall);
        }
        float size = _session.Player.BodySize();
        foreach ((Node3D model, Vector2 spot) in _fields)
        {
            Keep(model, spot, coast.GroundHeight(spot) * size);
        }
        if (null != _pier)
        {
            Keep(_pier, _pierSpot, 0f);
        }
    }

    private void PlaceFields(Coast coast, Hut hall)
    {
        float unit   = coast.Unit;
        float ring   = Balance.Current.Tribe.Village.BuildRadius * unit * FIELD_RING;
        float radius = FIELD_RADIUS * SCALE * unit;
        for (int i = 0; i < SHORE_RAYS && _fields.Count < FIELD_COUNT; i++)
        {
            // Every other ray around the hall, the second pass fills in between
            float   angle = Mathf.Tau * ((i * 2) % SHORE_RAYS + i * 2 / SHORE_RAYS) / SHORE_RAYS;
            Vector2 spot  = hall.Position + Vector2.FromAngle(angle) * ring;
            bool    isFree = coast.IsOnLand(spot) && coast.LandDepth(spot) > radius
                && !_tribe.Huts.Any(hut => IsInstanceValid(hut) && hut.Position.DistanceTo(spot) < hut.BlockRadius + radius)
                && !_fields.Exists(field => field.Spot.DistanceTo(spot) < radius * 2f);
            if (!isFree)
            {
                continue;
            }
            if (!coast.HasPadAt(spot))
            {
                coast.AddPad(spot, radius);
            }
            Node3D field = PartModels.New(FIELD_MODEL, CLOTH);
            field.Rotation = new Vector3(0f, Plane3D.FacingToYaw((hall.Position - spot).Angle()), 0f);     // the gate faces the city
            AddChild(field);
            _fields.Add((field, spot));
        }
        Log.Verbose("CityScenery", $"{_fields.Count} field(s) around the headquarters.");
    }

    private void PlacePier(Coast coast, Hut hall)
    {
        float   step     = SHORE_STEP * coast.Unit;
        float   shortest = float.MaxValue;
        Vector2 shore    = Vector2.Zero;
        float   facing   = 0f;
        for (int i = 0; i < SHORE_RAYS; i++)
        {
            float   angle = Mathf.Tau * i / SHORE_RAYS;
            Vector2 way   = Vector2.FromAngle(angle);
            Vector2 point = hall.Position;
            for (float walked = 0f; walked < shortest; walked += step)
            {
                if (!coast.IsOnLand(point + way * step))
                {
                    shortest = walked;
                    shore    = point;
                    facing   = angle;
                    break;
                }
                point += way * step;
            }
        }
        if (float.MaxValue == shortest)
        {
            return;
        }
        _pierSpot = shore;
        _pier     = PartModels.New(PIER_MODEL, CLOTH);
        _pier.Rotation = new Vector3(0f, Plane3D.FacingToYaw(facing), 0f);
        AddChild(_pier);
        Log.Verbose("CityScenery", $"Pier at {shore}, {shortest:F0} units from the headquarters.");
    }

    private void Keep(Node3D model, Vector2 spot, float height)
    {
        model.GlobalPosition = Plane3D.To3D(spot, height - Plane3D.Drop(spot));
        model.Scale          = Vector3.One * SCALE * _session.Coast.Unit;
    }
}
