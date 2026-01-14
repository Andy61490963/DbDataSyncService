using System.Text;
using Dapper;
using DbDataSyncService.SyncB.Models;

namespace DbDataSyncService.SyncB.Utilities;

public static class SqlApplyCommandBuilder
{
    private const string DefaultPk = "ID";

    public sealed record BuiltSql(string Sql, DynamicParameters Parameters, int RowCount);

    public static IReadOnlyList<BuiltSql> BuildBatches(SyncApplyJsonRequest request, int maxParameters = 2000)
    {
        if (maxParameters <= 0 || maxParameters > 2090)
            throw new ArgumentOutOfRangeException(nameof(maxParameters), "建議設在 1500~2000，留 buffer 避免踩 2100 上限。");

        var result = new List<BuiltSql>(capacity: 8);

        // 當前 batch 容器
        var sb = new StringBuilder(16 * 1024);
        var parameters = new DynamicParameters();
        var paramCount = 0;
        var rowCount = 0;

        void Flush()
        {
            if (sb.Length == 0) return;
            result.Add(new BuiltSql(sb.ToString(), parameters, rowCount));

            sb = new StringBuilder(16 * 1024);
            parameters = new DynamicParameters();
            paramCount = 0;
            rowCount = 0;
        }

        foreach (var table in request.Tables)
        {
            var pk = DefaultPk;
            var schemaTable = QuoteTwoPart(table.Table);
            var tableToken = SanitizeToken(schemaTable.Replace(".", "_"));

            // -------------------------
            // 1) Deletes：Dapper IN @list 會展開參數
            //    => 必須 chunk（每個 Guid 都是 1 param）
            // -------------------------
            var deletes = table.Rows
                .Where(r => string.Equals(r.Op, "D", StringComparison.OrdinalIgnoreCase))
                .Select(r => GetGuidKey(r, pk))
                .ToList();

            if (deletes.Count > 0)
            {
                // 只要 list 長度可能很大，就拆
                const int hardMaxDeleteChunk = 1000; // 先保守，避免跟其他語句混到爆
                foreach (var chunk in Chunk(deletes, hardMaxDeleteChunk))
                {
                    // 如果這批剩餘空間不夠容納 chunk，就 flush 先送出
                    // 這裡 chunk 會佔用 chunk.Count 個參數
                    if (paramCount + chunk.Count > maxParameters)
                        Flush();

                    var pDelName = $"del_{tableToken}_{result.Count}_{rowCount}";
                    sb.AppendLine($"DELETE FROM {schemaTable} WHERE {Q(pk)} IN @{pDelName};");

                    parameters.Add(pDelName, chunk);
                    paramCount += chunk.Count;
                    rowCount += chunk.Count;
                }
            }

            // -------------------------
            // 2) Upserts：每 row 會吃 (1 + 欄位數) 個參數
            // -------------------------
            var upserts = table.Rows
                .Where(r => string.Equals(r.Op, "U", StringComparison.OrdinalIgnoreCase))
                .ToList();

            for (var i = 0; i < upserts.Count; i++)
            {
                var row = upserts[i];
                var id = GetGuidKey(row, pk);

                var data = row.Data is null
                    ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string?>(row.Data, StringComparer.OrdinalIgnoreCase);

                data.Remove(pk);

                // 這筆 row 會新增的參數數量：1(id) + data.Count(每欄位一個)
                var requiredParams = 1 + data.Count;

                // 如果單筆就超過 maxParameters（理論上很少見，但還是防）
                if (requiredParams > maxParameters)
                {
                    throw new InvalidOperationException(
                        $"單筆資料欄位過多，導致參數數量({requiredParams})超過 maxParameters({maxParameters})。Table={table.Table} PK={id}");
                }

                // 不夠放就 flush
                if (paramCount + requiredParams > maxParameters)
                    Flush();

                var batchIndex = result.Count; // 用當下已 flush 的批數當 batch id
                var pIdName = $"u_{batchIndex}_{i}_{SanitizeToken(pk)}";
                parameters.Add(pIdName, id);
                paramCount += 1;

                // 沒有欄位要更新：確保存在
                if (data.Count == 0)
                {
                    sb.AppendLine($@"
IF NOT EXISTS (SELECT 1 FROM {schemaTable} WHERE {Q(pk)} = @{pIdName})
BEGIN
    INSERT INTO {schemaTable} ({Q(pk)}) VALUES (@{pIdName});
END
");
                    rowCount += 1;
                    continue;
                }

                var setList = new List<string>(data.Count);
                var colList = new List<string>(data.Count + 1) { Q(pk) };
                var valList = new List<string>(data.Count + 1) { $"@{pIdName}" };

                foreach (var (col, val) in data)
                {
                    var pName = $"u_{batchIndex}_{i}_{SanitizeToken(col)}";

                    var isDateCol = col.EndsWith("At", StringComparison.OrdinalIgnoreCase) ||
                                    col.EndsWith("Time", StringComparison.OrdinalIgnoreCase);

                    var valueExpr = isDateCol
                        ? $"TRY_CONVERT(datetime2, NULLIF(@{pName}, ''), 127)"
                        : $"NULLIF(@{pName}, '')";

                    setList.Add($"{Q(col)} = {valueExpr}");
                    colList.Add(Q(col));
                    valList.Add(valueExpr);

                    parameters.Add(pName, val);
                    paramCount += 1;
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

                rowCount += 1;
            }
        }

        Flush();
        return result;
    }

    private static Guid GetGuidKey(SyncRowPayload row, string pk)
    {
        if (!row.Key.TryGetValue(pk, out var v) || string.IsNullOrWhiteSpace(v))
            throw new InvalidOperationException($"缺少主鍵 {pk}");

        if (Guid.TryParse(v, out var parsed))
            return parsed;

        throw new InvalidOperationException($"主鍵 {pk} 格式不正確：{v}");
    }

    private static string Q(string identifier)
        => $"[{identifier.Replace("]", "]]")}]";

    private static string QuoteTwoPart(string twoPart)
    {
        var parts = twoPart.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            throw new InvalidOperationException($"Table 名稱必須是 schema.table：{twoPart}");
        return $"{Q(parts[0])}.{Q(parts[1])}";
    }

    private static string SanitizeToken(string token)
    {
        var sb = new StringBuilder(token.Length);
        foreach (var ch in token)
            sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        return sb.ToString();
    }

    private static IEnumerable<List<T>> Chunk<T>(List<T> source, int chunkSize)
    {
        if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));
        for (var i = 0; i < source.Count; i += chunkSize)
        {
            var size = Math.Min(chunkSize, source.Count - i);
            yield return source.GetRange(i, size);
        }
    }
}
