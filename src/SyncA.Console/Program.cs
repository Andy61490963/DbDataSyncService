using Serilog;
using DbDataSyncService.SyncA.Configuration;
using DbDataSyncService.SyncA.Data;
using DbDataSyncService.SyncA.Logging;
using DbDataSyncService.SyncA.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DbDataSyncService.SyncA;

public sealed class Program
{
    /// <summary>
    /// 主程式進入點（Console Job，執行一次同步後結束）
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        // 啟動期 Logger（只寫 Console，避免正式設定還沒讀到就爆）
        Log.Logger = SerilogConfigurator.CreateBootstrapLogger();

        try
        {
            var host = Host.CreateDefaultBuilder(args)
                // 讀 appsettings.json（ConnectionStrings / Logging / SyncJob）
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile(
                        "appsettings.json",
                        optional: false,
                        reloadOnChange: true);
                })
                // DI 註冊
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;

                    // Options 綁定
                    services.Configure<AppLoggingOptions>(
                        configuration.GetSection(AppLoggingOptions.SectionName));

                    services.Configure<SyncJobOptions>(
                        configuration.GetSection(SyncJobOptions.SectionName));

                    // 基礎設施
                    services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
                    services.AddSingleton<ChangeTrackingRepository>();

                    // HTTP Client（呼叫 B 端 Sync API）
                    services.AddHttpClient<SyncApiClient>();

                    // Job 本體（單次執行）
                    services.AddSingleton<SyncRunner>();
                })
                // Host 全域 Serilog（File / Seq / Console）
                .UseSerilog((context, services, loggerConfiguration) =>
                {
                    var loggingOptions = context.Configuration
                        .GetSection(AppLoggingOptions.SectionName)
                        .Get<AppLoggingOptions>() ?? new AppLoggingOptions();

                    SerilogConfigurator.Configure(loggerConfiguration, loggingOptions);
                })
                .Build();

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
        catch (OperationCanceledException)
        {
            Log.Warning("同步程序已取消");
            return 1;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "SyncA 同步程序啟動或執行失敗");
            return 2;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
