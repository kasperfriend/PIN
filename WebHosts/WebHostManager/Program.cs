using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Shared.Web;
using Shared.Web.Config;
using WebHost.Chat;
using WebHost.GameServerApi;

namespace WebHostManager;

internal static class Program
{
    /// <summary>Switch that installs PIN's own certificate in this machine's trust store and exits.</summary>
    private const string TrustCertificateSwitch = "--trust-cert";

    /// <summary>Switch that pairs with <see cref="TrustCertificateSwitch"/>: write the local machine store, not the user's.</summary>
    private const string MachineStoreSwitch = "--machine";

    private static readonly IEnumerable<Type> HostTypes = new[]
                                                          {
                                                              typeof(WebServer), typeof(WebHost.CatchAll.WebServer), typeof(WebHost.ClientApi.WebServer), typeof(WebHost.OperatorApi.WebServer), typeof(WebHost.InGameApi.WebServer), typeof(WebHost.Store.WebServer),
                                                              typeof(WebHost.Replay.WebServer), typeof(WebHost.Market.WebServer), typeof(WebHost.WebAsset.WebServer)
                                                          };

    private static IConfiguration Configuration { get; } = new ConfigurationBuilder()
                                                           .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "config"))
                                                           .AddJsonFile("appsettings.json", false, true)
                                                           .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", true)
                                                           .AddEnvironmentVariables()
                                                           .Build();

    private static Firefall FirefallConfig => Configuration.GetSection("Firefall").Get<Firefall>() ?? new Firefall();

    private static int Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
                     .ReadFrom.Configuration(Configuration)
                     .CreateLogger();

        try
        {
            // `--trust-cert` is a one-shot maintenance command, not a server: a client on this machine that
            // has to validate the https URLs PIN advertises needs PIN's own certificate in the Windows trust
            // store, and nothing else writes to a trust store without being asked. See TlsCertificateStore.
            if (args.Any(a => string.Equals(a, TrustCertificateSwitch, StringComparison.OrdinalIgnoreCase)))
            {
                return TrustCertificate(args.Any(a => string.Equals(a, MachineStoreSwitch, StringComparison.OrdinalIgnoreCase)));
            }

            Log.Information("Starting Web Hosts");

            // The traps a player blames on the network instead of on this configuration, said before anyone
            // starts opening firewalls: the client refuses an insecure oracle URL, and it cannot validate a
            // certificate this machine made up for an address it did not ask for.
            var urls = new PublicUrls(FirefallConfig);
            var warning = urls.ClientWarning();
            if (warning != null)
            {
                Log.Warning(warning);
            }

            // Resolved here rather than only inside the hosts, so the advice is about the very certificate the
            // hosts are about to serve with - and so that it is issued once, before the nine hosts start.
            var certificate = BaseWebServer.ResolveCertificate(Configuration);
            var advice = certificate.TrustInstructions(urls.PlainUrl(PublicUrls.OperatorApiHost));
            if (advice.Length > 0)
            {
                Log.Warning(advice);
            }

            var ct = CancellationToken.None;

            var hostsTasks = StartHosts(ct).ToList();

            // The GRPC GameServerAPI is hosted separately from the REST web hosts
            // because it needs HTTP/2 cleartext rather than the shared TLS setup.
            hostsTasks.Add(GameServerApiHost.Build(Configuration).RunAsync(ct));

            Log.Information("All Web Hosts started, waiting for all to stop or break/kill signal. (Ctrl-c on Windows)");

            Task.WaitAll(hostsTasks.ToArray(), ct);

            return 0;
        }
        catch (Exception ex)
        {
            LogBindAdvice(ex);
            Log.Fatal(ex, "Host terminated unexpectedly");

            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    ///     Installs the public half of PIN's own certificate in this machine's Trusted Root store - the
    ///     step that makes a client on the server machine accept the https URLs the server advertises.
    ///     <c>--machine</c> writes the local machine store (every account on the box, needs administrator)
    ///     instead of the current user's.
    /// </summary>
    /// <param name="localMachine">Whether to write the local machine store rather than the current user's.</param>
    /// <returns>0 when the certificate is in the store, 1 when it is not (and the console says why).</returns>
    private static int TrustCertificate(bool localMachine)
    {
        // Resolving the certificate is what issues it, so a first run of this command on a fresh server both
        // creates the file and puts it where the client will look for it.
        var certificate = BaseWebServer.ResolveCertificate(Configuration);
        if (!certificate.TryTrustOnThisMachine(localMachine, out var message))
        {
            // Written to the console as well as the log: the default log level hides anything below a
            // warning, and a command somebody ran by hand has to answer in the terminal he is looking at.
            Log.Error(message);
            Console.WriteLine(message);
            return 1;
        }

        Log.Information(message);
        Console.WriteLine(message);
        return 0;
    }

    /// <summary>
    ///     Turns the wall of identical <c>Hosting failed to start</c> stack traces a refused bind produces
    ///     into one line that says what Windows means by the socket error and what to do about it. A bind
    ///     Windows refuses is a problem of the machine, not of the network: 10013 (access denied) - WinNAT's
    ///     reserved port ranges, a leftover elevated instance, antivirus - and 10048 (address in use) -
    ///     another program already on the ports - cover nearly every crash of this kind. See the
    ///     troubleshooting section of the README.
    /// </summary>
    /// <param name="exception">What waiting for the hosts threw: one bind exception per host that failed.</param>
    private static void LogBindAdvice(Exception exception)
    {
        var aggregate = exception as AggregateException;
        IEnumerable<Exception> candidates = aggregate == null ? new[] { exception } : aggregate.InnerExceptions;
        var socketErrors = candidates.OfType<SocketException>()
                                     .GroupBy(socket => socket.SocketErrorCode)
                                     .ToList();

        if (socketErrors.Count == 0)
        {
            return;
        }

        var urls = FirefallConfig.WebHosts.Values
                                         .Where(host => !string.IsNullOrEmpty(host?.Urls))
                                         .SelectMany(host => host.Urls.Split(";", StringSplitOptions.RemoveEmptyEntries))
                                         .Distinct();
        var ports = $"{string.Join(" ", urls)} http://*:{Configuration.GetValue("Firefall:GameServerApi:Port", GameServerApiHost.DefaultPort)}";

        foreach (var socketError in socketErrors)
        {
            switch (socketError.Key)
            {
                case SocketError.AccessDenied:
                    Log.Error($"The hosts could not bind their ports - Windows refused the listen itself (socket error 10013, access denied) for {socketError.Count()} of them, and every stack trace above says only that. PIN listens on {ports} (the Firefall:WebHosts urls of config\\appsettings.json, plus the GRPC GameServerAPI port); nothing is wrong with the network, and the ports need not even be in use. What refuses a bind on Windows, in the order to check it: (1) a reserved port range - Hyper-V, WSL2 and Docker Desktop (WinNAT) reserve chunks of the TCP port space at every boot and refuse every bind into them, administrator or not, and the ranges move after a reboot, which is why a server that worked yesterday refuses to start today: `netsh interface ipv4 show excludedportrange protocol=tcp` lists them, `net stop winnat` frees them for this start, and run once as administrator while the ports are free, `netsh int ipv4 add excludedportrange protocol=tcp startport=4400 numberofports=16 store=persistent` (and the same for startport=44300) takes them out of the lottery for good; (2) a leftover WebHostManager.exe still running as administrator, which keeps every port it holds away from a normal start - end it in Task Manager and start again; (3) antivirus or a firewall that only lets signed executables open listening sockets - add an exclusion for PIN.");
                    break;
                case SocketError.AddressAlreadyInUse:
                    Log.Error($"The hosts could not bind their ports - another program is already listening on them (socket error 10048, address already in use) for {socketError.Count()} of them. PIN listens on {ports}. `netstat -ano | findstr :4400` names the process id of whatever holds a port, `tasklist /fi \"PID eq <pid>\"` its program - often a WebHostManager.exe from an earlier start that never quite exited.");
                    break;
                default:
                    Log.Error($"The hosts could not bind their ports - Windows answered {socketError.Count()} of the listens with socket error {(int)socketError.Key} ({socketError.Key}). PIN listens on {ports}.");
                    break;
            }
        }
    }

    private static IEnumerable<Task> StartHosts(CancellationToken ct)
    {
        return HostTypes.Select(t => BaseWebServer.Build(t, Configuration).RunAsync(ct)).ToList();
    }
}
