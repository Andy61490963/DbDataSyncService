using Serilog;
using DbDataSyncService.SyncA.Configuration;
using DbDataSyncService.SyncA.Data;
using DbDataSyncService.SyncA.Logging;
using DbDataSyncService.SyncA.Models;
using DbDataSyncService.SyncA.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DbDataSyncService.SyncA;

public sealed class Program
{
    public static async Task Main(string[] args)
    {
        // 啟動期 Logger（Host 還沒起來前用）
        Log.Logger = SerilogConfigurator.CreateBootstrapLogger();

        try
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;

                    // Options
                    services.Configure<AppLoggingOptions>(
                        configuration.GetSection(AppLoggingOptions.SectionName));

                    services.Configure<SyncJobOptions>(
                        configuration.GetSection(SyncJobOptions.SectionName));

                    // Infrastructure
                    services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
                    services.AddSingleton<ChangeTrackingRepository>();
                    services.AddSingleton<ISyncTableDefinition, PdfConfigSyncTableDefinition>();
                    services.AddSingleton<SyncRequestBuilder>();
                    services.AddHttpClient<SyncApiClient>();

                    // Core logic
                    services.AddSingleton<SyncRunner>();

                    // Worker
                    services.AddHostedService<Worker>();
                })
                .UseSerilog((context, services, loggerConfiguration) =>
                {
                    var loggingOptions = context.Configuration
                        .GetSection(AppLoggingOptions.SectionName)
                        .Get<AppLoggingOptions>() ?? new AppLoggingOptions();

                    SerilogConfigurator.Configure(loggerConfiguration, loggingOptions);
                })
                .Build();

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "SyncA Worker 啟動失敗");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
