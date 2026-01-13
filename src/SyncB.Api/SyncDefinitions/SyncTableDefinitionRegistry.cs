namespace DbDataSyncService.SyncB.SyncDefinitions;

/// <summary>
/// 同步資料表定義查詢器。
/// </summary>
public sealed class SyncTableDefinitionRegistry
{
    private readonly IReadOnlyDictionary<string, ISyncTableDefinition> _definitions;

    public SyncTableDefinitionRegistry(IEnumerable<ISyncTableDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(x => x.TableName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 依資料表名稱取得定義。
    /// </summary>
    public ISyncTableDefinition Get(string tableName)
    {
        if (!_definitions.TryGetValue(tableName, out var definition))
        {
            throw new InvalidOperationException($"未支援的資料表 {tableName}");
        }

        return definition;
    }
}
