using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Common.Accounts;
using Shared.Web;

namespace WebHost.CatchAll;

public class WebServer : BaseWebServer
{
    public WebServer(IConfiguration configuration)
        : base(configuration)
    {
    }

    protected override void ConfigureChildServices(IServiceCollection services)
    {
        // This host stands in for the client's WebAccounts service (among
        // others), so it can receive account creations: open the same store the
        // ClientApi host uses (idempotent, and the same path when unconfigured).
        AccountStore.Init(Configuration.GetValue<string>("Firefall:Accounts:AccountStorePath"));
    }

    protected override void ConfigureChild(IApplicationBuilder app, IWebHostEnvironment env)
    {
    }
}
