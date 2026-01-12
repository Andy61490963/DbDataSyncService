using System.Data;
using Dapper;
using DbDataSyncService.SyncA.Models;
using DbDataSyncService.SyncA.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace DbDataSyncService.SyncA.Data;

/// <summary>
/// 封裝 Change Tracking 查詢邏輯。
/// </summary>
public sealed class ChangeTrackingRepository
{
    private const string CurrentVersionSql = @"SELECT CHANGE_TRACKING_CURRENT_VERSION();";

    private const string ChangeLookupSql = @"/**/
WITH Changes AS (
    SELECT
        CT.ID,
        CT.SYS_CHANGE_OPERATION,
        CT.SYS_CHANGE_VERSION,
        ROW_NUMBER() OVER (PARTITION BY CT.ID ORDER BY CT.SYS_CHANGE_VERSION DESC) AS RN
    FROM CHANGETABLE(CHANGES dbo.PDFConfigSyncServiceConfig, @LastVersion) AS CT
)
SELECT
    ID,
    SYS_CHANGE_OPERATION AS Operation
FROM Changes
WHERE RN = 1;";

    private const string RowLookupSql = @"/**/
SELECT
    ID AS Id,
    ConfigKey,
    ConfigValue,
    IsEnabled,
    UpdatedAt
FROM dbo.PDFConfigSyncServiceConfig AS SRC
INNER JOIN @Ids AS IDS
    ON SRC.ID = IDS.Id;";

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<ChangeTrackingRepository> _logger;

    public ChangeTrackingRepository(ISqlConnectionFactory connectionFactory, ILogger<ChangeTrackingRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    /// <summary>
    /// 取得目前資料庫 Change Tracking 版本。
    /// </summary>
    public async Task<long> GetCurrentVersionAsync(CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(CurrentVersionSql, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// 取得指定版本之後的變更清單（僅保留每筆資料最新操作）。
    /// </summary>
    public async Task<IReadOnlyList<ChangeRow>> GetChangesAsync(long lastVersion, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var result = await connection.QueryAsync<(Guid Id, string Operation)>(
            new CommandDefinition(ChangeLookupSql, new { LastVersion = lastVersion }, cancellationToken: cancellationToken));

        var changes = result.Select(row => new ChangeRow
        {
            Id = row.Id,
            Operation = row.Operation.Equals("D", StringComparison.OrdinalIgnoreCase)
                ? ChangeOperation.Delete
                : ChangeOperation.InsertOrUpdate
        }).ToList();

        _logger.LogInformation("取得變更筆數 {Count}", changes.Count);
        return changes;
    }

    /// <summary>
    /// 依據指定主鍵清單取得完整資料列。
    /// </summary>
    public async Task<IReadOnlyList<PdfConfigSyncRow>> GetRowsByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<PdfConfigSyncRow>();
        }

        var tvp = TableValuedParameterBuilder.CreateGuidTable(ids);
        var parameters = new DynamicParameters();
        parameters.Add("Ids", tvp.AsTableValuedParameter(TableValuedParameterBuilder.GuidListTypeName));

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<PdfConfigSyncRow>(
            new CommandDefinition(RowLookupSql, parameters, cancellationToken: cancellationToken));

        return rows.ToList();
    }
}
