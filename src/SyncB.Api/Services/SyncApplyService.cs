using System.Data;
using Dapper;
using DbDataSyncService.SyncB.Data;
using DbDataSyncService.SyncB.Models;
using DbDataSyncService.SyncB.Utilities;
using Microsoft.Extensions.Logging;

namespace DbDataSyncService.SyncB.Services;

/// <summary>
/// 處理同步套用交易流程。
/// </summary>
public sealed class SyncApplyService
{
    private const string StateLookupSql = @"/**/
SELECT
    SyncKey,
    LastVersion,
    LastSyncTime,
    LastRowCount,
    LastError
FROM dbo.ZZ_SYNC_STATE
WHERE SyncKey = @SyncKey;";

    private const string UpdateStateSql = @"/**/
UPDATE dbo.ZZ_SYNC_STATE
SET LastVersion = @LastVersion,
    LastSyncTime = SYSUTCDATETIME(),
    LastRowCount = @LastRowCount,
    LastError = NULL
WHERE SyncKey = @SyncKey;";

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<SyncApplyService> _logger;

    public SyncApplyService(ISqlConnectionFactory connectionFactory, ILogger<SyncApplyService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    /// <summary>
    /// 套用同步資料，並在成功時更新同步水位。
    /// </summary>
    public async Task ApplyAsync(SyncApplyRequest request, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var currentState = await connection.QueryFirstOrDefaultAsync<SyncStateDto>(
            new CommandDefinition(
                StateLookupSql,
                new { request.SyncKey },
                transaction: transaction,
                cancellationToken: cancellationToken));

        if (currentState is null)
        {
            throw new InvalidOperationException($"找不到 SyncKey={request.SyncKey} 的同步水位");
        }

        if (request.FromVersion != currentState.LastVersion)
        {
            throw new InvalidOperationException(
                $"同步版本不一致，FromVersion={request.FromVersion} LastVersion={currentState.LastVersion}");
        }

        if (request.ToVersion < request.FromVersion)
        {
            throw new InvalidOperationException(
                $"同步版本範圍錯誤，FromVersion={request.FromVersion} ToVersion={request.ToVersion}");
        }

        var upsertTable = TableValuedParameterBuilder.CreatePdfConfigTable(request.Upserts);
        var deleteTable = TableValuedParameterBuilder.CreateGuidTable(request.Deletes);

        var upsertParameters = new DynamicParameters();
        upsertParameters.Add("Rows", upsertTable.AsTableValuedParameter(
            TableValuedParameterBuilder.PdfConfigTableTypeName));

        var deleteParameters = new DynamicParameters();
        deleteParameters.Add("Ids", deleteTable.AsTableValuedParameter(
            TableValuedParameterBuilder.GuidListTypeName));

        await connection.ExecuteAsync(
            new CommandDefinition(
                "dbo.usp_PDFConfigSync_Upsert",
                upsertParameters,
                transaction: transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                "dbo.usp_PDFConfigSync_Delete",
                deleteParameters,
                transaction: transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        var rowCount = request.Upserts.Count + request.Deletes.Count;
        await connection.ExecuteAsync(
            new CommandDefinition(
                UpdateStateSql,
                new { request.SyncKey, LastVersion = request.ToVersion, LastRowCount = rowCount },
                transaction: transaction,
                cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "同步套用成功，SyncKey={SyncKey} ToVersion={ToVersion} Rows={RowCount}",
            request.SyncKey,
            request.ToVersion,
            rowCount);
    }
}
