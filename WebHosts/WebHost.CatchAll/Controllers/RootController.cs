using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace WebHost.CatchAll.Controllers;

[ApiController]
public class RootController : ControllerBase
{
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
    ///     A request that would change something is logged as a warning, because
    ///     the client reads the empty 200 as success — which is how an account
    ///     creation posted to the WebAccounts host was swallowed whole before
    ///     this host served <c>POST api/v2/accounts</c> itself.
    /// </remarks>
    [Route("{*url}", Order = 999)]
    public IActionResult CatchAll()
    {
        if (!HttpMethods.IsGet(Request.Method) && !HttpMethods.IsHead(Request.Method))
        {
            Log.Warning("Catch-all host swallowed {Method} {Path}; the client reads the empty 200 as success", Request.Method, Request.Path);
        }

        return Ok();
    }
}

public class Products
{
    public Array Test { get; set; }
}
