// NOTE:
//   Resolves everything that touches: the player eats food, bigger cells swallow smaller
//   ones with pseudopods (the player too, in both directions), equal ones push apart.
//   It does not process on its own: Main calls Resolve once per physics frame, between
//   the coast update and the refill of the Spawner.
//
//   var contacts = new ContactResolver();
//   contacts.Setup(session);
//   contacts.SplashRequested += SpawnSplash;
//   AddChild(contacts);
//   contacts.Resolve(dt, playerSize, playerPosition, viewRadius);   // in _PhysicsProcess

//
//   Chapters:
//     SETTINGS           The session, the splash signal and the kill reward.
//     RESOLVE CONTACTS   Food, engulfing, player vs. cells and cells vs. cells per physics frame.
//     ENGULF             Swallows in progress, the player inside a bigger cell, cell pairs.
//

using System.Collections.Generic;
using Godot;

namespace CellOut;

public partial class ContactResolver : Node
{
    // ===========================================================================
    // ==============================   SETTINGS   ===============================
    // ===========================================================================
    //
    //   SplashRequested   A plop is due on the plane, Main spawns it.
    //   Setup             The session with player, layers and coast, before AddChild.
    //   KillReward        Biomass for an eaten cell.
    //

    [Signal] public delegate void SplashRequestedEventHandler(Vector2 position, float radius, Color color);

    private const float CRITTER_GROWTH_CAP = 3f;    // critters grow up to this many player sizes
    private const float DESPAWN_VIEWS = 2.5f;       // food and cells this many view radii away despawn
    private const float KILL_REWARD_FACTOR = 0.045f;

    private WorldSession _session;

    private Player Player => _session.Player;
    private Coast Coast => _session.Coast;
    private Node2D FoodLayer => _session.FoodLayer;
    private Node2D CritterLayer => _session.CritterLayer;

    /// <summary>Hands in the shared state, call before AddChild.</summary>
    /// <param name="session">(Required) Player, layers and coast.</param>
    public void Setup(WorldSession session)
    {
        _session = session;
    }

    /// <summary>Biomass for an eaten cell.</summary>
    /// <param name="radius">(Required) Radius of the eaten cell on the plane.</param>
    /// <returns>The biomass, about 7-13% of the own biomass for a large prey.</returns>
    /// <example><code>float reward = ContactResolver.KillReward(critter.Radius);</code></example>
    public static float KillReward(float radius)
    {
        return radius * radius * KILL_REWARD_FACTOR;
    }

    // ===========================================================================
    // ==========================   RESOLVE CONTACTS   ===========================
    // ===========================================================================
    //
    //   1. Food               Attracts, swallows and cleans up food.
    //   2. Engulf             Pulls swallowed cells into their eaters, digests them when done.
    //   3. Player vs. cells   Swallow smaller cells, get swallowed by bigger ones, bump equal ones,
    //                         cells far away despawn.
    //   4. Cells vs. cells    Bigger cells start swallowing smaller ones, equal ones push apart.
    //

    /// <summary>One physics frame of contacts. Size and position are the ones Main read at the start of its frame.</summary>
    /// <param name="dt">(Required) Frame time in seconds.</param>
    /// <param name="playerSize">(Required) Body size of the player at the start of the frame.</param>
    /// <param name="playerPosition">(Required) Position of the player at the start of the frame, before the coast moved it.</param>
    /// <param name="viewRadius">(Required) Visible radius on the plane at the start of the frame (FollowCamera.ViewRadius).</param>
    /// <example><code>contacts.Resolve(dt, Player.BodySize(), Player.GlobalPosition, followCamera.ViewRadius());</code></example>
    public void Resolve(float dt, float playerSize, Vector2 playerPosition, float viewRadius)
    {
        float despawnDistance = viewRadius * DESPAWN_VIEWS;

        // ---------------------------------------------------------------------------
        // Food
        //   Attracts, swallows and cleans up food.
        // ---------------------------------------------------------------------------

        foreach (Node node in FoodLayer.GetChildren())
        {
            var   food     = (Food)node;
            if (null != Coast && FoodHabitat.Land == food.Habitat)
            {
                food.GroundHeight = Coast.GroundHeight(food.Position) * playerSize;
            }
            else if (null != Coast)
            {
                food.Position = Coast.ToSea(food.Position, food.Size);
            }
            float distance = playerPosition.DistanceTo(food.Position);
            if (distance > despawnDistance)
            {
                food.QueueFree();
                continue;
            }
            if (distance < playerSize * Player.MagnetRange + 10f)
            {
                food.Position = food.Position.MoveToward(playerPosition, playerSize * 3f * dt);
            }
            if (distance < playerSize * 0.9f)
            {
                Player.AddBiomass(food.Value, food.IsMeat ? BiomassSource.Meat : BiomassSource.Plant);
                EmitSignal(SignalName.SplashRequested, food.Position, food.Size * 3f, food.ColorOf());
                food.QueueFree();
            }
        }

        // ---------------------------------------------------------------------------
        // Engulf
        //   Pulls swallowed cells into their eaters, digests them when done.
        // ---------------------------------------------------------------------------

        UpdateEngulfing(dt, playerSize);

        // ---------------------------------------------------------------------------
        // Player vs. cells
        //   Swallow smaller cells, get swallowed by bigger ones, bump equal ones, cells far away despawn.
        // ---------------------------------------------------------------------------

        float thornScale = playerSize / CellBody.REFERENCE_SIZE;
        foreach (Node node in CritterLayer.GetChildren())
        {
            var critter = (Critter)node;
            if (critter.IsQueuedForDeletion() || critter.IsEngulfed)
            {
                continue;
            }
            // Sea creatures stay in the water
            if (null != Coast)
            {
                critter.Position = Coast.ToSea(critter.Position, critter.Radius);
            }
            float distance = playerPosition.DistanceTo(critter.Position);
            if (distance > despawnDistance)
            {
                critter.QueueFree();
                continue;
            }

            // A jaw towards the cell and every division let the player swallow bigger cells
            float direction   = (critter.Position - playerPosition).Angle();
            float eatRatio    = Player.EatRatioToward(direction);
            bool  canEat      = Player.Stage >= Player.SINGLE_STAGE && playerSize > critter.Radius * eatRatio;
            bool  isDangerous = critter.CanEat(playerSize);
            float thornHit    = Player.ContactDamageToward(direction) * thornScale;
            float critterHit  = critter.ContactDamageToward(direction + Mathf.Pi);   // its spikes and jaws towards the player
            if (distance > playerSize + critter.Radius)
            {
                continue;
            }

            // Smaller: the membrane flows around it on contact, ENGULF finishes the meal
            if (canEat)
            {
                if (distance < playerSize + critter.Radius * ENGULF_CONTACT)
                {
                    // Spiky prey stings once while it is swallowed
                    critter.StartEngulf(Player);
                    Player.Digest(critterHit, critter.Position);
                    Log.Verbose("ContactResolver", $"Player engulfs a {critter.Kind} of radius {critter.Radius:F1}.");
                }
                continue;
            }

            // Bigger: its membrane flows around the player, pulls it in and digests it
            if (isDangerous)
            {
                EngulfPlayer(critter, distance, playerSize, thornHit, critterHit, dt);
                continue;
            }

            // Equal: push apart, spikes and jaws towards it hurt on bump
            Vector2 normal  = (critter.Position - playerPosition).Normalized();
            float   overlap = playerSize + critter.Radius - distance;
            critter.Position += normal * overlap * 0.5f;
            Player.Position  -= normal * overlap * 0.5f;
            if (thornHit > 0f && critter.BumpCooldown <= 0f)
            {
                critter.Hurt(thornHit, Player.GlobalPosition);
                critter.BumpCooldown = 0.5f;
            }
            if (critterHit > 0f)
            {
                Player.TakeDamage(critterHit, critter.Position);
            }
        }

        // ---------------------------------------------------------------------------
        // Cells vs. cells
        //   Bigger cells start swallowing smaller ones, equal ones push apart.
        // ---------------------------------------------------------------------------

        ResolveCritterContacts();
    }

    // ===========================================================================
    // ===============================   ENGULF   ================================
    // ===========================================================================
    //
    //   UpdateEngulfing          Advances every swallow, hands out the reward of finished ones.
    //   EngulfPlayer             One frame of a bigger cell swallowing the player.
    //   ResolveCritterContacts   Checks every pair of cells once: start swallowing or push apart.
    //

    private const float ENGULF_SECONDS = 1.4f;     // from the first touch to digested
    private const float ENGULF_CONTACT = 0.3f;     // share of the prey radius that must touch the player

    /*
      Every frame, for each swallowed cell:
      > Its eater is gone (eaten, freed)
          - Release it, it swims on.
      > Otherwise
          - The eater stretches pseudopods towards it (EngulfPoint, EngulfReach: up while
            the arms wrap around, down once the prey is inside).
          - Pull it in a little further, digest it once done: biomass for the player,
            growth for a critter.
    */
    /// <summary>Advances every swallow, hands out the reward of finished ones.</summary>
    private void UpdateEngulfing(float dt, float playerSize)
    {
        // Pseudopods only reach out while a swallow runs
        Player.EngulfReach = 0f;
        foreach (Node node in CritterLayer.GetChildren())
        {
            if (node is Critter critter)
            {
                critter.EngulfReach = 0f;
            }
        }

        foreach (Node node in CritterLayer.GetChildren())
        {
            if (node is not Critter prey || !prey.IsEngulfed || prey.IsQueuedForDeletion())
            {
                continue;
            }

            // Its eater is gone (eaten, freed)
            CellBody eater = prey.Engulfer;
            if (!IsInstanceValid(eater) || eater.IsQueuedForDeletion())
            {
                prey.ReleaseEngulf();
                continue;
            }

            // Pseudopods towards the prey, the strongest swallow of an eater wins
            float reach = Mathf.Sin(prey.EngulfProgress * Mathf.Pi);
            if (reach >= eater.EngulfReach)
            {
                eater.EngulfReach = reach;
                eater.EngulfPoint = prey.GlobalPosition;
            }
            if (!prey.AdvanceEngulf(dt, ENGULF_SECONDS))
            {
                continue;
            }

            // Digested
            if (eater == Player)
            {
                Player.AddBiomass(KillReward(prey.EngulfRadius), BiomassSource.Meat);
            }
            else if (eater is Critter hunter)
            {
                hunter.Devour(prey.EngulfRadius, playerSize * CRITTER_GROWTH_CAP);
            }
            prey.QueueFree();
        }
    }

    private const float PLAYER_PULL = 0.3f;        // pull into a bigger cell at full depth, share of the player's speed
    private const float PULL_AT_CONTACT = 0.25f;   // share of PLAYER_PULL at the first touch, it grows with the depth
    private const float DIGEST_PER_SECOND = 1f;    // share of the old hit damage per second inside
    private const float THORNS_PER_SECOND = 2f;    // spikes and jaws keep hurting the eater from inside

    /*
      A bigger cell swallows the player like the player swallows small ones, but the
      player can fight it:
      - The eater's pseudopods reach around the player, the deeper it sits the more.
      - The pull drags the player to the eater's center, weak at the first touch and up to
        PLAYER_PULL of its own speed deep inside, swimming away gets it out (contact lost =
        free). The eater itself slows down while it eats (Critter, ENGULF_SLOWDOWN).
      - Inside, the player is digested steadily, faster by the eater's spikes and jaws
        (armor towards the eater halves it), and its own spikes and jaws hurt the eater
        (its armor halves that), which may pop and set it free.
      - Health at zero while inside means game over (Player.Die), a cluster loses cells first.
      - The grace period after a death or lost cells stops the swallow.
    */
    /// <summary>One frame of a bigger cell swallowing the player.</summary>
    /// <param name="critterHit">(Required) Spike and jaw damage of the eater towards the player, per second inside.</param>
    private void EngulfPlayer(Critter critter, float distance, float playerSize, float thornHit, float critterHit, float dt)
    {
        if (Player.Invulnerable > 0f)
        {
            return;
        }

        // Pseudopods around the player
        float depth = 1f - distance / (playerSize + critter.Radius);
        critter.EngulfReach = Mathf.Max(critter.EngulfReach, Mathf.Clamp(depth * 2f, 0f, 1f));
        critter.EngulfPoint = Player.GlobalPosition;

        // Pull towards the eater's center, weak at the first touch, stronger the deeper
        Vector2 toCenter = critter.GlobalPosition - Player.GlobalPosition;
        float   pull     = PLAYER_PULL * Mathf.Lerp(PULL_AT_CONTACT, 1f, Mathf.Clamp(depth * 2f, 0f, 1f));
        Player.Position += toCenter.LimitLength(Player.CurrentSpeed() * pull * dt);

        // Digest the player, thorns hurt back
        float damage = (0 == Player.Stage ? 6f : 12f) * critter.Radius / playerSize;
        Player.Digest((damage * DIGEST_PER_SECOND + critterHit) * dt, critter.Position);
        if (thornHit > 0f)
        {
            critter.Hurt(thornHit * THORNS_PER_SECOND * dt, Player.GlobalPosition);
        }
    }

    /// <summary>Checks every pair of cells once: start swallowing or push apart.</summary>
    private void ResolveCritterContacts()
    {
        var critters = new List<Critter>();
        foreach (Node node in CritterLayer.GetChildren())
        {
            if (node is Critter critter && !critter.IsQueuedForDeletion() && !critter.IsEngulfed)
            {
                critters.Add(critter);
            }
        }

        for (int i = 0; i < critters.Count; i++)
        {
            for (int j = i + 1; j < critters.Count; j++)
            {
                Critter a = critters[i];
                Critter b = critters[j];
                if (a.IsQueuedForDeletion() || b.IsQueuedForDeletion() || a.IsEngulfed || b.IsEngulfed)
                {
                    continue;
                }
                float distance = a.Position.DistanceTo(b.Position);
                if (distance >= a.Radius + b.Radius)
                {
                    continue;
                }

                // Bigger one flows around the smaller one on contact, ENGULF finishes the meal
                Critter big   = a.Radius >= b.Radius ? a : b;
                Critter small = big == a ? b : a;
                if (big.CanEat(small.Radius))
                {
                    small.StartEngulf(big);
                    continue;
                }

                // Similar size: push apart
                Vector2 normal  = distance > 0.01f ? (b.Position - a.Position).Normalized() : Vector2.Right;
                float   overlap = a.Radius + b.Radius - distance;
                a.Position -= normal * overlap * 0.5f;
                b.Position += normal * overlap * 0.5f;
            }
        }
    }
}
