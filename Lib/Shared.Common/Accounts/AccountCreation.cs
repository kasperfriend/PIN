using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using Shared.Common.Characters;

namespace Shared.Common.Accounts;

/// <summary>
/// The create-account rules of the client's <c>POST api/v2/accounts</c> request,
/// kept pure of HTTP concerns (no request objects, no response shaping) so they
/// can be unit tested and shared by every web host that serves the endpoint.
///
/// Two things about the shipped client (beta-1869) drive the rules:
///
/// * It posts <c>email</c>, <c>password</c>, <c>country</c>, <c>birthday</c>,
///   <c>email_optin</c> and <c>referral_key</c> only. Its two confirmation boxes
///   are validated in the client's own UI, so the request carries no
///   <c>confirm_email</c>/<c>confirm_password</c> — that is the request shape the
///   reference implementation the client was reverse engineered against stores
///   (RIN.WebAPI's <c>CreateAccountReq</c>). Requiring the confirmation fields
///   rejected <em>every</em> account creation with <c>ERR_EMAIL_MISMATCH</c>.
/// * It never sees a usable error message for a non-JSON response, so a
///   creation has to be answered with JSON no matter how the request looks.
/// </summary>
public static class AccountCreation
{
    /// <summary>
    /// Lenient reading of the request body: the field names are pinned by
    /// <see cref="JsonPropertyNameAttribute"/>, but a caller that sends
    /// <c>email_optin</c> as a string, or a differently cased name, still binds.
    /// </summary>
    private static readonly JsonSerializerOptions RequestOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>
    /// Read the raw body of a create-account request. Returns null when it is
    /// empty or not JSON at all, so the caller can answer with the client's own
    /// error shape instead of a framework error page the client cannot parse.
    /// </summary>
    public static AccountCreationRequest Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AccountCreationRequest>(body, RequestOptions);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "An account creation request body could not be read as JSON");
            return null;
        }
    }

    /// <summary>
    /// Check a create-account request. Returns true when it may be handed to
    /// <see cref="AccountStore.TryCreate"/>; otherwise <paramref name="errorCode"/>
    /// and <paramref name="errorMessage"/> carry the original client error code
    /// and a human readable reason.
    /// </summary>
    public static bool Validate(AccountCreationRequest request, out string errorCode, out string errorMessage)
    {
        if (request == null)
        {
            errorCode = AccountErrors.ErrUnknown;
            errorMessage = "No account data received";
            return false;
        }

        return Validate(request.Email, request.ConfirmEmail, request.Password, request.ConfirmPassword, out errorCode, out errorMessage);
    }

    /// <summary>
    /// Check the email/password pair of a create-account request the way the
    /// client fills the form in: confirmations are only compared when they were
    /// actually sent, an email is required and a password is required.
    /// </summary>
    public static bool Validate(string email, string confirmEmail, string password, string confirmPassword, out string errorCode, out string errorMessage)
    {
        errorCode = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(email))
        {
            errorCode = AccountErrors.ErrNoEmail;
            errorMessage = "An email address is required";
            return false;
        }

        // Emails are compared the way they are stored: trimmed and ASCII-folded,
        // so "Me@Example.com" and "me@example.com " are the same address.
        if (HasValue(confirmEmail) &&
            !string.Equals(AccountStore.NormalizeEmail(email), AccountStore.NormalizeEmail(confirmEmail), StringComparison.Ordinal))
        {
            errorCode = AccountErrors.ErrEmailMismatch;
            errorMessage = "The email addresses do not match";
            return false;
        }

        if (string.IsNullOrEmpty(password))
        {
            errorCode = AccountErrors.ErrPasswordMismatch;
            errorMessage = "A password is required";
            return false;
        }

        // Passwords are compared verbatim — the client is case sensitive here.
        if (HasValue(confirmPassword) && !string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            errorCode = AccountErrors.ErrPasswordMismatch;
            errorMessage = "The passwords do not match";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validate and store a create-account request, and give the fresh account
    /// its zone-picker entries. Every failure is reported with the original
    /// client error code, so the client can show its own localized message.
    /// </summary>
    public static bool TryCreate(AccountCreationRequest request, out AccountRecord account, out string errorCode, out string errorMessage)
    {
        account = null;

        if (!Validate(request, out errorCode, out errorMessage))
        {
            return false;
        }

        if (!AccountStore.Default.TryCreate(
                request.Email,
                request.Password,
                request.Country,
                request.Birthday,
                request.EmailOptIn,
                request.ReferralKey,
                out account,
                out errorCode,
                out errorMessage))
        {
            return false;
        }

        SeedZonePicker(account);

        Log.Information("Created account {AccountId} ({Email})", account.AccountId, account.Email);

        return true;
    }

    /// <summary>Whether the client actually sent this field (null, empty and whitespace-only count as not sent).</summary>
    private static bool HasValue(string value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// Give the new account its own copy of the zone-picker entries, the same
    /// way the first-run seed does for the admin account. Best effort: the
    /// account is stored already, so a failure here must never turn a created
    /// account into a rejected creation (the client would show an error and then
    /// fail to log in with an account that does exist).
    /// </summary>
    private static void SeedZonePicker(AccountRecord account)
    {
        try
        {
            CharacterStore.EnsureSeededForAccount(account.AccountId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not seed the zone-picker entries of account {AccountId}; the account was created anyway", account.AccountId);
        }
    }
}
