using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Serilog;

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
///
/// Because the client's request is only ever observed through the server, the
/// reading of it is deliberately forgiving: a body that is not JSON is read as
/// a form body, and every attempt is logged with the (password-redacted) body it
/// arrived with, so a creation that does not behave can be diagnosed from the
/// server log alone instead of by guessing what the client sent.
/// </summary>
public static class AccountCreation
{
    /// <summary>The last path segment of every account creation URL, whatever service or prefix the client addresses it with.</summary>
    private const string CreationPathSuffix = "accounts";

    /// <summary>How much of a swallowed request body is worth logging.</summary>
    private const int MaxLoggedBodyLength = 4096;

    /// <summary>
    /// Lenient reading of the request body: the field names are pinned by
    /// <see cref="JsonPropertyNameAttribute"/>, but a caller that sends
    /// <c>email_optin</c> as a string, or a differently cased name, still binds.
    /// </summary>
    private static readonly JsonSerializerOptions RequestOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new LenientBooleanConverter() }
    };

    /// <summary>Matches the password fields of a JSON body, so they can be hidden in a log line.</summary>
    private static readonly Regex JsonPasswordPattern =
        new("\"(?<name>confirm_password|password)\"\\s*:\\s*\"[^\"]*\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Matches the password fields of a form body, so they can be hidden in a log line.</summary>
    private static readonly Regex FormPasswordPattern =
        new("(?<name>confirm_password|password)=[^&]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Read the raw body of a create-account request. Returns null when it is
    /// empty or carries no account data at all, so the caller can answer with
    /// the client's own error shape instead of a framework error page the client
    /// cannot parse.
    /// </summary>
    /// <remarks>
    /// JSON is tried first and a form body second: the client is believed to
    /// post JSON (that is what the reference implementation binds), but a form
    /// body is read rather than rejected because the only way to tell what the
    /// client really posts is to accept both and look at the log.
    /// </remarks>
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
            Log.Warning(ex, "An account creation request body could not be read as JSON; reading it as a form body instead");
            return ParseForm(body);
        }
    }

    /// <summary>
    /// Read a create-account request from a <c>application/x-www-form-urlencoded</c>
    /// body (<c>email=...&amp;password=...</c>). Returns null for anything that
    /// does not carry at least an email or a password field, so a JSON body that
    /// merely happens to contain an '=' is not mistaken for a form.
    /// </summary>
    public static AccountCreationRequest ParseForm(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || body.IndexOf('=') < 0)
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in body.Split('&'))
        {
            if (pair.Length == 0)
            {
                continue;
            }

            var equals = pair.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            var name = WebUtility.UrlDecode(pair.Substring(0, equals));

            // '+' is a space in a form body; UrlDecode alone leaves it.
            var value = WebUtility.UrlDecode(pair.Substring(equals + 1).Replace('+', ' '));
            values[name] = value;
        }

        if (!values.ContainsKey("email") && !values.ContainsKey("password"))
        {
            return null;
        }

        return new AccountCreationRequest
        {
            Email = values.TryGetValue("email", out var email) ? email : null,
            ConfirmEmail = values.TryGetValue("confirm_email", out var confirmEmail) ? confirmEmail : null,
            Password = values.TryGetValue("password", out var password) ? password : null,
            ConfirmPassword = values.TryGetValue("confirm_password", out var confirmPassword) ? confirmPassword : null,
            Birthday = values.TryGetValue("birthday", out var birthday) ? birthday : null,
            Country = values.TryGetValue("country", out var country) ? country : null,
            EmailOptIn = IsTruthy(values.TryGetValue("email_optin", out var optIn) ? optIn : null),
            ReferralKey = values.TryGetValue("referral_key", out var referralKey) ? referralKey : null
        };
    }

    /// <summary>
    /// Whether <paramref name="path"/> is an account creation URL: any path whose
    /// last segment is <c>accounts</c> (<c>/api/v2/accounts</c>,
    /// <c>/clientapi/api/v2/accounts</c>, <c>/accounts/</c>, ...).
    /// </summary>
    /// <remarks>
    /// The host that stands in for the client's WebAccounts service cannot know
    /// which prefix the client addresses its creation form with, and answering
    /// the wrong one with the catch-all's empty <c>200</c> is read by the client
    /// as "account created" — so it matches on the segment that is certain.
    /// Sub-paths of the collection (<c>/accounts/login</c>) are not creations.
    /// </remarks>
    public static bool IsCreationPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.TrimEnd('/');
        var separator = trimmed.LastIndexOf('/');
        var lastSegment = separator < 0 ? trimmed : trimmed.Substring(separator + 1);

        return string.Equals(lastSegment, CreationPathSuffix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Hide the password fields of a request body so it can be logged. PIN never
    /// stores a plaintext password, and a log line is not a place to start.
    /// </summary>
    public static string RedactForLog(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        var redacted = JsonPasswordPattern.Replace(body, "\"${name}\": \"***\"");
        redacted = FormPasswordPattern.Replace(redacted, "${name}=***");

        return redacted.Length <= MaxLoggedBodyLength
                   ? redacted
                   : redacted.Substring(0, MaxLoggedBodyLength) + "... (truncated)";
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
    /// Validate and store a create-account request. The fresh account starts
    /// with no characters — they arrive through the client's character creation
    /// flow — so nothing is seeded for it here. Every failure is reported with
    /// the original client error code, so the client can show its own localized
    /// message.
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

        Log.Information("Created account {AccountId} ({Email})", account.AccountId, account.Email);

        return true;
    }

    /// <summary>Whether the client actually sent this field (null, empty and whitespace-only count as not sent).</summary>
    private static bool HasValue(string value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>Read a boolean the way a form body spells it (<c>true</c>, <c>1</c>, <c>on</c>, <c>yes</c>).</summary>
    private static bool IsTruthy(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               value == "1";
    }

    /// <summary>
    /// Reads a JSON boolean that arrives as a string (<c>"email_optin": "true"</c>)
    /// or as a number as well. <see cref="JsonNumberHandling.AllowReadingFromString"/>
    /// only widens numbers, so without this a stringified flag would fail the
    /// whole deserialization and reject the creation.
    /// </summary>
    private sealed class LenientBooleanConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.String:
                    return IsTruthy(reader.GetString());
                case JsonTokenType.Number:
                    return reader.TryGetInt64(out var number) && number != 0;
                case JsonTokenType.Null:
                    return false;
                default:
                    throw new JsonException($"Cannot read a boolean from a JSON {reader.TokenType} token");
            }
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }
    }
}
