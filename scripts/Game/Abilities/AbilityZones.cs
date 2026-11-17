// NOTE:
//   Lasting zones the cell abilities leave in the water: poison clouds of a toxin gland
//   (they hurt and slow the other side) and slime patches of a slime gland (they slow
//   the other side). A zone belongs to a side, the player or the NPC cells, and never
//   touches its own side. Drawn as drifting puffs and glossy flat blobs, no rings.
//   AbilityResolver.cs owns it: Add, then Advance and Affect once per frame.
//
//   var zones = new AbilityZones();
//   AddChild(zones);
//   zones.Add(new AbilityZones.Zone(ZoneKind.Poison, position, radius, life, damagePerSecond, slow, isPlayerSide: true));
//   zones.Advance(dt);
//   zones.Affect(body, isPlayerSide: false, dt, out float damage, out float slow);

//
//   Chapters:
//     TYPES     The kinds of zones and one zone.
//     BUILD     Puff and patch multimeshes.
//     ZONES     Adding, aging, what a body inside suffers, drawing.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

// ===========================================================================
// ================================   TYPES   ================================
// ===========================================================================
//
//   ZoneKind   Poison (cloud) or slime (patch).
//

public enum ZoneKind { Poison, Slime }

public partial class AbilityZones : Node3D
{
    /// <summary>One zone on the plane.</summary>
    /// <param name="Kind">Poison cloud or slime patch.</param>
    /// <param name="Position">Center on the plane.</param>
    /// <param name="Radius">Radius on the plane.</param>
    /// <param name="Life">Seconds it stays.</param>
    /// <param name="Damage">Damage per second to the other side, already scaled for the owner.</param>
    /// <param name="Slow">Share of the speed the other side keeps inside, 1 = no slow.</param>
    /// <param name="IsPlayerSide">Laid by the player: only NPC cells suffer, else only the player.</param>
    public record struct Zone(ZoneKind Kind, Vector2 Position, float Radius, float Life, float Damage, float Slow, bool IsPlayerSide)
    {
        public float Age { get; set; }
        public float Seed { get; init; }
    }

    // ===========================================================================
    // ================================   BUILD   ================================
    // ===========================================================================
    //
    //   _Ready   1. Puffs     Soft discs facing the camera, several per cloud.
    //            2. Patches   Glossy flat discs lying on the plane.
    //

    private const int MAX_ZONES = 140;
    private const int PUFFS_PER_CLOUD = 7;
    private const int TEXTURE_SIZE = 32;

    private MultiMesh _puffs;
    private MultiMesh _patches;

    public override void _Ready()
    {
        // ---------------------------------------------------------------------------
        // Puffs
        //   Soft discs facing the camera, several per cloud.
        // ---------------------------------------------------------------------------

        _puffs = AddLayer(new QuadMesh { Size = Vector2.One }, MAX_ZONES * PUFFS_PER_CLOUD, true);

        // ---------------------------------------------------------------------------
        // Patches
        //   Glossy flat discs lying on the plane.
        // ---------------------------------------------------------------------------

        _patches = AddLayer(new PlaneMesh { Size = Vector2.One }, MAX_ZONES, false);
    }

    /// <summary>A multimesh of discs with a soft radial texture and vertex colors.</summary>
    private MultiMesh AddLayer(Mesh mesh, int count, bool isBillboard)
    {
        var texture = new GradientTexture2D
        {
            Gradient = new Gradient
            {
                Colors  = new[] { Colors.White, new Color(1f, 1f, 1f, 0.8f), new Color(1f, 1f, 1f, 0f) },
                Offsets = new[] { 0f, 0.55f, 1f },
            },
            Fill     = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo   = new Vector2(1f, 0.5f),
            Width    = TEXTURE_SIZE,
            Height   = TEXTURE_SIZE,
        };
        var material = new StandardMaterial3D
        {
            ShadingMode            = isBillboard ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
            Transparency           = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode          = isBillboard ? BaseMaterial3D.BillboardModeEnum.Enabled : BaseMaterial3D.BillboardModeEnum.Disabled,
            BillboardKeepScale     = true,
            AlbedoTexture          = texture,
            VertexColorUseAsAlbedo = true,
            Roughness              = 0.15f,
            MetallicSpecular       = 0.8f,
        };
        var multimesh = new MultiMesh
        {
            TransformFormat      = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors            = true,
            Mesh                 = mesh,
            InstanceCount        = count,
            VisibleInstanceCount = 0,
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh        = multimesh,
            MaterialOverride = material,
            CastShadow       = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        return multimesh;
    }

    // ===========================================================================
    // ================================   ZONES   ================================
    // ===========================================================================
    //
    //   Add       A new zone, the oldest goes when MAX_ZONES are reached.
    //   Clear     Removes every zone (organism, fresh start).
    //   Advance   Ages the zones and draws them: clouds swell and thin out, patches dry up.
    //   Affect    Damage per second and slow a body of a side suffers from every zone it is in.
    //

    private static readonly Color POISON_COLOR = new(0.62f, 0.95f, 0.3f, 0.32f);
    private static readonly Color SLIME_COLOR = new(0.85f, 0.95f, 0.55f, 0.5f);
    private const float PATCH_SINK = 0.02f;       // a patch lies this far below the plane, in its radius

    private readonly List<Zone> _zones = new();
    private float _time;

    /// <summary>A new zone, the oldest goes once MAX_ZONES exist.</summary>
    public void Add(Zone zone)
    {
        if (_zones.Count >= MAX_ZONES)
        {
            _zones.RemoveAt(0);
        }
        _zones.Add(zone with { Seed = GD.Randf() * 100f });
    }

    /// <summary>Removes every zone at once.</summary>
    public void Clear()
    {
        _zones.Clear();
    }

    /// <summary>Ages every zone and draws the living ones.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Advance(float dt)
    {
        _time += dt;
        int puffs   = 0;
        int patches = 0;
        for (int i = _zones.Count - 1; i >= 0; i--)
        {
            Zone zone = _zones[i];
            zone.Age += dt;
            if (zone.Age >= zone.Life)
            {
                _zones.RemoveAt(i);
                continue;
            }
            _zones[i] = zone;
            float t    = zone.Age / zone.Life;
            float fade = Mathf.Min(t * 6f, 1f) * (1f - t * t);

            // Clouds: puffs swirl slowly around the center, swelling as they thin out
            if (ZoneKind.Poison == zone.Kind)
            {
                for (int k = 0; k < PUFFS_PER_CLOUD; k++)
                {
                    float   angle  = zone.Seed + k * Mathf.Tau / PUFFS_PER_CLOUD + _time * 0.4f * (k % 2 == 0 ? 1f : -1f);
                    float   spread = zone.Radius * (0.25f + 0.45f * ((k * 37 % 7) / 7f));
                    Vector2 spot   = zone.Position + Vector2.FromAngle(angle) * spread;
                    float   size   = zone.Radius * (0.9f + 0.6f * t) * (0.7f + 0.3f * Mathf.Sin(zone.Seed + k));
                    float   height = zone.Radius * 0.15f * Mathf.Sin(_time * 0.8f + k);
                    _puffs.SetInstanceTransform(puffs, new Transform3D(Basis.FromScale(Vector3.One * size), Plane3D.To3D(spot, height)));
                    _puffs.SetInstanceColor(puffs, new Color(POISON_COLOR, POISON_COLOR.A * fade));
                    puffs++;
                }
                continue;
            }

            // Patches: a glossy blob on the plane, wobbling a little, it dries up
            float   wobble = 1f + 0.06f * Mathf.Sin(_time * 2f + zone.Seed);
            Vector3 scale  = new Vector3(zone.Radius * 2f * wobble, 1f, zone.Radius * 2f / wobble);
            Basis   basis  = new Basis(Vector3.Up, zone.Seed) * Basis.FromScale(scale);
            _patches.SetInstanceTransform(patches, new Transform3D(basis, Plane3D.To3D(zone.Position, -zone.Radius * PATCH_SINK)));
            _patches.SetInstanceColor(patches, new Color(SLIME_COLOR, SLIME_COLOR.A * fade));
            patches++;
        }
        _puffs.VisibleInstanceCount   = puffs;
        _patches.VisibleInstanceCount = patches;
    }

    /// <summary>What a body of one side suffers this frame: poison of every cloud it is in, the strongest slow.</summary>
    /// <param name="position">(Required) Center of the body on the plane.</param>
    /// <param name="size">(Required) Body size, the body is inside once it overlaps half its size.</param>
    /// <param name="isPlayerSide">(Required) The side of the body: zones of the same side spare it.</param>
    /// <param name="damagePerSecond">Poison damage per second of all clouds together.</param>
    /// <param name="slow">Share of the speed it keeps, 1 outside every zone.</param>
    public void Affect(Vector2 position, float size, bool isPlayerSide, out float damagePerSecond, out float slow)
    {
        damagePerSecond = 0f;
        slow            = 1f;
        foreach (Zone zone in _zones)
        {
            if (zone.IsPlayerSide == isPlayerSide || position.DistanceTo(zone.Position) > zone.Radius + size * 0.5f)
            {
                continue;
            }
            damagePerSecond += zone.Damage;
            slow             = Mathf.Min(slow, zone.Slow);
        }
    }
}
