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
    private readonly SyncJobOptions _options;
    private readonly ILogger<SyncRunner> _logger;

    public SyncRunner(
        ChangeTrackingRepository repository,
        SyncApiClient apiClient,
        IOptions<SyncJobOptions> options,
        ILogger<SyncRunner> logger)
    {
        _repository = repository;
        _apiClient = apiClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 執行一次同步作業，成功後即結束。
    /// </summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("開始同步，SyncKey={SyncKey}", _options.SyncKey);

        var state = await _apiClient.GetStateAsync(_options.SyncKey, cancellationToken);
        if (state is null)
        {
            throw new InvalidOperationException("無法取得遠端同步水位");
        }

        var currentVersion = await _repository.GetCurrentVersionAsync(cancellationToken);
        if (currentVersion <= state.LastVersion)
        {
            _logger.LogInformation("目前無需同步，CurrentVersion={CurrentVersion} LastVersion={LastVersion}",
                currentVersion, state.LastVersion);
            return;
        }

        var changes = await _repository.GetChangesAsync(state.LastVersion, cancellationToken);
        if (changes.Count == 0)
        {
            _logger.LogWarning(
                "偵測到版本前進但無變更資料，可能超出 CT 保留範圍，請檢查 LastVersion={LastVersion} CurrentVersion={CurrentVersion}",
                state.LastVersion, currentVersion);
            return;
        }

        var batches = changes.Batch(_options.BatchSize).ToList();
        for (var index = 0; index < batches.Count; index++)
        {
            var batch = batches[index];
            var isLastBatch = index == batches.Count - 1;

            var upsertIds = batch
                .Where(change => change.Operation == ChangeOperation.InsertOrUpdate)
                .Select(change => change.Id)
                .ToList();

            var deleteIds = batch
                .Where(change => change.Operation == ChangeOperation.Delete)
                .Select(change => change.Id)
                .ToList();

            var upserts = await _repository.GetRowsByIdsAsync(upsertIds, cancellationToken);

            var request = new SyncApplyRequest
            {
                SyncKey = _options.SyncKey,
                FromVersion = state.LastVersion,
                ToVersion = isLastBatch ? currentVersion : state.LastVersion,
                Upserts = upserts,
                Deletes = deleteIds
            };

            _logger.LogInformation(
                "送出批次 {Index}/{Total}，Upserts={Upserts} Deletes={Deletes}",
                index + 1,
                batches.Count,
                upserts.Count,
                deleteIds.Count);

            await _apiClient.ApplyAsync(request, cancellationToken);
        }

        _logger.LogInformation("同步完成，CurrentVersion={CurrentVersion}", currentVersion);
    }
}
