using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Shared.Common.Characters;
using WebHost.GameServerApi.Services;

namespace WebHost.GameServerApi;

/// <summary>
/// Hosts the GRPC GameServerAPI that the GameServer calls on login.
///
/// This listens over HTTP/2 cleartext (h2c) rather than TLS, because the
/// GameServer's default GrpcChannelAddress is a plain http:// address and
/// Grpc.Net.Client speaks h2c to it.
/// </summary>
public static class GameServerApiHost
{
    /// <summary>Must match GameServerSettings.GrpcChannelAddress on the GameServer.</summary>
    public const int DefaultPort = 5201;

    public static IHost Build(IConfiguration configuration)
    {
        var port = configuration.GetValue("Firefall:GameServerApi:Port", DefaultPort);
        var characterStorePath = configuration.GetValue<string>("Firefall:GameServerApi:CharacterStorePath");

        // Treat an empty config value the same as "unset" so the store falls back
        // to its default location next to the binary.
        CharacterStore.Init(string.IsNullOrWhiteSpace(characterStorePath) ? null : characterStorePath);

        Log.Information("Starting GRPC GameServerAPI on port {Port}", port);

        var builder = WebApplication.CreateBuilder();

        builder.Host.UseSerilog();

        builder.WebHost.ConfigureKestrel(options =>
        {
            // h2c: the GameServer connects over plain http, so no TLS here.
            options.ListenAnyIP(port, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });

        builder.Services.AddGrpc();

        var app = builder.Build();

        app.MapGrpcService<GameServerApiService>();
        app.MapGet("/", () => "GameServerAPI GRPC endpoint. Use a GRPC client.");

        return app;
    }
}
