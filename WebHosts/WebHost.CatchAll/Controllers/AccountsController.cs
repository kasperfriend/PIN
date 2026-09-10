using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shared.Web;

namespace WebHost.CatchAll.Controllers;

/// <summary>
///     Account creation, served by the catch-all host as well as by
///     <c>WebHost.ClientApi</c>.
///
///     The operator capability response (<c>WebHost.OperatorApi</c>, the
///     <c>/check</c> endpoint the client asks which service lives where) points
///     the client's WebAccounts service at this host, because PIN implements
///     neither that service nor any of the other web services the catch-all
///     stands in for. Its catch-all route answers every unimplemented request
///     with an empty <c>200</c> — so when the client posted its creation form
///     here, the client read that empty success as "account created", went on to
///     log in, and no account existed: the login answered
///     <c>ERR_INCORRECT_USERPASS</c> and the client sat in its login retry loop
///     with the game frozen instead of showing an error.
///
///     Creating the account here uses the same store as the ClientApi host (both
///     live in the WebHostManager process), so the account is playable through
///     either door.
/// </summary>
[ApiController]
public class AccountsController : ControllerBase
{
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(ILogger<AccountsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Create a new account, exactly as <c>WebHost.ClientApi</c> does. The
    ///     second route covers clients that address the API with the service
    ///     prefix in the path (<c>/clientapi/api/v2/...</c>), which is how the
    ///     reference implementation the client was reverse engineered against
    ///     hosts it.
    /// </summary>
    [Route("api/v2/accounts")]
    [Route("clientapi/api/v2/accounts")]
    [HttpPost]
    public async Task<IActionResult> CreateAccount()
    {
        return await AccountCreationEndpoint.HandleAsync(Request, _logger);
    }
}
