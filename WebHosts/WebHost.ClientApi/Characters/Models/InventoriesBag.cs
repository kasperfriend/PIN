namespace WebHost.ClientApi.Characters.Models;

/// <summary>
/// A character's bag: the items and resource pools it carries next to the gear it
/// wears. A fresh character owns neither, so both are empty until something
/// earns them — the answer used to be a fixed set of items and stacks for every
/// character that asked.
/// </summary>
public class InventoriesBag
{
    public object[] Items { get; set; } = [];

    public object[] Resources { get; set; } = [];
}
