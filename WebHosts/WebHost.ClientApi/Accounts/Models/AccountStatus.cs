namespace WebHost.ClientApi.Accounts.Models;

public class AccountStatus
{
    public ulong AccountId { get; set; }

    public bool CanLogin { get; set; }

    public bool IsDev { get; set; }

    public bool SteamAuthPrompt { get; set; }

    public bool SkipPrecursor { get; set; }

    public CaisStatus CaisStatus { get; set; }

    public int CharacterLimit { get; set; }

    public bool IsVip { get; set; }

    public long VipExpiration { get; set; }

    public long CreatedAt { get; set; }

    /// <summary>Live events reported on login; the late client build expects this block.</summary>
    public LoginEvents Events { get; set; }
}
