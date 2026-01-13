using System.Data;
using DbDataSyncService.SyncB.Models;
using DbDataSyncService.SyncB.Utilities;

namespace DbDataSyncService.SyncB.SyncDefinitions;

/// <summary>
/// PDF 設定同步資料表定義。
/// </summary>
public sealed class PdfConfigSyncTableDefinition : ISyncTableDefinition
{
    private static readonly string[] AllowedColumns =
    {
        nameof(PdfConfigSyncRow.ConfigKey),
        nameof(PdfConfigSyncRow.ConfigValue),
        nameof(PdfConfigSyncRow.IsEnabled),
        nameof(PdfConfigSyncRow.UpdatedAt)
    };

    /// <inheritdoc />
    public string TableName => "dbo.PDFConfigSyncServiceConfig";

    /// <inheritdoc />
    public string PrimaryKey => "ID";

    /// <inheritdoc />
    public string UpsertProcedure => "dbo.usp_PDFConfigSync_Upsert";

    /// <inheritdoc />
    public string DeleteProcedure => "dbo.usp_PDFConfigSync_Delete";

    /// <inheritdoc />
    public string UpsertTvpTypeName => "dbo.PDFConfigSyncServiceConfigTvp";

    /// <inheritdoc />
    public string DeleteTvpTypeName => "dbo.GuidList";

    /// <inheritdoc />
    public DataTable CreateUpsertTable()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        table.Columns.Add("ConfigKey", typeof(string));
        table.Columns.Add("ConfigValue", typeof(string));
        table.Columns.Add("IsEnabled", typeof(bool));
        table.Columns.Add("UpdatedAt", typeof(DateTime));
        return table;
    }

    /// <inheritdoc />
    public DataTable CreateDeleteTable()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        return table;
    }

    /// <inheritdoc />
    public int AddRows(SyncTablePayload payload, DataTable upserts, DataTable deletes)
    {
        var count = 0;

        foreach (var row in payload.Rows)
        {
            if (string.Equals(row.Op, SyncPayloadConstants.Delete, StringComparison.OrdinalIgnoreCase))
            {
                var key = new Dictionary<string, string?>(row.Key, StringComparer.OrdinalIgnoreCase);
                var id = SyncValueParser.ParseGuid(GetValue(key, PrimaryKey), PrimaryKey);
                deletes.Rows.Add(id);
                count++;
                continue;
            }

            if (!string.Equals(row.Op, SyncPayloadConstants.Upsert, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"不支援的操作 {row.Op}");
            }

            var mapped = MapUpsertRow(row);
            upserts.Rows.Add(mapped.Id, mapped.ConfigKey, mapped.ConfigValue, mapped.IsEnabled, mapped.UpdatedAt);
            count++;
        }

        return count;
    }

    private PdfConfigSyncRow MapUpsertRow(SyncRowPayload row)
    {
        if (row.Data is null)
        {
            throw new InvalidOperationException("Upsert 必須包含 data");
        }

        var data = new Dictionary<string, string?>(row.Data, StringComparer.OrdinalIgnoreCase);
        var key = new Dictionary<string, string?>(row.Key, StringComparer.OrdinalIgnoreCase);
        ValidateColumns(data.Keys);

        return new PdfConfigSyncRow
        {
            Id = SyncValueParser.ParseGuid(GetValue(key, PrimaryKey), PrimaryKey),
            ConfigKey = SyncValueParser.ParseRequiredString(GetValue(data, nameof(PdfConfigSyncRow.ConfigKey)), nameof(PdfConfigSyncRow.ConfigKey)),
            ConfigValue = SyncValueParser.ParseOptionalString(GetValue(data, nameof(PdfConfigSyncRow.ConfigValue))),
            IsEnabled = SyncValueParser.ParseBool(GetValue(data, nameof(PdfConfigSyncRow.IsEnabled)), nameof(PdfConfigSyncRow.IsEnabled)),
            UpdatedAt = SyncValueParser.ParseDateTime(GetValue(data, nameof(PdfConfigSyncRow.UpdatedAt)), nameof(PdfConfigSyncRow.UpdatedAt))
        };
    }

    private static void ValidateColumns(IEnumerable<string> columns)
    {
        var invalid = columns
            .Where(c => !AllowedColumns.Contains(c, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (invalid.Count > 0)
        {
            throw new InvalidOperationException($"偵測到未允許欄位: {string.Join(", ", invalid)}");
        }
    }

    private static string? GetValue(IReadOnlyDictionary<string, string?> source, string key)
    {
        return source.TryGetValue(key, out var value) ? value : null;
    }
}
