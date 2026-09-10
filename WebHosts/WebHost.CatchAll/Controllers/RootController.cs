using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shared.Common.Accounts;
using Shared.Web;

namespace WebHost.CatchAll.Controllers;

[ApiController]
public class RootController : ControllerBase
{
    /// <summary>Above this a swallowed body is not worth reading just to log it.</summary>
    private const int MaxLoggedBodyBytes = 64 * 1024;

    private readonly ILogger<RootController> _logger;

    public RootController(ILogger<RootController> logger)
    {
        _logger = logger;
    }

    [Route("products.json")]
    [HttpGet]
    [Produces("application/json")]
    public object Products()
    {
        return new Products() { Test = new Array[] { } };
    }

    /// <summary>
    ///     Everything else the client asks of the web services this host stands
    ///     in for (WebAccounts, Frontend, Store, Market, Replay, Web, ...): an
    ///     empty 200 keeps the client happy until the endpoint is implemented.
    /// </summary>
    /// <remarks>
    ///     A request that would change something is logged with the (password
    ///     redacted) body it arrived with, because the client reads the empty
    ///     200 as success — which is how an account creation posted to the
    ///     WebAccounts host was swallowed whole before this host served
    ///     <c>POST api/v2/accounts</c> itself.
    /// </remarks>
    [Route("{*url}", Order = 999)]
    public async Task<IActionResult> CatchAll()
    {
        // This host is what the operator capability response advertises as the
        // client's WebAccounts service, so it is a door the client's account
        // creation form can come through. Which path prefix the client addresses
        // that service with is not something the server can pin down from the
        // outside, and answering the wrong one with the catch-all's empty 200 is
        // the worst possible answer: the client reads it as "account created"
        // and then cannot log in to an account that was never stored. So every
        // POST to a path ending in "accounts" is served as a creation.
        if (HttpMethods.IsPost(Request.Method) && AccountCreation.IsCreationPath(Request.Path))
        {
            return await AccountCreationEndpoint.HandleAsync(Request, _logger);
        }

        if (!HttpMethods.IsGet(Request.Method) && !HttpMethods.IsHead(Request.Method))
        {
            // Only small bodies are read for the log: this host stands in for
            // services that could in principle be handed an upload.
            var body = Request.ContentLength.HasValue && Request.ContentLength.Value > 0 && Request.ContentLength.Value < MaxLoggedBodyBytes
                           ? AccountCreation.RedactForLog(await AccountCreationEndpoint.ReadBodyAsync(Request))
                           : $"(not read, Content-Length: {Request.ContentLength?.ToString() ?? "unknown"})";

            _logger.LogWarning(
                "Catch-all host swallowed {Method} {Path} ({ContentType}): {Body}; the client reads the empty 200 as success",
                Request.Method,
                Request.Path,
                string.IsNullOrEmpty(Request.ContentType) ? "(no content type)" : Request.ContentType,
                body);
        }

        return Ok();
    }
}

public class Products
{
    public Array Test { get; set; }
}
