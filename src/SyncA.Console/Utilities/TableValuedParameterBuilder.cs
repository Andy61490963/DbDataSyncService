using System.Data;

namespace DbDataSyncService.SyncA.Utilities;

/// <summary>
/// 建立 TVP 所需的 DataTable。
/// </summary>
public static class TableValuedParameterBuilder
{
    public const string GuidListTypeName = "dbo.GuidList";

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
}
