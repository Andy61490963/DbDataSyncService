using DbDataSyncService.SyncA.Configuration;
using DbDataSyncService.SyncA.Data;
using DbDataSyncService.SyncA.Models;
using DbDataSyncService.SyncA.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DbDataSyncService.SyncA.Services;

/// <summary>
/// 單次同步流程執行器，由排程器週期性呼叫。
/// </summary>
public sealed class SyncRunner
{
    private readonly ChangeTrackingRepository _repository;
    private readonly SyncApiClient _apiClient;
    private readonly SyncRequestBuilder _requestBuilder;
    private readonly SyncJobOptions _options;
    private readonly ILogger<SyncRunner> _logger;

    public SyncRunner(
        ChangeTrackingRepository repository,
        SyncApiClient apiClient,
        SyncRequestBuilder requestBuilder,
        IOptions<SyncJobOptions> options,
        ILogger<SyncRunner> logger)
    {
        _repository = repository;
        _apiClient = apiClient;
        _requestBuilder = requestBuilder;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 執行一次同步作業，成功後即結束。
    /// </summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("開始同步，SyncKey={SyncKey}", _options.SyncKey);

        var state = await _apiClient.GetStateAsync(_options.SyncKey, cancellationToken)
                    ?? throw new InvalidOperationException("無法取得遠端同步水位");

        var currentVersion = await _repository.GetCurrentVersionAsync(cancellationToken);
        if (currentVersion <= state.LastVersion)
        {
            _logger.LogInformation(
                "目前無需同步，CurrentVersion={CurrentVersion} LastVersion={LastVersion}",
                currentVersion, state.LastVersion);
            return;
        }

        var changes = await _repository.GetChangesAsync(state.LastVersion, cancellationToken);
        if (changes.Count == 0)
        {
            _logger.LogWarning(
                "偵測到版本前進但無變更資料，可能超出 CT 保留範圍，LastVersion={LastVersion} CurrentVersion={CurrentVersion}",
                state.LastVersion, currentVersion);
            return;
        }

        var batches = changes.Batch(_options.BatchSize).ToList();
        for (var index = 0; index < batches.Count; index++)
        {
            var batch = batches[index];
            var isLastBatch = index == batches.Count - 1;

            var upsertIds = batch
                .Where(x => x.Operation == ChangeOperation.InsertOrUpdate)
                .Select(x => x.Id)
                .ToList();

            var deleteIds = batch
                .Where(x => x.Operation == ChangeOperation.Delete)
                .Select(x => x.Id)
                .ToList();

            var request = await _requestBuilder.BuildAsync(
                syncKey: _options.SyncKey,
                fromVersion: state.LastVersion,
                toVersion: isLastBatch ? currentVersion : state.LastVersion,
                upsertIds: upsertIds,
                deleteIds: deleteIds,
                cancellationToken: cancellationToken);

            if (request is null)
            {
                _logger.LogInformation("批次 {Index}/{Total} 無資料可送", index + 1, batches.Count);
                continue;
            }

            _logger.LogInformation(
                "送出批次 {Index}/{Total}，Upserts={Upserts} Deletes={Deletes}",
                index + 1,
                batches.Count,
                upsertIds.Count,
                deleteIds.Count);

            // 不再只送 upserts，送完整 request（含 deletes）
            await _apiClient.ApplyAsync(request, cancellationToken);
        }

        _logger.LogInformation("同步完成，CurrentVersion={CurrentVersion}", currentVersion);
    }
}
