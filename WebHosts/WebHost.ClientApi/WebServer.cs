using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Common.Accounts;
using Shared.Web;
using WebHost.ClientApi.Characters;

namespace WebHost.ClientApi;

public class WebServer : BaseWebServer
{
    public WebServer(IConfiguration configuration)
        : base(configuration)
    {
    }

    protected override void ConfigureChildServices(IServiceCollection services)
    {
        // Open the account store from the configured path before the first
        // request touches it (idempotent; later callers fall back to the default
        // location next to the binary, like the character store).
        AccountStore.Init(Configuration.GetValue<string>("Firefall:Accounts:AccountStorePath"));

        services.AddScoped<ICharactersRepository, CharactersRepository>();
    }

    protected override void ConfigureChild(IApplicationBuilder app, IWebHostEnvironment env)
    {
    }
}
