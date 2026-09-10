namespace Shared.Common.Accounts;

/// <summary>
/// Why a login (a signed request) was not accepted by
/// <see cref="AccountStore.TryVerifyLogin"/>. The client is told the same
/// <c>ERR_INCORRECT_USERPASS</c> for all of them, but the server log needs the
/// difference: an unknown account means no account with that email was ever
/// stored (a creation that never landed), while a signature mismatch means the
/// account exists and the password is wrong.
/// </summary>
public enum LoginFailure
{
    /// <summary>The login was accepted.</summary>
    None = 0,

    /// <summary>The request carried no <c>X-Red5-Signature</c> header at all.</summary>
    MissingSignature,

    /// <summary>The signature header could not be parsed (malformed, or without a uid).</summary>
    MalformedSignature,

    /// <summary>No account owns the uid in the signature.</summary>
    UnknownAccount,

    /// <summary>The account exists, but the signature does not match its secret (wrong password).</summary>
    SignatureMismatch
}
