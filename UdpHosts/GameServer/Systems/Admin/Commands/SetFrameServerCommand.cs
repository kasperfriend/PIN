using System.Collections.Generic;
using System.Linq;
using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.StaticDB;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand(
    "Switch your (or your target's) battleframe on the fly",
    "setframe <assault|dreadnaught|biotech|engineer|recon|firecat|tigerclaw|electron|bastion|mammoth|rhino|dragonfly|recluse|nighthawk|raptor|graviton|arsenal|archangel|battlelab|...|frameId>",
    "setframe",
    "frame",
    "switchframe")]
public class SetFrameServerCommand : ServerCommand
{
    /// <summary>
    ///     Friendly names for the battleframes a character can actually own (the
    ///     char-create loadouts of build prod-1962, see
    ///     <see cref="HardcodedCharacterData.TempCharCreateLoadouts"/>). A numeric id is
    ///     accepted as well.
    /// </summary>
    private static readonly Dictionary<string, uint> FrameNames = new()
    {
        { "assault", 76164 },
        { "dreadnaught", 75772 },
        { "dread", 75772 },
        { "biotech", 75774 },
        { "engineer", 75775 },
        { "recon", 75773 },
        { "firecat", 76133 },
        { "tigerclaw", 76132 },
        { "electron", 76337 },
        { "bastion", 76338 },
        { "mammoth", 76331 },
        { "rhino", 76332 },
        { "dragonfly", 76335 },
        { "recluse", 76336 },
        { "nighthawk", 76333 },
        { "raptor", 76334 },
        { "graviton", 82359 },
        { "arsenal", 82360 },
        { "archangel", 82394 },
        { "battlelabtrainee", 77733 },
        { "beachparty", 124356 },
    };

    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer == null)
        {
            SourceFeedback("setframe requires a player character", context);
            return;
        }

        if (parameters.Length != 1)
        {
            SourceFeedback("Usage: setframe <frameName|frameId>", context);
            return;
        }

        string requested = parameters[0];
        uint chassisId = ParseUIntParameter(requested);
        if (chassisId == 0)
        {
            if (!FrameNames.TryGetValue(requested.ToLowerInvariant(), out chassisId))
            {
                SourceFeedback($"Unknown battleframe '{requested}'", context);
                return;
            }
        }

        if (SDBInterface.GetBattleframe(chassisId) == null)
        {
            SourceFeedback($"No dbitems::Battleframe row for id {chassisId}", context);
            return;
        }

        var character = context.Target as CharacterEntity ?? context.SourcePlayer.CharacterEntity;
        if (character == null || !character.IsPlayerControlled || character.Player == null)
        {
            SourceFeedback("setframe only applies to a player-controlled character", context);
            return;
        }

        var inventory = character.Player.Inventory;

        // The frame must exist as a loadout in the character's inventory; a cheat switch
        // to a frame the character does not own yet generates the database default
        // char-create loadout (frame + its starter gear), exactly like character creation.
        int loadoutId = inventory.GetLoadoutIdForChassis(chassisId);
        if (loadoutId == 0)
        {
            var createId = HardcodedCharacterData.TempCharCreateLoadouts
                .FirstOrDefault(pair => pair.Value == chassisId).Key;
            if (createId == 0)
            {
                SourceFeedback($"No char-create loadout exists for battleframe {chassisId}", context);
                return;
            }

            HardcodedCharacterData.GenerateCharCreateLoadoutAndItems(inventory, createId, chassisId);
            loadoutId = inventory.GetLoadoutIdForChassis(chassisId);
            if (loadoutId == 0)
            {
                SourceFeedback($"Failed to create a loadout for battleframe {chassisId}", context);
                return;
            }
        }

        var loadoutRefData = inventory.GetLoadoutReferenceData(loadoutId);
        if (loadoutRefData == null)
        {
            SourceFeedback($"No loadout data for loadout {loadoutId}", context);
            return;
        }

        character.ApplyLoadout(new CharacterLoadout(loadoutRefData));

        // Force the same refresh SelectLoadout does after a loadout change so the client
        // re-reads the frame (stats, visuals) immediately.
        if (character.Character_BaseController != null)
        {
            character.Character_BaseController.LevelProp = character.FrameProgressionLevel;
            character.Character_BaseController.EffectiveLevelProp = character.FrameProgressionLevel;
        }

        // The physics body still carries the previous frame's collision size until the
        // next full physics rebuild (login/respawn); the visual + stat + health swap is
        // live right away.
        SourceFeedback(
            $"Switched to battleframe {chassisId} (loadout {loadoutId}). Health: {character.CurrentHealth}/{character.MaxHealth.Value}. Physics collision is rebuilt on the next respawn.",
            context);
    }
}
