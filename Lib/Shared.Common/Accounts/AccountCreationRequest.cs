using System.Text.Json.Serialization;

namespace Shared.Common.Accounts;

/// <summary>
/// The body of the client's account creation request
/// (<c>POST api/v2/accounts</c>), shared by every web host that may receive it
/// (the client's ClientApi host, and the stand-in host PIN advertises for its
/// WebAccounts service).
///
/// The shipped client (beta-1869) posts <c>email</c>, <c>password</c>,
/// <c>country</c>, <c>birthday</c>, <c>email_optin</c> and <c>referral_key</c>
/// plus its Steam identity fields. It checks the two confirmation boxes of the
/// creation form in its own UI, so <c>confirm_email</c>/<c>confirm_password</c>
/// are <em>not</em> part of the request (that is the request shape the reference
/// implementation the client was reverse engineered against stores:
/// RIN.WebAPI's <c>CreateAccountReq</c>). They are accepted — and validated
/// against their partner field — when a caller does send them, but a missing one
/// is never an error; see <see cref="AccountCreation"/>.
/// </summary>
public sealed class AccountCreationRequest
{
    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("confirm_email")]
    public string ConfirmEmail { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; }

    [JsonPropertyName("confirm_password")]
    public string ConfirmPassword { get; set; }

    [JsonPropertyName("birthday")]
    public string Birthday { get; set; }

    [JsonPropertyName("country")]
    public string Country { get; set; }

    [JsonPropertyName("email_optin")]
    public bool EmailOptIn { get; set; }

    [JsonPropertyName("referral_key")]
    public string ReferralKey { get; set; }

    /// <summary>Steam session ticket of a Steam-linked creation; accepted and ignored (PIN has no Steam backend).</summary>
    [JsonPropertyName("steam_session_ticket")]
    public string SteamSessionTicket { get; set; }

    /// <summary>Steam user id of a Steam-linked creation; accepted and ignored.</summary>
    [JsonPropertyName("steam_user_id")]
    public string SteamUserId { get; set; }

    /// <summary>Steam CD key of a Steam-linked creation; accepted and ignored.</summary>
    [JsonPropertyName("steam_cdkey")]
    public string SteamCdKey { get; set; }
}
