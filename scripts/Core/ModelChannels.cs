// NOTE:
//   The channels of a model animation: every bone or node the animation moves is one
//   channel (the rattle of a rattlesnake: "Tail1" lifts the tail, "Rattle" shakes the bells).
//   A channel can be made bigger, smaller or switched off for one model instance: the
//   animation is copied for that instance and the keys of the channel are scaled around the
//   rest pose, so the other instances of the model keep playing the original.
//
//   IReadOnlyList<string> names = ModelChannels.Names(player);        // "Tail1", "Rattle"
//   ModelChannels.Apply(player, new Dictionary<string, float> { ["Tail1"] = 0f });   // only the bells shake

//
//   Chapters:
//     CHANNELS   Names of the channels and the scaled copy of an animation.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public static class ModelChannels
{
    // ===========================================================================
    // ==============================   CHANNELS   ===============================
    // ===========================================================================
    //
    //   Names         The channels of the animation a player plays, in track order.
    //   Apply         Plays a copy of the animation with some channels scaled.
    //   ChannelOf     The channel a track belongs to: its bone, else its node.
    //   ScaleTrack    Scales the keys of one track around its rest value.
    //   RestOf        The rest value of a track: the rest pose of its bone, else its first key.
    //

    private const string OWN_LIBRARY = "own";        // library of the scaled copy on the player of one instance

    /// <summary>The channels of the animation a player plays right now.</summary>
    /// <param name="player">(Required) A player with a current animation (PartModels.PlayLoop).</param>
    /// <returns>One name per bone or node the animation moves, in track order; empty without an animation.</returns>
    public static IReadOnlyList<string> Names(AnimationPlayer player)
    {
        var names = new List<string>();
        if (null == player || string.IsNullOrEmpty(player.CurrentAnimation))
        {
            return names;
        }
        Animation animation = player.GetAnimation(player.CurrentAnimation);
        for (int track = 0; track < animation.GetTrackCount(); track++)
        {
            string name = ChannelOf(animation, track);
            if (!names.Contains(name))
            {
                names.Add(name);
            }
        }
        return names;
    }

    /*
      A scaled copy for one instance:
      > The current animation is duplicated (the model shares the original with every instance)
      > Every track of a channel with an amount other than 1 has its keys scaled around the
        rest value: 0 freezes the channel in its rest pose, 2 doubles its motion
      > The copy lands in a library of its own on this player and plays on from where the
        original stood, at the same pace
    */
    /// <summary>Plays a copy of the current animation with some channels scaled.</summary>
    /// <param name="player">(Required) A player with a current animation.</param>
    /// <param name="amounts">(Required) Factor per channel name, channels left out stay as they are.</param>
    public static void Apply(AnimationPlayer player, IReadOnlyDictionary<string, float> amounts)
    {
        if (null == player || null == amounts || 0 == amounts.Count || string.IsNullOrEmpty(player.CurrentAnimation))
        {
            return;
        }
        string    current = player.CurrentAnimation;
        string    name    = current.Contains('/') ? current[(current.LastIndexOf('/') + 1)..] : current;
        double    at      = player.CurrentAnimationPosition;
        float     pace    = player.SpeedScale;
        var       copy    = (Animation)player.GetAnimation(current).Duplicate(true);
        Node      root    = player.GetNodeOrNull(player.RootNode);
        for (int track = 0; track < copy.GetTrackCount(); track++)
        {
            if (amounts.TryGetValue(ChannelOf(copy, track), out float amount) && !Mathf.IsEqualApprox(amount, 1f))
            {
                ScaleTrack(copy, track, amount, root);
            }
        }

        var library = new AnimationLibrary();
        library.AddAnimation(name, copy);
        if (player.HasAnimationLibrary(OWN_LIBRARY))
        {
            player.RemoveAnimationLibrary(OWN_LIBRARY);
        }
        player.AddAnimationLibrary(OWN_LIBRARY, library);
        player.Play($"{OWN_LIBRARY}/{name}");
        player.Seek(at, true);
        player.SpeedScale = pace;
    }

    /// <summary>The channel of a track: the bone it moves ("Rig/Skeleton3D:Tail1" -> "Tail1"), else the node.</summary>
    private static string ChannelOf(Animation animation, int track)
    {
        NodePath path = animation.TrackGetPath(track);
        if (path.GetSubNameCount() > 0)
        {
            return path.GetSubName(0);
        }
        return path.GetNameCount() > 0 ? path.GetName(path.GetNameCount() - 1) : path.ToString();
    }

    private static void ScaleTrack(Animation animation, int track, float amount, Node root)
    {
        int keys = animation.TrackGetKeyCount(track);
        if (0 == keys)
        {
            return;
        }
        Variant rest = RestOf(animation, track, root);
        for (int key = 0; key < keys; key++)
        {
            Variant value = animation.TrackGetKeyValue(track, key);
            switch (animation.TrackGetType(track))
            {
                case Animation.TrackType.Rotation3D:
                    // Around the rest rotation: the turn away from it grows or shrinks
                    Quaternion from = rest.AsQuaternion().Normalized();
                    Quaternion turn = (from.Inverse() * value.AsQuaternion().Normalized()).Normalized();
                    Vector3    axis = turn.GetAxis();
                    float      angle = Mathf.Wrap(turn.GetAngle(), -Mathf.Pi, Mathf.Pi);
                    Quaternion scaled = axis.IsNormalized() ? from * new Quaternion(axis, angle * amount) : from;
                    animation.TrackSetKeyValue(track, key, scaled);
                    break;
                case Animation.TrackType.Position3D:
                case Animation.TrackType.Scale3D:
                    animation.TrackSetKeyValue(track, key, rest.AsVector3().Lerp(value.AsVector3(), amount));
                    break;
                case Animation.TrackType.BlendShape:
                    animation.TrackSetKeyValue(track, key, Mathf.Lerp(rest.AsSingle(), value.AsSingle(), amount));
                    break;
            }
        }
    }

    /// <summary>The value a track rests at: the rest pose of its bone, else its first key.</summary>
    private static Variant RestOf(Animation animation, int track, Node root)
    {
        NodePath path = animation.TrackGetPath(track);
        if (path.GetSubNameCount() > 0 && root?.GetNodeOrNull(new NodePath(path.GetConcatenatedNames())) is Skeleton3D skeleton)
        {
            int bone = skeleton.FindBone(path.GetSubName(0));
            if (bone >= 0)
            {
                Transform3D pose = skeleton.GetBoneRest(bone);
                switch (animation.TrackGetType(track))
                {
                    case Animation.TrackType.Rotation3D: return pose.Basis.GetRotationQuaternion();
                    case Animation.TrackType.Position3D: return pose.Origin;
                    case Animation.TrackType.Scale3D:    return pose.Basis.Scale;
                }
            }
        }
        return animation.TrackGetKeyValue(track, 0);
    }
}
