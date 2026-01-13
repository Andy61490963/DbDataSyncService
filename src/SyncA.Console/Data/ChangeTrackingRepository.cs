using System.Collections.Concurrent;
using Dapper;
using DbDataSyncService.SyncA.Models;
using DbDataSyncService.SyncA.Utilities;
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

    private const string ColumnLookupSql = @"/**/
SELECT c.name
FROM sys.columns AS c
WHERE c.object_id = OBJECT_ID(@FullTableName)
ORDER BY c.column_id;";

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<ChangeTrackingRepository> _logger;

    private static readonly ConcurrentDictionary<string, string[]> ColumnCache =
        new(StringComparer.OrdinalIgnoreCase);

    public ChangeTrackingRepository(ISqlConnectionFactory connectionFactory, ILogger<ChangeTrackingRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    /// <summary>
    /// 取得目前 Change Tracking 版本。
    /// </summary>
    public async Task<long> GetCurrentVersionAsync(CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(CurrentVersionSql, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// 取得指定版本之後的變更。
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
    /// 取得指定資料表欄位清單。
    /// </summary>
    public async Task<string[]> GetColumnsAsync(string fullTableName, CancellationToken cancellationToken)
    {
        if (ColumnCache.TryGetValue(fullTableName, out var cached))
        {
            return cached;
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var cols = (await connection.QueryAsync<string>(
            new CommandDefinition(ColumnLookupSql, new { FullTableName = fullTableName }, cancellationToken: cancellationToken)))
            .ToArray();

        if (cols.Length == 0)
        {
            throw new InvalidOperationException($"找不到 Table schema：{fullTableName}");
        }

        ColumnCache[fullTableName] = cols;
        _logger.LogInformation("載入欄位完成，Table={Table} Columns={Count}", fullTableName, cols.Length);

        return cols;
    }

    /// <summary>
    /// 從 DB 取資料列，並把每個欄位都轉成 string?（JSON 送出前就統一字串）。
    /// </summary>
    public async Task<IReadOnlyList<Dictionary<string, string?>>> GetRowsAsStringDictionariesByIdsAsync(
        string tableName,
        string pkColumnName,
        string[] columns,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<Dictionary<string, string?>>();
        }

        static string Quote(string name) => $"[{name.Replace("]", "]]")}]";

        // ✅ 修正 ambiguous：所有欄位都指定來源 T，輸出欄位名保持原名
        var selectList = string.Join(", ", columns.Select(c => $"T.{Quote(c)} AS {Quote(c)}"));

        var sql = $@"/**/
SELECT {selectList}
FROM {tableName} AS T
INNER JOIN @Ids AS I
    ON I.Id = T.{Quote(pkColumnName)};";

        var tvp = TableValuedParameterBuilder.CreateGuidTable(ids);
        var parameters = new DynamicParameters();
        parameters.Add("Ids", tvp.AsTableValuedParameter(TableValuedParameterBuilder.GuidListTypeName));

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var reader = await connection.ExecuteReaderAsync(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        var result = new List<Dictionary<string, string?>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);

                if (await reader.IsDBNullAsync(i, cancellationToken))
                {
                    row[name] = null;
                    continue;
                }

                row[name] = InvariantValueConverter.ToInvariantString(reader.GetValue(i));
            }

            result.Add(row);
        }

        return result;
    }
}
