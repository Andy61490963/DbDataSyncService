using System.Data;
using Dapper;
using DbDataSyncService.SyncB.Models;
using Microsoft.Data.SqlClient;

namespace DbDataSyncService.SyncB.Data;

/// <summary>
/// 同步水位存取（交易內專用）。
/// </summary>
public sealed class SyncStateStore
{
    private const string StateLookupSql = @"/**/
SELECT SyncKey, LastVersion, LastSyncTime, LastRowCount, LastError
FROM dbo.ZZ_SYNC_STATE
WHERE SyncKey = @SyncKey;";

    private const string UpdateStateSql = @"/**/
UPDATE dbo.ZZ_SYNC_STATE
SET LastVersion = @LastVersion,
    LastSyncTime = SYSUTCDATETIME(),
    LastRowCount = @LastRowCount,
    LastError = NULL
WHERE SyncKey = @SyncKey;";

    /// <summary>
    /// 在指定交易內取得同步水位。
    /// </summary>
    public async Task<SyncStateDto?> GetStateAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string syncKey,
        CancellationToken cancellationToken)
    {
        return await connection.QueryFirstOrDefaultAsync<SyncStateDto>(
            new CommandDefinition(
                StateLookupSql,
                new { SyncKey = syncKey },
                transaction: transaction,
                cancellationToken: cancellationToken));
    }

    /// <summary>
    /// 在指定交易內更新同步水位。
    /// </summary>
    public async Task UpdateStateAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string syncKey,
        long lastVersion,
        int lastRowCount,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                UpdateStateSql,
                new { SyncKey = syncKey, LastVersion = lastVersion, LastRowCount = lastRowCount },
                transaction: transaction,
                cancellationToken: cancellationToken));
    }
}
