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

    /// <summary>Character name already taken.</summary>
    public const string ErrNameInUse = "ERR_NAME_IN_USE";

    /// <summary>Character name violates the naming rules (umbrella code for the reason list).</summary>
    public const string ErrNameInvalid = "ERR_NAME_INVALID";

    /// <summary>Character name is too short.</summary>
    public const string ErrNameTooShort = "ERR_NAME_TOO_SHORT";

    /// <summary>Character name is too long.</summary>
    public const string ErrNameTooLong = "ERR_NAME_TOO_LONG";

    /// <summary>Character name starts with a digit.</summary>
    public const string ErrNameStartsWithNumber = "ERR_NAME_STARTS_WITH_NUMBER";

    /// <summary>Character name contains characters that are not letters, digits or spaces.</summary>
    public const string ErrInvalidCharacter = "ERR_INVALID_CHARACTER";

    /// <summary>Another custom character already occupies this account's slot for the zone.</summary>
    public const string ErrDuplicateCharacter = "ERR_DUPLICATE_CHARACTER";

    /// <summary>Catch-all for errors without a dedicated code.</summary>
    public const string ErrUnknown = "ERR_UNKNOWN";
}
