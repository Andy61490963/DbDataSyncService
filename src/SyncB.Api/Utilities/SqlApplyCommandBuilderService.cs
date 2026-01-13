using System.Text;
using Dapper;
using DbDataSyncService.SyncB.Models;

namespace DbDataSyncService.SyncB.Utilities;

public static class SqlApplyCommandBuilder
{
    private const string DefaultPk = "ID";

    public sealed record BuiltSql(string Sql, DynamicParameters Parameters, int RowCount);

    public static BuiltSql Build(SyncApplyJsonRequest request)
    {
        var sb = new StringBuilder(8 * 1024);
        var parameters = new DynamicParameters();

        var totalRows = 0;

        foreach (var table in request.Tables)
        {
            var pk = DefaultPk;

            var deletes = table.Rows
                .Where(r => string.Equals(r.Op, "D", StringComparison.OrdinalIgnoreCase))
                .Select(r => GetGuidKey(r, pk))
                .ToList();

            var upserts = table.Rows
                .Where(r => string.Equals(r.Op, "U", StringComparison.OrdinalIgnoreCase))
                .ToList();

            totalRows += deletes.Count + upserts.Count;

            var schemaTable = QuoteTwoPart(table.Table);
            var tableToken = SanitizeToken(schemaTable.Replace(".", "_"));

            // 1) DELETE：一條就好
            if (deletes.Count > 0)
            {
                var pDelName = $"del_{tableToken}";
                sb.AppendLine($"DELETE FROM {schemaTable} WHERE {Q(pk)} IN @{pDelName};");
                parameters.Add(pDelName, deletes);
            }

            // 2) UPSERT：每筆 UPDATE + IF @@ROWCOUNT=0 INSERT
            for (var i = 0; i < upserts.Count; i++)
            {
                var row = upserts[i];
                var id = GetGuidKey(row, pk);

                // 你現在 data 是 Dictionary<string, string?>
                var data = row.Data is null
                    ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string?>(row.Data, StringComparer.OrdinalIgnoreCase);

                // 避免 PK 同時出現在 data
                data.Remove(pk);

                var pIdName = $"u_{i}_{SanitizeToken(pk)}";
                parameters.Add(pIdName, id);

                // 沒有欄位要更新：就確保存在（若不存在就 insert 只有 ID）
                if (data.Count == 0)
                {
                    sb.AppendLine($@"
IF NOT EXISTS (SELECT 1 FROM {schemaTable} WHERE {Q(pk)} = @{pIdName})
BEGIN
    INSERT INTO {schemaTable} ({Q(pk)}) VALUES (@{pIdName});
END
");
                    continue;
                }

                // UPDATE SET ...
                var setList = new List<string>(data.Count);
                var colList = new List<string>(data.Count + 1) { Q(pk) };
                var valList = new List<string>(data.Count + 1) { $"@{pIdName}" };

                foreach (var (col, val) in data)
                {
                    var pName = $"u_{i}_{SanitizeToken(col)}";

                    // ✅ 你要加的判斷：欄位名像 CreateAt / LastSyncAt / xxxTime 就當日期
                    var isDateCol = col.EndsWith("At", StringComparison.OrdinalIgnoreCase) ||
                                    col.EndsWith("Time", StringComparison.OrdinalIgnoreCase);

                    // ✅ 空字串變 NULL；日期用 TRY_CONVERT(datetime2, ..., 127) 吃 ISO
                    var valueExpr = isDateCol
                        ? $"TRY_CONVERT(datetime2, NULLIF(@{pName}, ''), 127)"
                        : $"NULLIF(@{pName}, '')";

                    // ✅ UPDATE 用 valueExpr（不是直接 @{pName}）
                    setList.Add($"{Q(col)} = {valueExpr}");

                    // ✅ INSERT 欄位跟值也用 valueExpr
                    colList.Add(Q(col));
                    valList.Add(valueExpr);

                    // 參數值仍然是字串（你現在就是 string?）
                    parameters.Add(pName, val);
                }

                sb.AppendLine($@"
UPDATE {schemaTable}
SET {string.Join(", ", setList)}
WHERE {Q(pk)} = @{pIdName};

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO {schemaTable} ({string.Join(", ", colList)})
    VALUES ({string.Join(", ", valList)});
END
");
            }
        }

        return new BuiltSql(sb.ToString(), parameters, totalRows);
    }

    private static Guid GetGuidKey(SyncRowPayload row, string pk)
    {
        // v 現在是 string?
        if (!row.Key.TryGetValue(pk, out var v) || string.IsNullOrWhiteSpace(v))
        {
            throw new InvalidOperationException($"缺少主鍵 {pk}");
        }

        if (Guid.TryParse(v, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"主鍵 {pk} 格式不正確：{v}");
    }

    private static string Q(string identifier)
        => $"[{identifier.Replace("]", "]]")}]";

    private static string QuoteTwoPart(string twoPart)
    {
        var parts = twoPart.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException($"Table 名稱必須是 schema.table：{twoPart}");
        }
        return $"{Q(parts[0])}.{Q(parts[1])}";
    }

    /// <summary>
    /// 讓 SQL 參數名稱只包含 [A-Za-z0-9_]
    /// （避免欄位名有空白、破折號、點號等造成參數名不合法）
    /// </summary>
    private static string SanitizeToken(string token)
    {
        var sb = new StringBuilder(token.Length);
        foreach (var ch in token)
        {
            sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }
        return sb.ToString();
    }
}
