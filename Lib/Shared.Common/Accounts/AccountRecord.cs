using System;

namespace Shared.Common.Accounts;

/// <summary>
/// A persisted player account, stored in <c>accounts.json</c> by <see cref="AccountStore"/>.
///
/// The <see cref="Uid"/> and <see cref="Secret"/> fields are the Red5
/// authentication material the client derives from email and password (see
/// <see cref="Red5Auth"/>): the uid identifies the account on every signed
/// request, the secret verifies the request signature. Neither the plaintext
/// password nor anything else that could log in is stored — plus a PBKDF2
/// <see cref="PasswordHash"/> of the password so the server can also verify
/// credentials directly (password changes, tooling) without re-deriving secrets.
/// </summary>
public class AccountRecord
{
    /// <summary>Numeric account id reported to the client as <c>account_id</c>.</summary>
    public ulong AccountId { get; set; }

    /// <summary>Email/username, normalized (ASCII-lowercased, trimmed) at creation.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Red5 uid (<see cref="Red5Auth.GenerateUserId"/> of the email) — the lookup key.</summary>
    public string Uid { get; set; } = string.Empty;

    /// <summary>Red5 auth secret (<see cref="Red5Auth.GenerateSecret"/> of email + password).</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Base64(salt + PBKDF2-HMACSHA256) of the password; empty when unknown.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Whether the account may use dev-only features.</summary>
    public bool IsDev { get; set; }

    /// <summary>Whether this is the built-in seeded admin account.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Whether this account was provisioned from an opaque client login ticket
    /// (the Steam session ticket a Steam-launched client signs its requests
    /// with) instead of from email + password. Such an account is keyed to the
    /// Steam account embedded in the ticket and is reused for it, but the
    /// client's signature cannot be verified — the ticket secret is only
    /// computable with a Steam backend PIN does not have — so signatures are
    /// not checked for it (see <see cref="AccountStore.TryVerifyLogin"/>).
    /// </summary>
    public bool TicketAuth { get; set; }

    /// <summary>How many characters the account may own (the seeded zone list has one entry per zone).</summary>
    public int CharacterLimit { get; set; } = AccountStore.DefaultCharacterLimit;

    /// <summary>Preferred UI language, two letters (updated via <c>api/v2/accounts/change_language</c>).</summary>
    public string Language { get; set; } = "en";

    /// <summary>Country code from account creation, if provided.</summary>
    public string Country { get; set; }

    /// <summary>Birthday from account creation (yyyy-MM-dd), if provided.</summary>
    public string Birthday { get; set; }

    /// <summary>Whether the account opted into emails.</summary>
    public bool EmailOptIn { get; set; }

    /// <summary>Referral key used at creation, if provided.</summary>
    public string ReferralKey { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }
}
