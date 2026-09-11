using Microsoft.AspNetCore.Mvc;

namespace WebHost.ClientApi.Controllers;

/// <summary>
///     The root of the ClientApi host. The client (and whatever else resolves
///     the <c>clientapi_host</c> of the operator capability response) probes it
///     with a bare <c>GET /</c> — observed right before the login/account
///     creation flow — and the empty object the other stand-in hosts answer
///     with is the least surprising success for a request nobody pinned a
///     payload to. It used to 404, which only made the request log lie about
///     the host being broken while the client carried on regardless.
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
