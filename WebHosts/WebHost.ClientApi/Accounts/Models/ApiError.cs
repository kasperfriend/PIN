namespace WebHost.ClientApi.Accounts.Models;

/// <summary>
/// Error body the original client understands: <c>{"code": "ERR_...", "message": "..."}</c>.
/// The client maps the code to its own localized string, so the spelling of the
/// code is what matters. Returned with HTTP 500, matching the original service.
/// </summary>
public class ApiError
{
    public string Code { get; set; }

    public string Message { get; set; }

    /// <summary>Extra data the client may inspect; the original service always sent <c>{ "silent": false }</c>.</summary>
    public ApiErrorData ErrorData { get; set; } = new();
}

public class ApiErrorData
{
    public bool Silent { get; set; }
}
