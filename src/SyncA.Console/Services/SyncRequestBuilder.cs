using DbDataSyncService.SyncA.Data;
using DbDataSyncService.SyncA.Models;

namespace DbDataSyncService.SyncA.Services;

/// <summary>
/// 將資料庫變更組成同步套用請求。
/// </summary>
public sealed class SyncRequestBuilder
{
    private readonly ChangeTrackingRepository _repository;
    private readonly ISyncTableDefinition _tableDefinition;

    public SyncRequestBuilder(ChangeTrackingRepository repository, ISyncTableDefinition tableDefinition)
    {
        _repository = repository;
        _tableDefinition = tableDefinition;
    }

    /// <summary>
    /// 建立同步請求；若無資料則回傳 null。
    /// </summary>
    public async Task<SyncApplyJsonRequest?> BuildAsync(
        string syncKey,
        long fromVersion,
        long toVersion,
        IReadOnlyCollection<Guid> upsertIds,
        IReadOnlyCollection<Guid> deleteIds,
        CancellationToken cancellationToken)
    {
        if ((upsertIds?.Count ?? 0) == 0 && (deleteIds?.Count ?? 0) == 0)
        {
            return null;
        }

        var columns = await _repository.GetColumnsAsync(_tableDefinition.TableName, cancellationToken);

        if (!columns.Any(c => c.Equals(_tableDefinition.PrimaryKey, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Table={_tableDefinition.TableName} 找不到 PK 欄位 {_tableDefinition.PrimaryKey}");
        }

        var upsertRows = await _repository.GetRowsAsStringDictionariesByIdsAsync(
            _tableDefinition.TableName,
            _tableDefinition.PrimaryKey,
            columns,
            upsertIds ?? Array.Empty<Guid>(),
            cancellationToken);

        var payloadTable = new SyncTablePayload
        {
            Table = _tableDefinition.TableName,
            Rows = new List<SyncRowPayload>(upsertRows.Count + (deleteIds?.Count ?? 0))
        };

        foreach (var data in upsertRows)
        {
            if (!data.TryGetValue(_tableDefinition.PrimaryKey, out var pkValue) || string.IsNullOrWhiteSpace(pkValue))
            {
                throw new InvalidOperationException(
                    $"Table={_tableDefinition.TableName} 讀到資料列卻沒有 PK={_tableDefinition.PrimaryKey}");
            }

            payloadTable.Rows.Add(new SyncRowPayload
            {
                Op = SyncPayloadConstants.Upsert,
                Key = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    [_tableDefinition.PrimaryKey] = pkValue
                },
                Data = RemoveKeyColumn(data, _tableDefinition.PrimaryKey)
            });
        }

        foreach (var id in deleteIds ?? Array.Empty<Guid>())
        {
            payloadTable.Rows.Add(new SyncRowPayload
            {
                Op = SyncPayloadConstants.Delete,
                Key = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    [_tableDefinition.PrimaryKey] = id.ToString("D")
                },
                Data = null
            });
        }

        return new SyncApplyJsonRequest
        {
            SyncKey = syncKey,
            FromVersion = fromVersion,
            ToVersion = toVersion,
            Tables = new List<SyncTablePayload> { payloadTable }
        };
    }

    private static Dictionary<string, string?> RemoveKeyColumn(
        Dictionary<string, string?> source,
        string pkColumnName)
    {
        var copy = new Dictionary<string, string?>(source, StringComparer.OrdinalIgnoreCase);
        copy.Remove(pkColumnName);
        return copy;
    }
}
