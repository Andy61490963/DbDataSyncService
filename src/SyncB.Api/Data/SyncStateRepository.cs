using Dapper;
using DbDataSyncService.SyncB.Models;
using Microsoft.Data.SqlClient;

namespace DbDataSyncService.SyncB.Data;

/// <summary>
/// 同步水位存取。
/// </summary>
public sealed class SyncStateRepository
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

    private readonly ISqlConnectionFactory _connectionFactory;

    public SyncStateRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// 取得同步水位。
    /// </summary>
    public async Task<SyncStateDto?> GetStateAsync(string syncKey, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<SyncStateDto>(
            new CommandDefinition(StateLookupSql, new { SyncKey = syncKey }, cancellationToken: cancellationToken));
    }
}
