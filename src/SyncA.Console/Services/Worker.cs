using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DbDataSyncService.SyncA.Configuration;

namespace DbDataSyncService.SyncA.Services;

/// <summary>
/// 同步背景 Worker（常駐，每隔一段時間執行一次同步）
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly SyncRunner _runner;
    private readonly SyncJobOptions _options;
    private readonly ILogger<Worker> _logger;

    public Worker(
        SyncRunner runner,
        IOptions<SyncJobOptions> options,
        ILogger<Worker> logger)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SyncWorker 啟動，Interval={IntervalSeconds} 秒",
            _options.IntervalSeconds);

        // 啟動後先跑一次（可視需求移除）
        await RunOnceSafelyAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_options.IntervalSeconds),
                    stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            await RunOnceSafelyAsync(stoppingToken);
        }

        _logger.LogInformation("SyncWorker 停止");
    }

    private async Task RunOnceSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _runner.RunOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("同步作業被取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步作業執行失敗");
        }
    }
}