using OmniTactica.AppCode.Data.Import;
using Microsoft.Data.Sqlite;

namespace OmniTactica.AppCode.Data.Database
{
    /// <summary>
    /// Core database service responsible for SQLite connection management.
    /// Does not contain game-specific logic - that belongs in repositories/services.
    /// </summary>
    public sealed class WahaSQLiteService
    {
        private readonly string _dbPath;

        public bool DatabaseExists => File.Exists(_dbPath);

        public WahaSQLiteService()
        {
            _dbPath = WahaDataImporter.GetDatabasePath();
        }

        internal SqliteConnection CreateConnection()
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Shared
            };

            return new SqliteConnection(builder.ToString());
        }

        internal SqliteConnection CreateWriteConnection()
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWrite,
                Cache = SqliteCacheMode.Shared
            };

            return new SqliteConnection(builder.ToString());
        }

        internal static SqliteConnection CreateWriteConnection(string dbPath)
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWrite,
                Cache = SqliteCacheMode.Shared
            };

            return new SqliteConnection(builder.ToString());
        }
    }
}