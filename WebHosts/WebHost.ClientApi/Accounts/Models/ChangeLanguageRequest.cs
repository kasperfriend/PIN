using System.Text.Json.Serialization;

namespace WebHost.ClientApi.Accounts.Models;

/// <summary>Body of <c>POST api/v2/accounts/change_language</c>, e.g. <c>{"language": "en"}</c>.</summary>
public class ChangeLanguageRequest
{
    [JsonPropertyName("language")]
    public string Language { get; set; }
}
