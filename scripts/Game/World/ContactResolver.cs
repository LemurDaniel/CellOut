// NOTE:
//   Resolves everything that touches: cells swallow food and smaller cells with
//   pseudopods (the player too, in both directions), equal ones push apart.
//   It does not process on its own: Main calls Resolve once per physics frame, between
//   the coast update and the refill of the Spawner.
//
//   var contacts = new ContactResolver();
//   contacts.Setup(session);
//   AddChild(contacts);
//   contacts.Resolve(dt, playerSize, playerPosition, viewRadius);   // in _PhysicsProcess

//
//   Chapters:
//     SETTINGS           The session and the kill reward.
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
    //   Setup        The session with player, layers and coast, before AddChild.
    //   KillReward   Biomass for an eaten cell.
    //

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
    /// <example><code>float reward = ContactResolver.KillReward(critter.BodySize());</code></example>
    public static float KillReward(float radius)
    {
        return radius * radius * KILL_REWARD_FACTOR;
    }

    // ===========================================================================
    // ==========================   RESOLVE CONTACTS   ===========================
    // ===========================================================================
    //
    //   1. Food               Attracts and cleans up food, the player and cells start swallowing what they touch.
    //   2. Engulf             Pulls swallowed cells into their eaters, digests them when done.
    //   3. Player vs. cells   Sting cells within reach, swallow smaller ones, get swallowed by bigger
    //                         ones, hold or bump equal ones, cells far away despawn.
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
        //   Attracts and cleans up food, the player and cells start swallowing what they touch.
        // ---------------------------------------------------------------------------

        foreach (Node node in FoodLayer.GetChildren())
        {
            var   food     = (Food)node;
            if (food.IsEngulfed || food.IsQueuedForDeletion())
            {
                continue;     // ENGULF moves it now
            }
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
            if (distance < playerSize + food.Size * 0.5f)
            {
                food.StartEngulf(Player);
                continue;
            }

            // A cell that touches it swallows it too, the patch empties without the player
            foreach (Node critterNode in CritterLayer.GetChildren())
            {
                if (critterNode is Critter grazer && !grazer.IsEngulfed && !grazer.IsQueuedForDeletion()
                    && grazer.Position.DistanceTo(food.Position) < grazer.BodySize() + food.Size * 0.5f)
                {
                    food.StartEngulf(grazer);
                    break;
                }
            }
        }

        // ---------------------------------------------------------------------------
        // Engulf
        //   Pulls swallowed cells into their eaters, digests them when done.
        // ---------------------------------------------------------------------------

        UpdateEngulfing(dt, playerSize);

        // ---------------------------------------------------------------------------
        // Player vs. cells
        //   Sting cells within reach, swallow smaller ones, get swallowed by bigger ones, hold or bump
        //   equal ones, cells far away despawn.
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
                critter.Position = Coast.ToSea(critter.Position, critter.BodySize());
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
            bool  canEat      = Player.Stage >= Player.SINGLE_STAGE && playerSize > critter.BodySize() * eatRatio;
            bool  isDangerous = critter.CanEat(playerSize);
            float thornHit    = Player.ContactDamageToward(direction) * thornScale;
            float critterHit  = critter.ContactDamageToward(direction + Mathf.Pi);   // its spikes and jaws towards the player
            StingFromDistance(critter, direction, distance, playerSize, thornScale);
            if (critter.IsQueuedForDeletion() || distance > playerSize + critter.BodySize())
            {
                continue;
            }

            // Smaller: the membrane flows around it on contact, ENGULF finishes the meal
            if (canEat)
            {
                if (distance < playerSize + critter.BodySize() * ENGULF_CONTACT)
                {
                    // Spiky prey stings once while it is swallowed
                    critter.StartEngulf(Player);
                    Player.Digest(critterHit, critter.Position);
                    Log.Verbose("ContactResolver", $"Player engulfs a {critter.Kind} of radius {critter.BodySize():F1}.");
                }
                continue;
            }

            // Bigger: its membrane flows around the player, pulls it in and digests it
            if (isDangerous)
            {
                EngulfPlayer(critter, distance, playerSize, thornHit, critterHit, dt);
                continue;
            }

            // Equal: a sucker towards it holds it on the membrane and drains it, else push apart
            Vector2 normal = (critter.Position - playerPosition).Normalized();
            if (Player.CellTraits.CanSuckToward(direction - Player.Facing))
            {
                critter.Position = Player.Position + normal * (playerSize + critter.BodySize()) * LATCH_GAP;
                critter.Hurt(Combat.SuckerDamage * Player.CellTraits.Toxin * thornScale * dt, Player.GlobalPosition);
            }
            else
            {
                float overlap = playerSize + critter.BodySize() - distance;
                critter.Position += normal * overlap * 0.5f;
                Player.Position  -= normal * overlap * 0.5f;
            }
            // Its suckers towards the player drain it while they touch
            float drain = critter.SuckerDrainToward(direction + Mathf.Pi);
            if (drain > 0f)
            {
                Player.Digest(drain * dt, critter.Position);
            }
            // Spikes and jaws towards each other hurt on bump
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
    //   UpdateFoodEngulfing      The same for swallowed food: biomass for the player, nothing for a critter.
    //   EngulfPlayer             One frame of a bigger cell swallowing the player.
    //   StingFromDistance        Nematocysts of the player and the cell shoot at each other within reach, a hit paralyses.
    //   ResolveCritterContacts   Checks every pair of cells once: start swallowing or push apart.
    //

    private const float ENGULF_SECONDS = 1.4f;     // from the first touch to digested
    private const float ENGULF_CONTACT = 0.3f;     // share of the prey radius that must touch the player
    private const float FOOD_ENGULF_SECONDS = 0.6f;
    private const float FOOD_ENGULF_REACH = 0.6f;  // food is small, the pseudopods reach out less than for a cell

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
        UpdateFoodEngulfing(dt);
    }

    /// <summary>Advances every swallowed piece of food, a smaller reach than for cells, runs after the reset of the reach.</summary>
    private void UpdateFoodEngulfing(float dt)
    {
        foreach (Node node in FoodLayer.GetChildren())
        {
            if (node is not Food food || !food.IsEngulfed || food.IsQueuedForDeletion())
            {
                continue;
            }

            // Its eater is gone (eaten, freed)
            if (food.Engulfer is not CellBody eater || !IsInstanceValid(eater) || eater.IsQueuedForDeletion())
            {
                food.ReleaseEngulf();
                continue;
            }

            // Pseudopods towards the food, unless a bigger swallow already reaches further
            float reach = Mathf.Sin(food.EngulfProgress * Mathf.Pi) * FOOD_ENGULF_REACH;
            if (reach >= eater.EngulfReach)
            {
                eater.EngulfReach = reach;
                eater.EngulfPoint = food.GlobalPosition;
            }
            if (!food.AdvanceEngulf(dt, FOOD_ENGULF_SECONDS))
            {
                continue;
            }

            // Digested: biomass only for the player, critters graze without growing
            if (eater == Player)
            {
                Player.AddBiomass(food.Value, food.IsMeat ? BiomassSource.Meat : BiomassSource.Plant);
            }
            food.QueueFree();
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
        float depth = 1f - distance / (playerSize + critter.BodySize());
        critter.EngulfReach = Mathf.Max(critter.EngulfReach, Mathf.Clamp(depth * 2f, 0f, 1f));
        critter.EngulfPoint = Player.GlobalPosition;

        // Pull towards the eater's center, weak at the first touch, stronger the deeper
        Vector2 toCenter = critter.GlobalPosition - Player.GlobalPosition;
        float   pull     = PLAYER_PULL * Mathf.Lerp(PULL_AT_CONTACT, 1f, Mathf.Clamp(depth * 2f, 0f, 1f));
        Player.Position += toCenter.LimitLength(Player.CurrentSpeed() * pull * dt);

        // Digest the player, thorns hurt back
        float damage = (0 == Player.Stage ? 6f : 12f) * critter.BodySize() / playerSize;
        Player.Digest((damage * DIGEST_PER_SECOND + critterHit) * dt, critter.Position);
        if (thornHit > 0f)
        {
            critter.Hurt(thornHit * THORNS_PER_SECOND * dt, Player.GlobalPosition);
        }
    }

    /// <summary>Arcs, reach and damage of nematocysts and suckers from data/balance.json (cell.combat).</summary>
    private static CellCombatBalance Combat => Balance.Current.Cell.Combat;

    private const float LATCH_GAP = 0.98f;         // share of the contact distance a held cell sits at, keeps the contact

    /*
      A nematocyst fires at a cell that is close but not touching yet, both ways:
      - Within reach (StingReach sizes of the shooter beyond its membrane) and inside the arc of a nematocyst.
      - The player hits the same cell once per StingCooldown, a cell reloads for StingCooldown.
      - Hurts the target (times the toxin; the player's shot scaled like spikes, the cell's
        unscaled like its spikes) and paralyses it for StunTime.
    */
    /// <summary>Nematocysts of the player and the cell shoot at each other within reach, a hit paralyses.</summary>
    /// <param name="direction">(Required) World direction from the player to the cell.</param>
    /// <param name="thornScale">(Required) Damage scale of the player size against REFERENCE_SIZE.</param>
    private void StingFromDistance(Critter critter, float direction, float distance, float playerSize, float thornScale)
    {
        float gap = distance - playerSize - critter.BodySize();

        // The player shoots at the cell
        float sting = Player.CellTraits.StingToward(direction - Player.Facing) * thornScale;
        if (sting > 0f && critter.StingCooldown <= 0f && gap <= playerSize * Combat.StingReach)
        {
            critter.Hurt(sting, Player.GlobalPosition);
            critter.Stun(Combat.StunTime);
            critter.StingCooldown = Combat.StingCooldown;
            Log.Verbose("ContactResolver", $"Player stings a {critter.Kind} for {sting:F1}, paralysed for {Combat.StunTime:F1} s.");
        }

        // The cell shoots back
        float shot = critter.StingToward(direction + Mathf.Pi);
        if (shot > 0f && critter.StingReload <= 0f && !critter.IsQueuedForDeletion() && gap <= critter.BodySize() * Combat.StingReach)
        {
            critter.StingReload = Combat.StingCooldown;
            if (Player.TakeDamage(shot, critter.Position))
            {
                Player.Stun(Combat.StunTime);
                Log.Verbose("ContactResolver", $"A {critter.Kind} stings the player for {shot:F1}, paralysed for {Combat.StunTime:F1} s.");
            }
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
                if (distance >= a.BodySize() + b.BodySize())
                {
                    continue;
                }

                // Bigger one flows around the smaller one on contact, ENGULF finishes the meal
                Critter big   = a.BodySize() >= b.BodySize() ? a : b;
                Critter small = big == a ? b : a;
                if (big.CanEat(small.BodySize()))
                {
                    small.StartEngulf(big);
                    continue;
                }

                // Similar size: push apart
                Vector2 normal  = distance > 0.01f ? (b.Position - a.Position).Normalized() : Vector2.Right;
                float   overlap = a.BodySize() + b.BodySize() - distance;
                a.Position -= normal * overlap * 0.5f;
                b.Position += normal * overlap * 0.5f;
            }
        }
    }
}
