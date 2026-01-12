using System.Data;
using DbDataSyncService.SyncB.Models;

namespace DbDataSyncService.SyncB.Utilities;

/// <summary>
/// 建立 TVP 所需的 DataTable。
/// </summary>
public static class TableValuedParameterBuilder
{
    public const string GuidListTypeName = "dbo.GuidList";
    public const string PdfConfigTableTypeName = "dbo.PDFConfigSyncServiceConfigTvp";

    /// <summary>
    /// 建立 GUID 清單的 TVP。
    /// </summary>
    public static DataTable CreateGuidTable(IEnumerable<Guid> ids)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));

        foreach (var id in ids)
        {
            table.Rows.Add(id);
        }

        return table;
    }

    /// <summary>
    /// 建立 PDF 設定同步資料的 TVP。
    /// </summary>
    public static DataTable CreatePdfConfigTable(IEnumerable<PdfConfigSyncRow> rows)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        table.Columns.Add("ConfigKey", typeof(string));
        table.Columns.Add("ConfigValue", typeof(string));
        table.Columns.Add("IsEnabled", typeof(bool));
        table.Columns.Add("UpdatedAt", typeof(DateTime));

        foreach (var row in rows)
        {
            table.Rows.Add(
                row.Id,
                row.ConfigKey,
                row.ConfigValue,
                row.IsEnabled,
                row.UpdatedAt);
        }

        return table;
    }
}
