using System.Data;
using Dapper;
using DbDataSyncService.SyncB.Data;
using DbDataSyncService.SyncB.Models;
using DbDataSyncService.SyncB.SyncDefinitions;
using Microsoft.Extensions.Logging;

namespace DbDataSyncService.SyncB.Services;

public sealed class SyncApplyService
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly SyncStateStore _stateStore;
    private readonly SyncTableDefinitionRegistry _tableRegistry;
    private readonly ILogger<SyncApplyService> _logger;

    public SyncApplyService(
        ISqlConnectionFactory connectionFactory,
        SyncStateStore stateStore,
        SyncTableDefinitionRegistry tableRegistry,
        ILogger<SyncApplyService> logger)
    {
        _connectionFactory = connectionFactory;
        _stateStore = stateStore;
        _tableRegistry = tableRegistry;
        _logger = logger;
    }

    /// <summary>
    /// 依序套用資料並更新同步水位（同一交易內完成）。
    /// </summary>
    public async Task ApplyAsync(SyncApplyJsonRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SyncKey))
        {
            throw new InvalidOperationException("syncKey 為必填");
        }

        if (request.Tables.Count == 0)
        {
            throw new InvalidOperationException("請求未包含任何資料表");
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var currentState = await _stateStore.GetStateAsync(
                connection,
                tx,
                request.SyncKey,
                cancellationToken);

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

            var totalRows = 0;

            foreach (var table in request.Tables)
            {
                var definition = _tableRegistry.Get(table.Table);
                var upserts = definition.CreateUpsertTable();
                var deletes = definition.CreateDeleteTable();
                totalRows += definition.AddRows(table, upserts, deletes);

                if (deletes.Rows.Count > 0)
                {
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            definition.DeleteProcedure,
                            new { Ids = deletes.AsTableValuedParameter(definition.DeleteTvpTypeName) },
                            transaction: tx,
                            commandType: CommandType.StoredProcedure,
                            cancellationToken: cancellationToken));
                }

                if (upserts.Rows.Count > 0)
                {
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            definition.UpsertProcedure,
                            new { Rows = upserts.AsTableValuedParameter(definition.UpsertTvpTypeName) },
                            transaction: tx,
                            commandType: CommandType.StoredProcedure,
                            cancellationToken: cancellationToken));
                }
            }

            await _stateStore.UpdateStateAsync(
                connection,
                tx,
                request.SyncKey,
                request.ToVersion,
                totalRows,
                cancellationToken);

            await tx.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "同步套用成功(JSON→TVP)，SyncKey={SyncKey} ToVersion={ToVersion} Rows={RowCount}",
                request.SyncKey,
                request.ToVersion,
                totalRows);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
