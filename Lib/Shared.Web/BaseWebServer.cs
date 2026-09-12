using System;
using System.IO;
using System.Net;
using System.Security.Authentication;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Shared.Common;
using Shared.Common.Certificates;
using Shared.Web.Config;

namespace Shared.Web;

public abstract class BaseWebServer
{
    /// <summary>Guards <see cref="CachedCertificate"/>; the hosts of one process are built one after another.</summary>
    private static readonly object CertificateLock = new();

    /// <summary>
    ///     The certificate every web host of this process serves with, and the public half of it every host
    ///     hands out to players. Resolved once - see <see cref="ResolveCertificate"/>.
    /// </summary>
    private static TlsCertificate CachedCertificate;

    protected BaseWebServer(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public static IHost Build(Type serverType, IConfiguration configuration)
    {
        try
        {
            if (serverType.FullName == null)
            {
                throw new ArgumentNullException(nameof(serverType.FullName));
            }

            Log.Information($"Starting web host {serverType.FullName}");

            // The TLS certificate of the https endpoints: a configured .pfx, the one PIN issued for the
            // advertised address, or none (then Kestrel's development certificate). See TlsCertificateStore.
            var certificate = ResolveCertificate(configuration);

            #pragma warning disable SYSLIB0039 // TLS 1.0 required
            var hostBuilder =
                Host.CreateDefaultBuilder()
                    .ConfigureWebHostDefaults(webBuilder =>
                                              {
                                                  webBuilder.UseKestrel((_, serverOpts) =>
                                                                        {
                                                                            serverOpts.ConfigureHttpsDefaults(opts =>
                                                                                                              {
                                                                                                                  opts.SslProtocols = SslProtocols.Tls | // Required by FF itself *sigh*, completely insecure
                                                                                                                                      SslProtocols.Tls12 |
                                                                                                                                      SslProtocols.Tls13;

                                                                                                                  // A client connecting over an IP address (LAN, VPN) can
                                                                                                                  // never validate the development certificate, which is
                                                                                                                  // issued for "localhost" - serve the certificate PIN
                                                                                                                  // issued for the advertised address instead (or the one
                                                                                                                  // that was configured for it). See Docs/REMOTE_PLAY.md.
                                                                                                                  if (certificate.HasCertificate)
                                                                                                                  {
                                                                                                                      opts.ServerCertificate = certificate.Certificate;
                                                                                                                  }
                                                                                                              });
                                                                        })
                                                            .UseStartup(serverType)
                                                            // Bind addresses come from config: "*" (the default in appsettings.json) listens on
                                                            // every interface - IPv4 0.0.0.0 and IPv6 [::] - so a host serves both the local
                                                            // client on "localhost" and players reaching it over LAN/VPN. Which address clients
                                                            // are *told* to use is decided separately by Firefall:PublicHost (see PublicUrls).
                                                            .UseUrls(configuration.GetSection("Firefall")
                                                                                  .Get<Firefall>()
                                                                                  .WebHosts[serverType.FullName.Replace(".WebServer", string.Empty)]
                                                                                  .Urls.Split(";"));
                                              })
                    .ConfigureHostConfiguration(hostConfigurationBuilder =>
                                                {
                                                    hostConfigurationBuilder.AddConfiguration(configuration);
                                                })
                    .ConfigureServices(serviceConfigurationBuilder =>
                                       {
                                           // ASP.NET Core routing has no built-in 'ulong' inline
                                           // route constraint, but the api/v3 controllers use
                                           // templates like {characterId:ulong}. Registering the
                                           // constraint here (for every web host) is load-bearing:
                                           // without it the endpoint matcher fails to build and
                                           // each request to the host 500s — the login flow of
                                           // WebHost.ClientApi included.
                                           serviceConfigurationBuilder.Configure<RouteOptions>(options =>
                                               options.ConstraintMap["ulong"] = typeof(ULongRouteConstraint));

                                           // The Firefall singleton is what PublicUrls and the hosts build their
                                           // advertised addresses from; the certificate alongside it is what the
                                           // operator host hands out to players (CertificateController).
                                           serviceConfigurationBuilder.AddSingleton(configuration.GetSection("Firefall")
                                                                                                 .Get<Firefall>() ?? new Firefall())
                                                                      .AddSingleton(certificate)
                                                                      .AddSwaggerGen()
                                                                      .AddControllers()
                                                                      .AddJsonOptions(options =>
                                                                                      {
                                                                                          options.JsonSerializerOptions
                                                                                                 .PropertyNamingPolicy = new SnakeCasePropertyNamingPolicy();
                                                                                      });
                                       })
                    .UseSerilog();
            #pragma warning restore SYSLIB0039
            return hostBuilder.Build();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");

            return null;
        }
    }

    /// <summary>
    ///     The certificate this process's hosts serve with: PIN's own (issued for the address the clients are
    ///     told to dial, or the <c>.pfx</c> configured over it) or none, in which case Kestrel keeps using the
    ///     ASP.NET Core development certificate - which is what a <c>localhost</c> server has always done, and
    ///     what a client dialling an address cannot validate. See <c>TlsCertificateStore</c>.
    /// </summary>
    /// <remarks>
    ///     Resolved once per process: <c>WebHostManager</c> builds nine hosts on the same configuration, all of
    ///     them must serve the same certificate, and the key and certificate files it keeps on disk are only
    ///     ever written by whoever gets there first. A host started with a different configuration - a
    ///     standalone <c>WebHost.&lt;host&gt;</c> binary - resolves its own.
    /// </remarks>
    /// <param name="configuration">The configuration the host is built from.</param>
    /// <returns>The certificate to serve, never <c>null</c> (a certificate-less one means "keep the dev certificate").</returns>
    public static TlsCertificate ResolveCertificate(IConfiguration configuration)
    {
        var config = configuration.GetSection("Firefall").Get<Firefall>() ?? new Firefall();
        var configured = config.Certificate ?? new FirefallCertificate();
        var urls = new PublicUrls(config);
        lock (CertificateLock)
        {
            return CachedCertificate ??= TlsCertificateStore.Resolve(
                urls.UseHttps,
                configured.AutoIssue,
                configured.Path,
                configured.Password,
                configured.StorePath,
                urls.Host);
        }
    }

    public void ConfigureServices(IServiceCollection services)
    {
        ConfigureChildServices(services);
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
                         {
                             c.SwaggerEndpoint("/swagger/v1/swagger.json", "Firefall API");
                         });

        // log requests which produce 404 responses so we can fill in the missing endpoints
        app.Use(async (context, next) =>
                {
                    context.Request.EnableBuffering();
                    await next();
                    if (context.Response.StatusCode == (int)HttpStatusCode.NotFound)
                    {
                        var req = context.Request;
                        using var streamReader =
                            new StreamReader(req.Body, Encoding.UTF8, true, 1024, true);
                        Log.Error($"NotFound: {req.GetEncodedUrl()} ;; {req.Method} ;; {await streamReader.ReadToEndAsync()}");
                        req.Body.Position = 0;
                        await next();
                    }
                });

        if (env.IsDevelopment())
        {
            _ = app.UseDeveloperExceptionPage();
        }

        // Both ports answer what they are asked for: the http endpoints are as functional as the TLS ones,
        // and the client dials whatever the configuration advertises (Firefall:AdvertiseHttps), so bouncing
        // plain requests to the TLS port is not what makes https work - it only hides the http half. It is
        // opt-in now, and even when it is on the certificate download routes stay plain: fetching the
        // certificate a client has to trust is the one request that cannot be conditioned on having
        // trusted it already. See TlsCertificateStore and Docs/REMOTE_PLAY.md.
        if ((Configuration.GetSection("Firefall").Get<Firefall>() ?? new Firefall()).RedirectHttpToHttps)
        {
            _ = app.UseWhen(context => !IsCertificateRequest(context), builder => builder.UseHttpsRedirection());
        }

        _ = app.UseSerilogRequestLogging()
               .UseRouting()
               .UseEndpoints(endpoints => { _ = endpoints.MapControllers(); });

        ConfigureChild(app, env);
    }

    protected virtual void ConfigureChildServices(IServiceCollection services)
    {
        services.AddSwaggerGen();
    }

    protected virtual void ConfigureChild(IApplicationBuilder app, IWebHostEnvironment env)
    {
    }

    /// <summary>
    ///     Whether a request is one of the certificate downloads, which answer in the clear even with
    ///     <c>Firefall:RedirectHttpToHttps</c> on - see <c>TlsCertificate.CerRoute</c>.
    /// </summary>
    /// <param name="context">The request being routed.</param>
    /// <returns><c>true</c> when the request must not be redirected to TLS.</returns>
    private static bool IsCertificateRequest(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path.Equals(TlsCertificate.CerRoute, StringComparison.OrdinalIgnoreCase) ||
               path.Equals(TlsCertificate.PemRoute, StringComparison.OrdinalIgnoreCase);
    }
}
