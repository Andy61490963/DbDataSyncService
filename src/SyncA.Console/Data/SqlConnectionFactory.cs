using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace DbDataSyncService.SyncA.Data;

/// <summary>
/// 建立 SQL 連線，集中管理連線字串與注入。
/// </summary>
public interface ISqlConnectionFactory
{
    /// <summary>
    /// 取得資料庫連線，呼叫端負責 Dispose。
    /// </summary>
    SqlConnection CreateConnection();
}

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly IConfiguration _configuration;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public SqlConnection CreateConnection()
    {
        var connectionString = _configuration.GetConnectionString("AConnection");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return new SqlConnection(connectionString);
    }
}
