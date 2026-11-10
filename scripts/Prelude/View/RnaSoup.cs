// NOTE:
//   The free nucleotides (A, U, G, C) of the RNA prelude (Prelude/RnaWorld.cs): beads with
//   their letter that drift and bob in the primordial soup. This component holds and
//   draws them, RnaWorld decides where new ones appear and which head swallows which.
//
//   var soup = new RnaSoup();
//   soup.Setup(palette, size);
//   AddChild(soup);
//   soup.Add(RnaBase.A, position, Vector2.Zero);
//   soup.Drift(time, dt);                                  // every frame
//   Vector2? prey = soup.Nearest(rival.HeadPosition, range);

//
//   Chapters:
//     NUCLEOTIDES   Adding, reading and removing nucleotides.
//     DRIFT         Drifting and bobbing, the nearest nucleotide to a point.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class RnaSoup : Node3D
{
    // ===========================================================================
    // =============================   NUCLEOTIDES   =============================
    // ===========================================================================
    //
    //   NUCLEOTIDE_RADIUS   Radius of a nucleotide bead, in body sizes.
    //   Setup               (Required) palette: the bead materials. (Required) size: the body size.
    //   Count               The number of free nucleotides.
    //   KindAt              The base of a nucleotide.
    //   PositionAt          The plane position of a nucleotide.
    //   Add                 A nucleotide bead with its letter at a point.
    //   RemoveAt            Frees one nucleotide.
    //

    public const float NUCLEOTIDE_RADIUS = 0.15f;

    private sealed class Nucleotide
    {
        public RnaBase Kind;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Phase;
        public Node3D Node;
    }

    private readonly List<Nucleotide> _nucleotides = new();
    private RnaPalette _palette;
    private float _size;

    public int Count => _nucleotides.Count;

    /// <summary>Hands over the materials and the scale, called once before AddChild.</summary>
    /// <param name="palette">(Required) The shared materials of the prelude.</param>
    /// <param name="size">(Required) Body size of the player, everything scales with it.</param>
    public void Setup(RnaPalette palette, float size)
    {
        _palette = palette;
        _size    = size;
    }

    /// <summary>The base of a nucleotide.</summary>
    /// <param name="index">(Required) Index below Count.</param>
    public RnaBase KindAt(int index)
    {
        return _nucleotides[index].Kind;
    }

    /// <summary>The plane position of a nucleotide.</summary>
    /// <param name="index">(Required) Index below Count.</param>
    public Vector2 PositionAt(int index)
    {
        return _nucleotides[index].Position;
    }

    /// <summary>Adds a nucleotide bead with its letter.</summary>
    /// <param name="kind">(Required) The base.</param>
    /// <param name="position">(Required) Plane position.</param>
    /// <param name="velocity">(Required) Start velocity in world units per second, it fades into the drift.</param>
    public void Add(RnaBase kind, Vector2 position, Vector2 velocity)
    {
        var node = new Node3D { Position = Plane3D.To3D(position) };
        AddChild(node);
        MeshInstance3D bead = CellVisual3D.AddMesh(node, CellVisual3D.SphereMesh(true), _palette.BaseMaterial(kind), Vector3.Zero, Vector3.One * NUCLEOTIDE_RADIUS * _size);
        bead.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        // The letter floats above the bead and always faces the camera
        node.AddChild(new Label3D
        {
            Text            = kind.ToString(),
            Billboard       = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize        = 64,
            OutlineSize     = 14,
            PixelSize       = NUCLEOTIDE_RADIUS * _size * 0.025f,
            Modulate        = RnaPalette.ColorOf(kind).Lightened(0.3f),
            OutlineModulate = new Color(0.05f, 0.05f, 0.1f),
            Position        = Vector3.Up * NUCLEOTIDE_RADIUS * 1.3f * _size,
            NoDepthTest     = true,
        });
        _nucleotides.Add(new Nucleotide { Kind = kind, Position = position, Velocity = velocity, Phase = Rand.Angle(), Node = node });
    }

    /// <summary>Frees one nucleotide.</summary>
    /// <param name="index">(Required) Index below Count.</param>
    public void RemoveAt(int index)
    {
        _nucleotides[index].Node.QueueFree();
        _nucleotides.RemoveAt(index);
    }

    // ===========================================================================
    // ================================   DRIFT   ================================
    // ===========================================================================
    //
    //   Drift     Every nucleotide drifts in a slowly turning direction and bobs.
    //   Nearest   The nearest nucleotide within a range, null for none.
    //

    private const float DRIFT_SPEED = 0.25f;         // body sizes per second

    /// <summary>Moves every nucleotide: drifting, bobbing and turning.</summary>
    /// <param name="time">(Required) Time of the prelude in seconds.</param>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    public void Drift(float time, float dt)
    {
        foreach (Nucleotide nucleotide in _nucleotides)
        {
            nucleotide.Velocity  = nucleotide.Velocity.Lerp(Vector2.FromAngle(nucleotide.Phase + time * 0.3f) * DRIFT_SPEED * _size, Mathf.Clamp(dt * 0.8f, 0f, 1f));
            nucleotide.Position += nucleotide.Velocity * dt;
            nucleotide.Node.Position = Plane3D.To3D(nucleotide.Position, Mathf.Sin(time * 1.5f + nucleotide.Phase) * 0.06f * _size);
            nucleotide.Node.Rotation = new Vector3(0f, time + nucleotide.Phase, 0f);
        }
    }

    /// <summary>The nearest nucleotide to a point.</summary>
    /// <param name="point">(Required) Plane point.</param>
    /// <param name="range">(Required) Only nucleotides closer than this count.</param>
    /// <returns>Its plane position, null when none is within the range.</returns>
    public Vector2? Nearest(Vector2 point, float range)
    {
        Vector2? nearest = null;
        float    best    = range;
        foreach (Nucleotide nucleotide in _nucleotides)
        {
            float distance = nucleotide.Position.DistanceTo(point);
            if (distance < best)
            {
                best    = distance;
                nearest = nucleotide.Position;
            }
        }
        return nearest;
    }
}
