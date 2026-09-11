using Microsoft.AspNetCore.Mvc;

namespace WebHost.InGameApi.Controllers;

/// <summary>
///     The root of the InGame host. The client probes it with a bare
///     <c>GET /</c> — observed right before the login/account creation flow,
///     alongside the same probe against the ClientApi host — and the empty
///     object the other stand-in hosts answer with is the least surprising
///     success for a request nobody pinned a payload to. It used to 404, which
///     only made the request log lie about the host being broken while the
///     client carried on regardless.
/// </summary>
[ApiController]
public class RootController : ControllerBase
{
    [Route("/")]
    [HttpGet]
    public object Root()
    {
        return new { };
    }
}
