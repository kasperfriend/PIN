using Microsoft.AspNetCore.Http;
using Shared.Common.Accounts;

namespace WebHost.ClientApi.Accounts;

/// <summary>
/// Identifies the account behind an incoming client request via its
/// <c>X-Red5-Signature</c> header: parse it, look the account up by uid and
/// verify the signature with the account's stored secret.
/// </summary>
public static class Red5AccountExtensions
{
    /// <summary>
    /// The account that signed the request, or null when the header is missing,
    /// malformed or fails verification (unknown uid / wrong password).
    /// </summary>
    public static AccountRecord TryGetRed5Account(this HttpContext context)
    {
        if (context?.Request == null)
        {
            return null;
        }

        if (!context.Request.Headers.TryGetValue(Red5Auth.SignatureHeaderName, out var values))
        {
            return null;
        }

        var header = values.Count > 0 ? values[0] : null;
        if (string.IsNullOrEmpty(header))
        {
            return null;
        }

        return AccountStore.Default.VerifyLogin(header);
    }
}
