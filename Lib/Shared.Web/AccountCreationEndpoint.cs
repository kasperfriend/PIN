using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shared.Common.Accounts;

namespace Shared.Web;

/// <summary>
///     The client's account creation endpoint (<c>POST api/v2/accounts</c>),
///     shared by every web host that can receive it: the client talks to the
///     ClientApi host for its in-game API, but the operator capability response
///     (<c>WebHost.OperatorApi</c>) advertises the catch-all host as the
///     client's WebAccounts service, and a creation form posted there has to
///     work just the same.
///
///     The request body is read by hand rather than through
///     <c>[FromBody]</c> model binding: the endpoint must never be rejected by
///     the framework (an unsupported or missing content type would answer
///     <c>415</c>/<c>400</c> with a problem-details body the Firefall client
///     cannot parse, which left it waiting for an answer forever). Whatever the
///     request looks like, the answer is JSON the client understands — the empty
///     object <c>{}</c> the original service returned on success, or
///     <c>{"code": ..., "message": ...}</c> with HTTP 500 for one of the client's
///     own error codes.
///
///     Every request is logged at <c>Warning</c> — the level the WebHostManager
///     shows by default — with the password-redacted body it arrived with. A
///     creation the server never answers, or answers with an error the client
///     cannot show, is otherwise indistinguishable in the log from a creation
///     the client never sent, and the client sits on its creation form in both
///     cases.
/// </summary>
public static class AccountCreationEndpoint
{
    /// <summary>Handle a <c>POST api/v2/accounts</c> request.</summary>
    /// <param name="request">The incoming request (its body is the creation form).</param>
    /// <param name="logger">Logger of the serving controller.</param>
    public static async Task<IActionResult> HandleAsync(HttpRequest request, ILogger logger)
    {
        try
        {
            var body = await ReadBodyAsync(request);

            logger.LogWarning(
                "Account creation request {Method} {Path} on {Host} ({ContentType}, {Length} characters): {Body}",
                request.Method,
                request.Path,
                request.Host.Value,
                string.IsNullOrEmpty(request.ContentType) ? "(no content type)" : request.ContentType,
                body?.Length ?? 0,
                AccountCreation.RedactForLog(body));

            if (!AccountCreation.TryCreate(AccountCreation.Parse(body), out var account, out var errorCode, out var errorMessage))
            {
                logger.LogWarning("Rejected the account creation request: {Code} ({Message})", errorCode, errorMessage);
                return Error(errorCode, errorMessage);
            }

            logger.LogWarning("Created account {AccountId} ({Email}); it starts fresh — no characters until you create one in-game", account.AccountId, account.Email);

            return new OkObjectResult(new { });
        }
        catch (Exception ex)
        {
            // The client waits for an answer it can parse; an exception escaping
            // here would answer with an empty 500 (or nothing at all) and leave
            // it frozen on the creation form.
            logger.LogError(ex, "An account creation request failed unexpectedly");
            return Error(AccountErrors.ErrUnknown, "The account could not be created");
        }
    }

    /// <summary>
    /// Read the raw request body, leaving the stream rewound so the rest of the
    /// pipeline (the 404 logger, for one) can read it too.
    /// </summary>
    /// <param name="request">The request to read.</param>
    public static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        // The 404 logging middleware already enables buffering; doing it here
        // too keeps the endpoint independent of the host's pipeline.
        request.EnableBuffering();

        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        return body;
    }

    private static ObjectResult Error(string code, string message)
    {
        return new ObjectResult(new { code, message })
               {
                   StatusCode = StatusCodes.Status500InternalServerError
               };
    }
}
