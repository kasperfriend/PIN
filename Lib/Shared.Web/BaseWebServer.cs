using System;
using System.IO;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
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
using Shared.Web.Config;

namespace Shared.Web;

public abstract class BaseWebServer
{
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

            // Optional TLS certificate, see LoadConfiguredCertificate. Null keeps Kestrel's default, the
            // ASP.NET Core development certificate.
            var certificate = LoadConfiguredCertificate(configuration);

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
                                                                                                                  // issued for "localhost" - serve the configured one
                                                                                                                  // instead when there is one. See Docs/REMOTE_PLAY.md.
                                                                                                                  if (certificate != null)
                                                                                                                  {
                                                                                                                      opts.ServerCertificate = certificate;
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

                                           serviceConfigurationBuilder.AddSingleton(configuration.GetSection("Firefall")
                                                                                                 .Get<Firefall>() ?? new Firefall())
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

        // Advertising plain http (Firefall:AdvertiseHttps = false) only works while the http requests are
        // not bounced back to https: with a TLS address bound, UseHttpsRedirection answers every plain
        // request with a 307 to the https port, and a remote client that does not trust PIN's self-signed
        // development certificate never gets through that redirect. The hosts keep listening on both
        // either way, so switching back to https is a configuration change and nothing more.
        if (Configuration.GetSection("Firefall").Get<Firefall>()?.AdvertiseHttps ?? true)
        {
            _ = app.UseHttpsRedirection();
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
    ///     Loads the TLS certificate named by <c>Firefall:Certificate:Path</c> - a PKCS#12 file (.pfx/.p12),
    ///     decrypted with <c>Firefall:Certificate:Password</c> when it has one.
    /// </summary>
    /// <remarks>
    ///     Kestrel's default is the ASP.NET Core development certificate: issued for <c>localhost</c> and
    ///     trusted only on the machine that ran <c>dotnet dev-certs https --trust</c>, so a player who
    ///     connects over a LAN or VPN address can never validate it. Serving a certificate whose subject or
    ///     SAN matches <c>Firefall:PublicHost</c> - and trusting that certificate on each
    ///     player's machine - is what makes the https hosts work remotely; <c>Firefall:AdvertiseHttps</c>
    ///     set to false is the alternative that avoids certificates altogether. An empty path (the default)
    ///     keeps the development certificate, so a local setup is untouched. A certificate that cannot be
    ///     loaded is reported and skipped rather than taking the hosts down. See <c>Docs/REMOTE_PLAY.md</c>.
    /// </remarks>
    /// <param name="configuration">The configuration the host is built from.</param>
    /// <returns>The configured certificate, or null when none is configured or it could not be loaded.</returns>
    private static X509Certificate2 LoadConfiguredCertificate(IConfiguration configuration)
    {
        var path = configuration.GetValue<string>("Firefall:Certificate:Path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!File.Exists(path))
        {
            Log.Error("Firefall:Certificate:Path points at {Path}, which does not exist - serving the development certificate instead", path);
            return null;
        }

        try
        {
            var password = configuration.GetValue<string>("Firefall:Certificate:Password") ?? string.Empty;
            return X509CertificateLoader.LoadPkcs12FromFile(path, password);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not load the TLS certificate from {Path} - serving the development certificate instead", path);
            return null;
        }
    }
}