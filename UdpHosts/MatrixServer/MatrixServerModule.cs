using Autofac;
using Serilog;
using Shared.Common;

namespace MatrixServer;

public class MatrixServerModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        RegisterTypes(builder);
        RegisterInstances(builder);
        base.Load(builder);
    }

    private static void RegisterTypes(ContainerBuilder builder)
    {
        builder.RegisterType<MatrixServerSettings>().SingleInstance();
        builder.RegisterType<MatrixServer>();
    }

    private static void RegisterInstances(ContainerBuilder builder)
    {
        builder.Register(ctx =>
                         {
                             // Console logging only: ReadFrom.AppSettings() goes through
                             // ConfigurationManager, which locates App.config via Assembly.CodeBase
                             // and throws inside the single-file MatrixServer.exe. There is no
                             // App.config to read in this project anyway; CLI --log-level remains
                             // the override.
                             var loggerConfig = new LoggerConfiguration()
                                                .WriteTo.Console(theme: SerilogTheme.Custom);

                             var settings = ctx.Resolve<MatrixServerSettings>();

                             if (settings.LogLevel.HasValue)
                             {
                                 loggerConfig = loggerConfig.MinimumLevel.Is(settings.LogLevel.Value);
                             }

                             return loggerConfig.CreateLogger();
                         }).As<ILogger>().SingleInstance();
    }
}