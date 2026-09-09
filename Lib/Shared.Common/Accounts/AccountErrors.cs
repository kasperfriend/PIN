namespace Shared.Common.Accounts;

/// <summary>
/// Error codes the original Firefall client understands in web API error
/// responses (<c>{"code": "...", "message": "..."}</c> with HTTP 500). The
/// client maps these codes to its own localized strings, so the exact spelling
/// matters. Subset relevant to account flows, taken from the client's error
/// string table (see RIN.WebAPI's <c>Error.Codes</c>).
/// </summary>
public static class AccountErrors
{
    /// <summary>Login failed — unknown account or wrong password.</summary>
    public const string ErrIncorrectUserPass = "ERR_INCORRECT_USERPASS";

    /// <summary>An account with this email already exists.</summary>
    public const string ErrAccountExists = "ERR_ACCOUNT_EXISTS";

    /// <summary>The two email fields of the creation form do not match.</summary>
    public const string ErrEmailMismatch = "ERR_EMAIL_MISMATCH";

    /// <summary>The two password fields of the creation form do not match.</summary>
    public const string ErrPasswordMismatch = "ERR_PASSWORD_MISMATCH";

    /// <summary>No email address was provided.</summary>
    public const string ErrNoEmail = "ERR_NO_EMAIL";

    /// <summary>The account is locked.</summary>
    public const string ErrAccountLocked = "ERR_ACCOUNT_LOCKED";

    /// <summary>Catch-all for errors without a dedicated code.</summary>
    public const string ErrUnknown = "ERR_UNKNOWN";
}
