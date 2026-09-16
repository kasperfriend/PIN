using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameServer.Data;

/// <summary>
///     The bag model the client holds: how many bags it believes it has and which of their slots
///     are occupied. The model arrives as the JSON payload of a <c>BagInventoryUpdate</c>, sent in
///     answer to the client's <c>BagInventorySettings</c> command, and the client enforces it
///     locally - when every slot it knows about is taken it refuses a vendor purchase outright
///     (red flash, no request on the wire) and anything the full <c>InventoryUpdate</c> brought it
///     has nowhere to go.
/// </summary>
/// <remarks>
///     <para>
///     The layout is what a live capture shows: nine bags under one bag type, plus a second,
///     empty bag type. What the bags hold is the mirror image of the real inventory - one slot
///     per carried item (guid and all) and one per stackable (guidless, with its quantity) -
///     where the previous hardcoded snapshot instead replayed one developer's frozen inventory,
///     eating 207 of the 225 slots the client believed existed and leaving trading dead for any
///     inventory bigger than the leftovers.
///     </para>
///     <para>
///     Bag length adapts to the content: the authentic 25 while everything fits, rounded up in
///     steps of <see cref="Columns" /> when it does not, so a large inventory always renders with
///     the slack of the rounding (and purchases keep fitting until the next bag-model sync).
///     </para>
/// </remarks>
public static class BagInventoryLayout
{
    /// <summary>How many bags the bag type carries, from the live capture.</summary>
    public const int NumberOfBags = 9;

    /// <summary>The authentic bag length (a 5x5 grid per bag).</summary>
    public const int DefaultBagLength = 25;

    /// <summary>Bag lengths round up to this grid width so the UI rows stay whole.</summary>
    public const int Columns = 5;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>One occupied slot of the bag model, as the client's JSON spells it.</summary>
    /// <param name="ItemGuid">The item's guid, or 0 for a stackable (resource pool).</param>
    /// <param name="ItemSdbId">The <c>dbitems::RootItem</c> id.</param>
    /// <param name="Quantity">1 for a carried item; the stack size for a stackable.</param>
    public sealed record BagSlot(ulong ItemGuid, uint ItemSdbId, uint Quantity);

    /// <summary>The bag length every bag of the model gets for the given slot count.</summary>
    /// <param name="slotCount">How many slots the content needs in total.</param>
    /// <returns>25 while the default nine bags hold everything, otherwise the next size whose rows stay whole.</returns>
    public static int BagLengthFor(int slotCount)
    {
        if (slotCount <= NumberOfBags * DefaultBagLength)
        {
            return DefaultBagLength;
        }

        var needed = (slotCount + NumberOfBags - 1) / NumberOfBags;
        return ((needed + Columns - 1) / Columns) * Columns;
    }

    /// <summary>The total slot count a model with the given bag length offers.</summary>
    public static int CapacityFor(int bagLength) => NumberOfBags * bagLength;

    /// <summary>
    ///     Builds the JSON payload of a <c>BagInventoryUpdate</c> for the player's real inventory:
    ///     the nine-bag definition and, mirrored into slots, every carried item and stackable.
    /// </summary>
    /// <param name="slots">The occupied slots, items first (guid set), stackables after (guid 0).</param>
    /// <returns>The JSON string for <c>BagInventoryUpdate.Data</c>.</returns>
    public static string BuildUpdateJson(IReadOnlyList<BagSlot> slots)
    {
        var definitions = new BagDefinition[NumberOfBags];
        for (var i = 0; i < definitions.Length; i++)
        {
            definitions[i] = new BagDefinition { Name = string.Empty, Length = BagLengthFor(slots.Count), AcceptTypes = [] };
        }

        var update = new BagUpdate
        {
            Version = 2,
            BagTypes =
            [
                new BagType { Definitions = definitions, Slots = slots },
                new BagType { Definitions = [], Slots = [] },
            ],
        };

        return JsonSerializer.Serialize(update, _jsonOptions);
    }

    /// <summary>The root of the <c>BagInventoryUpdate</c> JSON.</summary>
    private sealed class BagUpdate
    {
        public int Version { get; set; }
        public BagType[] BagTypes { get; set; }
    }

    private sealed class BagType
    {
        public BagDefinition[] Definitions { get; set; }
        public IReadOnlyList<BagSlot> Slots { get; set; }
    }

    private sealed class BagDefinition
    {
        public string Name { get; set; }
        public int Length { get; set; }
        public string[] AcceptTypes { get; set; }
    }
}
