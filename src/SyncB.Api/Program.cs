using DbDataSyncService.SyncB.Configuration;
using DbDataSyncService.SyncB.Data;
using DbDataSyncService.SyncB.Logging;
using DbDataSyncService.SyncB.Services;
using DbDataSyncService.SyncB.SyncDefinitions;
using Serilog;

namespace DbDataSyncService.SyncB;

public static class Program
{
    /// <summary>
    /// API 服務進入點，負責啟動 DI、日誌與 Controller。
    /// </summary>
    public static void Main(string[] args)
    {
        Log.Logger = SerilogConfigurator.CreateBootstrapLogger();

        try
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.Configure<AppLoggingOptions>(
                builder.Configuration.GetSection(AppLoggingOptions.SectionName));

            builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
            builder.Services.AddScoped<SyncStateRepository>();
            builder.Services.AddScoped<SyncStateStore>();
            builder.Services.AddSingleton<ISyncTableDefinition, PdfConfigSyncTableDefinition>();
            builder.Services.AddSingleton<SyncTableDefinitionRegistry>();
            builder.Services.AddScoped<SyncApplyService>();

            builder.Services.AddControllers();

            builder.Host.UseSerilog((context, _, loggerConfiguration) =>
            {
                var loggingOptions = context.Configuration
                    .GetSection(AppLoggingOptions.SectionName)
                    .Get<AppLoggingOptions>() ?? new AppLoggingOptions();

                SerilogConfigurator.Configure(loggerConfiguration, loggingOptions);
            });

            var app = builder.Build();
            app.MapControllers();
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "API 啟動失敗");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
