using System.Data;
using Dapper;
using DbDataSyncService.SyncB.Data;
using DbDataSyncService.SyncB.Models;
using DbDataSyncService.SyncB.Utilities;
using Microsoft.Extensions.Logging;

namespace DbDataSyncService.SyncB.Services;

public sealed class SyncApplyService
{
    private const string UpdateStateSql = @"/**/
UPDATE dbo.ZZ_SYNC_STATE
SET LastVersion = @LastVersion,
    LastSyncTime = SYSUTCDATETIME(),
    LastRowCount = @LastRowCount,
    LastError = NULL
WHERE SyncKey = @SyncKey;";

    private const string StateLookupSql = @"/**/
SELECT SyncKey, LastVersion, LastSyncTime, LastRowCount, LastError
FROM dbo.ZZ_SYNC_STATE
WHERE SyncKey = @SyncKey;";

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<SyncApplyService> _logger;

    public SyncApplyService(ISqlConnectionFactory connectionFactory, ILogger<SyncApplyService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task ApplyAsync(SyncApplyJsonRequest request, CancellationToken cancellationToken)
{
    await using var connection = _connectionFactory.CreateConnection();
    await connection.OpenAsync(cancellationToken);

    await using var tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

    try
    {
        var currentState = await connection.QueryFirstOrDefaultAsync<SyncStateDto>(
            new CommandDefinition(
                StateLookupSql,
                new { request.SyncKey },
                transaction: tx,
                cancellationToken: cancellationToken));

        if (currentState is null)
            throw new InvalidOperationException($"找不到 SyncKey={request.SyncKey} 的同步水位");

        if (request.FromVersion != currentState.LastVersion)
            throw new InvalidOperationException(
                $"同步版本不一致，FromVersion={request.FromVersion} LastVersion={currentState.LastVersion}");

        if (request.ToVersion < request.FromVersion)
            throw new InvalidOperationException(
                $"同步版本範圍錯誤，FromVersion={request.FromVersion} ToVersion={request.ToVersion}");

        // ✅ 改這裡：build 變成多批
        var batches = SqlApplyCommandBuilder.BuildBatches(request, maxParameters: 1000);

        var totalRows = 0;
        for (var i = 0; i < batches.Count; i++)
        {
            
            var batch = batches[i];
            totalRows += batch.RowCount;

            _logger.LogInformation("Apply batch {Index}/{Total}: Rows={Rows}", i + 1, batches.Count, batch.RowCount);
            
            if (string.IsNullOrWhiteSpace(batch.Sql))
                continue;

            await connection.ExecuteAsync(
                new CommandDefinition(
                    batch.Sql,
                    batch.Parameters,
                    transaction: tx,
                    cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                UpdateStateSql,
                new
                {
                    request.SyncKey,
                    LastVersion = request.ToVersion,
                    LastRowCount = totalRows
                },
                transaction: tx,
                cancellationToken: cancellationToken));

        await tx.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "同步套用成功(JSON→SQL Batches)，SyncKey={SyncKey} ToVersion={ToVersion} Rows={RowCount} Batches={Batches}",
            request.SyncKey,
            request.ToVersion,
            totalRows,
            batches.Count);
    }
    catch
    {
        await tx.RollbackAsync(cancellationToken);
        throw;
    }
}

}
