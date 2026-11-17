// NOTE:
//   Wires what the player presses in the HUD to the abilities: a click on an ability slot
//   works like its key (a cell ability; a creature in the sea its ability part), from the
//   pack on the three ways open fans (Game/Ui/AbilitySlots.cs) whose items are the way
//   itself or an ability part of its tab, and the keys 1 to 3 open those fans first
//   (PackEncounter.KeyRouter). Split out of Main, which calls Wire once.
//
//   AbilityInput.Wire(hud, player, abilities, creatureAbilities, encounter);

//
//   Chapters:
//     WIRE   Slot clicks, fan clicks and the keys of the ways.
//

namespace CellOut;

public static class AbilityInput
{
    // ===========================================================================
    // =================================   WIRE   ================================
    // ===========================================================================
    //
    //   Wire   Connects the HUD presses to the resolvers and routes the way keys through the fans.
    //

    /// <summary>Connects the presses of the HUD to the abilities of cell and creature, once at start.</summary>
    /// <param name="hud">(Required) The HUD with its ability slots.</param>
    /// <param name="player">(Required) The player: cell or creature.</param>
    /// <param name="abilities">(Required) The abilities of the cell parts.</param>
    /// <param name="creatureAbilities">(Required) The abilities of the creature parts.</param>
    /// <param name="encounter">(Required) The three ways of pack and tribe.</param>
    public static void Wire(Hud hud, Player player, AbilityResolver abilities, CreatureAbilityResolver creatureAbilities, PackEncounter encounter)
    {
        hud.BindWays(encounter);
        hud.AbilityPressed += index =>
        {
            if (!player.IsOrganism)
            {
                abilities.UseSlot(index);
                return;
            }
            int ways = creatureAbilities.SlotOffset;
            if (index < ways)
            {
                encounter.Use((PackWay)index);
                return;
            }
            creatureAbilities.UseSlot(index - ways);
        };
        hud.AbilityFanPressed += (way, part) =>
        {
            if (part < 0)
            {
                encounter.Use((PackWay)way);
                return;
            }
            creatureAbilities.UsePart((CreaturePartKind)part);
        };
        encounter.KeyRouter = way => hud.PressWayKey((int)way);
    }
}
