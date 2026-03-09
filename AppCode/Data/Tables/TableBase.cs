using OmniTactica.AppCode.Data.Database;
using Microsoft.Data.Sqlite;

namespace OmniTactica.AppCode.Data.Tables
{
    public abstract class TableBase
    {
        protected readonly WahaSQLiteService _db;

        protected TableBase(WahaSQLiteService db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

    protected async Task<List<T>> QueryListAsync<T>(
        string sql,
        Func<SqliteDataReader, T> map,
        params (string Name, object? Value)[] parameters)
    {
        var results = new List<T>();

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            results.Add(map(reader));

        return results;
    }

    protected async Task<T?> QuerySingleAsync<T>(
        string sql,
        Func<SqliteDataReader, T> map,
        params (string Name, object? Value)[] parameters)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
            return map(reader);

        return default;
    }

    protected static string S(SqliteDataReader r, string col)
        => r.IsDBNull(r.GetOrdinal(col)) ? "" : r.GetString(r.GetOrdinal(col));

    protected static int? I(SqliteDataReader r, string col)
        => r.IsDBNull(r.GetOrdinal(col)) ? null : r.GetInt32(r.GetOrdinal(col));
    }
}