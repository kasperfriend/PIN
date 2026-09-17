namespace GameServer.StaticDB.Records.customdata;

public record ShowRewardScreenCommandDef : ICommandDef
{
    public uint Id { get; set; }

    /// <summary><c>dbcharacter::RewardScreenType</c>: 0 generic, 1 loot box, 2 mission, 3 bounty streak, 4 lockbox.</summary>
    public byte ScreenType { get; set; }
    public uint TitleTextId { get; set; }
}
