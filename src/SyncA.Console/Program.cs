using DbDataSyncService.SyncA.Configuration;
using DbDataSyncService.SyncA.Data;
using DbDataSyncService.SyncA.Logging;
using DbDataSyncService.SyncA.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DbDataSyncService.SyncA;

public static class Program
{
    /// <summary>
    /// 主程式進入點，負責初始化 DI、設定日誌並執行一次同步流程。
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        Log.Logger = SerilogConfigurator.CreateBootstrapLogger();

        try
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.Configure<AppLoggingOptions>(
                builder.Configuration.GetSection(AppLoggingOptions.SectionName));
            builder.Services.Configure<SyncJobOptions>(
                builder.Configuration.GetSection(SyncJobOptions.SectionName));

            builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
            builder.Services.AddSingleton<ChangeTrackingRepository>();
            builder.Services.AddHttpClient<SyncApiClient>();
            builder.Services.AddSingleton<SyncRunner>();

            builder.Services.AddSerilog((context, _, loggerConfiguration) =>
            {
                var loggingOptions = context.Configuration
                    .GetSection(AppLoggingOptions.SectionName)
                    .Get<AppLoggingOptions>() ?? new AppLoggingOptions();

                SerilogConfigurator.Configure(loggerConfiguration, loggingOptions);
            });

            using var host = builder.Build();

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            var runner = host.Services.GetRequiredService<SyncRunner>();
            await runner.RunOnceAsync(cts.Token);

            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "同步程序啟動失敗");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
